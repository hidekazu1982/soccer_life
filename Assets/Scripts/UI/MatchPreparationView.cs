using SoccerClub.Core;
using SoccerClub.Data;
using SoccerClub.Match;
using UnityEngine;

namespace SoccerClub.UI
{
    /// <summary>試合前のスタメン紹介。Figma の match_preparation。</summary>
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

        Texture2D _circle;
        Texture2D _outside;
        Texture2D _capsule;
        GUIStyle _name;
        Font _font;

        public void Draw(Club club, TeamSheet sheet, GroundCrowd crowd, Texture2D emblem, Font font)
        {
            Ensure(font);
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(0f, 0f, GameRunner.ReferenceWidth, GameRunner.ReferenceHeight), Texture2D.whiteTexture);
            if (emblem != null)
                GUI.DrawTexture(EmblemRect, emblem, ScaleMode.StretchToFill, true);

            if (sheet == null || sheet.Formation == null) return;
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
            if (texture != null)
            {
                float poseW = PoseH * (texture.width / (float)texture.height);
                float poseX = PoseX + (PoseBoxW - poseW) * 0.5f;
                GUI.BeginGroup(circle);
                GUI.DrawTexture(new Rect(poseX, PoseY, poseW, PoseH), texture, ScaleMode.StretchToFill, true);
                GUI.EndGroup();
            }

            GUI.DrawTexture(circle, _outside, ScaleMode.StretchToFill, true);
            GUI.color = plate;
            GUI.DrawTexture(new Rect(card.x, card.y + NameY, CardW, NameH), _capsule, ScaleMode.StretchToFill, true);
            GUI.color = Color.white;
            GUI.Label(new Rect(card.x + 10f, card.y + NameY, CardW - 20f, NameH), player != null ? player.Name : "", _name);
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
            if (_outside == null) _outside = Disc(160, false);
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
