using SoccerClub.UI;
using UnityEngine;

namespace SoccerClub.Core
{
    /// <summary>
    /// メイン画面などの部品を、ほかの画面（クラブハウス、試合後）と同じクリーム色と茶色にそろえたもの（版 1.65。Figma の main_redesign）。
    /// ・上部の 4 枚：クリームの地、焦げ茶の線 4、角丸 22、上に茶色の見出しの帯（40）。年目と日付だけ幅 240。
    /// ・日送りなどのボタン：黄色の丸い札（線 5）に、左のイラストと「1日進める ▶」「NEXT DAY」。
    /// </summary>
    public sealed partial class GameRunner
    {
        // main_redesign の info_bar。x 336 から 240 / 349 / 349 / 349、間 10、y 28、高さ 104。
        const float CardTop = 28f;
        const float CardHeight = 104f;
        const float CardBand = 40f;
        const float CardRadius = 22f;
        const float CardLeft = 336f;
        const float CardDateWidth = 240f;
        const float CardWidth = 349f;
        const float CardGap = 10f;

        static readonly Color ChromeCream = new Color(1f, 0.992f, 0.965f, 0.96f);
        static readonly Color ChromeBrown = new Color(0.42f, 0.267f, 0.137f, 1f);
        static readonly Color ChromeDark = new Color(0.29f, 0.18f, 0.078f, 1f);
        static readonly Color ChromeYellow = new Color(0.961f, 0.773f, 0.259f, 1f);
        static readonly Color ChromeShadow = new Color(0.18f, 0.11f, 0.04f, 0.35f);
        static readonly Color ChromeLabel = new Color(1f, 0.973f, 0.906f, 1f);

        GUIStyle _cardLabelStyle;
        GUIStyle _cardValueStyle;
        GUIStyle _pillTitleStyle;
        GUIStyle _pillSubStyle;
        GUIStyle _chromeHit;
        Font _chromeFont;
        Texture2D _nextDayIcon;

        static Rect CardRect(int index)
        {
            float x = index == 0 ? CardLeft : CardLeft + CardDateWidth + CardGap + (CardWidth + CardGap) * (index - 1);
            float width = index == 0 ? CardDateWidth : CardWidth;
            return PinTop(new Rect(x, CardTop, width, CardHeight));
        }

        void EnsureChromeStyles()
        {
            if (_nextDayIcon == null) _nextDayIcon = Resources.Load<Texture2D>("MainScreen/icon_nextday");
            if (_cardLabelStyle != null && _chromeFont == _font) return;
            _chromeFont = _font;
            _cardLabelStyle = new GUIStyle(GUI.skin.label)
            {
                font = _font,
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Overflow,
                wordWrap = false,
                padding = new RectOffset(0, 0, 0, 0),
                normal = { textColor = ChromeLabel }
            };
            _cardValueStyle = new GUIStyle(_cardLabelStyle) { fontSize = 30, normal = { textColor = ChromeDark } };
            _pillTitleStyle = new GUIStyle(_cardLabelStyle) { fontSize = 34, normal = { textColor = ChromeDark } };
            _pillSubStyle = new GUIStyle(_cardLabelStyle) { fontSize = 20, normal = { textColor = ChromeBrown } };
            _chromeHit = new GUIStyle(GUIStyle.none);
        }

        /// <summary>上部の情報カード（見出しの帯と値）。</summary>
        void DrawInfoCard(Rect rect, string label, string value)
        {
            EnsureChromeStyles();
            ChromeRound(new Rect(rect.x, rect.y + 5f, rect.width, rect.height), ChromeShadow, CardRadius);
            ChromeRound(rect, ChromeCream, CardRadius);
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, CardBand), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f,
                ChromeBrown, Vector4.zero, new Vector4(CardRadius, CardRadius, 0f, 0f));
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, ChromeDark, 4f, CardRadius);
            FitChromeLabel(new Rect(rect.x + 12f, rect.y + 2f, rect.width - 24f, CardBand - 2f), label, _cardLabelStyle, 22);
            FitChromeLabel(new Rect(rect.x + 12f, rect.y + CardBand, rect.width - 24f, rect.height - CardBand - 6f), value, _cardValueStyle, 30);
        }

        /// <summary>
        /// 黄色の丸い札のボタン（main_redesign の button_nextday_pill）。高さ 112 を基準に、大きさに合わせて文字を縮める。
        /// icon が null なら文字だけ。dim（AUTO 中）は暗くする。
        /// </summary>
        void DrawDayPill(Rect rect, Texture2D icon, float iconAspect, string title, string subtitle, bool dim)
        {
            EnsureChromeStyles();
            float k = rect.height / 112f;
            float radius = rect.height * 0.5f;
            var previous = GUI.color;
            ChromeRound(new Rect(rect.x, rect.y + 5f * k, rect.width, rect.height), new Color(ChromeShadow.r, ChromeShadow.g, ChromeShadow.b, 0.4f), radius);
            var fill = dim ? Color.Lerp(ChromeYellow, new Color(0.55f, 0.55f, 0.55f, 1f), 0.55f) : ChromeYellow;
            ChromeRound(rect, fill, radius);
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, ChromeDark, 5f * k, radius);

            float left = rect.x + 16f * k;
            if (icon != null)
            {
                float h = 84f * k;
                float w = Mathf.Min(h * Mathf.Max(0.1f, iconAspect), 110f * k);
                GUI.color = dim ? new Color(0.75f, 0.75f, 0.75f, 1f) : Color.white;
                GUI.DrawTexture(new Rect(left, rect.center.y - h * 0.5f, w, h), icon, ScaleMode.ScaleToFit, true);
                GUI.color = previous;
                left += w + 4f * k;
            }

            var area = new Rect(left, rect.y, rect.xMax - 18f * k - left, rect.height);
            bool hasSub = !string.IsNullOrEmpty(subtitle);
            float titleH = 46f * k;
            float subH = hasSub ? 26f * k : 0f;
            float top = rect.center.y - (titleH + subH) * 0.5f;
            FitChromeLabel(new Rect(area.x, top, area.width, titleH), title + " ▶", _pillTitleStyle, Mathf.RoundToInt(34f * k));
            if (hasSub)
                FitChromeLabel(new Rect(area.x, top + titleH, area.width, subH), subtitle, _pillSubStyle, Mathf.RoundToInt(20f * k));
            GUI.color = previous;
        }

        static void ChromeRound(Rect rect, Color color, float radius)
        {
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, color, 0f, radius);
        }

        static void FitChromeLabel(Rect rect, string text, GUIStyle style, int size)
        {
            if (style == null) return;
            var content = new GUIContent(text ?? "");
            int previous = style.fontSize;
            style.fontSize = size;
            while (style.fontSize > 10 && style.CalcSize(content).x > rect.width)
                style.fontSize--;
            GUI.Label(rect, content, style);
            style.fontSize = previous;
        }
    }
}
