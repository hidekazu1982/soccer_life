using System;
using System.Collections.Generic;
using SoccerClub.Core;
using SoccerClub.Data;

namespace SoccerClub.Match
{
    /// <summary>
    /// 監督AI。得意システムは切り替えない。
    /// 各枠は ◎○ の選手を優先し、複数いるときは監督の起用性格で選ぶ。
    /// ◎○ がいない枠は、残り全員からパラメータが高い選手を置く。
    /// ベンチは最大7人を、同じ性格の高い順で埋める。
    /// </summary>
    public sealed class ManagerLineupAi : ILineupSelector
    {
        public TeamSheet BuildSheet(Club club)
        {
            var manager = club.Team != null ? club.Team.Manager : null;
            var formation = FormationCatalog.Get(manager != null
                ? manager.FavoriteFormation
                : FormationType.F433A);
            bool conditionFirst = manager != null && manager.LineupPriority == LineupPriority.Condition;

            var remaining = new List<Player>();
            if (club.Squad != null)
            {
                for (int i = 0; i < club.Squad.Count; i++)
                {
                    var player = club.Squad[i];
                    if (player == null || player.InjuryDays > 0) continue;
                    remaining.Add(player);
                }
            }

            var sheet = new TeamSheet { Formation = formation };
            for (int i = 0; i < formation.Slots.Length && i < 11; i++)
            {
                var slot = formation.Slots[i];
                var pick = SelectForSlot(remaining, slot, conditionFirst);
                sheet.StartingXi[i] = new LineupSlot { Slot = slot, Player = pick };
                if (pick != null) remaining.Remove(pick);
            }

            sheet.Bench = BuildBench(remaining, conditionFirst);
            return sheet;
        }

        static Player SelectForSlot(List<Player> remaining, Position slot, bool conditionFirst)
        {
            Player matched = null;
            Player fallback = null;
            for (int i = 0; i < remaining.Count; i++)
            {
                var player = remaining[i];
                if (Matches(player, slot))
                {
                    if (matched == null || Prefer(player, matched, slot, conditionFirst))
                        matched = player;
                }
                else if (fallback == null || Prefer(player, fallback, slot, false))
                {
                    fallback = player;
                }
            }

            return matched ?? fallback;
        }

        /// <summary>得意 ◎ と、同グループで付いた ○。△ はシステム不一致。</summary>
        static bool Matches(Player player, Position slot)
        {
            var suitability = player.SuitabilityAt(slot);
            return suitability == Suitability.Excellent || suitability == Suitability.Good;
        }

        static bool Prefer(Player candidate, Player current, Position slot, bool conditionFirst)
        {
            if (conditionFirst && candidate.Condition != current.Condition)
                return candidate.Condition > current.Condition;

            double candidateScore = SlotScore(candidate, slot);
            double currentScore = SlotScore(current, slot);
            if (candidateScore != currentScore)
                return candidateScore > currentScore;
            if (!conditionFirst && candidate.Condition != current.Condition)
                return candidate.Condition > current.Condition;
            return string.CompareOrdinal(candidate.Id, current.Id) < 0;
        }

        static double SlotScore(Player player, Position slot)
        {
            var suitability = player.SuitabilityAt(slot);
            return player.Overall * PositionUtility.Multiplier(suitability)
                   + player.EffectiveOffense(slot)
                   + player.EffectiveDefense(slot);
        }

        static Player[] BuildBench(List<Player> remaining, bool conditionFirst)
        {
            var pool = new List<Player>(remaining.Count);
            for (int i = 0; i < remaining.Count; i++)
            {
                if (remaining[i] != null) pool.Add(remaining[i]);
            }

            pool.Sort((a, b) => CompareBench(a, b, conditionFirst));
            int count = Math.Min(TeamSheet.MaxBench, pool.Count);
            var bench = new Player[count];
            for (int i = 0; i < count; i++)
                bench[i] = pool[i];
            return bench;
        }

        static int CompareBench(Player a, Player b, bool conditionFirst)
        {
            if (conditionFirst)
            {
                int byCondition = b.Condition.CompareTo(a.Condition);
                if (byCondition != 0) return byCondition;
            }

            int byScore = BenchScore(b).CompareTo(BenchScore(a));
            if (byScore != 0) return byScore;
            if (!conditionFirst)
            {
                int byCondition = b.Condition.CompareTo(a.Condition);
                if (byCondition != 0) return byCondition;
            }

            return string.CompareOrdinal(a.Id, b.Id);
        }

        static double BenchScore(Player player)
        {
            return SlotScore(player, player.BestPosition());
        }
    }
}
