using System;
using System.Collections.Generic;
using SoccerClub.Core;
using SoccerClub.Data;

namespace SoccerClub.Match
{
    /// <summary>
    /// 全10回抽選の試合。攻守決定 → デュエル → 突破時は GK との最終抽選。
    /// スタミナは前半から常時、(現在Sta/最大Sta) を Off/Def に乗算する。
    /// </summary>
    public sealed class MatchSimulationManager : IMatchSimulation
    {
        public const int DrawCount = 10;
        public const double AllPlayersStaminaDrain = 3d;
        public const double ParticipantExtraDrain = 7d;

        readonly ILineupSelector _lineupSelector;
        readonly GameRandom _random;

        public MatchSimulationManager(ILineupSelector lineupSelector, GameRandom random = null)
        {
            _lineupSelector = lineupSelector;
            _random = random ?? GameRandom.Create();
        }

        public MatchResult Simulate(Club home, Club away, bool allowPenaltyIfDraw)
        {
            var resultEvents = new List<DuelEvent>();
            var homeState = new MatchTeamState { Club = home, Sheet = _lineupSelector.BuildSheet(home) };
            var awayState = new MatchTeamState { Club = away, Sheet = _lineupSelector.BuildSheet(away) };
            var homeMatchday = Snapshot(homeState.Sheet);
            var awayMatchday = Snapshot(awayState.Sheet);
            var homeXi = CloneXi(homeState.Sheet);
            var awayXi = CloneXi(awayState.Sheet);
            var homeAppeared = new HashSet<Player>();
            var awayAppeared = new HashSet<Player>();
            var substitutions = new List<Substitution>();
            ResetSheetStamina(homeState.Sheet);
            ResetSheetStamina(awayState.Sheet);
            NoteAppeared(homeState.Sheet, homeAppeared);
            NoteAppeared(awayState.Sheet, awayAppeared);

            for (int round = 0; round < DrawCount; round++)
            {
                bool homeAttacks = RollAttacker(homeState, awayState);
                var attack = homeAttacks ? homeState : awayState;
                var defense = homeAttacks ? awayState : homeState;
                ResolveAttackRound(round, attack, defense, resultEvents);
                DrainForRound(homeState, awayState, resultEvents[resultEvents.Count - 1]);
                TrySubstitute(homeState, true, round, substitutions);
                TrySubstitute(awayState, false, round, substitutions);
                NoteAppeared(homeState.Sheet, homeAppeared);
                NoteAppeared(awayState.Sheet, awayAppeared);
            }

            var result = new MatchResult
            {
                Home = home,
                Away = away,
                HomeGoals = homeState.Goals,
                AwayGoals = awayState.Goals,
                Events = resultEvents,
                HomeAppeared = new List<Player>(homeAppeared),
                AwayAppeared = new List<Player>(awayAppeared),
                HomeBench = Unappeared(homeMatchday, homeAppeared),
                AwayBench = Unappeared(awayMatchday, awayAppeared),
                HomeXi = homeXi,
                AwayXi = awayXi,
                Substitutions = substitutions
            };

            if (result.HomeGoals == result.AwayGoals && allowPenaltyIfDraw)
            {
                ResolvePenalty(result, homeState, awayState);
            }

            return result;
        }

        public MatchResult PenaltyShootout(Club home, Club away, int homeGoals, int awayGoals)
        {
            var result = new MatchResult
            {
                Home = home,
                Away = away,
                HomeGoals = homeGoals,
                AwayGoals = awayGoals
            };
            var homeState = new MatchTeamState { Club = home, Sheet = _lineupSelector.BuildSheet(home) };
            var awayState = new MatchTeamState { Club = away, Sheet = _lineupSelector.BuildSheet(away) };
            ResolvePenalty(result, homeState, awayState);
            return result;
        }

        bool RollAttacker(MatchTeamState home, MatchTeamState away)
        {
            double h = TeamPower(home);
            double a = TeamPower(away);
            double total = h + a;
            if (total <= 0d) return _random.NextDouble() < 0.5d;
            return _random.NextDouble() < h / total;
        }

        static double TeamPower(MatchTeamState team)
        {
            double sum = 0d;
            var xi = team.Sheet.StartingXi;
            for (int i = 0; i < xi.Length; i++)
            {
                var slot = xi[i];
                if (slot == null || slot.Player == null) continue;
                sum += slot.Player.EffectiveOffense(slot.Slot);
            }
            return Math.Max(0.0001d, sum);
        }

        void ResolveAttackRound(int round, MatchTeamState attack, MatchTeamState defense, List<DuelEvent> events)
        {
            int attackerCount = 1;
            if (_random.NextDouble() < 0.1d)
            {
                attackerCount = 2;
                if (_random.NextDouble() < 0.1d) attackerCount = 3;
            }

            var attackers = Compact(WeightedPick(attack.Sheet, true, attackerCount));
            var attackerSlots = SlotsOf(attack.Sheet, attackers);
            int defenderCount = _random.NextDouble() < 0.45d ? 2 : 1;
            var field = Compact(WeightedPick(defense.Sheet, false, defenderCount));
            var gk = defense.Sheet.Goalkeeper;
            var defenseLine = new List<Player>(field);
            if (gk != null && !defenseLine.Contains(gk)) defenseLine.Add(gk);
            var defensePlayers = defenseLine.ToArray();
            var defenseSlots = SlotsOf(defense.Sheet, defensePlayers);

            var attackerLost = new List<bool>();
            var lostIndex = new List<int>();
            int a = 0;
            int d = 0;
            while (a < attackers.Length && d < defensePlayers.Length)
            {
                double off = attackers[a].EffectiveOffense(attackerSlots[a]);
                double def = defensePlayers[d].EffectiveDefense(defenseSlots[d]);
                if (Roll(off, def))
                {
                    attackerLost.Add(false);
                    lostIndex.Add(d);
                    d++;
                }
                else
                {
                    attackerLost.Add(true);
                    lostIndex.Add(a);
                    a++;
                }
            }

            int fieldLosses = 0;
            for (int i = 0; i < attackerLost.Count; i++)
            {
                if (!attackerLost[i] && lostIndex[i] < field.Length) fieldLosses++;
            }

            bool breakthrough = field.Length == 0 || fieldLosses >= field.Length;
            bool goal = defensePlayers.Length > 0 && d >= defensePlayers.Length && attackers.Length > 0;
            if (goal) attack.Goals++;
            var scorer = a < attackers.Length ? attackers[a] : (attackers.Length > 0 ? attackers[0] : null);

            events.Add(new DuelEvent
            {
                Round = round,
                AttackClub = attack.Club,
                DefenseClub = defense.Club,
                Attacker = scorer,
                Attackers = attackers,
                AttackerSlots = attackerSlots,
                Defenders = field,
                DefenderSlots = SlotsOf(defense.Sheet, field),
                DefenseLine = defensePlayers,
                DefenseSlots = defenseSlots,
                StepAttackerLost = attackerLost.ToArray(),
                StepIndex = lostIndex.ToArray(),
                Goalkeeper = gk,
                Breakthrough = breakthrough,
                Goal = goal
            });
        }

        static Player[] Compact(Player[] picked)
        {
            var list = new List<Player>();
            if (picked == null) return list.ToArray();
            for (int i = 0; i < picked.Length; i++)
            {
                if (picked[i] != null) list.Add(picked[i]);
            }

            return list.ToArray();
        }

        static Position[] SlotsOf(TeamSheet sheet, IList<Player> players)
        {
            var slots = new Position[players.Count];
            for (int i = 0; i < players.Count; i++)
                slots[i] = FindSlot(sheet, players[i]);
            return slots;
        }

        static LineupSlot[] CloneXi(TeamSheet sheet)
        {
            var copy = new LineupSlot[11];
            if (sheet == null || sheet.StartingXi == null) return copy;
            int count = Math.Min(11, sheet.StartingXi.Length);
            for (int i = 0; i < count; i++)
            {
                var slot = sheet.StartingXi[i];
                if (slot == null || slot.Player == null) continue;
                copy[i] = new LineupSlot { Slot = slot.Slot, Player = slot.Player };
            }

            return copy;
        }

        bool Roll(double left, double right)
        {
            double total = Math.Max(0.0001d, left + right);
            return _random.NextDouble() < left / total;
        }

        Player[] WeightedPick(TeamSheet sheet, bool byOffense, int count)
        {
            var result = new Player[count];
            var used = new HashSet<string>();
            for (int n = 0; n < count; n++)
            {
                double sum = 0d;
                for (int i = 0; i < sheet.StartingXi.Length; i++)
                {
                    var slot = sheet.StartingXi[i];
                    if (slot?.Player == null || used.Contains(slot.Player.Id)) continue;
                    if (IsGoalkeeper(slot)) continue;
                    sum += Weight(slot, byOffense);
                }

                if (sum <= 0d)
                {
                    result[n] = FirstUnused(sheet, used, true);
                    if (result[n] != null) used.Add(result[n].Id);
                    continue;
                }

                double cursor = _random.NextDouble() * sum;
                for (int i = 0; i < sheet.StartingXi.Length; i++)
                {
                    var slot = sheet.StartingXi[i];
                    if (slot?.Player == null || used.Contains(slot.Player.Id)) continue;
                    if (IsGoalkeeper(slot)) continue;
                    cursor -= Weight(slot, byOffense);
                    if (cursor <= 0d)
                    {
                        result[n] = slot.Player;
                        used.Add(slot.Player.Id);
                        break;
                    }
                }
            }

            return result;
        }

        static double Weight(LineupSlot slot, bool byOffense)
        {
            return byOffense ? slot.Player.EffectiveOffense(slot.Slot) : slot.Player.EffectiveDefense(slot.Slot);
        }

        static bool IsGoalkeeper(LineupSlot slot)
        {
            if (slot == null || slot.Player == null) return false;
            return slot.Slot == Position.GK || slot.Player.PreferredPosition == Position.GK;
        }

        static Player FirstUnused(TeamSheet sheet, HashSet<string> used, bool skipGoalkeeper)
        {
            for (int i = 0; i < sheet.StartingXi.Length; i++)
            {
                var slot = sheet.StartingXi[i];
                var player = slot?.Player;
                if (player == null || used.Contains(player.Id)) continue;
                if (skipGoalkeeper && IsGoalkeeper(slot)) continue;
                return player;
            }

            return null;
        }

        static Position FindSlot(TeamSheet sheet, Player player)
        {
            for (int i = 0; i < sheet.StartingXi.Length; i++)
            {
                if (sheet.StartingXi[i]?.Player == player) return sheet.StartingXi[i].Slot;
            }
            return player.PreferredPosition;
        }

        static List<Player> Snapshot(TeamSheet sheet)
        {
            var list = new List<Player>();
            if (sheet == null) return list;
            for (int i = 0; i < sheet.StartingXi.Length; i++)
            {
                var player = sheet.StartingXi[i]?.Player;
                if (player != null) list.Add(player);
            }

            if (sheet.Bench == null) return list;
            for (int i = 0; i < sheet.Bench.Length; i++)
            {
                if (sheet.Bench[i] != null) list.Add(sheet.Bench[i]);
            }

            return list;
        }

        static void NoteAppeared(TeamSheet sheet, HashSet<Player> appeared)
        {
            if (sheet == null) return;
            for (int i = 0; i < sheet.StartingXi.Length; i++)
            {
                var player = sheet.StartingXi[i]?.Player;
                if (player != null) appeared.Add(player);
            }
        }

        static List<Player> Unappeared(List<Player> matchday, HashSet<Player> appeared)
        {
            var bench = new List<Player>();
            for (int i = 0; i < matchday.Count; i++)
            {
                if (!appeared.Contains(matchday[i])) bench.Add(matchday[i]);
            }

            return bench;
        }

        static void ResetSheetStamina(TeamSheet sheet)
        {
            if (sheet == null) return;
            for (int i = 0; i < sheet.StartingXi.Length; i++)
                sheet.StartingXi[i]?.Player?.ResetMatchStamina();
            if (sheet.Bench == null) return;
            for (int i = 0; i < sheet.Bench.Length; i++)
                sheet.Bench[i]?.ResetMatchStamina();
        }

        /// <summary>
        /// スタメンのスタミナ合計が相手より少ないほど、そのチームの減り方を大きくする。
        /// 減ったスタミナ比だけ、実効攻撃と実効守備が下がる。
        /// </summary>
        static void DrainForRound(MatchTeamState home, MatchTeamState away, DuelEvent duel)
        {
            double homeSum = StaminaSum(home != null ? home.Sheet : null);
            double awaySum = StaminaSum(away != null ? away.Sheet : null);
            var involved = new HashSet<Player>();
            if (duel != null)
            {
                AddInvolved(involved, duel.Attackers);
                AddInvolved(involved, duel.Defenders);
                if (duel.Breakthrough && duel.Goalkeeper != null) involved.Add(duel.Goalkeeper);
            }

            DrainTeam(home != null ? home.Sheet : null, homeSum, awaySum, involved);
            DrainTeam(away != null ? away.Sheet : null, awaySum, homeSum, involved);
        }

        static void AddInvolved(HashSet<Player> involved, Player[] people)
        {
            if (people == null) return;
            for (int i = 0; i < people.Length; i++)
            {
                if (people[i] != null) involved.Add(people[i]);
            }
        }

        static double StaminaSum(TeamSheet sheet)
        {
            if (sheet == null || sheet.StartingXi == null) return 0d;
            double sum = 0d;
            for (int i = 0; i < sheet.StartingXi.Length; i++)
            {
                var player = sheet.StartingXi[i]?.Player;
                if (player != null) sum += Math.Max(0d, player.StaminaCurrent);
            }

            return sum;
        }

        static double DrainScale(double mine, double theirs)
        {
            if (mine >= theirs || theirs <= 0d) return 1d;
            return 1d + (theirs - mine) / theirs;
        }

        static void DrainTeam(TeamSheet sheet, double mine, double theirs, HashSet<Player> involved)
        {
            if (sheet == null) return;
            double scale = DrainScale(mine, theirs);
            for (int i = 0; i < sheet.StartingXi.Length; i++)
            {
                var player = sheet.StartingXi[i]?.Player;
                if (player == null) continue;
                double loss = AllPlayersStaminaDrain * scale;
                if (involved != null && involved.Contains(player))
                    loss += ParticipantExtraDrain * scale;
                player.StaminaCurrent = Math.Max(0d, player.StaminaCurrent - loss);
            }

            if (sheet.Bench == null) return;
            for (int i = 0; i < sheet.Bench.Length; i++)
            {
                var player = sheet.Bench[i];
                if (player == null) continue;
                player.StaminaCurrent = Math.Max(0d, player.StaminaCurrent - AllPlayersStaminaDrain);
            }
        }

        void TrySubstitute(MatchTeamState team, bool home, int round, List<Substitution> substitutions)
        {
            var sheet = team.Sheet;
            if (sheet.SubstitutionsUsed >= TeamSheet.MaxSubstitutions) return;
            if (sheet.Bench == null || sheet.Bench.Length == 0) return;

            int worstIndex = -1;
            double worstRatio = 1d;
            for (int i = 0; i < sheet.StartingXi.Length; i++)
            {
                var slot = sheet.StartingXi[i];
                if (slot?.Player == null) continue;
                double ratio = slot.Player.StaminaRatio();
                if (ratio < 0.45d && ratio < worstRatio)
                {
                    worstRatio = ratio;
                    worstIndex = i;
                }
            }

            if (worstIndex < 0) return;

            var targetSlot = sheet.StartingXi[worstIndex].Slot;
            int benchIndex = -1;
            double best = double.NegativeInfinity;
            for (int i = 0; i < sheet.Bench.Length; i++)
            {
                var candidate = sheet.Bench[i];
                if (candidate == null) continue;
                if (!PositionUtility.IsPlayable(candidate.SuitabilityAt(targetSlot))) continue;
                if (candidate.StaminaRatio() < 0.6d) continue;
                double score = candidate.EffectiveOffense(targetSlot) + candidate.EffectiveDefense(targetSlot);
                if (score > best)
                {
                    best = score;
                    benchIndex = i;
                }
            }

            if (benchIndex < 0) return;

            var outgoing = sheet.StartingXi[worstIndex].Player;
            var incoming = sheet.Bench[benchIndex];
            sheet.StartingXi[worstIndex].Player = incoming;
            sheet.Bench[benchIndex] = outgoing;
            sheet.SubstitutionsUsed++;
            if (substitutions != null)
            {
                substitutions.Add(new Substitution
                {
                    Round = round,
                    Home = home,
                    SlotIndex = worstIndex,
                    Out = outgoing,
                    In = incoming
                });
            }
        }

        void ResolvePenalty(MatchResult result, MatchTeamState home, MatchTeamState away)
        {
            int homePk = 0;
            int awayPk = 0;
            for (int i = 0; i < 5; i++)
            {
                if (Roll(TeamPower(home), TeamPower(away) * 0.35d)) homePk++;
                if (Roll(TeamPower(away), TeamPower(home) * 0.35d)) awayPk++;
            }

            while (homePk == awayPk)
            {
                bool h = Roll(TeamPower(home), TeamPower(away) * 0.35d);
                bool a = Roll(TeamPower(away), TeamPower(home) * 0.35d);
                if (h) homePk++;
                if (a) awayPk++;
            }

            result.DecidedByPenalty = true;
            if (homePk > awayPk) result.HomeGoals++;
            else result.AwayGoals++;
        }
    }
}
