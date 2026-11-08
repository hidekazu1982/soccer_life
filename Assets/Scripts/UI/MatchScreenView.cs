using System.Collections.Generic;
using System.Globalization;
using BigInteger = System.Numerics.BigInteger;
using SoccerClub.Competition;
using SoccerClub.Core;
using SoccerClub.Data;
using SoccerClub.Match;
using UnityEngine;

namespace SoccerClub.UI
{
    /// <summary>
    /// 試合日にメイン画面から切り替える、10回抽選の再生画面（Figma game2）。
    /// ホームは左、アウェイは右。攻撃側を左に置いた配置が基準で、アウェイの攻撃では左右を反転する。
    /// 左から 1〜3 が攻撃側（3 がシュートを打つ）、4〜6 が守備側（6 が GK）。
    /// </summary>
    public sealed class MatchScreenView
    {
        // 攻撃側が左のときの 1〜6 の位置（character3 の 244 角の左上）。
        static readonly Vector2[] SlotOrigins =
        {
            new Vector2(273f, 710f),
            new Vector2(436f, 635f),
            new Vector2(605f, 710f),
            new Vector2(937f, 635f),
            new Vector2(1104f, 710f),
            new Vector2(1269f, 635f)
        };
        const int ShooterSlot = 2;
        const int FirstDefenderSlot = 3;
        const int GoalkeeperSlot = 5;
        const float FrameSize = 244f;
        // 244 角（2048 キャンバスの 0.119 倍）の中で、シート 811x999 が占める位置。
        const float SheetLeft = 25.26f;
        const float SheetTop = 5.48f;
        const float SheetWidth = 193.2f;
        const float SheetHeight = 238f;
        // 名前は 24px、244 角の中心の下（中心 x +122、中心 y +254.5）。
        const float NameCenterX = 122f;
        const float NameCenterY = 254.5f;
        // ボール 50 角。3 の選手の足元（3 の左上から +138, +194）。
        const float BallSize = 50f;
        static readonly Vector2 BallAtFeet = new Vector2(138f, 194f);
        // 体の中心（ボールを当てる位置）。244 角の左上から。
        static readonly Vector2 BodyCenter = new Vector2(122f, 125f);
        static readonly Rect HomeEmblem = new Rect(32f, 32f, 293f, 220f);
        static readonly Rect AwayEmblem = new Rect(1597f, 32f, 293f, 220f);
        // score。ホームは右端 930、「-」は中心 960、アウェイは左端 990。縦の中心 141.5、128px。
        const float ScoreCenterY = 141.5f;
        const float ScoreHeight = 155f;

        const float WalkFrameSeconds = 0.1f;
        const float MatchLengthScale = 2f;
        const float GoalRoundWeight = 2f;

        // 1 回の抽選の中の時間割（単位は「回」。ゴールの回は 2 回分の長さがある）。
        const float BuildUpEnd = 0.30f;
        const float FirstLossAt = 0.08f;
        const float LossSeconds = 0.06f;
        const float ShotEnd = 0.50f;
        const float SettleEnd = 0.65f;
        const float BounceEnd = 0.80f;
        const float ScorePopLength = 0.5f;

        GUIStyle _title;
        GUIStyle _score;
        GUIStyle _body;
        GUIStyle _row;
        GUIStyle _scoreRight;
        GUIStyle _name;
        GUIStyle _bigScore;
        GUIStyle _bigScoreLeft;
        GUIStyle _bigScoreRight;
        Font _font;
        Vector2 _statsScroll;
        Texture2D _field;
        Texture2D _ball;
        Texture2D _ballFallback;
        readonly AvatarLibrary _avatars = new AvatarLibrary();
        readonly Dictionary<string, AvatarFrames> _figures = new Dictionary<string, AvatarFrames>();
        string _figureKey;

        public static float Duration(Fixture fixture, float daySeconds)
        {
            var events = fixture != null && fixture.Result != null ? fixture.Result.Events : null;
            if (events == null || events.Count == 0) return daySeconds * MatchLengthScale;
            float weight = 0f;
            for (int i = 0; i < events.Count; i++)
                weight += RoundWeight(events[i]);
            return daySeconds * MatchLengthScale * (weight / Mathf.Max(1, events.Count));
        }

        public void Draw(Fixture fixture, float progress, Club viewer, Font font)
        {
            EnsureStyles(font);
            EnsureArt();
            if (fixture != null) RememberFixture(fixture);
            GUI.color = Color.white;

            var result = fixture != null ? fixture.Result : null;
            var events = result != null ? result.Events : null;
            if (events == null || events.Count == 0)
            {
                DrawField(true);
                DrawHeader(fixture, 0, 0, false, false, 0f);
                return;
            }

            int round;
            float local;
            if (progress <= 0f)
            {
                round = 0;
                local = 0f;
            }
            else
            {
                Locate(events, progress, out round, out local);
            }

            var duel = events[round];
            bool attackLeft = duel.AttackClub == fixture.Home;
            float units = local * RoundWeight(duel);
            DrawField(attackLeft);
            DrawPlay(duel, attackLeft, units);

            int homeGoals = GoalsBefore(result, round);
            int awayGoals = GoalsBefore(result, round, false);
            bool scored = duel.Goal && progress > 0f && units >= ShotEnd;
            if (scored)
            {
                if (attackLeft) homeGoals++;
                else awayGoals++;
            }

            if (progress >= 1f && result.DecidedByPenalty)
            {
                homeGoals = result.HomeGoals;
                awayGoals = result.AwayGoals;
            }

            float pop = scored ? Mathf.Clamp01((units - ShotEnd) / ScorePopLength) : 1f;
            bool popping = scored && pop < 1f;
            DrawHeader(fixture, homeGoals, awayGoals, popping && attackLeft, popping && !attackLeft, pop);
            GUI.color = Color.white;
        }

        public void ResetStatsScroll()
        {
            _statsScroll = Vector2.zero;
        }

        // ---- 背景、エンブレム、スコア ----

        /// <summary>
        /// field は攻撃側が左の絵。攻撃側が右のときは左右を反転する。画面の端まで覆う。
        /// 絵の上半分（空）は透明なので、先に白で塗る（Figma の game2 の地の色）。
        /// </summary>
        void DrawField(bool attackLeft)
        {
            float x = GameRunner.HorizontalEdgeInset;
            float y = GameRunner.VerticalEdgeInset;
            var canvas = new Rect(-x, -y, GameRunner.ReferenceWidth + x * 2f, GameRunner.ReferenceHeight + y * 2f);
            GUI.color = Color.white;
            GUI.DrawTexture(canvas, Texture2D.whiteTexture);
            if (_field == null)
            {
                GUI.color = new Color(0.55f, 0.72f, 0.42f, 1f);
                GUI.DrawTexture(canvas, Texture2D.whiteTexture);
                GUI.color = Color.white;
                return;
            }

            DrawMaybeFlipped(canvas, _field, !attackLeft, ScaleMode.ScaleAndCrop);
        }

        void DrawHeader(Fixture fixture, int home, int away, bool homePop, bool awayPop, float pop)
        {
            if (fixture != null)
            {
                var homeEmblem = Emblem(fixture.Home);
                var awayEmblem = Emblem(fixture.Away);
                if (homeEmblem != null) GUI.DrawTexture(HomeEmblem, homeEmblem, ScaleMode.ScaleToFit, true);
                if (awayEmblem != null) GUI.DrawTexture(AwayEmblem, awayEmblem, ScaleMode.ScaleToFit, true);
            }

            float top = ScoreCenterY - ScoreHeight * 0.5f;
            DrawScoreFigure(new Rect(630f, top, 300f, ScoreHeight), home, homePop, pop, _bigScoreRight);
            GUI.Label(new Rect(930f, top, 60f, ScoreHeight), "-", _bigScore);
            DrawScoreFigure(new Rect(990f, top, 300f, ScoreHeight), away, awayPop, pop, _bigScoreLeft);
        }

        void DrawScoreFigure(Rect rect, int value, bool pop, float t, GUIStyle style)
        {
            string text = value.ToString(CultureInfo.InvariantCulture);
            if (!pop)
            {
                GUI.Label(rect, text, style);
                return;
            }

            float punch = 1.35f - 0.35f * Mathf.SmoothStep(0f, 1f, t);
            var matrix = GUI.matrix;
            var pivot = new Vector2(style.alignment == TextAnchor.MiddleRight ? rect.xMax - 70f : rect.x + 70f, rect.center.y);
            GUIUtility.ScaleAroundPivot(new Vector2(punch, punch), matrix.MultiplyPoint3x4(pivot));
            GUI.Label(rect, text, style);
            GUI.matrix = matrix;
        }

        // ---- 選手とボール ----

        sealed class Figure
        {
            public Player Player;
            public Position Position;
            public Club Club;
            public int Slot;
            public bool Offense;
            public float Alpha;
            public float Sink;
        }

        readonly List<Figure> _cast = new List<Figure>();

        void DrawPlay(DuelEvent duel, bool attackLeft, float units)
        {
            BuildCast(duel, units);

            // 奥の列（y の小さい方）から描く。
            _cast.Sort((a, b) =>
            {
                int byY = SlotOrigins[a.Slot].y.CompareTo(SlotOrigins[b.Slot].y);
                return byY != 0 ? byY : a.Slot.CompareTo(b.Slot);
            });
            float clock = UnityEngine.Time.time;
            for (int i = 0; i < _cast.Count; i++)
                DrawFigure(_cast[i], attackLeft, clock);

            DrawBall(duel, attackLeft, units, clock);

            for (int i = 0; i < _cast.Count; i++)
                DrawName(_cast[i], attackLeft);
        }

        /// <summary>
        /// 攻撃側は Attackers[0] を 3、[1] を 2、[2] を 1 に。守備側は GK を 6、フィールドの守備者を 4、5 に置く。
        /// 抽選に負けた選手も消さずに残す。攻撃側が負けたときは、ボールが次の選手の足元へ移る。
        /// </summary>
        void BuildCast(DuelEvent duel, float units)
        {
            _cast.Clear();
            var attackers = duel.Attackers ?? new Player[0];
            for (int i = 0; i < attackers.Length && i < 3; i++)
            {
                if (attackers[i] == null) continue;
                var position = duel.AttackerSlots != null && i < duel.AttackerSlots.Length ? duel.AttackerSlots[i] : attackers[i].PreferredPosition;
                AddFigure(duel, attackers[i], position, duel.AttackClub, ShooterSlot - i, true, i, units);
            }

            var line = duel.DefenseLine ?? new Player[0];
            int field = 0;
            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] == null) continue;
                bool keeper = i == line.Length - 1 && (duel.Goalkeeper == null || line[i] == duel.Goalkeeper);
                int slot = keeper ? GoalkeeperSlot : FirstDefenderSlot + field;
                if (!keeper)
                {
                    if (field >= 2) continue;
                    field++;
                }

                var position = duel.DefenseSlots != null && i < duel.DefenseSlots.Length ? duel.DefenseSlots[i] : line[i].PreferredPosition;
                AddFigure(duel, line[i], position, duel.DefenseClub, slot, false, i, units);
            }
        }

        void AddFigure(DuelEvent duel, Player player, Position position, Club club, int slot, bool offense, int index, float units)
        {
            // 抽選に負けた選手も消さずに残す（攻撃側も守備側も）。
            _cast.Add(new Figure { Player = player, Position = position, Club = club, Slot = slot, Offense = offense, Alpha = 1f, Sink = 0f });
        }

        void DrawFigure(Figure figure, bool attackLeft, float clock)
        {
            var frames = Frames(figure.Player, figure.Position, figure.Club);
            if (frames == null || frames.WalkSide == null || frames.WalkSide.Length == 0) return;
            int frame = (int)((clock + figure.Slot * 0.17f) / WalkFrameSeconds);
            var texture = frames.WalkSide[((frame % frames.WalkSide.Length) + frames.WalkSide.Length) % frames.WalkSide.Length];
            if (texture == null) return;

            var origin = SlotOrigin(figure.Slot, attackLeft);
            var rect = new Rect(origin.x + SheetLeft, origin.y + SheetTop + figure.Sink, SheetWidth, SheetHeight);
            // 横歩きの原画は左向き。攻撃側は守備側へ、守備側は攻撃側へ向ける。
            bool faceRight = figure.Offense == attackLeft;
            GUI.color = new Color(1f, 1f, 1f, figure.Alpha);
            DrawMaybeFlipped(rect, texture, faceRight, ScaleMode.StretchToFill);
            GUI.color = Color.white;
        }

        void DrawName(Figure figure, bool attackLeft)
        {
            if (figure.Player == null) return;
            var origin = SlotOrigin(figure.Slot, attackLeft);
            var rect = new Rect(origin.x + NameCenterX - 150f, origin.y + NameCenterY - 18f + figure.Sink, 300f, 36f);
            GUI.color = new Color(1f, 1f, 1f, figure.Alpha);
            GUI.Label(rect, figure.Player.Name ?? "", _name);
            GUI.color = Color.white;
        }

        /// <summary>
        /// ボールは持っている選手の足元で転がり、最後の 1 手でシュートする。
        /// ゴールなら GK のボール 1 個分先へ飛んで止まる。止められたら、止めた選手に当たって逆へ飛んで消える。
        /// </summary>
        void DrawBall(DuelEvent duel, bool attackLeft, float units, float clock)
        {
            var texture = _ball != null ? _ball : _ballFallback;
            if (texture == null) return;
            int steps = StepCount(duel);
            int attackerCount = duel.Attackers != null ? Mathf.Min(3, duel.Attackers.Length) : 0;
            if (attackerCount == 0) return;

            // 最後の 1 手より前の、攻撃側の脱落でボールが次の選手へ移る。
            int holder = 0;
            int previous = 0;
            float handOff = 1f;
            for (int s = 0; s < steps - 1; s++)
            {
                if (!duel.StepAttackerLost[s]) continue;
                float at = LossTime(s, steps);
                if (units < at) break;
                previous = holder;
                holder = Mathf.Min(holder + 1, attackerCount - 1);
                handOff = Mathf.Clamp01((units - at) / LossSeconds);
            }

            Vector2 feet = Vector2.Lerp(FeetBall(ShooterSlot - previous, attackLeft), FeetBall(ShooterSlot - holder, attackLeft), Smooth(handOff));
            float direction = attackLeft ? 1f : -1f;
            Vector2 center;
            float angle;
            float alpha = 1f;

            if (units < BuildUpEnd)
            {
                center = feet;
                angle = clock * 240f * direction;
            }
            else
            {
                Vector2 target = duel.Goal ? GoalTarget(attackLeft) : BlockTarget(duel, attackLeft);
                float flight = Mathf.Clamp01((units - BuildUpEnd) / (ShotEnd - BuildUpEnd));
                float spin = (clock * 240f + flight * 900f) * direction;
                if (units < ShotEnd)
                {
                    center = Arc(feet, target, Smooth(flight), duel.Goal ? 90f : 60f);
                    angle = spin;
                }
                else if (duel.Goal)
                {
                    // ネットに当たって少し落ちて止まる。
                    float settle = Mathf.Clamp01((units - ShotEnd) / (SettleEnd - ShotEnd));
                    var rest = new Vector2(target.x + 12f * direction, target.y + 70f);
                    center = Vector2.Lerp(target, rest, Smooth(settle));
                    center.y -= Mathf.Sin(settle * Mathf.PI) * 20f;
                    angle = (900f + settle * 180f) * direction;
                }
                else
                {
                    // 当たって逆方向へ、放物線で飛んで消える。
                    float bounce = Mathf.Clamp01((units - ShotEnd) / (BounceEnd - ShotEnd));
                    center = new Vector2(
                        target.x - 620f * bounce * direction,
                        target.y - 260f * bounce + 420f * bounce * bounce);
                    angle = (900f - bounce * 1080f) * direction;
                    alpha = 1f - Mathf.Clamp01((bounce - 0.6f) / 0.4f);
                }
            }

            if (alpha <= 0.01f) return;
            var rect = new Rect(center.x - BallSize * 0.5f, center.y - BallSize * 0.5f, BallSize, BallSize);
            var matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, matrix.MultiplyPoint3x4(rect.center));
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, true);
            GUI.color = Color.white;
            GUI.matrix = matrix;
        }

        /// <summary>ゴールの到達点。GK の体の中心から、ボール 1 個分ほど奥（ゴール側）。</summary>
        static Vector2 GoalTarget(bool attackLeft)
        {
            var keeper = SlotOrigin(GoalkeeperSlot, attackLeft) + BodyCenter;
            float side = attackLeft ? 1f : -1f;
            return new Vector2(keeper.x + side * (70f + BallSize), keeper.y + 10f);
        }

        /// <summary>止めた選手（最後まで残った守備側）の体。</summary>
        Vector2 BlockTarget(DuelEvent duel, bool attackLeft)
        {
            int defenderLosses = 0;
            int steps = StepCount(duel);
            for (int s = 0; s < steps; s++)
                if (!duel.StepAttackerLost[s]) defenderLosses++;
            int slot = GoalkeeperSlot;
            for (int i = 0; i < _cast.Count; i++)
            {
                var figure = _cast[i];
                if (figure.Offense) continue;
                int index = DefenseIndex(duel, figure.Player);
                if (index == defenderLosses)
                {
                    slot = figure.Slot;
                    break;
                }
            }

            var body = SlotOrigin(slot, attackLeft) + BodyCenter;
            float side = attackLeft ? -1f : 1f;
            return new Vector2(body.x + side * 40f, body.y);
        }

        static int DefenseIndex(DuelEvent duel, Player player)
        {
            if (duel.DefenseLine == null) return -1;
            for (int i = 0; i < duel.DefenseLine.Length; i++)
                if (duel.DefenseLine[i] == player) return i;
            return -1;
        }

        static Vector2 FeetBall(int slot, bool attackLeft)
        {
            slot = Mathf.Clamp(slot, 0, SlotOrigins.Length - 1);
            var origin = SlotOrigins[slot];
            float x = origin.x + BallAtFeet.x + BallSize * 0.5f;
            if (!attackLeft) x = GameRunner.ReferenceWidth - x;
            return new Vector2(x, origin.y + BallAtFeet.y + BallSize * 0.5f);
        }

        /// <summary>攻撃側が右のときは、左右を反転した位置。</summary>
        static Vector2 SlotOrigin(int slot, bool attackLeft)
        {
            var origin = SlotOrigins[Mathf.Clamp(slot, 0, SlotOrigins.Length - 1)];
            if (attackLeft) return origin;
            return new Vector2(GameRunner.ReferenceWidth - origin.x - FrameSize, origin.y);
        }

        /// <summary>最後の 1 手より前の脱落 step の時刻。積み上げの時間の中に等間隔で置く。</summary>
        static float LossTime(int step, int steps)
        {
            int earlier = Mathf.Max(1, steps - 1);
            float span = BuildUpEnd - FirstLossAt - LossSeconds;
            return FirstLossAt + span * step / earlier;
        }

        static Vector2 Arc(Vector2 from, Vector2 to, float t, float height)
        {
            var point = Vector2.Lerp(from, to, t);
            point.y -= Mathf.Sin(t * Mathf.PI) * height;
            return point;
        }

        static float Smooth(float t)
        {
            return Mathf.SmoothStep(0f, 1f, t);
        }

        static void DrawMaybeFlipped(Rect rect, Texture texture, bool flip, ScaleMode mode)
        {
            if (!flip)
            {
                GUI.DrawTexture(rect, texture, mode, true);
                return;
            }

            var matrix = GUI.matrix;
            GUI.matrix = matrix * Matrix4x4.TRS(new Vector3(rect.xMax, 0f, 0f), Quaternion.identity, new Vector3(-1f, 1f, 1f));
            GUI.DrawTexture(new Rect(0f, rect.y, rect.width, rect.height), texture, mode, true);
            GUI.matrix = matrix;
        }

        // ---- 抽選の時間割 ----

        static int GoalsBefore(MatchResult result, int round, bool home = true)
        {
            int goals = 0;
            for (int i = 0; i < round && i < result.Events.Count; i++)
            {
                var duel = result.Events[i];
                if (duel == null || !duel.Goal) continue;
                bool attackHome = duel.AttackClub == result.Home;
                if (attackHome == home) goals++;
            }

            return goals;
        }

        static float RoundWeight(DuelEvent duel)
        {
            return duel != null && duel.Goal ? GoalRoundWeight : 1f;
        }

        static void Locate(List<DuelEvent> events, float progress, out int round, out float local)
        {
            float weightSum = 0f;
            for (int i = 0; i < events.Count; i++)
                weightSum += RoundWeight(events[i]);
            float cursor = Mathf.Clamp01(progress) * weightSum;
            round = events.Count - 1;
            local = 1f;
            for (int i = 0; i < events.Count; i++)
            {
                float weight = RoundWeight(events[i]);
                if (cursor < weight || i == events.Count - 1)
                {
                    round = i;
                    local = weight <= 0f ? 1f : Mathf.Clamp01(cursor / weight);
                    if (progress >= 1f)
                    {
                        round = events.Count - 1;
                        local = 1f;
                    }

                    return;
                }

                cursor -= weight;
            }
        }

        static int StepCount(DuelEvent duel)
        {
            if (duel == null || duel.StepIndex == null || duel.StepAttackerLost == null) return 0;
            return Mathf.Min(duel.StepIndex.Length, duel.StepAttackerLost.Length);
        }

        static int LostStep(DuelEvent duel, bool attacker, int index)
        {
            int count = StepCount(duel);
            for (int i = 0; i < count; i++)
            {
                if (duel.StepAttackerLost[i] == attacker && duel.StepIndex[i] == index) return i;
            }

            return -1;
        }

        // ---- 素材 ----

        void RememberFixture(Fixture fixture)
        {
            string key = fixture.Id ?? "";
            if (_figureKey == key) return;
            foreach (var pair in _figures)
                DestroyFrames(pair.Value);
            _figures.Clear();
            _figureKey = key;
        }

        AvatarFrames Frames(Player player, Position position, Club club)
        {
            if (player == null || club == null) return null;
            if (!_avatars.Ready) _avatars.Load();
            if (!_avatars.Ready) return null;
            var group = PositionUtility.GroupOf(position);
            string key = player.Id + ":" + club.EmblemIndex + ":" + (int)group;
            AvatarFrames cached;
            if (_figures.TryGetValue(key, out cached) && cached != null) return cached;
            var frames = _avatars.Create(
                player.Id,
                AvatarLibrary.Kit(club.EmblemIndex),
                PositionUtility.GroupColor(group),
                Mathf.RoundToInt(SheetHeight));
            if (frames != null) _figures[key] = frames;
            return frames;
        }

        static void DestroyFrames(AvatarFrames frames)
        {
            if (frames == null) return;
            if (frames.Stand != null) UnityEngine.Object.Destroy(frames.Stand);
            DestroyAll(frames.WalkFront);
            DestroyAll(frames.WalkSide);
        }

        static void DestroyAll(Texture2D[] textures)
        {
            if (textures == null) return;
            for (int i = 0; i < textures.Length; i++)
                if (textures[i] != null) UnityEngine.Object.Destroy(textures[i]);
        }

        Texture2D Emblem(Club club)
        {
            if (club == null || club.EmblemIndex <= 0) return null;
            return Resources.Load<Texture2D>("emblem/emblem_" + club.EmblemIndex.ToString("00"));
        }

        void EnsureArt()
        {
            if (_field == null) _field = Resources.Load<Texture2D>("MainScreen/field");
            if (_ball == null) _ball = Resources.Load<Texture2D>("MainScreen/ball");
            if (_ball != null || _ballFallback != null) return;

            // ball.png が無いときの仮のボール。白い円に黒い縁。
            const int size = 64;
            _ballFallback = new Texture2D(size, size, TextureFormat.RGBA32, false);
            _ballFallback.hideFlags = HideFlags.HideAndDontSave;
            var pixels = new Color32[size * size];
            float radius = size * 0.5f - 1f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - size * 0.5f;
                    float dy = y + 0.5f - size * 0.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d > radius) continue;
                    bool rim = d > radius - 4f;
                    bool patch = Mathf.Abs(dx) < 9f && Mathf.Abs(dy) < 9f;
                    pixels[y * size + x] = rim || patch ? new Color32(30, 30, 30, 255) : new Color32(255, 255, 255, 255);
                }
            }

            _ballFallback.SetPixels32(pixels);
            _ballFallback.Apply(false, true);
        }

        public void DrawStats(Fixture fixture, Club player, Font font)
        {
            EnsureStyles(font);
            DrawBoard(new Color(0.08f, 0.24f, 0.16f, 1f));
            DrawHeading(fixture, player, "試合スタッツ");
            var ratings = MatchRatings.ForClub(fixture, player);
            var list = new Rect(120f, 250f, 1680f, 620f);
            if (ratings.Count == 0)
            {
                GUI.Label(list, "出場した選手はいません。", _body);
                return;
            }

            const float rowH = 36f;
            float width = list.width - 24f;
            _statsScroll = GUI.BeginScrollView(list, _statsScroll, new Rect(0f, 0f, width, (ratings.Count + 1) * rowH));
            DrawRatingRow(0f, width, rowH, "ポジション", "選手", "得点", "評価", _title);
            for (int i = 0; i < ratings.Count; i++)
            {
                var row = ratings[i];
                var person = row.Player;
                DrawRatingRow(
                    (i + 1) * rowH,
                    width,
                    rowH,
                    person != null ? person.PreferredPosition.ToString() : "",
                    person != null ? person.Name : "",
                    row.Goals.ToString(CultureInfo.InvariantCulture),
                    row.Score.ToString("0.0", CultureInfo.InvariantCulture),
                    _row);
            }

            GUI.EndScrollView();
        }

        public void DrawSettlement(Fixture fixture, Club player, int coreDelta, int lightDelta, Font font)
        {
            EnsureStyles(font);
            DrawBoard(new Color(0.10f, 0.14f, 0.22f, 1f));
            DrawHeading(fixture, player, "試合リザルト");
            float y = 270f;
            y = AttendanceLine(fixture, y);
            bool home = player != null && fixture.Home == player;
            var appearance = home ? fixture.HomeAppearancePaid : fixture.AwayAppearancePaid;
            var win = home ? fixture.HomeWinPaid : fixture.AwayWinPaid;
            if (!home)
            {
                GUI.Label(new Rect(160f, y, 1400f, 40f), "アウェイのため、入場料・広告費・運営費はありません。", _body);
                y += 56f;
                y = MoneyLine(y, "出場給", appearance, false);
                y = MoneyLine(y, "勝利給", win, false);
            }
            else
            {
                y = MoneyLine(y, "入場料", fixture.GateIncome, true);
                y = MoneyLine(y, "広告費", fixture.AdvertisingSpent, false);
                float operatingY = y;
                y = MoneyLine(y, "運営費", fixture.OperatingPaid, false);
                GUI.Label(new Rect(980f, operatingY, 520f, 40f), "必要 " + Money(fixture.OperatingRequired), _body);
                y = MoneyLine(y, "出場給", appearance, false);
                y = MoneyLine(y, "勝利給", win, false);
                var net = fixture.GateIncome - fixture.AdvertisingSpent - fixture.OperatingPaid - appearance - win;
                y = MoneyLine(y, "収支", net, net >= 0);
                if (fixture.OperatingPaid < fixture.OperatingRequired)
                {
                    GUI.Label(new Rect(160f, y, 1400f, 36f), "運営費が不足しているため、サポーターがさらに減ります。", _body);
                    y += 48f;
                }
            }

            int core = player != null ? player.CoreSupporters : 0;
            int light = player != null ? player.LightSupporters : 0;
            GUI.Label(new Rect(160f, y, 1400f, 40f), SupporterLine("コア", core, coreDelta), _body);
            y += 48f;
            GUI.Label(new Rect(160f, y, 1400f, 40f), SupporterLine("ライト", light, lightDelta), _body);
        }

        void DrawBoard(Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(new Rect(0f, 0f, GameRunner.ReferenceWidth, GameRunner.ReferenceHeight), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 1f, 1f, 0.06f);
            GUI.DrawTexture(new Rect(80f, 150f, 1760f, 740f), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        void DrawHeading(Fixture fixture, Club player, string title)
        {
            var result = fixture.Result;
            string kind = fixture.IsLeague ? "リーグ" : "カップ";
            GUI.Label(new Rect(120f, 28f, 800f, 48f), title, _title);
            GUI.Label(new Rect(120f, 78f, 800f, 36f), kind + "  " + fixture.Date.ToString("M月d日"), _body);
            int homeGoals = result != null ? result.HomeGoals : 0;
            int awayGoals = result != null ? result.AwayGoals : 0;
            string pk = result != null && result.DecidedByPenalty ? "   PK" : "";
            GUI.Label(
                new Rect(120f, 150f, 1680f, 64f),
                fixture.Home.Name + "   " + homeGoals + "  -  " + awayGoals + "   " + fixture.Away.Name + pk,
                _score);
            if (player == null || result == null) return;
            bool home = fixture.Home == player;
            int mine = home ? homeGoals : awayGoals;
            int theirs = home ? awayGoals : homeGoals;
            string outcome = mine > theirs ? "勝ち" : mine < theirs ? "負け" : "引き分け";
            GUI.Label(new Rect(120f, 214f, 400f, 36f), outcome, _title);
        }

        static void DrawRatingRow(float y, float width, float rowH, string position, string name, string goals, string rating, GUIStyle style)
        {
            GUI.Label(new Rect(0f, y, 160f, rowH), position, style);
            GUI.Label(new Rect(170f, y, width - 420f, rowH), name, style);
            GUI.Label(new Rect(width - 240f, y, 100f, rowH), goals, style);
            GUI.Label(new Rect(width - 120f, y, 120f, rowH), rating, style);
        }

        void DrawOpponentRating(Fixture fixture, Club viewer)
        {
            if (fixture == null || viewer == null) return;
            var opponent = fixture.Home == viewer ? fixture.Away : fixture.Away == viewer ? fixture.Home : null;
            if (opponent == null) return;
            GUI.Label(new Rect(1180f, 78f, 680f, 40f), "相手総合力", _title);
            GUI.Label(new Rect(1180f, 122f, 680f, 56f), ClubDialogView.FormatRating(opponent), _score);
            GUI.Label(new Rect(1180f, 182f, 680f, 36f), opponent.Name, _body);
        }

        float AttendanceLine(Fixture fixture, float y)
        {
            if (fixture == null || fixture.StadiumCapacity <= 0) return y;
            double rate = 100d * fixture.Attendance / fixture.StadiumCapacity;
            GUI.Label(
                new Rect(160f, y, 1500f, 40f),
                "入場者  " + fixture.Attendance.ToString("N0", CultureInfo.InvariantCulture)
                + " / " + fixture.StadiumCapacity.ToString("N0", CultureInfo.InvariantCulture)
                + "    収容率 " + rate.ToString("0.0", CultureInfo.InvariantCulture) + "%",
                _body);
            return y + 48f;
        }

        float MoneyLine(float y, string label, BigInteger amount, bool income)
        {
            string text = "0";
            if (amount != 0)
            {
                string digits = Money(BigInteger.Abs(amount));
                text = income && amount > 0 ? "+" + digits : "-" + digits;
            }

            GUI.Label(new Rect(160f, y, 340f, 40f), label, _body);
            GUI.Label(new Rect(520f, y, 400f, 40f), text, _scoreRight);
            return y + 48f;
        }

        static string SupporterLine(string label, int before, int delta)
        {
            return label + "  " + before.ToString("N0", CultureInfo.InvariantCulture)
                + " → " + (before + delta).ToString("N0", CultureInfo.InvariantCulture)
                + "  （" + Signed(delta) + "）";
        }

        static string Signed(int value)
        {
            if (value > 0) return "+" + value.ToString(CultureInfo.InvariantCulture);
            return value.ToString(CultureInfo.InvariantCulture);
        }

        static string Money(BigInteger amount)
        {
            if (amount < 0) amount = -amount;
            return MoneyText.Format(amount);
        }

        void EnsureStyles(Font font)
        {
            if (_title != null && _font == font) return;
            _font = font;
            _title = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 28,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            _score = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 36,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                wordWrap = false,
                normal = { textColor = Color.white }
            };
            _body = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 24,
                wordWrap = true,
                normal = { textColor = Color.white }
            };
            _row = new GUIStyle(_body)
            {
                fontSize = 22,
                wordWrap = false,
                clipping = TextClipping.Clip
            };
            _scoreRight = new GUIStyle(_body)
            {
                fontSize = 28,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            _name = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 24,
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Overflow,
                wordWrap = false,
                padding = new RectOffset(0, 0, 0, 0),
                normal = { textColor = Color.black }
            };
            _bigScore = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 128,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Overflow,
                wordWrap = false,
                padding = new RectOffset(0, 0, 0, 0),
                normal = { textColor = Color.black }
            };
            _bigScoreRight = new GUIStyle(_bigScore) { alignment = TextAnchor.MiddleRight };
            _bigScoreLeft = new GUIStyle(_bigScore) { alignment = TextAnchor.MiddleLeft };
        }
    }
}
