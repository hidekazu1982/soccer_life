using System;
using System.Collections.Generic;
using SoccerClub.Core;
using SoccerClub.Data;
using SoccerClub.Match;
using SoccerClub.Time;

namespace SoccerClub.Competition
{
    /// <summary>
    /// カップは同20チームの H&amp;A トーナメント。
    /// 組み合わせは順位を使わず、開幕時に抽選する。
    /// 合計スコア同点は即PK（アウェイゴール無し）。
    /// </summary>
    public sealed class CupService
    {
        readonly IMatchSimulation _match;
        GameRandom _random;
        public readonly List<Fixture> Fixtures = new List<Fixture>();
        public readonly Dictionary<string, CupTie> Ties = new Dictionary<string, CupTie>();

        public CupService(IMatchSimulation match, GameRandom random = null)
        {
            _match = match;
            _random = random ?? GameRandom.Create();
        }

        public void Generate(IReadOnlyList<Club> clubs, DateTime seasonStart, GameRandom random)
        {
            Fixtures.Clear();
            Ties.Clear();
            _round1Winners.Clear();
            _laterWinners.Clear();
            _open.Clear();
            _round2Seeds = null;
            if (random != null) _random = random;

            var draw = new List<Club>(clubs);
            Shuffle(draw);

            // ベスト16が16枠になるよう、余りを1回戦に回す。20クラブなら8クラブ。
            const int roundOf16 = 16;
            int prelimTeams = (draw.Count - roundOf16) * 2;
            if (prelimTeams < 0) prelimTeams = 0;
            if (prelimTeams > draw.Count) prelimTeams = draw.Count - (draw.Count % 2);

            var cupDates = WinterBreak.CupDates(seasonStart);
            _round2Date = cupDates[2];

            var round1 = new List<Club>();
            for (int i = 0; i < prelimTeams; i++) round1.Add(draw[i]);
            PairHomeAndAway(round1, 1, cupDates[0], 0);

            var slots = new List<DrawSlot>();
            for (int i = prelimTeams; i < draw.Count; i++)
                slots.Add(DrawSlot.Known(draw[i]));
            for (int i = 0; i + 1 < round1.Count; i += 2)
                slots.Add(DrawSlot.Waiting("C1-" + i));

            Shuffle(slots);
            for (int i = 0; i + 1 < slots.Count; i += 2)
                PlaceRoundOf16(slots[i], slots[i + 1], i);
        }

        void PlaceRoundOf16(DrawSlot home, DrawSlot away, int index)
        {
            if (home.Club != null && away.Club != null)
            {
                var pair = new List<Club> { home.Club, away.Club };
                PairHomeAndAway(pair, 2, _round2Date, index);
                return;
            }

            _open.Add(new OpenTie
            {
                TieId = "C2-" + index,
                Round = 2,
                FirstLeg = _round2Date,
                Home = home.Club,
                HomeFeeder = home.Feeder,
                Away = away.Club,
                AwayFeeder = away.Feeder
            });
        }

        List<Club> _round2Seeds;
        DateTime _round2Date;
        readonly List<Club> _round1Winners = new List<Club>();
        readonly List<OpenTie> _open = new List<OpenTie>();

        void PairHomeAndAway(List<Club> teams, int cupRound, DateTime firstLegDate, int indexBase)
        {
            for (int i = 0; i + 1 < teams.Count; i += 2)
            {
                var a = teams[i];
                var b = teams[i + 1];
                var tieId = "C" + cupRound + "-" + (indexBase + i);
                if (Ties.ContainsKey(tieId)) continue;
                Ties[tieId] = new CupTie { FirstHome = a, FirstAway = b };
                Fixtures.Add(new Fixture
                {
                    Id = tieId + "-1",
                    Date = firstLegDate,
                    Home = a,
                    Away = b,
                    IsCup = true,
                    CupRound = cupRound,
                    Leg = 1
                });
                Fixtures.Add(new Fixture
                {
                    Id = tieId + "-2",
                    Date = firstLegDate.AddDays(7),
                    Home = b,
                    Away = a,
                    IsCup = true,
                    CupRound = cupRound,
                    Leg = 2
                });
            }
        }

        public Club ResolveTie(string tieId, MatchResult first, MatchResult second)
        {
            var tie = Ties[tieId];
            tie.FirstHomeGoals = first.HomeGoals;
            tie.FirstAwayGoals = first.AwayGoals;
            // 2nd はホームが FirstAway
            tie.SecondHomeGoals = second.HomeGoals;
            tie.SecondAwayGoals = second.AwayGoals;
            tie.Complete = true;

            int a = tie.FirstHomeGoals + tie.SecondAwayGoals;
            int b = tie.FirstAwayGoals + tie.SecondHomeGoals;
            if (a != b) return a > b ? tie.FirstHome : tie.FirstAway;

            // 同点は即PK。第2戦の得点を 1 足し、そのカードの勝者にする。
            var pk = _match.PenaltyShootout(tie.FirstAway, tie.FirstHome, b, a);
            var winner = pk.Winner ?? tie.FirstHome;
            if (winner == tie.FirstAway) second.HomeGoals++;
            else second.AwayGoals++;
            second.DecidedByPenalty = true;
            return winner;
        }

        public void RegisterRound1Winner(Club winner)
        {
            RegisterWinner(winner, 1, null);
        }

        public void RegisterWinner(Club winner, int completedRound, string completedTieId)
        {
            if (completedRound == 1)
            {
                if (!_round1Winners.Contains(winner)) _round1Winners.Add(winner);
                if (FillFeeder(completedTieId, winner)) return;
                if (_round1Winners.Count >= 4 && _round2Seeds != null)
                {
                    PairHomeAndAway(_round1Winners, 2, _round2Date, _round2Seeds.Count);
                    _round2Seeds = null;
                }
                return;
            }

            _laterWinners.TryGetValue(completedRound, out var list);
            if (list == null)
            {
                list = new List<Club>();
                _laterWinners[completedRound] = list;
            }

            if (!list.Contains(winner)) list.Add(winner);

            int expected = ExpectedWinners(completedRound);
            if (list.Count < expected) return;

            int nextRound = completedRound + 1;
            DateTime nextDate = _round2Date.AddDays(14 * (completedRound - 1));
            Shuffle(list);
            PairHomeAndAway(list, nextRound, nextDate, 0);
        }

        bool FillFeeder(string feederId, Club winner)
        {
            if (string.IsNullOrEmpty(feederId) || winner == null) return false;
            bool found = false;
            for (int i = _open.Count - 1; i >= 0; i--)
            {
                var tie = _open[i];
                if (tie.HomeFeeder == feederId)
                {
                    tie.Home = winner;
                    tie.HomeFeeder = null;
                    found = true;
                }
                if (tie.AwayFeeder == feederId)
                {
                    tie.Away = winner;
                    tie.AwayFeeder = null;
                    found = true;
                }

                if (tie.Home == null || tie.Away == null) continue;
                var pair = new List<Club> { tie.Home, tie.Away };
                PairHomeAndAway(pair, tie.Round, tie.FirstLeg, TieIndex(tie.TieId));
                _open.RemoveAt(i);
            }

            return found;
        }

        public void AppendPending(Club club, DateTime today, List<CupScheduleLine> dest)
        {
            if (club == null || dest == null) return;
            var day = today.Date;
            for (int i = 0; i < _open.Count; i++)
            {
                var tie = _open[i];
                bool home = tie.Home == club;
                bool away = tie.Away == club;
                if (!home && !away) continue;
                string opponent = home ? SlotLabel(tie.Away, tie.AwayFeeder) : SlotLabel(tie.Home, tie.HomeFeeder);
                if (tie.FirstLeg.Date >= day)
                {
                    dest.Add(new CupScheduleLine
                    {
                        Date = tie.FirstLeg,
                        Round = tie.Round,
                        Leg = 1,
                        Home = home,
                        Opponent = opponent
                    });
                }

                var second = tie.FirstLeg.AddDays(7);
                if (second.Date >= day)
                {
                    dest.Add(new CupScheduleLine
                    {
                        Date = second,
                        Round = tie.Round,
                        Leg = 2,
                        Home = !home,
                        Opponent = opponent
                    });
                }
            }
        }

        string SlotLabel(Club club, string feeder)
        {
            if (club != null) return club.Name;
            CupTie tie;
            if (!string.IsNullOrEmpty(feeder) && Ties.TryGetValue(feeder, out tie)
                && tie.FirstHome != null && tie.FirstAway != null)
                return tie.FirstHome.Name + " 対 " + tie.FirstAway.Name + " の勝者";
            return "1回戦の勝者";
        }

        void Shuffle<T>(IList<T> list)
        {
            if (list == null || _random == null) return;
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                var swap = list[i];
                list[i] = list[j];
                list[j] = swap;
            }
        }

        static int TieIndex(string tieId)
        {
            if (string.IsNullOrEmpty(tieId)) return 0;
            int dash = tieId.LastIndexOf('-');
            int index;
            if (dash < 0 || !int.TryParse(tieId.Substring(dash + 1), out index)) return 0;
            return index;
        }

        readonly Dictionary<int, List<Club>> _laterWinners = new Dictionary<int, List<Club>>();

        public CupSnapshot CaptureSnapshot()
        {
            var snap = new CupSnapshot();
            snap.Fixtures.AddRange(Fixtures);
            var tieIds = new List<string>(Ties.Keys);
            tieIds.Sort(StringComparer.Ordinal);
            for (int i = 0; i < tieIds.Count; i++)
            {
                snap.TieIds.Add(tieIds[i]);
                snap.Ties.Add(Ties[tieIds[i]]);
            }

            snap.HasRound2Seeds = _round2Seeds != null;
            if (_round2Seeds != null) snap.Round2Seeds.AddRange(_round2Seeds);
            snap.Round2Date = _round2Date;
            snap.Round1Winners.AddRange(_round1Winners);
            for (int i = 0; i < _open.Count; i++)
            {
                var tie = _open[i];
                snap.OpenTies.Add(new OpenTieSnapshot
                {
                    TieId = tie.TieId,
                    Round = tie.Round,
                    FirstLeg = tie.FirstLeg,
                    Home = tie.Home,
                    HomeFeeder = tie.HomeFeeder,
                    Away = tie.Away,
                    AwayFeeder = tie.AwayFeeder
                });
            }
            var rounds = new List<int>(_laterWinners.Keys);
            rounds.Sort();
            for (int i = 0; i < rounds.Count; i++)
            {
                snap.LaterRounds.Add(rounds[i]);
                snap.LaterWinners.Add(new List<Club>(_laterWinners[rounds[i]]));
            }

            return snap;
        }

        public void ApplySnapshot(CupSnapshot snap)
        {
            Fixtures.Clear();
            Ties.Clear();
            _round1Winners.Clear();
            _laterWinners.Clear();
            _open.Clear();
            _round2Seeds = null;
            if (snap == null) return;

            if (snap.Fixtures != null) Fixtures.AddRange(snap.Fixtures);
            int ties = snap.TieIds == null ? 0 : snap.TieIds.Count;
            for (int i = 0; i < ties; i++)
            {
                if (snap.Ties == null || i >= snap.Ties.Count) break;
                if (string.IsNullOrEmpty(snap.TieIds[i]) || snap.Ties[i] == null) continue;
                Ties[snap.TieIds[i]] = snap.Ties[i];
            }

            _round2Date = snap.Round2Date;
            if (snap.HasRound2Seeds)
                _round2Seeds = snap.Round2Seeds != null ? new List<Club>(snap.Round2Seeds) : new List<Club>();
            if (snap.Round1Winners != null)
            {
                for (int i = 0; i < snap.Round1Winners.Count; i++)
                {
                    if (snap.Round1Winners[i] != null) _round1Winners.Add(snap.Round1Winners[i]);
                }
            }

            int rounds = snap.LaterRounds == null ? 0 : snap.LaterRounds.Count;
            for (int i = 0; i < rounds; i++)
            {
                var list = new List<Club>();
                if (snap.LaterWinners != null && i < snap.LaterWinners.Count && snap.LaterWinners[i] != null)
                    list.AddRange(snap.LaterWinners[i]);
                _laterWinners[snap.LaterRounds[i]] = list;
            }

            if (snap.OpenTies != null)
            {
                for (int i = 0; i < snap.OpenTies.Count; i++)
                {
                    var tie = snap.OpenTies[i];
                    if (tie == null || string.IsNullOrEmpty(tie.TieId)) continue;
                    _open.Add(new OpenTie
                    {
                        TieId = tie.TieId,
                        Round = tie.Round,
                        FirstLeg = tie.FirstLeg,
                        Home = tie.Home,
                        HomeFeeder = tie.HomeFeeder,
                        Away = tie.Away,
                        AwayFeeder = tie.AwayFeeder
                    });
                }
            }

            EnsureSeededRound();
        }

        /// <summary>抽選導入前のセーブだけ、保存してある上位12のベスト16を補う。</summary>
        void EnsureSeededRound()
        {
            if (_round2Seeds == null || _round2Seeds.Count < 2 || _round2Date == default) return;
            PairHomeAndAway(_round2Seeds, 2, _round2Date, 0);
        }

        static int ExpectedWinners(int completedRound)
        {
            switch (completedRound)
            {
                case 2: return 8;
                case 3: return 4;
                case 4: return 2;
                default: return 0;
            }
        }
    }

    /// <summary>カップの未消化カードと、まだ対戦表になっていない勝者。セーブ用。</summary>
    public sealed class CupSnapshot
    {
        public readonly List<Fixture> Fixtures = new List<Fixture>();
        public readonly List<string> TieIds = new List<string>();
        public readonly List<CupTie> Ties = new List<CupTie>();
        public bool HasRound2Seeds;
        public readonly List<Club> Round2Seeds = new List<Club>();
        public DateTime Round2Date;
        public readonly List<Club> Round1Winners = new List<Club>();
        public readonly List<int> LaterRounds = new List<int>();
        public readonly List<List<Club>> LaterWinners = new List<List<Club>>();
        public readonly List<OpenTieSnapshot> OpenTies = new List<OpenTieSnapshot>();
    }

    public struct CupScheduleLine
    {
        public DateTime Date;
        public int Round;
        public int Leg;
        public bool Home;
        public string Opponent;
    }

    sealed class DrawSlot
    {
        public Club Club;
        public string Feeder;

        public static DrawSlot Known(Club club) => new DrawSlot { Club = club };
        public static DrawSlot Waiting(string tieId) => new DrawSlot { Feeder = tieId };
    }

    sealed class OpenTie
    {
        public string TieId;
        public int Round;
        public DateTime FirstLeg;
        public Club Home;
        public string HomeFeeder;
        public Club Away;
        public string AwayFeeder;
    }

    public sealed class OpenTieSnapshot
    {
        public string TieId;
        public int Round;
        public DateTime FirstLeg;
        public Club Home;
        public string HomeFeeder;
        public Club Away;
        public string AwayFeeder;
    }
}
