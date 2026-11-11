using System;
using System.Collections.Generic;
using System.Globalization;
using SoccerClub.Competition;
using SoccerClub.Core;
using SoccerClub.Data;
using UnityEngine;

namespace SoccerClub.UI
{
    /// <summary>
    /// 試合後の順位表。Figma の ranking フレーム。
    /// 左に自クラブのシーズン日程（schedule）、右にリーグの順位表（ranking）。どちらも上下にスクロールできる。
    /// schedule は今日の試合を 420 に置き、未来の試合を上、過去の試合を下に 270 間隔で並べる。
    /// </summary>
    public sealed class RankingView
    {
        // schedule（187:318）。カード 240 角、間隔 270。今日の試合（Group 13）は 109,420。
        const float CardX = 109f;
        const float CardSize = 240f;
        const float CardPitch = 270f;
        const float CurrentCardY = 420f;
        static readonly Color PlayedCardColor = Hex(0xD1D1D6);
        static readonly Color FutureCardColor = Hex(0xF2F2F7);
        static readonly Rect ScheduleHit = new Rect(CardX, -2000f, CardSize, 5080f);

        // ranking（186:239）。見出し 441,80 / 1300×100、行は 100 ずつ。
        const float TableX = 441f;
        const float TableTop = 80f;
        const float TableWidth = 1300f;
        const float RowHeight = 100f;
        const float BodyTop = TableTop + RowHeight;
        const float BodyBottom = 956f;
        static readonly Rect BodyRect = new Rect(TableX, BodyTop, TableWidth, BodyBottom - BodyTop);
        static readonly Color HeaderColor = Hex(0xD1D1D6);
        static readonly Color OddRowColor = Hex(0xF2F2F7);
        static readonly Color EvenRowColor = Hex(0xE5E5EA);
        // Figma にはない。自クラブの行だけ薄い黄色にして、どこにいるか分かるようにする。
        static readonly Color MineRowColor = new Color(1f, 0.95f, 0.75f, 1f);
        // 列。順位 100、エンブレム 100、クラブ 400、数字 100×7。
        static readonly float[] NumberColumns = { 1041f, 1141f, 1241f, 1341f, 1441f, 1541f, 1641f };
        static readonly string[] NumberHeaders = { "勝点", "勝", "分", "負", "得点", "失点", "得失" };
        const float RankColumn = 441f;
        const float EmblemColumn = 541f;
        const float NameColumn = 641f;
        const float NameWidth = 400f;
        const float NamePadding = 20f;

        // scroll（186:244）。溝 1749,80 / 20×876、つまみは本文の見えている割合。
        static readonly Rect ScrollTrack = new Rect(1749f, TableTop, 20f, BodyBottom - TableTop);
        static readonly Color ScrollTrackColor = Hex(0x8E8E93);
        static readonly Color ScrollThumbColor = Hex(0xC7C7CC);

        const float WheelStep = 60f;

        readonly List<Card> _cards = new List<Card>();
        readonly List<Club> _table = new List<Club>();
        int _currentCard = -1;
        bool _prepared;
        float _scheduleScroll;
        float _tableScroll;
        Club _player;

        // ドラッグ中のリスト。0 はなし、1 は schedule、2 は ranking。
        int _dragging;
        float _dragLastY;

        GUIStyle _side;
        GUIStyle _cardText;
        GUIStyle _cardResult;
        GUIStyle _cell;
        GUIStyle _cellLeft;
        GUIStyle _buttonLabel;
        GUIStyle _hit;
        Texture2D _button;
        Font _font;

        struct Card
        {
            public DateTime Date;
            public Club Opponent;
            public string OpponentLabel;
            public bool Home;
            public string Title;
            public bool Played;
            public string Result;
        }

        /// <summary>画面を開くたびに呼ぶ。日程と順位を取り直し、スクロールを今日の試合と自クラブの行に合わせる。</summary>
        public void Reset()
        {
            _prepared = false;
            _dragging = 0;
        }

        public void Draw(GameSession session, Fixture today, Func<int, Texture2D> emblemOf, Font font)
        {
            if (session == null) return;
            EnsureStyles(font);
            if (!_prepared) Prepare(session, today);

            var screen = FullScreenRect();
            Fill(screen, Color.white);
            HandleInput(screen);
            DrawSchedule(screen, emblemOf);
            DrawTable(emblemOf);
        }

        void Prepare(GameSession session, Fixture today)
        {
            _prepared = true;
            _player = session.PlayerClub;
            CollectCards(session, today);
            _table.Clear();
            if (session.League != null) _table.AddRange(session.League.Table());

            _scheduleScroll = 0f;
            int mine = _table.IndexOf(_player);
            // 自クラブの行が本文の中央あたりに来るようにする。
            _tableScroll = ClampTable(mine < 0 ? 0f : mine * RowHeight - (BodyRect.height - RowHeight) * 0.5f);
        }

        void CollectCards(GameSession session, Fixture today)
        {
            _cards.Clear();
            _currentCard = -1;
            var club = session.PlayerClub;
            if (club == null) return;

            var fixtures = new List<Fixture>();
            AddFixtures(session.League != null ? session.League.Fixtures : null, club, fixtures);
            AddFixtures(session.Cup != null ? session.Cup.Fixtures : null, club, fixtures);
            fixtures.Sort((a, b) => a.Date.CompareTo(b.Date));

            int leagueRound = 0;
            for (int i = 0; i < fixtures.Count; i++)
            {
                var fixture = fixtures[i];
                bool home = fixture.Home == club;
                string title = fixture.IsLeague
                    ? "リーグ第" + (++leagueRound).ToString(CultureInfo.InvariantCulture) + "節"
                    : CupTitle(fixture.CupRound, fixture.Leg);
                if (fixture == today) _currentCard = _cards.Count;
                _cards.Add(new Card
                {
                    Date = fixture.Date,
                    Opponent = home ? fixture.Away : fixture.Home,
                    Home = home,
                    Title = title,
                    Played = fixture.Result != null,
                    Result = ResultText(fixture, home)
                });
            }

            // 対戦相手が決まっていないカップの試合。
            if (session.Cup != null)
            {
                var pending = new List<CupScheduleLine>();
                session.Cup.AppendPending(club, session.Time.CurrentDate, pending);
                for (int i = 0; i < pending.Count; i++)
                {
                    var line = pending[i];
                    _cards.Add(new Card
                    {
                        Date = line.Date,
                        OpponentLabel = "未定",
                        Home = line.Home,
                        Title = CupTitle(line.Round, line.Leg),
                        Played = false
                    });
                }
            }

            // 未定のカップを足したので並べ直す。今日の試合は日付と結果で探し直す。
            var current = _currentCard >= 0 ? (Card?)_cards[_currentCard] : null;
            _cards.Sort((a, b) => a.Date.CompareTo(b.Date));
            if (current.HasValue)
            {
                for (int i = 0; i < _cards.Count; i++)
                {
                    if (_cards[i].Date == current.Value.Date && _cards[i].Opponent == current.Value.Opponent)
                    {
                        _currentCard = i;
                        break;
                    }
                }
            }
            else
            {
                // 今日の試合が見つからなければ、最後に終わった試合（なければ最初の試合）を基準にする。
                _currentCard = 0;
                for (int i = 0; i < _cards.Count; i++)
                {
                    if (_cards[i].Played) _currentCard = i;
                }
            }
        }

        static void AddFixtures(IReadOnlyList<Fixture> source, Club club, List<Fixture> dest)
        {
            if (source == null) return;
            for (int i = 0; i < source.Count; i++)
            {
                var fixture = source[i];
                if (fixture == null || (fixture.Home != club && fixture.Away != club)) continue;
                dest.Add(fixture);
            }
        }

        static string CupTitle(int round, int leg)
        {
            return "カップ" + round.ToString(CultureInfo.InvariantCulture) + "回戦 第" + leg.ToString(CultureInfo.InvariantCulture) + "戦";
        }

        /// <summary>自クラブから見た勝敗と「自分-相手」。PK 決着は数字のあとに PK を付ける。</summary>
        static string ResultText(Fixture fixture, bool home)
        {
            var result = fixture.Result;
            if (result == null) return null;
            int mine = home ? result.HomeGoals : result.AwayGoals;
            int theirs = home ? result.AwayGoals : result.HomeGoals;
            string outcome = mine > theirs ? "Win" : mine < theirs ? "Defeat" : "Draw";
            string text = outcome + " " + mine.ToString(CultureInfo.InvariantCulture) + "-" + theirs.ToString(CultureInfo.InvariantCulture);
            if (result.DecidedByPenalty) text += " PK";
            return text;
        }

        // ---- schedule

        void DrawSchedule(Rect screen, Func<int, Texture2D> emblemOf)
        {
            for (int i = 0; i < _cards.Count; i++)
            {
                float y = CardY(i);
                if (y + CardSize < screen.yMin || y > screen.yMax) continue;
                DrawCard(_cards[i], y, emblemOf);
            }
        }

        float CardY(int index)
        {
            return CurrentCardY + (_currentCard - index) * CardPitch + _scheduleScroll;
        }

        void DrawCard(Card card, float y, Func<int, Texture2D> emblemOf)
        {
            Fill(new Rect(CardX, y, CardSize, CardSize), card.Played ? PlayedCardColor : FutureCardColor);
            float center = CardX + CardSize * 0.5f;
            Label(center, y + 27.5f, card.Home ? "HOME" : "AWAY", _side);

            // 終わった試合はエンブレムが上（+39）、これからの試合は下（+50）。
            var emblemRect = new Rect(CardX + 54f, y + (card.Played ? 39f : 50f), 133f, 100f);
            var emblem = card.Opponent != null && emblemOf != null ? emblemOf(card.Opponent.EmblemIndex - 1) : null;
            if (emblem != null)
                GUI.DrawTexture(emblemRect, emblem, ScaleMode.ScaleToFit, true);
            else
                GUI.Label(emblemRect, card.OpponentLabel ?? (card.Opponent != null ? card.Opponent.Name : ""), _cardText);

            if (card.Played)
            {
                FitLabel(CardLine(y + 159.5f), card.Title, _cardText, 32);
                FitLabel(CardLine(y + 198.5f), card.Result ?? "", _cardResult, 32);
            }
            else
            {
                FitLabel(CardLine(y + 170.5f), card.Title, _cardText, 32);
                Label(center, y + 212.5f, card.Date.ToString("MM/dd", CultureInfo.InvariantCulture), _cardText);
            }
        }

        /// <summary>カードの中の 1 行。左右 6 ずつ空けた幅に収める。</summary>
        static Rect CardLine(float centerY)
        {
            return new Rect(CardX + 6f, centerY - 20f, CardSize - 12f, 40f);
        }

        float ClampSchedule(float value)
        {
            if (_cards.Count == 0) return 0f;
            // どのカードも今日の位置（420）まで動かせる範囲。
            float min = -_currentCard * CardPitch;
            float max = (_cards.Count - 1 - _currentCard) * CardPitch;
            return Mathf.Clamp(value, min, max);
        }

        // ---- ranking

        void DrawTable(Func<int, Texture2D> emblemOf)
        {
            Fill(new Rect(TableX, TableTop, TableWidth, RowHeight), HeaderColor);
            Label(RankColumn + 50f, TableTop + 50f, "順位", _cell);
            GUI.Label(new Rect(NameColumn + NamePadding, TableTop, NameWidth - NamePadding, RowHeight), "クラブ", _cellLeft);
            for (int i = 0; i < NumberHeaders.Length; i++)
                Label(NumberColumns[i] + 50f, TableTop + 50f, NumberHeaders[i], _cell);

            GUI.BeginGroup(BodyRect);
            for (int i = 0; i < _table.Count; i++)
            {
                float y = i * RowHeight - _tableScroll;
                if (y + RowHeight < 0f || y > BodyRect.height) continue;
                DrawRow(_table[i], i + 1, y, emblemOf);
            }

            GUI.EndGroup();
            DrawScrollBar();
        }

        /// <summary>BeginGroup の中なので、x は TableX、y は BodyTop を引いた座標で描く。</summary>
        void DrawRow(Club club, int rank, float y, Func<int, Texture2D> emblemOf)
        {
            var color = club == _player ? MineRowColor : (rank & 1) == 1 ? OddRowColor : EvenRowColor;
            Fill(new Rect(0f, y, TableWidth, RowHeight), color);
            float mid = y + RowHeight * 0.5f;
            Label(RankColumn - TableX + 50f, mid, rank.ToString(CultureInfo.InvariantCulture), _cell);

            var emblem = emblemOf != null ? emblemOf(club.EmblemIndex - 1) : null;
            if (emblem != null)
                GUI.DrawTexture(new Rect(EmblemColumn - TableX, y + 12f, 100f, 76f), emblem, ScaleMode.ScaleToFit, true);

            var name = new Rect(NameColumn - TableX + NamePadding, y, NameWidth - NamePadding * 2f, RowHeight);
            FitLabel(name, club.Name ?? "", _cellLeft, 36);

            int[] values =
            {
                club.LeaguePoints, club.Wins, club.Draws, club.Losses, club.GoalsFor, club.GoalsAgainst, club.GoalDifference
            };
            for (int i = 0; i < values.Length; i++)
            {
                string text = values[i].ToString(CultureInfo.InvariantCulture);
                if (i == values.Length - 1 && values[i] > 0) text = "+" + text;
                FitLabel(new Rect(NumberColumns[i] - TableX, y, 100f, RowHeight), text, _cell, 36);
            }
        }

        void DrawScrollBar()
        {
            Fill(ScrollTrack, ScrollTrackColor);
            float content = _table.Count * RowHeight;
            if (content <= BodyRect.height) return;
            // つまみは見出しの下（本文の高さ）を動く。
            float span = BodyRect.height;
            float thumb = Mathf.Max(40f, span * BodyRect.height / content);
            float t = _tableScroll / (content - BodyRect.height);
            Fill(new Rect(ScrollTrack.x, BodyTop + (span - thumb) * t, ScrollTrack.width, thumb), ScrollThumbColor);
        }

        float ClampTable(float value)
        {
            float max = Mathf.Max(0f, _table.Count * RowHeight - BodyRect.height);
            return Mathf.Clamp(value, 0f, max);
        }

        // ---- input

        /// <summary>ホイールとドラッグ（タッチも同じ）で、ポインタの下のリストを動かす。</summary>
        void HandleInput(Rect screen)
        {
            var e = Event.current;
            if (e == null) return;
            var mouse = e.mousePosition;
            bool overSchedule = ScheduleHit.Contains(mouse) && mouse.y >= screen.yMin && mouse.y <= screen.yMax;
            bool overTable = new Rect(TableX, TableTop, ScrollTrack.xMax - TableX, BodyBottom - TableTop).Contains(mouse);
            switch (e.type)
            {
                case EventType.ScrollWheel:
                    if (overSchedule)
                    {
                        _scheduleScroll = ClampSchedule(_scheduleScroll - e.delta.y * WheelStep / 3f);
                        e.Use();
                    }
                    else if (overTable)
                    {
                        _tableScroll = ClampTable(_tableScroll + e.delta.y * WheelStep / 3f);
                        e.Use();
                    }

                    break;
                case EventType.MouseDown:
                    if (e.button != 0) break;
                    _dragging = overSchedule ? 1 : overTable ? 2 : 0;
                    _dragLastY = mouse.y;
                    if (_dragging != 0) e.Use();
                    break;
                case EventType.MouseDrag:
                    if (_dragging == 0) break;
                    float delta = mouse.y - _dragLastY;
                    _dragLastY = mouse.y;
                    // schedule は下に引くと上（未来）が見える。ranking は下に引くと上位が見える。
                    if (_dragging == 1) _scheduleScroll = ClampSchedule(_scheduleScroll + delta);
                    else _tableScroll = ClampTable(_tableScroll - delta);
                    e.Use();
                    break;
                case EventType.MouseUp:
                    if (_dragging != 0)
                    {
                        _dragging = 0;
                        e.Use();
                    }

                    break;
            }
        }

        /// <summary>オートでないときの「次へ」。sponsor のボタンと同じ button_01、155×60。</summary>
        public bool DrawNextButton(Rect rect, Font font)
        {
            EnsureStyles(font);
            if (_button == null)
                _button = Resources.Load<Texture2D>("MainScreen/button_01");
            if (_button != null)
                GUI.DrawTexture(rect, _button, ScaleMode.StretchToFill, true);
            else
                GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(1f, 0.8f, 0.2f, 1f), 0f, rect.height * 0.5f);
            GUI.Label(rect, "次へ", _buttonLabel);
            return GUI.Button(rect, GUIContent.none, _hit);
        }

        // ---- helpers

        static void Label(float centerX, float centerY, string text, GUIStyle style)
        {
            GUI.Label(new Rect(centerX - 200f, centerY - 30f, 400f, 60f), text, style);
        }

        /// <summary>幅に収まらないときだけ文字を小さくする。</summary>
        static void FitLabel(Rect rect, string text, GUIStyle style, int size)
        {
            int previous = style.fontSize;
            style.fontSize = size;
            var content = new GUIContent(text);
            while (style.fontSize > 16 && style.CalcSize(content).x > rect.width)
                style.fontSize -= 2;
            GUI.Label(rect, content, style);
            style.fontSize = previous;
        }

        static Rect FullScreenRect()
        {
            float ix = GameRunner.HorizontalEdgeInset;
            float iy = GameRunner.VerticalEdgeInset;
            return new Rect(-ix, -iy, GameRunner.ReferenceWidth + ix * 2f, GameRunner.ReferenceHeight + iy * 2f);
        }

        static void Fill(Rect rect, Color color)
        {
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, color, 0f, 0f);
        }

        static Color Hex(int rgb)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
        }

        void EnsureStyles(Font font)
        {
            if (_cell != null && _font == font) return;
            _font = font;
            var label = new GUIStyle(GUI.skin.label)
            {
                font = font,
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Overflow,
                wordWrap = false,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
                normal = { textColor = Color.black }
            };
            _side = new GUIStyle(label) { fontSize = 32, fontStyle = FontStyle.Bold };
            _cardText = new GUIStyle(label) { fontSize = 32 };
            _cardResult = new GUIStyle(label) { fontSize = 32, fontStyle = FontStyle.Bold };
            _cell = new GUIStyle(label) { fontSize = 36 };
            _cellLeft = new GUIStyle(label) { fontSize = 36, alignment = TextAnchor.MiddleLeft };
            _buttonLabel = new GUIStyle(label) { fontSize = 24 };
            _hit = new GUIStyle();
        }
    }
}
