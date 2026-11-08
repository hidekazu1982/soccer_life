using System;
using System.Collections.Generic;
using SoccerClub.Data;
using SoccerClub.Match;
using SoccerClub.Time;

namespace SoccerClub.Competition
{
    /// <summary>
    /// 20チーム総当たり H&amp;A（38節）。同点時は得失点差 → 総得点 → 名鑑順。
    /// </summary>
    public sealed class LeagueService
    {
        public const int TeamCount = 20;

        readonly List<Club> _clubs;
        readonly List<Fixture> _fixtures = new List<Fixture>();

        public IReadOnlyList<Fixture> Fixtures => _fixtures;
        public IReadOnlyList<Club> Clubs => _clubs;

        public LeagueService(IReadOnlyList<Club> clubs)
        {
            if (clubs.Count != TeamCount)
                throw new ArgumentException("League requires 20 clubs.");
            _clubs = new List<Club>(clubs);
        }

        /// <summary>
        /// 後半が未消化のセーブは、後半の節をウィンターブレイク明けへずらす。
        /// すでに後半の結果があるセーブはそのままにする。
        /// </summary>
        public static void AlignToWinterBreak(IList<Fixture> fixtures, DateTime seasonStart)
        {
            if (fixtures == null) return;
            var lastFirst = WinterBreak.LastFirstHalf(seasonStart);
            var resume = WinterBreak.SecondHalfOpener(seasonStart);
            var earliest = DateTime.MaxValue;
            bool found = false;
            for (int i = 0; i < fixtures.Count; i++)
            {
                var fixture = fixtures[i];
                if (fixture == null || !fixture.IsLeague || fixture.Date.Date <= lastFirst) continue;
                if (fixture.Result != null) return;
                if (fixture.Date.Date < earliest)
                {
                    earliest = fixture.Date.Date;
                    found = true;
                }
            }

            if (!found || earliest >= resume) return;
            var shift = resume - earliest;
            for (int i = 0; i < fixtures.Count; i++)
            {
                var fixture = fixtures[i];
                if (fixture == null || !fixture.IsLeague || fixture.Date.Date <= lastFirst) continue;
                fixture.Date = fixture.Date.Date.Add(shift);
            }
        }

        public void ReplaceFixtures(IReadOnlyList<Fixture> fixtures)
        {
            _fixtures.Clear();
            if (fixtures == null) return;
            for (int i = 0; i < fixtures.Count; i++)
            {
                if (fixtures[i] != null) _fixtures.Add(fixtures[i]);
            }
        }

        /// <summary>
        /// 前半は切れ目最少の総当たり。後半は同じカードを逆順にし、ホームを入れ替える。
        /// 同じ側が続くのは 2 節までである。
        /// </summary>
        public void GenerateSeason(DateTime seasonStart)
        {
            _fixtures.Clear();
            var firstDates = WinterBreak.FirstHalfLeagueDates(seasonStart);
            var secondDates = WinterBreak.SecondHalfLeagueDates(seasonStart);
            int rounds = _clubs.Count - 1;
            if (firstDates == null || firstDates.Length != rounds || secondDates == null || secondDates.Length != rounds)
                throw new ArgumentException("League round dates must cover every round.");

            int index = 0;
            for (int day = 0; day < rounds; day++)
                AddRound(firstDates[day], day, false, ref index);
            for (int i = 0; i < rounds; i++)
                AddRound(secondDates[i], rounds - 1 - i, true, ref index);
        }

        /// <summary>
        /// 固定クラブと、残り 19 クラブを円周に並べた 1 節。
        /// 第1節から1節おきに固定クラブがホーム。円周上の相手は、近い側から交互にホームを取る。
        /// </summary>
        void AddRound(DateTime date, int day, bool flip, ref int index)
        {
            int count = _clubs.Count;
            int mod = count - 1;
            int half = count / 2;
            int fixedTeam = count - 1;
            bool fixedHome = day % 2 == 0;
            if (flip) fixedHome = !fixedHome;
            int opponent = day;
            AddFixture(date, fixedHome ? fixedTeam : opponent, fixedHome ? opponent : fixedTeam, ref index);

            for (int k = 1; k < half; k++)
            {
                int a = (day + k) % mod;
                int b = day - k;
                if (b < 0) b += mod;
                bool aHome = k % 2 == 1;
                if (flip) aHome = !aHome;
                AddFixture(date, aHome ? a : b, aHome ? b : a, ref index);
            }
        }

        void AddFixture(DateTime date, int home, int away, ref int index)
        {
            _fixtures.Add(new Fixture
            {
                Id = "L" + index++,
                Date = date.Date,
                Home = _clubs[home],
                Away = _clubs[away],
                IsLeague = true
            });
        }

        public void ApplyLeagueResult(Club home, Club away, MatchResult result)
        {
            home.RecordMatch(result.HomeGoals, result.AwayGoals);
            away.RecordMatch(result.AwayGoals, result.HomeGoals);
        }

        public List<Club> Table()
        {
            var table = new List<Club>(_clubs);
            table.Sort((a, b) =>
            {
                int p = b.LeaguePoints.CompareTo(a.LeaguePoints);
                if (p != 0) return p;
                int gd = b.GoalDifference.CompareTo(a.GoalDifference);
                if (gd != 0) return gd;
                int gf = b.GoalsFor.CompareTo(a.GoalsFor);
                if (gf != 0) return gf;
                return a.EmblemIndex.CompareTo(b.EmblemIndex);
            });
            return table;
        }

        /// <summary>最も早いリーグ戦日から、そのシーズンの 8 月 1 日を求める。</summary>
        public static bool TryInferSeasonStart(IReadOnlyList<Fixture> fixtures, out DateTime seasonStart)
        {
            var earliest = DateTime.MaxValue;
            if (fixtures != null)
            {
                for (int i = 0; i < fixtures.Count; i++)
                {
                    var fixture = fixtures[i];
                    if (fixture == null || !fixture.IsLeague) continue;
                    if (fixture.Date < earliest) earliest = fixture.Date;
                }
            }

            if (earliest == DateTime.MaxValue)
            {
                seasonStart = default;
                return false;
            }

            int year = earliest.Month >= 8 ? earliest.Year : earliest.Year - 1;
            seasonStart = new DateTime(year, 8, 1);
            if (WinterBreak.FirstHalfOpener(seasonStart) > earliest.Date)
                seasonStart = seasonStart.AddYears(-1);
            return true;
        }

        public void StorePreviousRanks()
        {
            var table = Table();
            for (int i = 0; i < table.Count; i++) table[i].PreviousLeagueRank = i + 1;
        }
    }
}
