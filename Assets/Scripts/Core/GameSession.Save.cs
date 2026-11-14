using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using SoccerClub.Competition;
using SoccerClub.Data;
using SoccerClub.Economy;
using SoccerClub.Facilities;
using SoccerClub.Match;
using SoccerClub.Personnel;
using SoccerClub.Time;

namespace SoccerClub.Core
{
    public sealed partial class GameSession
    {
        public GameSaveFile CaptureSave()
        {
            if (PlayerClub == null || string.IsNullOrEmpty(PlayerClub.Id) || Clubs == null) return null;
            var clubs = new ClubSave[Clubs.Count];
            for (int i = 0; i < Clubs.Count; i++)
            {
                clubs[i] = WriteClub(Clubs[i]);
                if (clubs[i] == null) return null;
            }

            var cup = Cup.CaptureSnapshot();
            var leagueFixtures = WriteFixtures(League.Fixtures);
            var cupFixtures = WriteFixtures(cup.Fixtures);
            var ties = WriteTies(cup);
            var later = WriteLater(cup);
            var round2Ids = WriteIds(cup.Round2Seeds);
            var round1Ids = WriteIds(cup.Round1Winners);
            var pending = WritePending(cup);
            if (leagueFixtures == null || cupFixtures == null || ties == null || later == null || round2Ids == null || round1Ids == null || pending == null)
                return null;

            var file = new GameSaveFile
            {
                Version = GameSaveFile.CurrentVersion,
                RandomState = _random.State.ToString(CultureInfo.InvariantCulture),
                DateTicks = Time.CurrentDate.Ticks,
                GameOver = GameOver,
                LeagueLevel = LeagueLevel,
                DroughtSeasons = DroughtSeasons,
                TitleStreak = TitleStreak,
                PlayerClubId = PlayerClub.Id,
                Clubs = clubs,
                FrontMarket = WriteStaffList(FrontMarket),
                LeagueFixtures = leagueFixtures,
                CupFixtures = cupFixtures,
                CupTies = ties,
                HasRound2Seeds = cup.HasRound2Seeds,
                Round2SeedIds = round2Ids,
                Round2DateTicks = cup.Round2Date.Ticks,
                Round1WinnerIds = round1Ids,
                LaterRounds = later,
                PendingCupTies = pending,
                MonthLedger = WriteLedger(_month),
                PendingMonthReport = WriteLedger(_pendingReport),
                PendingSponsor = WriteOffer(_pendingOffer),
                PendingSponsorCut = WriteCut(_pendingCut)
            };
            WritePersonnel(file);
            WriteSupport(file);
            file.Relationships = _relations.Write();
            return file;
        }

        public static GameSession RestoreSave(GameSaveFile file)
        {
            if (file == null || file.Version != GameSaveFile.CurrentVersion || file.DateTicks <= 0) return null;
            ulong state;
            if (!ulong.TryParse(file.RandomState, NumberStyles.Integer, CultureInfo.InvariantCulture, out state))
                return null;
            if (file.Clubs == null || file.Clubs.Length != LeagueService.TeamCount) return null;

            var clubs = new List<Club>(file.Clubs.Length);
            var byId = new Dictionary<string, Club>();
            for (int i = 0; i < file.Clubs.Length; i++)
            {
                var club = ReadClub(file.Clubs[i]);
                if (club == null || string.IsNullOrEmpty(club.Id) || byId.ContainsKey(club.Id)) return null;
                club.IsPlayerOwned = club.Id == file.PlayerClubId;
                clubs.Add(club);
                byId.Add(club.Id, club);
            }

            Club player;
            if (string.IsNullOrEmpty(file.PlayerClubId) || !byId.TryGetValue(file.PlayerClubId, out player))
                return null;

            var leagueFixtures = ReadFixtures(file.LeagueFixtures, byId);
            if (leagueFixtures == null || leagueFixtures.Count == 0) return null;
            var seasonAnchor = SeasonStart;
            if (LeagueService.TryInferSeasonStart(leagueFixtures, out var inferred))
                seasonAnchor = inferred;
            LeagueService.AlignToWinterBreak(leagueFixtures, seasonAnchor);
            var cupFixtures = ReadFixtures(file.CupFixtures, byId);
            if (cupFixtures == null || cupFixtures.Count == 0) return null;
            var cupState = ReadCup(file, byId, cupFixtures);
            if (cupState == null) return null;

            var random = GameRandom.FromState(state);
            var economy = new EconomyManager();
            var match = new MatchSimulationManager(new ManagerLineupAi(), random);
            var league = new LeagueService(clubs);
            league.ReplaceFixtures(leagueFixtures);
            var cup = new CupService(match, random);
            cup.ApplySnapshot(cupState);
            var session = new GameSession(
                new TimeManager(new DateTime(file.DateTicks)),
                economy,
                new FacilityService(economy),
                new SeasonCalendar(league, cup, match, economy),
                league,
                cup,
                match,
                new ScoutingService(),
                new GeneralManagerAi(economy),
                new FrontOfficeHiring(economy),
                new AcademyService(economy),
                clubs,
                player,
                random);
            session.GameOver = file.GameOver;
            session.LeagueLevel = file.LeagueLevel < 1 ? 1 : file.LeagueLevel;
            session.DroughtSeasons = file.DroughtSeasons < 0 ? 0 : file.DroughtSeasons;
            session.TitleStreak = file.TitleStreak < 0 ? 0 : file.TitleStreak;
            session.ReadMonthBooks(file.MonthLedger, file.PendingMonthReport);
            session._pendingOffer = ReadOffer(file.PendingSponsor);
            if (session._pendingOffer != null)
                session._pendingOffer.ContractCap = ClubMath.SponsorContractCapFor(session.LeagueLevel);
            session._pendingCut = ReadCut(file.PendingSponsorCut);
            // 無所属のキャリアやり直し（ReadPersonnel 内）で関係を消せるよう、先に読む。
            session._relations.Read(file.Relationships);
            session.ReadPersonnel(file);
            session.ReadSupport(file);
            var market = ReadStaffList(file.FrontMarket);
            for (int i = 0; i < market.Count; i++)
            {
                if (market[i] != null) session._frontMarket.Add(market[i]);
            }

            // 古いセーブのミニイベントの結婚は、夫婦の関係に合わせて外す。
            session._relations.SyncMarried(session.AllPlayers());
            return session;
        }

        void ReadMonthBooks(MonthLedgerSave running, MonthLedgerSave pending)
        {
            ApplyLedger(_month, running);
            _pendingReport = pending != null && pending.Year > 0 ? new MonthLedger() : null;
            if (_pendingReport != null) ApplyLedger(_pendingReport, pending);
        }

        static SponsorSave[] WriteSponsors(List<SponsorContract> sponsors)
        {
            int count = sponsors == null ? 0 : sponsors.Count;
            var result = new List<SponsorSave>(count);
            for (int i = 0; i < count; i++)
            {
                var contract = sponsors[i];
                if (contract == null || contract.MonthsLeft <= 0) continue;
                result.Add(new SponsorSave
                {
                    Name = contract.Name ?? "",
                    Monthly = Money(contract.Monthly),
                    MonthsLeft = contract.MonthsLeft,
                    CutRank = contract.CutRank,
                    CutLosses = contract.CutLosses
                });
            }

            return result.ToArray();
        }

        static List<SponsorContract> ReadSponsors(SponsorSave[] saves)
        {
            var list = new List<SponsorContract>();
            if (saves == null) return list;
            for (int i = 0; i < saves.Length; i++)
            {
                var save = saves[i];
                if (save == null || save.MonthsLeft <= 0 || string.IsNullOrEmpty(save.Name)) continue;
                list.Add(new SponsorContract
                {
                    Name = save.Name,
                    Monthly = ReadMoney(save.Monthly),
                    MonthsLeft = save.MonthsLeft,
                    CutRank = save.CutRank,
                    CutLosses = save.CutLosses
                });
            }

            return list;
        }

        static SponsorOfferSave WriteOffer(SponsorOffer offer)
        {
            if (offer == null || string.IsNullOrEmpty(offer.SponsorName)) return null;
            return new SponsorOfferSave
            {
                SponsorName = offer.SponsorName,
                StaffName = offer.StaffName ?? "",
                Monthly = Money(offer.Monthly),
                Upfront = Money(offer.Upfront),
                Months = offer.Months,
                ActiveContracts = offer.ActiveContracts,
                CutRank = offer.CutRank,
                CutLosses = offer.CutLosses
            };
        }

        static SponsorCutSave WriteCut(SponsorCutNotice notice)
        {
            if (notice == null || notice.Contracts == null || notice.Contracts.Count == 0) return null;
            var lines = new SponsorCutLineSave[notice.Contracts.Count];
            for (int i = 0; i < notice.Contracts.Count; i++)
            {
                var contract = notice.Contracts[i];
                lines[i] = new SponsorCutLineSave
                {
                    Name = contract == null ? "" : contract.Name ?? "",
                    CutRank = contract == null ? 0 : contract.CutRank,
                    CutLosses = contract == null ? 0 : contract.CutLosses
                };
            }

            return new SponsorCutSave { Lines = lines };
        }

        static SponsorCutNotice ReadCut(SponsorCutSave save)
        {
            if (save == null || save.Lines == null || save.Lines.Length == 0) return null;
            var notice = new SponsorCutNotice();
            for (int i = 0; i < save.Lines.Length; i++)
            {
                var line = save.Lines[i];
                if (line == null || string.IsNullOrEmpty(line.Name)) continue;
                notice.Contracts.Add(new SponsorContract
                {
                    Name = line.Name,
                    CutRank = line.CutRank,
                    CutLosses = line.CutLosses
                });
            }

            return notice.Contracts.Count == 0 ? null : notice;
        }

        static SponsorOffer ReadOffer(SponsorOfferSave save)
        {
            if (save == null || string.IsNullOrEmpty(save.SponsorName) || save.Months <= 0) return null;
            return new SponsorOffer
            {
                SponsorName = save.SponsorName,
                StaffName = save.StaffName ?? "",
                Monthly = ReadMoney(save.Monthly),
                Upfront = ReadMoney(save.Upfront),
                Months = save.Months,
                ActiveContracts = save.ActiveContracts,
                CutRank = save.CutRank,
                CutLosses = save.CutLosses
            };
        }

        static MonthLedgerSave WriteLedger(MonthLedger ledger)
        {
            if (ledger == null) return null;
            return new MonthLedgerSave
            {
                Year = ledger.Year,
                Month = ledger.Month,
                Sponsor = Money(ledger.Sponsor),
                Goods = Money(ledger.Goods),
                Gate = Money(ledger.Gate),
                Broadcast = Money(ledger.Broadcast),
                Prize = Money(ledger.Prize),
                Operating = Money(ledger.Operating),
                Advertising = Money(ledger.Advertising),
                Development = Money(ledger.Development),
                Facilities = Money(ledger.Facilities),
                FrontWages = Money(ledger.FrontWages),
                SquadWages = Money(ledger.SquadWages),
                Reinforcement = Money(ledger.Reinforcement),
                Coins = Money(ledger.Coins)
            };
        }

        static void ApplyLedger(MonthLedger ledger, MonthLedgerSave save)
        {
            if (ledger == null || save == null) return;
            ledger.Year = save.Year;
            ledger.Month = save.Month;
            ledger.Sponsor = ReadMoney(save.Sponsor);
            ledger.Goods = ReadMoney(save.Goods);
            ledger.Gate = ReadMoney(save.Gate);
            ledger.Broadcast = ReadMoney(save.Broadcast);
            ledger.Prize = ReadMoney(save.Prize);
            ledger.Operating = ReadMoney(save.Operating);
            ledger.Advertising = ReadMoney(save.Advertising);
            ledger.Development = ReadMoney(save.Development);
            ledger.Facilities = ReadMoney(save.Facilities);
            ledger.FrontWages = ReadMoney(save.FrontWages);
            ledger.SquadWages = ReadMoney(save.SquadWages);
            ledger.Reinforcement = ReadMoney(save.Reinforcement);
            ledger.Coins = ReadMoney(save.Coins);
        }

        static ClubSave WriteClub(Club club)
        {
            if (club == null || club.Front == null || club.Team == null || club.Facilities == null) return null;
            return new ClubSave
            {
                Id = club.Id,
                Name = club.Name,
                EmblemIndex = club.EmblemIndex,
                NumberTextIsWhite = club.NumberTextIsWhite,
                Coins = Money(club.Coins),
                Reinforcement = Money(club.Reinforcement),
                Advertising = Money(club.Advertising),
                TicketPrice = club.TicketPrice,
                Operating = Money(club.Operating),
                CoreSupporters = club.CoreSupporters,
                LightSupporters = club.LightSupporters,
                DevelopmentPaid = club.DevelopmentPaid,
                MatchesSinceWindow = club.MatchesSinceWindow,
                PreviousLeagueRank = club.PreviousLeagueRank,
                LeaguePoints = club.LeaguePoints,
                GoalsFor = club.GoalsFor,
                GoalsAgainst = club.GoalsAgainst,
                Wins = club.Wins,
                Draws = club.Draws,
                Losses = club.Losses,
                LosingStreak = club.LosingStreak,
                UnpaidMonths = club.UnpaidMonths,
                Sponsors = WriteSponsors(club.Sponsors),
                Squad = WritePlayers(club.Squad),
                Academy = WritePlayers(club.Academy),
                Secretary = WriteStaff(club.Front.Secretary),
                GeneralManager = WriteStaff(club.Front.GeneralManager),
                Strengthening = WriteSlots(club.Front.Strengthening),
                Sales = WriteSlots(club.Front.Sales),
                PublicRelations = WriteSlots(club.Front.PublicRelations),
                Business = WriteSlots(club.Front.Business),
                AcademyStaff = WriteSlots(club.Front.Academy),
                Manager = WriteStaff(club.Team.Manager),
                HeadCoach = WriteStaff(club.Team.HeadCoach),
                Coaches = WriteSlots(club.Team.Coaches),
                PhysicalCoach = WriteStaff(club.Team.PhysicalCoach),
                Trainers = WriteSlots(club.Team.Trainers),
                Doctor = WriteStaff(club.Team.Doctor),
                Stadium = WriteFacility(club.Facilities.Stadium),
                Transport = WriteFacility(club.Facilities.Transport),
                TrainingGround = WriteFacility(club.Facilities.TrainingGround),
                TrainingFacility = WriteFacility(club.Facilities.TrainingFacility),
                Clubhouse = WriteFacility(club.Facilities.Clubhouse)
            };
        }

        static Club ReadClub(ClubSave save)
        {
            if (save == null || string.IsNullOrEmpty(save.Id)) return null;
            var club = new Club
            {
                Id = save.Id,
                Name = save.Name ?? "",
                EmblemIndex = save.EmblemIndex,
                NumberTextIsWhite = save.NumberTextIsWhite,
                Coins = ReadMoney(save.Coins),
                Reinforcement = ReadMoney(save.Reinforcement),
                Advertising = ReadMoney(save.Advertising),
                TicketPrice = save.TicketPrice,
                Operating = ReadMoney(save.Operating),
                CoreSupporters = save.CoreSupporters,
                LightSupporters = save.LightSupporters,
                DevelopmentPaid = save.DevelopmentPaid,
                MatchesSinceWindow = save.MatchesSinceWindow,
                PreviousLeagueRank = save.PreviousLeagueRank,
                LeaguePoints = save.LeaguePoints,
                GoalsFor = save.GoalsFor,
                GoalsAgainst = save.GoalsAgainst,
                Wins = save.Wins,
                Draws = save.Draws,
                Losses = save.Losses,
                LosingStreak = save.LosingStreak,
                UnpaidMonths = save.UnpaidMonths,
                Sponsors = ReadSponsors(save.Sponsors),
                Squad = ReadPlayers(save.Squad),
                Academy = ReadPlayers(save.Academy),
                Front = new FrontOffice
                {
                    Secretary = ReadStaff(save.Secretary),
                    GeneralManager = ReadStaff(save.GeneralManager),
                    Strengthening = ReadSlots(save.Strengthening),
                    Sales = ReadSlots(save.Sales),
                    PublicRelations = ReadSlots(save.PublicRelations),
                    Business = ReadSlots(save.Business),
                    Academy = ReadSlots(save.AcademyStaff)
                },
                Team = new TeamStaff
                {
                    Manager = ReadStaff(save.Manager),
                    HeadCoach = ReadStaff(save.HeadCoach),
                    Coaches = ReadSlots(save.Coaches),
                    PhysicalCoach = ReadStaff(save.PhysicalCoach),
                    Trainers = ReadSlots(save.Trainers),
                    Doctor = ReadStaff(save.Doctor)
                },
                Facilities = new ClubFacilities
                {
                    Stadium = ReadFacility(save.Stadium, FacilityKind.Stadium),
                    Transport = ReadFacility(save.Transport, FacilityKind.Transport),
                    TrainingGround = ReadFacility(save.TrainingGround, FacilityKind.TrainingGround),
                    TrainingFacility = ReadFacility(save.TrainingFacility, FacilityKind.TrainingFacility),
                    Clubhouse = ReadFacility(save.Clubhouse, FacilityKind.Clubhouse)
                }
            };
            if (club.Advertising > 0)
            {
                club.Coins += club.Advertising;
                club.Advertising = 0;
            }

            if (club.TicketPrice <= 0)
                club.TicketPrice = ClubMath.QuoteTicket(club);

            return club;
        }

        static PlayerSave[] WritePlayers(List<Player> players)
        {
            int count = players == null ? 0 : players.Count;
            var result = new List<PlayerSave>(count);
            for (int i = 0; i < count; i++)
            {
                var player = players[i];
                if (player == null) continue;
                var suits = new int[13];
                for (int s = 0; s < suits.Length; s++)
                    suits[s] = player.Suitabilities != null && s < player.Suitabilities.Length ? (int)player.Suitabilities[s] : 0;
                result.Add(new PlayerSave
                {
                    Id = player.Id,
                    Name = player.Name,
                    Age = player.Age,
                    Phase = (int)player.Phase,
                    Rarity = (int)player.Rarity,
                    Style = (int)player.Style,
                    PreferredPosition = (int)player.PreferredPosition,
                    Offense = DoubleText(player.Offense),
                    Defense = DoubleText(player.Defense),
                    StaminaMax = DoubleText(player.StaminaMax),
                    StaminaCurrent = DoubleText(player.StaminaCurrent),
                    Condition = player.Condition,
                    InjuryDays = player.InjuryDays,
                    Appearances = player.Appearances,
                    SeasonGoals = player.SeasonGoals,
                    SeasonRatingSum = DoubleText(player.SeasonRatingSum),
                    SeasonRatedMatches = player.SeasonRatedMatches,
                    LastPlayedTicks = player.LastPlayedDate.Ticks,
                    LastGoalTicks = player.LastGoalDate.Ticks,
                    Married = player.Married,
                    Children = player.Children,
                    Suitabilities = suits,
                    ContractSalary = Money(player.ContractSalary),
                    Gender = (int)player.Gender + 1
                });
            }

            return result.ToArray();
        }

        static List<Player> ReadPlayers(PlayerSave[] saves)
        {
            var players = new List<Player>();
            int count = saves == null ? 0 : saves.Length;
            for (int i = 0; i < count; i++)
            {
                var save = saves[i];
                if (save == null || string.IsNullOrEmpty(save.Id)) continue;
                var suits = new Suitability[13];
                int n = save.Suitabilities == null ? 0 : save.Suitabilities.Length;
                for (int s = 0; s < suits.Length && s < n; s++)
                    suits[s] = (Suitability)save.Suitabilities[s];
                players.Add(new Player
                {
                    Id = save.Id,
                    Name = NameCatalog.Contains(save.Name) ? save.Name : NameCatalog.ForId(save.Id),
                    Gender = save.Gender == 1 || save.Gender == 2 ? (Gender)(save.Gender - 1) : PlayerTraits.GenderForId(save.Id),
                    Age = save.Age,
                    Phase = (GrowthPhase)save.Phase,
                    Rarity = (Rarity)save.Rarity,
                    Style = (PlayingStyle)save.Style,
                    PreferredPosition = (Position)save.PreferredPosition,
                    Offense = ReadDouble(save.Offense),
                    Defense = ReadDouble(save.Defense),
                    StaminaMax = ReadDouble(save.StaminaMax),
                    StaminaCurrent = ReadDouble(save.StaminaCurrent),
                    Condition = save.Condition,
                    InjuryDays = save.InjuryDays,
                    Appearances = save.Appearances,
                    SeasonGoals = save.SeasonGoals,
                    SeasonRatingSum = ReadDouble(save.SeasonRatingSum),
                    SeasonRatedMatches = save.SeasonRatedMatches,
                    LastPlayedDate = save.LastPlayedTicks == 0 ? default(DateTime) : new DateTime(save.LastPlayedTicks),
                    LastGoalDate = save.LastGoalTicks == 0 ? default(DateTime) : new DateTime(save.LastGoalTicks),
                    Married = save.Married,
                    Children = save.Children,
                    Suitabilities = suits,
                    ContractSalary = ReadMoney(save.ContractSalary)
                });
            }

            return players;
        }

        static StaffSave[] WriteStaffList(IReadOnlyList<StaffMember> staff)
        {
            int count = staff == null ? 0 : staff.Count;
            var result = new StaffSave[count];
            for (int i = 0; i < count; i++)
                result[i] = WriteStaff(staff[i]);
            return result;
        }

        static List<StaffMember> ReadStaffList(StaffSave[] saves)
        {
            var staff = new List<StaffMember>();
            int count = saves == null ? 0 : saves.Length;
            for (int i = 0; i < count; i++)
            {
                var member = ReadStaff(saves[i]);
                if (member != null) staff.Add(member);
            }

            return staff;
        }

        static StaffSave[] WriteSlots(StaffMember[] slots)
        {
            int count = slots == null ? 0 : slots.Length;
            var result = new StaffSave[count];
            for (int i = 0; i < count; i++)
                result[i] = WriteStaff(slots[i]);
            return result;
        }

        static StaffMember[] ReadSlots(StaffSave[] saves)
        {
            int count = saves == null ? 0 : saves.Length;
            var slots = new StaffMember[count];
            for (int i = 0; i < count; i++)
                slots[i] = ReadStaff(saves[i]);
            return slots;
        }

        static StaffSave WriteStaff(StaffMember staff)
        {
            if (staff == null) return new StaffSave();
            return new StaffSave
            {
                Has = true,
                Id = staff.Id,
                Name = staff.Name,
                Age = staff.Age,
                Level = staff.Level,
                IsFrontOffice = staff.IsFrontOffice,
                FrontRole = (int)staff.FrontRole,
                TeamRole = (int)staff.TeamRole,
                Policy = (int)staff.Policy,
                FavoriteFormation = (int)staff.FavoriteFormation,
                FavoriteStyle = (int)staff.FavoriteStyle,
                LineupPriority = (int)staff.LineupPriority,
                ScoutedGroup = (int)staff.ScoutedGroup,
                Married = staff.Married,
                Children = staff.Children,
                JoinedTicks = staff.JoinedTicks
            };
        }

        static StaffMember ReadStaff(StaffSave save)
        {
            if (save == null || !save.Has || string.IsNullOrEmpty(save.Id)) return null;
            return new StaffMember
            {
                Id = save.Id,
                Name = NameCatalog.Contains(save.Name) ? save.Name : NameCatalog.ForId(save.Id),
                Age = save.Age,
                Married = save.Married,
                Children = save.Children,
                Level = save.Level,
                IsFrontOffice = save.IsFrontOffice,
                FrontRole = (FrontRole)save.FrontRole,
                TeamRole = (TeamStaffRole)save.TeamRole,
                Policy = (ReinforcementPolicy)save.Policy,
                FavoriteFormation = FormationCatalog.Clamp((FormationType)save.FavoriteFormation),
                FavoriteStyle = (PlayingStyle)save.FavoriteStyle,
                LineupPriority = (LineupPriority)save.LineupPriority,
                ScoutedGroup = (PositionGroup)save.ScoutedGroup,
                JoinedTicks = save.JoinedTicks
            };
        }

        static FacilitySave WriteFacility(Facility facility)
        {
            if (facility == null) return new FacilitySave { Level = 1, BaseCost = "0" };
            return new FacilitySave
            {
                Level = facility.Level,
                BaseCost = Money(facility.BaseCost)
            };
        }

        static Facility ReadFacility(FacilitySave save, FacilityKind kind)
        {
            int level = save == null ? 1 : save.Level;
            if (level < 1) level = 1;
            if (level > Facility.MaxLevel) level = Facility.MaxLevel;
            return new Facility
            {
                Kind = kind,
                Level = level,
                BaseCost = save == null ? BigInteger.Zero : ReadMoney(save.BaseCost)
            };
        }

        static FixtureSave[] WriteFixtures(IReadOnlyList<Fixture> fixtures)
        {
            int count = fixtures == null ? 0 : fixtures.Count;
            var result = new FixtureSave[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = WriteFixture(fixtures[i]);
                if (result[i] == null) return null;
            }

            return result;
        }

        static FixtureSave WriteFixture(Fixture fixture)
        {
            if (fixture == null || fixture.Home == null || fixture.Away == null) return null;
            if (string.IsNullOrEmpty(fixture.Home.Id) || string.IsNullOrEmpty(fixture.Away.Id)) return null;
            var save = new FixtureSave
            {
                Id = fixture.Id,
                DateTicks = fixture.Date.Ticks,
                HomeId = fixture.Home.Id,
                AwayId = fixture.Away.Id,
                IsLeague = fixture.IsLeague,
                IsCup = fixture.IsCup,
                CupRound = fixture.CupRound,
                Leg = fixture.Leg,
                ConsequencesApplied = fixture.ConsequencesApplied,
                GateIncome = Money(fixture.GateIncome),
                AdvertisingSpent = Money(fixture.AdvertisingSpent),
                OperatingPaid = Money(fixture.OperatingPaid),
                OperatingRequired = Money(fixture.OperatingRequired),
                HomeAppearancePaid = Money(fixture.HomeAppearancePaid),
                HomeWinPaid = Money(fixture.HomeWinPaid),
                AwayAppearancePaid = Money(fixture.AwayAppearancePaid),
                AwayWinPaid = Money(fixture.AwayWinPaid),
                Attendance = fixture.Attendance,
                StadiumCapacity = fixture.StadiumCapacity
            };
            if (fixture.Result != null)
            {
                save.HasResult = true;
                save.HomeGoals = fixture.Result.HomeGoals;
                save.AwayGoals = fixture.Result.AwayGoals;
                save.DecidedByPenalty = fixture.Result.DecidedByPenalty;
            }

            return save;
        }

        static List<Fixture> ReadFixtures(FixtureSave[] saves, Dictionary<string, Club> clubs)
        {
            var fixtures = new List<Fixture>();
            int count = saves == null ? 0 : saves.Length;
            for (int i = 0; i < count; i++)
            {
                var save = saves[i];
                if (save == null) return null;
                Club home;
                Club away;
                if (!clubs.TryGetValue(save.HomeId ?? "", out home) || !clubs.TryGetValue(save.AwayId ?? "", out away))
                    return null;
                var fixture = new Fixture
                {
                    Id = save.Id,
                    Date = new DateTime(save.DateTicks),
                    Home = home,
                    Away = away,
                    IsLeague = save.IsLeague,
                    IsCup = save.IsCup,
                    CupRound = save.CupRound,
                    Leg = save.Leg,
                    ConsequencesApplied = save.ConsequencesApplied,
                    GateIncome = ReadMoney(save.GateIncome),
                    AdvertisingSpent = ReadMoney(save.AdvertisingSpent),
                    OperatingPaid = ReadMoney(save.OperatingPaid),
                    OperatingRequired = ReadMoney(save.OperatingRequired),
                    HomeAppearancePaid = ReadMoney(save.HomeAppearancePaid),
                    HomeWinPaid = ReadMoney(save.HomeWinPaid),
                    AwayAppearancePaid = ReadMoney(save.AwayAppearancePaid),
                    AwayWinPaid = ReadMoney(save.AwayWinPaid),
                    Attendance = save.Attendance,
                    StadiumCapacity = save.StadiumCapacity
                };
                if (save.HasResult)
                {
                    fixture.Result = new MatchResult
                    {
                        Home = home,
                        Away = away,
                        HomeGoals = save.HomeGoals,
                        AwayGoals = save.AwayGoals,
                        DecidedByPenalty = save.DecidedByPenalty
                    };
                }

                fixtures.Add(fixture);
            }

            return fixtures;
        }

        static CupTieSave[] WriteTies(CupSnapshot cup)
        {
            int count = cup.TieIds.Count;
            if (cup.Ties.Count < count) return null;
            var ties = new CupTieSave[count];
            for (int i = 0; i < count; i++)
            {
                var tie = cup.Ties[i];
                if (tie == null || tie.FirstHome == null || tie.FirstAway == null) return null;
                if (string.IsNullOrEmpty(tie.FirstHome.Id) || string.IsNullOrEmpty(tie.FirstAway.Id)) return null;
                ties[i] = new CupTieSave
                {
                    Id = cup.TieIds[i],
                    FirstHomeId = tie.FirstHome.Id,
                    FirstAwayId = tie.FirstAway.Id,
                    FirstHomeGoals = tie.FirstHomeGoals,
                    FirstAwayGoals = tie.FirstAwayGoals,
                    SecondHomeGoals = tie.SecondHomeGoals,
                    SecondAwayGoals = tie.SecondAwayGoals,
                    Complete = tie.Complete
                };
            }

            return ties;
        }

        static LaterRoundSave[] WriteLater(CupSnapshot cup)
        {
            int count = cup.LaterRounds.Count;
            if (cup.LaterWinners.Count < count) return null;
            var rounds = new LaterRoundSave[count];
            for (int i = 0; i < count; i++)
            {
                var ids = WriteIds(cup.LaterWinners[i]);
                if (ids == null) return null;
                rounds[i] = new LaterRoundSave { Round = cup.LaterRounds[i], ClubIds = ids };
            }

            return rounds;
        }

        static PendingCupTieSave[] WritePending(CupSnapshot cup)
        {
            int count = cup.OpenTies == null ? 0 : cup.OpenTies.Count;
            var result = new PendingCupTieSave[count];
            for (int i = 0; i < count; i++)
            {
                var tie = cup.OpenTies[i];
                string homeId;
                string homeFeeder;
                string awayId;
                string awayFeeder;
                if (tie == null || string.IsNullOrEmpty(tie.TieId)) return null;
                if (!WriteSlot(tie.Home, tie.HomeFeeder, out homeId, out homeFeeder)) return null;
                if (!WriteSlot(tie.Away, tie.AwayFeeder, out awayId, out awayFeeder)) return null;
                result[i] = new PendingCupTieSave
                {
                    TieId = tie.TieId,
                    Round = tie.Round,
                    FirstLegTicks = tie.FirstLeg.Ticks,
                    HomeId = homeId,
                    HomeFeeder = homeFeeder,
                    AwayId = awayId,
                    AwayFeeder = awayFeeder
                };
            }

            return result;
        }

        static bool WriteSlot(Club club, string feeder, out string id, out string feed)
        {
            if (club != null && !string.IsNullOrEmpty(club.Id))
            {
                id = club.Id;
                feed = "";
                return true;
            }

            id = "";
            feed = feeder ?? "";
            return feed.Length > 0;
        }

        static string[] WriteIds(List<Club> clubs)
        {
            int count = clubs == null ? 0 : clubs.Count;
            var ids = new string[count];
            for (int i = 0; i < count; i++)
            {
                if (clubs[i] == null || string.IsNullOrEmpty(clubs[i].Id)) return null;
                ids[i] = clubs[i].Id;
            }

            return ids;
        }

        static CupSnapshot ReadCup(GameSaveFile file, Dictionary<string, Club> clubs, List<Fixture> fixtures)
        {
            var snap = new CupSnapshot();
            snap.Fixtures.AddRange(fixtures);
            int ties = file.CupTies == null ? 0 : file.CupTies.Length;
            for (int i = 0; i < ties; i++)
            {
                var save = file.CupTies[i];
                if (save == null || string.IsNullOrEmpty(save.Id)) return null;
                Club home;
                Club away;
                if (!clubs.TryGetValue(save.FirstHomeId ?? "", out home) || !clubs.TryGetValue(save.FirstAwayId ?? "", out away))
                    return null;
                snap.TieIds.Add(save.Id);
                snap.Ties.Add(new CupTie
                {
                    FirstHome = home,
                    FirstAway = away,
                    FirstHomeGoals = save.FirstHomeGoals,
                    FirstAwayGoals = save.FirstAwayGoals,
                    SecondHomeGoals = save.SecondHomeGoals,
                    SecondAwayGoals = save.SecondAwayGoals,
                    Complete = save.Complete
                });
            }

            snap.HasRound2Seeds = file.HasRound2Seeds;
            if (file.HasRound2Seeds && !AddClubs(file.Round2SeedIds, clubs, snap.Round2Seeds)) return null;
            snap.Round2Date = new DateTime(file.Round2DateTicks);
            if (!AddClubs(file.Round1WinnerIds, clubs, snap.Round1Winners)) return null;
            int rounds = file.LaterRounds == null ? 0 : file.LaterRounds.Length;
            for (int i = 0; i < rounds; i++)
            {
                var round = file.LaterRounds[i];
                if (round == null) return null;
                var winners = new List<Club>();
                if (!AddClubs(round.ClubIds, clubs, winners)) return null;
                snap.LaterRounds.Add(round.Round);
                snap.LaterWinners.Add(winners);
            }

            if (!ReadPending(file.PendingCupTies, clubs, snap)) return null;
            return snap;
        }

        static bool ReadPending(PendingCupTieSave[] pending, Dictionary<string, Club> clubs, CupSnapshot snap)
        {
            int count = pending == null ? 0 : pending.Length;
            for (int i = 0; i < count; i++)
            {
                var save = pending[i];
                if (save == null || string.IsNullOrEmpty(save.TieId)) return false;
                Club home;
                Club away;
                string homeFeeder;
                string awayFeeder;
                if (!ReadSlot(save.HomeId, save.HomeFeeder, clubs, out home, out homeFeeder)) return false;
                if (!ReadSlot(save.AwayId, save.AwayFeeder, clubs, out away, out awayFeeder)) return false;
                snap.OpenTies.Add(new OpenTieSnapshot
                {
                    TieId = save.TieId,
                    Round = save.Round,
                    FirstLeg = new DateTime(save.FirstLegTicks),
                    Home = home,
                    HomeFeeder = homeFeeder,
                    Away = away,
                    AwayFeeder = awayFeeder
                });
            }

            return true;
        }

        static bool ReadSlot(string id, string feeder, Dictionary<string, Club> clubs, out Club club, out string feed)
        {
            club = null;
            feed = null;
            if (!string.IsNullOrEmpty(id)) return clubs.TryGetValue(id, out club);
            if (string.IsNullOrEmpty(feeder)) return false;
            feed = feeder;
            return true;
        }

        static bool AddClubs(string[] ids, Dictionary<string, Club> clubs, List<Club> dest)
        {
            int count = ids == null ? 0 : ids.Length;
            for (int i = 0; i < count; i++)
            {
                Club club;
                if (string.IsNullOrEmpty(ids[i]) || !clubs.TryGetValue(ids[i], out club)) return false;
                dest.Add(club);
            }

            return true;
        }

        static string DoubleText(double value) => value.ToString("G17", CultureInfo.InvariantCulture);

        static double ReadDouble(string text)
        {
            double value;
            if (string.IsNullOrEmpty(text) || !double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return 0d;
            return value;
        }

        static string Money(BigInteger amount) => amount.ToString(CultureInfo.InvariantCulture);

        static BigInteger ReadMoney(string text)
        {
            if (string.IsNullOrEmpty(text)) return BigInteger.Zero;
            BigInteger amount;
            return BigInteger.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out amount)
                ? amount
                : BigInteger.Zero;
        }
    }
}
