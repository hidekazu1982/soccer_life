using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;
using SoccerClub.Competition;
using SoccerClub.Data;
using SoccerClub.Economy;

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
    }

    public sealed class SimulationSettings
    {
        public int PlayerClubIndex;
        public int? Seed;
        public int Seasons = 100;
        /// <summary>フロントの空き枠を、レベル0のスタッフがいるとみなして採用するか。</summary>
        public bool FillEmptyFront = true;
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
        DateTime _lastRecorded = DateTime.MinValue;

        public HeadlessSimulator(SimulationSettings settings)
        {
            Settings = settings ?? new SimulationSettings();
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
                if (story.Kind == ClubStoryKind.Stadium && Session.SimulationAcceptsStadium()
                    && Session.OwnerApprovesFacilityUpgrade(FacilityKind.Stadium))
                    _stadiumUps++;

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
                Operating = _operating
            };
            FrontStats(club.Front, out record.FrontFilled, out record.FrontAverageLevel, out record.FrontMinLevel);

            _stadiumUps = _groundUps = _roomUps = _houseUps = _frontHires = 0;
            _income = BigInteger.Zero;
            _expense = BigInteger.Zero;
            _gate = BigInteger.Zero;
            _operating = BigInteger.Zero;
            return record;
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
                + "home_avg_attendance,capacity,ticket,gate,operating");
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
                    .Append(r.Operating.ToString(inv))
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
