using System.Numerics;
using SoccerClub.Data;
using SoccerClub.Economy;

namespace SoccerClub.Facilities
{
    public interface IFacilityService
    {
        bool TryUpgrade(Club club, FacilityKind kind);
        bool SecretarySuggestsUpgrade(Club club, out FacilityKind kind);
    }

    /// <summary>秘書は施設Lvアップを提案する。オーナー決裁は総資金から支払う。</summary>
    public sealed class FacilityService : IFacilityService
    {
        readonly EconomyManager _economy;

        public FacilityService(EconomyManager economy)
        {
            _economy = economy;
        }

        public bool TryUpgrade(Club club, FacilityKind kind)
        {
            var facility = club.Facilities.Get(kind);
            if (!facility.CanUpgrade()) return false;
            var cost = facility.NextUpgradeCost();
            if (!_economy.TrySpend(club, cost, "facility-" + kind)) return false;
            facility.Upgrade();
            return true;
        }

        public bool SecretarySuggestsUpgrade(Club club, out FacilityKind kind)
        {
            kind = FacilityKind.Stadium;
            if (club.Front.Secretary == null) return false;

            Facility best = null;
            foreach (var candidate in new[]
                     {
                         club.Facilities.Stadium,
                         club.Facilities.TrainingGround,
                         club.Facilities.TrainingFacility,
                         club.Facilities.Clubhouse
                     })
            {
                if (!candidate.CanUpgrade()) continue;
                if (best == null || candidate.Level < best.Level)
                    best = candidate;
            }

            if (best == null) return false;
            kind = best.Kind;
            return club.Coins >= best.NextUpgradeCost();
        }
    }
}
