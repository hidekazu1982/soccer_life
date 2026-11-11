using System;
using System.Numerics;
using SoccerClub.Competition;
using SoccerClub.Data;
using SoccerClub.Personnel;

namespace SoccerClub.Core
{
    /// <summary>
    /// 高速シミュレーター用のオーナー判断。ゲーム本体のフルオートとは別ルール。
    /// ・資金が総年俸を下回っているときは、施設のレベルアップをしない。
    /// ・レベルアップ後の資金が総年俸を下回るときは、レベルアップをしない。
    /// ・フロントは、所属スタッフより高レベルの候補がいて、入れ替え後も資金が総年俸を下回らないときに入れ替える。
    /// </summary>
    public sealed partial class GameSession
    {
        const int SimulationStepLimit = 64;

        /// <summary>シミュレーターが1日の始めに行う投資の件数。</summary>
        public struct SimulationSpend
        {
            public int Stadium;
            public int TrainingGround;
            public int TrainingFacility;
            public int Clubhouse;
            public int Front;
        }

        /// <summary>
        /// 施設とフロントへの投資を、フルオートと同じ優先度で繰り返す。
        /// スタジアム（入場者が収容の80%超のとき）、練習場、トレーニング室、フロント、クラブハウス。
        /// </summary>
        /// <param name="fillEmptyFront">空き枠を「レベル0のスタッフがいる」とみなして採用するか。</param>
        public SimulationSpend SpendForSimulation(bool fillEmptyFront)
        {
            var spend = new SimulationSpend();
            if (PlayerClub == null || GameOver) return spend;
            for (int step = 0; step < SimulationStepLimit; step++)
            {
                if (HomeAttendanceExceedsCapacity() && TrySimulationUpgrade(FacilityKind.Stadium))
                {
                    spend.Stadium++;
                    continue;
                }

                if (TrySimulationUpgrade(FacilityKind.TrainingGround))
                {
                    spend.TrainingGround++;
                    continue;
                }

                if (TrySimulationUpgrade(FacilityKind.TrainingFacility))
                {
                    spend.TrainingFacility++;
                    continue;
                }

                if (TrySimulationFront(fillEmptyFront))
                {
                    spend.Front++;
                    continue;
                }

                if (TrySimulationUpgrade(FacilityKind.Clubhouse))
                {
                    spend.Clubhouse++;
                    continue;
                }

                break;
            }

            return spend;
        }

        bool TrySimulationUpgrade(FacilityKind kind)
        {
            if (!SimulationCanUpgrade(kind)) return false;
            return OwnerApprovesFacilityUpgrade(kind);
        }

        bool SimulationCanUpgrade(FacilityKind kind)
        {
            var club = PlayerClub;
            var facility = club != null && club.Facilities != null ? club.Facilities.Get(kind) : null;
            if (facility == null || !facility.CanUpgrade()) return false;
            var payroll = ClubMath.RenewalAnnual(club);
            // ルール1：資金が総年俸を下回っているときはしない。
            if (club.Coins < payroll) return false;
            // ルール2：支払い後に総年俸を下回るならしない。
            return club.Coins - facility.NextUpgradeCost() >= payroll;
        }

        /// <summary>ルール3：所属スタッフより高レベルで、入れ替え後も資金が総年俸を下回らない候補と入れ替える。</summary>
        bool TrySimulationFront(bool fillEmptyFront)
        {
            var club = PlayerClub;
            var front = club != null ? club.Front : null;
            if (front == null || _frontMarket.Count == 0) return false;

            var payroll = ClubMath.RenewalAnnual(club);
            if (club.Coins < payroll) return false;

            StaffMember best = null;
            int bestSlot = -1;
            int bestGain = 0;
            for (int slot = 0; slot < FrontOffice.SlotCount; slot++)
            {
                var current = front.GetAt(slot);
                if (current == null && !fillEmptyFront) continue;
                var role = FrontOffice.RoleAt(slot);
                int currentLevel = current != null ? current.Level : 0;
                var currentWage = current != null ? ClubMath.StaffAnnual(current) : BigInteger.Zero;
                for (int i = 0; i < _frontMarket.Count; i++)
                {
                    var candidate = _frontMarket[i];
                    if (candidate == null || candidate.FrontRole != role) continue;
                    if (candidate.Level <= currentLevel) continue;
                    if (!ClubMath.StaffMarketAllows(club, candidate.Level)) continue;
                    // 入れ替え後の総年俸と、支払い後の資金で判定する。
                    var payrollAfter = payroll - currentWage + ClubMath.StaffAnnual(candidate);
                    if (club.Coins - FrontOfficeHiring.HireCost(candidate) < payrollAfter) continue;
                    int gain = candidate.Level - currentLevel;
                    if (best != null && gain <= bestGain) continue;
                    best = candidate;
                    bestSlot = slot;
                    bestGain = gain;
                }
            }

            if (best == null) return false;
            StaffMember dismissed;
            return OwnerContractsFront(best, bestSlot, out dismissed);
        }

        /// <summary>無所属選手の一覧。シミュレーターの集計用（読み取りのみ）。</summary>
        public System.Collections.Generic.IReadOnlyList<Player> SimulationFreeAgents => _freeAgents;

        /// <summary>リーグの全日程が消化済み。シーズン記録の取得に使う。</summary>
        public bool SimulationLeagueComplete()
        {
            DateTime last;
            return LeagueIsComplete(out last);
        }
    }
}
