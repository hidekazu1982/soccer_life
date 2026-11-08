using SoccerClub.Data;

namespace SoccerClub.Match
{
    public interface ILineupSelector
    {
        TeamSheet BuildSheet(Club club);
    }

    public interface IMatchSimulation
    {
        MatchResult Simulate(Club home, Club away, bool allowPenaltyIfDraw);
        MatchResult PenaltyShootout(Club home, Club away, int homeGoals, int awayGoals);
    }
}
