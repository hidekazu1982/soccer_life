using System;
using System.Collections.Generic;
using System.Globalization;
using SoccerClub.Competition;
using SoccerClub.Data;
using SoccerClub.Economy;
using SoccerClub.Facilities;
using SoccerClub.Match;
using SoccerClub.Personnel;
using SoccerClub.Time;
using SoccerClub.World;

namespace SoccerClub.Core
{
    /// <summary>
    /// 1日進行。オーナーは資金、フロント採用、予算だけを決める。
    /// </summary>
    public sealed partial class GameSession
    {
        public TimeManager Time { get; }
        public EconomyManager Economy { get; }
        public FacilityService Facilities { get; }
        public SeasonCalendar Calendar { get; }
        public LeagueService League { get; }
        public CupService Cup { get; }
        public IMatchSimulation Match { get; }
        public ScoutingService Scouting { get; }
        public GeneralManagerAi GeneralManager { get; }
        public FrontOfficeHiring FrontHiring { get; }
        public AcademyService Academy { get; }
        public IReadOnlyList<Club> Clubs { get; }
        public Club PlayerClub { get; }
        public bool GameOver { get; private set; }

        /// <summary>1から始まる。自クラブがリーグを優勝するたびに1上がる。</summary>
        public int LeagueLevel { get; set; } = 1;

        /// <summary>優勝で相手が強くなる倍率。LV2で1回、LV3でその上にさらに1回、と掛ける。</summary>
        public const double LeagueLevelStep = 1.15d;

        public string SeasonNotice { get; private set; }

        /// <summary>予算、採用、施設の決裁で状態が変わったとき。</summary>
        public event Action StateChanged;

        /// <summary>シーズンの区切り。8月1日で何年目かを数える。試合日はここから土曜に揃える。</summary>
        public static readonly DateTime SeasonStart = new DateTime(2026, 8, 1);

        /// <summary>新規ゲームの開始日。月曜。</summary>
        public static readonly DateTime OpeningDate = new DateTime(2026, 8, 3);

        /// <summary>いまのリーグ日程が属するシーズンの開始日。初年度は 2026-08-01。</summary>
        public DateTime CurrentSeasonStart
        {
            get
            {
                if (League != null && LeagueService.TryInferSeasonStart(League.Fixtures, out var inferred))
                    return inferred;
                return SeasonStart;
            }
        }

        readonly GameRandom _random;
        readonly List<StaffMember> _frontMarket = new List<StaffMember>();
        readonly List<Player> _freeAgents = new List<Player>();
        int _freeAgentSerial;
        readonly Dictionary<string, PendingGrowth> _pending = new Dictionary<string, PendingGrowth>();
        readonly HashSet<string> _freshInjuries = new HashSet<string>();
        readonly MonthLedger _month = new MonthLedger();
        MonthLedger _pendingReport;
        SponsorOffer _pendingOffer;
        SponsorCutNotice _pendingCut;

        public IReadOnlyList<StaffMember> FrontMarket => _frontMarket;

        sealed class PendingGrowth
        {
            public double Offense;
            public double Defense;
            public double Stamina;
            public double OffenseDone;
            public double DefenseDone;
            public double StaminaDone;
        }

        GameSession(
            TimeManager time,
            EconomyManager economy,
            FacilityService facilities,
            SeasonCalendar calendar,
            LeagueService league,
            CupService cup,
            IMatchSimulation match,
            ScoutingService scouting,
            GeneralManagerAi gm,
            FrontOfficeHiring frontHiring,
            AcademyService academy,
            IReadOnlyList<Club> clubs,
            Club playerClub,
            GameRandom random)
        {
            Time = time;
            Economy = economy;
            Facilities = facilities;
            Calendar = calendar;
            League = league;
            Cup = cup;
            Match = match;
            Scouting = scouting;
            GeneralManager = gm;
            FrontHiring = frontHiring;
            Academy = academy;
            Clubs = clubs;
            PlayerClub = playerClub;
            _random = random;
            ReleaseOperatingReserve(playerClub);
            Economy.OnBankrupt += club =>
            {
                if (club.IsPlayerOwned) GameOver = true;
            };
            Economy.OnLedger += NoteMonthLedger;
        }

        public static GameSession CreateNew(int playerClubIndex, int? seed = null)
        {
            var random = seed.HasValue ? new GameRandom(seed.Value) : GameRandom.Create();
            var clubs = WorldFactory.CreateLeague(random, playerClubIndex, out var playerClub);
            var seasonStart = SeasonStart;

            var economy = new EconomyManager();
            var lineup = new ManagerLineupAi();
            var match = new MatchSimulationManager(lineup, random);
            var league = new LeagueService(clubs);
            league.GenerateSeason(seasonStart);
            var cup = new CupService(match, random);
            cup.Generate(clubs, seasonStart, random);
            var calendar = new SeasonCalendar(league, cup, match, economy);
            var time = new TimeManager(OpeningDate);
            var facilities = new FacilityService(economy);
            var scouting = new ScoutingService();
            var gm = new GeneralManagerAi(economy);
            var front = new FrontOfficeHiring(economy);
            var academy = new AcademyService(economy);

            var session = new GameSession(
                time, economy, facilities, calendar, league, cup, match,
                scouting, gm, front, academy, clubs, playerClub, random);
            session.RefillFrontMarket();
            session.EnsureFreeAgents();
            session.EnsureContracts();
            return session;
        }

        /// <summary>セーブに無所属が無いとき、3000人を用意する。</summary>
        public void EnsureFreeAgents()
        {
            if (_freeAgents.Count > 0) return;
            int serial = 0;
            WorldFactory.FillFreeAgents(_random, _freeAgents, ref serial);
            _freeAgentSerial = serial;
        }

        /// <summary>この日の試合を、能力上昇の前に決着させる。自クラブのカードがなければ null。</summary>
        public Fixture ResolveTodayMatches()
        {
            if (GameOver) return null;
            var fixtures = Calendar.PlayDate(Time.CurrentDate);
            for (int i = 0; i < fixtures.Count; i++)
            {
                var fixture = fixtures[i];
                if (fixture.Home == PlayerClub || fixture.Away == PlayerClub)
                    return fixture;
            }

            return null;
        }

        /// <summary>当日の自クラブの試合カード。未消化でも返す。なければ null。</summary>
        public Fixture PlayerFixtureOnCurrentDate()
        {
            if (GameOver || PlayerClub == null) return null;
            var fixtures = Calendar.FixturesOn(Time.CurrentDate);
            for (int i = 0; i < fixtures.Count; i++)
            {
                var fixture = fixtures[i];
                if (fixture.Home == PlayerClub || fixture.Away == PlayerClub)
                    return fixture;
            }

            return null;
        }

        /// <summary>メイン画面にいる自クラブの選手かスタッフを、id から探す。</summary>
        public bool TryClubPerson(string id, out Player player, out StaffMember staff)
        {
            player = null;
            staff = null;
            if (string.IsNullOrEmpty(id) || PlayerClub == null) return false;
            player = FindInRoster(PlayerClub.Squad, id);
            if (player == null) player = FindInRoster(PlayerClub.Academy, id);
            if (player != null) return true;
            staff = FindStaff(PlayerClub, id);
            return staff != null;
        }

        static Player FindInRoster(List<Player> roster, string id)
        {
            if (roster == null) return null;
            for (int i = 0; i < roster.Count; i++)
            {
                var player = roster[i];
                if (player != null && player.Id == id) return player;
            }

            return null;
        }

        static StaffMember FindStaff(Club club, string id)
        {
            if (club == null) return null;
            var front = club.Front;
            if (front != null)
            {
                var found = MatchStaff(front.Secretary, id);
                if (found != null) return found;
                found = MatchStaff(front.GeneralManager, id);
                if (found != null) return found;
                found = MatchSlots(front.Strengthening, id);
                if (found != null) return found;
                found = MatchSlots(front.Sales, id);
                if (found != null) return found;
                found = MatchSlots(front.PublicRelations, id);
                if (found != null) return found;
                found = MatchSlots(front.Business, id);
                if (found != null) return found;
                found = MatchSlots(front.Academy, id);
                if (found != null) return found;
            }

            var team = club.Team;
            if (team == null) return null;
            var staff = MatchStaff(team.Manager, id);
            if (staff != null) return staff;
            staff = MatchStaff(team.HeadCoach, id);
            if (staff != null) return staff;
            staff = MatchSlots(team.Coaches, id);
            if (staff != null) return staff;
            staff = MatchStaff(team.PhysicalCoach, id);
            if (staff != null) return staff;
            staff = MatchSlots(team.Trainers, id);
            if (staff != null) return staff;
            return MatchStaff(team.Doctor, id);
        }

        static StaffMember MatchStaff(StaffMember staff, string id)
        {
            return staff != null && staff.Id == id ? staff : null;
        }

        static StaffMember MatchSlots(StaffMember[] slots, string id)
        {
            if (slots == null) return null;
            for (int i = 0; i < slots.Length; i++)
            {
                var found = MatchStaff(slots[i], id);
                if (found != null) return found;
            }

            return null;
        }

        /// <summary>
        /// 練習日に能力増減を抽選する。
        /// 他クラブと無所属は、施設とスタッフをリーグLVとして計算する。
        /// </summary>
        public void RollDailyGrowth()
        {
            _pending.Clear();
            if (GameOver || PlayerClub == null || !IsPracticeDay(Time.CurrentDate)) return;
            var playing = ClubsWithMatch(Time.CurrentDate);
            bool trainAcademy = Time.CurrentDate.Day == 15;
            int rivalLevel = LeagueLevel < 1 ? 1 : LeagueLevel;
            for (int c = 0; c < Clubs.Count; c++)
            {
                var club = Clubs[c];
                if (club == null) continue;
                bool owned = club.IsPlayerOwned;
                int? forced = owned ? (int?)null : rivalLevel;
                if (!playing.Contains(club))
                    RollRoster(club, club.Squad, false, forced, true);
                if (trainAcademy && club.DevelopmentPaid)
                    RollRoster(club, club.Academy, true, forced, false);
            }

            for (int i = 0; i < _freeAgents.Count; i++)
            {
                var player = _freeAgents[i];
                if (player == null || player.InjuryDays > 0 || player.Condition <= Player.ForcedRestCondition || string.IsNullOrEmpty(player.Id)) continue;
                if (_pending.ContainsKey(player.Id)) continue;
                _pending[player.Id] = RollOne(PlayerClub, player, false, rivalLevel);
            }
        }

        void RollRoster(Club club, List<Player> roster, bool academy, int? forcedLevel, bool restAfterMatch)
        {
            if (roster == null) return;
            for (int i = 0; i < roster.Count; i++)
            {
                var player = roster[i];
                if (player == null || player.InjuryDays > 0 || player.Condition <= Player.ForcedRestCondition || string.IsNullOrEmpty(player.Id)) continue;
                if (restAfterMatch && RestsAfterMatch(player)) continue;
                _pending[player.Id] = RollOne(club, player, academy, forcedLevel);
            }
        }

        static void ReleaseOperatingReserve(Club club)
        {
            if (club == null || club.Operating == 0) return;
            club.Coins += club.Operating;
            club.Operating = 0;
        }

        public bool ClubTrainsToday(Club club)
        {
            if (club == null || GameOver) return false;
            return IsPracticeDay(Time.CurrentDate) && !ClubsWithMatch(Time.CurrentDate).Contains(club);
        }

        public bool ClubHasMatchOn(Club club, DateTime date)
        {
            return club != null && ClubsWithMatch(date).Contains(club);
        }

        /// <summary>練習しない選手と、日曜に休むスタッフ・フロント。</summary>
        public void CopyGroundAbsentees(Club club, HashSet<string> dest)
        {
            if (dest == null) return;
            dest.Clear();
            if (club == null || club.Squad == null) return;
            bool trains = ClubTrainsToday(club);
            var squad = club.Squad;
            for (int i = 0; i < squad.Count; i++)
            {
                var player = squad[i];
                if (player == null || string.IsNullOrEmpty(player.Id)) continue;
                if (!trains || player.Condition <= Player.ForcedRestCondition || RestsAfterMatch(player))
                    dest.Add(player.Id);
            }

            if (!IsHoliday(Time.CurrentDate)) return;
            var team = club.Team;
            if (team != null)
            {
                AddAbsent(dest, team.Manager);
                AddAbsent(dest, team.HeadCoach);
                AddAbsentSlots(dest, team.Coaches);
                AddAbsent(dest, team.PhysicalCoach);
                AddAbsentSlots(dest, team.Trainers);
                AddAbsent(dest, team.Doctor);
            }

            var front = club.Front;
            if (front == null) return;
            AddAbsent(dest, front.Secretary);
            AddAbsent(dest, front.GeneralManager);
            AddAbsentSlots(dest, front.Strengthening);
            AddAbsentSlots(dest, front.Sales);
            AddAbsentSlots(dest, front.PublicRelations);
            AddAbsentSlots(dest, front.Business);
            AddAbsentSlots(dest, front.Academy);
        }

        static void AddAbsent(HashSet<string> dest, StaffMember staff)
        {
            if (staff != null && !string.IsNullOrEmpty(staff.Id))
                dest.Add(staff.Id);
        }

        static void AddAbsentSlots(HashSet<string> dest, StaffMember[] slots)
        {
            if (slots == null) return;
            for (int i = 0; i < slots.Length; i++)
                AddAbsent(dest, slots[i]);
        }

        static bool IsWeekday(DateTime date)
        {
            switch (date.DayOfWeek)
            {
                case DayOfWeek.Monday:
                case DayOfWeek.Tuesday:
                case DayOfWeek.Wednesday:
                case DayOfWeek.Thursday:
                case DayOfWeek.Friday:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>練習しうる日。シーズン中の日曜、ウィンターブレイク、オフシーズンは練習しない。</summary>
        bool IsPracticeDay(DateTime date)
        {
            return !ShowsHolidayBoard(date);
        }

        /// <summary>水曜の試合の翌日。スタメンと途中出場だけ休み、他の選手は練習する。</summary>
        bool RestsAfterMatch(Player player)
        {
            if (player == null || player.LastPlayedDate.Ticks == 0 || Time.CurrentDate.DayOfWeek != DayOfWeek.Thursday)
                return false;
            return player.LastPlayedDate.Date == Time.CurrentDate.Date.AddDays(-1);
        }

        /// <summary>日付と曜日。日曜の曜日だけ赤にする。</summary>
        public static string FormatCalendarDate(DateTime date)
        {
            string weekday = date.ToString("ddd", CultureInfo.InvariantCulture);
            if (date.DayOfWeek == DayOfWeek.Sunday)
                weekday = "<color=#E53935>" + weekday + "</color>";
            return date.ToString("MM/dd ", CultureInfo.InvariantCulture) + weekday;
        }

        HashSet<Club> ClubsWithMatch(DateTime date)
        {
            var playing = new HashSet<Club>();
            var fixtures = Calendar.FixturesOn(date);
            for (int i = 0; i < fixtures.Count; i++)
            {
                var fixture = fixtures[i];
                if (fixture == null) continue;
                if (fixture.Home != null) playing.Add(fixture.Home);
                if (fixture.Away != null) playing.Add(fixture.Away);
            }

            return playing;
        }

        public bool TryGetDailyDelta(string playerId, out int delta)
        {
            PendingGrowth step;
            if (playerId != null && _pending.TryGetValue(playerId, out step))
            {
                delta = DisplayDelta(step);
                return true;
            }

            delta = 0;
            return false;
        }

        /// <summary>
        /// その日に抽選した増減のうち、動く能力（0 攻撃、1 守備、2 スタミナ）と表示上の値。
        /// 1 日に動く能力は 1 つだけ。表示上 0 のときは false。
        /// </summary>
        public bool TryGetDailyStat(string playerId, out int stat, out int delta)
        {
            stat = 0;
            delta = 0;
            PendingGrowth step;
            if (playerId == null || !_pending.TryGetValue(playerId, out step)) return false;
            if (step.Offense != 0d) { stat = 0; delta = Player.ToDisplay(step.Offense); }
            else if (step.Defense != 0d) { stat = 1; delta = Player.ToDisplay(step.Defense); }
            else if (step.Stamina != 0d) { stat = 2; delta = Player.ToDisplay(step.Stamina); }
            return delta != 0;
        }

        public void CopyDailyDeltas(Dictionary<string, int> dest)
        {
            if (dest == null) return;
            dest.Clear();
            foreach (var pair in _pending)
            {
                int shown = DisplayDelta(pair.Value);
                if (shown != 0) dest[pair.Key] = shown;
            }
        }

        /// <summary>抽選済みの増減を、その日の進み具合 fraction だけ反映する。</summary>
        public void ApplyGrowth(double fraction)
        {
            if (fraction <= 0d || GameOver || _pending.Count == 0) return;
            for (int c = 0; c < Clubs.Count; c++)
                ApplyClubGrowth(Clubs[c], fraction);
            ApplyRosterGrowth(_freeAgents, fraction);
        }

        /// <summary>当日の試合と収支を処理し、日付を1日進める。成長の残りがあればここで埋める。</summary>
        public IReadOnlyList<Fixture> AdvanceDay()
        {
            if (GameOver) return Array.Empty<Fixture>();
            var closing = Time.CurrentDate.Date;
            bool monthEnd = closing.Day == DateTime.DaysInMonth(closing.Year, closing.Month);

            FinishGrowth();
            EnsureContracts();
            var fixtures = Calendar.PlayDate(Time.CurrentDate);
            ApplyMatchConsequences(fixtures);
            ApplyCondition(fixtures);
            CutPlayerSponsors();

            if (Time.DayOfMonth == 1)
            {
                var broadcast = ClubMath.BroadcastMonthly(LeagueStrengthFactor);
                for (int i = 0; i < Clubs.Count; i++)
                {
                    var club = Clubs[i];
                    Economy.ApplyMonthlyIncome(club);
                    if (broadcast > 0) Economy.Apply(club, broadcast, "broadcast");
                    ClubMath.ApplyPublicRelationsInflow(club);
                    Economy.PayMonthlyWages(club);
                    Economy.TryPayDevelopment(club);
                }

                RefillFrontMarket();
            }

            if (IsContractRenewalDay())
                AgeForNewSeason();

            TryStartNextSeason();

            if (Time.CurrentDate.Month == 6 && Time.CurrentDate.Day == 15)
            {
                for (int i = 0; i < Clubs.Count; i++)
                {
                    var club = Clubs[i];
                    double youthScale = club.IsPlayerOwned ? 1d : LeagueStrengthFactor;
                    Academy.TryPromote(club, _random, youthScale);
                }
            }

            Time.AdvanceDay();
            ClosePersonnelWindowIfLeft(closing, Time.CurrentDate.Date);

            TickInjuries();
            if (PlayerClub.Coins < 0) GameOver = true;
            if (monthEnd) PublishMonthReport(closing);
            return fixtures;
        }

        public bool HasMonthReport => _pendingReport != null;

        public MonthLedger PendingMonthReport => _pendingReport;

        public bool HasSponsorOffer => _pendingOffer != null;

        public SponsorOffer PendingSponsorOffer => _pendingOffer;

        public bool HasSponsorCut => _pendingCut != null && _pendingCut.Contracts.Count > 0;

        public SponsorCutNotice PendingSponsorCut => _pendingCut;

        public void DismissSponsorCut()
        {
            _pendingCut = null;
        }

        /// <summary>消化した平日に、営業がスポンサーを連れてくるか抽選する。</summary>
        public bool TryCreateSponsorOffer(DateTime playedDate)
        {
            if (GameOver || PlayerClub == null || _pendingOffer != null) return false;
            if (!IsWeekday(playedDate)) return false;
            int level = PlayerClub.Front != null ? PlayerClub.Front.SalesLevel() : 0;
            if (level <= 0) return false;
            int active = ActiveSponsorCount(PlayerClub);
            if (active >= ClubMath.SponsorContractCap) return false;
            if (_random.NextDouble() >= ClubMath.SponsorOfferChance) return false;

            var monthly = ClubMath.SponsorContractMonthly(PlayerClub);
            if (monthly <= 0) return false;
            var name = PickSponsor(PlayerClub, out int cutRank, out int cutLosses);
            if (name == null) return false;
            _pendingOffer = MakeSponsorOffer(name, monthly, active, cutRank, cutLosses);
            return true;
        }

        /// <summary>
        /// デバッグ用。曜日・抽選・営業レベル・契約上限を無視して、スポンサー契約を今すぐ出す。
        /// 営業がいなくて月額が 0 のときは下限額、空いている候補がないときは仮の社名にする。
        /// </summary>
        public bool DebugCreateSponsorOffer()
        {
            if (GameOver || PlayerClub == null) return false;
            if (_pendingOffer != null) return true;
            var monthly = ClubMath.SponsorContractMonthly(PlayerClub);
            if (monthly <= 0) monthly = new System.Numerics.BigInteger(ClubMath.SponsorContractMonthlyFloor);
            var name = PickSponsor(PlayerClub, out int cutRank, out int cutLosses);
            if (name == null)
            {
                name = "デバッグ商事";
                cutRank = 0;
                cutLosses = 0;
            }

            _pendingOffer = MakeSponsorOffer(name, monthly, ActiveSponsorCount(PlayerClub), cutRank, cutLosses);
            return true;
        }

        SponsorOffer MakeSponsorOffer(string name, System.Numerics.BigInteger monthly, int active, int cutRank, int cutLosses)
        {
            var lead = LeadSales(PlayerClub);
            return new SponsorOffer
            {
                SponsorName = name,
                StaffName = lead != null ? lead.Name : "",
                Monthly = monthly,
                Upfront = ClubMath.SponsorUpfront(monthly),
                Months = ClubMath.SponsorContractMonths,
                ActiveContracts = active,
                CutRank = cutRank,
                CutLosses = cutLosses
            };
        }

        public void AcceptSponsorOffer()
        {
            var offer = _pendingOffer;
            _pendingOffer = null;
            if (offer == null || PlayerClub == null) return;
            if (PlayerClub.Sponsors == null) PlayerClub.Sponsors = new List<SponsorContract>();
            PlayerClub.Sponsors.Add(new SponsorContract
            {
                Name = offer.SponsorName,
                Monthly = offer.Monthly,
                MonthsLeft = offer.Months,
                CutRank = offer.CutRank,
                CutLosses = offer.CutLosses
            });
            if (offer.Upfront > 0) Economy.Apply(PlayerClub, offer.Upfront, "sponsor");
            NotifyChanged();
        }

        public void DeclineSponsorOffer()
        {
            if (_pendingOffer == null) return;
            _pendingOffer = null;
            NotifyChanged();
        }

        static int ActiveSponsorCount(Club club)
        {
            if (club == null || club.Sponsors == null) return 0;
            int count = 0;
            for (int i = 0; i < club.Sponsors.Count; i++)
            {
                var contract = club.Sponsors[i];
                if (contract != null && contract.MonthsLeft > 0) count++;
            }

            return count;
        }

        string PickSponsor(Club club, out int cutRank, out int cutLosses)
        {
            cutRank = 0;
            cutLosses = 0;
            int rank = LeagueRank(club);
            int streak = club.LosingStreak;
            int free = 0;
            var catalog = SponsorCatalog.All;
            for (int i = 0; i < catalog.Length; i++)
            {
                if (SponsorEligible(club, catalog[i], rank, streak)) free++;
            }

            if (free == 0) return null;
            int pick = _random.Next(free);
            for (int i = 0; i < catalog.Length; i++)
            {
                var profile = catalog[i];
                if (!SponsorEligible(club, profile, rank, streak)) continue;
                if (pick == 0)
                {
                    cutRank = profile.CutRank;
                    cutLosses = profile.CutLosses;
                    return profile.Name;
                }

                pick--;
            }

            return null;
        }

        static bool SponsorEligible(Club club, SponsorContract profile, int rank, int streak)
        {
            if (profile == null || SponsorNameTaken(club, profile.Name)) return false;
            return !ClubMath.SponsorBroken(profile.CutRank, profile.CutLosses, rank, streak);
        }

        static bool SponsorNameTaken(Club club, string name)
        {
            if (club == null || club.Sponsors == null) return false;
            for (int i = 0; i < club.Sponsors.Count; i++)
            {
                var contract = club.Sponsors[i];
                if (contract != null && contract.MonthsLeft > 0 && contract.Name == name) return true;
            }

            return false;
        }

        static StaffMember LeadSales(Club club)
        {
            if (club == null || club.Front == null || club.Front.Sales == null) return null;
            StaffMember best = null;
            var sales = club.Front.Sales;
            for (int i = 0; i < sales.Length; i++)
            {
                var member = sales[i];
                if (member == null) continue;
                if (best == null || member.Level > best.Level) best = member;
            }

            return best;
        }

        public void DismissMonthReport()
        {
            _pendingReport = null;
        }

        void PublishMonthReport(DateTime month)
        {
            var report = _month.Copy();
            report.Year = month.Year;
            report.Month = month.Month;
            report.Coins = PlayerClub != null ? PlayerClub.Coins : 0;
            _pendingReport = report;
            _month.Clear();
        }

        void NoteMonthLedger(Club club, LedgerEntry entry)
        {
            if (club != PlayerClub || entry.Amount == 0) return;
            var reason = entry.Reason ?? "";
            var amount = entry.Amount;
            if (reason == "sponsor") _month.Sponsor += amount;
            else if (reason == "goods") _month.Goods += amount;
            else if (reason == "gate") _month.Gate += amount;
            else if (reason == "broadcast") _month.Broadcast += amount;
            else if (reason == "prize") _month.Prize += amount;
            else if (reason == "operating") _month.Operating -= amount;
            else if (reason == "advertising") _month.Advertising -= amount;
            else if (reason == "development") _month.Development -= amount;
            else if (reason.StartsWith("facility-")) _month.Facilities -= amount;
            else if (reason == "front-wage" || reason.StartsWith("front-hire-") || reason.StartsWith("front-renew-"))
                _month.FrontWages -= amount;
            else if (reason == "squad-wage") _month.SquadWages -= amount;
            else if (reason.StartsWith("sign-")
                || reason.StartsWith("staff-")
                || reason.StartsWith("academy-")
                || reason.StartsWith("player-renew-")
                || reason.StartsWith("team-renew-")
                || reason.StartsWith("retain-"))
                _month.Reinforcement -= amount;
        }

        public bool OwnerMovesBudget(BudgetKind kind, int sign)
        {
            if (kind == BudgetKind.Operating) return false;
            var step = new System.Numerics.BigInteger(ClubMath.BudgetStep);
            if (sign > 0)
            {
                if (PlayerClub.Coins < step) return false;
                PlayerClub.Coins -= step;
                PlayerClub.AddBudget(kind, step);
                NotifyChanged();
                return true;
            }

            if (PlayerClub.BudgetOf(kind) < step) return false;
            PlayerClub.AddBudget(kind, -step);
            PlayerClub.Coins += step;
            NotifyChanged();
            return true;
        }

        public bool OwnerHiresFrontOffice(StaffMember staff)
        {
            if (!FrontHiring.TryHire(PlayerClub, staff)) return false;
            staff.JoinedTicks = Time.CurrentDate.Date.Ticks;
            _frontMarket.Remove(staff);
            NotifyChanged();
            return true;
        }

        public bool OwnerContractsFront(StaffMember staff, int slot, out StaffMember dismissed)
        {
            dismissed = null;
            if (!FrontHiring.TryContract(PlayerClub, staff, slot, out dismissed)) return false;
            // 在籍日数（サポートキャラの交代提案は在籍 1 年以上が対象）を契約日から数える。
            staff.JoinedTicks = Time.CurrentDate.Date.Ticks;
            if (dismissed != null && !_frontMarket.Contains(dismissed))
                _frontMarket.Add(dismissed);
            _frontMarket.Remove(staff);
            NotifyChanged();
            return true;
        }

        public bool OwnerDismissesFront(int slot)
        {
            if (PlayerClub == null || PlayerClub.Front == null) return false;
            var staff = PlayerClub.Front.GetAt(slot);
            if (staff == null) return false;
            PlayerClub.Front.SetAt(slot, null);
            if (!_frontMarket.Contains(staff))
                _frontMarket.Add(staff);
            NotifyChanged();
            return true;
        }

        public bool OwnerApprovesFacilityUpgrade(FacilityKind kind)
        {
            if (!Facilities.TryUpgrade(PlayerClub, kind)) return false;
            NotifyChanged();
            return true;
        }

        void NotifyChanged()
        {
            _supportDirty = true;
            StateChanged?.Invoke();
        }

        void FinishGrowth()
        {
            if (_pending.Count == 0) return;

            for (int c = 0; c < Clubs.Count; c++)
            {
                var club = Clubs[c];
                FinishRoster(club.Squad);
                FinishRoster(club.Academy);
            }

            FinishRoster(_freeAgents);
            _pending.Clear();
        }

        void FinishRoster(List<Player> roster)
        {
            if (roster == null) return;
            for (int i = 0; i < roster.Count; i++)
            {
                var player = roster[i];
                PendingGrowth step;
                if (player == null || !_pending.TryGetValue(player.Id, out step)) continue;
                double offense = step.Offense - step.OffenseDone;
                double defense = step.Defense - step.DefenseDone;
                double stamina = step.Stamina - step.StaminaDone;
                if (Math.Abs(offense) < 0.0001d) offense = 0d;
                if (Math.Abs(defense) < 0.0001d) defense = 0d;
                if (Math.Abs(stamina) < 0.0001d) stamina = 0d;
                if (offense != 0d || defense != 0d || stamina != 0d)
                    player.ApplyDailyGrowth(offense, defense, stamina);
            }
        }

        void ApplyClubGrowth(Club club, double fraction)
        {
            ApplyRosterGrowth(club.Squad, fraction);
            ApplyRosterGrowth(club.Academy, fraction);
        }

        void ApplyRosterGrowth(List<Player> roster, double fraction)
        {
            if (roster == null) return;
            for (int i = 0; i < roster.Count; i++)
            {
                var player = roster[i];
                PendingGrowth step;
                if (player == null || !_pending.TryGetValue(player.Id, out step)) continue;
                double offense = step.Offense * fraction;
                double defense = step.Defense * fraction;
                double stamina = step.Stamina * fraction;
                player.ApplyDailyGrowth(offense, defense, stamina);
                step.OffenseDone += offense;
                step.DefenseDone += defense;
                step.StaminaDone += stamina;
            }
        }

        static int DisplayDelta(PendingGrowth step)
        {
            return Player.ToDisplay(step.Offense + step.Defense + step.Stamina);
        }

        void TickInjuries()
        {
            for (int c = 0; c < Clubs.Count; c++)
            {
                var squad = Clubs[c].Squad;
                for (int i = 0; i < squad.Count; i++)
                {
                    var player = squad[i];
                    if (player == null || player.InjuryDays <= 0 || string.IsNullOrEmpty(player.Id)) continue;
                    if (_freshInjuries.Contains(player.Id)) continue;
                    player.InjuryDays--;
                }
            }

            _freshInjuries.Clear();
        }

        PendingGrowth RollOne(Club club, Player player, bool academy, int? forcedLevel)
        {
            int raw = _random.NextDouble() < UpChance(player.Age)
                ? PositiveAmount(player.Age)
                : -NegativeAmount(player.Age);
            int stat = PickPracticeStat(player.PreferredPosition);
            bool growOffense = stat == 0;
            bool growDefense = stat == 1;
            bool growStamina = stat == 2;
            double offense = 0d;
            double defense = 0d;
            double stamina = 0d;
            if (raw > 0)
            {
                double offenseMult;
                double staminaMult;
                if (academy)
                {
                    offenseMult = ClubMath.AcademyMultiplier(club, forcedLevel);
                    staminaMult = offenseMult;
                }
                else
                {
                    ClubMath.PracticeMultiplier(club, player, out offenseMult, out staminaMult, forcedLevel);
                }

                int condition = player.ConditionGainPercent();
                if (growOffense) offense = ScaledGain(ClubMath.TrainingBaseAdd * offenseMult, condition);
                if (growDefense) defense = ScaledGain(ClubMath.TrainingBaseAdd * offenseMult, condition);
                if (growStamina) stamina = ScaledGain(ClubMath.TrainingBaseAdd * staminaMult, condition);
            }
            else
            {
                if (growOffense) offense = raw;
                if (growDefense) defense = raw;
                if (growStamina) stamina = raw;
            }

            return new PendingGrowth { Offense = offense, Defense = defense, Stamina = stamina };
        }

        /// <summary>基礎点に練習効果をかけた上昇量へ、調子と 90〜100% をかけ、小数第1位で四捨五入する。</summary>
        double ScaledGain(double multiplied, int conditionPercent)
        {
            int swing = 90 + _random.Next(11);
            double value = multiplied * conditionPercent * swing / 10000d;
            value = Math.Round(value, 1, MidpointRounding.AwayFromZero);
            if (value < 0.1d) value = 0.1d;
            return value;
        }

        /// <summary>0 がオフェンス、1 がディフェンス、2 がスタミナ。RW と LW は AM と同じ。</summary>
        int PickPracticeStat(Position position)
        {
            int defense;
            int offense;
            switch (position)
            {
                case Position.GK:
                    defense = 65;
                    offense = 5;
                    break;
                case Position.CB:
                    defense = 60;
                    offense = 10;
                    break;
                case Position.LSB:
                case Position.RSB:
                case Position.LWB:
                case Position.RWB:
                    defense = 50;
                    offense = 20;
                    break;
                case Position.DM:
                    defense = 40;
                    offense = 30;
                    break;
                case Position.AM:
                case Position.LSH:
                case Position.RSH:
                case Position.RW:
                case Position.LW:
                    defense = 20;
                    offense = 50;
                    break;
                default:
                    defense = 10;
                    offense = 60;
                    break;
            }

            int roll = _random.Next(100);
            if (roll < defense) return 1;
            if (roll < defense + offense) return 0;
            return 2;
        }

        static double UpChance(int age)
        {
            if (age < 16) age = 16;
            double chance;
            if (age <= 21) chance = 0.68d + (age - 16) * 0.03d;
            else if (age <= 27) chance = 0.83d + (age - 21) * 0.015d;
            else if (age <= 31) chance = 0.92d - (age - 27) * 0.12d;
            else if (age <= 36) chance = Math.Max(0.06d, 0.20d - (age - 32) * 0.04d);
            else chance = 0.06d;
            if (chance < 0.05d) chance = 0.05d;
            if (chance > 0.97d) chance = 0.97d;
            return chance;
        }

        int PositiveAmount(int age)
        {
            if (age >= 22 && age <= 28) return 1 + _random.Next(3);
            if (age < 22) return 1 + _random.Next(2);
            return 1;
        }

        int NegativeAmount(int age)
        {
            if (age >= 35) return 1 + _random.Next(3);
            if (age >= 32) return 1 + _random.Next(2);
            return 1;
        }

        void ApplyMatchConsequences(IReadOnlyList<Fixture> fixtures)
        {
            for (int i = 0; i < fixtures.Count; i++)
            {
                var fixture = fixtures[i];
                if (fixture == null || fixture.ConsequencesApplied || fixture.Result == null) continue;
                var result = fixture.Result;
                var winner = result.Winner;
                if (winner == fixture.Home)
                {
                    ClubMath.ApplyWin(fixture.Home);
                    ClubMath.ApplyLoss(fixture.Away);
                }
                else if (winner == fixture.Away)
                {
                    ClubMath.ApplyWin(fixture.Away);
                    ClubMath.ApplyLoss(fixture.Home);
                }

                ClubMath.ApplyOperatingShortfall(fixture.Home, fixture.OperatingPaid, fixture.OperatingRequired);
                NoteStadiumDemand(fixture);
                ClubMath.ApplyWordOfMouth(fixture.Home, fixture.Attendance);
                NoteResult(fixture.Home, result.Winner);
                NoteResult(fixture.Away, result.Winner);
                RollInjuries(fixture.Home, result.HomeAppeared);
                RollInjuries(fixture.Away, result.AwayAppeared);
                CountAppearances(fixture.Home, result.HomeAppeared, fixture.Date);
                CountAppearances(fixture.Away, result.AwayAppeared, fixture.Date);
                NoteGoals(result, fixture.Date);
                NoteSeasonRatings(fixture);
                fixture.ConsequencesApplied = true;
            }
        }

        static void NoteGoals(MatchResult result, DateTime date)
        {
            if (result == null || result.Events == null) return;
            var day = date.Date;
            for (int i = 0; i < result.Events.Count; i++)
            {
                var duel = result.Events[i];
                if (duel == null || !duel.Goal || duel.Attacker == null) continue;
                duel.Attacker.LastGoalDate = day;
            }
        }

        static void NoteSeasonRatings(Fixture fixture)
        {
            if (fixture == null) return;
            AddSeasonRatings(fixture, fixture.Home);
            AddSeasonRatings(fixture, fixture.Away);
        }

        static void AddSeasonRatings(Fixture fixture, Club club)
        {
            var rows = MatchRatings.ForClub(fixture, club);
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row == null || row.Player == null) continue;
                row.Player.SeasonGoals += row.Goals;
                row.Player.SeasonRatingSum += row.Score;
                row.Player.SeasonRatedMatches++;
            }
        }

        static void NoteResult(Club club, Club winner)
        {
            if (club == null) return;
            if (winner == null || winner == club) club.LosingStreak = 0;
            else club.LosingStreak++;
        }

        void CutPlayerSponsors()
        {
            if (PlayerClub == null || _pendingCut != null) return;
            var sponsors = PlayerClub.Sponsors;
            if (sponsors == null) return;
            int rank = LeagueRank(PlayerClub);
            int streak = PlayerClub.LosingStreak;
            var cut = new List<SponsorContract>();
            for (int i = sponsors.Count - 1; i >= 0; i--)
            {
                var contract = sponsors[i];
                if (contract == null || contract.MonthsLeft <= 0) continue;
                if (!ClubMath.SponsorBroken(contract.CutRank, contract.CutLosses, rank, streak)) continue;
                cut.Add(contract);
                sponsors.RemoveAt(i);
            }

            if (cut.Count == 0) return;
            cut.Reverse();
            _pendingCut = new SponsorCutNotice();
            _pendingCut.Contracts.AddRange(cut);
        }

        void RollInjuries(Club club, List<Player> appeared)
        {
            if (appeared == null) return;
            var candidates = new List<Player>();
            for (int i = 0; i < appeared.Count; i++)
            {
                var player = appeared[i];
                if (player == null || player.InjuryDays > 0 || string.IsNullOrEmpty(player.Id)) continue;
                if (!candidates.Contains(player)) candidates.Add(player);
            }

            if (candidates.Count == 0) return;
            ClubMath.InjuryOutcome(club, out var rate, out var days);
            if (_random.NextDouble() >= rate) return;

            int count = 1;
            if (candidates.Count >= 2 && _random.NextDouble() < ClubMath.SecondInjuryChance)
                count = 2;
            if (count == 2 && candidates.Count >= 3 && _random.NextDouble() < ClubMath.ThirdInjuryChance)
                count = 3;

            for (int n = 0; n < count; n++)
            {
                int pick = _random.Next(candidates.Count);
                var player = candidates[pick];
                candidates.RemoveAt(pick);
                player.InjuryDays = days;
                _freshInjuries.Add(player.Id);
            }
        }

        static void CountAppearances(Club club, List<Player> appeared, DateTime date)
        {
            club.MatchesSinceWindow++;
            if (appeared == null) return;
            var day = date.Date;
            for (int i = 0; i < appeared.Count; i++)
            {
                var player = appeared[i];
                if (player == null) continue;
                player.Appearances++;
                player.LastPlayedDate = day;
            }
        }

        void ApplyCondition(IReadOnlyList<Fixture> fixtures)
        {
            for (int c = 0; c < Clubs.Count; c++)
            {
                var club = Clubs[c];
                var appeared = new HashSet<Player>();
                var bench = new HashSet<Player>();
                bool played = false;
                for (int i = 0; i < fixtures.Count; i++)
                {
                    var fixture = fixtures[i];
                    if (fixture == null || fixture.Result == null) continue;
                    if (fixture.Home != club && fixture.Away != club) continue;
                    played = true;
                    var result = fixture.Result;
                    AddAll(fixture.Home == club ? result.HomeAppeared : result.AwayAppeared, appeared);
                    AddAll(fixture.Home == club ? result.HomeBench : result.AwayBench, bench);
                }

                bool sunday = Time.CurrentDate.DayOfWeek == DayOfWeek.Sunday;
                bool trainingDay = IsPracticeDay(Time.CurrentDate) && !played;
                int league = LeagueLevel;
                int trainingCost = ClubMath.ConditionAmount(Player.TrainingConditionCost, Player.TrainingFatiguePerLeague, league);
                int matchCost = ClubMath.ConditionAmount(Player.MatchPlayedConditionCost, Player.MatchFatiguePerLeague, league);
                int benchCost = ClubMath.ConditionAmount(Player.MatchBenchConditionCost, Player.BenchFatiguePerLeague, league);
                int sundayRecovery = ClubMath.ConditionAmount(Player.SundayConditionRecovery, Player.SundayRecoveryPerLeague, league);
                int dayOffRecovery = ClubMath.ConditionAmount(Player.DayOffConditionRecovery, Player.DayOffRecoveryPerLeague, league);
                int forcedRest = ClubMath.ConditionAmount(Player.ForcedRestRecovery, Player.ForcedRestPerLeague, league);
                bool unpaid = club.UnpaidMonths > 0;
                if (unpaid)
                {
                    // 給料未払いのあいだは回復が半分。
                    sundayRecovery /= 2;
                    dayOffRecovery /= 2;
                    forcedRest /= 2;
                }
                for (int i = 0; i < club.Squad.Count; i++)
                {
                    var player = club.Squad[i];
                    if (player == null) continue;
                    if (played && appeared.Contains(player)) player.AddCondition(-matchCost);
                    else if (played && bench.Contains(player)) player.AddCondition(-benchCost);
                    else if (played) player.AddCondition(dayOffRecovery);
                    else if (player.Condition <= Player.ForcedRestCondition) player.AddCondition(forcedRest);
                    else if (sunday) player.AddCondition(sundayRecovery);
                    else if (RestsAfterMatch(player)) player.AddCondition(dayOffRecovery);
                    else if (!trainingDay) player.AddCondition(dayOffRecovery);
                    else player.AddCondition(-trainingCost);
                }

                RecoverCondition(club, unpaid);
            }
        }

        static void AddAll(List<Player> source, HashSet<Player> dest)
        {
            if (source == null) return;
            for (int i = 0; i < source.Count; i++)
            {
                if (source[i] != null) dest.Add(source[i]);
            }
        }

        static void RecoverCondition(Club club, bool unpaid)
        {
            int budget = ClubMath.TrainerRecoveryBudget(club);
            if (unpaid) budget /= 2;
            if (budget <= 0 || club.Squad == null || club.Squad.Count == 0) return;
            var tired = new List<Player>(club.Squad.Count);
            for (int i = 0; i < club.Squad.Count; i++)
            {
                var player = club.Squad[i];
                if (player != null && player.Condition < 100) tired.Add(player);
            }

            tired.Sort((a, b) => a.Condition.CompareTo(b.Condition));
            int index = 0;
            while (budget > 0 && index < tired.Count)
            {
                int current = tired[index].Condition;
                int next = index + 1 < tired.Count ? tired[index + 1].Condition : 100;
                int gap = next - current;
                if (gap <= 0)
                {
                    index++;
                    continue;
                }

                int group = index + 1;
                int need = gap * group;
                if (budget >= need)
                {
                    for (int k = 0; k < group; k++)
                        tired[k].AddCondition(gap);
                    budget -= need;
                    index++;
                    continue;
                }

                int each = budget / group;
                int remainder = budget % group;
                for (int k = 0; k < group; k++)
                    tired[k].AddCondition(each + (k < remainder ? 1 : 0));
                budget = 0;
            }
        }

        bool IsContractRenewalDay()
        {
            var date = Time.CurrentDate.Date;
            return date > SeasonStart && date.Month == 8 && date.Day == 1;
        }

        public double LeagueStrengthFactor
        {
            get
            {
                int steps = LeagueLevel - 1;
                if (steps <= 0) return 1d;
                return Math.Pow(LeagueLevelStep, steps);
            }
        }

        public void ClearSeasonNotice()
        {
            SeasonNotice = null;
        }

        /// <summary>
        /// リーグ全日程が終わり、次の8月1日を過ぎていれば新シーズンを組む。
        /// 自クラブが優勝したときだけリーグLVを上げ、対戦相手を強くする。
        /// </summary>
        void TryStartNextSeason()
        {
            for (int guard = 0; guard < 20 && RollOneSeason(); guard++)
            {
            }
        }

        bool RollOneSeason()
        {
            if (!LeagueIsComplete(out var lastMatch)) return false;
            var nextStart = new DateTime(lastMatch.Year, 8, 1);
            if (lastMatch.Month >= 8) nextStart = nextStart.AddYears(1);
            if (Time.CurrentDate.Date < nextStart) return false;

            var table = League.Table();
            var champion = table.Count > 0 ? table[0] : null;
            bool playerWon = champion != null && champion == PlayerClub;
            PayLeaguePrizes(table);
            League.StorePreviousRanks();
            for (int i = 0; i < Clubs.Count; i++)
            {
                var club = Clubs[i];
                if (club == null) continue;
                club.ResetSeasonStats();
                ResetPlayerSeason(club.Squad);
                ResetPlayerSeason(club.Academy);
            }

            ResetPlayerSeason(_freeAgents);

            if (playerWon)
            {
                LeagueLevel++;
                StrengthenRivals();
                SeasonNotice = "リーグ優勝。リーグLVが " + LeagueLevel + " になりました。対戦相手が強くなります。";
            }
            else
            {
                string name = champion != null ? champion.Name : "他クラブ";
                SeasonNotice = name + " がリーグを優勝しました。リーグLVは " + LeagueLevel + " のままです。";
            }

            Calendar.ForgetCupLegs();
            League.GenerateSeason(nextStart);
            Cup.Generate(Clubs, nextStart, _random);
            PlayMissedFixtures(Time.CurrentDate.Date);
            return true;
        }

        static void ResetPlayerSeason(List<Player> roster)
        {
            if (roster == null) return;
            for (int i = 0; i < roster.Count; i++)
            {
                var player = roster[i];
                if (player == null) continue;
                player.SeasonGoals = 0;
                player.SeasonRatingSum = 0d;
                player.SeasonRatedMatches = 0;
            }
        }

        static bool LeagueIsComplete(IReadOnlyList<Fixture> fixtures, out DateTime lastMatch)
        {
            lastMatch = DateTime.MinValue;
            if (fixtures == null || fixtures.Count == 0) return false;
            for (int i = 0; i < fixtures.Count; i++)
            {
                var fixture = fixtures[i];
                if (fixture == null || !fixture.IsLeague) continue;
                if (fixture.Result == null) return false;
                if (fixture.Date > lastMatch) lastMatch = fixture.Date;
            }

            return lastMatch != DateTime.MinValue;
        }

        bool LeagueIsComplete(out DateTime lastMatch)
        {
            return LeagueIsComplete(League.Fixtures, out lastMatch);
        }

        void PlayMissedFixtures(DateTime today)
        {
            for (int guard = 0; guard < 400; guard++)
            {
                var next = DateTime.MaxValue;
                ConsiderMissed(League.Fixtures, today, ref next);
                ConsiderMissed(Cup.Fixtures, today, ref next);
                if (next == DateTime.MaxValue) return;
                var played = Calendar.PlayDate(next);
                ApplyMatchConsequences(played);
            }
        }

        static void ConsiderMissed(IReadOnlyList<Fixture> fixtures, DateTime today, ref DateTime next)
        {
            if (fixtures == null) return;
            for (int i = 0; i < fixtures.Count; i++)
            {
                var fixture = fixtures[i];
                if (fixture == null || fixture.Result != null) continue;
                var day = fixture.Date.Date;
                if (day < today && day < next) next = day;
            }
        }

        void StrengthenRivals()
        {
            for (int i = 0; i < Clubs.Count; i++)
            {
                var club = Clubs[i];
                if (club == null || club.IsPlayerOwned) continue;
                ScaleRoster(club.Squad, LeagueLevelStep);
                ScaleRoster(club.Academy, LeagueLevelStep);
                if (club.Reinforcement > 0)
                    club.Reinforcement = ClubMath.RoundCoins(ClubMath.ToDouble(club.Reinforcement) * LeagueLevelStep);
            }
        }

        static void ScaleRoster(List<Player> roster, double factor)
        {
            if (roster == null || factor <= 1d) return;
            for (int i = 0; i < roster.Count; i++)
            {
                var player = roster[i];
                if (player == null) continue;
                WorldFactory.ScaleOverall(player, player.Overall * factor);
            }
        }

        void AgeForNewSeason()
        {
            for (int i = 0; i < Clubs.Count; i++)
            {
                var club = Clubs[i];
                if (club == null) continue;
                AgeSquad(club.Squad);
                RenewContracts(club.Squad);
                AcademyService.AgeAndRetire(club);
                RetireAged(club.Front);
                RetireAged(club.Team);
            }

            AgeFreeAgents();
        }

        void AgeFreeAgents()
        {
            for (int i = 0; i < _freeAgents.Count; i++)
            {
                var player = _freeAgents[i];
                if (player == null) continue;
                player.Age++;
                if (player.Age > ClubMath.FreeAgentMaxAge)
                    ResetFreeAgentCareer(player);
                else
                    player.Phase = WorldFactory.PhaseForAge(player.Age);
            }
        }

        void ResetFreeAgentCareer(Player player)
        {
            var fresh = WorldFactory.CreatePlayer(_random, player.Id, 18, GrowthPhase.Youth, 1d, player.PreferredPosition);
            player.Age = 18;
            player.Phase = GrowthPhase.Youth;
            player.Offense = fresh.Offense;
            player.Defense = fresh.Defense;
            player.StaminaMax = fresh.StaminaMax;
            player.StaminaCurrent = fresh.StaminaMax;
            player.Condition = fresh.Condition;
            player.InjuryDays = 0;
            player.Appearances = 0;
            player.LastPlayedDate = default;
            player.LastGoalDate = default;
            player.SeasonGoals = 0;
            player.SeasonRatingSum = 0d;
            player.SeasonRatedMatches = 0;
            player.Married = false;
            player.Children = 0;
            player.ContractSalary = 0;
        }

        static void AgeSquad(List<Player> roster)
        {
            if (roster == null) return;
            for (int i = 0; i < roster.Count; i++)
            {
                var player = roster[i];
                if (player == null) continue;
                player.Age++;
                player.Phase = WorldFactory.PhaseForAge(player.Age);
            }
        }

        static void RetireAged(FrontOffice front)
        {
            if (front == null) return;
            front.Secretary = ClearIfRetired(front.Secretary);
            front.GeneralManager = ClearIfRetired(front.GeneralManager);
            ClearRetired(front.Strengthening);
            ClearRetired(front.Sales);
            ClearRetired(front.PublicRelations);
            ClearRetired(front.Business);
            ClearRetired(front.Academy);
        }

        static void RetireAged(TeamStaff team)
        {
            if (team == null) return;
            team.Manager = ClearIfRetired(team.Manager);
            team.HeadCoach = ClearIfRetired(team.HeadCoach);
            ClearRetired(team.Coaches);
            team.PhysicalCoach = ClearIfRetired(team.PhysicalCoach);
            ClearRetired(team.Trainers);
            team.Doctor = ClearIfRetired(team.Doctor);
        }

        static void ClearRetired(StaffMember[] slots)
        {
            if (slots == null) return;
            for (int i = 0; i < slots.Length; i++)
                slots[i] = ClearIfRetired(slots[i]);
        }

        static StaffMember ClearIfRetired(StaffMember staff)
        {
            if (staff == null) return null;
            staff.Age++;
            return staff.Age >= ClubMath.RetirementAge ? null : staff;
        }

        int LeagueRank(Club club)
        {
            if (club.Wins + club.Draws + club.Losses == 0) return 0;
            var table = League.Table();
            for (int i = 0; i < table.Count; i++)
            {
                if (table[i] == club) return i + 1;
            }

            return 0;
        }

        /// <summary>契約の無い所属選手に、いまの実力の年俸で契約を付ける。加入直後とセーブ移行用。</summary>
        void EnsureContracts()
        {
            for (int c = 0; c < Clubs.Count; c++)
            {
                var squad = Clubs[c] != null ? Clubs[c].Squad : null;
                if (squad == null) continue;
                for (int i = 0; i < squad.Count; i++)
                {
                    var player = squad[i];
                    if (player != null && player.ContractSalary <= 0)
                        player.ContractSalary = ClubMath.PlayerAnnual(player);
                }
            }
        }

        static void RenewContracts(List<Player> squad)
        {
            if (squad == null) return;
            for (int i = 0; i < squad.Count; i++)
            {
                var player = squad[i];
                if (player != null) player.ContractSalary = ClubMath.RenewedContract(player);
            }
        }

        /// <summary>リーグ最終順位の賞金。そのシーズンのリーグLVの倍率をかける。</summary>
        void PayLeaguePrizes(List<Club> table)
        {
            if (table == null) return;
            double strength = LeagueStrengthFactor;
            for (int i = 0; i < table.Count; i++)
            {
                var prize = ClubMath.LeaguePrize(i + 1, strength);
                if (prize > 0 && table[i] != null) Economy.Apply(table[i], prize, "prize");
            }
        }

        void RefillFrontMarket()
        {
            _frontMarket.Clear();
            int floor = PlayerClub.Facilities.MarketLevelFloor();
            int cap = PlayerClub.Facilities.MarketLevelCap();
            var roles = new[]
            {
                FrontRole.Secretary, FrontRole.GeneralManager,
                FrontRole.Strengthening, FrontRole.Strengthening,
                FrontRole.Sales, FrontRole.Sales,
                FrontRole.PublicRelations, FrontRole.PublicRelations,
                FrontRole.Business, FrontRole.Business,
                FrontRole.Academy, FrontRole.Academy
            };
            for (int i = 0; i < roles.Length; i++)
            {
                _frontMarket.Add(WorldFactory.CreateFrontCandidate(
                    _random,
                    "front-" + Time.CurrentDate.Ticks + "-" + i,
                    roles[i],
                    floor,
                    cap));
            }
        }
    }
}
