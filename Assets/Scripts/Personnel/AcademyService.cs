using System.Collections.Generic;
using SoccerClub.Core;
using SoccerClub.Data;
using SoccerClub.Economy;
using SoccerClub.World;

namespace SoccerClub.Personnel
{
    /// <summary>6月15日に、17歳以上で最も総合力の高いアカデミー選手を所属へ上げる。</summary>
    public sealed class AcademyService
    {
        readonly EconomyManager _economy;

        public AcademyService(EconomyManager economy)
        {
            _economy = economy;
        }

        public bool TryPromote(Club club, GameRandom random, double youthScale = 1d)
        {
            if (club.Academy == null || club.Academy.Count == 0) return false;
            if (club.Squad.Count >= ClubMath.SquadLimit) return false;

            Player best = null;
            for (int i = 0; i < club.Academy.Count; i++)
            {
                var player = club.Academy[i];
                if (player == null || player.Age < 17) continue;
                if (best == null || player.Overall > best.Overall)
                    best = player;
            }

            if (best == null) return false;
            var salary = ClubMath.PlayerAnnual(best);
            if (!_economy.TrySpendReinforcement(club, salary, "academy-" + best.Id)) return false;

            club.Academy.Remove(best);
            club.Squad.Add(best);
            if (club.Academy.Count < ClubMath.AcademyLimit)
            {
                var youth = WorldFactory.CreatePlayer(
                    random,
                    club.Id + "-academy-" + random.Next(100000),
                    15,
                    GrowthPhase.Youth,
                    0.75d);
                if (youthScale > 1.0001d)
                    WorldFactory.ScaleOverall(youth, youth.Overall * youthScale);
                club.Academy.Add(youth);
            }

            return true;
        }

        public static void AgeAndRetire(Club club)
        {
            if (club.Academy == null) return;
            var leaving = new List<Player>();
            for (int i = 0; i < club.Academy.Count; i++)
            {
                var player = club.Academy[i];
                if (player == null) continue;
                player.Age++;
                if (player.Age >= 18) leaving.Add(player);
            }

            for (int i = 0; i < leaving.Count; i++)
                club.Academy.Remove(leaving[i]);
        }
    }
}
