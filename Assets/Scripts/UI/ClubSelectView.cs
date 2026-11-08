using SoccerClub.Core;
using SoccerClub.World;
using UnityEngine;

namespace SoccerClub.UI
{
    /// <summary>
    /// 初回開始時に 20 クラブから 1 つ選ぶ。配置は Figma の clubselect。
    /// 画像は Resources/emblem。決定ボタンは仮で button_nextday を使う。
    /// </summary>
    public sealed class ClubSelectView
    {
        const int Columns = 5;
        const float CellWidth = 213f;
        const float CellHeight = 160f;
        const float GridX = 428f;
        const float GridY = 111f;

        static readonly Rect NamePlateRect = new Rect(610f, 783f, 700f, 40f);
        static readonly Rect NameTextRect = new Rect(640f, 784f, 640f, 39f);
        static readonly Rect ConfirmRect = new Rect(795f, 839f, 331f, 130f);
        static readonly Color NamePlateColor = new Color32(0xCA, 0xC6, 0x5A, 255);

        readonly Texture2D[] _emblems = new Texture2D[WorldFactory.LeagueSize];
        Texture2D _confirm;
        Texture2D _namePlate;
        int _selected = -1;
        GUIStyle _name;
        GUIStyle _hit;
        Font _font;

        public void Load()
        {
            _confirm = Resources.Load<Texture2D>("MainScreen/button_nextday");
            for (int i = 0; i < _emblems.Length; i++)
                _emblems[i] = Resources.Load<Texture2D>("emblem/emblem_" + (i + 1).ToString("00"));
        }

        public Texture2D EmblemTexture(int clubIndex)
        {
            if (clubIndex < 0 || clubIndex >= _emblems.Length) return null;
            return _emblems[clubIndex];
        }

        /// <summary>開始が確定したフレームだけ true。clubIndex は 0〜19。</summary>
        public bool Draw(Font font, out int clubIndex)
        {
            EnsureStyles(font);
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(0f, 0f, GameRunner.ReferenceWidth, GameRunner.ReferenceHeight), Texture2D.whiteTexture);

            var mouse = GameRunner.MouseInReference();
            bool start = DrawEmblems(mouse);
            DrawNamePlate();
            if (DrawConfirm(mouse))
                start = true;

            GUI.color = Color.white;
            clubIndex = _selected;
            return start && _selected >= 0;
        }

        bool DrawEmblems(Vector2 mouse)
        {
            bool start = false;
            for (int i = 0; i < WorldFactory.LeagueSize; i++)
            {
                int col = i % Columns;
                int row = i / Columns;
                var cell = new Rect(GridX + col * CellWidth, GridY + row * CellHeight, CellWidth, CellHeight);
                bool hover = cell.Contains(mouse);
                GUI.color = hover ? new Color(0.94f, 0.94f, 0.94f, 1f) : Color.white;
                if (_emblems[i] != null)
                    GUI.DrawTexture(cell, _emblems[i], ScaleMode.StretchToFill, true);

                if (GUI.Button(cell, GUIContent.none, _hit))
                {
                    _selected = i;
                    if (Event.current != null && Event.current.clickCount >= 2)
                        start = true;
                }
            }

            GUI.color = Color.white;
            return start;
        }

        void DrawNamePlate()
        {
            bool selected = _selected >= 0 && _selected < WorldFactory.Clubs.Length;
            int emblemIndex = selected ? _selected + 1 : 0;
            GUI.color = selected ? AvatarLibrary.UniformColor(emblemIndex) : NamePlateColor;
            GUI.DrawTexture(NamePlateRect, _namePlate, ScaleMode.StretchToFill, true);
            GUI.color = Color.white;
            if (!selected) return;
            _name.normal.textColor = AvatarLibrary.NumberColor(emblemIndex);
            GUI.Label(NameTextRect, WorldFactory.ClubNames[_selected], _name);
        }

        bool DrawConfirm(Vector2 mouse)
        {
            bool ready = _selected >= 0 && _selected < WorldFactory.ClubNames.Length;
            bool over = ready && ConfirmRect.Contains(mouse);
            GUI.color = !ready
                ? new Color(1f, 1f, 1f, 0.45f)
                : (over ? new Color(1f, 0.97f, 0.86f, 1f) : Color.white);
            if (_confirm != null)
                GUI.DrawTexture(ConfirmRect, _confirm, ScaleMode.StretchToFill, true);
            GUI.color = Color.white;
            return ready && GUI.Button(ConfirmRect, GUIContent.none, _hit);
        }

        void EnsureStyles(Font font)
        {
            if (_namePlate == null)
                _namePlate = Capsule(700, 40);
            if (_name != null && _font == font) return;
            _font = font;
            _name = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 32,
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Clip,
                wordWrap = false,
                normal = { textColor = Color.black }
            };
            var empty = new RectOffset(0, 0, 0, 0);
            _hit = new GUIStyle
            {
                margin = empty,
                padding = empty,
                border = empty
            };
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
