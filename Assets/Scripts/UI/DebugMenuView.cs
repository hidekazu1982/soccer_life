using System;
using System.Collections.Generic;
using UnityEngine;

namespace SoccerClub.UI
{
    /// <summary>
    /// デバッグビルドだけに出す「デバッグ」ボタンと、その上に開くメニュー。
    /// 項目は <see cref="Add"/> で増やす。見出し（Section）ごとに、登録した順で並ぶ。
    /// </summary>
    public sealed class DebugMenuView
    {
        public sealed class Entry
        {
            public string Section;
            public string Label;
            public Color Color;
            /// <summary>実行できたら true。</summary>
            public Func<bool> Run;
        }

        const float PanelWidth = 340f;
        const float PanelGap = 12f;
        const float Pad = 16f;
        const float TitleHeight = 40f;
        const float SectionHeight = 34f;
        const float ItemHeight = 56f;
        const float ItemGap = 8f;
        const float SectionGap = 8f;

        static readonly Color PanelColor = new Color(0.08f, 0.10f, 0.12f, 0.94f);
        static readonly Color ToggleColor = new Color(0.30f, 0.30f, 0.34f, 1f);
        static readonly Color ToggleOpenColor = new Color(0.55f, 0.22f, 0.50f, 1f);
        static readonly Color DisabledColor = new Color(0.32f, 0.32f, 0.32f, 1f);

        readonly List<Entry> _entries = new List<Entry>();
        readonly List<string> _sections = new List<string>();
        bool _open;
        Font _font;
        GUIStyle _toggle;
        GUIStyle _title;
        GUIStyle _section;
        GUIStyle _item;
        GUIStyle _note;

        public bool IsOpen => _open;

        public void Close()
        {
            _open = false;
        }

        public void Add(string section, string label, Color color, Func<bool> run)
        {
            if (!_sections.Contains(section)) _sections.Add(section);
            _entries.Add(new Entry { Section = section, Label = label, Color = color, Run = run });
        }

        /// <summary>開いている間は画面全体を受け持つ（外側のクリックで建物が反応しないように）。</summary>
        public bool Hits(Rect button, Vector2 mouse)
        {
            return _open || button.Contains(mouse);
        }

        /// <summary>
        /// ボタンとメニューを描く。押された項目を返す（なければ null）。
        /// enabled が false の間は項目を灰色にして押せなくする。
        /// </summary>
        public Entry Draw(Rect button, Vector2 mouse, bool enabled, string disabledNote, Font font)
        {
            EnsureStyles(font);
            var click = Event.current;
            bool released = click != null && click.type == EventType.MouseUp && click.button == 0;

            Fill(button, _open ? ToggleOpenColor : ToggleColor);
            GUI.Label(button, _open ? "デバッグ ▼" : "デバッグ ▲", _toggle);
            if (released && button.Contains(mouse))
            {
                _open = !_open;
                click.Use();
                return null;
            }

            if (!_open) return null;

            var panel = PanelRect(button);
            Fill(panel, PanelColor);
            float x = panel.x + Pad;
            float width = panel.width - Pad * 2f;
            float y = panel.y + Pad;
            GUI.Label(new Rect(x, y, width, TitleHeight), "デバッグメニュー", _title);
            y += TitleHeight;
            if (!enabled && !string.IsNullOrEmpty(disabledNote))
            {
                GUI.Label(new Rect(x, y, width, SectionHeight), disabledNote, _note);
                y += SectionHeight;
            }

            Entry picked = null;
            for (int s = 0; s < _sections.Count; s++)
            {
                y += SectionGap;
                GUI.Label(new Rect(x, y, width, SectionHeight), "■ " + _sections[s], _section);
                y += SectionHeight;
                for (int i = 0; i < _entries.Count; i++)
                {
                    var entry = _entries[i];
                    if (entry.Section != _sections[s]) continue;
                    var rect = new Rect(x, y, width, ItemHeight);
                    Fill(rect, enabled ? entry.Color : DisabledColor);
                    GUI.Label(rect, entry.Label, _item);
                    if (released && enabled && picked == null && rect.Contains(mouse))
                        picked = entry;
                    y += ItemHeight + ItemGap;
                }
            }

            if (released)
            {
                if (picked != null)
                {
                    _open = false;
                    click.Use();
                }
                else if (panel.Contains(mouse))
                {
                    click.Use();
                }
                else
                {
                    // 外側のクリックは閉じるだけ。日送りボタンなどにはそのまま届ける。
                    _open = false;
                }
            }

            return picked;
        }

        Rect PanelRect(Rect button)
        {
            float height = Pad * 2f + TitleHeight + SectionHeight;
            for (int s = 0; s < _sections.Count; s++)
            {
                height += SectionGap + SectionHeight;
                for (int i = 0; i < _entries.Count; i++)
                {
                    if (_entries[i].Section == _sections[s]) height += ItemHeight + ItemGap;
                }
            }

            // 右端をボタンに揃える。画面の左にはみ出すときは、左端をボタンに揃える。
            float x = button.xMax - PanelWidth;
            if (x < 0f) x = button.x;
            return new Rect(x, button.y - PanelGap - height, PanelWidth, height);
        }

        static void Fill(Rect rect, Color color)
        {
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        void EnsureStyles(Font font)
        {
            if (_toggle != null && _font == font) return;
            _font = font;
            _toggle = Style(22, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            _title = Style(26, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
            _section = Style(20, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.78f, 0.84f, 0.90f, 1f));
            _item = Style(24, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            _note = Style(18, FontStyle.Normal, TextAnchor.MiddleLeft, new Color(1f, 0.75f, 0.45f, 1f));
        }

        GUIStyle Style(int size, FontStyle fontStyle, TextAnchor anchor, Color color)
        {
            var style = new GUIStyle(GUIStyle.none)
            {
                fontSize = size,
                fontStyle = fontStyle,
                alignment = anchor,
                normal = { textColor = color }
            };
            if (_font != null) style.font = _font;
            return style;
        }
    }
}
