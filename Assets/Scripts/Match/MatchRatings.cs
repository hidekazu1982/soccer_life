using System;
using System.Collections.Generic;
using SoccerClub.Competition;
using SoccerClub.Data;

namespace SoccerClub.Match
{
    public sealed class PlayerMatchRating
    {
        public Player Player;
        public int Goals;
        public double Score;
    }

    /// <summary>
    /// 出場を 6.0 とし、得点、攻守の成否、GK のセーブと失点、勝敗で 1.0〜10.0 に収める。
    /// </summary>
    public static class MatchRatings
    {
        public static List<PlayerMatchRating> ForClub(Fixture fixture, Club club)
        {
            var list = new List<PlayerMatchRating>();
            if (fixture == null || club == null || fixture.Result == null) return list;

            var result = fixture.Result;
            var map = new Dictionary<Player, PlayerMatchRating>();
            var appeared = club == result.Home ? result.HomeAppeared : result.AwayAppeared;
            if (appeared != null)
            {
                for (int i = 0; i < appeared.Count; i++)
                    Touch(map, appeared[i]);
            }

            var events = result.Events;
            if (events != null)
            {
                for (int i = 0; i < events.Count; i++)
                    ApplyEvent(map, events[i], club);
            }

            bool home = club == fixture.Home;
            int mine = home ? result.HomeGoals : result.AwayGoals;
            int theirs = home ? result.AwayGoals : result.HomeGoals;
            double bonus = mine > theirs ? 0.4d : mine < theirs ? -0.3d : 0d;
            foreach (var row in map.Values)
            {
                if (row == null || row.Player == null) continue;
                row.Score = Clamp(row.Score + bonus);
                list.Add(row);
            }

            list.Sort(Compare);
            return list;
        }

        static void ApplyEvent(Dictionary<Player, PlayerMatchRating> map, DuelEvent duel, Club club)
        {
            if (duel == null) return;
            if (duel.AttackClub == club)
            {
                var attackers = duel.Attackers != null && duel.Attackers.Length > 0
                    ? duel.Attackers
                    : (duel.Attacker != null ? new[] { duel.Attacker } : new Player[0]);
                for (int i = 0; i < attackers.Length; i++)
                {
                    var row = Touch(map, attackers[i]);
                    if (row == null) continue;
                    bool scorer = duel.Goal && attackers[i] == duel.Attacker;
                    if (scorer)
                    {
                        row.Goals++;
                        row.Score += 1.5d;
                    }
                    else if (Lost(duel, true, i))
                    {
                        row.Score -= 0.2d;
                    }
                }
            }

            if (duel.DefenseClub == club && duel.Defenders != null)
            {
                for (int i = 0; i < duel.Defenders.Length; i++)
                {
                    var defender = duel.Defenders[i];
                    if (defender == null) continue;
                    var row = Touch(map, defender);
                    row.Score += duel.Breakthrough ? -0.3d : 0.3d;
                }
            }

            if (duel.DefenseClub == club && duel.Goalkeeper != null && duel.Breakthrough)
            {
                var row = Touch(map, duel.Goalkeeper);
                row.Score += duel.Goal ? -0.5d : 0.7d;
            }
        }

        static PlayerMatchRating Touch(Dictionary<Player, PlayerMatchRating> map, Player player)
        {
            if (player == null) return null;
            PlayerMatchRating row;
            if (!map.TryGetValue(player, out row))
            {
                row = new PlayerMatchRating { Player = player, Score = 6d };
                map[player] = row;
            }

            return row;
        }

        static bool Lost(DuelEvent duel, bool attacker, int index)
        {
            if (duel.StepAttackerLost == null || duel.StepIndex == null) return false;
            int count = Math.Min(duel.StepAttackerLost.Length, duel.StepIndex.Length);
            for (int i = 0; i < count; i++)
            {
                if (duel.StepAttackerLost[i] == attacker && duel.StepIndex[i] == index) return true;
            }

            return false;
        }

        static int Compare(PlayerMatchRating a, PlayerMatchRating b)
        {
            int score = b.Score.CompareTo(a.Score);
            if (score != 0) return score;
            int goals = b.Goals.CompareTo(a.Goals);
            if (goals != 0) return goals;
            string left = a.Player != null ? a.Player.Name : "";
            string right = b.Player != null ? b.Player.Name : "";
            return string.Compare(left, right, StringComparison.Ordinal);
        }

        static double Clamp(double score)
        {
            if (score < 1d) return 1d;
            if (score > 10d) return 10d;
            return score;
        }
    }
}
