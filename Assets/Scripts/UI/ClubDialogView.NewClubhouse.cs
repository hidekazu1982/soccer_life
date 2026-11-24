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
    /// 新しいクラブハウス（Figma の main の new_clubhouse）。選手・スタッフ・フロントの 3 タブを新しいデザインで描く。
    /// 左が選んだ人の詳細（選手詳細 206:3 / スタッフ詳細 211:75 / フロント詳細 215:2）、
    /// 右が一覧（選手リスト 206:618 / スタッフリスト 211:3 / フロントリスト 214:2）。フロントは ClubDialogView.NewFront.cs。
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

        // ---- 一覧。見出し 656,329 / 高さ 56、行 100（間 8）、見える範囲 393〜925。
        const float NcListX = 656f;
        const float NcListWidth = 1044f;
        const float NcHeadY = 329f;
        const float NcHeadHeight = 56f;
        static readonly Rect NcRows = new Rect(656f, 393f, 1044f, 532f);
        const float NcRowHeight = 100f;
        const float NcRowStride = 108f;
        static readonly Rect NcScrollTrack = new Rect(1708f, 393f, 10f, 532f);

        // 見出しの列（選手とスタッフで同じ位置）。
        static readonly float[] NcHeadX = { 656f, 756f, 828f, 1124f, 1208f, 1332f, 1636f };
        static readonly float[] NcHeadW = { 100f, 72f, 296f, 84f, 124f, 304f, 64f };
        static readonly string[] NcPlayerHeadLabels = { "POS", "M/F", "名前", "AGE", "総合力", "年俸", "" };
        static readonly PlayerSort[] NcPlayerHeadSort =
        {
            PlayerSort.Position, PlayerSort.None, PlayerSort.Name, PlayerSort.Age, PlayerSort.Overall, PlayerSort.Salary, PlayerSort.Condition
        };
        static readonly string[] NcStaffHeadLabels = { "POS", "M/F", "名前", "AGE", "LV", "年俸", "" };
        static readonly NcStaffSort[] NcStaffHeadSort =
        {
            NcStaffSort.Role, NcStaffSort.None, NcStaffSort.Name, NcStaffSort.Age, NcStaffSort.Level, NcStaffSort.Salary, NcStaffSort.None
        };

        // ---- 詳細（詳細枠 208:32、中身は 236〜597）。
        static readonly Rect NcDetailPanel = new Rect(192f, 329f, 448f, 596f);
        const float NcDetailX = 236f;
        const float NcDetailWidth = 361f;
        const float NcPillX = 241f;
        const float NcRightX = 588f;
        static readonly Rect NcPortraitBox = new Rect(236f, 451f, 160f, 160f);
        // character3（244 角を 195,451 に置く）。シートは 2048 角の 212,46 / 1622×1998 を 244/2048 倍。
        const float NcPortraitScale = 244f / 2048f;
        static readonly Rect NcPortraitSheet = new Rect(
            195f + 212f * NcPortraitScale, 451f + 46f * NcPortraitScale, 1622f * NcPortraitScale, 1998f * NcPortraitScale);
        // 顔の右の箱（404〜597）。
        const float NcBoxX = 404f;
        const float NcBoxWidth = 193f;
        // 関係とパーソナルデータ（667〜899）は、多いときにスクロールする。
        static readonly Rect NcInfoArea = new Rect(236f, 667f, 361f, 232f);
        static readonly Rect NcInfoTrack = new Rect(605f, 667f, 10f, 232f);
        const float NcInfoStride = 48f;

        static readonly Color NcGray = Hex(0x8E8E93);
        static readonly Color NcGray2 = Hex(0xAEAEB2);
        static readonly Color NcGray3 = Hex(0xC7C7CC);
        static readonly Color NcGray4 = Hex(0xD1D1D6);
        static readonly Color NcGray5 = Hex(0xE5E5EA);
        static readonly Color NcGray6 = Hex(0xF2F2F7);
        // Figma に無い。選んでいる行は白くして分かるようにする。
        static readonly Color NcSelected = Color.white;

        enum NcStaffSort
        {
            None,
            Role,
            Name,
            Age,
            Level,
            Salary
        }

        sealed class NcStaffEntry
        {
            public StaffMember Staff;
            public TeamStaffRole Role;
            public int Order;
        }

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

        readonly List<NcStaffEntry> _ncStaff = new List<NcStaffEntry>();
        float _ncStaffScroll;
        string _ncStaffSelectedId;
        NcStaffSort _ncStaffSort = NcStaffSort.Role;
        bool _ncStaffAsc = true;

        /// <summary>新しいクラブハウスで描くか。選手・スタッフ・フロントのタブ。</summary>
        bool UsesNewClubhouse => _kind == Kind.Clubhouse
                                 && (_page == ClubPage.Players || _page == ClubPage.Staff || _page == ClubPage.Front);

        void DrawNewClubhouse(GameSession session, Club club, System.Action<string> focusCharacter, System.Action<string> setStatus)
        {
            EnsureNcStyles();
            var previous = GUI.color;
            GUI.color = new Color(0.04f, 0.06f, 0.05f, 0.45f);
            GUI.DrawTexture(new Rect(-GameRunner.HorizontalEdgeInset, -GameRunner.VerticalEdgeInset,
                GameRunner.ReferenceWidth + GameRunner.HorizontalEdgeInset * 2f,
                GameRunner.ReferenceHeight + GameRunner.VerticalEdgeInset * 2f), Texture2D.whiteTexture);
            // 暗幕の色のままだと、顔やコンディションのアイコンが暗く半透明になる。
            GUI.color = Color.white;
            NcFill(NcDialog, NcGray6);
            GUI.Label(NcTitle, "クラブハウス", _ncTitle);

            if (session.GameOver)
                GUI.Label(new Rect(NcDialog.x, 240f, NcDialog.width, 28f), "資金ショートのためゲームオーバーです。", _ncSmallCenter);

            if (_page == ClubPage.Front)
            {
                DrawNcFront(session, club, setStatus);
            }
            else if (_page == ClubPage.Staff)
            {
                CollectNcStaff(club);
                DrawNcStaffList();
                DrawNcStaffDetail(session, club, SelectedNcStaff());
            }
            else
            {
                if (club != null) club.EnsureShirtNumbers();
                CollectRoster(club);
                DrawNcPlayerList();
                DrawNcDetail(session, club, SelectedPlayer());
            }

            // 契約の確認中は、確認のパネルだけ押せる。
            if (_page == ClubPage.Front && _frontConfirm)
            {
                DrawFrontConfirm(session, setStatus);
                GUI.color = previous;
                return;
            }

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

        // ---- 選手の一覧

        void DrawNcPlayerList()
        {
            for (int i = 0; i < NcHeadX.Length; i++)
            {
                var sort = NcPlayerHeadSort[i];
                string label = NcPlayerHeadLabels[i];
                if (sort != PlayerSort.None && _playerSort == sort)
                    label += _playerSortAsc ? "▲" : "▼";
                if (DrawNcHead(i, label, sort != PlayerSort.None))
                    TogglePlayerSort(sort);
            }

            float content = NcContent(_roster.Count);
            int clicked = UpdateNcScroll(content, _roster.Count, ref _playerScroll);
            if (clicked >= 0 && _roster[clicked] != null)
                _selectedPlayerId = _roster[clicked].Id;

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
                    DrawNcPlayerRow(_roster[i], y);
                }

                GUI.EndGroup();
            }

            DrawNcScrollbar(content, _playerScroll);
        }

        /// <summary>BeginGroup の中。x は NcListX を 0 にした座標。</summary>
        void DrawNcPlayerRow(Player player, float y)
        {
            DrawNcRowBase(y, player.Id == _selectedPlayerId, player.BestPosition().ToString(), player.Gender, player.Name, player.Age);
            FitNcLabel(new Rect(1208f - NcListX, y, 124f, NcRowHeight), player.DisplayOverall.ToString(CultureInfo.InvariantCulture), _ncCell, 36);
            FitNcLabel(new Rect(1340f - NcListX, y, 288f, NcRowHeight), MoneyText.Format(player.AnnualSalary), _ncCellRight, 36);
            DrawTex(new Rect(1644f - NcListX, y + 26f, 48f, 49f), ConditionIcon(player));
        }

        /// <summary>行の地、POS、M/F、名前、年齢（選手とスタッフで共通）。</summary>
        void DrawNcRowBase(float y, bool selected, string pos, Gender gender, string name, int age)
        {
            NcFill(new Rect(0f, y, NcListWidth, NcRowHeight), selected ? NcSelected : NcGray5);
            NcFill(new Rect(0f, y, 100f, 100f), NcGray4);
            FitNcLabel(new Rect(0f, y, 100f, NcRowHeight), pos, _ncCell, 36);
            NcFill(new Rect(112f, y + 10f, 48f, 80f), NcGray4);
            GUI.Label(new Rect(112f, y, 48f, NcRowHeight), gender == Gender.Female ? "F" : "M", _ncCell);
            FitNcLabel(new Rect(177f, y, 288f, NcRowHeight), name ?? "", _ncCellLeft, 36);
            GUI.Label(new Rect(1124f - NcListX, y, 84f, NcRowHeight), age.ToString(CultureInfo.InvariantCulture), _ncCell);
        }

        /// <summary>見出し 1 列。並べ替えできる列なら、押されたら true。</summary>
        bool DrawNcHead(int column, string label, bool sortable)
        {
            var cell = new Rect(NcHeadX[column], NcHeadY, NcHeadW[column], NcHeadHeight);
            NcFill(cell, NcGray4);
            FitNcLabel(cell, label, _ncCell, 36);
            return sortable && GUI.Button(cell, GUIContent.none, _hit);
        }

        static float NcContent(int count)
        {
            return count == 0 ? 0f : count * NcRowStride - (NcRowStride - NcRowHeight);
        }

        /// <summary>
        /// 一覧のスクロールと行のタップ。ドラッグ、ホイール、つまみで動かす。
        /// ドラッグしないで離したら、その行の番号を返す（なければ -1）。
        /// </summary>
        int UpdateNcScroll(float content, int count, ref float scroll)
        {
            float maxScroll = Mathf.Max(0f, content - NcRows.height);
            scroll = Mathf.Clamp(scroll, 0f, maxScroll);
            var e = Event.current;
            if (e == null) return -1;

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
                    scroll = Mathf.Clamp(scroll - (e.mousePosition.y - _playerDragLastY), 0f, maxScroll);
                    _playerDragLastY = e.mousePosition.y;
                    e.Use();
                }
            }
            else if (e.type == EventType.MouseUp && e.button == 0 && _playerPress)
            {
                bool dragged = _playerDragging;
                _playerPress = false;
                _playerDragging = false;
                int clicked = -1;
                if (!dragged && overRows)
                {
                    float local = e.mousePosition.y - NcRows.y + scroll;
                    int index = (int)(local / NcRowStride);
                    if (index >= 0 && index < count && local - index * NcRowStride <= NcRowHeight)
                        clicked = index;
                }

                e.Use();
                return clicked;
            }

            if (e.type == EventType.MouseUp)
            {
                if (_playerThumbDrag) e.Use();
                _playerThumbDrag = false;
            }

            if (maxScroll <= 0.5f) return -1;

            var track = NcScrollTrack;
            float thumbH = NcThumbHeight(content);
            float span = Mathf.Max(1f, track.height - thumbH);
            float thumbY = track.y + span * (scroll / maxScroll);
            // 細いバーなので、つかむ範囲は左右に広げる。
            var grab = new Rect(track.x - 12f, thumbY, track.width + 24f, thumbH);
            if (e.type == EventType.ScrollWheel && (overRows || grab.Contains(e.mousePosition)))
            {
                scroll = Mathf.Clamp(scroll + e.delta.y * 40f, 0f, maxScroll);
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
                scroll = Mathf.Clamp01((y - track.y) / span) * maxScroll;
                e.Use();
            }

            return -1;
        }

        float NcThumbHeight(float content)
        {
            if (content <= NcRows.height) return NcScrollTrack.height;
            return Mathf.Clamp(NcScrollTrack.height * NcRows.height / content, 48f, NcScrollTrack.height);
        }

        void DrawNcScrollbar(float content, float scroll)
        {
            var track = NcScrollTrack;
            NcFill(track, NcGray);
            float maxScroll = Mathf.Max(0f, content - NcRows.height);
            float thumbH = NcThumbHeight(content);
            float span = Mathf.Max(1f, track.height - thumbH);
            float t = maxScroll <= 0.5f ? 0f : scroll / maxScroll;
            NcFill(new Rect(track.x, track.y + span * t, track.width, thumbH), NcGray4);
        }

        // ---- スタッフの一覧

        /// <summary>チームスタッフを役職の順（監督、ヘッドコーチ、コーチ、フィジカルコーチ、トレーナー、ドクター）に集める。空き枠も行にする。</summary>
        void CollectNcStaff(Club club)
        {
            _ncStaff.Clear();
            var team = club != null ? club.Team : null;
            if (team == null) return;
            AddNcStaff(team.Manager, TeamStaffRole.Manager);
            AddNcStaff(team.HeadCoach, TeamStaffRole.HeadCoach);
            AddNcStaffSlots(team.Coaches, TeamStaffRole.Coach);
            AddNcStaff(team.PhysicalCoach, TeamStaffRole.PhysicalCoach);
            AddNcStaffSlots(team.Trainers, TeamStaffRole.Trainer);
            AddNcStaff(team.Doctor, TeamStaffRole.Doctor);
            _ncStaff.Sort(CompareNcStaff);
        }

        void AddNcStaffSlots(StaffMember[] slots, TeamStaffRole role)
        {
            if (slots == null) return;
            for (int i = 0; i < slots.Length; i++)
                AddNcStaff(slots[i], role);
        }

        void AddNcStaff(StaffMember staff, TeamStaffRole role)
        {
            _ncStaff.Add(new NcStaffEntry { Staff = staff, Role = role, Order = _ncStaff.Count });
        }

        /// <summary>空き枠はいつも最後。同じ値なら役職の順。</summary>
        int CompareNcStaff(NcStaffEntry a, NcStaffEntry b)
        {
            if ((a.Staff == null) != (b.Staff == null)) return a.Staff == null ? 1 : -1;
            int compared = 0;
            if (a.Staff != null && b.Staff != null)
            {
                switch (_ncStaffSort)
                {
                    case NcStaffSort.Name:
                        compared = string.CompareOrdinal(a.Staff.Name ?? "", b.Staff.Name ?? "");
                        break;
                    case NcStaffSort.Age:
                        compared = a.Staff.Age.CompareTo(b.Staff.Age);
                        break;
                    case NcStaffSort.Level:
                        compared = a.Staff.Level.CompareTo(b.Staff.Level);
                        break;
                    case NcStaffSort.Salary:
                        compared = a.Staff.AnnualSalary.CompareTo(b.Staff.AnnualSalary);
                        break;
                }
            }

            if (compared == 0) compared = a.Order.CompareTo(b.Order);
            return _ncStaffAsc || _ncStaffSort == NcStaffSort.None ? compared : -compared;
        }

        void ToggleNcStaffSort(NcStaffSort sort)
        {
            if (_ncStaffSort == sort) _ncStaffAsc = !_ncStaffAsc;
            else
            {
                _ncStaffSort = sort;
                // 名前と役職は昇順、数値は高い順から。
                _ncStaffAsc = sort == NcStaffSort.Role || sort == NcStaffSort.Name;
            }

            _ncStaffScroll = 0f;
        }

        StaffMember SelectedNcStaff()
        {
            StaffMember first = null;
            for (int i = 0; i < _ncStaff.Count; i++)
            {
                var staff = _ncStaff[i].Staff;
                if (staff == null) continue;
                if (first == null) first = staff;
                if (staff.Id == _ncStaffSelectedId) return staff;
            }

            _ncStaffSelectedId = first != null ? first.Id : null;
            return first;
        }

        void DrawNcStaffList()
        {
            for (int i = 0; i < NcHeadX.Length; i++)
            {
                var sort = NcStaffHeadSort[i];
                string label = NcStaffHeadLabels[i];
                if (sort != NcStaffSort.None && _ncStaffSort == sort)
                    label += _ncStaffAsc ? "▲" : "▼";
                if (DrawNcHead(i, label, sort != NcStaffSort.None))
                    ToggleNcStaffSort(sort);
            }

            float content = NcContent(_ncStaff.Count);
            int clicked = UpdateNcScroll(content, _ncStaff.Count, ref _ncStaffScroll);
            if (clicked >= 0 && _ncStaff[clicked].Staff != null)
                _ncStaffSelectedId = _ncStaff[clicked].Staff.Id;

            if (_ncStaff.Count == 0)
            {
                GUI.Label(NcRows, "スタッフはいません。", _ncCell);
            }
            else
            {
                GUI.BeginGroup(NcRows);
                for (int i = 0; i < _ncStaff.Count; i++)
                {
                    float y = i * NcRowStride - _ncStaffScroll;
                    if (y + NcRowHeight < 0f || y > NcRows.height) continue;
                    DrawNcStaffRow(_ncStaff[i], y);
                }

                GUI.EndGroup();
            }

            DrawNcScrollbar(content, _ncStaffScroll);
        }

        void DrawNcStaffRow(NcStaffEntry entry, float y)
        {
            var staff = entry.Staff;
            if (staff == null)
            {
                NcFill(new Rect(0f, y, NcListWidth, NcRowHeight), NcGray5);
                NcFill(new Rect(0f, y, 100f, 100f), NcGray4);
                FitNcLabel(new Rect(0f, y, 100f, NcRowHeight), NcRoleCode(entry.Role), _ncCell, 36);
                GUI.Label(new Rect(177f, y, 288f, NcRowHeight), "（空き）", _ncCellLeft);
                return;
            }

            DrawNcRowBase(y, staff.Id == _ncStaffSelectedId, NcRoleCode(entry.Role), staff.Gender, staff.Name, staff.Age);
            FitNcLabel(new Rect(1208f - NcListX, y, 124f, NcRowHeight), staff.Level.ToString(CultureInfo.InvariantCulture), _ncCell, 36);
            FitNcLabel(new Rect(1340f - NcListX, y, 288f, NcRowHeight), MoneyText.Format(staff.AnnualSalary), _ncCellRight, 36);
        }

        /// <summary>POS の欄の略号。</summary>
        static string NcRoleCode(TeamStaffRole role)
        {
            switch (role)
            {
                case TeamStaffRole.Manager: return "MGR";
                case TeamStaffRole.HeadCoach: return "HC";
                case TeamStaffRole.Coach: return "CO";
                case TeamStaffRole.PhysicalCoach: return "PHY";
                case TeamStaffRole.Trainer: return "TR";
                default: return "DR";
            }
        }

        // ---- 詳細（選手とスタッフで共通の部分）

        /// <summary>1 行目（略号・名前・背番号）、2 行目（性別・誕生日・年齢）、顔、年俸。</summary>
        void DrawNcDetailCommon(string id, string pos, string name, string number, Gender gender, int age, Texture2D portrait, string salary)
        {
            NcFill(new Rect(NcDetailX, 355f, NcDetailWidth, 40f), NcGray4);
            NcPill(new Rect(NcPillX, 359f, 63f, 32f), pos);
            FitNcLabel(new Rect(320f, 355f, 168f, 40f), name ?? "", _ncSmall, 20);
            if (!string.IsNullOrEmpty(number))
                GUI.Label(new Rect(496f, 355f, 60f, 40f), number, _ncSmall);

            NcFill(new Rect(NcDetailX, 403f, NcDetailWidth, 40f), NcGray4);
            NcPill(new Rect(NcPillX, 407f, 63f, 32f), gender == Gender.Female ? "Female" : "Male");
            int month;
            int day;
            CharacterLife.Birthday(id, out month, out day);
            GUI.Label(new Rect(320f, 403f, 70f, 40f), "誕生日", _ncSmall);
            GUI.Label(new Rect(396f, 403f, 90f, 40f), month.ToString(CultureInfo.InvariantCulture) + "/" + day.ToString(CultureInfo.InvariantCulture), _ncSmall);
            GUI.Label(new Rect(496f, 403f, 80f, 40f), age.ToString(CultureInfo.InvariantCulture) + "歳", _ncSmall);

            // 顔（160 角に切り抜く）。
            NcFill(NcPortraitBox, NcGray4);
            if (portrait != null)
            {
                GUI.BeginGroup(NcPortraitBox);
                var sheet = new Rect(NcPortraitSheet.x - NcPortraitBox.x, NcPortraitSheet.y - NcPortraitBox.y, NcPortraitSheet.width, NcPortraitSheet.height);
                GUI.DrawTexture(sheet, portrait, ScaleMode.StretchToFill, true);
                GUI.EndGroup();
            }

            NcFill(new Rect(NcDetailX, 619f, NcDetailWidth, 40f), NcGray4);
            GUI.Label(new Rect(244f, 619f, 80f, 40f), "年俸", _ncSmall);
            FitNcLabel(new Rect(330f, 619f, NcRightX - 330f, 40f), salary, _ncSmallRight, 20);
        }

        // ---- 選手の詳細

        void DrawNcDetail(GameSession session, Club club, Player player)
        {
            NcFill(NcDetailPanel, NcGray5);
            if (player == null) return;

            DrawNcDetailCommon(
                player.Id,
                player.BestPosition().ToString(),
                player.Name,
                player.ShirtNumber > 0 ? player.ShirtNumber.ToString(CultureInfo.InvariantCulture) : null,
                player.Gender,
                player.Age,
                PortraitOf(player, club),
                MoneyText.Format(player.AnnualSalary));

            // 総合力と OFF・DEF・STA。
            NcFill(new Rect(NcBoxX, 451f, NcBoxWidth, 40f), NcGray4);
            GUI.Label(new Rect(NcBoxX, 451f, NcBoxWidth, 40f), player.DisplayOverall.ToString(CultureInfo.InvariantCulture), _ncOverall);
            NcStat(491f, "OFF", player.DisplayOffense, NcGray3);
            NcStat(531f, "DEF", player.DisplayDefense, NcGray4);
            NcStat(571f, "STA", player.DisplayStaminaMax, NcGray3);

            DrawNcInfo(session, player.Id, player.Children);
        }

        void NcStat(float y, string label, int value, Color color)
        {
            NcFill(new Rect(NcBoxX, y, NcBoxWidth, 40f), color);
            GUI.Label(new Rect(413f, y, 60f, 40f), label, _ncSmall);
            GUI.Label(new Rect(480f, y, NcRightX - 480f, 40f), value.ToString(CultureInfo.InvariantCulture), _ncSmallRight);
        }

        // ---- スタッフの詳細

        void DrawNcStaffDetail(GameSession session, Club club, StaffMember staff)
        {
            NcFill(NcDetailPanel, NcGray5);
            if (staff == null) return;

            DrawNcDetailCommon(
                staff.Id,
                NcRoleCode(staff.TeamRole),
                staff.Name,
                null,
                staff.Gender,
                staff.Age,
                NcStaffStand(staff, club),
                MoneyText.Format(staff.AnnualSalary));

            // LV と、役職ごとの 3 行（監督はシステム・タイプ・起用方針）。
            NcFill(new Rect(NcBoxX, 451f, NcBoxWidth, 40f), NcGray4);
            GUI.Label(new Rect(NcBoxX, 451f, NcBoxWidth, 40f), "LV " + staff.Level.ToString(CultureInfo.InvariantCulture), _ncOverall);
            string l1, v1, l2, v2, l3, v3;
            NcStaffTraits(staff, out l1, out v1, out l2, out v2, out l3, out v3);
            NcTrait(491f, l1, v1);
            NcTrait(531f, l2, v2);
            NcTrait(571f, l3, v3);

            DrawNcInfo(session, staff.Id, staff.Children);
        }

        /// <summary>左にラベル（97 幅）、右に値（96 幅）。</summary>
        void NcTrait(float y, string label, string value)
        {
            NcFill(new Rect(NcBoxX, y, 97f, 40f), NcGray3);
            NcFill(new Rect(NcBoxX + 97f, y, 96f, 40f), NcGray4);
            if (!string.IsNullOrEmpty(label))
                FitNcLabel(new Rect(413f, y, 84f, 40f), label, _ncSmall, 20);
            if (!string.IsNullOrEmpty(value))
                FitNcLabel(new Rect(508f, y, 84f, 40f), value, _ncSmall, 20);
        }

        static void NcStaffTraits(StaffMember staff, out string l1, out string v1, out string l2, out string v2, out string l3, out string v3)
        {
            l1 = v1 = l2 = v2 = l3 = v3 = null;
            switch (staff.TeamRole)
            {
                case TeamStaffRole.Manager:
                    l1 = "システム";
                    v1 = FormationCatalog.Get(staff.FavoriteFormation).DisplayName;
                    l2 = "タイプ";
                    v2 = NcStyleLabel(staff.FavoriteStyle);
                    l3 = "起用方針";
                    v3 = staff.LineupPriority == LineupPriority.Condition ? "調子重視" : "能力重視";
                    break;
                case TeamStaffRole.HeadCoach:
                    l1 = "担当";
                    v1 = "全員";
                    l2 = "練習";
                    v2 = "攻守・スタミナ";
                    break;
                case TeamStaffRole.Coach:
                    l1 = "担当";
                    v1 = NcGroupLabel(staff.ScoutedGroup);
                    l2 = "全員";
                    v2 = NcPercent(ClubMath.CoachBonusPercent(staff.Level, false));
                    l3 = "担当の選手";
                    v3 = NcPercent(ClubMath.CoachBonusPercent(staff.Level, true));
                    break;
                case TeamStaffRole.PhysicalCoach:
                    l1 = "担当";
                    v1 = "全員";
                    l2 = "練習";
                    v2 = "スタミナ";
                    break;
                case TeamStaffRole.Trainer:
                    l1 = "回復量";
                    v1 = staff.Level.ToString(CultureInfo.InvariantCulture);
                    l2 = "回復";
                    v2 = "練習・試合後";
                    break;
                default:
                    l1 = "担当";
                    v1 = "全員";
                    l2 = "効果";
                    v2 = "怪我を減らす";
                    break;
            }
        }

        static string NcStyleLabel(PlayingStyle style)
        {
            switch (style)
            {
                case PlayingStyle.Possession: return "ポゼッション";
                case PlayingStyle.Counter: return "カウンター";
                case PlayingStyle.HighPress: return "ハイプレス";
                case PlayingStyle.Direct: return "ダイレクト";
                default: return "バランス";
            }
        }

        static string NcGroupLabel(PositionGroup group)
        {
            switch (group)
            {
                case PositionGroup.GK: return "GK";
                case PositionGroup.DF: return "DF";
                case PositionGroup.MF: return "MF";
                default: return "FW";
            }
        }

        static string NcPercent(double percent)
        {
            return "+" + percent.ToString("0.0", CultureInfo.InvariantCulture) + "%";
        }

        /// <summary>スタッフの立ち絵（選手と同じ切り抜き）。服は役職どおり（チームスタッフはユニフォーム）。</summary>
        Texture2D NcStaffStand(StaffMember staff, Club club)
        {
            if (staff == null || club == null) return null;
            const int StandGroup = -3;
            if (_portrait != null && _portraitId == staff.Id && _portraitEmblem == club.EmblemIndex && _portraitGroup == StandGroup)
                return _portrait;
            ReleasePortrait();
            if (!_avatars.Ready) _avatars.Load();
            if (!_avatars.Ready) return null;
            var stand = _avatars.CreateStand(staff.Id, AvatarLibrary.Kit(club.EmblemIndex), AvatarLibrary.NaturalHair(staff.Id), 0, AvatarLibrary.OutfitOf(staff), gender: staff.Gender);
            if (stand == null) return null;
            _portrait = stand;
            _portraitId = staff.Id;
            _portraitEmblem = club.EmblemIndex;
            _portraitGroup = StandGroup;
            return _portrait;
        }

        // ---- 関係とパーソナルデータ

        /// <summary>関係（夫婦・恋人・親友・友達）と、パーソナルデータ（趣味・好きな食べ物・苦手な食べ物・子ども）。多いときはスクロールする。</summary>
        void DrawNcInfo(GameSession session, string id, int childCount, float top = 667f, float bottom = 899f)
        {
            var area = new Rect(NcInfoArea.x, top, NcInfoArea.width, bottom - top);
            var track = new Rect(NcInfoTrack.x, top, NcInfoTrack.width, bottom - top);
            string key = id + "|" + (session != null ? session.Time.CurrentDate.Ticks : 0L);
            if (_ncInfoKey != key)
            {
                _ncInfoKey = key;
                _ncInfoScroll = 0f;
                BuildNcRelations(session, id);
            }

            string hobby = CharacterLife.Hobby(id);
            if (CharacterLife.IsMovie(hobby)) hobby = "映画（" + CharacterLife.Movie(id) + "）";
            bool children = childCount > 0;
            int rows = _ncRelations.Count + 3 + (children ? 1 : 0);
            float content = rows * NcInfoStride - (NcInfoStride - 40f);
            float maxScroll = Mathf.Max(0f, content - area.height);
            UpdateNcInfoScroll(maxScroll, area, track);

            GUI.BeginGroup(area);
            float y = -_ncInfoScroll;
            for (int i = 0; i < _ncRelations.Count; i++, y += NcInfoStride)
            {
                NcFill(new Rect(0f, y, NcDetailWidth, 40f), NcGray4);
                NcPill(new Rect(5f, y + 4f, 63f, 32f), _ncRelations[i].Key);
                FitNcLabel(new Rect(84f, y, 260f, 40f), _ncRelations[i].Value, _ncSmall, 20);
            }

            NcPersonal(y, "趣味", hobby);
            y += NcInfoStride;
            NcPersonal(y, "好きな食べ物", CharacterLife.FavoriteFood(id));
            y += NcInfoStride;
            NcPersonal(y, "苦手な食べ物", CharacterLife.DislikedFood(id));
            if (children)
            {
                y += NcInfoStride;
                NcPersonal(y, "子ども", childCount.ToString(CultureInfo.InvariantCulture) + "人");
            }

            GUI.EndGroup();

            if (maxScroll > 0.5f)
            {
                NcFill(track, NcGray);
                float thumbH = Mathf.Clamp(track.height * area.height / content, 32f, track.height);
                float span = Mathf.Max(1f, track.height - thumbH);
                NcFill(new Rect(track.x, track.y + span * (_ncInfoScroll / maxScroll), track.width, thumbH), NcGray4);
            }
        }

        void NcPersonal(float y, string label, string value)
        {
            NcFill(new Rect(0f, y, NcDetailWidth, 40f), NcGray4);
            NcFill(new Rect(5f, y + 4f, 139f, 32f), NcGray2);
            GUI.Label(new Rect(5f, y + 4f, 139f, 32f), label, _ncSmallCenter);
            FitNcLabel(new Rect(152f, y, 200f, 40f), value ?? "", _ncSmall, 20);
        }

        void UpdateNcInfoScroll(float maxScroll, Rect infoArea, Rect infoTrack)
        {
            _ncInfoScroll = Mathf.Clamp(_ncInfoScroll, 0f, maxScroll);
            var e = Event.current;
            if (e == null) return;
            var area = new Rect(infoArea.x, infoArea.y, infoTrack.xMax - infoArea.x, infoArea.height);
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
        void BuildNcRelations(GameSession session, string id)
        {
            _ncRelations.Clear();
            var relations = session != null && session.Relations != null ? session.Relations.For(id) : null;
            if (relations == null || relations.Count == 0) return;
            var people = ProfilePeople(session);
            var sorted = new List<Relationship>(relations);
            sorted.Sort((a, b) => NcStageOrder(a.Stage).CompareTo(NcStageOrder(b.Stage)));
            for (int i = 0; i < sorted.Count; i++)
            {
                var rel = sorted[i];
                if (NcStageOrder(rel.Stage) >= 4) continue;
                string otherId = rel.Other(id);
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
