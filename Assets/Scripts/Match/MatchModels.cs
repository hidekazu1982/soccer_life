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
        /// <summary>PK 戦で勝敗を決めたか。得点（HomeGoals / AwayGoals）には PK を足さない。</summary>
        public bool DecidedByPenalty;
        /// <summary>PK 戦の成功数。古いセーブは 0-0（得点に 1 足してある）。</summary>
        public int HomePk;
        public int AwayPk;
        /// <summary>PK 戦の勝者。古いセーブは null。</summary>
        public Club PenaltyWinner;
        /// <summary>PK 戦の 1 本ずつ（蹴った順）。セーブしない。</summary>
        public System.Collections.Generic.List<PenaltyKick> PenaltyKicks = new System.Collections.Generic.List<PenaltyKick>();
        public System.Collections.Generic.List<DuelEvent> Events = new System.Collections.Generic.List<DuelEvent>();
        public System.Collections.Generic.List<Player> HomeAppeared = new System.Collections.Generic.List<Player>();
        public System.Collections.Generic.List<Player> AwayAppeared = new System.Collections.Generic.List<Player>();
        public System.Collections.Generic.List<Player> HomeBench = new System.Collections.Generic.List<Player>();
        public System.Collections.Generic.List<Player> AwayBench = new System.Collections.Generic.List<Player>();
        public LineupSlot[] HomeXi = new LineupSlot[11];
        public LineupSlot[] AwayXi = new LineupSlot[11];
        public System.Collections.Generic.List<Substitution> Substitutions = new System.Collections.Generic.List<Substitution>();

        /// <summary>この試合の勝者。PK 戦で決めた試合は PK 戦の勝者。引き分けは null。</summary>
        public Club Winner =>
            DecidedByPenalty && PenaltyWinner != null
                ? PenaltyWinner
                : HomeGoals == AwayGoals ? null : (HomeGoals > AwayGoals ? Home : Away);

        /// <summary>club から見た勝敗。1 勝ち、-1 負け、0 引き分け。</summary>
        public int OutcomeFor(Club club)
        {
            var winner = Winner;
            if (winner == null) return 0;
            return winner == club ? 1 : -1;
        }

        /// <summary>「PK 5-4」（club 側を先に）。PK 戦でない試合は空。古いセーブは数を持たないので「PK」だけ。</summary>
        public string PenaltyText(Club club)
        {
            if (!DecidedByPenalty) return "";
            if (HomePk == 0 && AwayPk == 0) return "PK";
            bool home = club == null || club == Home;
            int mine = home ? HomePk : AwayPk;
            int theirs = home ? AwayPk : HomePk;
            return "PK " + mine + "-" + theirs;
        }
    }

    /// <summary>PK 戦の 1 本。キッカーと GK の 1 対 1。</summary>
    public sealed class PenaltyKick
    {
        public bool Home;
        public Player Kicker;
        public Player Keeper;
        public bool Scored;
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
