using System;
using System.Collections.Generic;
using System.Globalization;
using SoccerClub.Competition;
using SoccerClub.Core;
using SoccerClub.Data;
using SoccerClub.Personnel;
using SoccerClub.Time;
using UnityEngine;

namespace SoccerClub.UI
{
    /// <summary>
    /// 新しいスタジアム（Figma の main_stadium_dialog_standings / _scorers / _rating。仕様書 版 1.39〜1.46）。
    /// 枠・看板・✕ はクラブハウスと同じで、タブは「順位」「得点」「評価」。
    /// 順位タブは左に順位表（192〜1040）、右に日程・結果（1072〜1704）。得点・評価はランキング（192〜1704）。
    /// 行は高さ 96 を 102 間隔で、エンブレムは 88 角。
    /// </summary>
    public sealed partial class ClubDialogView
    {
        // ---- タブ（y 265、x 192 / 440 / 688）。
        const float NsTabX = 192f;
        static readonly string[] NsTabLabels = { "順位", "得点", "評価" };
        static readonly StadiumPage[] NsTabPages = { StadiumPage.Table, StadiumPage.Goals, StadiumPage.Ratings };
        static readonly Rect NsInfo = new Rect(940f, 265f, 660f, 56f);

        // ---- 一覧の共通（見出し 329 / 56、行は 391 から）。
        const float NsHeadY = 329f;
        const float NsRowsY = 391f;
        const float NsRowsHeight = 534f;
        const float NsRowHeight = 96f;
        const float NsRowStride = 102f;
        const float NsEmblem = 88f;

        // ---- 順位表（192 から 848 幅）。
        static readonly Rect NsTableRows = new Rect(192f, NsRowsY, 848f, NsRowsHeight);
        static readonly Rect NsTableTrack = new Rect(1048f, NsRowsY, 10f, NsRowsHeight);
        static readonly float[] NsTableW = { 80f, 320f, 72f, 88f, 64f, 64f, 64f, 96f };
        static readonly string[] NsTableHead = { "順位", "クラブ", "試合", "勝点", "勝", "分", "負", "得失" };

        // ---- 日程・結果（1072 から 632 幅）。
        static readonly Rect NsScheduleRows = new Rect(1072f, NsRowsY, 632f, NsRowsHeight);
        static readonly Rect NsScheduleTrack = new Rect(1714f, NsRowsY, 10f, NsRowsHeight);
        static readonly float[] NsScheduleW = { 96f, 80f, 344f, 112f };
        static readonly string[] NsScheduleHead = { "節/日付", "H/A", "対戦相手", "結果" };

        // ---- ランキング（192 から 1512 幅）。
        static readonly Rect NsRankRows = new Rect(192f, NsRowsY, 1512f, NsRowsHeight);
        static readonly Rect NsRankTrack = new Rect(1714f, NsRowsY, 10f, NsRowsHeight);
        static readonly float[] NsRankW = { 100f, 100f, 72f, 360f, 84f, 400f, 124f, 272f };

        static readonly Color NsGold = Hex(0xE0A800);
        static readonly Color NsSilver = Hex(0x9AA0A6);
        static readonly Color NsBronze = Hex(0xB87333);
        static readonly Color NsPastRow = Hex(0xF6EBD0);
        static readonly Color NsWin = Hex(0xD9534F);
        static readonly Color NsDraw = Hex(0x8E8E93);
        static readonly Color NsLoss = Hex(0x2F5E9E);
        static readonly Color NsNext = Hex(0xB07A00);

        enum NsGameState
        {
            Past,
            Next,
            Future
        }

        sealed class NsGame
        {
            public DateTime Date;
            public string Section;
            public bool Home;
            public Club Opponent;
            public string OpponentName;
            public int Mine;
            public int Theirs;
            public bool HasResult;
            public int Outcome; // 1 勝、0 分、-1 負
            public NsGameState State;
        }

        sealed class NsRank
        {
            public Player Player;
            public Club Club;
            public int Goals;
            public int Matches;
            public double Average;
            public bool Own;
        }

        readonly List<Club> _nsTable = new List<Club>();
        readonly List<NsGame> _nsGames = new List<NsGame>();
        readonly List<NsRank> _nsRanks = new List<NsRank>();
        readonly List<CupScheduleLine> _nsPending = new List<CupScheduleLine>();
        string _nsDataKey;
        float _nsTableScroll;
        float _nsScheduleScroll;
        float _nsRankScroll;
        bool _nsScrollPlaced;
        int _nsDragList = -1;
        float _nsDragLastY;

        /// <summary>新しいスタジアムを描く。</summary>
        void DrawNewStadium(GameSession session, Club club)
        {
            EnsureNcStyles();
            if (_stadiumPage != StadiumPage.Goals && _stadiumPage != StadiumPage.Ratings)
                _stadiumPage = StadiumPage.Table;

            var previous = GUI.color;
            GUI.color = new Color(0.04f, 0.06f, 0.05f, 0.45f);
            GUI.DrawTexture(new Rect(-GameRunner.HorizontalEdgeInset, -GameRunner.VerticalEdgeInset,
                GameRunner.ReferenceWidth + GameRunner.HorizontalEdgeInset * 2f,
                GameRunner.ReferenceHeight + GameRunner.VerticalEdgeInset * 2f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            DrawNcFrame("スタジアム");

            if (session.GameOver)
                GUI.Label(new Rect(NcDialog.x, 232f, NcDialog.width, 28f), "資金ショートのためゲームオーバーです。", _ncSmallCenter);

            if (club != null)
            {
                CollectNsData(session, club);
                DrawNsInfo(session, club);
                if (_stadiumPage == StadiumPage.Table)
                {
                    DrawNsTable(club);
                    DrawNsSchedule();
                }
                else
                {
                    DrawNsRanking(_stadiumPage == StadiumPage.Goals);
                }
            }

            // タブと閉じるボタンは最後に置いて、一覧のドラッグより先に押せるようにする。
            for (int i = 0; i < NsTabLabels.Length; i++)
            {
                var tab = new Rect(NsTabX + NcTabStep * i, NcTabY, NcTabWidth, NcTabHeight);
                bool on = _stadiumPage == NsTabPages[i];
                if (on) NcBox(tab, NcYellow, NcDarkBrown, 4f, 28f);
                else NcBox(tab, NcTabOff, NcPanelLine, 3f, 28f);
                NcLabel(tab, NsTabLabels[i], _ncBold, on ? NcDarkBrown : NcHeadBrown);
                if (!on && GUI.Button(tab, GUIContent.none, _hit))
                {
                    _stadiumPage = NsTabPages[i];
                    _nsRankScroll = 0f;
                    _nsDataKey = null;
                }
            }

            NcRound(new Rect(NcClose.x, NcClose.y + 4f, NcClose.width, NcClose.height), NcShade(0.35f), 50f);
            NcBox(NcClose, NcYellow, NcDarkBrown, 5f, 50f);
            GUI.Label(NcClose, "✕", _ncClose);
            if (GUI.Button(NcClose, GUIContent.none, _hit))
                Close();

            GUI.color = previous;
        }

        /// <summary>タブの右：リーグ LV と、スタジアムのレベル・収容（施設の情報はここに残す）。</summary>
        void DrawNsInfo(GameSession session, Club club)
        {
            var stadium = club.Facilities.Get(FacilityKind.Stadium);
            string text = "リーグLV " + session.LeagueLevel.ToString(CultureInfo.InvariantCulture)
                          + "　スタジアム Lv" + stadium.Level.ToString(CultureInfo.InvariantCulture)
                          + "・収容 " + club.Facilities.StadiumCapacity().ToString("N0", CultureInfo.InvariantCulture) + "人";
            var style = _ncCellRight;
            var color = style.normal.textColor;
            style.normal.textColor = NcHeadBrown;
            FitNcLabel(NsInfo, text, style, 24);
            style.normal.textColor = color;
        }

        // ---- データ（日付・タブが変わったときだけ作り直す）

        void CollectNsData(GameSession session, Club club)
        {
            int played = 0;
            for (int i = 0; i < session.League.Fixtures.Count; i++)
                if (session.League.Fixtures[i] != null && session.League.Fixtures[i].Result != null) played++;
            string key = _stadiumPage + "|" + session.Time.CurrentDate.Ticks.ToString(CultureInfo.InvariantCulture)
                         + "|" + played.ToString(CultureInfo.InvariantCulture);
            if (_nsDataKey == key) return;
            _nsDataKey = key;

            if (_stadiumPage == StadiumPage.Table)
            {
                _nsTable.Clear();
                _nsTable.AddRange(session.League.Table());
                CollectNsGames(session, club);
                if (!_nsScrollPlaced)
                {
                    _nsScrollPlaced = true;
                    int own = _nsTable.IndexOf(club);
                    _nsTableScroll = own < 0 ? 0f : Mathf.Max(0f, (own - 1) * NsRowStride);
                    int next = _nsGames.FindIndex(g => g.State == NsGameState.Next);
                    _nsScheduleScroll = next < 0 ? Mathf.Max(0f, _nsGames.Count * NsRowStride) : Mathf.Max(0f, (next - 4) * NsRowStride);
                }
            }
            else
            {
                CollectNsRanks(session, _stadiumPage == StadiumPage.Goals);
            }
        }

        /// <summary>自クラブのリーグ戦とカップ戦（日付順）。リーグは日付順に数えて「第N節」、カップは「カップN」。</summary>
        void CollectNsGames(GameSession session, Club club)
        {
            _nsGames.Clear();
            var today = session.Time.CurrentDate.Date;
            int section = 0;
            var league = new List<Fixture>();
            AddNsFixtures(session.League.Fixtures, club, league);
            league.Sort((a, b) => a.Date.CompareTo(b.Date));
            for (int i = 0; i < league.Count; i++)
            {
                section++;
                _nsGames.Add(NsGameOf(club, league[i], "第" + section.ToString(CultureInfo.InvariantCulture) + "節"));
            }

            if (session.Cup != null)
            {
                var cup = new List<Fixture>();
                AddNsFixtures(session.Cup.Fixtures, club, cup);
                for (int i = 0; i < cup.Count; i++)
                    _nsGames.Add(NsGameOf(club, cup[i], "カップ" + cup[i].CupRound.ToString(CultureInfo.InvariantCulture)));

                _nsPending.Clear();
                session.Cup.AppendPending(club, session.Time.CurrentDate, _nsPending);
                for (int i = 0; i < _nsPending.Count; i++)
                {
                    var line = _nsPending[i];
                    _nsGames.Add(new NsGame
                    {
                        Date = line.Date,
                        Section = "カップ" + line.Round.ToString(CultureInfo.InvariantCulture),
                        Home = line.Home,
                        OpponentName = line.Opponent
                    });
                }
            }

            _nsGames.Sort((a, b) => a.Date.CompareTo(b.Date));
            bool nextFound = false;
            for (int i = 0; i < _nsGames.Count; i++)
            {
                var g = _nsGames[i];
                if (g.HasResult) g.State = NsGameState.Past;
                else if (!nextFound && g.Date.Date >= today)
                {
                    g.State = NsGameState.Next;
                    nextFound = true;
                }
                else g.State = NsGameState.Future;
            }
        }

        static void AddNsFixtures(IReadOnlyList<Fixture> source, Club club, List<Fixture> dest)
        {
            if (source == null) return;
            for (int i = 0; i < source.Count; i++)
            {
                var f = source[i];
                if (f != null && (f.Home == club || f.Away == club)) dest.Add(f);
            }
        }

        static NsGame NsGameOf(Club club, Fixture fixture, string section)
        {
            bool home = fixture.Home == club;
            var opponent = home ? fixture.Away : fixture.Home;
            var game = new NsGame
            {
                Date = fixture.Date,
                Section = section,
                Home = home,
                Opponent = opponent,
                OpponentName = opponent != null ? opponent.Name : ""
            };
            if (fixture.Result != null)
            {
                game.HasResult = true;
                game.Mine = home ? fixture.Result.HomeGoals : fixture.Result.AwayGoals;
                game.Theirs = home ? fixture.Result.AwayGoals : fixture.Result.HomeGoals;
                var winner = fixture.Result.Winner;
                game.Outcome = winner == null ? 0 : winner == club ? 1 : -1;
            }

            return game;
        }

        /// <summary>今シーズンの得点（個人に付いた分）か、評価の平均（評価した試合がある選手）。</summary>
        void CollectNsRanks(GameSession session, bool goals)
        {
            _nsRanks.Clear();
            if (session.Clubs == null) return;
            var mine = session.PlayerClub;
            for (int c = 0; c < session.Clubs.Count; c++)
            {
                var club = session.Clubs[c];
                if (club == null || club.Squad == null) continue;
                for (int i = 0; i < club.Squad.Count; i++)
                {
                    var p = club.Squad[i];
                    if (p == null) continue;
                    if (goals && p.SeasonGoals <= 0) continue;
                    if (!goals && p.SeasonRatedMatches <= 0) continue;
                    _nsRanks.Add(new NsRank
                    {
                        Player = p,
                        Club = club,
                        Goals = p.SeasonGoals,
                        Matches = p.SeasonRatedMatches,
                        Average = p.SeasonRatedMatches > 0 ? p.SeasonRatingSum / p.SeasonRatedMatches : 0d,
                        Own = club == mine
                    });
                }
            }

            if (goals)
                _nsRanks.Sort((a, b) =>
                {
                    int g = b.Goals.CompareTo(a.Goals);
                    return g != 0 ? g : string.CompareOrdinal(a.Player.Name, b.Player.Name);
                });
            else
                _nsRanks.Sort((a, b) =>
                {
                    int v = b.Average.CompareTo(a.Average);
                    if (v != 0) return v;
                    int m = b.Matches.CompareTo(a.Matches);
                    return m != 0 ? m : string.CompareOrdinal(a.Player.Name, b.Player.Name);
                });
        }

        // ---- 順位表

        void DrawNsTable(Club own)
        {
            DrawNsHead(NsTableRows.x, NsTableW, NsTableHead, 1);
            float content = NsContent(_nsTable.Count);
            UpdateNsScroll(0, NsTableRows, NsTableTrack, content, ref _nsTableScroll);
            GUI.BeginGroup(NsTableRows);
            for (int i = 0; i < _nsTable.Count; i++)
            {
                float y = i * NsRowStride - _nsTableScroll;
                if (y + NsRowHeight < 0f || y > NsTableRows.height) continue;
                var club = _nsTable[i];
                var row = new Rect(0f, y, NsTableRows.width, NsRowHeight);
                bool mine = club == own;
                DrawNsRow(row, mine ? Color.white : NcListRow, mine);
                float x = 0f;
                NsRankBand(new Rect(x, y, NsTableW[0], NsRowHeight), i + 1);
                x += NsTableW[0];
                DrawNsEmblem(new Rect(x + 12f, y + 4f, NsEmblem, NsEmblem), club);
                FitNcLabel(new Rect(x + 12f + NsEmblem + 10f, y, NsTableW[1] - NsEmblem - 34f, NsRowHeight), club.Name ?? "", _ncCellLeft, 24);
                x += NsTableW[1];
                int played = club.Wins + club.Draws + club.Losses;
                int gd = club.GoalDifference;
                string[] values =
                {
                    played.ToString(CultureInfo.InvariantCulture),
                    club.LeaguePoints.ToString(CultureInfo.InvariantCulture),
                    club.Wins.ToString(CultureInfo.InvariantCulture),
                    club.Draws.ToString(CultureInfo.InvariantCulture),
                    club.Losses.ToString(CultureInfo.InvariantCulture),
                    (gd > 0 ? "+" : "") + gd.ToString(CultureInfo.InvariantCulture)
                };
                for (int v = 0; v < values.Length; v++)
                {
                    FitNcLabel(new Rect(x, y, NsTableW[v + 2], NsRowHeight), values[v], _ncCell, 24);
                    x += NsTableW[v + 2];
                }

                if (mine) NcOutline(row, NcYellow, 4f, 16f);
            }

            GUI.EndGroup();
            DrawNsScrollbar(NsTableTrack, NsTableRows.height, content, _nsTableScroll);
        }

        // ---- 日程・結果

        void DrawNsSchedule()
        {
            DrawNsHead(NsScheduleRows.x, NsScheduleW, NsScheduleHead, 2, 20);
            float content = NsContent(_nsGames.Count);
            UpdateNsScroll(1, NsScheduleRows, NsScheduleTrack, content, ref _nsScheduleScroll);
            if (_nsGames.Count == 0)
            {
                GUI.Label(NsScheduleRows, "予定の試合はありません。", _ncCell);
                return;
            }

            GUI.BeginGroup(NsScheduleRows);
            for (int i = 0; i < _nsGames.Count; i++)
            {
                float y = i * NsRowStride - _nsScheduleScroll;
                if (y + NsRowHeight < 0f || y > NsScheduleRows.height) continue;
                DrawNsGame(_nsGames[i], new Rect(0f, y, NsScheduleRows.width, NsRowHeight));
            }

            GUI.EndGroup();
            DrawNsScrollbar(NsScheduleTrack, NsScheduleRows.height, content, _nsScheduleScroll);
        }

        void DrawNsGame(NsGame g, Rect row)
        {
            bool next = g.State == NsGameState.Next;
            // 試合後の順位表では、次の試合ではなく今日の試合を強調する（_nsHighlightDate）。
            bool today = _nsHighlightDate.HasValue && g.HasResult && g.Date.Date == _nsHighlightDate.Value.Date;
            bool focus = _nsHighlightDate.HasValue ? today : next;
            var fill = focus ? Color.white : g.State == NsGameState.Past ? NsPastRow : next ? Color.white : NcListRow;
            DrawNsRow(row, fill, focus);

            // 節と日付（上下 2 段）。
            float x = row.x;
            var section = new Rect(x, row.y, NsScheduleW[0], row.height);
            NcRound(section, NcBrown, new Vector4(16f, 0f, 0f, 16f));
            FitNcLabel(new Rect(section.x + 4f, row.y + 16f, section.width - 8f, 32f), g.Section, _ncPosText, 22);
            int dateSize = _ncSmallCenter.fontSize;
            _ncSmallCenter.fontSize = 20;
            NcLabel(new Rect(section.x, row.y + 50f, section.width, 28f), g.Date.ToString("MM/dd", CultureInfo.InvariantCulture), _ncSmallCenter, NcCream);
            _ncSmallCenter.fontSize = dateSize;
            x += NsScheduleW[0];

            // HOME / AWAY の札（68×32）。
            var badge = new Rect(x + (NsScheduleW[1] - 68f) * 0.5f, row.y + 32f, 68f, 32f);
            NcRound(badge, g.Home ? NcBrown : NcMaleFill, 16f);
            NsSmallLabel(badge, g.Home ? "HOME" : "AWAY", 16, g.Home ? Color.white : NcMaleText);
            x += NsScheduleW[1];

            // 対戦相手（エンブレム 88 角＋名前）。
            DrawNsEmblem(new Rect(x + 8f, row.y + 4f, NsEmblem, NsEmblem), g.Opponent);
            FitNcLabel(new Rect(x + 8f + NsEmblem + 8f, row.y, NsScheduleW[2] - NsEmblem - 28f, row.height), g.OpponentName ?? "", _ncCellLeft, 24);
            x += NsScheduleW[2];

            // 結果：スコアを左、勝分負の丸い札を右。次の試合は「次の試合」の文字（枠は付けない）。
            var result = new Rect(x, row.y, NsScheduleW[3], row.height);
            if (g.HasResult)
            {
                string score = g.Mine.ToString(CultureInfo.InvariantCulture) + "-" + g.Theirs.ToString(CultureInfo.InvariantCulture);
                FitNcLabel(new Rect(result.x, row.y, 64f, row.height), score, _ncCellRight, 22);
                var mark = new Rect(result.x + 70f, row.y + 30f, 36f, 36f);
                NcRound(mark, g.Outcome > 0 ? NsWin : g.Outcome < 0 ? NsLoss : NsDraw, 18f);
                NsSmallLabel(mark, g.Outcome > 0 ? "勝" : g.Outcome < 0 ? "負" : "分", 20, Color.white);
            }
            else if (next)
            {
                NsSmallLabel(result, "次の試合", 20, NsNext);
            }
            else
            {
                NcLabel(result, "-", _ncCell, NcBrown);
            }

            if (focus) NcOutline(row, NcYellow, 4f, 16f);
        }

        // ---- 得点・評価のランキング

        void DrawNsRanking(bool goals)
        {
            string[] head = { "順位", "POS", "M/F", "選手", "年齢", "クラブ", "試合", goals ? "得点" : "平均評価" };
            DrawNsHead(NsRankRows.x, NsRankW, head, -1);
            float content = NsContent(_nsRanks.Count);
            UpdateNsScroll(2, NsRankRows, NsRankTrack, content, ref _nsRankScroll);
            if (_nsRanks.Count == 0)
            {
                GUI.Label(NsRankRows, goals ? "まだ得点はありません。" : "まだ評価はありません。", _ncCell);
                return;
            }

            GUI.BeginGroup(NsRankRows);
            for (int i = 0; i < _nsRanks.Count; i++)
            {
                float y = i * NsRowStride - _nsRankScroll;
                if (y + NsRowHeight < 0f || y > NsRankRows.height) continue;
                DrawNsRankRow(_nsRanks[i], i + 1, new Rect(0f, y, NsRankRows.width, NsRowHeight), goals);
            }

            GUI.EndGroup();
            DrawNsScrollbar(NsRankTrack, NsRankRows.height, content, _nsRankScroll);
        }

        void DrawNsRankRow(NsRank r, int rank, Rect row, bool goals)
        {
            DrawNsRow(row, r.Own ? Color.white : NcListRow, r.Own);
            float x = row.x;
            float y = row.y;
            NsRankBand(new Rect(x, y, NsRankW[0], row.height), rank);
            x += NsRankW[0];

            // POS（64×36、ポジションカラー）。
            var group = PositionUtility.GroupOf(r.Player.BestPosition());
            var pos = new Rect(x + (NsRankW[1] - 64f) * 0.5f, y + 30f, 64f, 36f);
            NcRound(pos, PositionUtility.GroupColor(group), 12f);
            NsSmallLabel(pos, group.ToString(), 20, group == PositionGroup.MF ? NcText : Color.white);
            x += NsRankW[1];

            // M/F（48×72）。
            bool female = r.Player.Gender == Gender.Female;
            var mf = new Rect(x + (NsRankW[2] - 48f) * 0.5f, y + 12f, 48f, 72f);
            NcRound(mf, female ? NcFemaleFill : NcMaleFill, 12f);
            NcLabel(mf, female ? "F" : "M", _ncPosText, female ? NcFemaleText : NcMaleText);
            x += NsRankW[2];

            FitNcLabel(new Rect(x + 16f, y, NsRankW[3] - 32f, row.height), r.Player.Name ?? "", _ncCellLeft, 24);
            x += NsRankW[3];
            GUI.Label(new Rect(x, y, NsRankW[4], row.height), r.Player.Age.ToString(CultureInfo.InvariantCulture), _ncCell);
            x += NsRankW[4];

            DrawNsEmblem(new Rect(x + 16f, y + 4f, NsEmblem, NsEmblem), r.Club);
            FitNcLabel(new Rect(x + 16f + NsEmblem + 10f, y, NsRankW[5] - NsEmblem - 42f, row.height), r.Club.Name ?? "", _ncCellLeft, 24);
            x += NsRankW[5];
            GUI.Label(new Rect(x, y, NsRankW[6], row.height), r.Matches.ToString(CultureInfo.InvariantCulture), _ncCell);
            x += NsRankW[6];

            // 値（28px の太字、上位 3 人は #8B5A2B）。得点は後ろに小さく「点」。
            var valueColor = rank <= 3 ? NcBrown : NcText;
            string value = goals ? r.Goals.ToString(CultureInfo.InvariantCulture) : r.Average.ToString("0.00", CultureInfo.InvariantCulture);
            if (goals)
            {
                NcLabel(new Rect(x, y, NsRankW[7] * 0.5f + 8f, row.height), value, _ncOverall, valueColor);
                NsSmallLabel(new Rect(x + NsRankW[7] * 0.5f + 12f, y + 4f, 28f, row.height), "点", 20, NcHeadBrown);
            }
            else
            {
                var previous = _ncOverall.alignment;
                _ncOverall.alignment = TextAnchor.MiddleCenter;
                NcLabel(new Rect(x, y, NsRankW[7], row.height), value, _ncOverall, valueColor);
                _ncOverall.alignment = previous;
            }

            if (r.Own) NcOutline(row, NcYellow, 4f, 16f);
        }

        // ---- 部品

        /// <summary>見出しの帯（#6B4423、角丸 16）。leftColumn の列だけ左寄せ（16 あける）。</summary>
        void DrawNsHead(float x, float[] widths, string[] labels, int leftColumn, int size = 24)
        {
            float total = 0f;
            for (int i = 0; i < widths.Length; i++) total += widths[i];
            NcRound(new Rect(x, NsHeadY, total, NcHeadHeight), NcHeadBrown, 16f);
            for (int i = 0; i < widths.Length; i++)
            {
                var cell = new Rect(x, NsHeadY, widths[i], NcHeadHeight);
                bool left = i == leftColumn || (leftColumn < 0 && (i == 3 || i == 5));
                if (left)
                {
                    var style = _ncCellLeft;
                    var color = style.normal.textColor;
                    style.normal.textColor = NcCream;
                    FitNcLabel(new Rect(cell.x + 16f, cell.y, cell.width - 16f, cell.height), labels[i], style, size);
                    style.normal.textColor = color;
                }
                else
                {
                    FitNcLabel(cell, labels[i], _ncHeadText, i == 0 ? size : 24);
                }

                x += widths[i];
            }
        }

        /// <summary>行の地（角丸 16、線 #E2CFA5 の 2）。強調する行は呼び出し側で最後に黄色の線を重ねる。</summary>
        static void DrawNsRow(Rect row, Color fill, bool highlight)
        {
            NcRound(row, fill, 16f);
            if (!highlight) NcOutline(row, NcRowLine, 2f, 16f);
        }

        /// <summary>順位の札（行の左端、左だけ角丸）。1 位は金、2 位は銀、3 位は銅、ほかは茶色。</summary>
        void NsRankBand(Rect band, int rank)
        {
            var color = rank == 1 ? NsGold : rank == 2 ? NsSilver : rank == 3 ? NsBronze : NcBrown;
            NcRound(band, color, new Vector4(16f, 0f, 0f, 16f));
            NcLabel(band, rank.ToString(CultureInfo.InvariantCulture), _ncPosText, Color.white);
        }

        void DrawNsEmblem(Rect rect, Club club)
        {
            var emblem = EmblemOf(club);
            if (emblem != null) GUI.DrawTexture(rect, emblem, ScaleMode.ScaleToFit, true);
        }

        void NsSmallLabel(Rect rect, string text, int size, Color color)
        {
            int previous = _ncPosText.fontSize;
            _ncPosText.fontSize = size;
            NcLabel(rect, text, _ncPosText, color);
            _ncPosText.fontSize = previous;
        }

        static float NsContent(int count)
        {
            return count == 0 ? 0f : count * NsRowStride - (NsRowStride - NsRowHeight);
        }

        static void DrawNsScrollbar(Rect track, float view, float content, float scroll)
        {
            float maxScroll = Mathf.Max(0f, content - view);
            if (maxScroll <= 0.5f) return;
            NcRound(track, NcRowLine, 5f);
            float thumbH = Mathf.Clamp(track.height * view / content, 40f, track.height);
            float span = Mathf.Max(1f, track.height - thumbH);
            NcRound(new Rect(track.x, track.y + span * (scroll / maxScroll), track.width, thumbH), NcBrown, 5f);
        }

        /// <summary>ホイールとドラッグで縦にスクロールする（list は 0 順位表、1 日程、2 ランキング）。</summary>
        void UpdateNsScroll(int list, Rect rows, Rect track, float content, ref float scroll)
        {
            float maxScroll = Mathf.Max(0f, content - rows.height);
            scroll = Mathf.Clamp(scroll, 0f, maxScroll);
            var e = Event.current;
            if (e == null) return;
            var area = new Rect(rows.x, rows.y, track.xMax - rows.x, rows.height);
            bool over = area.Contains(e.mousePosition);
            if (e.type == EventType.ScrollWheel && over)
            {
                scroll = Mathf.Clamp(scroll + e.delta.y * 24f, 0f, maxScroll);
                e.Use();
            }
            else if (e.type == EventType.MouseDown && e.button == 0 && over)
            {
                _nsDragList = list;
                _nsDragLastY = e.mousePosition.y;
                e.Use();
            }
            else if (_nsDragList == list && e.type == EventType.MouseDrag)
            {
                scroll = Mathf.Clamp(scroll - (e.mousePosition.y - _nsDragLastY), 0f, maxScroll);
                _nsDragLastY = e.mousePosition.y;
                e.Use();
            }
            else if (_nsDragList == list && e.type == EventType.MouseUp)
            {
                _nsDragList = -1;
                e.Use();
            }
        }

        /// <summary>開き直したときは、自クラブの行と次の試合が見える位置から始める。</summary>
        void ResetNewStadium()
        {
            _nsDataKey = null;
            _nsScrollPlaced = false;
            _nsTableScroll = 0f;
            _nsScheduleScroll = 0f;
            _nsRankScroll = 0f;
            _nsDragList = -1;
        }
    }
}
