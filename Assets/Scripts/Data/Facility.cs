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

        /// <summary>
        /// Lv の建設/次レベル費用。スタジアムは BaseCost × 1.025^(Level-1)（Lv200 まで届くよう緩やか）、
        /// ほかは BaseCost × 1.5^(Level-1)。
        /// </summary>
        public BigInteger CostForLevel(int level)
        {
            int n = Math.Max(0, level - 1);
            if (Kind == FacilityKind.Stadium)
                return BaseCost * BigInteger.Pow(41, n) / BigInteger.Pow(40, n);
            return BaseCost * BigInteger.Pow(3, n) / BigInteger.Pow(2, n);
        }

        public BigInteger NextUpgradeCost() => CostForLevel(Level + 1);

        public int LevelCap => Kind == FacilityKind.Stadium ? StadiumMaxLevel : MaxLevel;

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
