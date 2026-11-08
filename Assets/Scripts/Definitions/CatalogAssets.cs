using SoccerClub.Core;
using SoccerClub.Data;
using UnityEngine;

namespace SoccerClub.Definitions
{
    [CreateAssetMenu(menuName = "SoccerClub/Player Template")]
    public sealed class PlayerTemplate : ScriptableObject
    {
        public string DisplayName;
        public int Age = 22;
        public GrowthPhase Phase;
        public Rarity Rarity;
        public PlayingStyle Style;
        public Position PreferredPosition;
        public double Offense = 50;
        public double Defense = 50;
        public double Stamina = 80;
        public int Condition = 80;
        public Suitability[] Suitabilities = new Suitability[13];

        public Player CreateRuntime(string id)
        {
            return new Player
            {
                Id = id,
                Name = DisplayName,
                Age = Age,
                Phase = Phase,
                Rarity = Rarity,
                Style = Style,
                PreferredPosition = PreferredPosition,
                Offense = Offense,
                Defense = Defense,
                StaminaMax = Stamina,
                StaminaCurrent = Stamina,
                Condition = Condition,
                Suitabilities = (Suitability[])Suitabilities.Clone()
            };
        }
    }

    [CreateAssetMenu(menuName = "SoccerClub/Staff Template")]
    public sealed class StaffTemplate : ScriptableObject
    {
        public string DisplayName;
        public int Age = 40;
        public int Level = 10;
        public bool IsFrontOffice = true;
        public FrontRole FrontRole;
        public TeamStaffRole TeamRole;
        public ReinforcementPolicy Policy = ReinforcementPolicy.Strength;
        public FormationType FavoriteFormation;
        public PlayingStyle FavoriteStyle;
        public LineupPriority LineupPriority;
        public PositionGroup ScoutedGroup;

        public StaffMember CreateRuntime(string id)
        {
            return new StaffMember
            {
                Id = id,
                Name = DisplayName,
                Age = Age,
                Level = Level,
                IsFrontOffice = IsFrontOffice,
                FrontRole = FrontRole,
                TeamRole = TeamRole,
                Policy = Policy,
                FavoriteFormation = FavoriteFormation,
                FavoriteStyle = FavoriteStyle,
                LineupPriority = LineupPriority,
                ScoutedGroup = ScoutedGroup
            };
        }
    }

    [CreateAssetMenu(menuName = "SoccerClub/Economy Config")]
    public sealed class EconomyConfig : ScriptableObject
    {
        public int CoinYen = 10000;
    }
}
