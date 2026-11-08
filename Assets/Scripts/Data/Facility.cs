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

        public FacilityKind Kind;
        public int Level = 1;
        public BigInteger BaseCost;

        /// <summary>Lv の建設/次レベル費用: BaseCost × 1.5^(Level-1)</summary>
        public BigInteger CostForLevel(int level)
        {
            int n = Math.Max(0, level - 1);
            return BaseCost * BigInteger.Pow(3, n) / BigInteger.Pow(2, n);
        }

        public BigInteger NextUpgradeCost() => CostForLevel(Level + 1);

        public bool CanUpgrade() => Level < MaxLevel;

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

        public BigInteger StadiumCapacity()
        {
            return 5000 + Stadium.Level * 2000;
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
