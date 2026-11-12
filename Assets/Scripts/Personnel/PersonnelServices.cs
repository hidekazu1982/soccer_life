using System;
using System.Collections.Generic;
using System.Numerics;
using SoccerClub.Core;
using SoccerClub.Data;
using SoccerClub.Economy;

namespace SoccerClub.Personnel
{
    public sealed class TransferShortlist
    {
        public readonly List<Player> Players = new List<Player>();
        public readonly List<StaffMember> Staff = new List<StaffMember>();
    }

    public interface IGeneralManager
    {
        void ReleaseContracts(Club club);
        void SignListed(Club club, TransferShortlist list, DateTime day);
    }

    /// <summary>強化部が、共有市場から自クラブのリストを作る。</summary>
    public sealed class ScoutingService
    {
        public TransferShortlist BuildShortlist(
            Club club,
            IReadOnlyList<Player> players,
            IReadOnlyList<StaffMember> staff,
            HashSet<string> takenPlayers,
            HashSet<string> takenStaff)
        {
            var list = new TransferShortlist();
            var scouts = club.Front != null ? club.Front.Strengthening : null;
            int level = club.Front != null ? club.Front.StrengtheningLevel() : 0;
            if (scouts == null || level <= 0) return list;

            double target = 80d + level * 12d;
            int floor = club.Facilities.MarketLevelFloor();
            int cap = club.Facilities.MarketLevelCap();
            int active = 0;
            for (int i = 0; i < scouts.Length; i++)
            {
                if (scouts[i] != null) active++;
            }

            if (active <= 0) return list;
            int perScout = active == 1 ? 8 : 4;
            for (int i = 0; i < scouts.Length; i++)
            {
                var scout = scouts[i];
                if (scout == null) continue;
                for (int n = 0; n < perScout; n++)
                {
                    var player = TakeClosestPlayer(players, scout.ScoutedGroup, target, takenPlayers);
                    if (player == null) break;
                    list.Players.Add(player);
                }
            }

            // 最低人数を下回るポジショングループは、担当の強化部員がいなくても候補を足す（GK 不在を防ぐ）。
            foreach (PositionGroup group in Enum.GetValues(typeof(PositionGroup)))
            {
                int lacking = ClubMath.SquadGroupFloor(group) - ClubMath.SquadGroupCount(club, group);
                for (int n = 0; n < lacking + 1 && lacking > 0; n++)
                {
                    var player = TakeClosestPlayer(players, group, target, takenPlayers);
                    if (player == null) break;
                    list.Players.Add(player);
                }
            }

            for (int n = 0; n < 4; n++)
            {
                var member = TakeClosestStaff(staff, level, floor, cap, takenStaff);
                if (member == null) break;
                list.Staff.Add(member);
            }

            return list;
        }

        static Player TakeClosestPlayer(
            IReadOnlyList<Player> market,
            PositionGroup group,
            double target,
            HashSet<string> taken)
        {
            Player best = null;
            double bestDistance = double.MaxValue;
            for (int i = 0; i < market.Count; i++)
            {
                var player = market[i];
                if (player == null || taken.Contains(player.Id)) continue;
                if (PositionUtility.GroupOf(player.PreferredPosition) != group) continue;
                double distance = Math.Abs(player.Overall - target);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = player;
                }
            }

            if (best != null) taken.Add(best.Id);
            return best;
        }

        static StaffMember TakeClosestStaff(
            IReadOnlyList<StaffMember> market,
            int level,
            int floor,
            int cap,
            HashSet<string> taken)
        {
            StaffMember best = null;
            int bestDistance = int.MaxValue;
            for (int i = 0; i < market.Count; i++)
            {
                var member = market[i];
                if (member == null || taken.Contains(member.Id)) continue;
                if (member.Level < floor || member.Level > cap) continue;
                int distance = Math.Abs(member.Level - level);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = member;
                }
            }

            if (best != null) taken.Add(best.Id);
            return best;
        }
    }

    /// <summary>GM は強化部リストから獲得し、方針に沿って契約を満了させる。</summary>
    public sealed class GeneralManagerAi : IGeneralManager
    {
        readonly EconomyManager _economy;

        public GeneralManagerAi(EconomyManager economy)
        {
            _economy = economy;
        }

        public void ReleaseContracts(Club club)
        {
            var policy = PolicyOf(club);
            var leaving = new List<Player>();
            for (int i = 0; i < club.Squad.Count; i++)
            {
                var player = club.Squad[i];
                if (player == null || !ShouldRelease(club, player, policy)) continue;
                // 放出後にポジショングループが最低人数を下回るなら残す。
                var group = PositionUtility.GroupOf(player.PreferredPosition);
                int remain = ClubMath.SquadGroupCount(club, group);
                for (int j = 0; j < leaving.Count; j++)
                {
                    if (PositionUtility.GroupOf(leaving[j].PreferredPosition) == group) remain--;
                }

                if (remain - 1 < ClubMath.SquadGroupFloor(group)) continue;
                leaving.Add(player);
            }

            for (int i = 0; i < leaving.Count; i++)
                club.Squad.Remove(leaving[i]);
        }

        public void SignListed(Club club, TransferShortlist list, DateTime day)
        {
            if (list == null) return;
            var players = new List<Player>(list.Players);
            players.Sort((a, b) => CompareWanted(club, a, b));
            for (int i = 0; i < players.Count; i++)
            {
                if (club.Squad.Count >= ClubMath.SquadLimit) break;
                var player = players[i];
                if (!ClubMath.SquadHasRoomFor(club, player)) continue;
                var cost = ClubMath.TransferFee(player) + ClubMath.PlayerAnnual(player);
                if (!_economy.TrySpendReinforcement(club, cost, "sign-" + player.Id)) continue;
                club.Squad.Add(player);
            }

            var staff = new List<StaffMember>(list.Staff);
            staff.Sort((a, b) => b.Level.CompareTo(a.Level));
            for (int i = 0; i < staff.Count; i++)
                TryHireTeamStaff(club, staff[i], day);
        }

        public static int CompareWanted(Club club, Player a, Player b)
        {
            if (ReferenceEquals(a, b)) return 0;
            if (a == null) return 1;
            if (b == null) return -1;
            // 最低人数を下回るポジショングループの選手を先に、上限に達しているグループの選手は後に。
            int needA = PositionRank(club, a);
            int needB = PositionRank(club, b);
            if (needA != needB) return needA.CompareTo(needB);
            switch (PolicyOf(club))
            {
                case ReinforcementPolicy.Youth:
                    return a.Age.CompareTo(b.Age);
                case ReinforcementPolicy.Manager:
                    int fitA = Fits(club, a) ? 0 : 1;
                    int fitB = Fits(club, b) ? 0 : 1;
                    if (fitA != fitB) return fitA.CompareTo(fitB);
                    return b.Overall.CompareTo(a.Overall);
                default:
                    return b.Overall.CompareTo(a.Overall);
            }
        }

        /// <summary>0: 足りないグループ、1: 空きがあるグループ、2: 上限に達したグループ。</summary>
        static int PositionRank(Club club, Player player)
        {
            if (ClubMath.SquadNeeds(club, player)) return 0;
            return ClubMath.SquadHasRoomFor(club, player) ? 1 : 2;
        }

        static bool ShouldRelease(Club club, Player player, ReinforcementPolicy policy)
        {
            bool lowPlay = club.MatchesSinceWindow > 0
                           && (double)player.Appearances / club.MatchesSinceWindow < 0.25d;
            bool veteran = player.Age >= 32 && lowPlay;
            if (policy == ReinforcementPolicy.Manager)
            {
                var manager = club.Team != null ? club.Team.Manager : null;
                bool unfit = manager != null && !ClubMath.FitsManager(manager, player);
                return unfit || veteran;
            }

            return veteran;
        }

        static bool Fits(Club club, Player player)
        {
            var manager = club.Team != null ? club.Team.Manager : null;
            return ClubMath.FitsManager(manager, player);
        }

        static ReinforcementPolicy PolicyOf(Club club)
        {
            var gm = club.Front != null ? club.Front.GeneralManager : null;
            return gm != null ? gm.Policy : ReinforcementPolicy.Strength;
        }

        void TryHireTeamStaff(Club club, StaffMember staff, DateTime day)
        {
            if (staff == null || staff.IsFrontOffice) return;
            if (!ClubMath.StaffMarketAllows(club, staff.Level)) return;
            if (!HasEmptySlot(club, staff.TeamRole)) return;
            var salary = ClubMath.StaffAnnual(staff);
            if (!_economy.TrySpendReinforcement(club, salary, "staff-" + staff.Id)) return;
            if (staff.JoinedTicks <= 0 && day.Ticks > 0) staff.JoinedTicks = day.Date.Ticks;
            Assign(club.Team, staff);
        }

        static bool HasEmptySlot(Club club, TeamStaffRole role)
        {
            var team = club.Team;
            if (team == null) return false;
            switch (role)
            {
                case TeamStaffRole.Manager: return team.Manager == null;
                case TeamStaffRole.HeadCoach: return team.HeadCoach == null;
                case TeamStaffRole.Coach: return HasNull(team.Coaches);
                case TeamStaffRole.PhysicalCoach: return team.PhysicalCoach == null;
                case TeamStaffRole.Trainer: return HasNull(team.Trainers);
                case TeamStaffRole.Doctor: return team.Doctor == null;
                default: return false;
            }
        }

        static bool HasNull(StaffMember[] slots)
        {
            if (slots == null) return false;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null) return true;
            }

            return false;
        }

        static void Assign(TeamStaff team, StaffMember staff)
        {
            switch (staff.TeamRole)
            {
                case TeamStaffRole.Manager:
                    team.Manager = staff;
                    break;
                case TeamStaffRole.HeadCoach:
                    team.HeadCoach = staff;
                    break;
                case TeamStaffRole.Coach:
                    PutSlot(team.Coaches, staff);
                    break;
                case TeamStaffRole.PhysicalCoach:
                    team.PhysicalCoach = staff;
                    break;
                case TeamStaffRole.Trainer:
                    PutSlot(team.Trainers, staff);
                    break;
                case TeamStaffRole.Doctor:
                    team.Doctor = staff;
                    break;
            }
        }

        static void PutSlot(StaffMember[] slots, StaffMember staff)
        {
            if (slots == null) return;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null)
                {
                    slots[i] = staff;
                    return;
                }
            }
        }
    }

    /// <summary>オーナーが総資金からフロントを採用する。契約金と年俸をその場で払う。</summary>
    public sealed class FrontOfficeHiring
    {
        readonly EconomyManager _economy;

        public FrontOfficeHiring(EconomyManager economy)
        {
            _economy = economy;
        }

        public static BigInteger SigningFee(StaffMember staff) => ClubMath.StaffAnnual(staff);

        public static BigInteger HireCost(StaffMember staff)
        {
            if (staff == null) return BigInteger.Zero;
            return SigningFee(staff) + ClubMath.StaffAnnual(staff);
        }

        public bool TryHire(Club club, StaffMember staff)
        {
            if (staff == null || !staff.IsFrontOffice) return false;
            if (!ClubMath.StaffMarketAllows(club, staff.Level)) return false;
            if (!_economy.TrySpend(club, HireCost(staff), "front-hire-" + staff.Id)) return false;
            Assign(club.Front, staff);
            return true;
        }

        public bool TryContract(Club club, StaffMember staff, int slot, out StaffMember dismissed)
        {
            dismissed = null;
            if (staff == null || club == null || club.Front == null || !staff.IsFrontOffice) return false;
            if (slot < 0 || slot >= FrontOffice.SlotCount) return false;
            if (staff.FrontRole != FrontOffice.RoleAt(slot)) return false;
            if (!ClubMath.StaffMarketAllows(club, staff.Level)) return false;
            if (!_economy.TrySpend(club, HireCost(staff), "front-hire-" + staff.Id)) return false;
            dismissed = club.Front.GetAt(slot);
            club.Front.SetAt(slot, staff);
            return true;
        }

        static void Assign(FrontOffice front, StaffMember staff)
        {
            switch (staff.FrontRole)
            {
                case FrontRole.Secretary:
                    front.Secretary = staff;
                    break;
                case FrontRole.GeneralManager:
                    front.GeneralManager = staff;
                    break;
                case FrontRole.Strengthening:
                    PutSlot(front.Strengthening, staff);
                    break;
                case FrontRole.Sales:
                    PutSlot(front.Sales, staff);
                    break;
                case FrontRole.PublicRelations:
                    PutSlot(front.PublicRelations, staff);
                    break;
                case FrontRole.Business:
                    PutSlot(front.Business, staff);
                    break;
                case FrontRole.Academy:
                    PutSlot(front.Academy, staff);
                    break;
            }
        }

        static void PutSlot(StaffMember[] slots, StaffMember staff)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null)
                {
                    slots[i] = staff;
                    return;
                }
            }

            slots[slots.Length - 1] = staff;
        }
    }
}
