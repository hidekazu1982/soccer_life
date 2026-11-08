using System;
using System.Collections.Generic;
using System.Numerics;
using SoccerClub.Data;
using SoccerClub.Match;

namespace SoccerClub.Competition
{
    public sealed class Fixture
    {
        public string Id;
        public DateTime Date;
        public Club Home;
        public Club Away;
        public bool IsLeague;
        public bool IsCup;
        public int CupRound;
        public int Leg; // 1 or 2
        public MatchResult Result;
        public bool ConsequencesApplied;
        public BigInteger GateIncome;
        public BigInteger AdvertisingSpent;
        public BigInteger OperatingPaid;
        public BigInteger OperatingRequired;
        public BigInteger HomeAppearancePaid;
        public BigInteger HomeWinPaid;
        public BigInteger AwayAppearancePaid;
        public BigInteger AwayWinPaid;
        public int Attendance;
        public int StadiumCapacity;
    }

    public sealed class CupTie
    {
        public Club FirstHome;
        public Club FirstAway;
        public int FirstHomeGoals;
        public int FirstAwayGoals;
        public int SecondHomeGoals;
        public int SecondAwayGoals;
        public bool Complete;

        public int AggregateFor(Club club)
        {
            int sum = 0;
            if (club == FirstHome) sum += FirstHomeGoals + SecondAwayGoals;
            else sum += FirstAwayGoals + SecondHomeGoals;
            return sum;
        }
    }

    public interface ICompetitionCalendar
    {
        IReadOnlyList<Fixture> FixturesOn(DateTime date);
        void RecordResult(Fixture fixture, MatchResult result);
        IReadOnlyList<Club> LeagueTable();
    }
}
