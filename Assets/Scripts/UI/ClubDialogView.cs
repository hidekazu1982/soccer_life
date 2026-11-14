using System;
using System.Collections.Generic;
using System.Globalization;
using BigInteger = System.Numerics.BigInteger;
using SoccerClub.Competition;
using SoccerClub.Core;
using SoccerClub.Data;
using SoccerClub.Personnel;
using SoccerClub.Time;
using UnityEngine;

namespace SoccerClub.UI
{
    /// <summary>
    /// スタジアム、練習場、クラブハウス、MENU の詳細ダイアログ。
    /// </summary>
    public sealed class ClubDialogView
    {
        public enum Kind
        {
            None,
            Menu,
            Stadium,
            Training,
            Clubhouse
        }

        enum ClubPage
        {
            Strengthen,
            Players,
            Staff,
            Front
        }

        enum StadiumPage
        {
            Schedule,
            Results,
            Table,
            Goals,
            Ratings
        }

        public static readonly Rect Bounds = new Rect(240f, 188f, 1420f, 690f);

        // Figma main / dialog_01（1920×1080）。閉じるボタンは枠の右上にはみ出す。
        static readonly Rect ClubhouseDialogRect = new Rect(250f, 150f, 1421f, 781f);
        static readonly Rect ClubhouseHitBounds = new Rect(250f, 134f, 1437f, 797f);
        static readonly Rect ClubhouseFrameRect = new Rect(333f, 345f, 1255f, 562f);
        static readonly Rect ClubhouseTitleRect = new Rect(821f, 174f, 262f, 53f);
        static readonly Rect ClubhouseCloseRect = new Rect(1564f, 134f, 123f, 125f);
        static readonly Rect ClubhousePageRect = new Rect(356f, 372f, 1210f, 500f);
        static readonly float[] ClubhouseTabX = { 342f, 548f, 754f, 960f };
        const float ClubhouseTabWidth = 202f;
        const float ClubhouseTabOnY = 265f;
        const float ClubhouseTabOnHeight = 99f;
        const float ClubhouseTabOffY = 272f;
        const float ClubhouseTabOffHeight = 90f;
        static readonly string[] ClubhouseTabLabels = { "強化", "選手", "スタッフ", "フロント" };
        static readonly Rect ClubhouseCardLeft = new Rect(360f, 384f, 592f, 500f);
        static readonly Rect ClubhouseCardRight = new Rect(968f, 384f, 592f, 500f);
        static readonly Rect ClubhouseButtonLeft = new Rect(536f, 699f, 240f, 93f);
        static readonly Rect ClubhouseButtonRight = new Rect(1147f, 699f, 240f, 93f);
        static readonly Rect PlayerAvatarRect = new Rect(355f, 382f, 415f, 511f);
        static readonly Rect PlayerListRect = new Rect(786f, 380f, 786f, 511f);
        static readonly Rect PlayerHeaderRect = new Rect(802f, 388f, 734f, 45f);
        static readonly Rect PlayerRowsRect = new Rect(802f, 445f, 734f, 432f);
        static readonly Rect PlayerScrollTrackRect = new Rect(1544f, 441f, 20f, 442f);
        static readonly Rect PlayerPortraitRect = new Rect(475f, 405f, 177f, 240f);
        static readonly Rect RadarTexRect = new Rect(463f, 708f, 200f, 176f);
        static readonly Rect StaIconRect = new Rect(532f, 661f, 63f, 26f);
        static readonly Rect DefIconRect = new Rect(415f, 825f, 63f, 26f);
        static readonly Rect OffIconRect = new Rect(647f, 825f, 63f, 26f);
        static readonly Rect StaValueRect = new Rect(513f, 684f, 100f, 29f);
        static readonly Rect DefValueRect = new Rect(396f, 851f, 100f, 29f);
        static readonly Rect OffValueRect = new Rect(628f, 851f, 100f, 29f);
        const float PlayerRowHeight = 80f;
        const float PlayerRowGap = 8f;
        const float PlayerRowStride = PlayerRowHeight + PlayerRowGap;
        static readonly Rect FrontListRect = new Rect(777f, 388f, 786f, 511f);
        static readonly Rect FrontAvatarRect = new Rect(346f, 390f, 415f, 511f);
        static readonly Rect FrontHeaderRect = new Rect(793f, 396f, 734f, 45f);
        static readonly Rect FrontRowsRect = new Rect(793f, 453f, 734f, 432f);
        static readonly Rect FrontScrollTrackRect = new Rect(1535f, 449f, 20f, 442f);
        static readonly Rect FrontRoleBar = new Rect(373f, 403f, 360f, 32f);
        static readonly Rect FrontLevelPill = new Rect(641f, 403f, 92f, 32f);
        static readonly Rect FrontNameBar = new Rect(373f, 441f, 360f, 32f);
        static readonly Rect FrontAgePill = new Rect(641f, 441f, 92f, 32f);
        static readonly Rect FrontSalaryBar = new Rect(373f, 479f, 360f, 32f);
        static readonly Rect FrontSalaryPill = new Rect(373f, 479f, 92f, 32f);
        static readonly Rect FrontPortraitCircle = new Rect(473f, 519f, 160f, 160f);
        static readonly Rect FrontFeeBar = new Rect(373f, 780f, 360f, 32f);
        static readonly Rect FrontFeePill = new Rect(373f, 780f, 92f, 32f);
        static readonly Rect FrontContractButton = new Rect(493f, 828f, 120f, 46f);
        static readonly Rect FrontBackButton = new Rect(1435f, 396f, 120f, 46f);
        const float FrontRowHeight = 80f;
        const float FrontRowGap = 8f;
        const float FrontRowStride = FrontRowHeight + FrontRowGap;
        const int FrontBustSize = 160;
        const int RadarWidth = 200;
        const int RadarHeight = 176;
        static readonly Color Ink = new Color(58f / 255f, 6f / 255f, 0f, 1f);
        static readonly Color CardGray = new Color(217f / 255f, 217f / 255f, 217f / 255f, 1f);
        static readonly Color HeaderGray = new Color(142f / 255f, 142f / 255f, 147f / 255f, 1f);
        static readonly Color32 RadarGray = new Color32(0x8E, 0x8E, 0x93, 255);
        static readonly Color32 RadarGhost = new Color32(0x8E, 0x8E, 0x93, 110);
        static readonly Color32 RadarSta = new Color32(0xFF, 0xCC, 0x00, 191);
        static readonly Color32 RadarDef = new Color32(0x5B, 0x7F, 0xD8, 191);
        static readonly Color32 RadarOff = new Color32(0xD8, 0x5B, 0x5E, 191);

        readonly List<Fixture> _fixtures = new List<Fixture>();
        readonly List<CupScheduleLine> _pendingCup = new List<CupScheduleLine>();
        readonly List<ScheduleRow> _rows = new List<ScheduleRow>();
        Kind _kind;
        ClubPage _page;
        StadiumPage _stadiumPage;
        readonly List<SeasonLine> _ranks = new List<SeasonLine>();
        bool _confirmReset;

        /// <summary>メニューの音量ゲージが操作する BGM。GameRunner が渡す。</summary>
        public ScreenBgm Bgm;
        // 音量ゲージ。日送りゲージと同じ画像（599x87 の比率）を使う。
        const float VolumeGaugeWidth = 440f;
        const float VolumeGaugeHeight = VolumeGaugeWidth * 87f / 599f;
        Texture2D _gaugeTrack;
        Texture2D _gaugeFill;
        Texture2D _gaugeFrame;
        bool _volumeDragging;
        bool _resetRequested;
        Vector2 _scroll;
        GUIStyle _title;
        GUIStyle _body;
        GUIStyle _small;
        GUIStyle _button;
        GUIStyle _tab;
        GUIStyle _hit;
        GUIStyle _tabLabel;
        GUIStyle _facilityName;
        GUIStyle _facilityEffect;
        GUIStyle _facilityCost;
        GUIStyle _levelButton;
        GUIStyle _headerNotice;
        GUIStyle _listHead;
        GUIStyle _posLabel;
        GUIStyle _playerName;
        GUIStyle _playerStat;
        GUIStyle _radarValue;
        Font _font;
        string _notice;
        Texture2D _dialogFrame;
        Texture2D _contentFrame;
        Texture2D _tabOn;
        Texture2D _tabOff;
        Texture2D _titleClubhouse;
        Texture2D _closeButton;
        Texture2D _levelButtonImage;
        Texture2D _playerListFrame;
        Texture2D _avatarFrame;
        Texture2D _playerRow;
        Texture2D _playerHeader;
        Texture2D _scrollTrack;
        Texture2D _scrollThumb;
        Texture2D _iconSta;
        Texture2D _iconDef;
        Texture2D _iconOff;
        readonly Texture2D[] _positionIcons = new Texture2D[4];
        readonly Texture2D[] _conditions = new Texture2D[6];
        readonly Texture2D[] _emblems = new Texture2D[LeagueService.TeamCount];
        enum PlayerSort
        {
            None,
            Position,
            Name,
            Age,
            Overall,
            Condition
        }

        readonly List<Player> _roster = new List<Player>();
        PlayerSort _playerSort = PlayerSort.Position;
        bool _playerSortAsc = true;
        bool _playerPress;
        bool _playerDragging;
        Vector2 _playerPressPos;
        float _playerDragLastY;
        readonly AvatarLibrary _avatars = new AvatarLibrary();
        string _selectedPlayerId;
        float _playerScroll;
        bool _playerThumbDrag;
        float _playerThumbGrab;
        string _portraitId;
        int _portraitEmblem = -1;
        int _portraitGroup = -1;
        Texture2D _portrait;
        Texture2D _radar;
        int _radarSta = int.MinValue;
        int _radarOff = int.MinValue;
        int _radarDef = int.MinValue;
        GUIStyle _tableHead;
        GUIStyle _tableHeadLeft;
        GUIStyle _tableNum;
        GUIStyle _tableClub;
        GUIStyle _tableMine;
        GUIStyle _frontButton;
        GUIStyle _pillText;
        GUIStyle _pillSmall;
        GUIStyle _pillRight;
        GUIStyle _frontConfirmText;
        readonly List<StaffMember> _frontListed = new List<StaffMember>();
        bool _frontMarket;
        bool _frontConfirm;
        int _frontTargetSlot = -1;
        string _frontSelectedId;
        string _frontConfirmId;
        float _frontScroll;
        bool _frontPress;
        bool _frontDragging;
        bool _frontThumbDrag;
        Vector2 _frontPressPos;
        float _frontDragLastY;
        float _frontThumbGrab;
        Texture2D _pillDisc;

        public Kind Current => _kind;
        public bool IsOpen => _kind != Kind.None;
        public Rect ActiveBounds => _kind == Kind.Clubhouse ? ClubhouseHitBounds : Bounds;

        public void Load()
        {
            _dialogFrame = Resources.Load<Texture2D>("MainScreen/dialog_01");
            _contentFrame = Resources.Load<Texture2D>("MainScreen/frame_03");
            _tabOn = Resources.Load<Texture2D>("MainScreen/tab_on");
            _tabOff = Resources.Load<Texture2D>("MainScreen/tab_off");
            _titleClubhouse = Resources.Load<Texture2D>("MainScreen/title_clubhouse");
            _closeButton = Resources.Load<Texture2D>("MainScreen/button_close");
            _levelButtonImage = Resources.Load<Texture2D>("MainScreen/button_01");
            _playerListFrame = Resources.Load<Texture2D>("MainScreen/frame_playerlist");
            _avatarFrame = Resources.Load<Texture2D>("MainScreen/frame_avatar");
            _playerRow = Resources.Load<Texture2D>("MainScreen/frame_player");
            _playerHeader = Resources.Load<Texture2D>("MainScreen/frame_player_header");
            _scrollTrack = Resources.Load<Texture2D>("MainScreen/scrollbar_track");
            _scrollThumb = Resources.Load<Texture2D>("MainScreen/scrollbar_thumb");
            _iconSta = Resources.Load<Texture2D>("MainScreen/icon_sta");
            _iconDef = Resources.Load<Texture2D>("MainScreen/icon_def");
            _iconOff = Resources.Load<Texture2D>("MainScreen/icon_off");
            _positionIcons[(int)PositionGroup.GK] = Resources.Load<Texture2D>("MainScreen/frame_position_gk");
            _positionIcons[(int)PositionGroup.DF] = Resources.Load<Texture2D>("MainScreen/frame_position_df");
            _positionIcons[(int)PositionGroup.MF] = Resources.Load<Texture2D>("MainScreen/frame_position_mf");
            _positionIcons[(int)PositionGroup.FW] = Resources.Load<Texture2D>("MainScreen/frame_position_fw");
            for (int i = 0; i < _conditions.Length; i++)
                _conditions[i] = Resources.Load<Texture2D>("MainScreen/condition_" + (i + 1).ToString("00"));
            _avatars.Load();
            for (int i = 0; i < _emblems.Length; i++)
                _emblems[i] = Resources.Load<Texture2D>("emblem/emblem_" + (i + 1).ToString("00"));
        }

        public void Open(Kind kind)
        {
            _kind = kind;
            _scroll = Vector2.zero;
            _page = ClubPage.Strengthen;
            _stadiumPage = StadiumPage.Schedule;
            _notice = "";
            _confirmReset = false;
            _selectedPlayerId = null;
            _playerScroll = 0f;
            ResetFront();
            _playerThumbDrag = false;
            _playerPress = false;
            _playerDragging = false;
            _playerSort = PlayerSort.Position;
            _playerSortAsc = true;
            ReleasePortrait();
            ReleaseRadar();
        }

        public void Close()
        {
            _kind = Kind.None;
            _confirmReset = false;
            _volumeDragging = false;
            _playerThumbDrag = false;
            _playerPress = false;
            _playerDragging = false;
            ReleasePortrait();
            ReleaseRadar();
        }

        /// <summary>データリセットの確定を一度だけ取り出す。</summary>
        public bool TakeResetRequest()
        {
            if (!_resetRequested) return false;
            _resetRequested = false;
            return true;
        }

        public void Draw(GameSession session, Font font, string status, Action<string> setStatus, Action<string> focusCharacter)
        {
            if (_kind == Kind.None || session == null) return;
            EnsureStyles(font);
            if (_kind == Kind.Clubhouse)
            {
                DrawClubhouseDialog(session, session.PlayerClub, setStatus, focusCharacter);
                return;
            }

            UseText(Color.white);
            GUI.color = new Color(0.04f, 0.06f, 0.05f, 0.45f);
            GUI.DrawTexture(new Rect(0f, 0f, GameRunner.ReferenceWidth, GameRunner.ReferenceHeight), Texture2D.whiteTexture);
            GUI.color = new Color(0.10f, 0.14f, 0.12f, 0.96f);
            GUI.DrawTexture(Bounds, Texture2D.whiteTexture);
            GUI.color = Color.white;

            var club = session.PlayerClub;
            float x = Bounds.x + 28f;
            float y = Bounds.y + 18f;
            float width = Bounds.width - 56f;
            GUI.Label(new Rect(x, y, width - 120f, 44f), Title(_kind), _title);
            if (GUI.Button(new Rect(Bounds.xMax - 128f, y, 96f, 36f), "閉じる", _button))
                Close();

            if (session.GameOver)
                GUI.Label(new Rect(x, y + 46f, width, 32f), "資金ショートのためゲームオーバーです。", _body);

            var content = new Rect(x, Bounds.y + 78f, width, Bounds.height - 136f);
            switch (_kind)
            {
                case Kind.Menu:
                    DrawMenu(session, club, content, status);
                    break;
                case Kind.Stadium:
                    DrawStadium(session, club, content, setStatus);
                    break;
                case Kind.Training:
                    DrawTraining(session, club, content, setStatus);
                    break;
            }

            GUI.Label(new Rect(x, Bounds.yMax - 48f, width, 32f), status ?? "", _small);
        }

        void DrawMenu(GameSession session, Club club, Rect area, string status)
        {
            if (_confirmReset)
            {
                DrawResetConfirm(area);
                return;
            }

            DrawVolume(new Rect(area.xMax - VolumeGaugeWidth, area.y, VolumeGaugeWidth, 36f + 8f + VolumeGaugeHeight));

            float y = area.y;
            GUI.Label(new Rect(area.x, y, area.width, 36f), "クラブ: " + club.Name, _body);
            y += 40f;
            GUI.Label(new Rect(area.x, y, area.width, 36f), "資金: " + MoneyText.Format(club.Coins), _body);
            y += 40f;
            GUI.Label(
                new Rect(area.x, y, area.width, 36f),
                "リーグLV" + session.LeagueLevel + "  "
                + club.LeaguePoints + "点  " + club.Wins + "勝 " + club.Draws + "分 " + club.Losses + "敗",
                _body);
            y += 40f;
            GUI.Label(new Rect(area.x, y, area.width, 36f), "総合力: " + FormatRating(club), _body);
            y += 40f;
            GUI.Label(new Rect(area.x, y, area.width, 36f), "選手人数: " + club.Squad.Count, _body);
            y += 40f;
            GUI.Label(new Rect(area.x, y, area.width, 36f), SecretaryLine(session, club), _body);
            y += 48f;
            GUI.Label(new Rect(area.x, y, area.width, 64f), status ?? "", _small);
            y += 80f;
            if (GUI.Button(new Rect(area.x, y, 280f, 52f), "スタジアム", _button))
                Open(Kind.Stadium);
            if (GUI.Button(new Rect(area.x + 300f, y, 280f, 52f), "練習場", _button))
                Open(Kind.Training);
            if (GUI.Button(new Rect(area.x + 600f, y, 280f, 52f), "クラブハウス", _button))
                Open(Kind.Clubhouse);
            y += 72f;
            GUI.Label(new Rect(area.x, y, area.width, 32f), "進行は自動で保存されます。", _small);
            y += 40f;
            if (GUI.Button(new Rect(area.x, y, 280f, 52f), "データをリセット", _button))
                _confirmReset = true;
        }

        /// <summary>メニュー右上の音量。ゲージをクリック、またはドラッグして変える。</summary>
        void DrawVolume(Rect area)
        {
            if (Bgm == null) return;
            if (_gaugeTrack == null) _gaugeTrack = Resources.Load<Texture2D>("MainScreen/gage_framebg_nextday");
            if (_gaugeFill == null) _gaugeFill = Resources.Load<Texture2D>("MainScreen/gage_bar_nextday");
            if (_gaugeFrame == null) _gaugeFrame = Resources.Load<Texture2D>("MainScreen/gage_frame_nextday");

            int percent = Mathf.RoundToInt(Bgm.UserVolume * 100f);
            GUI.Label(new Rect(area.x, area.y, area.width, 36f), "音量  " + percent + "%", _body);
            var gauge = new Rect(area.x, area.y + 44f, VolumeGaugeWidth, VolumeGaugeHeight);
            float scale = gauge.width / 599f;
            float origin = 51f * scale;
            float span = 498f * scale;

            if (_gaugeTrack != null)
                GUI.DrawTexture(gauge, _gaugeTrack, ScaleMode.StretchToFill, true);
            float shown = origin + span * Mathf.Clamp01(Bgm.UserVolume);
            if (_gaugeFill != null && shown > origin)
            {
                GUI.BeginGroup(new Rect(gauge.x, gauge.y, shown, gauge.height));
                GUI.DrawTexture(new Rect(0f, 0f, gauge.width, gauge.height), _gaugeFill, ScaleMode.StretchToFill, true);
                GUI.EndGroup();
            }

            if (_gaugeFrame != null)
                GUI.DrawTexture(gauge, _gaugeFrame, ScaleMode.StretchToFill, true);

            var pointer = Event.current;
            if (pointer == null) return;
            var mouse = pointer.mousePosition;
            if (pointer.type == EventType.MouseDown && pointer.button == 0 && gauge.Contains(mouse))
            {
                _volumeDragging = true;
                Bgm.SetUserVolume((mouse.x - gauge.x - origin) / span);
                pointer.Use();
            }
            else if (_volumeDragging && pointer.button == 0 && (pointer.type == EventType.MouseDrag || pointer.type == EventType.MouseUp))
            {
                Bgm.SetUserVolume((mouse.x - gauge.x - origin) / span);
                if (pointer.type == EventType.MouseUp) _volumeDragging = false;
                pointer.Use();
            }
            else if (_volumeDragging && !Input.GetMouseButton(0))
            {
                _volumeDragging = false;
            }
        }

        void DrawResetConfirm(Rect area)
        {
            GUI.Label(new Rect(area.x, area.y, area.width, 40f), "セーブデータを消して、クラブ選択からやり直します。", _body);
            GUI.Label(new Rect(area.x, area.y + 44f, area.width, 36f), "この操作は取り消せません。", _body);
            if (GUI.Button(new Rect(area.x, area.y + 108f, 220f, 52f), "消去する", _button))
            {
                _confirmReset = false;
                _resetRequested = true;
                Close();
            }

            if (GUI.Button(new Rect(area.x + 240f, area.y + 108f, 220f, 52f), "キャンセル", _button))
                _confirmReset = false;
        }

        void DrawStandings(GameSession session, Rect area)
        {
            var table = session.League.Table();
            const float rowH = 24f;
            const float rankW = 56f;
            const float emblemW = 32f;
            const float ptsW = 72f;
            const float playedW = 64f;
            const float resultW = 56f;
            const float goalsW = 72f;
            const float gdW = 80f;
            float nameW = area.width - (rankW + emblemW + ptsW + playedW + resultW * 3f + goalsW * 2f + gdW);
            if (nameW < 180f) nameW = 180f;

            float y = area.y;
            DrawStandingsHeader(area.x, y, rowH, rankW, emblemW, nameW, ptsW, playedW, resultW, goalsW, gdW);
            y += rowH;
            var player = session.PlayerClub;
            for (int i = 0; i < table.Count; i++)
            {
                DrawStandingsRow(
                    table[i], i + 1, player != null && table[i] == player,
                    area.x, y, rowH, rankW, emblemW, nameW, ptsW, playedW, resultW, goalsW, gdW);
                y += rowH;
            }
        }

        void DrawStandingsHeader(
            float x, float y, float rowH,
            float rankW, float emblemW, float nameW, float ptsW, float playedW, float resultW, float goalsW, float gdW)
        {
            GUI.color = new Color(1f, 1f, 1f, 0.12f);
            GUI.DrawTexture(new Rect(x, y, rankW + emblemW + nameW + ptsW + playedW + resultW * 3f + goalsW * 2f + gdW, rowH), Texture2D.whiteTexture);
            GUI.color = Color.white;
            Cell(ref x, y, rowH, rankW, "順", _tableHead);
            x += emblemW;
            Cell(ref x, y, rowH, nameW, "クラブ", _tableHeadLeft);
            Cell(ref x, y, rowH, ptsW, "勝点", _tableHead);
            Cell(ref x, y, rowH, playedW, "試合", _tableHead);
            Cell(ref x, y, rowH, resultW, "勝", _tableHead);
            Cell(ref x, y, rowH, resultW, "分", _tableHead);
            Cell(ref x, y, rowH, resultW, "敗", _tableHead);
            Cell(ref x, y, rowH, goalsW, "得点", _tableHead);
            Cell(ref x, y, rowH, goalsW, "失点", _tableHead);
            Cell(ref x, y, rowH, gdW, "得失", _tableHead);
        }

        void DrawStandingsRow(
            Club club, int rank, bool mine,
            float x, float y, float rowH,
            float rankW, float emblemW, float nameW, float ptsW, float playedW, float resultW, float goalsW, float gdW)
        {
            float width = rankW + emblemW + nameW + ptsW + playedW + resultW * 3f + goalsW * 2f + gdW;
            if (mine)
                GUI.color = new Color(0.95f, 0.78f, 0.28f, 0.38f);
            else if ((rank & 1) == 0)
                GUI.color = new Color(1f, 1f, 1f, 0.06f);
            if (mine || (rank & 1) == 0)
                GUI.DrawTexture(new Rect(x, y, width, rowH), Texture2D.whiteTexture);
            GUI.color = Color.white;

            var nameStyle = mine ? _tableMine : _tableClub;
            Cell(ref x, y, rowH, rankW, rank.ToString(CultureInfo.InvariantCulture), _tableNum);
            var emblem = EmblemOf(club);
            if (emblem != null)
            {
                float size = rowH - 4f;
                GUI.DrawTexture(new Rect(x + (emblemW - size) * 0.5f, y + 2f, size, size), emblem, ScaleMode.ScaleToFit, true);
            }

            x += emblemW;
            Cell(ref x, y, rowH, nameW, club.Name, nameStyle);
            Cell(ref x, y, rowH, ptsW, club.LeaguePoints.ToString(CultureInfo.InvariantCulture), _tableNum);
            int played = club.Wins + club.Draws + club.Losses;
            Cell(ref x, y, rowH, playedW, played.ToString(CultureInfo.InvariantCulture), _tableNum);
            Cell(ref x, y, rowH, resultW, club.Wins.ToString(CultureInfo.InvariantCulture), _tableNum);
            Cell(ref x, y, rowH, resultW, club.Draws.ToString(CultureInfo.InvariantCulture), _tableNum);
            Cell(ref x, y, rowH, resultW, club.Losses.ToString(CultureInfo.InvariantCulture), _tableNum);
            Cell(ref x, y, rowH, goalsW, club.GoalsFor.ToString(CultureInfo.InvariantCulture), _tableNum);
            Cell(ref x, y, rowH, goalsW, club.GoalsAgainst.ToString(CultureInfo.InvariantCulture), _tableNum);
            int gd = club.GoalDifference;
            var previous = _tableNum.normal.textColor;
            _tableNum.normal.textColor = gd > 0
                ? new Color(0.65f, 0.9f, 0.55f, 1f)
                : gd < 0
                    ? new Color(0.95f, 0.55f, 0.5f, 1f)
                    : Color.white;
            Cell(ref x, y, rowH, gdW, (gd > 0 ? "+" : "") + gd.ToString(CultureInfo.InvariantCulture), _tableNum);
            _tableNum.normal.textColor = previous;
        }

        static void Cell(ref float x, float y, float rowH, float width, string text, GUIStyle style)
        {
            GUI.Label(new Rect(x, y, width, rowH), text, style);
            x += width;
        }

        Texture2D EmblemOf(Club club)
        {
            if (club == null) return null;
            int index = club.EmblemIndex - 1;
            if (index < 0 || index >= _emblems.Length) return null;
            return _emblems[index];
        }

        void DrawStadium(GameSession session, Club club, Rect area, Action<string> setStatus)
        {
            StadiumTab(area, 0f, 110f, StadiumPage.Schedule, "予定");
            StadiumTab(area, 118f, 110f, StadiumPage.Results, "結果");
            StadiumTab(area, 236f, 140f, StadiumPage.Table, "順位表");
            StadiumTab(area, 384f, 200f, StadiumPage.Goals, "得点ランキング");
            StadiumTab(area, 592f, 220f, StadiumPage.Ratings, "評価点ランキング");

            var body = new Rect(area.x, area.y + 48f, area.width, area.height - 48f);
            if (_stadiumPage == StadiumPage.Table)
            {
                DrawStandings(session, body);
                return;
            }

            if (_stadiumPage == StadiumPage.Goals)
            {
                DrawGoalRanking(session, body);
                return;
            }

            if (_stadiumPage == StadiumPage.Ratings)
            {
                DrawRatingRanking(session, body);
                return;
            }

            float leftW = 460f;
            DrawUpgrade(session, club, FacilityKind.Stadium, body.x, body.y, leftW, StadiumEffect(club), setStatus);
            var schedule = new Rect(body.x + leftW + 28f, body.y, body.width - leftW - 28f, body.height);
            DrawSchedule(session, club, schedule);
        }

        void StadiumTab(Rect area, float x, float width, StadiumPage page, string label)
        {
            bool on = _stadiumPage == page;
            if (!GUI.Button(new Rect(area.x + x, area.y, width, 36f), on ? "● " + label : label, _tab))
                return;
            if (_stadiumPage == page) return;
            _stadiumPage = page;
            _scroll = Vector2.zero;
        }

        void DrawGoalRanking(GameSession session, Rect area)
        {
            FillRanks(session, true);
            DrawRankTable(area, true, "まだ得点はありません。");
        }

        void DrawRatingRanking(GameSession session, Rect area)
        {
            FillRanks(session, false);
            DrawRankTable(area, false, "まだ評価はありません。");
        }

        void FillRanks(GameSession session, bool goals)
        {
            _ranks.Clear();
            if (session == null || session.Clubs == null) return;
            var mine = session.PlayerClub;
            for (int c = 0; c < session.Clubs.Count; c++)
            {
                var club = session.Clubs[c];
                if (club == null || club.Squad == null) continue;
                for (int i = 0; i < club.Squad.Count; i++)
                {
                    var player = club.Squad[i];
                    if (player == null) continue;
                    if (goals && player.SeasonGoals <= 0) continue;
                    if (!goals && player.SeasonRatedMatches <= 0) continue;
                    _ranks.Add(new SeasonLine
                    {
                        Name = player.Name ?? "",
                        ClubName = club.Name ?? "",
                        Goals = player.SeasonGoals,
                        Matches = player.SeasonRatedMatches,
                        Average = player.SeasonRatedMatches > 0 ? player.SeasonRatingSum / player.SeasonRatedMatches : 0d,
                        Own = club == mine
                    });
                }
            }

            if (goals) _ranks.Sort(CompareGoals);
            else _ranks.Sort(CompareRatings);
        }

        static int CompareGoals(SeasonLine a, SeasonLine b)
        {
            int goals = b.Goals.CompareTo(a.Goals);
            if (goals != 0) return goals;
            return string.Compare(a.Name, b.Name, StringComparison.Ordinal);
        }

        static int CompareRatings(SeasonLine a, SeasonLine b)
        {
            int average = b.Average.CompareTo(a.Average);
            if (average != 0) return average;
            int matches = b.Matches.CompareTo(a.Matches);
            if (matches != 0) return matches;
            return string.Compare(a.Name, b.Name, StringComparison.Ordinal);
        }

        void DrawRankTable(Rect area, bool goals, string empty)
        {
            const float rowH = 28f;
            const float rankW = 64f;
            const float valueW = 110f;
            float extra = goals ? valueW : valueW * 2f;
            float flex = area.width - 24f - rankW - extra;
            if (flex < 240f) flex = 240f;
            float nameW = flex * 0.46f;
            float clubW = flex - nameW;
            float y = area.y;
            float x = area.x;
            Cell(ref x, y, rowH, rankW, "順", _tableHead);
            Cell(ref x, y, rowH, nameW, "選手", _tableHeadLeft);
            Cell(ref x, y, rowH, clubW, "クラブ", _tableHeadLeft);
            Cell(ref x, y, rowH, valueW, goals ? "得点" : "評価", _tableHead);
            if (!goals)
                Cell(ref x, y, rowH, valueW, "試合", _tableHead);

            var list = new Rect(area.x, area.y + rowH, area.width, area.height - rowH);
            if (_ranks.Count == 0)
            {
                GUI.Label(list, empty, _body);
                return;
            }

            float width = rankW + nameW + clubW + extra;
            _scroll = GUI.BeginScrollView(list, _scroll, new Rect(0f, 0f, width, _ranks.Count * rowH));
            for (int i = 0; i < _ranks.Count; i++)
            {
                var line = _ranks[i];
                float rowY = i * rowH;
                if (line.Own)
                    GUI.color = new Color(0.95f, 0.78f, 0.28f, 0.38f);
                else if ((i & 1) == 1)
                    GUI.color = new Color(1f, 1f, 1f, 0.06f);
                if (line.Own || (i & 1) == 1)
                    GUI.DrawTexture(new Rect(0f, rowY, width, rowH), Texture2D.whiteTexture);
                GUI.color = Color.white;
                float cx = 0f;
                var nameStyle = line.Own ? _tableMine : _tableClub;
                Cell(ref cx, rowY, rowH, rankW, (i + 1).ToString(CultureInfo.InvariantCulture), _tableNum);
                Cell(ref cx, rowY, rowH, nameW, line.Name, nameStyle);
                Cell(ref cx, rowY, rowH, clubW, line.ClubName, nameStyle);
                string value = goals
                    ? line.Goals.ToString(CultureInfo.InvariantCulture)
                    : line.Average.ToString("0.00", CultureInfo.InvariantCulture);
                Cell(ref cx, rowY, rowH, valueW, value, _tableNum);
                if (!goals)
                    Cell(ref cx, rowY, rowH, valueW, line.Matches.ToString(CultureInfo.InvariantCulture), _tableNum);
            }

            GUI.EndScrollView();
        }

        sealed class SeasonLine
        {
            public string Name;
            public string ClubName;
            public int Goals;
            public int Matches;
            public double Average;
            public bool Own;
        }

        void DrawTraining(GameSession session, Club club, Rect area, Action<string> setStatus)
        {
            float width = Mathf.Min(640f, area.width);
            DrawUpgrade(session, club, FacilityKind.TrainingGround, area.x, area.y, width, TrainingGroundEffect(club), setStatus);
        }

        void DrawClubhouseDialog(GameSession session, Club club, Action<string> setStatus, Action<string> focusCharacter)
        {
            var previous = GUI.color;
            GUI.color = new Color(0.04f, 0.06f, 0.05f, 0.45f);
            GUI.DrawTexture(new Rect(0f, 0f, GameRunner.ReferenceWidth, GameRunner.ReferenceHeight), Texture2D.whiteTexture);
            GUI.color = Color.white;
            DrawTex(ClubhouseDialogRect, _dialogFrame);
            DrawTex(ClubhouseFrameRect, _contentFrame);

            for (int i = 0; i < ClubhouseTabX.Length; i++)
            {
                if ((ClubPage)i == _page) continue;
                DrawTex(new Rect(ClubhouseTabX[i], ClubhouseTabOffY, ClubhouseTabWidth, ClubhouseTabOffHeight), _tabOff);
            }

            int selected = (int)_page;
            DrawTex(new Rect(ClubhouseTabX[selected], ClubhouseTabOnY, ClubhouseTabWidth, ClubhouseTabOnHeight), _tabOn);

            for (int i = 0; i < ClubhouseTabLabels.Length; i++)
            {
                GUI.Label(
                    new Rect(ClubhouseTabX[i], 294f, ClubhouseTabWidth, 42f),
                    ClubhouseTabLabels[i],
                    _tabLabel);
            }

            DrawTex(ClubhouseTitleRect, _titleClubhouse);
            DrawTex(ClubhouseCloseRect, _closeButton);

            if (session.GameOver)
            {
                GUI.Label(
                    new Rect(ClubhouseDialogRect.x, 226f, ClubhouseDialogRect.width, 32f),
                    "資金ショートのためゲームオーバーです。",
                    _headerNotice);
            }

            UseText(new Color(0.08f, 0.08f, 0.08f, 1f));
            switch (_page)
            {
                case ClubPage.Strengthen:
                    DrawStrengthen(session, club, setStatus);
                    break;
                case ClubPage.Players:
                    DrawPlayers(club);
                    break;
                case ClubPage.Staff:
                    DrawTeamStaff(club, ClubhousePageRect, focusCharacter);
                    break;
                case ClubPage.Front:
                    DrawFront(session, club, setStatus);
                    break;
            }

            if (_page == ClubPage.Strengthen && !string.IsNullOrEmpty(_notice))
            {
                GUI.Label(new Rect(333f, 808f, 1255f, 36f), _notice, _facilityEffect);
            }

            if (_frontConfirm && _page == ClubPage.Front)
            {
                DrawFrontConfirm(session, setStatus);
            }
            else
            {
                for (int i = 0; i < ClubhouseTabX.Length; i++)
                {
                    var tab = new Rect(ClubhouseTabX[i], ClubhouseTabOffY, ClubhouseTabWidth, ClubhouseTabOnY + ClubhouseTabOnHeight - ClubhouseTabOffY);
                    if (GUI.Button(tab, GUIContent.none, _hit))
                        SetPage((ClubPage)i);
                }

                if (GUI.Button(ClubhouseCloseRect, GUIContent.none, _hit))
                    Close();
            }

            GUI.color = previous;
        }

        void DrawStrengthen(GameSession session, Club club, Action<string> setStatus)
        {
            DrawFacilityCard(
                session,
                club,
                FacilityKind.Clubhouse,
                "クラブハウス",
                MarketRangeLabel(club),
                ClubhouseCardLeft,
                ClubhouseButtonLeft,
                setStatus);
            DrawFacilityCard(
                session,
                club,
                FacilityKind.TrainingFacility,
                "トレーニング室",
                "スタミナの練習効果 " + FormatTrainingBonus(club.Facilities.TrainingRoomBonusPercent()),
                ClubhouseCardRight,
                ClubhouseButtonRight,
                setStatus);
        }

        void DrawFacilityCard(
            GameSession session,
            Club club,
            FacilityKind kind,
            string title,
            string effect,
            Rect card,
            Rect button,
            Action<string> setStatus)
        {
            GUI.color = CardGray;
            GUI.DrawTexture(card, Texture2D.whiteTexture);
            GUI.color = Color.white;

            var facility = club.Facilities.Get(kind);
            string level = facility.Level.ToString(CultureInfo.InvariantCulture);
            GUI.Label(
                new Rect(card.x, 520f, card.width, 48f),
                title + "  <size=32>LV </size><size=40>" + level + "</size>",
                _facilityName);
            GUI.Label(new Rect(card.x, 580f, card.width, 42f), effect, _facilityEffect);
            GUI.Label(new Rect(card.x, 649f, card.width, 44f), CostLine(facility), _facilityCost);

            DrawTex(button, _levelButtonImage);
            GUI.Label(new Rect(button.x, button.y - 6f, button.width, button.height), "レベルアップ", _levelButton);
            if (GUI.Button(button, GUIContent.none, _hit))
                TryUpgradeNamed(session, kind, title, setStatus);
        }

        static string CostLine(Facility facility)
        {
            if (!facility.CanUpgrade()) return "レベル上限";
            return "費用 " + MoneyText.Format(facility.NextUpgradeCost());
        }

        static void DrawTex(Rect rect, Texture2D texture)
        {
            if (texture != null)
                GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, true);
        }

        void SetPage(ClubPage page)
        {
            if (_page == page) return;
            _page = page;
            _scroll = Vector2.zero;
            _notice = "";
            ResetFront();
        }

        void ResetFront()
        {
            _frontMarket = false;
            _frontConfirm = false;
            _frontTargetSlot = -1;
            _frontSelectedId = null;
            _frontConfirmId = null;
            _frontScroll = 0f;
            _frontPress = false;
            _frontDragging = false;
            _frontThumbDrag = false;
        }

        float DrawUpgrade(GameSession session, Club club, FacilityKind kind, float x, float y, float width, string effect, Action<string> setStatus, bool light = false)
        {
            var facility = club.Facilities.Get(kind);
            var card = new Rect(x, y, width, 196f);
            GUI.color = light ? CardGray : new Color(1f, 1f, 1f, 0.06f);
            GUI.DrawTexture(card, Texture2D.whiteTexture);
            GUI.color = Color.white;
            float tx = x + 16f;
            float ty = y + 12f;
            float tw = width - 32f;
            GUI.Label(new Rect(tx, ty, tw, 34f), FacilityLabel(kind) + "  Lv" + facility.Level + GradeSuffix(kind, facility.Level), _body);
            ty += 36f;
            GUI.Label(new Rect(tx, ty, tw, 32f), effect, _small);
            ty += 32f;
            GUI.Label(new Rect(tx, ty, tw, 28f), "資金  " + MoneyText.Format(club.Coins), _small);
            ty += 30f;
            if (facility.CanUpgrade())
            {
                GUI.Label(new Rect(tx, ty, tw - 150f, 28f), "次の費用  " + MoneyText.Format(facility.NextUpgradeCost()), _small);
                if (GUI.Button(new Rect(tx + tw - 140f, ty - 4f, 140f, 40f), "Lvアップ", _button))
                    TryUpgrade(session, kind, setStatus);
            }
            else
            {
                GUI.Label(new Rect(tx, ty, tw, 28f), "レベル上限です。", _small);
            }

            return y + 208f;
        }

        void DrawSchedule(GameSession session, Club club, Rect area)
        {
            bool results = _stadiumPage == StadiumPage.Results;
            CollectFixtures(session, club, results);
            var list = area;
            int breakAt = WinterBreakRowIndex(session);
            if (_rows.Count == 0 && breakAt < 0)
            {
                GUI.Label(list, results ? "結果はまだありません。" : "予定の試合はありません。", _body);
                return;
            }

            const float rowH = 36f;
            int rows = _rows.Count + (breakAt >= 0 ? 1 : 0);
            _scroll = GUI.BeginScrollView(list, _scroll, new Rect(0f, 0f, list.width - 24f, rows * rowH));
            int line = 0;
            for (int i = 0; i < _rows.Count; i++)
            {
                if (i == breakAt)
                {
                    GUI.Label(new Rect(0f, line * rowH, list.width - 24f, rowH), WinterBreakLabel(session.CurrentSeasonStart), _small);
                    line++;
                }

                GUI.Label(new Rect(0f, line * rowH, list.width - 24f, rowH), _rows[i].Label, _small);
                line++;
            }

            GUI.EndScrollView();
        }

        int WinterBreakRowIndex(GameSession session)
        {
            if (_stadiumPage != StadiumPage.Schedule || session == null) return -1;
            var seasonStart = session.CurrentSeasonStart;
            var opener = WinterBreak.SecondHalfOpener(seasonStart);
            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i].Date.Date < opener) continue;
                if (i == 0)
                    return WinterBreak.Contains(seasonStart, session.Time.CurrentDate) ? 0 : -1;
                if (_rows[i - 1].Date.Date <= WinterBreak.LastFirstHalf(seasonStart))
                    return i;
                return -1;
            }

            return -1;
        }

        static string WinterBreakLabel(DateTime seasonStart)
        {
            var start = WinterBreak.Start(seasonStart);
            var end = WinterBreak.End(seasonStart);
            return start.ToString("MM/dd", CultureInfo.InvariantCulture)
                   + " - "
                   + end.ToString("MM/dd", CultureInfo.InvariantCulture)
                   + "  ウィンターブレイク";
        }

        /// <summary>
        /// 選手タブ。左が選択中のアバターとスタミナ・攻撃・守備のレーダー、右が所属一覧。
        /// </summary>
        void DrawPlayers(Club club)
        {
            DrawTex(PlayerListRect, _playerListFrame);
            DrawTex(PlayerAvatarRect, _avatarFrame);
            DrawTex(PlayerHeaderRect, _playerHeader);
            DrawSortHeader(new Rect(808f, 388f, 80f, 45f), "POS", PlayerSort.Position);
            DrawSortHeader(new Rect(892f, 388f, 370f, 45f), "選手名", PlayerSort.Name);
            DrawSortHeader(new Rect(1262f, 388f, 70f, 45f), "年齢", PlayerSort.Age);
            DrawSortHeader(new Rect(1332f, 388f, 130f, 45f), "総合力", PlayerSort.Overall);
            DrawSortHeader(new Rect(1462f, 388f, 74f, 45f), "調子", PlayerSort.Condition);

            CollectRoster(club);
            float content = _roster.Count == 0 ? 0f : _roster.Count * PlayerRowStride - PlayerRowGap;
            UpdatePlayerScroll(content);
            DrawRosterRows();
            DrawPlayerScrollbar(content);

            var selected = SelectedPlayer();
            DrawPortrait(selected, club);
            if (selected != null)
                DrawRadar(selected);
        }

        void CollectRoster(Club club)
        {
            _roster.Clear();
            var squad = club == null ? null : club.Squad;
            if (squad == null) return;
            for (int i = 0; i < squad.Count; i++)
            {
                if (squad[i] != null) _roster.Add(squad[i]);
            }

            if (_playerSort != PlayerSort.None)
                _roster.Sort(CompareRoster);
        }

        void DrawSortHeader(Rect rect, string label, PlayerSort sort)
        {
            string mark = _playerSort == sort ? (_playerSortAsc ? " ▲" : " ▼") : "";
            GUI.Label(rect, label + mark, _listHead);
            if (GUI.Button(rect, GUIContent.none, _hit))
                TogglePlayerSort(sort);
        }

        void TogglePlayerSort(PlayerSort sort)
        {
            if (_playerSort == sort)
                _playerSortAsc = !_playerSortAsc;
            else
            {
                _playerSort = sort;
                _playerSortAsc = true;
            }
        }

        int CompareRoster(Player a, Player b)
        {
            if (ReferenceEquals(a, b)) return 0;
            if (a == null) return 1;
            if (b == null) return -1;
            int sign = _playerSortAsc ? 1 : -1;
            int compared = 0;
            switch (_playerSort)
            {
                case PlayerSort.Position:
                    compared = ((int)a.BestPosition()).CompareTo((int)b.BestPosition());
                    break;
                case PlayerSort.Name:
                    compared = string.Compare(a.Name, b.Name, StringComparison.Ordinal);
                    break;
                case PlayerSort.Age:
                    compared = a.Age.CompareTo(b.Age);
                    break;
                case PlayerSort.Overall:
                    compared = a.Overall.CompareTo(b.Overall);
                    break;
                case PlayerSort.Condition:
                    compared = a.Condition.CompareTo(b.Condition);
                    break;
            }

            if (compared != 0) return compared * sign;
            int overall = b.Overall.CompareTo(a.Overall);
            if (overall != 0) return overall;
            return string.CompareOrdinal(a.Id, b.Id);
        }

        Player SelectedPlayer()
        {
            for (int i = 0; i < _roster.Count; i++)
            {
                if (_roster[i].Id == _selectedPlayerId) return _roster[i];
            }

            if (_roster.Count == 0)
            {
                _selectedPlayerId = null;
                return null;
            }

            _selectedPlayerId = _roster[0].Id;
            return _roster[0];
        }

        void DrawRosterRows()
        {
            if (_roster.Count == 0)
            {
                GUI.Label(PlayerRowsRect, "選手はいません。", _playerName);
                return;
            }

            GUI.BeginGroup(PlayerRowsRect);
            for (int i = 0; i < _roster.Count; i++)
            {
                float y = i * PlayerRowStride - _playerScroll;
                if (y + PlayerRowHeight < 0f || y > PlayerRowsRect.height) continue;
                var player = _roster[i];
                var row = new Rect(0f, y, PlayerRowsRect.width, PlayerRowHeight);
                DrawTex(row, _playerRow);
                if (player.Id == _selectedPlayerId)
                {
                    var previous = GUI.color;
                    GUI.color = new Color(1f, 1f, 1f, 0.22f);
                    GUI.DrawTexture(row, Texture2D.whiteTexture);
                    GUI.color = previous;
                    DrawBorder(row, 3f, Color.white);
                }

                var position = player.BestPosition();
                var badge = new Rect(8f, y + 10f, 76f, 60f);
                DrawTex(badge, _positionIcons[(int)PositionUtility.GroupOf(position)]);
                var group = PositionUtility.GroupOf(position);
                _posLabel.normal.textColor = group == PositionGroup.MF || group == PositionGroup.FW ? Ink : Color.white;
                GUI.Label(badge, position.ToString(), _posLabel);
                GUI.Label(new Rect(100f, y, 360f, PlayerRowHeight), player.Name ?? "", _playerName);
                GUI.Label(new Rect(468f, y, 60f, PlayerRowHeight), player.Age.ToString(CultureInfo.InvariantCulture), _playerStat);
                GUI.Label(new Rect(538f, y, 124f, PlayerRowHeight), player.DisplayOverall.ToString(CultureInfo.InvariantCulture), _playerStat);
                DrawTex(new Rect(666f, y + 9f, 60f, 61f), ConditionIcon(player));
            }

            GUI.EndGroup();
        }

        void UpdatePlayerScroll(float content)
        {
            float maxScroll = Mathf.Max(0f, content - PlayerRowsRect.height);
            _playerScroll = Mathf.Clamp(_playerScroll, 0f, maxScroll);
            var e = Event.current;
            if (e == null) return;

            bool overRows = PlayerRowsRect.Contains(e.mousePosition);
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
                    SelectRosterAt(e.mousePosition);
                e.Use();
                return;
            }

            if (e.type == EventType.MouseUp)
            {
                if (_playerThumbDrag) e.Use();
                _playerThumbDrag = false;
            }

            if (maxScroll <= 0.5f) return;

            var track = PlayerScrollTrackRect;
            float thumbH = Mathf.Clamp(track.height * PlayerRowsRect.height / content, 48f, track.height);
            float span = Mathf.Max(1f, track.height - thumbH);
            float thumbY = track.y + span * (_playerScroll / maxScroll);
            var thumb = new Rect(track.x, thumbY, track.width, thumbH);
            bool overList = overRows || track.Contains(e.mousePosition);
            if (e.type == EventType.ScrollWheel && overList)
            {
                _playerScroll = Mathf.Clamp(_playerScroll - e.delta.y * 40f, 0f, maxScroll);
                e.Use();
            }
            else if (e.type == EventType.MouseDown && e.button == 0 && thumb.Contains(e.mousePosition))
            {
                _playerThumbDrag = true;
                _playerThumbGrab = e.mousePosition.y - thumbY;
                e.Use();
            }
            else if (e.type == EventType.MouseDown && e.button == 0 && track.Contains(e.mousePosition))
            {
                float dir = e.mousePosition.y < thumbY ? -1f : 1f;
                _playerScroll = Mathf.Clamp(_playerScroll + dir * PlayerRowsRect.height, 0f, maxScroll);
                e.Use();
            }
            else if (_playerThumbDrag && e.type == EventType.MouseDrag)
            {
                float y = e.mousePosition.y - _playerThumbGrab;
                _playerScroll = Mathf.Clamp01((y - track.y) / span) * maxScroll;
                e.Use();
            }
        }

        void SelectRosterAt(Vector2 mouse)
        {
            float local = mouse.y - PlayerRowsRect.y + _playerScroll;
            int index = (int)(local / PlayerRowStride);
            if (index < 0 || index >= _roster.Count) return;
            if (_roster[index] != null)
                _selectedPlayerId = _roster[index].Id;
        }

        void DrawPlayerScrollbar(float content)
        {
            DrawTex(PlayerScrollTrackRect, _scrollTrack);
            float maxScroll = Mathf.Max(0f, content - PlayerRowsRect.height);
            var track = PlayerScrollTrackRect;
            float thumbH = maxScroll <= 0.5f
                ? track.height
                : Mathf.Clamp(track.height * PlayerRowsRect.height / content, 48f, track.height);
            float span = Mathf.Max(1f, track.height - thumbH);
            float t = maxScroll <= 0.5f ? 0f : _playerScroll / maxScroll;
            DrawTex(new Rect(track.x, track.y + span * t, track.width, thumbH), _scrollThumb);
        }

        void DrawPortrait(Player player, Club club)
        {
            var texture = PortraitOf(player, club);
            if (texture == null) return;
            // シートは髪型のぶん左右に広い。体が枠に収まる大きさで、高さ基準で下揃え・中央に描く。
            var rect = PlayerPortraitRect;
            float aspect = texture.width / (float)Mathf.Max(1, texture.height);
            float h = Mathf.Min(rect.height, rect.width / (aspect * AvatarLibrary.BodyFraction));
            float w = h * aspect;
            GUI.DrawTexture(new Rect(rect.center.x - w * 0.5f, rect.yMax - h, w, h), texture, ScaleMode.StretchToFill, true);
        }

        Texture2D PortraitOf(Player player, Club club)
        {
            if (player == null || club == null) return null;
            int group = (int)PositionUtility.GroupOf(player.BestPosition());
            if (_portrait != null && _portraitId == player.Id && _portraitEmblem == club.EmblemIndex && _portraitGroup == group)
                return _portrait;
            ReleasePortrait();
            if (!_avatars.Ready) _avatars.Load();
            if (!_avatars.Ready) return null;
            var hair = PositionUtility.GroupColor((PositionGroup)group);
            var stand = _avatars.CreateStand(player.Id, AvatarLibrary.Kit(club.EmblemIndex), hair);
            if (stand == null) return null;

            _portrait = stand;
            _portraitId = player.Id;
            _portraitEmblem = club.EmblemIndex;
            _portraitGroup = group;
            return _portrait;
        }

        void DrawRadar(Player player)
        {
            int sta = player.DisplayStaminaMax;
            int off = player.DisplayOffense;
            int def = player.DisplayDefense;
            if (_radar == null || sta != _radarSta || off != _radarOff || def != _radarDef)
                RebuildRadar(sta, off, def);
            if (_radar != null)
                GUI.DrawTexture(RadarTexRect, _radar, ScaleMode.StretchToFill, true);
            DrawTex(StaIconRect, _iconSta);
            DrawTex(DefIconRect, _iconDef);
            DrawTex(OffIconRect, _iconOff);
            GUI.Label(StaValueRect, sta.ToString(CultureInfo.InvariantCulture), _radarValue);
            GUI.Label(DefValueRect, def.ToString(CultureInfo.InvariantCulture), _radarValue);
            GUI.Label(OffValueRect, off.ToString(CultureInfo.InvariantCulture), _radarValue);
        }

        void RebuildRadar(int sta, int off, int def)
        {
            ReleaseRadar();
            _radarSta = sta;
            _radarOff = off;
            _radarDef = def;
            var top = new Vector2(100f, 10f);
            var left = new Vector2(13.4f, 160f);
            var right = new Vector2(186.6f, 160f);
            var pix = new Color32[RadarWidth * RadarHeight];
            FillTriangle(pix, RadarWidth, RadarHeight, top, left, right, RadarGhost);
            const float max = 10000f;
            var mid = (top + left + right) / 3f;
            var staAt = Vector2.Lerp(mid, top, Mathf.Clamp01(sta / max));
            var defAt = Vector2.Lerp(mid, left, Mathf.Clamp01(def / max));
            var offAt = Vector2.Lerp(mid, right, Mathf.Clamp01(off / max));
            FillTriangle(pix, RadarWidth, RadarHeight, staAt, defAt, offAt, RadarGray);
            FillCircle(pix, RadarWidth, RadarHeight, staAt, 7f, RadarSta);
            FillCircle(pix, RadarWidth, RadarHeight, defAt, 7f, RadarDef);
            FillCircle(pix, RadarWidth, RadarHeight, offAt, 7f, RadarOff);

            var raw = new Color32[pix.Length];
            for (int y = 0; y < RadarHeight; y++)
                Array.Copy(pix, y * RadarWidth, raw, (RadarHeight - 1 - y) * RadarWidth, RadarWidth);
            _radar = new Texture2D(RadarWidth, RadarHeight, TextureFormat.RGBA32, false);
            _radar.hideFlags = HideFlags.HideAndDontSave;
            _radar.wrapMode = TextureWrapMode.Clamp;
            _radar.filterMode = FilterMode.Bilinear;
            _radar.SetPixels32(raw);
            _radar.Apply(false, true);
        }

        static void FillTriangle(Color32[] pix, int w, int h, Vector2 a, Vector2 b, Vector2 c, Color32 color)
        {
            float area = Edge(a, b, c);
            if (Math.Abs(area) < 0.001f) return;
            int x0 = Mathf.Clamp(Mathf.FloorToInt(Math.Min(a.x, Math.Min(b.x, c.x))), 0, w - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt(Math.Max(a.x, Math.Max(b.x, c.x))), 0, w - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(Math.Min(a.y, Math.Min(b.y, c.y))), 0, h - 1);
            int y1 = Mathf.Clamp(Mathf.CeilToInt(Math.Max(a.y, Math.Max(b.y, c.y))), 0, h - 1);
            for (int y = y0; y <= y1; y++)
            {
                int row = y * w;
                for (int x = x0; x <= x1; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float w0 = Edge(b, c, p);
                    float w1 = Edge(c, a, p);
                    float w2 = Edge(a, b, p);
                    if (w0 * area < 0f || w1 * area < 0f || w2 * area < 0f) continue;
                    BlendPixel(ref pix[row + x], color);
                }
            }
        }

        static void FillCircle(Color32[] pix, int w, int h, Vector2 center, float radius, Color32 color)
        {
            float r2 = radius * radius;
            int x0 = Mathf.Clamp(Mathf.FloorToInt(center.x - radius), 0, w - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt(center.x + radius), 0, w - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(center.y - radius), 0, h - 1);
            int y1 = Mathf.Clamp(Mathf.CeilToInt(center.y + radius), 0, h - 1);
            for (int y = y0; y <= y1; y++)
            {
                int row = y * w;
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x + 0.5f - center.x;
                    float dy = y + 0.5f - center.y;
                    if (dx * dx + dy * dy > r2) continue;
                    BlendPixel(ref pix[row + x], color);
                }
            }
        }

        static float Edge(Vector2 a, Vector2 b, Vector2 p)
        {
            return (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
        }

        static void BlendPixel(ref Color32 dst, Color32 src)
        {
            if (src.a == 0) return;
            if (src.a == 255 || dst.a == 0)
            {
                dst = src;
                return;
            }

            int sa = src.a;
            int inv = 255 - sa;
            dst.r = (byte)((src.r * sa + dst.r * inv) / 255);
            dst.g = (byte)((src.g * sa + dst.g * inv) / 255);
            dst.b = (byte)((src.b * sa + dst.b * inv) / 255);
            dst.a = (byte)(sa + dst.a * inv / 255);
        }

        static void DrawBorder(Rect rect, float thickness, Color color)
        {
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        /// <summary>怪我中は 06。それ以外は 80 以上から 20 点刻みで 01〜05。</summary>
        Texture2D ConditionIcon(Player player)
        {
            int index;
            if (player.InjuryDays > 0) index = 5;
            else if (player.Condition >= 80) index = 0;
            else if (player.Condition >= 60) index = 1;
            else if (player.Condition >= 40) index = 2;
            else if (player.Condition >= 20) index = 3;
            else index = 4;
            return _conditions[index];
        }

        void ReleasePortrait()
        {
            if (_portrait != null) UnityEngine.Object.Destroy(_portrait);
            _portrait = null;
            _portraitId = null;
            _portraitEmblem = -1;
            _portraitGroup = -1;
        }

        void ReleaseRadar()
        {
            if (_radar != null) UnityEngine.Object.Destroy(_radar);
            _radar = null;
            _radarSta = int.MinValue;
            _radarOff = int.MinValue;
            _radarDef = int.MinValue;
        }

        void DrawTeamStaff(Club club, Rect area, Action<string> focusCharacter)
        {
            const float rowH = 40f;
            _scroll = GUI.BeginScrollView(area, _scroll, new Rect(0f, 0f, area.width - 24f, 8f * rowH));
            float y = 0f;
            var team = club.Team;
            if (team != null)
            {
                y = StaffRow(y, area.width - 24f, "監督", team.Manager, focusCharacter);
                y = StaffRow(y, area.width - 24f, "ヘッドコーチ", team.HeadCoach, focusCharacter);
                y = StaffSlots(y, area.width - 24f, "コーチ", team.Coaches, focusCharacter);
                y = StaffRow(y, area.width - 24f, "フィジカルコーチ", team.PhysicalCoach, focusCharacter);
                y = StaffSlots(y, area.width - 24f, "トレーナー", team.Trainers, focusCharacter);
                y = StaffRow(y, area.width - 24f, "ドクター", team.Doctor, focusCharacter);
            }

            GUI.EndScrollView();
        }

        void DrawFront(GameSession session, Club club, Action<string> setStatus)
        {
            BuildFrontList(session, club);
            if (!_frontConfirm)
                HandleFrontPointer();
            if (_frontMarket && !_frontConfirm && YellowButton(FrontBackButton, "戻る"))
                LeaveFrontMarket(club);
            BuildFrontList(session, club);
            var selected = ResolveFrontSelection();
            float content = FrontContentHeight();

            DrawTex(FrontListRect, _playerListFrame);
            DrawTex(FrontAvatarRect, _avatarFrame);
            DrawTex(FrontHeaderRect, _playerHeader);
            GUI.Label(new Rect(839f, 396f, 121f, 45f), "POST", _listHead);
            GUI.Label(new Rect(1069f, 396f, 160f, 45f), "名前", _listHead);
            GUI.Label(new Rect(1303f, 396f, 81f, 45f), "LV", _listHead);
            if (_frontMarket && !_frontConfirm)
                DrawYellowLabel(FrontBackButton, "戻る");

            DrawFrontRows();
            DrawFrontScrollbar(content);
            DrawFrontDetails(selected, club);
            if (_frontConfirm) return;

            if (_frontMarket)
            {
                if (selected != null && YellowButton(FrontContractButton, "契約"))
                {
                    _frontConfirm = true;
                    _frontConfirmId = selected.Id;
                }
            }
            else if (selected != null && YellowButton(FrontContractButton, "契約解除"))
            {
                int slot = FrontSlotOf(selected);
                if (slot >= 0 && session.OwnerDismissesFront(slot))
                {
                    setStatus(selected.Name + "を無償で解雇しました。");
                    if (_frontSelectedId == selected.Id) _frontSelectedId = null;
                }
            }
        }

        void BuildFrontList(GameSession session, Club club)
        {
            _frontListed.Clear();
            if (club == null || club.Front == null) return;
            if (!_frontMarket)
            {
                for (int i = 0; i < FrontOffice.SlotCount; i++)
                    _frontListed.Add(club.Front.GetAt(i));
                return;
            }

            if (_frontTargetSlot < 0 || _frontTargetSlot >= FrontOffice.SlotCount)
            {
                _frontMarket = false;
                for (int i = 0; i < FrontOffice.SlotCount; i++)
                    _frontListed.Add(club.Front.GetAt(i));
                return;
            }

            var role = FrontOffice.RoleAt(_frontTargetSlot);
            var market = session != null ? session.FrontMarket : null;
            if (market == null) return;
            for (int i = 0; i < market.Count; i++)
            {
                var staff = market[i];
                if (staff == null || staff.FrontRole != role) continue;
                if (!ClubMath.StaffMarketAllows(club, staff.Level)) continue;
                _frontListed.Add(staff);
            }
        }

        float FrontContentHeight()
        {
            if (_frontListed.Count == 0) return 0f;
            return _frontListed.Count * FrontRowStride - FrontRowGap;
        }

        StaffMember ResolveFrontSelection()
        {
            StaffMember first = null;
            for (int i = 0; i < _frontListed.Count; i++)
            {
                var staff = _frontListed[i];
                if (staff == null) continue;
                if (first == null) first = staff;
                if (staff.Id == _frontSelectedId) return staff;
            }

            if (first == null)
            {
                _frontSelectedId = null;
                return null;
            }

            _frontSelectedId = first.Id;
            return first;
        }

        void DrawFrontRows()
        {
            GUI.BeginGroup(FrontRowsRect);
            if (_frontListed.Count == 0)
            {
                GUI.Label(new Rect(16f, 24f, FrontRowsRect.width - 32f, 80f), "採用できるフロントはいません。", _playerName);
                GUI.EndGroup();
                return;
            }

            for (int i = 0; i < _frontListed.Count; i++)
            {
                float y = i * FrontRowStride - _frontScroll;
                if (y + FrontRowHeight < 0f || y > FrontRowsRect.height) continue;
                var staff = _frontListed[i];
                var row = new Rect(0f, y, FrontRowsRect.width, FrontRowHeight);
                if (staff == null)
                {
                    GUI.color = Color.white;
                    GUI.DrawTexture(row, Texture2D.whiteTexture);
                    string role = FrontRoleLabel(FrontOffice.RoleAt(i));
                    GUI.Label(new Rect(16f, y, 560f, FrontRowHeight), role + "の枠が空いています。", _playerName);
                    DrawYellowLabel(new Rect(600f, y + 17f, 120f, 46f), "探す");
                    continue;
                }

                DrawTex(row, _playerRow);
                if (staff.Id == _frontSelectedId)
                {
                    var previous = GUI.color;
                    GUI.color = new Color(1f, 1f, 1f, 0.22f);
                    GUI.DrawTexture(row, Texture2D.whiteTexture);
                    GUI.color = previous;
                    DrawBorder(row, 3f, Color.white);
                }

                var post = FrontRoleLabel(staff.FrontRole);
                GUI.Label(new Rect(16f, y, 188f, FrontRowHeight), post, _playerName);
                GUI.Label(new Rect(212f, y, 290f, FrontRowHeight), staff.Name ?? "", _playerName);
                GUI.Label(new Rect(510f, y, 80f, FrontRowHeight), staff.Level.ToString(CultureInfo.InvariantCulture), _playerStat);
                if (!_frontMarket)
                    DrawYellowLabel(new Rect(600f, y + 17f, 120f, 46f), "変更");
            }

            GUI.EndGroup();
        }

        void HandleFrontPointer()
        {
            float content = FrontContentHeight();
            float maxScroll = Mathf.Max(0f, content - FrontRowsRect.height);
            _frontScroll = Mathf.Clamp(_frontScroll, 0f, maxScroll);
            var e = Event.current;
            if (e == null) return;
            bool overRows = FrontRowsRect.Contains(e.mousePosition);
            if (e.type == EventType.MouseDown && e.button == 0 && overRows)
            {
                _frontPress = true;
                _frontDragging = false;
                _frontPressPos = e.mousePosition;
                _frontDragLastY = e.mousePosition.y;
            }
            else if (_frontPress && e.type == EventType.MouseDrag)
            {
                if (!_frontDragging && Mathf.Abs(e.mousePosition.y - _frontPressPos.y) > 8f)
                    _frontDragging = true;
                if (_frontDragging && maxScroll > 0.5f)
                {
                    _frontScroll = Mathf.Clamp(_frontScroll - (e.mousePosition.y - _frontDragLastY), 0f, maxScroll);
                    _frontDragLastY = e.mousePosition.y;
                    e.Use();
                }
            }
            else if (e.type == EventType.MouseUp && e.button == 0 && _frontPress)
            {
                bool dragged = _frontDragging;
                _frontPress = false;
                _frontDragging = false;
                if (!dragged && overRows)
                    ClickFrontRow(e.mousePosition);
                e.Use();
                return;
            }

            if (e.type == EventType.MouseUp)
            {
                if (_frontThumbDrag) e.Use();
                _frontThumbDrag = false;
            }

            if (maxScroll <= 0.5f) return;
            var track = FrontScrollTrackRect;
            float thumbH = Mathf.Clamp(track.height * FrontRowsRect.height / content, 48f, track.height);
            float span = Mathf.Max(1f, track.height - thumbH);
            float thumbY = track.y + span * (_frontScroll / maxScroll);
            var thumb = new Rect(track.x, thumbY, track.width, thumbH);
            bool overList = overRows || track.Contains(e.mousePosition);
            if (e.type == EventType.ScrollWheel && overList)
            {
                _frontScroll = Mathf.Clamp(_frontScroll - e.delta.y * 40f, 0f, maxScroll);
                e.Use();
            }
            else if (e.type == EventType.MouseDown && e.button == 0 && thumb.Contains(e.mousePosition))
            {
                _frontThumbDrag = true;
                _frontThumbGrab = e.mousePosition.y - thumbY;
                e.Use();
            }
            else if (e.type == EventType.MouseDown && e.button == 0 && track.Contains(e.mousePosition))
            {
                float dir = e.mousePosition.y < thumbY ? -1f : 1f;
                _frontScroll = Mathf.Clamp(_frontScroll + dir * FrontRowsRect.height, 0f, maxScroll);
                e.Use();
            }
            else if (_frontThumbDrag && e.type == EventType.MouseDrag)
            {
                float y = e.mousePosition.y - _frontThumbGrab;
                _frontScroll = Mathf.Clamp01((y - track.y) / span) * maxScroll;
                e.Use();
            }
        }

        void ClickFrontRow(Vector2 mouse)
        {
            float local = mouse.y - FrontRowsRect.y + _frontScroll;
            int index = (int)(local / FrontRowStride);
            if (index < 0 || index >= _frontListed.Count) return;
            float into = local - index * FrontRowStride;
            if (into < 0f || into > FrontRowHeight) return;
            var staff = _frontListed[index];
            var button = new Rect(FrontRowsRect.x + 600f, FrontRowsRect.y + index * FrontRowStride - _frontScroll + 17f, 120f, 46f);
            if (!_frontMarket && FrontRowsRect.Contains(mouse) && button.Contains(mouse))
            {
                OpenFrontMarket(index);
                return;
            }

            if (staff != null)
                _frontSelectedId = staff.Id;
        }

        void OpenFrontMarket(int slot)
        {
            _frontMarket = true;
            _frontTargetSlot = slot;
            _frontScroll = 0f;
            _frontConfirm = false;
            _frontConfirmId = null;
            _frontSelectedId = null;
            _frontPress = false;
            _frontDragging = false;
        }

        void LeaveFrontMarket(Club club)
        {
            StaffMember current = club != null && club.Front != null ? club.Front.GetAt(_frontTargetSlot) : null;
            _frontSelectedId = current != null ? current.Id : null;
            _frontMarket = false;
            _frontConfirm = false;
            _frontConfirmId = null;
            _frontScroll = 0f;
        }

        int FrontSlotOf(StaffMember staff)
        {
            if (staff == null || _frontMarket) return -1;
            for (int i = 0; i < _frontListed.Count; i++)
            {
                if (_frontListed[i] == staff) return i;
            }

            return -1;
        }

        void DrawFrontDetails(StaffMember staff, Club club)
        {
            if (staff == null) return;
            DrawPill(FrontRoleBar);
            DrawPill(FrontLevelPill);
            GUI.Label(new Rect(FrontRoleBar.x + 8f, FrontRoleBar.y, 156f, FrontRoleBar.height), FrontRoleLabel(staff.FrontRole), _pillRight);
            GUI.Label(new Rect(FrontLevelPill.x + 8f, FrontLevelPill.y, 28f, FrontLevelPill.height), "LV", _pillSmall);
            GUI.Label(new Rect(FrontLevelPill.x + 34f, FrontLevelPill.y, 52f, FrontLevelPill.height), staff.Level.ToString(CultureInfo.InvariantCulture), _pillText);

            DrawPill(FrontNameBar);
            DrawPill(FrontAgePill);
            GUI.Label(new Rect(FrontNameBar.x, FrontNameBar.y, FrontAgePill.x - FrontNameBar.x, FrontNameBar.height), staff.Name ?? "", _pillText);
            GUI.Label(new Rect(FrontAgePill.x + 12f, FrontAgePill.y, 40f, FrontAgePill.height), staff.Age.ToString(CultureInfo.InvariantCulture), _pillText);
            GUI.Label(new Rect(FrontAgePill.x + 48f, FrontAgePill.y, 36f, FrontAgePill.height), "歳", _pillSmall);

            DrawMoneyPill(FrontSalaryBar, FrontSalaryPill, "年俸", staff.AnnualSalary);
            var bust = PortraitOfStaff(staff, club);
            if (bust != null)
                GUI.DrawTexture(FrontPortraitCircle, bust, ScaleMode.StretchToFill, true);
            if (_frontMarket)
                DrawMoneyPill(FrontFeeBar, FrontFeePill, "契約金", FrontOfficeHiring.SigningFee(staff));
        }

        void DrawMoneyPill(Rect bar, Rect label, string caption, BigInteger amount)
        {
            DrawPill(bar);
            DrawPill(label);
            GUI.Label(label, caption, _pillSmall);
            GUI.Label(new Rect(bar.x + 100f, bar.y, bar.width - 112f, bar.height), MoneyText.Format(amount), _pillRight);
        }

        void DrawFrontConfirm(GameSession session, Action<string> setStatus)
        {
            var candidate = FrontById(_frontConfirmId);
            var current = session != null && session.PlayerClub != null && session.PlayerClub.Front != null
                ? session.PlayerClub.Front.GetAt(_frontTargetSlot)
                : null;
            bool replace = current != null;
            string role = candidate != null ? FrontRoleLabel(candidate.FrontRole) : FrontRoleLabel(FrontOffice.RoleAt(_frontTargetSlot));
            string message;
            if (candidate == null)
            {
                message = "契約するフロントが見つかりません。";
            }
            else if (replace)
            {
                var fee = FrontOfficeHiring.SigningFee(candidate);
                var salary = candidate.AnnualSalary;
                message = current.Name + "を無償で解雇し、" + candidate.Name + "を" + role + "として契約します。\n"
                    + "契約金 " + MoneyText.Format(fee) + " と年俸 " + MoneyText.Format(salary)
                    + "、合計 " + MoneyText.Format(fee + salary) + " を支払います。";
            }
            else
            {
                var fee = FrontOfficeHiring.SigningFee(candidate);
                var salary = candidate.AnnualSalary;
                message = candidate.Name + "を" + role + "として契約します。\n"
                    + "契約金 " + MoneyText.Format(fee) + " と年俸 " + MoneyText.Format(salary)
                    + "、合計 " + MoneyText.Format(fee + salary) + " を支払います。";
            }

            var shade = new Rect(0f, 0f, GameRunner.ReferenceWidth, GameRunner.ReferenceHeight);
            var panel = new Rect(520f, 360f, 880f, 340f);
            GUI.color = new Color(0.04f, 0.05f, 0.05f, 0.55f);
            GUI.DrawTexture(shade, Texture2D.whiteTexture);
            GUI.color = new Color(0.94f, 0.93f, 0.9f, 1f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(panel.x + 36f, panel.y + 28f, panel.width - 72f, 180f), message, _frontConfirmText);
            var cancel = new Rect(panel.x + 150f, panel.y + 236f, 220f, 64f);
            var ok = new Rect(panel.x + 510f, panel.y + 236f, 220f, 64f);
            var pointer = Event.current;
            if (pointer != null && pointer.type == EventType.MouseUp && pointer.button == 0 && !panel.Contains(pointer.mousePosition))
            {
                _frontConfirm = false;
                pointer.Use();
                return;
            }
            if (candidate != null && YellowButton(ok, "契約する"))
                CommitFrontContract(session, candidate, current, setStatus);
            if (YellowButton(cancel, "やめる"))
                _frontConfirm = false;
        }

        void CommitFrontContract(GameSession session, StaffMember candidate, StaffMember current, Action<string> setStatus)
        {
            StaffMember dismissed;
            if (session != null && session.OwnerContractsFront(candidate, _frontTargetSlot, out dismissed))
            {
                string role = FrontRoleLabel(candidate.FrontRole);
                setStatus(current != null
                    ? current.Name + "を解雇し、" + candidate.Name + "を" + role + "として契約しました。"
                    : candidate.Name + "を" + role + "として契約しました。");
                _frontSelectedId = candidate.Id;
                _frontMarket = false;
                _frontConfirm = false;
                _frontConfirmId = null;
                _frontScroll = 0f;
                return;
            }

            setStatus("契約できません。資金が足りません。");
            _frontConfirm = false;
        }

        StaffMember FrontById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < _frontListed.Count; i++)
            {
                if (_frontListed[i] != null && _frontListed[i].Id == id) return _frontListed[i];
            }

            return null;
        }

        void DrawFrontScrollbar(float content)
        {
            DrawTex(FrontScrollTrackRect, _scrollTrack);
            float maxScroll = Mathf.Max(0f, content - FrontRowsRect.height);
            var track = FrontScrollTrackRect;
            float thumbH = maxScroll <= 0.5f
                ? track.height
                : Mathf.Clamp(track.height * FrontRowsRect.height / content, 48f, track.height);
            float span = Mathf.Max(1f, track.height - thumbH);
            float t = maxScroll <= 0.5f ? 0f : _frontScroll / maxScroll;
            DrawTex(new Rect(track.x, track.y + span * t, track.width, thumbH), _scrollThumb);
        }

        void DrawPill(Rect rect)
        {
            EnsurePillDisc();
            var previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.5f);
            float d = rect.height;
            GUI.DrawTexture(new Rect(rect.x, rect.y, d, d), _pillDisc, ScaleMode.StretchToFill, true);
            GUI.DrawTexture(new Rect(rect.xMax - d, rect.y, d, d), _pillDisc, ScaleMode.StretchToFill, true);
            if (rect.width > d)
                GUI.DrawTexture(new Rect(rect.x + d * 0.5f, rect.y, rect.width - d, rect.height), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        void EnsurePillDisc()
        {
            if (_pillDisc != null) return;
            const int size = 32;
            _pillDisc = new Texture2D(size, size, TextureFormat.RGBA32, false);
            _pillDisc.hideFlags = HideFlags.HideAndDontSave;
            var pixels = new Color32[size * size];
            float r = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - r;
                    float dy = y - r;
                    if (dx * dx + dy * dy <= r * r)
                        pixels[y * size + x] = new Color32(255, 255, 255, 255);
                }
            }

            _pillDisc.SetPixels32(pixels);
            _pillDisc.Apply(false, true);
            _pillDisc.wrapMode = TextureWrapMode.Clamp;
        }

        bool YellowButton(Rect rect, string label)
        {
            DrawYellowLabel(rect, label);
            return GUI.Button(rect, GUIContent.none, _hit);
        }

        void DrawYellowLabel(Rect rect, string label)
        {
            DrawTex(rect, _levelButtonImage);
            GUI.Label(rect, label, _frontButton);
        }

        Texture2D PortraitOfStaff(StaffMember staff, Club club)
        {
            if (staff == null || club == null) return null;
            if (_portrait != null && _portraitId == staff.Id && _portraitEmblem == club.EmblemIndex && _portraitGroup == -2)
                return _portrait;
            ReleasePortrait();
            if (!_avatars.Ready) _avatars.Load();
            if (!_avatars.Ready) return null;
            var stand = _avatars.CreateStand(staff.Id, AvatarLibrary.Kit(club.EmblemIndex), AvatarLibrary.NaturalHair(staff.Id), 0, AvatarLibrary.OutfitOf(staff));
            var circle = CircleBust(stand);
            if (stand != null) UnityEngine.Object.Destroy(stand);
            if (circle == null) return null;
            _portrait = circle;
            _portraitId = staff.Id;
            _portraitEmblem = club.EmblemIndex;
            _portraitGroup = -2;
            return circle;
        }

        static Texture2D CircleBust(Texture2D stand)
        {
            if (stand == null) return null;
            var source = stand.GetPixels32();
            int sw = stand.width;
            int sh = stand.height;
            int size = FrontBustSize;
            var pixels = new Color32[size * size];
            // 体の幅（シート中央の BodyFraction）を円の直径に合わせる。左右の髪のはみ出しは円の外。
            float bodyW = Mathf.Max(1f, sw * AvatarLibrary.BodyFraction);
            float uLeft = (1f - AvatarLibrary.BodyFraction) * 0.5f;
            float scale = size / bodyW;
            float drawnH = sh * scale;
            float radius = size * 0.5f;
            var fill = new Color32(255, 255, 255, 255);
            for (int y = 0; y < size; y++)
            {
                int row = (size - 1 - y) * size;
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - radius;
                    float dy = y + 0.5f - radius;
                    if (dx * dx + dy * dy > radius * radius) continue;
                    float vTop = drawnH <= 0f ? 1f : y / drawnH;
                    var sample = vTop > 1f ? fill : SampleBust(source, sw, sh, uLeft + (x + 0.5f) / size * AvatarLibrary.BodyFraction, vTop);
                    pixels[row + x] = OverWhite(sample);
                }
            }

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.HideAndDontSave;
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            return tex;
        }

        static Color32 SampleBust(Color32[] source, int sw, int sh, float u, float vTop)
        {
            float x = Mathf.Clamp(u, 0f, 1f) * (sw - 1);
            float y = (1f - Mathf.Clamp(vTop, 0f, 1f)) * (sh - 1);
            int x0 = Mathf.Clamp(Mathf.FloorToInt(x), 0, sw - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(y), 0, sh - 1);
            int x1 = Mathf.Min(x0 + 1, sw - 1);
            int y1 = Mathf.Min(y0 + 1, sh - 1);
            float tx = x - Mathf.Floor(x);
            float ty = y - Mathf.Floor(y);
            return LerpBust(
                LerpBust(source[y0 * sw + x0], source[y0 * sw + x1], tx),
                LerpBust(source[y1 * sw + x0], source[y1 * sw + x1], tx),
                ty);
        }

        static Color32 LerpBust(Color32 a, Color32 b, float t)
        {
            return new Color32(LerpBustByte(a.r, b.r, t), LerpBustByte(a.g, b.g, t), LerpBustByte(a.b, b.b, t), LerpBustByte(a.a, b.a, t));
        }

        static byte LerpBustByte(byte a, byte b, float t)
        {
            return (byte)Mathf.Clamp(Mathf.RoundToInt(a + (b - a) * t), 0, 255);
        }

        static Color32 OverWhite(Color32 sample)
        {
            if (sample.a >= 255) return new Color32(sample.r, sample.g, sample.b, 255);
            int keep = 255 - sample.a;
            return new Color32(
                (byte)((sample.r * sample.a + 255 * keep) / 255),
                (byte)((sample.g * sample.a + 255 * keep) / 255),
                (byte)((sample.b * sample.a + 255 * keep) / 255),
                255);
        }

        float PersonRow(float y, float width, string text, string id, Action<string> focusCharacter)
        {
            GUI.Label(new Rect(0f, y, width - 120f, 36f), text, _small);
            if (!string.IsNullOrEmpty(id) && GUI.Button(new Rect(width - 108f, y, 96f, 32f), "詳細", _button))
                focusCharacter(id);
            return y + 40f;
        }

        float StaffRow(float y, float width, string role, StaffMember staff, Action<string> focusCharacter)
        {
            if (staff == null)
                return PersonRow(y, width, role + "  空き", null, focusCharacter);
            return PersonRow(
                y,
                width,
                role + "  " + staff.Name + "  " + staff.Age + "歳  Lv" + staff.Level
                + "  年俸 " + MoneyText.Format(staff.AnnualSalary),
                staff.Id,
                focusCharacter);
        }

        float StaffSlots(float y, float width, string role, StaffMember[] slots, Action<string> focusCharacter)
        {
            int count = slots == null ? 0 : slots.Length;
            if (count == 0) return StaffRow(y, width, role, null, focusCharacter);
            for (int i = 0; i < count; i++)
                y = StaffRow(y, width, role, slots[i], focusCharacter);
            return y;
        }

        void CollectFixtures(GameSession session, Club club, bool results)
        {
            _fixtures.Clear();
            _rows.Clear();
            AddFixtures(session, session.League.Fixtures, club, results);
            AddFixtures(session, session.Cup.Fixtures, club, results);
            _fixtures.Sort((a, b) => results ? b.Date.CompareTo(a.Date) : a.Date.CompareTo(b.Date));
            for (int i = 0; i < _fixtures.Count; i++)
                _rows.Add(ScheduleRow.FromFixture(club, _fixtures[i]));

            if (results || session.Cup == null) return;
            _pendingCup.Clear();
            session.Cup.AppendPending(club, session.Time.CurrentDate, _pendingCup);
            for (int i = 0; i < _pendingCup.Count; i++)
                _rows.Add(ScheduleRow.FromPending(_pendingCup[i]));
            _rows.Sort((a, b) => a.Date.CompareTo(b.Date));
        }

        void AddFixtures(GameSession session, IReadOnlyList<Fixture> source, Club club, bool results)
        {
            if (source == null) return;
            for (int i = 0; i < source.Count; i++)
            {
                var fixture = source[i];
                if (fixture == null || (fixture.Home != club && fixture.Away != club)) continue;
                if ((fixture.Result != null) != results) continue;
                if (!results && fixture.Date.Date < session.Time.CurrentDate.Date) continue;
                _fixtures.Add(fixture);
            }
        }

        void TryUpgrade(GameSession session, FacilityKind kind, Action<string> setStatus)
        {
            TryUpgradeNamed(session, kind, FacilityLabel(kind), setStatus);
        }

        void TryUpgradeNamed(GameSession session, FacilityKind kind, string label, Action<string> setStatus)
        {
            var before = session.PlayerClub.Facilities.Get(kind).Level;
            string message = session.OwnerApprovesFacilityUpgrade(kind)
                ? label + " を Lv" + (before + 1) + " にしました。"
                : label + " は強化できません。資金か上限を確認してください。";
            _notice = message;
            setStatus(message);
        }

        static string StadiumEffect(Club club)
        {
            return "収容 " + club.Facilities.StadiumCapacity()
                   + "  コア " + club.CoreSupporters
                   + "  ライト " + club.LightSupporters;
        }

        static string MarketRangeLabel(Club club)
        {
            int min = club.Facilities.MarketLevelFloor();
            int max = club.Facilities.MarketLevelCap();
            if (min == max) return "採用できるレベル Lv" + max;
            return "採用できるレベル Lv" + min + "〜Lv" + max;
        }

        static string FormatTrainingBonus(double percent)
        {
            return "+" + percent.ToString("0.0", CultureInfo.InvariantCulture) + "%";
        }

        static string TrainingGroundEffect(Club club)
        {
            return "攻守の練習効果 " + FormatTrainingBonus(club.Facilities.TrainingGroundBonusPercent());
        }

        static string SecretaryLine(GameSession session, Club club)
        {
            FacilityKind kind;
            if (club.Front == null || club.Front.Secretary == null)
                return "秘書は不在です。";
            if (!session.Facilities.SecretarySuggestsUpgrade(club, out kind))
                return "秘書の提案: いま支払える施設強化はありません。";
            return "秘書の提案: " + FacilityLabel(kind) + " をレベルアップできます。";
        }

        public static string FormatRating(Club club)
        {
            long sum = 0;
            var squad = club.Squad;
            for (int i = 0; i < squad.Count; i++)
            {
                if (squad[i] != null) sum += squad[i].DisplayOverall;
            }

            return sum.ToString("N0", CultureInfo.InvariantCulture);
        }

        static string Title(Kind kind)
        {
            switch (kind)
            {
                case Kind.Menu: return "MENU";
                case Kind.Stadium: return "スタジアム";
                case Kind.Training: return "練習場";
                default: return "クラブハウス";
            }
        }

        static string MatchKind(Fixture fixture)
        {
            if (fixture.IsLeague) return "リーグ";
            return "カップ" + fixture.CupRound + " 第" + fixture.Leg + "戦";
        }

        struct ScheduleRow
        {
            public DateTime Date;
            public string Label;

            public static ScheduleRow FromFixture(Club club, Fixture fixture)
            {
                bool home = fixture.Home == club;
                string side = home ? "ホーム" : "アウェイ";
                string opponent = home ? fixture.Away.Name : fixture.Home.Name;
                string score = "";
                if (fixture.Result != null)
                {
                    int mine = home ? fixture.Result.HomeGoals : fixture.Result.AwayGoals;
                    int theirs = home ? fixture.Result.AwayGoals : fixture.Result.HomeGoals;
                    score = "  " + mine + " - " + theirs;
                    if (fixture.Result.DecidedByPenalty) score += " PK";
                }

                return new ScheduleRow
                {
                    Date = fixture.Date,
                    Label = GameSession.FormatCalendarDate(fixture.Date) + "  " + MatchKind(fixture) + "  " + side + score + "  " + opponent
                };
            }

            public static ScheduleRow FromPending(CupScheduleLine line)
            {
                string side = line.Home ? "ホーム" : "アウェイ";
                return new ScheduleRow
                {
                    Date = line.Date,
                    Label = GameSession.FormatCalendarDate(line.Date)
                            + "  カップ" + line.Round + " 第" + line.Leg + "戦  " + side + "  " + line.Opponent
                };
            }
        }

        static string GradeSuffix(FacilityKind kind, int level)
        {
            if (kind != FacilityKind.Stadium && kind != FacilityKind.TrainingGround && kind != FacilityKind.Clubhouse)
                return "";
            return "  グレード" + MainScreenView.GradeForLevel(level);
        }

        static string FacilityLabel(FacilityKind kind)
        {
            switch (kind)
            {
                case FacilityKind.Stadium: return "スタジアム";
                case FacilityKind.Transport: return "交通機関";
                case FacilityKind.TrainingGround: return "練習場";
                case FacilityKind.TrainingFacility: return "トレーニング室";
                default: return "クラブハウス";
            }
        }

        static string FrontRoleLabel(FrontRole role)
        {
            switch (role)
            {
                case FrontRole.Secretary: return "秘書";
                case FrontRole.GeneralManager: return "GM";
                case FrontRole.Strengthening: return "強化部";
                case FrontRole.Sales: return "営業";
                case FrontRole.PublicRelations: return "広報";
                case FrontRole.Business: return "事業";
                case FrontRole.Academy: return "アカデミー";
                default: return "フロント";
            }
        }

        void EnsureStyles(Font font)
        {
            if (_title != null && _font == font) return;
            _font = font;
            _title = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 36,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            _body = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 24,
                wordWrap = true,
                normal = { textColor = Color.white }
            };
            _small = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 22,
                wordWrap = true,
                richText = true,
                normal = { textColor = Color.white }
            };
            _button = new GUIStyle(GUI.skin.button) { font = font, fontSize = 22 };
            _tab = new GUIStyle(GUI.skin.button) { font = font, fontSize = 20 };
            var empty = new RectOffset(0, 0, 0, 0);
            _hit = new GUIStyle
            {
                margin = empty,
                padding = empty,
                border = empty
            };
            _tabLabel = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 32,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperCenter,
                normal = { textColor = Ink }
            };
            _facilityName = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 40,
                alignment = TextAnchor.MiddleCenter,
                richText = true,
                normal = { textColor = Color.black }
            };
            _facilityEffect = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 32,
                alignment = TextAnchor.MiddleCenter,
                richText = true,
                normal = { textColor = Color.black }
            };
            _facilityCost = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 32,
                alignment = TextAnchor.MiddleCenter,
                richText = true,
                normal = { textColor = Color.black }
            };
            _levelButton = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 32,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Ink }
            };
            _headerNotice = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 22,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            _listHead = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 24,
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Clip,
                normal = { textColor = HeaderGray }
            };
            _posLabel = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 32,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Clip,
                normal = { textColor = Color.white }
            };
            _playerName = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 36,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                wordWrap = false,
                normal = { textColor = Ink }
            };
            _playerStat = new GUIStyle(_playerName)
            {
                alignment = TextAnchor.MiddleCenter
            };
            _radarValue = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 24,
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Clip,
                normal = { textColor = Color.black }
            };
            var tight = new RectOffset(2, 2, 0, 0);
            _tableHead = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                padding = tight,
                normal = { textColor = new Color(0.82f, 0.86f, 0.84f, 1f) }
            };
            _tableHeadLeft = new GUIStyle(_tableHead) { alignment = TextAnchor.MiddleLeft };
            _tableNum = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter,
                padding = tight,
                normal = { textColor = Color.white }
            };
            _tableClub = new GUIStyle(_tableNum)
            {
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip
            };
            _tableMine = new GUIStyle(_tableClub)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.93f, 0.62f, 1f) }
            };
            _frontButton = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 24,
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Clip,
                normal = { textColor = Color.black }
            };
            _pillText = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 24,
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Clip,
                normal = { textColor = Color.white }
            };
            _pillSmall = new GUIStyle(_pillText)
            {
                fontSize = 20
            };
            _pillRight = new GUIStyle(_pillText)
            {
                alignment = TextAnchor.MiddleRight
            };
            _frontConfirmText = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 28,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                normal = { textColor = Ink }
            };
        }

        void UseText(Color color)
        {
            _body.normal.textColor = color;
            _small.normal.textColor = color;
        }
    }
}
