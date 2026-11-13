using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;
using SoccerClub.Competition;
using SoccerClub.Data;
using SoccerClub.Economy;
using SoccerClub.Match;

namespace SoccerClub.Core
{
    /// <summary>1シーズン分の結果。8月1日の朝（新シーズンを組む前）に記録する。</summary>
    public sealed class SimulationSeasonRecord
    {
        public int Season;
        public int StartYear;
        public int LeagueLevel;
        public int Rank;
        public int Points;
        public int Wins;
        public int Draws;
        public int Losses;
        public int GoalsFor;
        public int GoalsAgainst;
        public string Champion;
        public bool WonLeague;
        public string CupWinner;
        public bool WonCup;
        public BigInteger Coins;
        /// <summary>強化費（補強用に分けた資金）の残高。</summary>
        public BigInteger Reinforcement;
        public BigInteger Goods;
        public BigInteger SigningSpend;
        public BigInteger Payroll;
        public BigInteger Income;
        public BigInteger Expense;
        public int StadiumLevel;
        public int TrainingGroundLevel;
        public int TrainingFacilityLevel;
        public int ClubhouseLevel;
        public int StadiumUps;
        public int TrainingGroundUps;
        public int TrainingFacilityUps;
        public int ClubhouseUps;
        public int FrontHires;
        public int FrontFilled;
        public double FrontAverageLevel;
        public int FrontMinLevel;
        public int SquadCount;
        public int SquadAverageOverall;
        public int CoreSupporters;
        public int LightSupporters;
        public bool Bankrupt;
        public int HomeAverageAttendance;
        public int Capacity;
        public int TicketPrice;
        public BigInteger Gate;
        public BigInteger Operating;
        public BigInteger SponsorIncome;
        public int SponsorCount;
        public BigInteger SponsorAverageMonthly;
        public int PlayerTop11;
        public int RivalTop11Average;
        public int RivalTop11Max;
        public int ChampionTop11;
        public int FreeAgentTop11;

        // 診断用（自クラブ）
        public int XiCount;
        public int XiOffense;
        public int XiDefense;
        public int RivalXiOffense;
        public int RivalXiDefense;
        public int TeamStaffFilled;
        public bool HasManager;
        public int AverageCondition;
        public int Injured;
        public int Goalkeepers;
        public int SquadOffense;
        public int SquadDefense;
    }

    public sealed class SimulationSettings
    {
        public int PlayerClubIndex;
        public int? Seed;
        public int Seasons = 100;
        /// <summary>フロントの空き枠を、レベル0のスタッフがいるとみなして採用するか。</summary>
        public bool FillEmptyFront = true;

        /// <summary>優勝で相手が強くなる倍率（調整用）。</summary>
        public double LeagueStep = GameSession.DefaultLeagueLevelStep;

        /// <summary>練習場・トレーニング室の費用の伸び（千分率、調整用）。</summary>
        public int TrainingCostGrowthPermil = Facility.DefaultTrainingCostGrowthPermil;

        /// <summary>クラブハウスの費用の伸び（千分率、調整用）。</summary>
        public int ClubhouseCostGrowthPermil = Facility.DefaultClubhouseCostGrowthPermil;

        /// <summary>他クラブの強さの基準倍率（調整用）。</summary>
        public double RivalScale = GameSession.DefaultRivalStrengthScale;

        /// <summary>成長施設の基本価格の倍率（調整用）。</summary>
        public double BaseScale = Facility.DefaultGrowthFacilityBaseScale;

        /// <summary>無冠が続いたときの他クラブの弱まり（調整用）。</summary>
        public double Relief = GameSession.DefaultRivalDroughtRelief;

        /// <summary>連覇が続いたときの他クラブの強まり（調整用）。</summary>
        public double Streak = GameSession.DefaultRivalStreakBoost;

        /// <summary>成長施設の上限「リーグLV＋この値」（調整用、0 で無制限）。</summary>
        public int Allowance = Facility.DefaultLeagueLevelAllowance;
    }

    /// <summary>
    /// 画面と演出を通さず、GameSession の処理だけで日を進める。
    /// GameRunner のフルオート進行と同じ順序で呼ぶ（朝イベント → 投資 → 練習抽選 → 試合 → 日送り → スポンサー）。
    /// </summary>
    public sealed class HeadlessSimulator
    {
        const int EventGuard = 32;
        const int StoryStepGuard = 16;

        public GameSession Session { get; }
        public SimulationSettings Settings { get; }
        public List<SimulationSeasonRecord> Records { get; } = new List<SimulationSeasonRecord>();
        public int DaysSimulated { get; private set; }
        public bool Finished => Session.GameOver || Records.Count >= Settings.Seasons;

        int _stadiumUps;
        int _groundUps;
        int _roomUps;
        int _houseUps;
        int _frontHires;
        BigInteger _income;
        BigInteger _expense;
        BigInteger _gate;
        BigInteger _operating;
        BigInteger _sponsor;
        BigInteger _goods;
        BigInteger _signing;
        DateTime _lastRecorded = DateTime.MinValue;

        public HeadlessSimulator(SimulationSettings settings)
        {
            Settings = settings ?? new SimulationSettings();
            GameSession.LeagueLevelStep = Settings.LeagueStep > 1d ? Settings.LeagueStep : GameSession.DefaultLeagueLevelStep;
            Facility.TrainingCostGrowthPermil = Settings.TrainingCostGrowthPermil > 1000
                ? Settings.TrainingCostGrowthPermil : Facility.DefaultTrainingCostGrowthPermil;
            Facility.GrowthFacilityBaseScale = Settings.BaseScale > 0d ? Settings.BaseScale : Facility.DefaultGrowthFacilityBaseScale;
            Facility.LeagueLevelAllowance = Settings.Allowance >= 0 ? Settings.Allowance : Facility.DefaultLeagueLevelAllowance;
            GameSession.RivalStreakBoost = Settings.Streak >= 0d && Settings.Streak < 1d ? Settings.Streak : GameSession.DefaultRivalStreakBoost;
            GameSession.RivalDroughtRelief = Settings.Relief >= 0d && Settings.Relief < 1d ? Settings.Relief : GameSession.DefaultRivalDroughtRelief;
            GameSession.RivalStrengthScale = Settings.RivalScale > 0d ? Settings.RivalScale : GameSession.DefaultRivalStrengthScale;
            Facility.ClubhouseCostGrowthPermil = Settings.ClubhouseCostGrowthPermil > 1000
                ? Settings.ClubhouseCostGrowthPermil : Facility.DefaultClubhouseCostGrowthPermil;
            Session = GameSession.CreateNew(Settings.PlayerClubIndex, Settings.Seed);
            Session.Economy.OnLedger += NoteLedger;
        }

        /// <summary>1日進める。終了していれば false。</summary>
        public bool StepDay()
        {
            if (Finished) return false;
            var today = Session.Time.CurrentDate.Date;

            // 8月1日の朝、前シーズンの成績を記録する（ロールオーバーは当日の AdvanceDay で起きる）。
            if (today.Month == 8 && today.Day == 1 && today > GameSession.SeasonStart && _lastRecorded != today)
            {
                _lastRecorded = today;
                Records.Add(Capture(false));
                if (Finished) return false;
            }

            RunMorningEvents();
            Count(Session.SpendForSimulation(Settings.FillEmptyFront));

            Session.ClearSeasonNotice();
            Session.RollDailyGrowth();
            Session.ResolveTodayMatches();
            var played = Session.Time.CurrentDate;
            Session.AdvanceDay();
            DaysSimulated++;

            Session.TryCreateSponsorOffer(played);
            if (Session.HasMonthReport) Session.DismissMonthReport();
            if (Session.HasSponsorCut) Session.DismissSponsorCut();
            if (Session.HasSponsorOffer) Session.AcceptSponsorOffer();

            if (Session.GameOver)
            {
                Records.Add(Capture(true));
                return false;
            }

            return !Finished;
        }

        void RunMorningEvents()
        {
            for (int guard = 0; guard < EventGuard && Session.TryOpenMorningEvent(); guard++)
            {
                var story = Session.CurrentStory;
                if (story == null) break;
                Session.ApplyOpenStory();
                for (int step = 0; step < StoryStepGuard && Session.ContinueStory(); step++)
                {
                }

                if (Session.CurrentStory != null) Session.CloseStory();
            }
        }

        void Count(GameSession.SimulationSpend spend)
        {
            _stadiumUps += spend.Stadium;
            _groundUps += spend.TrainingGround;
            _roomUps += spend.TrainingFacility;
            _houseUps += spend.Clubhouse;
            _frontHires += spend.Front;
        }

        void NoteLedger(Club club, LedgerEntry entry)
        {
            if (club != Session.PlayerClub || entry.Amount == 0) return;
            if (entry.Amount > 0) _income += entry.Amount;
            else _expense -= entry.Amount;
            if (entry.Reason == "gate") _gate += entry.Amount;
            else if (entry.Reason == "operating") _operating -= entry.Amount;
            else if (entry.Reason == "sponsor") _sponsor += entry.Amount;
            else if (entry.Reason == "goods") _goods += entry.Amount;
            else if (entry.Amount < 0 && entry.Reason != null && entry.Reason.StartsWith("sign-")) _signing -= entry.Amount;
        }

        SimulationSeasonRecord Capture(bool bankrupt)
        {
            var club = Session.PlayerClub;
            var table = Session.League.Table();
            int rank = table.IndexOf(club) + 1;
            var champion = table.Count > 0 ? table[0] : null;
            var cupWinner = CupWinner(Session.Cup != null ? Session.Cup.Fixtures : null);
            var facilities = club.Facilities;

            var record = new SimulationSeasonRecord
            {
                Season = Records.Count + 1,
                StartYear = Session.CurrentSeasonStart.Year,
                LeagueLevel = Session.LeagueLevel,
                Rank = rank,
                Points = club.LeaguePoints,
                Wins = club.Wins,
                Draws = club.Draws,
                Losses = club.Losses,
                GoalsFor = club.GoalsFor,
                GoalsAgainst = club.GoalsAgainst,
                Champion = champion != null ? champion.Name : "",
                WonLeague = champion == club && Session.SimulationLeagueComplete(),
                CupWinner = cupWinner != null ? cupWinner.Name : "",
                WonCup = cupWinner == club,
                Coins = club.Coins,
                Reinforcement = club.Reinforcement,
                Goods = _goods,
                SigningSpend = _signing,
                Payroll = ClubMath.RenewalAnnual(club),
                Income = _income,
                Expense = _expense,
                StadiumLevel = facilities.Stadium.Level,
                TrainingGroundLevel = facilities.TrainingGround.Level,
                TrainingFacilityLevel = facilities.TrainingFacility.Level,
                ClubhouseLevel = facilities.Clubhouse.Level,
                StadiumUps = _stadiumUps,
                TrainingGroundUps = _groundUps,
                TrainingFacilityUps = _roomUps,
                ClubhouseUps = _houseUps,
                FrontHires = _frontHires,
                SquadCount = club.Squad != null ? club.Squad.Count : 0,
                SquadAverageOverall = AverageOverall(club.Squad),
                CoreSupporters = club.CoreSupporters,
                LightSupporters = club.LightSupporters,
                Bankrupt = bankrupt,
                HomeAverageAttendance = AverageHomeAttendance(club),
                Capacity = ClubMath.Capacity(club),
                TicketPrice = ClubMath.TicketYen(club),
                Gate = _gate,
                Operating = _operating,
                SponsorIncome = _sponsor
            };
            SponsorStats(club, out record.SponsorCount, out record.SponsorAverageMonthly);
            StrengthStats(record, club, champion);
            Diagnose(record, club);
            FrontStats(club.Front, out record.FrontFilled, out record.FrontAverageLevel, out record.FrontMinLevel);

            _stadiumUps = _groundUps = _roomUps = _houseUps = _frontHires = 0;
            _income = BigInteger.Zero;
            _expense = BigInteger.Zero;
            _gate = BigInteger.Zero;
            _operating = BigInteger.Zero;
            _sponsor = BigInteger.Zero;
            _goods = BigInteger.Zero;
            _signing = BigInteger.Zero;
            return record;
        }

        /// <summary>上位11人の平均総合力（表示値）。対戦相手との力関係を見るため。</summary>
        static int Top11(List<Player> roster)
        {
            if (roster == null || roster.Count == 0) return 0;
            var list = new List<double>(roster.Count);
            for (int i = 0; i < roster.Count; i++)
            {
                if (roster[i] != null) list.Add(roster[i].Overall);
            }

            if (list.Count == 0) return 0;
            list.Sort((a, b) => b.CompareTo(a));
            int n = Math.Min(11, list.Count);
            double sum = 0d;
            for (int i = 0; i < n; i++) sum += list[i];
            return Player.ToDisplay(sum / n);
        }

        void StrengthStats(SimulationSeasonRecord record, Club club, Club champion)
        {
            record.PlayerTop11 = Top11(club.Squad);
            record.ChampionTop11 = champion != null ? Top11(champion.Squad) : 0;
            long sum = 0;
            int count = 0;
            int max = 0;
            for (int i = 0; i < Session.Clubs.Count; i++)
            {
                var rival = Session.Clubs[i];
                if (rival == null || rival == club) continue;
                int top = Top11(rival.Squad);
                sum += top;
                count++;
                if (top > max) max = top;
            }

            record.RivalTop11Average = count == 0 ? 0 : (int)(sum / count);
            record.RivalTop11Max = max;
            record.FreeAgentTop11 = Top11(new List<Player>(Session.SimulationFreeAgents));
        }

        static readonly ManagerLineupAi DiagnosticLineup = new ManagerLineupAi();

        /// <summary>シーズン終了時点のスタメン（監督AI）の攻撃・守備合計（表示値）。</summary>
        static void XiPower(Club club, out int count, out double offense, out double defense)
        {
            count = 0;
            offense = 0d;
            defense = 0d;
            if (club == null) return;
            var sheet = DiagnosticLineup.BuildSheet(club);
            if (sheet == null || sheet.StartingXi == null) return;
            for (int i = 0; i < sheet.StartingXi.Length; i++)
            {
                var slot = sheet.StartingXi[i];
                if (slot == null || slot.Player == null) continue;
                count++;
                offense += slot.Player.Offense * PositionUtility.Multiplier(slot.Player.SuitabilityAt(slot.Slot));
                defense += slot.Player.Defense * PositionUtility.Multiplier(slot.Player.SuitabilityAt(slot.Slot));
            }
        }

        void Diagnose(SimulationSeasonRecord record, Club club)
        {
            XiPower(club, out record.XiCount, out var off, out var def);
            record.XiOffense = Player.ToDisplay(off);
            record.XiDefense = Player.ToDisplay(def);

            double rOff = 0d, rDef = 0d;
            int rivals = 0;
            for (int i = 0; i < Session.Clubs.Count; i++)
            {
                var rival = Session.Clubs[i];
                if (rival == null || rival == club) continue;
                XiPower(rival, out _, out var o, out var d);
                rOff += o;
                rDef += d;
                rivals++;
            }

            record.RivalXiOffense = rivals == 0 ? 0 : Player.ToDisplay(rOff / rivals);
            record.RivalXiDefense = rivals == 0 ? 0 : Player.ToDisplay(rDef / rivals);

            var team = club.Team;
            int filled = 0;
            if (team != null)
            {
                if (team.Manager != null) filled++;
                if (team.HeadCoach != null) filled++;
                if (team.PhysicalCoach != null) filled++;
                if (team.Doctor != null) filled++;
                if (team.Coaches != null) foreach (var s in team.Coaches) if (s != null) filled++;
                if (team.Trainers != null) foreach (var s in team.Trainers) if (s != null) filled++;
            }

            record.TeamStaffFilled = filled;
            record.HasManager = team != null && team.Manager != null;

            long cond = 0;
            int n = 0;
            double sOff = 0d, sDef = 0d;
            if (club.Squad != null)
            {
                foreach (var p in club.Squad)
                {
                    if (p == null) continue;
                    cond += p.Condition;
                    n++;
                    if (p.InjuryDays > 0) record.Injured++;
                    if (p.PreferredPosition == Position.GK) record.Goalkeepers++;
                    sOff += p.Offense;
                    sDef += p.Defense;
                }
            }

            record.AverageCondition = n == 0 ? 0 : (int)(cond / n);
            record.SquadOffense = n == 0 ? 0 : Player.ToDisplay(sOff / n);
            record.SquadDefense = n == 0 ? 0 : Player.ToDisplay(sDef / n);
        }

        static void SponsorStats(Club club, out int count, out BigInteger average)
        {
            count = 0;
            average = BigInteger.Zero;
            if (club == null || club.Sponsors == null) return;
            var sum = BigInteger.Zero;
            for (int i = 0; i < club.Sponsors.Count; i++)
            {
                var contract = club.Sponsors[i];
                if (contract == null || contract.MonthsLeft <= 0) continue;
                sum += contract.Monthly;
                count++;
            }

            if (count > 0) average = sum / count;
        }

        int AverageHomeAttendance(Club club)
        {
            long sum = 0;
            int count = 0;
            AddHome(Session.League != null ? Session.League.Fixtures : null, club, ref sum, ref count);
            AddHome(Session.Cup != null ? Session.Cup.Fixtures : null, club, ref sum, ref count);
            return count == 0 ? 0 : (int)(sum / count);
        }

        static void AddHome(IReadOnlyList<Fixture> fixtures, Club club, ref long sum, ref int count)
        {
            if (fixtures == null) return;
            for (int i = 0; i < fixtures.Count; i++)
            {
                var f = fixtures[i];
                if (f == null || f.Home != club || f.Result == null) continue;
                sum += f.Attendance;
                count++;
            }
        }

        static int AverageOverall(List<Player> squad)
        {
            if (squad == null) return 0;
            double sum = 0d;
            int count = 0;
            for (int i = 0; i < squad.Count; i++)
            {
                if (squad[i] == null) continue;
                sum += squad[i].Overall;
                count++;
            }

            return count == 0 ? 0 : Player.ToDisplay(sum / count);
        }

        static void FrontStats(FrontOffice front, out int filled, out double average, out int min)
        {
            filled = 0;
            average = 0d;
            min = 0;
            if (front == null) return;
            int sum = 0;
            int lowest = int.MaxValue;
            for (int slot = 0; slot < FrontOffice.SlotCount; slot++)
            {
                var staff = front.GetAt(slot);
                int level = staff != null ? staff.Level : 0;
                if (staff != null) filled++;
                sum += level;
                if (level < lowest) lowest = level;
            }

            average = (double)sum / FrontOffice.SlotCount;
            min = lowest == int.MaxValue ? 0 : lowest;
        }

        /// <summary>最終ラウンドの2クラブを合計得点で比べる。PK はゲーム側で第2戦の得点に1足してある。</summary>
        static Club CupWinner(IReadOnlyList<Fixture> fixtures)
        {
            if (fixtures == null || fixtures.Count == 0) return null;
            int finalRound = 0;
            for (int i = 0; i < fixtures.Count; i++)
            {
                if (fixtures[i] != null && fixtures[i].CupRound > finalRound) finalRound = fixtures[i].CupRound;
            }

            Club a = null;
            Club b = null;
            int goalsA = 0;
            int goalsB = 0;
            for (int i = 0; i < fixtures.Count; i++)
            {
                var fixture = fixtures[i];
                if (fixture == null || fixture.CupRound != finalRound) continue;
                if (fixture.Result == null) return null;
                if (a == null)
                {
                    a = fixture.Home;
                    b = fixture.Away;
                }

                if ((fixture.Home != a || fixture.Away != b) && (fixture.Home != b || fixture.Away != a))
                    return null; // 最終ラウンドに複数カードがある＝まだ決勝ではない
                goalsA += fixture.Home == a ? fixture.Result.HomeGoals : fixture.Result.AwayGoals;
                goalsB += fixture.Home == b ? fixture.Result.HomeGoals : fixture.Result.AwayGoals;
            }

            if (a == null || goalsA == goalsB) return null;
            return goalsA > goalsB ? a : b;
        }

        public string BuildCsv()
        {
            var sb = new StringBuilder();
            sb.AppendLine("season,start_year,league_lv,rank,points,w,d,l,gf,ga,champion,won_league,cup_winner,won_cup,"
                + "coins,payroll,income,expense,stadium_lv,ground_lv,room_lv,clubhouse_lv,"
                + "stadium_up,ground_up,room_up,clubhouse_up,front_hires,front_filled,front_avg_lv,front_min_lv,"
                + "squad,squad_avg_overall,core_supporters,light_supporters,bankrupt,"
                + "home_avg_attendance,capacity,ticket,gate,operating,sponsor_income,sponsor_count,sponsor_avg_monthly,"
                + "player_top11,rival_top11_avg,rival_top11_max,champion_top11,free_agent_top11,"
                + "xi_count,xi_offense,xi_defense,rival_xi_offense,rival_xi_defense,team_staff,has_manager,avg_condition,injured,goalkeepers,squad_offense,squad_defense,reinforcement,goods,signing_spend");
            var inv = CultureInfo.InvariantCulture;
            for (int i = 0; i < Records.Count; i++)
            {
                var r = Records[i];
                sb.Append(r.Season.ToString(inv)).Append(',')
                    .Append(r.StartYear.ToString(inv)).Append(',')
                    .Append(r.LeagueLevel.ToString(inv)).Append(',')
                    .Append(r.Rank.ToString(inv)).Append(',')
                    .Append(r.Points.ToString(inv)).Append(',')
                    .Append(r.Wins.ToString(inv)).Append(',')
                    .Append(r.Draws.ToString(inv)).Append(',')
                    .Append(r.Losses.ToString(inv)).Append(',')
                    .Append(r.GoalsFor.ToString(inv)).Append(',')
                    .Append(r.GoalsAgainst.ToString(inv)).Append(',')
                    .Append(Csv(r.Champion)).Append(',')
                    .Append(r.WonLeague ? "1" : "0").Append(',')
                    .Append(Csv(r.CupWinner)).Append(',')
                    .Append(r.WonCup ? "1" : "0").Append(',')
                    .Append(r.Coins.ToString(inv)).Append(',')
                    .Append(r.Payroll.ToString(inv)).Append(',')
                    .Append(r.Income.ToString(inv)).Append(',')
                    .Append(r.Expense.ToString(inv)).Append(',')
                    .Append(r.StadiumLevel.ToString(inv)).Append(',')
                    .Append(r.TrainingGroundLevel.ToString(inv)).Append(',')
                    .Append(r.TrainingFacilityLevel.ToString(inv)).Append(',')
                    .Append(r.ClubhouseLevel.ToString(inv)).Append(',')
                    .Append(r.StadiumUps.ToString(inv)).Append(',')
                    .Append(r.TrainingGroundUps.ToString(inv)).Append(',')
                    .Append(r.TrainingFacilityUps.ToString(inv)).Append(',')
                    .Append(r.ClubhouseUps.ToString(inv)).Append(',')
                    .Append(r.FrontHires.ToString(inv)).Append(',')
                    .Append(r.FrontFilled.ToString(inv)).Append(',')
                    .Append(r.FrontAverageLevel.ToString("0.0", inv)).Append(',')
                    .Append(r.FrontMinLevel.ToString(inv)).Append(',')
                    .Append(r.SquadCount.ToString(inv)).Append(',')
                    .Append(r.SquadAverageOverall.ToString(inv)).Append(',')
                    .Append(r.CoreSupporters.ToString(inv)).Append(',')
                    .Append(r.LightSupporters.ToString(inv)).Append(',')
                    .Append(r.Bankrupt ? "1" : "0").Append(',')
                    .Append(r.HomeAverageAttendance.ToString(inv)).Append(',')
                    .Append(r.Capacity.ToString(inv)).Append(',')
                    .Append(r.TicketPrice.ToString(inv)).Append(',')
                    .Append(r.Gate.ToString(inv)).Append(',')
                    .Append(r.Operating.ToString(inv)).Append(',')
                    .Append(r.SponsorIncome.ToString(inv)).Append(',')
                    .Append(r.SponsorCount.ToString(inv)).Append(',')
                    .Append(r.SponsorAverageMonthly.ToString(inv)).Append(',')
                    .Append(r.PlayerTop11.ToString(inv)).Append(',')
                    .Append(r.RivalTop11Average.ToString(inv)).Append(',')
                    .Append(r.RivalTop11Max.ToString(inv)).Append(',')
                    .Append(r.ChampionTop11.ToString(inv)).Append(',')
                    .Append(r.FreeAgentTop11.ToString(inv)).Append(',')
                    .Append(r.XiCount.ToString(inv)).Append(',')
                    .Append(r.XiOffense.ToString(inv)).Append(',')
                    .Append(r.XiDefense.ToString(inv)).Append(',')
                    .Append(r.RivalXiOffense.ToString(inv)).Append(',')
                    .Append(r.RivalXiDefense.ToString(inv)).Append(',')
                    .Append(r.TeamStaffFilled.ToString(inv)).Append(',')
                    .Append(r.HasManager ? "1" : "0").Append(',')
                    .Append(r.AverageCondition.ToString(inv)).Append(',')
                    .Append(r.Injured.ToString(inv)).Append(',')
                    .Append(r.Goalkeepers.ToString(inv)).Append(',')
                    .Append(r.SquadOffense.ToString(inv)).Append(',')
                    .Append(r.SquadDefense.ToString(inv)).Append(',')
                    .Append(r.Reinforcement.ToString(inv)).Append(',')
                    .Append(r.Goods.ToString(inv)).Append(',')
                    .Append(r.SigningSpend.ToString(inv))
                    .AppendLine();
            }

            return sb.ToString();
        }

        public string BuildSummary()
        {
            int leagueWins = 0;
            int cupWins = 0;
            int firstLeague = 0;
            for (int i = 0; i < Records.Count; i++)
            {
                if (Records[i].WonLeague)
                {
                    leagueWins++;
                    if (firstLeague == 0) firstLeague = Records[i].Season;
                }

                if (Records[i].WonCup) cupWins++;
            }

            var last = Records.Count > 0 ? Records[Records.Count - 1] : null;
            var sb = new StringBuilder();
            sb.Append("シーズン ").Append(Records.Count).Append(" / ").Append(Settings.Seasons)
                .Append("  日数 ").Append(DaysSimulated).AppendLine();
            sb.Append("リーグ優勝 ").Append(leagueWins).Append("回（初優勝 ")
                .Append(firstLeague > 0 ? firstLeague + "年目" : "なし").Append("）  カップ優勝 ")
                .Append(cupWins).AppendLine("回");
            if (last != null)
            {
                sb.Append("最終 リーグLV ").Append(last.LeagueLevel)
                    .Append("  資金 ").Append(MoneyText.Format(last.Coins))
                    .Append("  総年俸 ").Append(MoneyText.Format(last.Payroll)).AppendLine();
                sb.Append("施設 スタジアム").Append(last.StadiumLevel)
                    .Append(" 練習場").Append(last.TrainingGroundLevel)
                    .Append(" トレーニング室").Append(last.TrainingFacilityLevel)
                    .Append(" クラブハウス").Append(last.ClubhouseLevel)
                    .Append("  フロント平均Lv ").Append(last.FrontAverageLevel.ToString("0.0", CultureInfo.InvariantCulture))
                    .AppendLine();
            }

            if (Session.GameOver) sb.AppendLine("※ 破産でゲームオーバー");
            return sb.ToString();
        }

        static string Csv(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            if (text.IndexOf(',') < 0 && text.IndexOf('"') < 0) return text;
            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }
    }
}
