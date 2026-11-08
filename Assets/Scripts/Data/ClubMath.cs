using System;
using System.Numerics;
using SoccerClub.Core;

namespace SoccerClub.Data
{
    /// <summary>版 1.3 の金額、レベル係数、サポーター、練習倍率。</summary>
    public static class ClubMath
    {
        public const int BudgetStep = 1000;
        public const int CoreFloor = 100;
        public const int LightFloor = 200;
        public const int MaxStaffLevel = 999;
        public const int RetirementAge = 70;

        /// <summary>無所属はベテラン（32歳）になる年に引退し、18歳へ戻る。</summary>
        public const int FreeAgentMaxAge = 31;
        public const int SquadLimit = 25;
        public const int MinimumSquad = 15;
        public const int AcademyLimit = 8;

        public static double LevelFactor(int level)
        {
            if (level <= 0) return 0d;
            return 1d - Math.Pow(0.997d, level);
        }

        /// <summary>GM の交渉成功率。レベル 24 で 0.77、上限 0.95、下限 0.05。</summary>
        public static double NegotiationChance(int gmLevel)
        {
            if (gmLevel < 0) gmLevel = 0;
            double chance = 0.05d + gmLevel * 0.03d;
            if (chance < 0.05d) chance = 0.05d;
            if (chance > 0.95d) chance = 0.95d;
            return chance;
        }

        public static int RoundInt(double value)
        {
            return (int)Math.Round(value, MidpointRounding.AwayFromZero);
        }

        public static BigInteger RoundCoins(double value)
        {
            if (double.IsNaN(value) || value <= 0d) return BigInteger.Zero;
            return new BigInteger(Math.Round(value, MidpointRounding.AwayFromZero));
        }

        public static BigInteger FloorCoins(double value)
        {
            if (double.IsNaN(value) || value <= 0d) return BigInteger.Zero;
            return new BigInteger(Math.Floor(value));
        }

        public static double ToDouble(BigInteger value)
        {
            if (value > long.MaxValue) return long.MaxValue;
            if (value < long.MinValue) return long.MinValue;
            return (long)value;
        }

        public static int StaffBase(StaffMember staff)
        {
            if (staff == null) return 0;
            if (staff.IsFrontOffice)
            {
                switch (staff.FrontRole)
                {
                    case FrontRole.Secretary:
                        return 1_200_000;
                    case FrontRole.GeneralManager:
                        return staff.Policy == ReinforcementPolicy.Manager ? 4_800_000 : 3_000_000;
                    default:
                        return 800_000;
                }
            }

            switch (staff.TeamRole)
            {
                case TeamStaffRole.Manager: return 4_000_000;
                case TeamStaffRole.HeadCoach: return 2_000_000;
                case TeamStaffRole.Coach: return 1_250_000;
                case TeamStaffRole.PhysicalCoach: return 1_250_000;
                case TeamStaffRole.Trainer: return 1_000_000;
                case TeamStaffRole.Doctor: return 1_500_000;
                default: return 1_000_000;
            }
        }

        public static BigInteger StaffAnnual(StaffMember staff)
        {
            if (staff == null) return BigInteger.Zero;
            return StaffAnnual(StaffBase(staff), staff.Level);
        }

        public static BigInteger StaffAnnual(int baseAmount, int level)
        {
            return RoundCoins(baseAmount * (10d + level) / 10d);
        }

        /// <summary>現在所属している選手、チームスタッフ、フロントの年俸合計。</summary>
        public static BigInteger RenewalAnnual(Club club)
        {
            if (club == null) return BigInteger.Zero;
            var total = BigInteger.Zero;
            var squad = club.Squad;
            if (squad != null)
            {
                for (int i = 0; i < squad.Count; i++)
                {
                    if (squad[i] != null) total += squad[i].AnnualSalary;
                }
            }

            AddTeam(ref total, club.Team);
            AddFront(ref total, club.Front);
            return total;
        }

        static void AddTeam(ref BigInteger total, TeamStaff team)
        {
            if (team == null) return;
            AddStaff(ref total, team.Manager);
            AddStaff(ref total, team.HeadCoach);
            AddStaff(ref total, team.PhysicalCoach);
            AddStaff(ref total, team.Doctor);
            AddStaffSlots(ref total, team.Coaches);
            AddStaffSlots(ref total, team.Trainers);
        }

        static void AddFront(ref BigInteger total, FrontOffice front)
        {
            if (front == null) return;
            for (int i = 0; i < FrontOffice.SlotCount; i++)
                AddStaff(ref total, front.GetAt(i));
        }

        static void AddStaffSlots(ref BigInteger total, StaffMember[] slots)
        {
            if (slots == null) return;
            for (int i = 0; i < slots.Length; i++)
                AddStaff(ref total, slots[i]);
        }

        static void AddStaff(ref BigInteger total, StaffMember staff)
        {
            if (staff != null) total += staff.AnnualSalary;
        }

        /// <summary>移籍金の年齢倍率。年俸は年齢を掛けない。</summary>
        public static void PlayerRates(int age, out double wageFactor, out double feeMultiple)
        {
            if (age <= 21)
            {
                wageFactor = 0.50d;
                feeMultiple = 8d;
            }
            else if (age <= 28)
            {
                wageFactor = 1d;
                feeMultiple = 5d;
            }
            else if (age <= 31)
            {
                wageFactor = 1.15d;
                feeMultiple = 3d;
            }
            else if (age <= 34)
            {
                wageFactor = 0.90d;
                feeMultiple = 1.5d;
            }
            else
            {
                wageFactor = 0.70d;
                feeMultiple = 1.5d;
            }
        }

        /// <summary>表示総合力と同じ万円。総合力 1000 は年俸 1000 万円。年齢は掛けない。</summary>
        public static BigInteger PlayerAnnual(Player player)
        {
            if (player == null) return BigInteger.Zero;
            int overall = player.DisplayOverall;
            if (overall <= 0) return BigInteger.Zero;
            return new BigInteger(overall) * 10_000;
        }

        public static BigInteger PlayerMonthly(Player player)
        {
            return PlayerAnnual(player) / 12;
        }

        /// <summary>出場給、勝利給。どちらも年俸の 1%。</summary>
        public static BigInteger MatchBonus(Player player)
        {
            if (player == null) return BigInteger.Zero;
            return PlayerAnnual(player) / 100;
        }

        public static BigInteger TransferFee(Player player)
        {
            PlayerRates(player.Age, out _, out var multiple);
            return RoundCoins(ToDouble(PlayerAnnual(player)) * multiple);
        }

        public static int Capacity(Club club)
        {
            var value = club.Facilities.StadiumCapacity();
            if (value > int.MaxValue) return int.MaxValue;
            return (int)value;
        }

        /// <summary>練習で足す基礎点。施設の +n% をこの値にかける。</summary>
        public const double TrainingBaseAdd = 10d;

        public const double TrainingPercentAtLevel10 = 1d;
        public const double TrainingPercentStep = 1d;

        /// <summary>得意ポジションの選手にかけるコーチ効果の倍率。</summary>
        public const double CoachSpecialtyMultiplier = 1.5d;

        /// <summary>
        /// 練習場・トレーニング室・コーチの表示加算。Lv10 は +1.0%、1 レベルごとに +1.0%。
        /// 小数第2位以下は四捨五入し、小数第1位までにする。Lv999 は +990.0%。
        /// </summary>
        public static double TrainingBonusPercent(int level)
        {
            if (level < 1) level = 1;
            if (level > Facility.MaxLevel) level = Facility.MaxLevel;
            double percent = TrainingPercentAtLevel10 + (level - 10) * TrainingPercentStep;
            if (percent < 0d) percent = 0d;
            return Math.Round(percent, 1, MidpointRounding.AwayFromZero);
        }

        public static double TrainingMultiplier(int level)
        {
            return 1d + TrainingBonusPercent(level) / 100d;
        }

        /// <summary>コーチ1人の攻撃・守備への加算。得意ポジションの選手は 1.5 倍。</summary>
        public static double CoachBonusPercent(int level, bool specialty)
        {
            double percent = TrainingBonusPercent(level);
            if (specialty) percent *= CoachSpecialtyMultiplier;
            return percent;
        }

        public static bool FitsManager(StaffMember manager, Player player)
        {
            if (manager == null || player == null) return false;
            if (player.Style == manager.FavoriteStyle) return true;
            var slots = FormationCatalog.Get(manager.FavoriteFormation).Slots;
            var preferred = player.PreferredPosition;
            var fit = player.SuitabilityAt(preferred);
            if (fit != Suitability.Excellent && fit != Suitability.Good) return false;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == preferred) return true;
            }

            return false;
        }

        public static void PracticeMultiplier(Club club, Player player, out double offenseDefense, out double stamina, int? forcedLevel = null)
        {
            var team = club != null ? club.Team : null;
            double head = Factor(team != null ? team.HeadCoach : null, forcedLevel);
            double manager = 0d;
            if (team != null && team.Manager != null && FitsManager(team.Manager, player))
                manager = LevelFactor(forcedLevel ?? team.Manager.Level);

            double coach = 0d;
            if (team != null && team.Coaches != null && player != null)
            {
                var group = PositionUtility.GroupOf(player.PreferredPosition);
                for (int i = 0; i < team.Coaches.Length; i++)
                {
                    var member = team.Coaches[i];
                    if (member == null) continue;
                    coach += CoachBonusPercent(forcedLevel ?? member.Level, member.ScoutedGroup == group);
                }
            }

            double physical = Factor(team != null ? team.PhysicalCoach : null, forcedLevel);
            double ground = FacilityTrainingBonus(club, true, forcedLevel);
            double room = FacilityTrainingBonus(club, false, forcedLevel);
            offenseDefense = 1d + head * 0.45d + coach / 100d + manager * 0.35d + ground;
            stamina = 1d + head * 0.45d + physical * 0.40d + manager * 0.35d + room;
        }

        public static double AcademyMultiplier(Club club, int? forcedLevel = null)
        {
            int level = forcedLevel ?? (club != null && club.Front != null ? club.Front.AcademyLevel() : 0);
            return 1d + LevelFactor(level) * 0.50d;
        }

        static double FacilityTrainingBonus(Club club, bool ground, int? forcedLevel)
        {
            if (forcedLevel.HasValue)
                return TrainingMultiplier(forcedLevel.Value) - 1d;
            if (club == null || club.Facilities == null) return 0d;
            double multiplier = ground
                ? club.Facilities.TrainingGroundMultiplier()
                : club.Facilities.TrainingRoomMultiplier();
            return multiplier - 1d;
        }

        public static BigInteger DevelopmentFee(Club club)
        {
            int level = club.Front != null ? club.Front.AcademyLevel() : 0;
            if (level <= 0) return BigInteger.Zero;
            return RoundCoins(400_000d * (10d + level) / 10d);
        }

        public static BigInteger SponsorIncome(Club club)
        {
            int level = club.Front != null ? club.Front.SalesLevel() : 0;
            return FloorCoins(800_000d + level * 200_000d);
        }

        public const int SponsorContractMonths = 6;
        public const int SponsorContractCap = 8;
        public const double SponsorOfferChance = 0.15d;

        /// <summary>営業が連れてくる契約の月額の下限。円。</summary>
        public const int SponsorContractMonthlyFloor = 10_000_000;

        /// <summary>契約した日に入る契約金は、月額のこの倍数。</summary>
        public const int SponsorUpfrontMultiple = 2;

        /// <summary>営業が連れてくる 1 社の月額。順位では増減しない。営業がいないときは 0。下限は 1000 万円。</summary>
        public static BigInteger SponsorContractMonthly(Club club)
        {
            int level = club.Front != null ? club.Front.SalesLevel() : 0;
            if (level <= 0) return BigInteger.Zero;
            var amount = FloorCoins(400_000d + level * 30_000d);
            var floor = new BigInteger(SponsorContractMonthlyFloor);
            return amount < floor ? floor : amount;
        }

        public static BigInteger SponsorUpfront(BigInteger monthly)
        {
            if (monthly <= 0) return BigInteger.Zero;
            return monthly * SponsorUpfrontMultiple;
        }

        /// <summary>このスポンサーの解消条件を、いまの順位と連敗が満たしているか。</summary>
        public static bool SponsorBroken(int cutRank, int cutLosses, int rank, int losingStreak)
        {
            if (cutRank > 0 && rank > 0 && rank >= cutRank) return true;
            if (cutLosses > 0 && losingStreak >= cutLosses) return true;
            return false;
        }

        public static string SponsorCutLabel(int cutRank, int cutLosses)
        {
            if (cutRank > 0) return cutRank + "位以下で解消";
            if (cutLosses > 0) return cutLosses + "連敗で解消";
            return "解消条件なし";
        }

        public static int TicketYen(Club club)
        {
            if (club != null && club.TicketPrice > 0) return club.TicketPrice;
            return QuoteTicket(club);
        }

        /// <summary>事業部がいまの実力から出すチケット価格。円。</summary>
        public static int QuoteTicket(Club club)
        {
            int level = club != null && club.Front != null ? club.Front.BusinessLevel() : 0;
            int supporters = club != null ? club.CoreSupporters : 0;
            return 4000 + (int)Math.Floor(12000d * LevelFactor(level)) + supporters / 15;
        }

        /// <summary>ホーム1試合の広告費。広報部のレベルに比例し、総資金から払う。</summary>
        public static BigInteger MatchAdvertising(Club club)
        {
            int pr = club != null && club.Front != null ? club.Front.PublicRelationsLevel() : 0;
            if (pr <= 0) return BigInteger.Zero;
            return new BigInteger(220_000L * pr);
        }

        public static int GoodsYen(Club club)
        {
            int level = club.Front != null ? club.Front.BusinessLevel() : 0;
            return 1500 + (int)Math.Floor(8000d * LevelFactor(level)) + club.CoreSupporters / 30;
        }

        public static int Attendance(Club club, BigInteger advertising)
        {
            int basic = RoundInt(club.CoreSupporters * 0.90d + club.LightSupporters * 0.35d);
            int cap = Capacity(club);
            if (basic < 0) basic = 0;
            int room = Math.Max(0, cap - basic);
            int pr = club.Front != null ? club.Front.PublicRelationsLevel() : 0;
            int yenPer = Math.Max(200, RoundInt(856d * 80d / (80d + pr)));
            int extra = 0;
            if (advertising > 0 && yenPer > 0)
            {
                extra = (int)Math.Floor(ToDouble(advertising) / yenPer);
                if (extra > room) extra = room;
                if (extra < 0) extra = 0;
            }

            int total = basic + extra;
            if (total > cap) total = cap;
            return Math.Max(0, total);
        }

        public static BigInteger GateIncome(Club club, BigInteger advertising)
        {
            return FloorCoins(Attendance(club, advertising) * (double)TicketYen(club));
        }

        public static BigInteger GoodsIncome(Club club)
        {
            long sold = club.CoreSupporters + club.LightSupporters / 4L;
            if (sold < 0) sold = 0;
            return FloorCoins(sold * (double)GoodsYen(club));
        }

        /// <summary>ホーム1試合の運営費。満員売上の22%で、空席でもかかる。</summary>
        public static BigInteger OperatingRequired(Club club)
        {
            int cap = Capacity(club);
            int ticket = TicketYen(club);
            if (cap <= 0 || ticket <= 0) return BigInteger.Zero;
            return FloorCoins(cap * (double)ticket * 0.22d);
        }

        public static void ApplyWin(Club club)
        {
            long light = club.LightSupporters + Math.Max(10L, RoundInt(club.LightSupporters * 0.012d));
            long promotion = Math.Max(5L, RoundInt(light * 0.008d));
            long room = (long)Capacity(club) - club.CoreSupporters;
            if (room < 0) room = 0;
            if (promotion > room) promotion = room;
            if (promotion > light) promotion = light;
            club.CoreSupporters = ClampCount(club.CoreSupporters + promotion, CoreFloor);
            club.LightSupporters = ClampCount(light - promotion, LightFloor);
        }

        public static void ApplyLoss(Club club)
        {
            int lightLoss = Math.Max(8, RoundInt(club.LightSupporters * 0.014d));
            int coreLoss = Math.Max(3, RoundInt(club.CoreSupporters * 0.005d));
            club.LightSupporters = Reduce(club.LightSupporters, lightLoss, LightFloor);
            club.CoreSupporters = Reduce(club.CoreSupporters, coreLoss, CoreFloor);
        }

        /// <summary>
        /// 勝敗と、ホームなら運営費不足を反映した増減を返す。クラブの人数は変えない。
        /// </summary>
        public static void PreviewMatchSupporters(
            Club club,
            bool won,
            bool lost,
            bool home,
            BigInteger paid,
            BigInteger required,
            out int coreDelta,
            out int lightDelta)
        {
            int core = club.CoreSupporters;
            int light = club.LightSupporters;
            if (won) ApplyWin(club);
            else if (lost) ApplyLoss(club);
            if (home) ApplyOperatingShortfall(club, paid, required);
            coreDelta = club.CoreSupporters - core;
            lightDelta = club.LightSupporters - light;
            club.CoreSupporters = core;
            club.LightSupporters = light;
        }

        public static void ApplyOperatingShortfall(Club club, BigInteger paid, BigInteger required)
        {
            if (required <= 0 || paid >= required) return;
            double rate = 1d - ToDouble(paid) / ToDouble(required);
            if (rate < 0d) rate = 0d;
            if (rate > 1d) rate = 1d;
            club.CoreSupporters = Reduce(club.CoreSupporters, RoundInt(club.CoreSupporters * rate), CoreFloor);
            club.LightSupporters = Reduce(club.LightSupporters, RoundInt(club.LightSupporters * rate), LightFloor);
        }

        public const double InjuryOccurrenceRate = 0.075d;
        public const double SecondInjuryChance = 0.20d;
        public const double ThirdInjuryChance = 0.05d;

        /// <summary>
        /// 試合で怪我人が出るかの抽選確率と、出たときの欠場日数。どちらもドクター係数で下がる。
        /// 人数は別抽選で、1人確定、2人目 20%、3人目 5%、4人目は出ない。
        /// </summary>
        public static void InjuryOutcome(Club club, out double rate, out int days)
        {
            double doctor = Factor(club.Team != null ? club.Team.Doctor : null);
            double scale = 1d - 0.92d * doctor;
            rate = InjuryOccurrenceRate * scale;
            days = Math.Max(2, RoundInt(24d * scale));
        }

        /// <summary>リーグLV1の基礎値に、優勝のたびに増える分を足す。</summary>
        public static int ConditionAmount(int baseAmount, int perLeague, int leagueLevel)
        {
            if (leagueLevel < 1) leagueLevel = 1;
            long value = (long)baseAmount + (long)perLeague * (leagueLevel - 1);
            if (value > 1000) return 1000;
            if (value < 0) return 0;
            return (int)value;
        }

        /// <summary>トレーナー全員のレベル合計。1 レベルが回復 1 点。</summary>
        public static int TrainerRecoveryBudget(Club club)
        {
            var trainers = club != null && club.Team != null ? club.Team.Trainers : null;
            if (trainers == null) return 0;
            int sum = 0;
            for (int i = 0; i < trainers.Length; i++)
            {
                var trainer = trainers[i];
                if (trainer != null && trainer.Level > 0)
                    sum += trainer.Level;
            }

            return sum;
        }

        public static bool StaffMarketAllows(Club club, int level)
        {
            if (club == null || club.Facilities == null) return false;
            return level >= club.Facilities.MarketLevelFloor() && level <= club.Facilities.MarketLevelCap();
        }

        static double Factor(StaffMember staff, int? forcedLevel = null)
        {
            if (staff == null) return 0d;
            return LevelFactor(forcedLevel ?? staff.Level);
        }

        static int ClampCount(long value, int floor)
        {
            if (value < floor) return floor;
            if (value > 1000000000L) return 1000000000;
            return (int)value;
        }

        static int Reduce(int current, int loss, int floor)
        {
            if (current <= floor) return current;
            int next = current - Math.Max(0, loss);
            return next < floor ? floor : next;
        }
    }
}
