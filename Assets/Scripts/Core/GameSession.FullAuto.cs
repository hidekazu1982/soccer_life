using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using SoccerClub.Competition;
using SoccerClub.Data;
using SoccerClub.Personnel;

namespace SoccerClub.Core
{
    public sealed partial class GameSession
    {
        const int FullAutoStepLimit = 64;

        /// <summary>
        /// 資金が総年俸を上回るあいだ、余剰を優先度どおり使う。
        /// スタジアム、練習場、トレーニング室、フロント。番号に無いクラブハウス本体はその後。
        /// 下回るときは何もしない。
        /// </summary>
        public string SpendFullAuto()
        {
            if (PlayerClub == null || GameOver) return null;
            int stadium = 0;
            int ground = 0;
            int room = 0;
            int hired = 0;
            int house = 0;
            for (int step = 0; step < FullAutoStepLimit; step++)
            {
                if (!FundsAbovePayroll()) break;
                if (TryFullAutoStadium())
                {
                    stadium++;
                    continue;
                }

                if (TryFullAutoUpgrade(FacilityKind.TrainingGround))
                {
                    ground++;
                    continue;
                }

                if (TryFullAutoUpgrade(FacilityKind.TrainingFacility))
                {
                    room++;
                    continue;
                }

                if (TryFullAutoFront())
                {
                    hired++;
                    continue;
                }

                if (TryFullAutoUpgrade(FacilityKind.Clubhouse))
                {
                    house++;
                    continue;
                }

                break;
            }

            if (stadium == 0 && ground == 0 && room == 0 && hired == 0 && house == 0) return null;
            var parts = new List<string>();
            if (stadium > 0) parts.Add("スタジアム+" + stadium.ToString(CultureInfo.InvariantCulture));
            if (ground > 0) parts.Add("練習場+" + ground.ToString(CultureInfo.InvariantCulture));
            if (room > 0) parts.Add("トレーニング室+" + room.ToString(CultureInfo.InvariantCulture));
            if (hired > 0) parts.Add("フロント+" + hired.ToString(CultureInfo.InvariantCulture));
            if (house > 0) parts.Add("クラブハウス+" + house.ToString(CultureInfo.InvariantCulture));
            return string.Join("、", parts);
        }

        /// <summary>スタジアム提案を受けるか。直近ホームが現在の収容の80%を超え、支払い後も総年俸を上回る。</summary>
        public bool FullAutoAcceptsStadiumUpgrade()
        {
            return HomeAttendanceExceedsCapacity() && CanUpgradeWithSurplus(FacilityKind.Stadium);
        }

        bool TryFullAutoStadium()
        {
            if (!HomeAttendanceExceedsCapacity()) return false;
            return TryFullAutoUpgrade(FacilityKind.Stadium);
        }

        bool TryFullAutoUpgrade(FacilityKind kind)
        {
            if (!CanUpgradeWithSurplus(kind)) return false;
            return OwnerApprovesFacilityUpgrade(kind);
        }

        bool CanUpgradeWithSurplus(FacilityKind kind)
        {
            var facilities = PlayerClub != null ? PlayerClub.Facilities : null;
            var facility = facilities != null ? facilities.Get(kind) : null;
            if (facility == null || !facility.CanUpgrade()) return false;
            return SurplusCovers(facility.NextUpgradeCost());
        }

        /// <summary>直近のホーム試合の入場者が、いまの収容人数の80%を超えている。</summary>
        bool HomeAttendanceExceedsCapacity()
        {
            var fixture = LatestPlayedHomeFixture();
            var facilities = PlayerClub != null ? PlayerClub.Facilities : null;
            if (fixture == null || facilities == null) return false;
            long capacity = (long)facilities.StadiumCapacity();
            if (capacity <= 0) return false;
            return (long)fixture.Attendance * 100L > capacity * 80L;
        }

        Fixture LatestPlayedHomeFixture()
        {
            Fixture best = null;
            ConsiderHome(League != null ? League.Fixtures : null, ref best);
            ConsiderHome(Cup != null ? Cup.Fixtures : null, ref best);
            return best;
        }

        void ConsiderHome(IReadOnlyList<Fixture> fixtures, ref Fixture best)
        {
            if (fixtures == null || PlayerClub == null) return;
            for (int i = 0; i < fixtures.Count; i++)
            {
                var fixture = fixtures[i];
                if (fixture == null || fixture.Result == null || fixture.Home != PlayerClub) continue;
                if (best == null || fixture.Date > best.Date)
                    best = fixture;
            }
        }

        bool TryFullAutoFront()
        {
            var club = PlayerClub;
            var front = club != null ? club.Front : null;
            if (front == null || _frontMarket.Count == 0) return false;

            StaffMember best = null;
            int bestSlot = -1;
            int bestLevel = -1;
            bool bestEmpty = false;
            for (int slot = 0; slot < FrontOffice.SlotCount; slot++)
            {
                var current = front.GetAt(slot);
                var role = FrontOffice.RoleAt(slot);
                bool empty = current == null;
                for (int i = 0; i < _frontMarket.Count; i++)
                {
                    var candidate = _frontMarket[i];
                    if (candidate == null || candidate.FrontRole != role) continue;
                    if (!ClubMath.StaffMarketAllows(club, candidate.Level)) continue;
                    if (!empty && candidate.Level <= current.Level) continue;
                    if (!SurplusCovers(FrontOfficeHiring.HireCost(candidate))) continue;
                    bool better = best == null
                        || (empty && !bestEmpty)
                        || (empty == bestEmpty && candidate.Level > bestLevel);
                    if (!better) continue;
                    best = candidate;
                    bestSlot = slot;
                    bestLevel = candidate.Level;
                    bestEmpty = empty;
                }
            }

            if (best == null) return false;
            StaffMember dismissed;
            return OwnerContractsFront(best, bestSlot, out dismissed);
        }

        bool FundsAbovePayroll()
        {
            var club = PlayerClub;
            if (club == null) return false;
            return club.Coins > ClubMath.RenewalAnnual(club);
        }

        bool SurplusCovers(BigInteger cost)
        {
            var club = PlayerClub;
            if (club == null || cost < 0) return false;
            var payroll = ClubMath.RenewalAnnual(club);
            return club.Coins > payroll && club.Coins - cost > payroll;
        }
    }
}
