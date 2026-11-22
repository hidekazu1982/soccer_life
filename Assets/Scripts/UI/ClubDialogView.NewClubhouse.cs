using System.Collections.Generic;
using System.Globalization;
using SoccerClub.Competition;
using SoccerClub.Core;
using SoccerClub.Data;
using SoccerClub.Personnel;
using SoccerClub.Time;
using UnityEngine;

namespace SoccerClub.UI
{
    /// <summary>
    /// 新しいクラブハウス（Figma の main の new_clubhouse）。いまは選手タブだけ新しいデザインで描く。
    /// 左が選んだ選手の詳細、右が選手一覧。スタッフ・フロントのタブは今までの画面を使う。
    /// </summary>
    public sealed partial class ClubDialogView
    {
        // ---- 枠（dialog 205:2）。
        static readonly Rect NcDialog = new Rect(160f, 158f, 1600f, 800f);
        static readonly Rect NcTitle = new Rect(160f, 190f, 1600f, 58f);
        static readonly Rect NcClose = new Rect(1628f, 190f, 100f, 100f);
        const float NcTabY = 265f;
        const float NcTabWidth = 240f;
        const float NcTabHeight = 56f;
        const float NcTabStep = 248f;
        // Figma のタブは 656 から 3 つ。強化タブはなくした（施設は事業部の提案に任せる）。
        const float NcTabX = 656f;
        static readonly string[] NcTabLabels = { "選手", "スタッフ", "フロント" };
        static readonly ClubPage[] NcTabPages = { ClubPage.Players, ClubPage.Staff, ClubPage.Front };

        // ---- 一覧（player_list 207:664）。見出し 656,329 / 高さ 56、行 100（間 8）、見える範囲 393〜925。
        const float NcListX = 656f;
        const float NcListWidth = 1044f;
        const float NcHeadY = 329f;
        const float NcHeadHeight = 56f;
        static readonly Rect NcRows = new Rect(656f, 393f, 1044f, 532f);
        const float NcRowHeight = 100f;
        const float NcRowStride = 108f;
        static readonly Rect NcScrollTrack = new Rect(1708f, 393f, 10f, 532f);

        // 見出しの列。x、幅、文字、並べ替え。
        static readonly float[] NcHeadX = { 656f, 756f, 828f, 1124f, 1208f, 1332f, 1636f };
        static readonly float[] NcHeadW = { 100f, 72f, 296f, 84f, 124f, 304f, 64f };
        static readonly string[] NcHeadLabels = { "POS", "M/F", "name", "age", "param", "annual salary", "" };
        static readonly PlayerSort[] NcHeadSort =
        {
            PlayerSort.Position, PlayerSort.None, PlayerSort.Name, PlayerSort.Age, PlayerSort.Overall, PlayerSort.Salary, PlayerSort.Condition
        };

        // ---- 選手の詳細（player 208:32 と player01 206:3）。
        static readonly Rect NcDetailPanel = new Rect(192f, 329f, 448f, 596f);
        const float NcDetailX = 252f;
        const float NcDetailWidth = 328f;
        static readonly Rect NcPortraitBox = new Rect(252f, 451f, 160f, 160f);
        // character3（244 角を 211,451 に置く）。シートは 2048 角の 212,46 / 1622×1998 を 244/2048 倍。
        const float NcPortraitScale = 244f / 2048f;
        static readonly Rect NcPortraitSheet = new Rect(
            211f + 212f * NcPortraitScale, 451f + 46f * NcPortraitScale, 1622f * NcPortraitScale, 1998f * NcPortraitScale);
        // 関係と個人データ（667〜899）は、多いときにスクロールする。
        static readonly Rect NcInfoArea = new Rect(252f, 667f, 336f, 232f);
        static readonly Rect NcInfoTrack = new Rect(590f, 667f, 6f, 232f);
        const float NcInfoStride = 48f;

        static readonly Color NcGray = Hex(0x8E8E93);
        static readonly Color NcGray2 = Hex(0xAEAEB2);
        static readonly Color NcGray3 = Hex(0xC7C7CC);
        static readonly Color NcGray4 = Hex(0xD1D1D6);
        static readonly Color NcGray5 = Hex(0xE5E5EA);
        static readonly Color NcGray6 = Hex(0xF2F2F7);
        // Figma に無い。選んでいる行は白くして分かるようにする。
        static readonly Color NcSelected = Color.white;

        GUIStyle _ncTitle;
        GUIStyle _ncClose;
        GUIStyle _ncCell;
        GUIStyle _ncCellLeft;
        GUIStyle _ncCellRight;
        GUIStyle _ncSmall;
        GUIStyle _ncSmallCenter;
        GUIStyle _ncSmallRight;
        GUIStyle _ncOverall;
        Font _ncFont;

        float _ncInfoScroll;
        bool _ncInfoPress;
        float _ncInfoLastY;
        string _ncInfoKey;
        readonly List<KeyValuePair<string, string>> _ncRelations = new List<KeyValuePair<string, string>>();

        /// <summary>新しいクラブハウスで描くか。選手タブだけ。</summary>
        bool UsesNewClubhouse => _kind == Kind.Clubhouse && _page == ClubPage.Players;

        void DrawNewClubhouse(GameSession session, Club club, System.Action<string> focusCharacter)
        {
            EnsureNcStyles();
            var previous = GUI.color;
            GUI.color = new Color(0.04f, 0.06f, 0.05f, 0.45f);
            GUI.DrawTexture(new Rect(-GameRunner.HorizontalEdgeInset, -GameRunner.VerticalEdgeInset,
                GameRunner.ReferenceWidth + GameRunner.HorizontalEdgeInset * 2f,
                GameRunner.ReferenceHeight + GameRunner.VerticalEdgeInset * 2f), Texture2D.whiteTexture);
            NcFill(NcDialog, NcGray6);
            GUI.Label(NcTitle, "クラブハウス", _ncTitle);

            if (session.GameOver)
                GUI.Label(new Rect(NcDialog.x, 240f, NcDialog.width, 28f), "資金ショートのためゲームオーバーです。", _ncSmallCenter);

            if (club != null) club.EnsureShirtNumbers();
            CollectRoster(club);
            DrawNcPlayerList();
            var selected = SelectedPlayer();
            DrawNcDetail(session, club, selected);

            // タブと閉じるボタンは最後に置いて、一覧のドラッグより先に押せるようにする。
            for (int i = 0; i < NcTabLabels.Length; i++)
            {
                var tab = new Rect(NcTabX + NcTabStep * i, NcTabY, NcTabWidth, NcTabHeight);
                bool on = _page == NcTabPages[i];
                NcFill(tab, on ? NcGray4 : NcGray5);
                GUI.Label(tab, NcTabLabels[i], _ncCell);
                if (!on && GUI.Button(tab, GUIContent.none, _hit))
                    SetPage(NcTabPages[i]);
            }

            NcFill(NcClose, NcGray5);
            GUI.Label(NcClose, "✕", _ncClose);
            if (GUI.Button(NcClose, GUIContent.none, _hit))
                Close();

            GUI.color = previous;
        }

        // ---- 一覧

        void DrawNcPlayerList()
        {
            for (int i = 0; i < NcHeadX.Length; i++)
            {
                var cell = new Rect(NcHeadX[i], NcHeadY, NcHeadW[i], NcHeadHeight);
                NcFill(cell, NcGray4);
                string label = NcHeadLabels[i];
                var sort = NcHeadSort[i];
                if (sort != PlayerSort.None && _playerSort == sort)
                    label += _playerSortAsc ? "▲" : "▼";
                FitNcLabel(cell, label, _ncCell, 36);
                if (sort != PlayerSort.None && GUI.Button(cell, GUIContent.none, _hit))
                    TogglePlayerSort(sort);
            }

            float content = _roster.Count == 0 ? 0f : _roster.Count * NcRowStride - (NcRowStride - NcRowHeight);
            UpdateNcScroll(content);

            if (_roster.Count == 0)
            {
                GUI.Label(NcRows, "選手はいません。", _ncCell);
            }
            else
            {
                GUI.BeginGroup(NcRows);
                for (int i = 0; i < _roster.Count; i++)
                {
                    float y = i * NcRowStride - _playerScroll;
                    if (y + NcRowHeight < 0f || y > NcRows.height) continue;
                    DrawNcRow(_roster[i], y);
                }

                GUI.EndGroup();
            }

            DrawNcScrollbar(content);
        }

        /// <summary>BeginGroup の中。x は NcListX を 0 にした座標。</summary>
        void DrawNcRow(Player player, float y)
        {
            bool selected = player.Id == _selectedPlayerId;
            NcFill(new Rect(0f, y, NcListWidth, NcRowHeight), selected ? NcSelected : NcGray5);
            NcFill(new Rect(0f, y, 100f, 100f), NcGray4);
            GUI.Label(new Rect(15f, y, 85f, NcRowHeight), player.BestPosition().ToString(), _ncCellLeft);
            NcFill(new Rect(112f, y + 10f, 48f, 80f), NcGray4);
            GUI.Label(new Rect(112f, y, 48f, NcRowHeight), player.Gender == Gender.Female ? "F" : "M", _ncCell);
            FitNcLabel(new Rect(177f, y, 288f, NcRowHeight), player.Name ?? "", _ncCellLeft, 36);
            GUI.Label(new Rect(1124f - NcListX, y, 84f, NcRowHeight), player.Age.ToString(CultureInfo.InvariantCulture), _ncCell);
            FitNcLabel(new Rect(1208f - NcListX, y, 124f, NcRowHeight), player.DisplayOverall.ToString(CultureInfo.InvariantCulture), _ncCell, 36);
            FitNcLabel(new Rect(1340f - NcListX, y, 288f, NcRowHeight), MoneyText.Format(player.AnnualSalary), _ncCellRight, 36);
            DrawTex(new Rect(1644f - NcListX, y + 26f, 48f, 49f), ConditionIcon(player));
        }

        void UpdateNcScroll(float content)
        {
            float maxScroll = Mathf.Max(0f, content - NcRows.height);
            _playerScroll = Mathf.Clamp(_playerScroll, 0f, maxScroll);
            var e = Event.current;
            if (e == null) return;

            bool overRows = NcRows.Contains(e.mousePosition);
            if (e.type == EventType.MouseDown && e.button == 0 && overRows)
            {
                _playerPress = true;
                _playerDragging = false;
                _playerPressPos = e.mousePosition;
                _playerDragLastY = e.mousePosition.y;
            }
            else if (_playerPress && e.type == EventType.MouseDrag)
            {
                if (!_playerDragging && Mathf.Abs(e.mousePosition.y - _playerPressPos.y) > 8f)
                    _playerDragging = true;
                if (_playerDragging && maxScroll > 0.5f)
                {
                    _playerScroll = Mathf.Clamp(_playerScroll - (e.mousePosition.y - _playerDragLastY), 0f, maxScroll);
                    _playerDragLastY = e.mousePosition.y;
                    e.Use();
                }
            }
            else if (e.type == EventType.MouseUp && e.button == 0 && _playerPress)
            {
                bool dragged = _playerDragging;
                _playerPress = false;
                _playerDragging = false;
                if (!dragged && overRows)
                {
                    float local = e.mousePosition.y - NcRows.y + _playerScroll;
                    int index = (int)(local / NcRowStride);
                    if (index >= 0 && index < _roster.Count && _roster[index] != null
                        && local - index * NcRowStride <= NcRowHeight)
                        _selectedPlayerId = _roster[index].Id;
                }

                e.Use();
                return;
            }

            if (e.type == EventType.MouseUp)
            {
                if (_playerThumbDrag) e.Use();
                _playerThumbDrag = false;
            }

            if (maxScroll <= 0.5f) return;

            var track = NcScrollTrack;
            float thumbH = NcThumbHeight(content);
            float span = Mathf.Max(1f, track.height - thumbH);
            float thumbY = track.y + span * (_playerScroll / maxScroll);
            // 細いバーなので、つかむ範囲は左右に広げる。
            var grab = new Rect(track.x - 12f, thumbY, track.width + 24f, thumbH);
            if (e.type == EventType.ScrollWheel && (overRows || grab.Contains(e.mousePosition)))
            {
                _playerScroll = Mathf.Clamp(_playerScroll + e.delta.y * 40f, 0f, maxScroll);
                e.Use();
            }
            else if (e.type == EventType.MouseDown && e.button == 0 && grab.Contains(e.mousePosition))
            {
                _playerThumbDrag = true;
                _playerThumbGrab = e.mousePosition.y - thumbY;
                e.Use();
            }
            else if (_playerThumbDrag && e.type == EventType.MouseDrag)
            {
                float y = e.mousePosition.y - _playerThumbGrab;
                _playerScroll = Mathf.Clamp01((y - track.y) / span) * maxScroll;
                e.Use();
            }
        }

        float NcThumbHeight(float content)
        {
            if (content <= NcRows.height) return NcScrollTrack.height;
            return Mathf.Clamp(NcScrollTrack.height * NcRows.height / content, 48f, NcScrollTrack.height);
        }

        void DrawNcScrollbar(float content)
        {
            var track = NcScrollTrack;
            NcFill(track, NcGray);
            float maxScroll = Mathf.Max(0f, content - NcRows.height);
            float thumbH = NcThumbHeight(content);
            float span = Mathf.Max(1f, track.height - thumbH);
            float t = maxScroll <= 0.5f ? 0f : _playerScroll / maxScroll;
            NcFill(new Rect(track.x, track.y + span * t, track.width, thumbH), NcGray4);
        }

        // ---- 選手の詳細

        void DrawNcDetail(GameSession session, Club club, Player player)
        {
            NcFill(NcDetailPanel, NcGray5);
            if (player == null) return;

            // 1行目：ポジション・名前・背番号。
            var row = new Rect(NcDetailX, 355f, NcDetailWidth, 40f);
            NcFill(row, NcGray4);
            NcPill(new Rect(257f, 359f, 63f, 32f), player.BestPosition().ToString());
            FitNcLabel(new Rect(336f, 355f, 168f, 40f), player.Name ?? "", _ncSmall, 20);
            if (player.ShirtNumber > 0)
                GUI.Label(new Rect(512f, 355f, 60f, 40f), player.ShirtNumber.ToString(CultureInfo.InvariantCulture), _ncSmall);

            // 2行目：性別・誕生日・年齢。
            row = new Rect(NcDetailX, 403f, NcDetailWidth, 40f);
            NcFill(row, NcGray4);
            NcPill(new Rect(257f, 407f, 63f, 32f), player.Gender == Gender.Female ? "Female" : "Male");
            int month;
            int day;
            CharacterLife.Birthday(player.Id, out month, out day);
            GUI.Label(new Rect(336f, 403f, 70f, 40f), "誕生日", _ncSmall);
            GUI.Label(new Rect(412f, 403f, 90f, 40f), month.ToString(CultureInfo.InvariantCulture) + "/" + day.ToString(CultureInfo.InvariantCulture), _ncSmall);
            GUI.Label(new Rect(512f, 403f, 64f, 40f), player.Age.ToString(CultureInfo.InvariantCulture) + "歳", _ncSmall);

            // 顔（160 角に切り抜く）。
            NcFill(NcPortraitBox, NcGray4);
            var portrait = PortraitOf(player, club);
            if (portrait != null)
            {
                GUI.BeginGroup(NcPortraitBox);
                var sheet = new Rect(NcPortraitSheet.x - NcPortraitBox.x, NcPortraitSheet.y - NcPortraitBox.y, NcPortraitSheet.width, NcPortraitSheet.height);
                GUI.DrawTexture(sheet, portrait, ScaleMode.StretchToFill, true);
                GUI.EndGroup();
            }

            // 総合力と OFF・DEF・STA。
            NcFill(new Rect(420f, 451f, 160f, 40f), NcGray4);
            GUI.Label(new Rect(420f, 451f, 160f, 40f), player.DisplayOverall.ToString(CultureInfo.InvariantCulture), _ncOverall);
            NcStat(491f, "OFF", player.DisplayOffense, NcGray3);
            NcStat(531f, "DEF", player.DisplayDefense, NcGray4);
            NcStat(571f, "STA", player.DisplayStaminaMax, NcGray3);

            // 年俸。
            NcFill(new Rect(NcDetailX, 619f, NcDetailWidth, 40f), NcGray4);
            GUI.Label(new Rect(260f, 619f, 80f, 40f), "年俸", _ncSmall);
            FitNcLabel(new Rect(340f, 619f, 232f, 40f), MoneyText.Format(player.AnnualSalary), _ncSmallRight, 20);

            DrawNcInfo(session, player);
        }

        void NcStat(float y, string label, int value, Color color)
        {
            NcFill(new Rect(420f, y, 160f, 40f), color);
            GUI.Label(new Rect(429f, y, 60f, 40f), label, _ncSmall);
            GUI.Label(new Rect(480f, y, 92f, 40f), value.ToString(CultureInfo.InvariantCulture), _ncSmallRight);
        }

        /// <summary>関係（夫婦・恋人・親友・友達）と、パーソナルデータ（趣味・好きな食べ物・苦手な食べ物・子ども）。多いときはスクロールする。</summary>
        void DrawNcInfo(GameSession session, Player player)
        {
            string key = player.Id + "|" + (session != null ? session.Time.CurrentDate.Ticks : 0L);
            if (_ncInfoKey != key)
            {
                _ncInfoKey = key;
                _ncInfoScroll = 0f;
                BuildNcRelations(session, player);
            }

            string hobby = CharacterLife.Hobby(player.Id);
            if (CharacterLife.IsMovie(hobby)) hobby = "映画（" + CharacterLife.Movie(player.Id) + "）";
            bool children = player.Children > 0;
            int rows = _ncRelations.Count + 3 + (children ? 1 : 0);
            float content = rows * NcInfoStride - (NcInfoStride - 40f);
            float maxScroll = Mathf.Max(0f, content - NcInfoArea.height);
            UpdateNcInfoScroll(maxScroll);

            GUI.BeginGroup(NcInfoArea);
            float y = -_ncInfoScroll;
            for (int i = 0; i < _ncRelations.Count; i++, y += NcInfoStride)
            {
                NcFill(new Rect(0f, y, NcDetailWidth, 40f), NcGray4);
                NcPill(new Rect(5f, y + 4f, 63f, 32f), _ncRelations[i].Key);
                FitNcLabel(new Rect(84f, y, 236f, 40f), _ncRelations[i].Value, _ncSmall, 20);
            }

            NcPersonal(y, "趣味", hobby);
            y += NcInfoStride;
            NcPersonal(y, "好きな食べ物", CharacterLife.FavoriteFood(player.Id));
            y += NcInfoStride;
            NcPersonal(y, "苦手な食べ物", CharacterLife.DislikedFood(player.Id));
            if (children)
            {
                y += NcInfoStride;
                NcPersonal(y, "子ども", player.Children.ToString(CultureInfo.InvariantCulture) + "人");
            }

            GUI.EndGroup();

            if (maxScroll > 0.5f)
            {
                NcFill(NcInfoTrack, NcGray);
                float thumbH = Mathf.Clamp(NcInfoTrack.height * NcInfoArea.height / content, 32f, NcInfoTrack.height);
                float span = Mathf.Max(1f, NcInfoTrack.height - thumbH);
                NcFill(new Rect(NcInfoTrack.x, NcInfoTrack.y + span * (_ncInfoScroll / maxScroll), NcInfoTrack.width, thumbH), NcGray4);
            }
        }

        void NcPersonal(float y, string label, string value)
        {
            NcFill(new Rect(0f, y, NcDetailWidth, 40f), NcGray4);
            NcFill(new Rect(5f, y + 4f, 139f, 32f), NcGray2);
            GUI.Label(new Rect(5f, y + 4f, 139f, 32f), label, _ncSmallCenter);
            FitNcLabel(new Rect(152f, y, 168f, 40f), value ?? "", _ncSmall, 20);
        }

        void UpdateNcInfoScroll(float maxScroll)
        {
            _ncInfoScroll = Mathf.Clamp(_ncInfoScroll, 0f, maxScroll);
            var e = Event.current;
            if (e == null) return;
            var area = new Rect(NcInfoArea.x, NcInfoArea.y, NcInfoArea.width + 12f, NcInfoArea.height);
            bool over = area.Contains(e.mousePosition);
            if (e.type == EventType.ScrollWheel && over && maxScroll > 0.5f)
            {
                _ncInfoScroll = Mathf.Clamp(_ncInfoScroll + e.delta.y * 24f, 0f, maxScroll);
                e.Use();
            }
            else if (e.type == EventType.MouseDown && e.button == 0 && over)
            {
                _ncInfoPress = true;
                _ncInfoLastY = e.mousePosition.y;
                e.Use();
            }
            else if (_ncInfoPress && e.type == EventType.MouseDrag)
            {
                _ncInfoScroll = Mathf.Clamp(_ncInfoScroll - (e.mousePosition.y - _ncInfoLastY), 0f, maxScroll);
                _ncInfoLastY = e.mousePosition.y;
                e.Use();
            }
            else if (_ncInfoPress && e.type == EventType.MouseUp)
            {
                _ncInfoPress = false;
                e.Use();
            }
        }

        /// <summary>関係の一覧を作る。夫婦・恋人・親友・友達の順。名前は所属クラブを問わず探す。</summary>
        void BuildNcRelations(GameSession session, Player player)
        {
            _ncRelations.Clear();
            var relations = session != null && session.Relations != null ? session.Relations.For(player.Id) : null;
            if (relations == null || relations.Count == 0) return;
            var people = ProfilePeople(session);
            var sorted = new List<Relationship>(relations);
            sorted.Sort((a, b) => NcStageOrder(a.Stage).CompareTo(NcStageOrder(b.Stage)));
            for (int i = 0; i < sorted.Count; i++)
            {
                var rel = sorted[i];
                if (NcStageOrder(rel.Stage) >= 4) continue;
                string otherId = rel.Other(player.Id);
                ProfilePerson person;
                string name = otherId != null && people.TryGetValue(otherId, out person) ? person.Name : (otherId ?? "");
                _ncRelations.Add(new KeyValuePair<string, string>(StageLabel(rel.Stage), name));
            }
        }

        static int NcStageOrder(RelationStage stage)
        {
            switch (stage)
            {
                case RelationStage.Married: return 0;
                case RelationStage.Lover: return 1;
                case RelationStage.BestFriend: return 2;
                case RelationStage.Friend: return 3;
                default: return 4;
            }
        }

        // ---- 部品

        void NcPill(Rect rect, string text)
        {
            NcFill(rect, NcGray2);
            FitNcLabel(rect, text, _ncSmallCenter, 20);
        }

        static void NcFill(Rect rect, Color color)
        {
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, color, 0f, 0f);
        }

        /// <summary>幅に収まらないときだけ文字を小さくする。</summary>
        static void FitNcLabel(Rect rect, string text, GUIStyle style, int size)
        {
            int previous = style.fontSize;
            style.fontSize = size;
            var content = new GUIContent(text);
            while (style.fontSize > 12 && style.CalcSize(content).x > rect.width)
                style.fontSize -= 2;
            GUI.Label(rect, content, style);
            style.fontSize = previous;
        }

        static Color Hex(int rgb)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
        }

        void EnsureNcStyles()
        {
            if (_ncCell != null && _ncFont == _font) return;
            _ncFont = _font;
            var label = new GUIStyle(GUI.skin.label)
            {
                font = _font,
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Overflow,
                wordWrap = false,
                richText = false,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
                normal = { textColor = Color.black }
            };
            _ncTitle = new GUIStyle(label) { fontSize = 48 };
            _ncClose = new GUIStyle(label) { fontSize = 64 };
            _ncCell = new GUIStyle(label) { fontSize = 36 };
            _ncCellLeft = new GUIStyle(label) { fontSize = 36, alignment = TextAnchor.MiddleLeft };
            _ncCellRight = new GUIStyle(label) { fontSize = 36, alignment = TextAnchor.MiddleRight };
            _ncSmall = new GUIStyle(label) { fontSize = 20, alignment = TextAnchor.MiddleLeft };
            _ncSmallCenter = new GUIStyle(label) { fontSize = 20 };
            _ncSmallRight = new GUIStyle(label) { fontSize = 20, alignment = TextAnchor.MiddleRight };
            _ncOverall = new GUIStyle(label) { fontSize = 24 };
        }
    }
}
