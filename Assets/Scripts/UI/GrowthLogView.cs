using System.Collections.Generic;
using UnityEngine;

namespace SoccerClub.UI
{
    /// <summary>練習で増減した能力の種類。</summary>
    public enum GrowthStat
    {
        Offense,
        Defense,
        Stamina
    }

    /// <summary>
    /// メイン画面左のログ（Figma main の log）。選手の能力が増減したときに
    /// 「アイコン 値 選手名」を 1 行で出す。最大 8 行。あふれたら上へスクロールし、
    /// 古い行は上で消え、新しい行は下に入る。一番上に着いた行は 1 秒で消える。
    /// </summary>
    public sealed class GrowthLogView
    {
        public const int VisibleRows = 8;

        // log 176:65。31,252 から 47 ずつ。アイコン 40x39（icon_*2）、値は 71 で 32px、名前は 180 で 24px。
        const float RowPitch = 47f;
        const float IconWidth = 40f;
        const float IconHeight = 39f;
        const float ValueX = 40f;
        const float NameX = 149f;
        const float ScrollSeconds = 0.25f;
        const float FadeInSeconds = 0.2f;
        // 一番上に着いてからこの秒数で消す。
        const float TopSeconds = 1f;

        static readonly Color PlusColor = new Color(0f, 0f, 1f, 1f);
        static readonly Color MinusColor = new Color(1f, 0x38 / 255f, 0x3C / 255f, 1f);

        sealed class Entry
        {
            public string Name;
            public GrowthStat Stat;
            public int Delta;
            public float Age;
        }

        // _rows の先頭 _leaving 行は、上へ流れて消えていく途中の行。
        readonly List<Entry> _rows = new List<Entry>();
        int _leaving;
        float _scroll;
        float _topAge;
        Texture2D _iconOffense;
        Texture2D _iconDefense;
        Texture2D _iconStamina;
        GUIStyle _value;
        GUIStyle _name;
        Font _font;

        int Visible => _rows.Count - _leaving;

        public void Clear()
        {
            _rows.Clear();
            _leaving = 0;
            _scroll = 0f;
            _topAge = 0f;
        }

        public void Add(string playerName, GrowthStat stat, int delta)
        {
            if (delta == 0) return;
            _rows.Add(new Entry { Name = playerName ?? "", Stat = stat, Delta = delta });
            if (Visible > VisibleRows) PopTop();
        }

        /// <summary>一番上の行を上へ流して消す。残りの行は 1 行ぶん上がる。</summary>
        void PopTop()
        {
            if (Visible <= 0) return;
            _leaving++;
            _scroll += RowPitch;
            _topAge = 0f;
        }

        /// <summary>スクロールを進め、一番上に 1 秒いた行を消し、流れ切った行を捨てる。</summary>
        public void Tick(float deltaTime)
        {
            for (int i = 0; i < _rows.Count; i++)
                _rows[i].Age += deltaTime;
            if (_scroll > 0f)
            {
                // 一度に何行も消えたときは、たまった分を同じ時間で流す。
                float speed = Mathf.Max(RowPitch, _scroll) / ScrollSeconds;
                _scroll = Mathf.Max(0f, _scroll - speed * deltaTime);
            }
            else if (Visible > 0)
            {
                // 一番上に着いてから数える。スクロール中は止める。
                _topAge += deltaTime;
                if (_topAge >= TopSeconds) PopTop();
            }

            while (_leaving > 0 && RowY(0, 0f) <= -RowPitch)
            {
                _rows.RemoveAt(0);
                _leaving--;
            }
        }

        /// <summary>origin からの行の高さ位置。</summary>
        float RowY(int index, float originY)
        {
            return originY + (index - _leaving) * RowPitch + _scroll;
        }

        /// <summary>origin は log の左上（Figma 31,252）。</summary>
        public void Draw(Vector2 origin, Font font)
        {
            if (_rows.Count == 0) return;
            EnsureAssets(font);
            var previous = GUI.color;
            float bottom = origin.y + (VisibleRows - 1) * RowPitch;
            for (int i = 0; i < _rows.Count; i++)
            {
                var entry = _rows[i];
                float y = RowY(i, origin.y);
                float alpha = 1f;
                if (y < origin.y) alpha = Mathf.Clamp01(1f - (origin.y - y) / RowPitch);
                else if (y > bottom) alpha = Mathf.Clamp01(1f - (y - bottom) / RowPitch);
                alpha *= Mathf.Clamp01(entry.Age / FadeInSeconds);
                if (alpha <= 0.01f) continue;

                GUI.color = new Color(1f, 1f, 1f, alpha);
                var icon = Icon(entry.Stat);
                if (icon != null)
                    GUI.DrawTexture(new Rect(origin.x, y, IconWidth, IconHeight), icon, ScaleMode.ScaleToFit, true);
                _value.normal.textColor = entry.Delta > 0 ? PlusColor : MinusColor;
                string value = entry.Delta > 0 ? "+" + entry.Delta : entry.Delta.ToString();
                GUI.Label(new Rect(origin.x + ValueX, y, NameX - ValueX, IconHeight), value, _value);
                GUI.Label(new Rect(origin.x + NameX, y, 260f, IconHeight), entry.Name, _name);
            }

            GUI.color = previous;
        }

        Texture2D Icon(GrowthStat stat)
        {
            switch (stat)
            {
                case GrowthStat.Offense: return _iconOffense;
                case GrowthStat.Defense: return _iconDefense;
                default: return _iconStamina;
            }
        }

        void EnsureAssets(Font font)
        {
            if (_iconOffense == null) _iconOffense = Resources.Load<Texture2D>("MainScreen/icon_off2");
            if (_iconDefense == null) _iconDefense = Resources.Load<Texture2D>("MainScreen/icon_def2");
            if (_iconStamina == null) _iconStamina = Resources.Load<Texture2D>("MainScreen/icon_sta2");
            if (_value != null && _font == font) return;
            _font = font;
            _value = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 32,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Overflow,
                wordWrap = false,
                padding = new RectOffset(0, 0, 0, 0)
            };
            _name = new GUIStyle(_value) { fontSize = 24, normal = { textColor = Color.black } };
        }
    }
}
