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
    /// 新しいクラブハウス（Figma の main_clubhouse_design_player / main_clubhouse_design_front）。
    /// 選手・スタッフ・フロントの 3 タブを、茶色とクリーム色のデザイン（角丸、ステッチ、看板の見出し）で描く。
    /// 左が選んだ人の詳細（詳細枠 192〜760）、右が一覧（776〜1700）。文字は 24（見出し 40、✕ 64）。
    /// フロントは ClubDialogView.NewFront.cs。
    /// </summary>
    public sealed partial class ClubDialogView
    {
        // ---- 枠（dialog 205:2）。
        static readonly Rect NcDialog = new Rect(160f, 158f, 1600f, 800f);
        static readonly Rect NcStitch = new Rect(176f, 174f, 1568f, 768f);
        static readonly Rect NcTitlePlank = new Rect(750f, 136f, 420f, 76f);
        static readonly Rect NcClose = new Rect(1628f, 190f, 100f, 100f);
        const float NcTabY = 265f;
        const float NcTabWidth = 240f;
        const float NcTabHeight = 56f;
        const float NcTabStep = 248f;
        // Figma のタブは 776 から 3 つ（一覧の左端にそろえる）。強化タブはなくした（施設は事業部の提案に任せる）。
        const float NcTabX = 776f;
        static readonly string[] NcTabLabels = { "選手", "スタッフ", "フロント" };
        static readonly ClubPage[] NcTabPages = { ClubPage.Players, ClubPage.Staff, ClubPage.Front };

        // ---- 一覧。見出し 776,329 / 高さ 56、行 100（間 8）、見える範囲 393〜925。
        const float NcListX = 776f;
        const float NcListWidth = 924f;
        const float NcHeadY = 329f;
        const float NcHeadHeight = 56f;
        static readonly Rect NcRows = new Rect(776f, 393f, 924f, 532f);
        const float NcRowHeight = 100f;
        const float NcRowStride = 108f;
        static readonly Rect NcScrollTrack = new Rect(1708f, 393f, 10f, 532f);

        // 見出しの列（選手とスタッフで同じ位置）。
        static readonly float[] NcHeadX = { 776f, 876f, 948f, 1204f, 1288f, 1412f, 1636f };
        static readonly float[] NcHeadW = { 100f, 72f, 256f, 84f, 124f, 224f, 64f };
        // 行の中の位置（一覧の左端を 0 にした座標）。
        const float NcRowName = 177f;
        const float NcRowNameWidth = 240f;
        const float NcRowAge = 428f;
        const float NcRowParam = 512f;
        const float NcRowSalaryRight = 850f;
        const float NcRowCondition = 868f;
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

        // ---- 詳細（詳細枠 192,329 / 568×596、中身は 236〜717）。
        static readonly Rect NcDetailPanel = new Rect(192f, 329f, 568f, 596f);
        const float NcDetailX = 236f;
        const float NcDetailWidth = 481f;
        const float NcPillX = 241f;
        // 右寄せの値の右端。
        const float NcRightX = 709f;
        static readonly Rect NcPortraitBox = new Rect(236f, 451f, 160f, 160f);
        // character3（244 角を 195,451 に置く）。シートは 2048 角の 212,46 / 1622×1998 を 244/2048 倍。
        const float NcPortraitScale = 244f / 2048f;
        static readonly Rect NcPortraitSheet = new Rect(
            195f + 212f * NcPortraitScale, 451f + 46f * NcPortraitScale, 1622f * NcPortraitScale, 1998f * NcPortraitScale);
        // 顔の右の箱（404〜717）。特徴の行は、左にラベル（120）、右に値（193）。
        const float NcBoxX = 404f;
        const float NcBoxWidth = 313f;
        const float NcTraitLabelWidth = 120f;
        // 関係とパーソナルデータ（667〜899）は、多いときにスクロールする。
        static readonly Rect NcInfoArea = new Rect(236f, 667f, 481f, 232f);
        static readonly Rect NcInfoTrack = new Rect(725f, 667f, 10f, 232f);
        const float NcInfoStride = 48f;
        const float NcPersonalPillWidth = 160f;

        // ---- 色（Figma の main_clubhouse_design_*）。
        static readonly Color NcCream = Hex(0xFFF8E7);        // ダイアログの地、見出しの文字
        static readonly Color NcDarkBrown = Hex(0x4A2E14);    // ダイアログ・看板・閉じる・選択タブの線
        static readonly Color NcStitchColor = Hex(0xD9C08E);  // ステッチ（破線）
        static readonly Color NcPanel = Hex(0xF3E6C8);        // 詳細枠
        static readonly Color NcPanelLine = Hex(0xC9A66B);    // 詳細枠・タブ・顔の線
        static readonly Color NcRowFill = Hex(0xFBF3DF);      // 詳細の行
        static readonly Color NcRowLine = Hex(0xE2CFA5);      // 行の線、スクロールの溝
        static readonly Color NcBrown = Hex(0x8B5A2B);        // 札、スタッフ・フロントの POS、スクロールのつまみ
        static readonly Color NcHeadBrown = Hex(0x6B4423);    // 一覧の見出し
        static readonly Color NcText = Hex(0x3B2A1A);         // 本文
        static readonly Color NcYellow = Hex(0xF5C542);       // 選択中、LV の箱、主ボタン
        static readonly Color NcListRow = Hex(0xFFFDF6);      // 一覧の行
        static readonly Color NcTabOff = Hex(0xF6EBD0);
        static readonly Color NcPlayerPos = Hex(0x4F86C6);    // 選手の POS
        static readonly Color NcMaleFill = Hex(0xD6E8FA);
        static readonly Color NcMaleText = Hex(0x2F5E9E);
        // Figma は M だけ。F は同じ形で桃色にする。
        static readonly Color NcFemaleFill = Hex(0xFADDE6);
        static readonly Color NcFemaleText = Hex(0xB03A68);
        static readonly Color NcDangerFill = Hex(0xF1D9C9);   // 解雇ボタン
        static readonly Color NcDangerLine = Hex(0xB5654A);
        static readonly Color NcDangerText = Hex(0x9A3F2A);
        static readonly Color NcShadowColor = new Color(0x2E / 255f, 0x1C / 255f, 0x0A / 255f, 1f);

        static Texture2D _ncPlankTexture;
        static Texture2D _ncPortraitTexture;

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
        GUIStyle _ncHeadText;
        GUIStyle _ncPosText;
        GUIStyle _ncPillText;
        GUIStyle _ncBold;
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
            DrawNcFrame();

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
                if (on) NcBox(tab, NcYellow, NcDarkBrown, 4f, 28f);
                else NcBox(tab, NcTabOff, NcPanelLine, 3f, 28f);
                NcLabel(tab, NcTabLabels[i], _ncBold, on ? NcDarkBrown : NcHeadBrown);
                if (!on && GUI.Button(tab, GUIContent.none, _hit))
                    SetPage(NcTabPages[i]);
            }

            NcRound(new Rect(NcClose.x, NcClose.y + 4f, NcClose.width, NcClose.height), NcShade(0.35f), 50f);
            NcBox(NcClose, NcYellow, NcDarkBrown, 5f, 50f);
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
            DrawNcRowBase(y, player.Id == _selectedPlayerId, player.BestPosition().ToString(), NcPlayerPos, player.Gender, player.Name, player.Age);
            FitNcLabel(new Rect(NcRowParam, y, NcHeadW[4], NcRowHeight), player.DisplayOverall.ToString(CultureInfo.InvariantCulture), _ncCell, 24);
            FitNcLabel(NcSalaryCell(y), MoneyText.Format(player.AnnualSalary), _ncCellRight, 24);
            DrawTex(new Rect(NcRowCondition, y + 26f, 48f, 49f), ConditionIcon(player));
        }

        /// <summary>年俸の欄（右端 850 にそろえる）。</summary>
        static Rect NcSalaryCell(float y)
        {
            return new Rect(NcRowSalaryRight - 210f, y, 210f, NcRowHeight);
        }

        /// <summary>行の地（選択中は白地に黄色の線）。左に POS の札。</summary>
        void DrawNcRowFrame(float y, bool selected, Color posColor, string pos)
        {
            var row = new Rect(0f, y, NcListWidth, NcRowHeight);
            if (selected) NcRound(row, Color.white, 16f);
            else NcRound(row, NcListRow, 16f);
            NcRound(new Rect(0f, y, 100f, NcRowHeight), posColor, new Vector4(16f, 0f, 0f, 16f));
            if (selected) NcOutline(row, NcYellow, 4f, 16f);
            else NcOutline(row, NcRowLine, 2f, 16f);
            FitNcLabel(new Rect(0f, y, 100f, NcRowHeight), pos, _ncPosText, 24);
        }

        /// <summary>行の地、POS、M/F、名前、年齢（選手・スタッフ・フロントで共通）。</summary>
        void DrawNcRowBase(float y, bool selected, string pos, Color posColor, Gender gender, string name, int age)
        {
            DrawNcRowFrame(y, selected, posColor, pos);
            bool female = gender == Gender.Female;
            var mf = new Rect(112f, y + 10f, 48f, 80f);
            NcRound(mf, female ? NcFemaleFill : NcMaleFill, 12f);
            NcLabel(mf, female ? "F" : "M", _ncPosText, female ? NcFemaleText : NcMaleText);
            FitNcLabel(new Rect(NcRowName, y, NcRowNameWidth, NcRowHeight), name ?? "", _ncCellLeft, 24);
            GUI.Label(new Rect(NcRowAge, y, NcHeadW[3], NcRowHeight), age.ToString(CultureInfo.InvariantCulture), _ncCell);
        }

        /// <summary>空き枠の行。</summary>
        void DrawNcEmptyRow(float y, Color posColor, string pos, string text)
        {
            DrawNcRowFrame(y, false, posColor, pos);
            NcLabel(new Rect(NcRowName, y, 600f, NcRowHeight), text, _ncCellLeft, new Color(NcText.r, NcText.g, NcText.b, 0.55f));
        }

        /// <summary>見出し 1 列。並べ替えできる列なら、押されたら true。地は列ごとでなく DrawNcHeadBar でまとめて描く。</summary>
        bool DrawNcHead(int column, string label, bool sortable)
        {
            if (column == 0) DrawNcHeadBar();
            var cell = new Rect(NcHeadX[column], NcHeadY, NcHeadW[column], NcHeadHeight);
            FitNcLabel(cell, label, _ncHeadText, 24);
            return sortable && GUI.Button(cell, GUIContent.none, _hit);
        }

        static void DrawNcHeadBar()
        {
            NcRound(new Rect(NcListX, NcHeadY, NcListWidth, NcHeadHeight), NcHeadBrown, 16f);
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
            NcRound(track, NcRowLine, 5f);
            float maxScroll = Mathf.Max(0f, content - NcRows.height);
            float thumbH = NcThumbHeight(content);
            float span = Mathf.Max(1f, track.height - thumbH);
            float t = maxScroll <= 0.5f ? 0f : scroll / maxScroll;
            NcRound(new Rect(track.x, track.y + span * t, track.width, thumbH), NcBrown, 5f);
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
                DrawNcEmptyRow(y, NcBrown, NcRoleCode(entry.Role), "（空き）");
                return;
            }

            DrawNcRowBase(y, staff.Id == _ncStaffSelectedId, NcRoleCode(entry.Role), NcBrown, staff.Gender, staff.Name, staff.Age);
            FitNcLabel(new Rect(NcRowParam, y, NcHeadW[4], NcRowHeight), staff.Level.ToString(CultureInfo.InvariantCulture), _ncCell, 24);
            FitNcLabel(NcSalaryCell(y), MoneyText.Format(staff.AnnualSalary), _ncCellRight, 24);
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
            // 顔（160 角、角丸 20）。先に描いて、はみ出した角を詳細枠の色で消す。
            NcRound(NcPortraitBox, Color.white, 20f);
            GUI.DrawTexture(NcPortraitBox, NcPortraitBackground(), ScaleMode.StretchToFill, true, 0f, Color.white, 0f, 20f);
            if (portrait != null)
            {
                GUI.BeginGroup(NcPortraitBox);
                var sheet = new Rect(NcPortraitSheet.x - NcPortraitBox.x, NcPortraitSheet.y - NcPortraitBox.y, NcPortraitSheet.width, NcPortraitSheet.height);
                GUI.DrawTexture(sheet, portrait, ScaleMode.StretchToFill, true);
                GUI.EndGroup();
            }

            NcOutline(new Rect(NcPortraitBox.x - 20f, NcPortraitBox.y - 20f, NcPortraitBox.width + 40f, NcPortraitBox.height + 40f), NcPanel, 20f, 40f);
            NcOutline(NcPortraitBox, NcPanelLine, 3f, 20f);

            NcRowBox(new Rect(NcDetailX, 355f, NcDetailWidth, 40f));
            NcPill(new Rect(NcPillX, 359f, 63f, 32f), pos);
            FitNcLabel(new Rect(320f, 355f, 300f, 40f), name ?? "", _ncSmall, 24);
            if (!string.IsNullOrEmpty(number))
                GUI.Label(new Rect(NcRightX - 80f, 355f, 80f, 40f), number, _ncSmallRight);

            NcRowBox(new Rect(NcDetailX, 403f, NcDetailWidth, 40f));
            NcPill(new Rect(NcPillX, 407f, 63f, 32f), gender == Gender.Female ? "Female" : "Male");
            int month;
            int day;
            CharacterLife.Birthday(id, out month, out day);
            GUI.Label(new Rect(320f, 403f, 76f, 40f), "誕生日", _ncSmall);
            GUI.Label(new Rect(396f, 403f, 90f, 40f), month.ToString(CultureInfo.InvariantCulture) + "/" + day.ToString(CultureInfo.InvariantCulture), _ncSmall);
            GUI.Label(new Rect(NcRightX - 100f, 403f, 100f, 40f), age.ToString(CultureInfo.InvariantCulture) + "歳", _ncSmallRight);

            NcRowBox(new Rect(NcDetailX, 619f, NcDetailWidth, 40f));
            GUI.Label(new Rect(244f, 619f, 80f, 40f), "年俸", _ncSmall);
            FitNcLabel(new Rect(330f, 619f, NcRightX - 330f, 40f), salary, _ncSmallRight, 24);
        }

        /// <summary>詳細枠（角丸 24、線 3）。</summary>
        static void DrawNcDetailPanel()
        {
            NcBox(NcDetailPanel, NcPanel, NcPanelLine, 3f, 24f);
        }

        /// <summary>顔の右の一番上の箱（黄色）。</summary>
        void NcTopBox(string text)
        {
            var box = new Rect(NcBoxX, 451f, NcBoxWidth, 40f);
            NcBox(box, NcYellow, NcHeadBrown, 2f, 12f);
            GUI.Label(box, text, _ncOverall);
        }

        // ---- 選手の詳細

        void DrawNcDetail(GameSession session, Club club, Player player)
        {
            DrawNcDetailPanel();
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
            NcTopBox(player.DisplayOverall.ToString(CultureInfo.InvariantCulture));
            NcStat(491f, "OFF", player.DisplayOffense);
            NcStat(531f, "DEF", player.DisplayDefense);
            NcStat(571f, "STA", player.DisplayStaminaMax);

            DrawNcInfo(session, player.Id, player.Children);
        }

        void NcStat(float y, string label, int value)
        {
            NcRowBox(new Rect(NcBoxX, y, NcBoxWidth, 40f));
            GUI.Label(new Rect(413f, y, 60f, 40f), label, _ncSmall);
            GUI.Label(new Rect(480f, y, NcRightX - 480f, 40f), value.ToString(CultureInfo.InvariantCulture), _ncSmallRight);
        }

        // ---- スタッフの詳細

        void DrawNcStaffDetail(GameSession session, Club club, StaffMember staff)
        {
            DrawNcDetailPanel();
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
            NcTopBox("LV " + staff.Level.ToString(CultureInfo.InvariantCulture));
            string l1, v1, l2, v2, l3, v3;
            NcStaffTraits(staff, out l1, out v1, out l2, out v2, out l3, out v3);
            NcTrait(491f, l1, v1);
            NcTrait(531f, l2, v2);
            NcTrait(571f, l3, v3);

            DrawNcInfo(session, staff.Id, staff.Children);
        }

        /// <summary>左に茶色のラベル（120 幅）、右に値（193 幅）。</summary>
        void NcTrait(float y, string label, string value)
        {
            float valueX = NcBoxX + NcTraitLabelWidth;
            NcRowBox(new Rect(valueX, y, NcBoxWidth - NcTraitLabelWidth, 40f));
            NcRound(new Rect(NcBoxX, y, NcTraitLabelWidth, 40f), NcBrown, 16f);
            if (!string.IsNullOrEmpty(label))
                FitNcLabel(new Rect(413f, y, NcTraitLabelWidth - 14f, 40f), label, _ncPillText, 24);
            if (!string.IsNullOrEmpty(value))
                FitNcLabel(new Rect(valueX + 7f, y, NcBoxWidth - NcTraitLabelWidth - 14f, 40f), value, _ncSmall, 24);
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
                NcRowBox(new Rect(0f, y, NcDetailWidth, 40f));
                NcPill(new Rect(5f, y + 4f, 63f, 32f), _ncRelations[i].Key);
                FitNcLabel(new Rect(84f, y, NcDetailWidth - 96f, 40f), _ncRelations[i].Value, _ncSmall, 24);
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
                NcRound(track, NcRowLine, 5f);
                float thumbH = Mathf.Clamp(track.height * area.height / content, 32f, track.height);
                float span = Mathf.Max(1f, track.height - thumbH);
                NcRound(new Rect(track.x, track.y + span * (_ncInfoScroll / maxScroll), track.width, thumbH), NcBrown, 5f);
            }
        }

        void NcPersonal(float y, string label, string value)
        {
            NcRowBox(new Rect(0f, y, NcDetailWidth, 40f));
            NcPill(new Rect(5f, y + 4f, NcPersonalPillWidth, 32f), label);
            FitNcLabel(new Rect(NcPersonalPillWidth + 13f, y, NcDetailWidth - NcPersonalPillWidth - 25f, 40f), value ?? "", _ncSmall, 24);
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

        /// <summary>茶色の札（角丸 16）に白い太字。</summary>
        void NcPill(Rect rect, string text)
        {
            NcRound(rect, NcBrown, 16f);
            FitNcLabel(rect, text, _ncPillText, 24);
        }

        /// <summary>詳細の行の箱（クリーム地、線 2、角丸 12）。</summary>
        static void NcRowBox(Rect rect)
        {
            NcBox(rect, NcRowFill, NcRowLine, 2f, 12f);
        }

        /// <summary>枠：影、ダイアログ、ステッチ、看板の見出し。</summary>
        void DrawNcFrame()
        {
            NcRound(new Rect(NcDialog.x, NcDialog.y + 10f, NcDialog.width, NcDialog.height), NcShade(0.3f), 36f);
            NcBox(NcDialog, NcCream, NcDarkBrown, 6f, 36f);
            NcDashedRect(NcStitch, NcStitchColor, 3f, 24f, 14f, 10f);

            NcRound(new Rect(NcTitlePlank.x, NcTitlePlank.y + 5f, NcTitlePlank.width, NcTitlePlank.height), NcShade(0.4f), 38f);
            GUI.DrawTexture(NcTitlePlank, NcPlankTexture(), ScaleMode.StretchToFill, true, 0f, Color.white, 0f, 38f);
            NcOutline(NcTitlePlank, NcDarkBrown, 5f, 38f);
            GUI.Label(NcTitlePlank, "クラブハウス", _ncTitle);
        }

        static Color NcShade(float alpha)
        {
            return new Color(NcShadowColor.r, NcShadowColor.g, NcShadowColor.b, alpha);
        }

        static void NcRound(Rect rect, Color color, float radius)
        {
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, color, 0f, radius);
        }

        /// <summary>角ごとの丸み（左上、右上、右下、左下）。</summary>
        static void NcRound(Rect rect, Color color, Vector4 radii)
        {
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, color, Vector4.zero, radii);
        }

        /// <summary>内側に線だけを描く。</summary>
        static void NcOutline(Rect rect, Color color, float width, float radius)
        {
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, color, width, radius);
        }

        static void NcBox(Rect rect, Color fill, Color line, float width, float radius)
        {
            NcRound(rect, fill, radius);
            if (width > 0f) NcOutline(rect, line, width, radius);
        }

        /// <summary>角丸の破線（ステッチ）。辺は破線、角は円弧のまま。</summary>
        static void NcDashedRect(Rect rect, Color color, float width, float radius, float dash, float gap)
        {
            NcDashLine(rect.x + radius, rect.xMax - radius, rect.y, true, color, width, dash, gap);
            NcDashLine(rect.x + radius, rect.xMax - radius, rect.yMax - width, true, color, width, dash, gap);
            NcDashLine(rect.y + radius, rect.yMax - radius, rect.x, false, color, width, dash, gap);
            NcDashLine(rect.y + radius, rect.yMax - radius, rect.xMax - width, false, color, width, dash, gap);
            var size = radius * 2f;
            NcCorner(new Rect(rect.x, rect.y, radius, radius), new Vector2(0f, 0f), size, color, width, radius);
            NcCorner(new Rect(rect.xMax - radius, rect.y, radius, radius), new Vector2(-radius, 0f), size, color, width, radius);
            NcCorner(new Rect(rect.x, rect.yMax - radius, radius, radius), new Vector2(0f, -radius), size, color, width, radius);
            NcCorner(new Rect(rect.xMax - radius, rect.yMax - radius, radius, radius), new Vector2(-radius, -radius), size, color, width, radius);
        }

        static void NcDashLine(float from, float to, float at, bool horizontal, Color color, float width, float dash, float gap)
        {
            for (float p = from; p < to; p += dash + gap)
            {
                float len = Mathf.Min(dash, to - p);
                var r = horizontal ? new Rect(p, at, len, width) : new Rect(at, p, width, len);
                GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, color, 0f, 0f);
            }
        }

        static void NcCorner(Rect clip, Vector2 offset, float size, Color color, float width, float radius)
        {
            GUI.BeginGroup(clip);
            NcOutline(new Rect(offset.x, offset.y, size, size), color, width, radius);
            GUI.EndGroup();
        }

        /// <summary>看板の縦グラデーション（上 #A8703A → 下 #7A4A22）。</summary>
        static Texture2D NcPlankTexture()
        {
            if (_ncPlankTexture == null)
                _ncPlankTexture = NcGradient(new[] { Hex(0xA8703A), Hex(0x7A4A22) }, new[] { 0f, 1f });
            return _ncPlankTexture;
        }

        /// <summary>顔の背景の縦グラデーション（空色 → 薄緑 → 緑）。</summary>
        static Texture2D NcPortraitBackground()
        {
            if (_ncPortraitTexture == null)
                _ncPortraitTexture = NcGradient(new[] { Hex(0xBFE3F2), Hex(0xE8F5E0), Hex(0xA9D18E) }, new[] { 0f, 0.7f, 1f });
            return _ncPortraitTexture;
        }

        /// <summary>上から下へのグラデーション。Unity のテクスチャは下が 0 なので逆に詰める。</summary>
        static Texture2D NcGradient(Color[] colors, float[] stops)
        {
            const int Height = 64;
            var texture = new Texture2D(1, Height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            for (int i = 0; i < Height; i++)
            {
                float t = i / (Height - 1f);
                Color c = colors[colors.Length - 1];
                for (int s = 0; s < stops.Length - 1; s++)
                {
                    if (t > stops[s + 1]) continue;
                    c = Color.Lerp(colors[s], colors[s + 1], Mathf.InverseLerp(stops[s], stops[s + 1], t));
                    break;
                }

                texture.SetPixel(0, Height - 1 - i, c);
            }

            texture.Apply();
            return texture;
        }

        /// <summary>文字色を変えて 1 回だけ描く。</summary>
        static void NcLabel(Rect rect, string text, GUIStyle style, Color color)
        {
            var previous = style.normal.textColor;
            style.normal.textColor = color;
            GUI.Label(rect, text, style);
            style.normal.textColor = previous;
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
                normal = { textColor = NcText }
            };
            // Figma は Zen Maru Gothic（Medium / Bold / Black）。手元のフォントで、Bold・Black は太字にする。
            var bold = new GUIStyle(label) { fontSize = 24, fontStyle = FontStyle.Bold };
            _ncTitle = new GUIStyle(bold) { fontSize = 40, normal = { textColor = NcCream } };
            _ncClose = new GUIStyle(bold) { fontSize = 64, normal = { textColor = NcDarkBrown } };
            _ncCell = new GUIStyle(bold);
            _ncCellLeft = new GUIStyle(bold) { alignment = TextAnchor.MiddleLeft };
            _ncCellRight = new GUIStyle(bold) { alignment = TextAnchor.MiddleRight };
            _ncBold = new GUIStyle(bold);
            _ncHeadText = new GUIStyle(bold) { normal = { textColor = NcCream } };
            _ncPosText = new GUIStyle(bold) { normal = { textColor = Color.white } };
            _ncPillText = new GUIStyle(bold) { normal = { textColor = NcCream } };
            _ncSmall = new GUIStyle(label) { fontSize = 24, alignment = TextAnchor.MiddleLeft };
            _ncSmallCenter = new GUIStyle(label) { fontSize = 24 };
            _ncSmallRight = new GUIStyle(label) { fontSize = 24, alignment = TextAnchor.MiddleRight };
            _ncOverall = new GUIStyle(label) { fontSize = 24 };
        }
    }
}
