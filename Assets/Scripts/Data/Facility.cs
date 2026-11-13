using System;
using System.Numerics;

namespace SoccerClub.Data
{
    public enum FacilityKind
    {
        Stadium,
        Transport,
        TrainingGround,
        TrainingFacility,
        Clubhouse
    }

    [Serializable]
    public sealed class Facility
    {
        public const int MaxLevel = 999;

        /// <summary>スタジアムの上限レベル。収容 5,000 + Lv×475 で、Lv200 が 10 万人。</summary>
        public const int StadiumMaxLevel = 200;

        public FacilityKind Kind;
        public int Level = 1;
        public BigInteger BaseCost;

        /// <summary>練習場・トレーニング室の費用の伸び（千分率）。1080 = 1.08 倍。シミュレーターから変えられる。</summary>
        public const int DefaultTrainingCostGrowthPermil = 1080;
        public static int TrainingCostGrowthPermil = DefaultTrainingCostGrowthPermil;

        /// <summary>クラブハウスの費用の伸び（千分率）。1500 = 1.5 倍。シミュレーターから変えられる。</summary>
        public const int DefaultClubhouseCostGrowthPermil = 1080;
        public static int ClubhouseCostGrowthPermil = DefaultClubhouseCostGrowthPermil;

        /// <summary>練習場・トレーニング室・クラブハウスの基本価格にかける倍率（序盤の上がり方の調整）。シミュレーターから変えられる。</summary>
        public const double DefaultGrowthFacilityBaseScale = 1d;
        public static double GrowthFacilityBaseScale = DefaultGrowthFacilityBaseScale;

        BigInteger ScaledBase()
        {
            double scale = GrowthFacilityBaseScale > 0d ? GrowthFacilityBaseScale : DefaultGrowthFacilityBaseScale;
            if (Math.Abs(scale - 1d) < 1e-9) return BaseCost;
            return BaseCost * new BigInteger(Math.Round(scale * 1000d)) / 1000;
        }

        /// <summary>
        /// Lv の建設/次レベル費用。スタジアムは BaseCost × 1.04^(Level-1)、
        /// 練習場・トレーニング室は BaseCost × (TrainingCostGrowthPermil/1000)^(Level-1)、
        /// クラブハウスは BaseCost × (ClubhouseCostGrowthPermil/1000)^(Level-1)、交通は BaseCost × 1.5^(Level-1)。
        /// </summary>
        public BigInteger CostForLevel(int level)
        {
            int n = Math.Max(0, level - 1);
            if (Kind == FacilityKind.Stadium)
                return BaseCost * BigInteger.Pow(26, n) / BigInteger.Pow(25, n);
            if (Kind == FacilityKind.TrainingGround || Kind == FacilityKind.TrainingFacility)
            {
                int g = TrainingCostGrowthPermil > 1000 ? TrainingCostGrowthPermil : DefaultTrainingCostGrowthPermil;
                return ScaledBase() * BigInteger.Pow(g, n) / BigInteger.Pow(1000, n);
            }
            if (Kind == FacilityKind.Clubhouse)
            {
                int h = ClubhouseCostGrowthPermil > 1000 ? ClubhouseCostGrowthPermil : DefaultClubhouseCostGrowthPermil;
                return ScaledBase() * BigInteger.Pow(h, n) / BigInteger.Pow(1000, n);
            }
            return BaseCost * BigInteger.Pow(3, n) / BigInteger.Pow(2, n);
        }

        public BigInteger NextUpgradeCost() => CostForLevel(Level + 1);

        public int LevelCap
        {
            get
            {
                if (Kind == FacilityKind.Stadium) return StadiumMaxLevel;
                bool growth = Kind == FacilityKind.TrainingGround
                              || Kind == FacilityKind.TrainingFacility
                              || Kind == FacilityKind.Clubhouse;
                if (!growth || LeagueLevelAllowance <= 0) return MaxLevel;
                int cap = Math.Max(1, CurrentLeagueLevel) + LeagueLevelAllowance;
                return cap < MaxLevel ? cap : MaxLevel;
            }
        }

        /// <summary>
        /// 練習場・トレーニング室・クラブハウスは「リーグLV＋この値」までしか上げられない。0 で無制限。
        /// シミュレーターから変えられる。
        /// </summary>
        public const int DefaultLeagueLevelAllowance = 30;
        public static int LeagueLevelAllowance = DefaultLeagueLevelAllowance;

        /// <summary>いまのリーグLV。GameSession が設定する。</summary>
        public static int CurrentLeagueLevel = 1;

        public bool CanUpgrade() => Level < LevelCap;

        public void Upgrade()
        {
            if (!CanUpgrade()) return;
            Level++;
        }
    }

    [Serializable]
    public sealed class ClubFacilities
    {
        public Facility Stadium = new Facility { Kind = FacilityKind.Stadium };
        public Facility Transport = new Facility { Kind = FacilityKind.Transport };
        public Facility TrainingGround = new Facility { Kind = FacilityKind.TrainingGround };
        public Facility TrainingFacility = new Facility { Kind = FacilityKind.TrainingFacility };
        public Facility Clubhouse = new Facility { Kind = FacilityKind.Clubhouse };

        public Facility Get(FacilityKind kind)
        {
            switch (kind)
            {
                case FacilityKind.Stadium: return Stadium;
                case FacilityKind.Transport: return Transport;
                case FacilityKind.TrainingGround: return TrainingGround;
                case FacilityKind.TrainingFacility: return TrainingFacility;
                default: return Clubhouse;
            }
        }

        /// <summary>1 レベルあたりの収容の増え方。</summary>
        public const int StadiumSeatsPerLevel = 475;

        /// <summary>収容の上限。</summary>
        public const int StadiumMaxCapacity = 100000;

        public BigInteger StadiumCapacity()
        {
            long seats = 5000L + (long)Stadium.Level * StadiumSeatsPerLevel;
            if (seats > StadiumMaxCapacity) seats = StadiumMaxCapacity;
            return seats;
        }

        public double TrainingGroundMultiplier() => ClubMath.TrainingMultiplier(TrainingGround.Level);

        public double TrainingRoomMultiplier() => ClubMath.TrainingMultiplier(TrainingFacility.Level);

        public double TrainingGroundBonusPercent() => ClubMath.TrainingBonusPercent(TrainingGround.Level);

        public double TrainingRoomBonusPercent() => ClubMath.TrainingBonusPercent(TrainingFacility.Level);

        /// <summary>雇えるスタッフレベルの上限。クラブハウスと同じ。</summary>
        public int MarketLevelCap()
        {
            int level = Clubhouse.Level;
            if (level < 1) level = 1;
            if (level > Facility.MaxLevel) level = Facility.MaxLevel;
            return level;
        }

        /// <summary>リストに出るレベルの下限。上限の 5 下まで。</summary>
        public int MarketLevelFloor()
        {
            int floor = MarketLevelCap() - 5;
            return floor < 1 ? 1 : floor;
        }
    }
}
