using System.Collections.Generic;
using SoccerClub.Core;
using SoccerClub.Data;
using SoccerClub.Match;
using UnityEngine;

namespace SoccerClub.UI
{
    /// <summary>
    /// 試合前のスタメン紹介。Figma の match_preparation。
    /// 背景はロッカールームの絵（MatchBackdrop）。丸の外に体がはみ出さないよう、選手の絵は丸く切り抜いてから描く。
    /// </summary>
    public sealed class MatchPreparationView
    {
        const float CardW = 260f;
        const float Circle = 160f;
        const float CircleX = 50f;
        const float NameY = 144f;
        const float NameH = 32f;
        const float PoseX = 13.333f;
        const float PoseY = 27.51f;
        const float PoseBoxW = 133.333f;
        const float PoseH = 178.942f;

        static readonly Rect EmblemRect = new Rect(0f, 0f, 293f, 220f);
        static readonly Color32 GkColor = new Color32(0xC8, 0x4C, 0x4A, 255);
        static readonly Color32 DfColor = new Color32(0x5D, 0x8D, 0xB1, 255);
        static readonly Color32 MfColor = new Color32(0xCA, 0xC6, 0x5A, 255);
        static readonly Color32 FwColor = new Color32(0x75, 0xB0, 0x5E, 255);
        // 背景の絵の上に重ねる色。カードが絵に埋もれないよう少し暗くする。
        static readonly Color BackdropOverlay = new Color(0f, 0f, 0f, 0.3f);
        const int PortraitSize = 160;

        Texture2D _circle;
        Texture2D _capsule;
        // 選手 id → 丸く切り抜いた絵。元の絵（横歩きのコマ）が変わったら作り直す。
        readonly Dictionary<string, Portrait> _portraits = new Dictionary<string, Portrait>();
        TeamSheet _portraitSheet;

        sealed class Portrait
        {
            public Texture2D Source;
            public Texture2D Texture;
        }

        GUIStyle _name;
        Font _font;

        public void Draw(Club club, TeamSheet sheet, GroundCrowd crowd, Texture2D emblem, Font font)
        {
            Ensure(font);
            GUI.color = Color.white;
            MatchBackdrop.Draw(BackdropOverlay);
            if (emblem != null)
                GUI.DrawTexture(EmblemRect, emblem, ScaleMode.StretchToFill, true);

            if (sheet == null || sheet.Formation == null) return;
            if (sheet != _portraitSheet)
            {
                // 試合ごとに作り直す。前の試合の絵は捨てる。
                _portraitSheet = sheet;
                ClearPortraits();
            }

            var slots = sheet.Formation.Slots;
            int count = sheet.StartingXi != null ? sheet.StartingXi.Length : 0;
            for (int i = 0; i < count && i < slots.Length; i++)
            {
                var lineup = sheet.StartingXi[i];
                var position = lineup != null ? lineup.Slot : slots[i];
                var player = lineup != null ? lineup.Player : null;
                var card = FormationLayout.Card(sheet.Formation.Type, i);
                DrawCard(card, position, player, crowd);
            }

            GUI.color = Color.white;
        }

        void DrawCard(Vector2 card, Position position, Player player, GroundCrowd crowd)
        {
            var plate = Plate(position);
            var circle = new Rect(card.x + CircleX, card.y, Circle, Circle);
            GUI.color = new Color(plate.r / 255f, plate.g / 255f, plate.b / 255f, 0.5f);
            GUI.DrawTexture(circle, _circle, ScaleMode.StretchToFill, true);

            GUI.color = Color.white;
            var texture = player != null && crowd != null ? crowd.WalkSide(player.Id) : null;
            var portrait = texture != null ? PortraitOf(player.Id, texture) : null;
            if (portrait != null) GUI.DrawTexture(circle, portrait, ScaleMode.StretchToFill, true);

            GUI.color = plate;
            GUI.DrawTexture(new Rect(card.x, card.y + NameY, CardW, NameH), _capsule, ScaleMode.StretchToFill, true);
            GUI.color = Color.white;
            GUI.Label(new Rect(card.x + 10f, card.y + NameY, CardW - 20f, NameH), player != null ? player.Name : "", _name);
        }

        /// <summary>
        /// 横歩きのコマを丸（160 角）の中の元の位置に置き、丸の外を透明にした絵。
        /// コマは読み取りできないテクスチャなので、一度 RenderTexture に写して読み戻してから切り抜く。
        /// </summary>
        Texture2D PortraitOf(string id, Texture2D source)
        {
            if (string.IsNullOrEmpty(id) || source == null) return null;
            Portrait cached;
            if (_portraits.TryGetValue(id, out cached) && cached.Source == source && cached.Texture != null)
                return cached.Texture;

            var readable = ReadableCopy(source);
            if (readable == null) return null;

            float poseW = PoseH * (source.width / (float)source.height);
            float poseX = PoseX + (PoseBoxW - poseW) * 0.5f;
            float radius = PortraitSize * 0.5f;
            var pixels = new Color32[PortraitSize * PortraitSize];
            for (int row = 0; row < PortraitSize; row++)
            {
                // row は下から数える（Texture2D の並び）。GUI の y は上から。
                float guiY = PortraitSize - row - 0.5f;
                float v = 1f - (guiY - PoseY) / PoseH;
                for (int x = 0; x < PortraitSize; x++)
                {
                    float guiX = x + 0.5f;
                    float u = (guiX - poseX) / poseW;
                    if (u < 0f || u > 1f || v < 0f || v > 1f) continue;
                    float dx = guiX - radius;
                    float dy = guiY - radius;
                    // 丸の縁は 1px でなめらかに消す。
                    float cover = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                    if (cover <= 0f) continue;
                    var c = readable.GetPixelBilinear(u, v);
                    c.a *= cover;
                    pixels[row * PortraitSize + x] = c;
                }
            }

            UnityEngine.Object.Destroy(readable);
            var texture = Finish(PortraitSize, PortraitSize, pixels);
            if (cached != null && cached.Texture != null) UnityEngine.Object.Destroy(cached.Texture);
            _portraits[id] = new Portrait { Source = source, Texture = texture };
            return texture;
        }

        void ClearPortraits()
        {
            foreach (var entry in _portraits.Values)
                if (entry.Texture != null) UnityEngine.Object.Destroy(entry.Texture);
            _portraits.Clear();
        }

        static Texture2D ReadableCopy(Texture2D source)
        {
            var rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active;
            try
            {
                Graphics.Blit(source, rt);
                RenderTexture.active = rt;
                var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
                copy.hideFlags = HideFlags.HideAndDontSave;
                copy.ReadPixels(new Rect(0f, 0f, source.width, source.height), 0, 0, false);
                copy.Apply(false, false);
                copy.filterMode = FilterMode.Bilinear;
                copy.wrapMode = TextureWrapMode.Clamp;
                return copy;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        static Color Plate(Position position)
        {
            switch (PositionUtility.GroupOf(position))
            {
                case PositionGroup.GK: return GkColor;
                case PositionGroup.DF: return DfColor;
                case PositionGroup.MF: return MfColor;
                default: return FwColor;
            }
        }

        void Ensure(Font font)
        {
            if (_circle == null) _circle = Disc(160, true);
            if (_capsule == null) _capsule = Capsule(260, 32);
            if (_name != null && _font == font) return;
            _font = font;
            _name = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 24,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                wordWrap = false,
                normal = { textColor = Color.white }
            };
        }

        static Texture2D Disc(int size, bool inside)
        {
            var pixels = new Color32[size * size];
            float radius = size * 0.5f;
            float limit = radius * radius;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - radius;
                    float dy = y + 0.5f - radius;
                    bool on = (dx * dx + dy * dy <= limit) == inside;
                    pixels[y * size + x] = on ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
                }
            }

            return Finish(size, size, pixels);
        }

        static Texture2D Capsule(int width, int height)
        {
            var pixels = new Color32[width * height];
            float radius = height * 0.5f;
            float limit = radius * radius;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, radius, width - radius);
                    float dx = x + 0.5f - cx;
                    float dy = y + 0.5f - radius;
                    bool on = dx * dx + dy * dy <= limit;
                    pixels[y * width + x] = on ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
                }
            }

            return Finish(width, height, pixels);
        }

        static Texture2D Finish(int width, int height, Color32[] pixels)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            return texture;
        }
    }
}
