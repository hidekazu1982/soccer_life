using System.Collections.Generic;
using System.Globalization;
using SoccerClub.Competition;
using SoccerClub.Core;
using SoccerClub.Data;
using SoccerClub.Match;
using UnityEngine;

namespace SoccerClub.UI
{
    /// <summary>
    /// 試合中の重ね表示（版 1.60。Figma の game_clock / game_goal / game_substitution）。
    /// ・時計：チャンスごとに試合時間（分）を決め、スコアの枠の上の縁に「前半／後半 n'」を出す。
    /// ・ゴールの記録：エンブレムの下に、そのチームのゴールを「n' 名前」で 1 行ずつ。
    /// ・ゴール演出：シュートが入ったら、暗転・光・チームカラーの斜めの帯・「GOAL!!」・得点者の札。
    /// ・交代演出：前の回のあとに交代があれば、次の回の頭に小さな札をすっと出して消す。
    /// ・スコアの枠と上部の 11 人のカード（パーティの招待画面と同じ party_player_card の形）。
    /// </summary>
    public sealed partial class MatchScreenView
    {
        // ---- 時計 ----
        const float HalfMinutes = 45f;

        // ---- スコアの枠と時計の札 ----
        static readonly Rect ScoreFrame = new Rect(700f, 255.5f, 520f, 176f);
        static readonly Color CardCream = new Color(1f, 0.992f, 0.965f, 1f);
        static readonly Color CardLine = new Color(0.886f, 0.812f, 0.647f, 1f);
        static readonly Color CardBrown = new Color(0.231f, 0.165f, 0.102f, 1f);
        static readonly Color ClockInk = new Color(0.11f, 0.11f, 0.14f, 0.85f);
        static readonly Color AccentYellow = new Color(1f, 0.82f, 0.18f, 1f);
        static readonly Color InGreen = new Color(0.22f, 0.68f, 0.32f, 1f);
        static readonly Color OutPink = new Color(0.88f, 0.25f, 0.33f, 1f);

        // ---- 上部の 11 人のカード（party_player_card） ----
        const float CardWidth = 144f;
        const float CardHeight = 160f;
        const float CardStep = 168f;
        const float CardAvatarHeight = 112f;
        const float CardAvatarScale = 0.7f;

        // ---- ゴールの記録 ----
        const float GoalListTop = 462f;
        const float GoalRowHeight = 34f;
        const float GoalRowStep = 40f;
        const int GoalRowsMax = 5;

        // ---- ゴール演出（回の進みの単位。シュートが入った ShotEnd から） ----
        const float GoalFxLength = 1.3f;
        const float GoalFxIn = 0.14f;
        const float GoalFxOut = 0.2f;

        // ---- 交代演出（回の進みの単位） ----
        const float SubStart = 0.10f;
        const float SubLength = 0.85f;
        const float SubSlide = 0.10f;

        GUIStyle _ovClock;
        GUIStyle _ovSmall;
        GUIStyle _ovGoal;
        GUIStyle _ovCardName;
        GUIStyle _ovBadge;
        GUIStyle _ovBig;
        GUIStyle _ovScorer;
        Font _ovFont;

        readonly Dictionary<string, int[]> _minutes = new Dictionary<string, int[]>();

        /// <summary>
        /// チャンスごとの試合時間（分）。前半に半分（端数は前半）、後半に残りを置き、それぞれの 45 分を等分した区間の中で
        /// 試合ごとに少しずらす。同じ試合なら何度描いても同じ分になる（fixture の id から決める）。
        /// </summary>
        int[] MinutesOf(Fixture fixture, int count)
        {
            string key = (fixture != null ? fixture.Id : "") + "|" + count;
            int[] minutes;
            if (_minutes.TryGetValue(key, out minutes)) return minutes;
            minutes = new int[count];
            int first = (count + 1) / 2;
            uint seed = 2166136261u;
            string id = fixture != null ? fixture.Id ?? "" : "";
            for (int i = 0; i < id.Length; i++)
            {
                seed ^= id[i];
                seed *= 16777619u;
            }

            var random = new System.Random((int)(seed & 0x7fffffff));
            int previous = 0;
            for (int i = 0; i < count; i++)
            {
                bool firstHalf = i < first;
                int slots = firstHalf ? first : count - first;
                int index = firstHalf ? i : i - first;
                float span = HalfMinutes / Mathf.Max(1, slots);
                float start = (firstHalf ? 0f : HalfMinutes) + span * index;
                int minute = Mathf.FloorToInt(start + 1f + (float)random.NextDouble() * Mathf.Max(0f, span - 1f));
                int floor = firstHalf ? 1 : 46;
                int ceiling = firstHalf ? 45 : 90;
                minute = Mathf.Clamp(Mathf.Max(minute, previous + 1, floor), floor, ceiling);
                minutes[i] = minute;
                previous = minute;
            }

            _minutes[key] = minutes;
            return minutes;
        }

        static bool FirstHalf(int round, int count) => round < (count + 1) / 2;

        static string MinuteText(int minute) => minute.ToString(CultureInfo.InvariantCulture) + "'";

        /// <summary>シュートを打った攻撃側（最後の 1 手の前までに負けた人数ぶん、次の選手へ移る）。</summary>
        static Player ShooterOf(DuelEvent duel)
        {
            if (duel == null || duel.Attackers == null || duel.Attackers.Length == 0) return duel != null ? duel.Attacker : null;
            int count = Mathf.Min(3, duel.Attackers.Length);
            int steps = StepCount(duel);
            int holder = 0;
            for (int s = 0; s < steps - 1 && duel.StepAttackerLost != null && s < duel.StepAttackerLost.Length; s++)
                if (duel.StepAttackerLost[s]) holder = Mathf.Min(holder + 1, count - 1);
            return duel.Attackers[holder] ?? duel.Attacker;
        }

        // =========================================================== スコアの枠と時計

        void DrawScoreFrame()
        {
            GUI.color = Color.white;
            RoundRect(new Rect(ScoreFrame.x, ScoreFrame.y + 4f, ScoreFrame.width, ScoreFrame.height), new Color(0f, 0f, 0f, 0.25f), 24f);
            RoundRect(ScoreFrame, new Color(CardCream.r, CardCream.g, CardCream.b, 0.92f), 24f);
            RoundOutline(ScoreFrame, CardLine, 3f, 24f);
        }

        /// <summary>スコアの枠の上の縁に乗せる時計の札：黄色の「前半／後半」と白の「n'」。</summary>
        void DrawClock(Fixture fixture, List<DuelEvent> events, int round)
        {
            if (events == null || events.Count == 0) return;
            EnsureOverlayStyles();
            var minutes = MinutesOf(fixture, events.Count);
            int index = Mathf.Clamp(round, 0, events.Count - 1);
            string half = FirstHalf(index, events.Count) ? "前半" : "後半";
            string minute = MinuteText(minutes[index]);
            float halfWidth = _ovSmall.CalcSize(new GUIContent(half)).x + 24f;
            float minuteWidth = _ovClock.CalcSize(new GUIContent(minute)).x;
            float width = 24f + halfWidth + 12f + minuteWidth + 28f;
            var pill = new Rect(960f - width * 0.5f, ScoreFrame.y - 24f, width, 50f);
            RoundRect(pill, ClockInk, 25f);
            RoundOutline(pill, new Color(1f, 1f, 1f, 0.6f), 2f, 25f);
            var halfRect = new Rect(pill.x + 24f, pill.y + 11f, halfWidth, 28f);
            RoundRect(halfRect, AccentYellow, 12f);
            Label(halfRect, half, _ovSmall, new Color(0.11f, 0.11f, 0.14f, 1f));
            Label(new Rect(halfRect.xMax + 12f, pill.y, minuteWidth + 4f, pill.height), minute, _ovClock, Color.white);
        }

        // =========================================================== ゴールの記録

        /// <summary>エンブレムの下に、そのチームのゴールを 1 行ずつ。ホームは左寄せ、アウェイは右寄せ。多いときは新しい 5 本。</summary>
        void DrawGoalLists(Fixture fixture, MatchResult result, int round, bool scoredNow)
        {
            if (result == null || result.Events == null) return;
            EnsureOverlayStyles();
            var minutes = MinutesOf(fixture, result.Events.Count);
            for (int side = 0; side < 2; side++)
            {
                bool home = side == 0;
                var rows = new List<string>();
                var minutesOf = new List<string>();
                int last = scoredNow ? round : round - 1;
                for (int i = 0; i <= last && i < result.Events.Count; i++)
                {
                    var duel = result.Events[i];
                    if (duel == null || !duel.Goal) continue;
                    if ((duel.AttackClub == result.Home) != home) continue;
                    var shooter = ShooterOf(duel);
                    rows.Add(shooter != null ? shooter.Name ?? "" : "");
                    minutesOf.Add(MinuteText(minutes[i]));
                }

                int start = Mathf.Max(0, rows.Count - GoalRowsMax);
                for (int i = start; i < rows.Count; i++)
                    DrawGoalRow(home, GoalListTop + (i - start) * GoalRowStep, minutesOf[i], rows[i]);
            }
        }

        void DrawGoalRow(bool home, float y, string minute, string name)
        {
            float minuteWidth = _ovGoal.CalcSize(new GUIContent(minute)).x;
            float nameWidth = Mathf.Min(300f, _ovGoal.CalcSize(new GUIContent(name)).x);
            float width = 12f + 20f + 10f + minuteWidth + 10f + nameWidth + 16f;
            float x = home ? 40f : GameRunner.ReferenceWidth - 40f - width;
            var row = new Rect(x, y, width, GoalRowHeight);
            RoundRect(row, new Color(0f, 0f, 0f, 0.5f), GoalRowHeight * 0.5f);
            var ball = new Rect(row.x + 12f, row.y + 7f, 20f, 20f);
            RoundRect(ball, Color.white, 10f);
            RoundOutline(ball, new Color(0.11f, 0.11f, 0.14f, 1f), 3f, 10f);
            Label(new Rect(ball.xMax + 10f, row.y, minuteWidth + 2f, row.height), minute, _ovGoal, AccentYellow);
            Label(new Rect(ball.xMax + 20f + minuteWidth, row.y, nameWidth + 2f, row.height), name, _ovGoal, Color.white);
        }

        // =========================================================== 交代演出

        /// <summary>前の回のあとに交代があれば、この回の頭に札をすっと出して消す。ホームは左、アウェイは右。</summary>
        void DrawSubstitutions(Fixture fixture, MatchResult result, int round, float units)
        {
            if (result == null || result.Substitutions == null || round <= 0) return;
            if (units < SubStart || units > SubStart + SubLength) return;
            EnsureOverlayStyles();
            var minutes = MinutesOf(fixture, result.Events.Count);
            // 交代は前の回のあと。分は前の回と今の回の間をとる。
            int minute = (minutes[round - 1] + minutes[Mathf.Min(round, minutes.Length - 1)]) / 2;
            float t = (units - SubStart) / SubLength;
            float slide = Mathf.Clamp01((units - SubStart) / SubSlide);
            float alpha = Mathf.Clamp01(Smooth(slide)) * (1f - Mathf.Clamp01((t - 0.8f) / 0.2f));
            for (int i = 0; i < result.Substitutions.Count; i++)
            {
                var sub = result.Substitutions[i];
                if (sub == null || sub.Round != round - 1) continue;
                int goals = GoalsListedFor(result, round, sub.Home);
                float y = GoalListTop + Mathf.Min(goals, GoalRowsMax) * GoalRowStep + 8f;
                var club = sub.Home ? result.Home : result.Away;
                DrawSubstitutionCard(sub, club, minute, y, alpha, 1f - Smooth(slide));
            }
        }

        static int GoalsListedFor(MatchResult result, int round, bool home)
        {
            int goals = 0;
            for (int i = 0; i < round && i < result.Events.Count; i++)
            {
                var duel = result.Events[i];
                if (duel != null && duel.Goal && (duel.AttackClub == result.Home) == home) goals++;
            }

            return goals;
        }

        void DrawSubstitutionCard(Substitution sub, Club club, int minute, float y, float alpha, float offset)
        {
            if (alpha <= 0.01f) return;
            const float width = 560f;
            const float height = 96f;
            const float band = 110f;
            float x = sub.Home ? 40f - 60f * offset : GameRunner.ReferenceWidth - 40f - width + 60f * offset;
            var card = new Rect(x, y, width, height);
            var kit = club != null ? AvatarLibrary.Kit(club.EmblemIndex) : _standsKit;
            Color team = kit.First;

            RoundRect(new Rect(card.x, card.y + 4f, card.width, card.height), new Color(0f, 0f, 0f, 0.3f * alpha), 16f);
            RoundRect(card, new Color(1f, 1f, 1f, 0.94f * alpha), 16f);
            var bandRect = new Rect(card.x, card.y, band, card.height);
            GUI.DrawTexture(bandRect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f,
                new Color(team.r, team.g, team.b, alpha), Vector4.zero, new Vector4(16f, 0f, 0f, 16f));
            Label(new Rect(bandRect.x, bandRect.y + 10f, band, 40f), "交代", _ovClock, new Color(1f, 1f, 1f, alpha));
            Label(new Rect(bandRect.x, bandRect.y + 50f, band, 34f), MinuteText(minute), _ovSmall, new Color(AccentYellow.r, AccentYellow.g, AccentYellow.b, alpha));

            float rowX = card.x + band + 24f;
            DrawSubLine(new Rect(rowX, card.y + 10f, width - band - 40f, 36f), "▲ IN", InGreen, sub.In, alpha);
            DrawSubLine(new Rect(rowX, card.y + 50f, width - band - 40f, 36f), "▼ OUT", OutPink, sub.Out, alpha);
            GUI.color = Color.white;
        }

        void DrawSubLine(Rect row, string arrow, Color color, Player player, float alpha)
        {
            color.a = alpha;
            float arrowWidth = 88f;
            Label(new Rect(row.x, row.y, arrowWidth, row.height), arrow, _ovSmall, color);
            var badge = new Rect(row.x + arrowWidth, row.y + 5f, 48f, 26f);
            if (player != null)
            {
                RoundRect(badge, new Color(0.11f, 0.11f, 0.14f, alpha), 8f);
                Label(badge, player.PreferredPosition.ToString(), _ovBadge, new Color(1f, 1f, 1f, alpha));
            }
            Label(new Rect(badge.xMax + 12f, row.y, row.width - arrowWidth - 60f, row.height), player != null ? player.Name ?? "" : "", _ovCardName,
                new Color(CardBrown.r, CardBrown.g, CardBrown.b, alpha));
        }

        // =========================================================== ゴール演出

        /// <summary>
        /// シュートが入ったら（ShotEnd から GoalFxLength のあいだ）、画面を暗くし、黄色の光の筋を回し、
        /// 得点したクラブの色の斜めの帯を左から滑り込ませ、「GOAL!!」を大きく弾ませて出す。下に得点者の札（エンブレム・分・名前）。
        /// </summary>
        void DrawGoalEffect(Fixture fixture, MatchResult result, int round, DuelEvent duel, float units)
        {
            if (duel == null || !duel.Goal || units < ShotEnd || units > ShotEnd + GoalFxLength) return;
            EnsureOverlayStyles();
            float t = (units - ShotEnd) / GoalFxLength;
            float appear = Mathf.Clamp01((units - ShotEnd) / GoalFxIn);
            float fade = 1f - Mathf.Clamp01((t - (1f - GoalFxOut)) / GoalFxOut);
            float alpha = Smooth(appear) * fade;
            if (alpha <= 0.01f) return;

            float inset = GameRunner.HorizontalEdgeInset;
            float vertical = GameRunner.VerticalEdgeInset;
            var screen = new Rect(-inset, -vertical, GameRunner.ReferenceWidth + inset * 2f, GameRunner.ReferenceHeight + vertical * 2f);
            GUI.color = new Color(0f, 0f, 0f, 0.45f * alpha);
            GUI.DrawTexture(screen, Texture2D.whiteTexture);

            // 光の筋（中央から放射、ゆっくり回る）。
            var center = new Vector2(GameRunner.ReferenceWidth * 0.5f, 520f);
            var matrix = GUI.matrix;
            float spin = UnityEngine.Time.unscaledTime * 20f;
            for (int i = 0; i < 12; i++)
            {
                GUI.matrix = matrix;
                GUIUtility.RotateAroundPivot(spin + i * 30f, matrix.MultiplyPoint3x4(center));
                GUI.color = new Color(1f, 0.88f, 0.35f, 0.28f * alpha);
                GUI.DrawTexture(new Rect(center.x, center.y - 40f, 1400f, 80f), Texture2D.whiteTexture);
            }

            GUI.matrix = matrix;

            // チームカラーの斜めの帯（上下に黄色と白の線）。
            var club = duel.AttackClub;
            var kit = club != null ? AvatarLibrary.Kit(club.EmblemIndex) : _standsKit;
            Color team = kit.First;
            float slide = 1f - Smooth(appear);
            var bandRect = new Rect(-inset - 300f - 1600f * slide, 380f, screen.width + 600f, 300f);
            GUIUtility.RotateAroundPivot(6f, matrix.MultiplyPoint3x4(new Vector2(GameRunner.ReferenceWidth * 0.5f, 530f)));
            GUI.color = new Color(team.r * 0.6f, team.g * 0.6f, team.b * 0.6f, alpha);
            GUI.DrawTexture(bandRect, Texture2D.whiteTexture);
            GUI.color = new Color(team.r, team.g, team.b, alpha);
            GUI.DrawTexture(new Rect(bandRect.x, bandRect.y + 40f, bandRect.width, bandRect.height - 80f), Texture2D.whiteTexture);
            GUI.color = new Color(AccentYellow.r, AccentYellow.g, AccentYellow.b, alpha);
            GUI.DrawTexture(new Rect(bandRect.x, bandRect.y, bandRect.width, 14f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(bandRect.x, bandRect.yMax - 14f, bandRect.width, 14f), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.DrawTexture(new Rect(bandRect.x, bandRect.y + 24f, bandRect.width, 6f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(bandRect.x, bandRect.yMax - 30f, bandRect.width, 6f), Texture2D.whiteTexture);

            // 「GOAL!!」：大きく出て弾み、縁は黄色。
            float pop = 1f + 0.6f * (1f - Smooth(Mathf.Clamp01((units - ShotEnd - GoalFxIn * 0.5f) / (GoalFxIn * 1.5f))));
            var textRect = new Rect(0f, 380f, GameRunner.ReferenceWidth, 300f);
            GUIUtility.ScaleAroundPivot(new Vector2(pop, pop), GUI.matrix.MultiplyPoint3x4(textRect.center));
            for (int dx = -2; dx <= 2; dx++)
            for (int dy = -2; dy <= 2; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                Label(new Rect(textRect.x + dx * 4f, textRect.y + dy * 4f, textRect.width, textRect.height), "GOAL!!", _ovBig,
                    new Color(AccentYellow.r, AccentYellow.g, AccentYellow.b, alpha));
            }

            Label(new Rect(textRect.x + 10f, textRect.y + 14f, textRect.width, textRect.height), "GOAL!!", _ovBig, new Color(0.11f, 0.11f, 0.14f, 0.6f * alpha));
            Label(textRect, "GOAL!!", _ovBig, new Color(1f, 1f, 1f, alpha));
            GUI.matrix = matrix;

            // 得点者の札。
            var shooter = ShooterOf(duel);
            var minutes = MinutesOf(fixture, result.Events.Count);
            string minute = MinuteText(minutes[Mathf.Clamp(round, 0, minutes.Length - 1)]);
            string name = shooter != null ? shooter.Name ?? "" : "";
            float minuteWidth = _ovScorer.CalcSize(new GUIContent(minute)).x;
            float nameWidth = Mathf.Min(560f, _ovScorer.CalcSize(new GUIContent(name)).x);
            float cardWidth = 28f + 80f + 20f + minuteWidth + 20f + nameWidth + 36f;
            float rise = 30f * (1f - Smooth(Mathf.Clamp01((units - ShotEnd - GoalFxIn) / GoalFxIn)));
            var card = new Rect(960f - cardWidth * 0.5f, 820f + rise, cardWidth, 104f);
            RoundRect(new Rect(card.x, card.y + 4f, card.width, card.height), new Color(0f, 0f, 0f, 0.35f * alpha), 20f);
            RoundRect(card, new Color(1f, 1f, 1f, 0.95f * alpha), 20f);
            RoundOutline(card, new Color(team.r, team.g, team.b, alpha), 4f, 20f);
            var emblem = Emblem(club);
            if (emblem != null)
            {
                GUI.color = new Color(1f, 1f, 1f, alpha);
                GUI.DrawTexture(new Rect(card.x + 28f, card.y + 12f, 80f, 80f), emblem, ScaleMode.ScaleToFit, true);
            }

            Label(new Rect(card.x + 128f, card.y, minuteWidth + 4f, card.height), minute, _ovScorer, new Color(team.r, team.g, team.b, alpha));
            Label(new Rect(card.x + 148f + minuteWidth, card.y, nameWidth + 4f, card.height), name, _ovScorer, new Color(0.11f, 0.11f, 0.14f, alpha));
            GUI.color = Color.white;
        }

        // =========================================================== 上部の 11 人のカード

        /// <summary>
        /// パーティの招待画面と同じ選手カード（144×160、地 #FFFDF6・線 #E2CFA5・角丸 16・影）。
        /// 上 112 に立ち絵（0.7 倍）、左上にポジションの札、下 48 に名前。
        /// </summary>
        void DrawPlayerCard(int index, LineupSlot slot, Club club)
        {
            EnsureOverlayStyles();
            var card = new Rect(ElevenLeft + CardStep * index, ElevenTop, CardWidth, CardHeight);
            RoundRect(new Rect(card.x, card.y + 3f, card.width, card.height), new Color(0f, 0f, 0f, 0.3f), 16f);
            RoundRect(card, CardCream, 16f);
            var avatarArea = new Rect(card.x, card.y, card.width, CardAvatarHeight);
            GUI.DrawTexture(avatarArea, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f,
                new Color(0.86f, 0.91f, 0.94f, 1f), Vector4.zero, new Vector4(16f, 16f, 0f, 0f));
            RoundOutline(card, CardLine, 2f, 16f);
            if (slot == null || slot.Player == null) return;

            var frames = Frames(slot.Player, slot.Slot, club);
            var walk = frames != null ? frames.WalkSide : null;
            if (walk != null && walk.Length > 0)
            {
                var texture = walk[Mathf.Min(ElevenWalkFrame, walk.Length - 1)];
                if (texture != null)
                {
                    float frame = FrameSize * CardAvatarScale;
                    float shift = (CardWidth - frame) * 0.5f;
                    GUI.BeginGroup(avatarArea);
                    GUI.color = Color.white;
                    GUI.DrawTexture(
                        new Rect(shift + SheetLeft * CardAvatarScale, SheetTop * CardAvatarScale, SheetWidth * CardAvatarScale, SheetHeight * CardAvatarScale),
                        texture,
                        ScaleMode.StretchToFill,
                        true);
                    GUI.EndGroup();
                }
            }

            var badge = new Rect(card.x + 6f, card.y + 6f, 44f, 26f);
            Color group = PositionUtility.GroupColor(PositionUtility.GroupOf(slot.Slot));
            RoundRect(badge, group, 8f);
            Label(badge, slot.Slot.ToString(), _ovBadge, Color.white);

            string name = slot.Player.Name ?? "";
            var nameRect = new Rect(card.x + 8f, card.y + CardAvatarHeight, card.width - 16f, CardHeight - CardAvatarHeight);
            int size = 20;
            _ovCardName.fontSize = size;
            while (size > 12 && _ovCardName.CalcSize(new GUIContent(name)).x > nameRect.width)
                _ovCardName.fontSize = --size;
            Label(nameRect, name, _ovCardName, CardBrown);
            _ovCardName.fontSize = 20;
        }

        // =========================================================== 共通

        static void RoundRect(Rect rect, Color color, float radius)
        {
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, color, 0f, radius);
        }

        static void RoundOutline(Rect rect, Color color, float width, float radius)
        {
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, color, width, radius);
        }

        static void Label(Rect rect, string text, GUIStyle style, Color color)
        {
            var previous = style.normal.textColor;
            style.normal.textColor = color;
            GUI.color = Color.white;
            GUI.Label(rect, text, style);
            style.normal.textColor = previous;
        }

        void EnsureOverlayStyles()
        {
            if (_ovClock != null && _ovFont == _font) return;
            _ovFont = _font;
            var baseStyle = new GUIStyle(GUI.skin.label)
            {
                font = _font,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
                clipping = TextClipping.Overflow,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0)
            };
            _ovClock = new GUIStyle(baseStyle) { fontSize = 34, alignment = TextAnchor.MiddleLeft };
            _ovSmall = new GUIStyle(baseStyle) { fontSize = 20 };
            _ovGoal = new GUIStyle(baseStyle) { fontSize = 22, alignment = TextAnchor.MiddleLeft };
            _ovCardName = new GUIStyle(baseStyle) { fontSize = 20, clipping = TextClipping.Clip };
            _ovBadge = new GUIStyle(baseStyle) { fontSize = 16 };
            _ovBig = new GUIStyle(baseStyle) { fontSize = 220, fontStyle = FontStyle.BoldAndItalic };
            _ovScorer = new GUIStyle(baseStyle) { fontSize = 44, alignment = TextAnchor.MiddleLeft };
        }
    }
}
