using UnityEngine;

namespace SoccerClub.UI
{
    /// <summary>メイン画面で、歩いているキャラの頭上に出す吹き出しアイコンの種類。</summary>
    public enum HeadBubbleKind
    {
        /// <summary>施設のレベルアップ（事業部）。</summary>
        Facility,
        /// <summary>フロント人事（GM、いなければ秘書）。</summary>
        Personnel,
        /// <summary>スポンサーの提案（営業部）。</summary>
        Sponsor,
        Friend,
        BestFriend,
        Lover,
        Spouse,
        /// <summary>居残り練習。</summary>
        ExtraPractice
    }

    /// <summary>
    /// 頭上の吹き出しアイコン。丸い吹き出し（縁は種類の色）に記号を 1 つ出し、上下にゆっくり揺らす。
    /// タップされたら Draw が true を返す。座標はメイン画面の chrome 座標。
    /// </summary>
    public sealed class HeadBubbleView
    {
        // 版 1.61 で 72 → 108 に拡大（触りやすくする）。タップの判定はさらに HitPad だけ外へ広げる。
        public const float Size = 108f;
        const float TailHeight = 24f;
        const float TailWidth = 33f;
        const float Lift = 8f;
        const float BobPixels = 6f;
        const float BobSpeed = 3f;
        const float Border = 7f;
        const float HitPad = 24f;

        Texture2D _disc;
        Texture2D _tail;
        GUIStyle _glyph;
        GUIStyle _hit;
        Font _font;

        /// <summary>頭の位置（chrome 座標）から、吹き出しの範囲を決める。index で揺れの位相をずらす。</summary>
        public static Rect BoundsAt(Vector2 head, float time, int index)
        {
            float bob = Mathf.Sin(time * BobSpeed + index * 1.3f) * BobPixels;
            return new Rect(head.x - Size * 0.5f, head.y - Lift - TailHeight - Size + bob, Size, Size + TailHeight);
        }

        public bool Draw(Rect bounds, HeadBubbleKind kind, Font font)
        {
            Ensure(font);
            var previous = GUI.color;
            var edge = EdgeColor(kind);
            var disc = new Rect(bounds.x, bounds.y, Size, Size);
            float cx = disc.center.x;

            GUI.color = new Color(0f, 0f, 0f, 0.25f);
            GUI.DrawTexture(new Rect(disc.x + 3f, disc.y + 6f, Size, Size), _disc);
            GUI.DrawTexture(new Rect(cx - TailWidth * 0.5f + 3f, disc.yMax - 6f, TailWidth, TailHeight + 6f), _tail);

            GUI.color = edge;
            GUI.DrawTexture(disc, _disc);
            GUI.DrawTexture(new Rect(cx - TailWidth * 0.5f, disc.yMax - 12f, TailWidth, TailHeight + 12f), _tail);

            GUI.color = Color.white;
            var inner = new Rect(disc.x + Border, disc.y + Border, Size - Border * 2f, Size - Border * 2f);
            GUI.DrawTexture(inner, _disc);
            GUI.DrawTexture(new Rect(cx - 9f, disc.yMax - Border - 6f, 18f, TailHeight - 3f), _tail);

            _glyph.normal.textColor = edge;
            GUI.Label(inner, Glyph(kind), _glyph);
            GUI.color = previous;
            var hit = new Rect(bounds.x - HitPad, bounds.y - HitPad, bounds.width + HitPad * 2f, bounds.height + HitPad * 2f);
            return GUI.Button(hit, GUIContent.none, _hit);
        }

        static string Glyph(HeadBubbleKind kind)
        {
            // 版 1.62：スタッフやフロントの提案と練習は汎用の「！」、友達と親友は「♪」、恋人と夫婦は「♡」。
            switch (kind)
            {
                case HeadBubbleKind.Friend:
                case HeadBubbleKind.BestFriend:
                    return "♪";
                case HeadBubbleKind.Lover:
                case HeadBubbleKind.Spouse:
                    return "♡";
                default:
                    return "！";
            }
        }

        static Color EdgeColor(HeadBubbleKind kind)
        {
            switch (kind)
            {
                case HeadBubbleKind.Facility: return new Color(0.93f, 0.55f, 0.15f, 1f);
                case HeadBubbleKind.Personnel: return new Color(0.18f, 0.37f, 0.62f, 1f);
                case HeadBubbleKind.Sponsor: return new Color(0.20f, 0.55f, 0.30f, 1f);
                case HeadBubbleKind.Friend: return new Color(0.95f, 0.62f, 0.08f, 1f);
                case HeadBubbleKind.BestFriend: return new Color(0.55f, 0.35f, 0.80f, 1f);
                case HeadBubbleKind.Lover: return new Color(0.93f, 0.35f, 0.55f, 1f);
                case HeadBubbleKind.ExtraPractice: return new Color(0.10f, 0.58f, 0.62f, 1f);
                default: return new Color(0.82f, 0.18f, 0.32f, 1f);
            }
        }

        void Ensure(Font font)
        {
            if (_disc == null) _disc = Disc(128);
            if (_tail == null) _tail = TailDown(32, 32);
            if (_hit == null)
            {
                _hit = new GUIStyle(GUIStyle.none);
            }

            if (_glyph != null && _font == font) return;
            _font = font;
            _glyph = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 60,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Overflow,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0)
            };
        }

        static Texture2D Disc(int size)
        {
            var pixels = new Color32[size * size];
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - r;
                    float dy = y + 0.5f - r;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(r - d) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }

            return Finish(pixels, size, size);
        }

        /// <summary>下向きの三角。SetPixels は下が先頭なので、画面上で頂点が下に来るよう反転する。</summary>
        static Texture2D TailDown(int width, int height)
        {
            var pixels = new Color32[width * height];
            float center = (width - 1) * 0.5f;
            float span = Mathf.Max(1, height - 1);
            for (int y = 0; y < height; y++)
            {
                float along = 1f - y / span;
                float half = (1f - along) * center;
                for (int x = 0; x < width; x++)
                {
                    float over = half + 0.5f - Mathf.Abs(x - center);
                    byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(over) * 255f);
                    pixels[y * width + x] = new Color32(255, 255, 255, a);
                }
            }

            return Finish(pixels, width, height);
        }

        static Texture2D Finish(Color32[] pixels, int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
