using SoccerClub.Core;
using SoccerClub.Data;

namespace SoccerClub.Match
{
    public sealed class LineupSlot
    {
        public Position Slot;
        public Player Player;
    }

    public sealed class TeamSheet
    {
        public Formation Formation;
        public LineupSlot[] StartingXi = new LineupSlot[11];
        public Player[] Bench = new Player[0];
        public int SubstitutionsUsed;
        public const int MaxSubstitutions = 5;
        public const int MaxBench = 7;

        public Player Goalkeeper
        {
            get
            {
                for (int i = 0; i < StartingXi.Length; i++)
                {
                    if (StartingXi[i] != null && StartingXi[i].Slot == Position.GK)
                        return StartingXi[i].Player;
                }
                return StartingXi[0] != null ? StartingXi[0].Player : null;
            }
        }
    }

    public sealed class MatchTeamState
    {
        public Club Club;
        public TeamSheet Sheet;
        public int Goals;
    }

    public sealed class MatchResult
    {
        public Club Home;
        public Club Away;
        public int HomeGoals;
        public int AwayGoals;
        public bool DecidedByPenalty;
        public System.Collections.Generic.List<DuelEvent> Events = new System.Collections.Generic.List<DuelEvent>();
        public System.Collections.Generic.List<Player> HomeAppeared = new System.Collections.Generic.List<Player>();
        public System.Collections.Generic.List<Player> AwayAppeared = new System.Collections.Generic.List<Player>();
        public System.Collections.Generic.List<Player> HomeBench = new System.Collections.Generic.List<Player>();
        public System.Collections.Generic.List<Player> AwayBench = new System.Collections.Generic.List<Player>();
        public LineupSlot[] HomeXi = new LineupSlot[11];
        public LineupSlot[] AwayXi = new LineupSlot[11];
        public System.Collections.Generic.List<Substitution> Substitutions = new System.Collections.Generic.List<Substitution>();

        public Club Winner =>
            HomeGoals == AwayGoals ? null : (HomeGoals > AwayGoals ? Home : Away);
    }

    public sealed class DuelEvent
    {
        public int Round;
        public Club AttackClub;
        public Club DefenseClub;
        public Player Attacker;
        public Player[] Attackers = new Player[0];
        public Position[] AttackerSlots = new Position[0];
        public Player[] Defenders;
        public Position[] DefenderSlots = new Position[0];
        /// <summary>フィールドの守備者に続き、最後が GK。</summary>
        public Player[] DefenseLine = new Player[0];
        public Position[] DefenseSlots = new Position[0];
        /// <summary>脱落した順。true ならオフェンス側の StepIndex。</summary>
        public bool[] StepAttackerLost = new bool[0];
        public int[] StepIndex = new int[0];
        public Player Goalkeeper;
        public bool Breakthrough;
        public bool Goal;
    }

    public sealed class Substitution
    {
        public int Round;
        public bool Home;
        public int SlotIndex;
        public Player Out;
        public Player In;
    }
}
