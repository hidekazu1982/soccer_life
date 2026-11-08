using System;
using System.Numerics;
using SoccerClub.Core;

namespace SoccerClub.Data
{
    [Serializable]
    public sealed class StaffMember
    {
        public string Id;
        public string Name;
        public int Age = 30;
        public int Level = 1;

        /// <summary>加入日。0 は初期所属で、2026-08-01 から在籍している。</summary>
        public long JoinedTicks;

        public bool IsFrontOffice;
        public FrontRole FrontRole;
        public TeamStaffRole TeamRole;

        /// <summary>GM の強化方針。</summary>
        public ReinforcementPolicy Policy = ReinforcementPolicy.Strength;

        /// <summary>監督の得意システム。</summary>
        public FormationType FavoriteFormation;

        /// <summary>監督の得意戦術。選手のプレースタイルと同じ 5 種。</summary>
        public PlayingStyle FavoriteStyle;

        /// <summary>監督の起用性格。複数候補のとき、パラメータかコンディションのどちらを先に見るか。</summary>
        public LineupPriority LineupPriority;

        /// <summary>強化部とコーチが狙うポジショングループ。</summary>
        public PositionGroup ScoutedGroup = PositionGroup.FW;

        /// <summary>ミニイベント用。給料や能力には影響しない。</summary>
        public bool Married;

        public int Children;

        public BigInteger AnnualSalary => ClubMath.StaffAnnual(this);
    }

    [Serializable]
    public sealed class FrontOffice
    {
        public StaffMember Secretary;
        public StaffMember GeneralManager;
        public StaffMember[] Strengthening = new StaffMember[2];
        public StaffMember[] Sales = new StaffMember[2];
        public StaffMember[] PublicRelations = new StaffMember[2];
        public StaffMember[] Business = new StaffMember[2];
        public StaffMember[] Academy = new StaffMember[2];

        public int SalesLevel() => Highest(Sales);

        public int PublicRelationsLevel() => Highest(PublicRelations);

        public int BusinessLevel() => Highest(Business);

        public int AcademyLevel() => Highest(Academy);

        public int StrengtheningLevel() => Highest(Strengthening);

        public static int Highest(StaffMember[] staff)
        {
            int best = 0;
            if (staff == null) return best;
            for (int i = 0; i < staff.Length; i++)
            {
                if (staff[i] != null && staff[i].Level > best)
                    best = staff[i].Level;
            }

            return best;
        }

        public const int SlotCount = 12;

        static readonly FrontRole[] SlotRoles =
        {
            FrontRole.Secretary,
            FrontRole.GeneralManager,
            FrontRole.Strengthening, FrontRole.Strengthening,
            FrontRole.Sales, FrontRole.Sales,
            FrontRole.PublicRelations, FrontRole.PublicRelations,
            FrontRole.Business, FrontRole.Business,
            FrontRole.Academy, FrontRole.Academy
        };

        public static FrontRole RoleAt(int slot)
        {
            if (slot < 0 || slot >= SlotRoles.Length) return FrontRole.Secretary;
            return SlotRoles[slot];
        }

        public static int IndexAt(int slot)
        {
            var role = RoleAt(slot);
            int index = 0;
            for (int i = 0; i < slot && i < SlotRoles.Length; i++)
            {
                if (SlotRoles[i] == role) index++;
            }

            return index;
        }

        public StaffMember GetAt(int slot)
        {
            if (slot < 0 || slot >= SlotCount) return null;
            return Get(RoleAt(slot), IndexAt(slot));
        }

        public void SetAt(int slot, StaffMember staff)
        {
            if (slot < 0 || slot >= SlotCount) return;
            Set(RoleAt(slot), IndexAt(slot), staff);
        }

        StaffMember Get(FrontRole role, int index)
        {
            switch (role)
            {
                case FrontRole.Secretary: return Secretary;
                case FrontRole.GeneralManager: return GeneralManager;
                case FrontRole.Strengthening: return At(Strengthening, index);
                case FrontRole.Sales: return At(Sales, index);
                case FrontRole.PublicRelations: return At(PublicRelations, index);
                case FrontRole.Business: return At(Business, index);
                case FrontRole.Academy: return At(Academy, index);
                default: return null;
            }
        }

        void Set(FrontRole role, int index, StaffMember staff)
        {
            switch (role)
            {
                case FrontRole.Secretary:
                    Secretary = staff;
                    break;
                case FrontRole.GeneralManager:
                    GeneralManager = staff;
                    break;
                case FrontRole.Strengthening:
                    Put(ref Strengthening, index, staff);
                    break;
                case FrontRole.Sales:
                    Put(ref Sales, index, staff);
                    break;
                case FrontRole.PublicRelations:
                    Put(ref PublicRelations, index, staff);
                    break;
                case FrontRole.Business:
                    Put(ref Business, index, staff);
                    break;
                case FrontRole.Academy:
                    Put(ref Academy, index, staff);
                    break;
            }
        }

        static StaffMember At(StaffMember[] slots, int index)
        {
            if (slots == null || index < 0 || index >= slots.Length) return null;
            return slots[index];
        }

        static void Put(ref StaffMember[] slots, int index, StaffMember staff)
        {
            if (slots == null) slots = new StaffMember[2];
            if (index < 0 || index >= slots.Length) return;
            slots[index] = staff;
        }
    }

    [Serializable]
    public sealed class TeamStaff
    {
        public StaffMember Manager;
        public StaffMember HeadCoach;
        public StaffMember[] Coaches = new StaffMember[2];
        public StaffMember PhysicalCoach;
        public StaffMember[] Trainers = new StaffMember[2];
        public StaffMember Doctor;
    }
}
