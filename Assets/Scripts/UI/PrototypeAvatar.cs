using SoccerClub.Core;
using UnityEngine;

namespace SoccerClub.UI
{
    /// <summary>
    /// character2 の試作。正面のシャツ色は所属クラブのチームカラー。
    /// 左は side_walk、右はその左右反転。
    /// </summary>
    public sealed class PrototypeAvatar
    {
        const int FrontFrames = 9;
        const int SideFrames = 6;
        const float FrameSeconds = 0.1f;
        const float DisplaySize = 280f;
        const float Speed = 90f;

        static readonly Vector2[] Corners =
        {
            new Vector2(240f, 760f),
            new Vector2(1500f, 760f),
            new Vector2(1500f, 1000f),
            new Vector2(240f, 1000f)
        };

        Texture2D _head;
        Texture2D _shirtLine;
        Texture2D _shirtSkin;
        Texture2D _shirtTint;
        Color32[] _shirtColor;
        int _shirtColorWidth;
        int _shirtColorHeight;
        Color32 _appliedTint;
        bool _hasTint;
        readonly Texture2D[] _pantsFront = new Texture2D[FrontFrames];
        readonly Texture2D[] _shirtSide = new Texture2D[SideFrames];
        readonly Texture2D[] _pantsSide = new Texture2D[SideFrames];
        Vector2 _pos = Corners[0];
        int _leg;
        float _frameTime;
        int _frame;
        bool _loaded;

        public void Load()
        {
            _head = Resources.Load<Texture2D>("Prototype/head_01");
            _shirtLine = Resources.Load<Texture2D>("Prototype/front_line_shirt");
            _shirtSkin = Resources.Load<Texture2D>("Prototype/front_line_skin_color");
            LoadShirtColor();
            for (int i = 0; i < FrontFrames; i++)
                _pantsFront[i] = Resources.Load<Texture2D>("Prototype/pants_front_walk_" + (i + 1).ToString("00"));
            for (int i = 0; i < SideFrames; i++)
            {
                string n = (i + 1).ToString("00");
                _shirtSide[i] = Resources.Load<Texture2D>("Prototype/shirt_side_walk_" + n);
                _pantsSide[i] = Resources.Load<Texture2D>("Prototype/pants_side_walk_" + n);
            }

            _loaded = _head != null && _shirtLine != null && _shirtSkin != null && _shirtColor != null
                && _pantsFront[0] != null && _shirtSide[0] != null && _pantsSide[0] != null;
        }

        public void Tick(float delta)
        {
            if (!_loaded || delta <= 0f) return;
            var target = Corners[(_leg + 1) % Corners.Length];
            var offset = target - _pos;
            float distance = offset.magnitude;
            float step = Speed * delta;
            if (distance <= step)
            {
                _pos = target;
                _leg = (_leg + 1) % Corners.Length;
                _frame = 0;
                _frameTime = 0f;
            }
            else
            {
                _pos += offset / distance * step;
            }

            _frameTime += delta;
            int count = FacingFront ? FrontFrames : SideFrames;
            while (_frameTime >= FrameSeconds)
            {
                _frameTime -= FrameSeconds;
                _frame = (_frame + 1) % count;
            }
        }

        public void Draw(Color32 teamColor)
        {
            if (!_loaded) return;
            var rect = new Rect(_pos.x - DisplaySize * 0.5f, _pos.y - DisplaySize, DisplaySize, DisplaySize);
            bool front = FacingFront;
            bool flip = !front && _leg == 0;
            if (front)
            {
                DrawPart(rect, _pantsFront[_frame % FrontFrames], false);
                DrawPart(rect, _shirtLine, false);
                DrawPart(rect, ShirtTint(teamColor), false);
                DrawPart(rect, _shirtSkin, false);
            }
            else
            {
                int index = _frame % SideFrames;
                DrawPart(rect, _pantsSide[index], flip);
                DrawPart(rect, _shirtSide[index], flip);
            }

            DrawPart(rect, _head, false);
        }

        bool FacingFront => _leg == 1 || _leg == 3;

        void LoadShirtColor()
        {
            var asset = Resources.Load<TextAsset>("Prototype/front_line_shirt_color");
            if (asset == null) return;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(asset.bytes))
            {
                Object.Destroy(tex);
                return;
            }

            _shirtColor = tex.GetPixels32();
            _shirtColorWidth = tex.width;
            _shirtColorHeight = tex.height;
            Object.Destroy(tex);
        }

        Texture2D ShirtTint(Color32 teamColor)
        {
            if (_hasTint && _appliedTint.r == teamColor.r && _appliedTint.g == teamColor.g && _appliedTint.b == teamColor.b)
                return _shirtTint;

            var pixels = new Color32[_shirtColor.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                var src = _shirtColor[i];
                pixels[i] = src.a == 0
                    ? src
                    : new Color32(teamColor.r, teamColor.g, teamColor.b, src.a);
            }

            if (_shirtTint == null)
            {
                _shirtTint = new Texture2D(_shirtColorWidth, _shirtColorHeight, TextureFormat.RGBA32, false);
                _shirtTint.hideFlags = HideFlags.HideAndDontSave;
                _shirtTint.filterMode = FilterMode.Bilinear;
                _shirtTint.wrapMode = TextureWrapMode.Clamp;
            }

            _shirtTint.SetPixels32(pixels);
            _shirtTint.Apply(false, false);
            _appliedTint = teamColor;
            _hasTint = true;
            return _shirtTint;
        }

        static void DrawPart(Rect rect, Texture2D texture, bool flip)
        {
            if (texture == null) return;
            if (flip) rect = new Rect(rect.xMax, rect.y, -rect.width, rect.height);
            GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, true);
        }
    }
}
