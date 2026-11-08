using System;
using System.Collections.Generic;
using SoccerClub.Data;
using SoccerClub.Economy;
using SoccerClub.Match;

namespace SoccerClub.Competition
{
    /// <summary>リーグとカップの日程を束ね、当日の試合を処理する。</summary>
    public sealed class SeasonCalendar : ICompetitionCalendar
    {
        readonly LeagueService _league;
        readonly CupService _cup;
        readonly IMatchSimulation _match;
        readonly EconomyManager _economy;
        readonly Dictionary<string, MatchResult> _firstLegs = new Dictionary<string, MatchResult>();

        public SeasonCalendar(LeagueService league, CupService cup, IMatchSimulation match, EconomyManager economy)
        {
            _league = league;
            _cup = cup;
            _match = match;
            _economy = economy;
        }

        public IReadOnlyList<Fixture> FixturesOn(DateTime date)
        {
            var list = new List<Fixture>();
            AddOnDate(_league.Fixtures, date, list);
            AddOnDate(_cup.Fixtures, date, list);
            return list;
        }

        public IReadOnlyList<Fixture> PlayDate(DateTime date)
        {
            var today = FixturesOn(date);
            for (int i = 0; i < today.Count; i++)
                PlayFixture(today[i]);
            return today;
        }

        public void PlayFixture(Fixture fixture)
        {
            if (fixture == null || fixture.Result != null) return;

            bool cupSecondLeg = fixture.IsCup && fixture.Leg == 2;
            var result = _match.Simulate(fixture.Home, fixture.Away, false);
            RecordResult(fixture, result);
            var settlement = _economy.SettleHomeMatch(fixture.Home);
            fixture.GateIncome = settlement.Gate;
            fixture.AdvertisingSpent = settlement.AdvertisingSpent;
            fixture.OperatingPaid = settlement.OperatingSpent;
            fixture.OperatingRequired = settlement.OperatingRequired;
            fixture.Attendance = settlement.Attendance;
            fixture.StadiumCapacity = settlement.Capacity;
            var homeBonus = _economy.PayMatchBonuses(fixture.Home, result.HomeAppeared, result.Winner == fixture.Home);
            var awayBonus = _economy.PayMatchBonuses(fixture.Away, result.AwayAppeared, result.Winner == fixture.Away);
            fixture.HomeAppearancePaid = homeBonus.Appearance;
            fixture.HomeWinPaid = homeBonus.Win;
            fixture.AwayAppearancePaid = awayBonus.Appearance;
            fixture.AwayWinPaid = awayBonus.Win;
            if (cupSecondLeg) ResolveCupSecondLeg(fixture, result);
        }

        public void RecordResult(Fixture fixture, MatchResult result)
        {
            fixture.Result = result;
            if (fixture.IsLeague)
                _league.ApplyLeagueResult(fixture.Home, fixture.Away, result);
            if (fixture.IsCup && fixture.Leg == 1)
                _firstLegs[TieIdOf(fixture)] = result;
        }

        public IReadOnlyList<Club> LeagueTable() => _league.Table();

        /// <summary>新しいシーズンのカップは、同じ対戦IDを使う。前季の第1戦を残さない。</summary>
        public void ForgetCupLegs()
        {
            _firstLegs.Clear();
        }

        void ResolveCupSecondLeg(Fixture second, MatchResult secondResult)
        {
            var tieId = TieIdOf(second);
            if (!_firstLegs.TryGetValue(tieId, out var first))
            {
                first = FindFirstLeg(tieId);
                if (first == null) return;
            }

            var winner = _cup.ResolveTie(tieId, first, secondResult);
            if (winner != null) _cup.RegisterWinner(winner, second.CupRound, tieId);
        }

        MatchResult FindFirstLeg(string tieId)
        {
            for (int i = 0; i < _cup.Fixtures.Count; i++)
            {
                var f = _cup.Fixtures[i];
                if (f.Leg == 1 && TieIdOf(f) == tieId && f.Result != null)
                {
                    _firstLegs[tieId] = f.Result;
                    return f.Result;
                }
            }

            return null;
        }

        static string TieIdOf(Fixture fixture)
        {
            var id = fixture.Id;
            int dash = id.LastIndexOf('-');
            return dash > 0 ? id.Substring(0, dash) : id;
        }

        static void AddOnDate(IReadOnlyList<Fixture> source, DateTime date, List<Fixture> dest)
        {
            var day = date.Date;
            for (int i = 0; i < source.Count; i++)
            {
                if (source[i].Date.Date == day) dest.Add(source[i]);
            }
        }
    }
}
