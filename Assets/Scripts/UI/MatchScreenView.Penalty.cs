using SoccerClub.Core;
using SoccerClub.Data;
using SoccerClub.Match;
using UnityEngine;

namespace SoccerClub.UI
{
    /// <summary>
    /// PK 戦の演出。試合の 10 回のあとに続けて、1 本ずつキッカーと GK の 1 対 1 を見せる。
    /// ・最初に「PK戦」を出す。
    /// ・1 本ごとに、蹴る側を左にしたピッチで、キッカー（3 の位置）と相手 GK（6 の位置）を向かい合わせる。
    ///   助走のあとシュートし、成功ならゴールへ、失敗なら GK に当たって跳ね返る。そのあと「成功！」「失敗…」を出す。
    /// ・スコアの下に PK の成功数と、1 本ずつの ○（成功）× （失敗）を出す。
    /// ・最後に勝ったクラブを出す。
    /// 長さは回の単位で、MatchScreenView.Duration に足す。
    /// </summary>
    public sealed partial class MatchScreenView
    {
        const float PenaltyIntroUnits = 1f;
        const float PenaltyKickUnits = 1.5f;
        const float PenaltyOutroUnits = 1.5f;

        // 1 本の中の時間割（0〜1）。
        const float KickRunEnd = 0.30f;
        const float KickShotEnd = 0.50f;
        const float KickSettleEnd = 0.65f;
        const float KickBounceEnd = 0.80f;
        const float KickLabelAt = 0.55f;

        // スコアの下の PK の表示。
        static readonly Rect PenaltyTallyRect = new Rect(660f, 430f, 600f, 70f);
        const float PenaltyMarkY = 505f;
        const float PenaltyMarkSize = 40f;
        const float PenaltyMarkGap = 6f;
        static readonly Rect PenaltyBannerRect = new Rect(0f, 520f, 1920f, 160f);

        static readonly Color PenaltyScoredColor = new Color(0.16f, 0.42f, 0.9f, 1f);
        static readonly Color PenaltyMissedColor = new Color(0.86f, 0.2f, 0.2f, 1f);

        GUIStyle _pkTally;
        GUIStyle _pkMark;
        GUIStyle _pkBanner;
        Font _pkFont;

        /// <summary>PK 戦の長さ（回の単位）。PK 戦が無い試合と、1 本ずつの記録が無い古いセーブは 0。</summary>
        static float PenaltyWeight(MatchResult result)
        {
            if (result == null || !result.DecidedByPenalty || result.PenaltyKicks == null || result.PenaltyKicks.Count == 0)
                return 0f;
            return PenaltyIntroUnits + result.PenaltyKicks.Count * PenaltyKickUnits + PenaltyOutroUnits;
        }

        void DrawPenalty(Fixture fixture, MatchResult result, Club viewer, float units)
        {
            EnsurePenaltyStyles();
            var kicks = result.PenaltyKicks;
            float kickUnits = units - PenaltyIntroUnits;
            int index = kickUnits < 0f ? -1 : Mathf.Min(kicks.Count, (int)(kickUnits / PenaltyKickUnits));
            float local = index < 0 || index >= kicks.Count ? 1f : Mathf.Clamp01((kickUnits - index * PenaltyKickUnits) / PenaltyKickUnits);

            // いま蹴っている 1 本（イントロは 1 本目、最後は最後の 1 本の向き）。
            var kick = kicks[Mathf.Clamp(index, 0, kicks.Count - 1)];
            bool attackLeft = kick.Home;
            DrawField(attackLeft);

            if (index >= 0 && index < kicks.Count)
                DrawKick(result, kick, attackLeft, local);

            // 決まった本数（いまの 1 本は、シュートが届いたら数える）。
            int shown = index < 0 ? 0 : index >= kicks.Count ? kicks.Count : index + (local >= KickShotEnd ? 1 : 0);
            DrawHeader(fixture, result.HomeGoals, result.AwayGoals, false, false, 1f);
            DrawPenaltyTally(result, shown);
            DrawEleven(result, viewer, result.Events != null ? result.Events.Count : 0, true);

            if (index < 0)
            {
                DrawBanner("PK戦", Color.black);
            }
            else if (index >= kicks.Count)
            {
                var winner = result.PenaltyWinner;
                if (winner != null)
                    DrawBanner(winner.Name + " の勝ち！", Color.black);
            }
            else if (local >= KickLabelAt)
            {
                DrawBanner(kick.Scored ? "成功！" : "失敗…", kick.Scored ? PenaltyScoredColor : PenaltyMissedColor);
            }
        }

        void DrawKick(MatchResult result, PenaltyKick kick, bool attackLeft, float t)
        {
            var kickerClub = kick.Home ? result.Home : result.Away;
            var keeperClub = kick.Home ? result.Away : result.Home;
            float clock = UnityEngine.Time.time;
            _cast.Clear();
            if (kick.Kicker != null)
                _cast.Add(new Figure { Player = kick.Kicker, Position = kick.Kicker.PreferredPosition, Club = kickerClub, Slot = ShooterSlot, Offense = true, Alpha = 1f });
            if (kick.Keeper != null)
                _cast.Add(new Figure { Player = kick.Keeper, Position = Position.GK, Club = keeperClub, Slot = GoalkeeperSlot, Offense = false, Alpha = 1f });

            for (int i = 0; i < _cast.Count; i++)
                DrawFigure(_cast[i], attackLeft, clock);
            DrawPenaltyBall(kick.Scored, attackLeft, t, clock);
            for (int i = 0; i < _cast.Count; i++)
                DrawName(_cast[i], attackLeft);
        }

        /// <summary>助走のあいだは足元。シュートは GK へ。成功ならゴールの奥で止まり、失敗なら GK に当たって跳ね返って消える。</summary>
        void DrawPenaltyBall(bool scored, bool attackLeft, float t, float clock)
        {
            var texture = _ball != null ? _ball : _ballFallback;
            if (texture == null) return;
            float direction = attackLeft ? 1f : -1f;
            Vector2 feet = FeetBall(ShooterSlot, attackLeft);
            var keeperBody = SlotOrigin(GoalkeeperSlot, attackLeft) + BodyCenter;
            Vector2 target = scored ? GoalTarget(attackLeft) : new Vector2(keeperBody.x - direction * 40f, keeperBody.y);
            Vector2 center;
            float angle;
            float alpha = 1f;

            if (t < KickRunEnd)
            {
                center = feet;
                angle = 0f;
            }
            else if (t < KickShotEnd)
            {
                float flight = Mathf.Clamp01((t - KickRunEnd) / (KickShotEnd - KickRunEnd));
                center = Arc(feet, target, Smooth(flight), scored ? 70f : 50f);
                angle = flight * 900f * direction;
            }
            else if (scored)
            {
                float settle = Mathf.Clamp01((t - KickShotEnd) / (KickSettleEnd - KickShotEnd));
                var rest = new Vector2(target.x + 12f * direction, target.y + 70f);
                center = Vector2.Lerp(target, rest, Smooth(settle));
                center.y -= Mathf.Sin(settle * Mathf.PI) * 20f;
                angle = (900f + settle * 180f) * direction;
            }
            else
            {
                float bounce = Mathf.Clamp01((t - KickShotEnd) / (KickBounceEnd - KickShotEnd));
                center = new Vector2(target.x - 620f * bounce * direction, target.y - 260f * bounce + 420f * bounce * bounce);
                angle = (900f - bounce * 1080f) * direction;
                alpha = 1f - Mathf.Clamp01((bounce - 0.6f) / 0.4f);
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

        /// <summary>スコアの下に「PK 3 - 2」と、ホームは中央の左、アウェイは右に 1 本ずつの ○ ×。</summary>
        void DrawPenaltyTally(MatchResult result, int shown)
        {
            int home = 0;
            int away = 0;
            int homeCount = 0;
            int awayCount = 0;
            var kicks = result.PenaltyKicks;
            for (int i = 0; i < shown && i < kicks.Count; i++)
            {
                if (kicks[i].Home)
                {
                    homeCount++;
                    if (kicks[i].Scored) home++;
                }
                else
                {
                    awayCount++;
                    if (kicks[i].Scored) away++;
                }
            }

            GUI.color = Color.white;
            GUI.Label(PenaltyTallyRect, "PK  " + home + " - " + away, _pkTally);

            float step = PenaltyMarkSize + PenaltyMarkGap;
            // 本数が多いとき（サドンデスが長いとき）は詰める。
            int most = Mathf.Max(5, Mathf.Max(homeCount, awayCount));
            float room = 560f;
            if (most * step > room) step = room / most;

            int h = 0;
            int a = 0;
            for (int i = 0; i < shown && i < kicks.Count; i++)
            {
                var kick = kicks[i];
                float x = kick.Home
                    ? 930f - (h++ + 1) * step
                    : 990f + a++ * step;
                DrawMark(new Rect(x, PenaltyMarkY, PenaltyMarkSize, PenaltyMarkSize), kick.Scored);
            }
        }

        void DrawMark(Rect rect, bool scored)
        {
            var previous = _pkMark.normal.textColor;
            _pkMark.normal.textColor = scored ? PenaltyScoredColor : PenaltyMissedColor;
            GUI.Label(rect, scored ? "○" : "×", _pkMark);
            _pkMark.normal.textColor = previous;
        }

        void DrawBanner(string text, Color color)
        {
            var previous = _pkBanner.normal.textColor;
            _pkBanner.normal.textColor = color;
            GUI.Label(PenaltyBannerRect, text, _pkBanner);
            _pkBanner.normal.textColor = previous;
        }

        void EnsurePenaltyStyles()
        {
            if (_pkTally != null && _pkFont == _font) return;
            _pkFont = _font;
            _pkTally = new GUIStyle(_bigScore) { fontSize = 56 };
            _pkMark = new GUIStyle(_bigScore) { fontSize = 36 };
            _pkBanner = new GUIStyle(_bigScore) { fontSize = 112 };
        }
    }
}
