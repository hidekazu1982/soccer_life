using System;
using System.Collections.Generic;
using System.Globalization;
using SoccerClub.Competition;
using SoccerClub.Data;
using SoccerClub.Match;
using SoccerClub.Time;
using SoccerClub.UI;
using UnityEngine;

namespace SoccerClub.Core
{
    /// <summary>
    /// Play 開始時にクラブを選び、メイン画面とオーナー操作を出す。
    /// </summary>
    public sealed class GameRunner : MonoBehaviour
    {
        public const int ReferenceWidth = 1920;
        public const int ReferenceHeight = 1080;
        const float DayAdvanceSeconds = 10f;
        const float SeasonHolidayAdvanceSeconds = 5f;
        const float ClosedPeriodAdvanceSeconds = 1f;
        const float ReviewSeconds = 5f;
        const float FullAutoNextSeconds = 0.45f;

        enum DayPhase
        {
            Idle,
            Lineup,
            Advancing,
            Stats,
            Settlement,
            MonthReport,
            SponsorCut,
            SponsorOffer,
            Story
        }

        static readonly Rect EmblemDesign = new Rect(31f, 32f, 293f, 220f);
        static Rect EmblemRect => ShiftLeft(PinTop(EmblemDesign));
        // main2 の frame_00。247 / 606 / 965 / 1324、高さ 100、幅 349、間隔 10。
        const float InfoFrameHeight = 100f;
        const float InfoFrameWidth = 349f;
        const float InfoFrameGap = 10f;
        const float InfoFrameTop = 32f;
        const float InfoRowX = 247f;
        static Rect DatePlateRect => PinTop(new Rect(InfoRowX, InfoFrameTop, InfoFrameWidth, InfoFrameHeight));
        static Rect RatingPlateRect => PinTop(new Rect(InfoRowX + InfoFrameWidth + InfoFrameGap, InfoFrameTop, InfoFrameWidth, InfoFrameHeight));
        static Rect FundsPlateRect => PinTop(new Rect(InfoRowX + (InfoFrameWidth + InfoFrameGap) * 2f, InfoFrameTop, InfoFrameWidth, InfoFrameHeight));
        static Rect SalaryPlateRect => PinTop(new Rect(InfoRowX + (InfoFrameWidth + InfoFrameGap) * 3f, InfoFrameTop, InfoFrameWidth, InfoFrameHeight));
        static Rect MenuButtonRect => PinTop(new Rect(ReferenceWidth - 48f - 156f, 36f, 156f, 156f));
        const float DayChromeScale = 0.5f;
        const float DayButtonWidth = 599f * DayChromeScale;
        const float DayButtonHeight = 235f * DayChromeScale;
        const float DayGaugeWidth = 599f * DayChromeScale;
        const float DayGaugeHeight = 87f * DayChromeScale;
        const float DayGaugeFillOrigin = 51f * DayChromeScale;
        const float DayGaugeFillSpan = 498f * DayChromeScale;
        // day_off。Figma main2 上の 572,0 / 776×574。文字は看板画像とは別レイヤー。
        const float DayOffX = 572f;
        const float DayOffWidth = 776f;
        const float DayOffHeight = 574f;
        const float DayOffSlideSeconds = 0.6f;
        static readonly Rect DayOffTitleRect = new Rect(832f, 269f, 256f, 155f);
        static readonly Rect DayOffSubtitleRect = new Rect(821f, 424f, 278f, 77f);
        static readonly Rect DayButtonDesign = new Rect(ReferenceWidth - 36f - DayButtonWidth, ReferenceHeight - 20f - DayButtonHeight, DayButtonWidth, DayButtonHeight);
        static Rect DayButtonRect => PinBottom(DayButtonDesign);
        // main2 の日送り。1557,865 / 1557,918 / 1422,988。試合画面とリザルトは DayButtonRect のまま。
        // main の auto（176:44）。ゲージ 1557,879 / 212×30.7、AUTO はその右 1769,850。
        static Rect MainGaugeRect => ShiftRight(PinBottom(new Rect(1557f, 879f, 212f, 30.74f)));
        static Rect MainDayButtonRect => ShiftRight(PinBottom(new Rect(1557f, 918f, 331f, 130f)));
        static Rect MainAutoRect => ShiftRight(PinBottom(new Rect(1769f, 850f, 119f, 60f)));
        // デバッグビルドだけの「フル」と「デバッグ」。どの画面でも左下に並べる（フル 36,992、デバッグはその右）。
        static Rect DebugFullAutoRect => ShiftLeft(PinBottom(new Rect(36f, 992f, 119f, 60f)));
        static Rect DebugButtonRect => ShiftLeft(PinBottom(new Rect(36f + 119f + AutoDesignGap, 992f, 119f, 60f)));
        // 以前の配置で渡していた基準。FullAutoRect は位置を無視して DebugFullAutoRect を返す。
        static Rect MainFullAutoAnchor => MainAutoRect;
        // game2（試合中）の auto。ゲージ 1559,1017 / 212×30.7、AUTO はその右 1771,988。
        static Rect MatchGaugeRect => ShiftRight(PinBottom(new Rect(1559f, 1017f, 212f, 30.74f)));
        static Rect MatchAutoRect => ShiftRight(PinBottom(new Rect(1771f, 988f, 119f, 60f)));
        // meeting_room の AUTO は従来どおりゲージの左 1422,988。
        static Rect MeetingAutoRect => ShiftRight(PinBottom(new Rect(1422f, 988f, 119f, 60f)));
        static Rect MeetingGaugeRect => ShiftRight(new Rect(1557f, 988f, 331f, 48f));
        // sponsor の auto。ゲージ 1557,1017 / 212×30.7、AUTO はその右 1769,988。
        static Rect SponsorGaugeRect => ShiftRight(PinBottom(new Rect(1557f, 1017f, 212f, 30.74f)));
        static Rect SponsorAutoRect => ShiftRight(PinBottom(new Rect(1769f, 988f, 119f, 60f)));
        static Rect SponsorFullAutoAnchor => ShiftRight(PinBottom(new Rect(1557f, 988f, 119f, 60f)));
        // match_preparation の日送り。Figma の 1573,879 / 1573,932 / 1438,1002。
        static Rect LineupGaugeRect => PinBottom(new Rect(1573f, 879f, 331f, 48f));
        static Rect LineupDayButtonRect => PinBottom(new Rect(1573f, 932f, 331f, 130f));
        static Rect LineupAutoRect => PinBottom(new Rect(1438f, 1002f, 119f, 60f));
        static Rect DayBarRect => new Rect(DayButtonRect.x, DayButtonRect.y - 8f * DayChromeScale - DayGaugeHeight, DayGaugeWidth, DayGaugeHeight);
        // button_gameday の中身。枠そのものは日送りボタンと同じ DayButtonRect。
        const float GameDayDesignWidth = 331f;
        const float GameDayDesignHeight = 130f;
        // button_auto は日送りボタンの左。Figma では下端を揃え、間隔は 16。
        const float AutoDesignWidth = 119f;
        const float AutoDesignHeight = 60f;
        const float AutoDesignGap = 16f;
        static readonly Color GameDayTextColor = new Color(58f / 255f, 6f / 255f, 0f, 1f);
        static readonly Rect DetailDesign = new Rect(36f, 220f, 460f, 680f);
        static Rect DetailRect => KeepOnScreen(DetailDesign);

        public GameSession Session { get; private set; }

        string _status = "右下の「1日進める」で日が進みます。シーズン中の休日は5秒、ウィンターブレイクとオフシーズンは1秒です。";
        float _dayElapsed = -1f;
        float _growthCursor;
        bool _matchScreen;
        DayPhase _phase;
        int _coreDelta;
        int _lightDelta;
        int _postMatchStepFrame = -1;
        Fixture _liveFixture;
        DateTime _playerFixtureDate;
        bool _playerFixtureCached;
        Fixture _playerFixture;
        readonly Dictionary<string, int> _dayDeltas = new Dictionary<string, int>();
        readonly HashSet<string> _groundAbsentees = new HashSet<string>();
        DateTime _squadDepartureDate;
        readonly MatchScreenView _matchScreenView = new MatchScreenView();
        readonly MatchPreparationView _lineupView = new MatchPreparationView();
        TeamSheet _lineupSheet;
        readonly MonthReportView _monthReportView = new MonthReportView();
        readonly SponsorOfferView _sponsorOfferView = new SponsorOfferView();
        readonly GrowthLogView _growthLog = new GrowthLogView();
        // その日のうちにログへ出す予定。グラウンドの増減ポップと同じ時刻に出す。
        readonly List<GrowthLogItem> _growthLogQueue = new List<GrowthLogItem>();

        struct GrowthLogItem
        {
            public float At;
            public string Name;
            public GrowthStat Stat;
            public int Delta;
        }
        readonly SponsorCutView _sponsorCutView = new SponsorCutView();
        readonly ClubEventView _eventView = new ClubEventView();
        readonly DebugMenuView _debugMenu = new DebugMenuView();
        readonly MeetingRoomView _meeting = new MeetingRoomView();
        bool _meetingClock;
        bool _meetingSuccess;
        int _storyStepFrame = -1;
        readonly ClubDialogView _dialogs = new ClubDialogView();
        bool _suppressOwnerOpen;
        readonly ClubSelectView _clubSelect = new ClubSelectView();
        readonly ScreenBgm _bgm = new ScreenBgm();
        MainScreenView _mainScreen;
        GroundCrowd _crowd;
        GUIStyle _titleStyle;
        GUIStyle _bodyStyle;
        GUIStyle _smallStyle;
        GUIStyle _buttonStyle;
        GUIStyle _plateLabelStyle;
        GUIStyle _plateLabelLeft;
        GUIStyle _plateLabelRight;
        GUIStyle _plateValueStyle;
        GUIStyle _noticeStyle;
        GUIStyle _dayButtonStyle;
        GUIStyle _gameDayTitleStyle;
        GUIStyle _gameDaySubtitleStyle;
        GUIStyle _dayOffTitleStyle;
        GUIStyle _dayOffSubtitleStyle;
        GUIStyle _volumeLabelStyle;
        Texture2D _infoFrame;
        Texture2D _circleTex;
        Texture2D _nextDayButton;
        Texture2D _gameDayButton;
        Texture2D _autoOff;
        Texture2D _autoOn;
        bool _autoPlay;
        bool _fullAuto;
        System.Numerics.BigInteger _payrollBeforeGrowth;
        bool _holdPayroll;
        Texture2D _dayGaugeFrame;
        Texture2D _dayGaugeTrack;
        Texture2D _dayGaugeFill;
        Texture2D _dayOff;
        float _dayOffShown;
        float _viewZoom = 1f;
        Vector2 _viewOrigin;
        readonly MiniEventDirector _miniEvents = new MiniEventDirector();
        Font _font;
        string _bootError;
        bool _worldUiReady;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Application.isBatchMode) return;
            if (FindAnyObjectByType<GameRunner>() != null) return;
            var go = new GameObject("SoccerClubSession");
            DontDestroyOnLoad(go);
            go.AddComponent<GameRunner>();
        }

        void Awake()
        {
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
#if !UNITY_EDITOR && !UNITY_ANDROID
            var res = Screen.currentResolution;
            if (res.width > 0 && res.height > 0)
                Screen.SetResolution(res.width, res.height, FullScreenMode.FullScreenWindow);
#endif
            try
            {
                _font = JapaneseFont.Resolve();
                _clubSelect.Load();
                SetupDebugMenu();
                _mainScreen = new MainScreenView();
                _infoFrame = Resources.Load<Texture2D>("MainScreen/frame_00");
                _nextDayButton = Resources.Load<Texture2D>("MainScreen/button_nextday");
                _gameDayButton = Resources.Load<Texture2D>("MainScreen/button_gameday");
                _autoOff = Resources.Load<Texture2D>("MainScreen/button_auto_off");
                _autoOn = Resources.Load<Texture2D>("MainScreen/button_auto_on");
                _dayGaugeFrame = Resources.Load<Texture2D>("MainScreen/gage_frame_nextday");
                _dayGaugeTrack = Resources.Load<Texture2D>("MainScreen/gage_framebg_nextday");
                _dayGaugeFill = Resources.Load<Texture2D>("MainScreen/gage_bar_nextday");
                _dayOff = Resources.Load<Texture2D>("MainScreen/day_off");
                if (GameSaveStore.TryRead(out var restored))
                {
                    AttachSession(restored);
                    _status = "前回の続きから始めます。";
                    Debug.Log("SoccerClub: restored " + Session.PlayerClub.Name + "  coins=" + Session.PlayerClub.Coins);
                }
            }
            catch (Exception exception)
            {
                _bootError = exception.ToString();
                Debug.LogException(exception);
            }
        }

        void EnsureWorldUi()
        {
            if (_worldUiReady) return;
            if (_mainScreen == null)
                _mainScreen = new MainScreenView();
            _mainScreen.Load();
            _dialogs.Load();
            _worldUiReady = true;
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) TryAutoSave();
        }

        void OnApplicationQuit()
        {
            TryAutoSave();
        }

        void BeginGame(int clubIndex)
        {
            try
            {
                AttachSession(GameSession.CreateNew(clubIndex));
                if (!GameSaveStore.Write(Session))
                    _status = "オートセーブに失敗しました。";
                Debug.Log("SoccerClub: " + Session.PlayerClub.Name + "  coins=" + Session.PlayerClub.Coins);
            }
            catch (Exception exception)
            {
                _bootError = exception.ToString();
                Debug.LogException(exception);
            }
        }

        void AttachSession(GameSession session)
        {
            EnsureWorldUi();
            if (Session != null) Session.StateChanged -= OnSessionChanged;
            Session = session;
            _growthLog.Clear();
            _growthLogQueue.Clear();
            Session.StateChanged += OnSessionChanged;
            _playerFixtureCached = false;
            _squadDepartureDate = DateTime.MinValue;
            _crowd = new GroundCrowd();
            _crowd.Load();
            TickCrowd(0f);
            if (Session.HasMonthReport)
            {
                _phase = DayPhase.MonthReport;
                _dayElapsed = -1f;
                _status = "収支報告";
            }
            else if (Session.HasSponsorCut)
            {
                _phase = DayPhase.SponsorCut;
                _dayElapsed = -1f;
                _status = "スポンサー打ち切り";
            }
            else if (Session.HasSponsorOffer)
            {
                _phase = DayPhase.SponsorOffer;
                _dayElapsed = -1f;
                _status = "スポンサー獲得";
            }
        }

        void OnSessionChanged()
        {
            TryAutoSave();
        }

        void TryAutoSave()
        {
            if (Session == null || _dayElapsed >= 0f || _phase != DayPhase.Idle) return;
            if (!GameSaveStore.Write(Session))
                _status = "オートセーブに失敗しました。";
        }

        void ReturnToClubSelect()
        {
            GameSaveStore.Delete();
            if (GameSaveStore.Exists)
            {
                _status = "セーブデータを消去できませんでした。";
                _dialogs.Open(ClubDialogView.Kind.Menu);
                return;
            }

            if (Session != null) Session.StateChanged -= OnSessionChanged;
            Session = null;
            _crowd = null;
            _squadDepartureDate = DateTime.MinValue;
            _dayElapsed = -1f;
            _growthCursor = 0f;
            _autoPlay = false;
            _fullAuto = false;
            _holdPayroll = false;
            _matchScreen = false;
            _phase = DayPhase.Idle;
            _lineupSheet = null;
            _liveFixture = null;
            _playerFixtureCached = false;
            _dayDeltas.Clear();
            _dialogs.Close();
            _status = "右下の「1日進める」で日が進みます。シーズン中の休日は5秒、ウィンターブレイクとオフシーズンは1秒です。";
        }

        void TickCrowd(float deltaTime)
        {
            if (_crowd == null || Session == null) return;
            if (Session.IsClosedPeriod(Session.Time.CurrentDate))
            {
                _crowd.ClearSelection();
                return;
            }

            var date = Session.Time.CurrentDate.Date;
            bool stageDeparture = false;
            bool hideNonSquad = false;
            if (date.DayOfWeek == DayOfWeek.Sunday && _squadDepartureDate != date)
            {
                _squadDepartureDate = date;
                // 前日の土曜が練習日のときだけ、選手を出してから退場させる。試合日の翌日は選手もスタッフも最初から退場済み。
                bool matchYesterday = Session.ClubHasMatchOn(Session.PlayerClub, date.AddDays(-1));
                stageDeparture = !matchYesterday;
                hideNonSquad = matchYesterday;
            }

            Session.CopyGroundAbsentees(Session.PlayerClub, _groundAbsentees);
            _crowd.Tick(deltaTime, Session.PlayerClub, _groundAbsentees, stageDeparture, hideNonSquad);
        }

        void UpdateBgm()
        {
            if (_bootError != null) return;
            _bgm.Ensure(gameObject);
            _dialogs.Bgm = _bgm;
            // スタッツを挟んでも、試合の再生からリザルトまで同じ曲を止めない。
            bool match = Session != null
                && (_phase == DayPhase.Lineup
                    || (_liveFixture != null
                        && ((_matchScreen && _phase == DayPhase.Advancing)
                            || _phase == DayPhase.Stats
                            || _phase == DayPhase.Settlement)));
            _bgm.Tick(match, UnityEngine.Time.deltaTime);
        }

        void UpdateDayOff()
        {
            bool off = Session != null && Session.ShowsHolidayBoard(Session.Time.CurrentDate);
            float target = off ? 1f : 0f;
            float step = DayOffSlideSeconds <= 0f ? 1f : UnityEngine.Time.deltaTime / DayOffSlideSeconds;
            _dayOffShown = Mathf.MoveTowards(_dayOffShown, target, step);
        }

        void Update()
        {
            UpdateBgm();
            UpdateDayOff();
            UpdateViewCamera(UnityEngine.Time.deltaTime);
            if (_crowd != null && Session != null)
            {
                TickCrowd(UnityEngine.Time.deltaTime);
                bool hideCast = Session.IsClosedPeriod(Session.Time.CurrentDate);
                if (!CoveringScreen && !hideCast)
                {
                    _pinChrome = false;
                    var world = ScreenTo(MainViewMatrix(), Input.mousePosition);
                    var chrome = ScreenTo(UiFitMatrix(), Input.mousePosition);
                    bool down = Input.GetMouseButtonDown(0);
                    bool held = Input.GetMouseButton(0);
                    bool up = Input.GetMouseButtonUp(0);
                    if (down)
                        _suppressOwnerOpen = ShouldSuppressOwnerOpen(chrome, world);
                    else if (!held && !up)
                        _suppressOwnerOpen = false;
                    _crowd.HandlePointer(world, down, held, up, PointerBlocked(chrome, world));
                }
                else if (!Input.GetMouseButton(0) && !Input.GetMouseButtonUp(0))
                {
                    _suppressOwnerOpen = false;
                }
            }
            if (Session == null || (Session.GameOver && _phase != DayPhase.MonthReport && _phase != DayPhase.SponsorCut && _phase != DayPhase.SponsorOffer))
            {
                _dayElapsed = -1f;
                _phase = DayPhase.Idle;
                _matchScreen = false;
                _lineupSheet = null;
                _holdPayroll = false;
                return;
            }

            if (_fullAuto && _dialogs.IsOpen)
                _dialogs.Close();

            if (_phase == DayPhase.Lineup)
            {
                if (_autoPlay)
                {
                    if (_dayElapsed < 0f) _dayElapsed = 0f;
                    _dayElapsed += UnityEngine.Time.deltaTime;
                    if (_dayElapsed >= NextSeconds(ReviewSeconds))
                        ConfirmLineup();
                }
            }
            else if (_phase == DayPhase.Story)
            {
                if (IsMeetingRoom())
                {
                    BeginMeetingClock();
                    _dayElapsed += UnityEngine.Time.deltaTime;
                    if (_dayElapsed >= MeetingResultAt())
                    {
                        Session.ApplyOpenStory();
                        _meetingSuccess = IsTransferMeeting()
                            ? Session.TransferDeparted()
                            : Session.SigningJoined();
                    }

                    if (_dayElapsed >= MeetingDuration())
                        FinishMeeting();
                }
                else if (_autoPlay)
                {
                    if (_dayElapsed < 0f) _dayElapsed = 0f;
                    _dayElapsed += UnityEngine.Time.deltaTime;
                    float wait = _fullAuto && NeedsManualNext() ? FullAutoNextSeconds : StorySeconds();
                    if (_dayElapsed >= wait)
                        AdvanceAutoStory();
                }
            }
            else if (_phase == DayPhase.Stats || _phase == DayPhase.Settlement)
            {
                if (_autoPlay)
                {
                    if (_dayElapsed < 0f) _dayElapsed = 0f;
                    _dayElapsed += UnityEngine.Time.deltaTime;
                    if (_dayElapsed >= NextSeconds(ReviewSeconds))
                        ContinuePostMatch();
                }
            }
            else if (_phase == DayPhase.MonthReport || _phase == DayPhase.SponsorCut || _phase == DayPhase.SponsorOffer)
            {
                if (_autoPlay)
                {
                    if (_dayElapsed < 0f) _dayElapsed = 0f;
                    _dayElapsed += UnityEngine.Time.deltaTime;
                    if (_dayElapsed >= NextSeconds(DayAdvanceSeconds))
                        ContinuePostMatch();
                }
            }
            else if (_phase == DayPhase.Advancing && _dayElapsed >= 0f)
            {
                _dayElapsed += UnityEngine.Time.deltaTime;
                float progressed = Mathf.Clamp01(_dayElapsed / AdvanceSeconds());
                float slice = progressed - _growthCursor;
                if (slice > 0f)
                {
                    Session.ApplyGrowth(slice);
                    _growthCursor = progressed;
                }

                FlushGrowthLog(_dayElapsed);

                if (_dayElapsed >= AdvanceSeconds())
                {
                    _dayElapsed = AdvanceSeconds();
                    if (_growthCursor < 1f)
                    {
                        Session.ApplyGrowth(1f - _growthCursor);
                        _growthCursor = 1f;
                    }

                    FlushGrowthLog(float.MaxValue);

                    _holdPayroll = false;
                    if (_matchScreen && _liveFixture != null && _liveFixture.Result != null)
                        BeginStats();
                    else
                        FinishAdvancedDay();
                }
            }

            bool seasonNotice = Session != null && !string.IsNullOrEmpty(Session.SeasonNotice);
            bool holdDialog = _dialogs.IsOpen && !_fullAuto;
            bool holdNotice = seasonNotice && !_fullAuto;
            if (_phase == DayPhase.Idle && Session != null && Session.CurrentStory != null && !_dialogs.IsOpen)
                OpenStoryPhase();
            else if (_autoPlay && _phase == DayPhase.Idle && _dayElapsed < 0f && !holdDialog && !holdNotice)
                BeginDayAdvance();
        }

        bool CoveringScreen =>
            _phase == DayPhase.Lineup || _phase == DayPhase.Stats || _phase == DayPhase.Settlement || _phase == DayPhase.MonthReport
            || _phase == DayPhase.SponsorCut || _phase == DayPhase.SponsorOffer || _phase == DayPhase.Story || _matchScreen;

        float PaceSeconds()
        {
            if (Session == null) return DayAdvanceSeconds;
            var date = Session.Time.CurrentDate;
            if (Session.IsClosedPeriod(date)) return ClosedPeriodAdvanceSeconds;
            if (date.DayOfWeek == DayOfWeek.Sunday) return SeasonHolidayAdvanceSeconds;
            return DayAdvanceSeconds;
        }

        float AdvanceSeconds()
        {
            if (_matchScreen && _liveFixture != null)
                return MatchScreenView.Duration(_liveFixture, PaceSeconds());
            return PaceSeconds();
        }

        float StorySeconds()
        {
            var story = Session != null ? Session.CurrentStory : null;
            if (story != null && (story.Kind == ClubStoryKind.Transfer
                || story.Kind == ClubStoryKind.Signing
                || story.Kind == ClubStoryKind.NewYear))
                return ReviewSeconds;
            return DayAdvanceSeconds;
        }

        bool CanAutoStory()
        {
            var story = Session != null ? Session.CurrentStory : null;
            return story != null
                && (story.Kind == ClubStoryKind.Transfer
                    || story.Kind == ClubStoryKind.Signing
                    || story.Kind == ClubStoryKind.NewYear);
        }

        bool NeedsManualNext()
        {
            return Session != null && Session.CurrentStory != null && !CanAutoStory();
        }

        float NextSeconds(float normal)
        {
            return _fullAuto ? FullAutoNextSeconds : normal;
        }

        void AdvanceAutoStory()
        {
            var story = Session != null ? Session.CurrentStory : null;
            if (_fullAuto && story != null && story.Kind == ClubStoryKind.Stadium && Session.FullAutoAcceptsStadiumUpgrade())
                Session.OwnerApprovesFacilityUpgrade(FacilityKind.Stadium);
            AdvanceOpenStory();
        }

        void OnGUI()
        {
            if (_bootError != null)
            {
                GUI.Label(new Rect(24f, 24f, Screen.width - 48f, Screen.height - 48f), _bootError);
                return;
            }

            EnsureStyles();
            var previousMatrix = GUI.matrix;
            var previousColor = GUI.color;

            GUI.matrix = Matrix4x4.identity;
            GUI.color = new Color(0.74f, 0.86f, 0.93f, 1f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            if (Session == null)
            {
                UseUiScreen();
                if (_clubSelect.Draw(_font, out int chosen))
                    BeginGame(chosen);
                GUI.matrix = previousMatrix;
                GUI.color = previousColor;
                return;
            }

            var club = Session.PlayerClub;
            if (_phase == DayPhase.Lineup)
            {
                UseUiScreen();
                var emblem = _clubSelect.EmblemTexture(club.EmblemIndex - 1);
                _lineupView.Draw(club, _lineupSheet, _crowd, emblem, _font);
                DrawLineupChrome();
                GUI.matrix = previousMatrix;
                GUI.color = previousColor;
                return;
            }

            if (_phase == DayPhase.Stats && _liveFixture != null)
            {
                UseUiScreen();
                _matchScreenView.DrawStats(_liveFixture, club, _font);
                DrawReviewChrome();
                GUI.matrix = previousMatrix;
                GUI.color = previousColor;
                return;
            }

            if (_phase == DayPhase.Settlement && _liveFixture != null)
            {
                UseUiScreen();
                _matchScreenView.DrawSettlement(_liveFixture, club, _coreDelta, _lightDelta, _font);
                DrawReviewChrome();
                GUI.matrix = previousMatrix;
                GUI.color = previousColor;
                return;
            }

            if (_phase == DayPhase.MonthReport && Session.PendingMonthReport != null)
            {
                UseUiScreen();
                _monthReportView.Draw(Session.PendingMonthReport, _font);
                DrawReviewChrome();
                GUI.matrix = previousMatrix;
                GUI.color = previousColor;
                return;
            }

            if (_phase == DayPhase.SponsorCut && Session.PendingSponsorCut != null)
            {
                UseUiScreen();
                _sponsorCutView.Draw(Session.PendingSponsorCut, _font);
                DrawReviewChrome();
                GUI.matrix = previousMatrix;
                GUI.color = previousColor;
                return;
            }

            if (_phase == DayPhase.SponsorOffer && Session.PendingSponsorOffer != null)
            {
                UseUiScreen();
                var choice = _sponsorOfferView.Draw(Session.PendingSponsorOffer, Session, _font);
                float sponsorProgress = _autoPlay && _dayElapsed >= 0f
                    ? Mathf.Clamp01(_dayElapsed / NextSeconds(DayAdvanceSeconds))
                    : 0f;
                DrawDayGauge(SponsorGaugeRect, sponsorProgress);
                DrawAutoButton(SponsorAutoRect);
                DrawFullAutoButton(SponsorFullAutoAnchor);
                if (choice == SponsorChoice.Accept) AcceptSponsorOffer();
                else if (choice == SponsorChoice.Decline) DeclineSponsorOffer();
                GUI.matrix = previousMatrix;
                GUI.color = previousColor;
                return;
            }

            if (_phase == DayPhase.Story)
            {
                if (Session.CurrentStory == null)
                    _phase = DayPhase.Idle;
                else if (IsMeetingRoom())
                {
                    UseUiScreen();
                    float elapsed = _dayElapsed < 0f ? 0f : _dayElapsed;
                    bool outcome = _meetingClock ? _meetingSuccess : PredictedMeetingOutcome();
                    _meeting.Draw(Session.CurrentStory, Session, _font, elapsed, outcome);
                    float duration = IsTransferMeeting()
                        ? MeetingRoomView.TransferDuration(outcome)
                        : MeetingRoomView.Duration(outcome);
                    DrawDayGauge(MeetingGaugeRect, duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration));
                    DrawAutoButton(MeetingAutoRect);
                    DrawFullAutoButton(MeetingAutoRect);
                    GUI.matrix = previousMatrix;
                    GUI.color = previousColor;
                    return;
                }
                else
                {
                    UseUiScreen();
                    if (_eventView.Draw(Session.CurrentStory, Session, _font))
                        AdvanceOpenStory();
                    if (CanAutoStory())
                        DrawPersonnelAuto();
                    GUI.matrix = previousMatrix;
                    GUI.color = previousColor;
                    return;
                }
            }

            if (_matchScreen && _phase == DayPhase.Advancing && _dayElapsed >= 0f && _liveFixture != null)
            {
                UseUiScreen();
                float progress = _dayElapsed <= 0f ? 0f : Mathf.Clamp01(_dayElapsed / AdvanceSeconds());
                _matchScreenView.Draw(_liveFixture, progress, club, _font);
                if (_crowd != null)
                    _crowd.DrawGrowthRow(new Rect(48f, 900f, 1120f, 150f), _dayElapsed);
                DrawDayGauge(MatchGaugeRect, Mathf.Clamp01(_dayElapsed / AdvanceSeconds()));
                DrawAutoButton(MatchAutoRect);
                DrawFullAutoButton(MatchAutoRect);
                GUI.matrix = previousMatrix;
                GUI.color = previousColor;
                return;
            }

            UseMainWorld();
            _mainScreen.DrawScenery(club);
            bool hideCast = Session.IsClosedPeriod(Session.Time.CurrentDate);
            string talking = _miniEvents.Active ? _miniEvents.SpeakerId : null;
            if (_crowd != null && !hideCast)
                _crowd.Draw(_dayElapsed, true, talking);
            _mainScreen.DrawClubhouse(club);
            if (_crowd != null && !hideCast)
                _crowd.Draw(_dayElapsed, false, talking);
            var worldMouse = MouseInReference();

            UseMainChrome();
            var chromeMouse = MouseInReference();
            bool dialogOpen = _dialogs.IsOpen;
            bool overDialog = false;
            if (dialogOpen)
            {
                UseUiScreen();
                overDialog = _dialogs.ActiveBounds.Contains(MouseInReference());
                UseMainChrome();
            }

            bool overPlayer = !hideCast && _crowd != null && _crowd.IsPointerBusy(worldMouse);
            bool overDetail = !dialogOpen && _crowd != null && _crowd.SelectedId != null && DetailRect.Contains(chromeMouse);
            bool overChrome = EmblemRect.Contains(chromeMouse)
                || HitsInfoPlate(chromeMouse)
                || MenuButtonRect.Contains(chromeMouse)
                || HitsMainDayButton(chromeMouse)
                || HitsMainAutoButton(chromeMouse)
                || HitsMainFullAutoButton(chromeMouse)
                || HitsDebugMenu(chromeMouse)
                || HitsMainDayGauge(chromeMouse)
                || overPlayer
                || overDetail
                || overDialog;
            if (!dialogOpen && !_suppressOwnerOpen && !overChrome)
            {
                UseMainWorld();
                if (_mainScreen.DrawBuildingHits(club, out var picked))
                    OpenPlace(PlaceKind(picked));
                UseMainChrome();
            }

            bool openBefore = dialogOpen;
            if (!_dialogs.IsOpen && _crowd != null && _crowd.SelectedId != null)
                DrawCharacterDetail(club, _crowd.SelectedId);

            bool clubhouseDialog = _dialogs.Current == ClubDialogView.Kind.Clubhouse;
            if (Event.current.type == EventType.Repaint)
                _growthLog.Tick(UnityEngine.Time.unscaledDeltaTime);
            if (!_dialogs.IsOpen && (_crowd == null || _crowd.SelectedId == null))
                _growthLog.Draw(GrowthLogOrigin, _font);
            DrawEmblem(club);
            DrawDayOff();
            if (!hideCast)
                DrawCharacterMessage(club);
            if (!clubhouseDialog)
                DrawInfoPlates(club);
            DrawMenuButton();
            DrawAutoButton(MainAutoRect);
            DrawFullAutoButton(MainFullAutoAnchor);
            DrawDebugMenu();
            DrawDayControl();
            if (_dialogs.IsOpen)
            {
                UseUiScreen();
                _dialogs.Draw(Session, _font, _status, message => _status = message, FocusCharacter);
                UseMainChrome();
            }
            if (_dialogs.TakeResetRequest())
            {
                ReturnToClubSelect();
                GUI.matrix = previousMatrix;
                GUI.color = previousColor;
                return;
            }

            if (clubhouseDialog)
                DrawInfoPlates(club);
            DrawSeasonNotice();
            ClosePlaceOnOutsideClick(openBefore);

            GUI.matrix = previousMatrix;
            GUI.color = previousColor;
        }

        void OpenPlace(ClubDialogView.Kind kind)
        {
            _dialogs.Open(kind);
            if (_crowd != null) _crowd.ClearSelection();
        }

        void FocusCharacter(string id)
        {
            _dialogs.Close();
            if (_crowd != null) _crowd.Select(id);
        }

        static ClubDialogView.Kind PlaceKind(FacilityKind kind)
        {
            switch (kind)
            {
                case FacilityKind.Stadium: return ClubDialogView.Kind.Stadium;
                case FacilityKind.TrainingGround: return ClubDialogView.Kind.Training;
                default: return ClubDialogView.Kind.Clubhouse;
            }
        }

        void ClosePlaceOnOutsideClick(bool wasOpen)
        {
            if (!wasOpen || !_dialogs.IsOpen) return;
            var click = Event.current;
            if (click.type != EventType.MouseUp || click.button != 0) return;
            var ui = ScreenTo(UiFitMatrix(), Input.mousePosition);
            if (_dialogs.ActiveBounds.Contains(ui)) return;
            var chrome = ui;
            if (MenuButtonRect.Contains(chrome) || HitsMainDayButton(chrome) || HitsMainAutoButton(chrome) || HitsMainFullAutoButton(chrome))
                return;
            if (HitsMainDayGauge(chrome)) return;
            _dialogs.Close();
            click.Use();
        }

        /// <summary>
        /// 横幅 1920 を画面の左右端に合わせる。16:9 より横長のときは上下が隠れる。
        /// UI は <see cref="UiFitMatrix"/> で、隠れる割合に合わせて均一に縮小する。
        /// </summary>
        internal static Matrix4x4 GuiScaleMatrix()
        {
            float scale = Screen.width / (float)ReferenceWidth;
            float oy = (Screen.height - ReferenceHeight * scale) * 0.5f;
            return Matrix4x4.TRS(new Vector3(0f, oy, 0f), Quaternion.identity, new Vector3(scale, scale, 1f));
        }

        /// <summary>
        /// 上下が隠れる割合だけ UI を均一に縮小し、画面の中に収める。
        /// 横幅 1920 の絵は <see cref="GuiScaleMatrix"/> 側で左右の端に合わせたままにする。
        /// </summary>
        internal static Matrix4x4 UiFitMatrix()
        {
            float scale = Mathf.Min(Screen.width / (float)ReferenceWidth, Screen.height / (float)ReferenceHeight);
            float ox = (Screen.width - ReferenceWidth * scale) * 0.5f;
            float oy = (Screen.height - ReferenceHeight * scale) * 0.5f;
            return Matrix4x4.TRS(new Vector3(ox, oy, 0f), Quaternion.identity, new Vector3(scale, scale, 1f));
        }

        Matrix4x4 MainViewMatrix()
        {
            var view = GuiScaleMatrix();
            if (_viewZoom <= 1.001f) return view;
            float zoom = _viewZoom;
            var pan = Matrix4x4.TRS(
                new Vector3(-_viewOrigin.x * zoom, -_viewOrigin.y * zoom, 0f),
                Quaternion.identity,
                new Vector3(zoom, zoom, 1f));
            return view * pan;
        }

        /// <summary>地面の基準座標を、ズームしていない UI 座標へ変換する。</summary>
        Vector2 WorldToChrome(Vector2 world)
        {
            float zoom = Mathf.Max(1f, _viewZoom);
            float wx = (world.x - _viewOrigin.x) * zoom;
            float wy = (world.y - _viewOrigin.y) * zoom;
            float worldScale = Screen.width / (float)ReferenceWidth;
            float worldOffsetY = (Screen.height - ReferenceHeight * worldScale) * 0.5f;
            float screenX = wx * worldScale;
            float screenY = worldOffsetY + wy * worldScale;
            float uiScale = Mathf.Min(worldScale, Screen.height / (float)ReferenceHeight);
            if (uiScale <= 0f) return world;
            float offsetX = (Screen.width - ReferenceWidth * uiScale) * 0.5f;
            float offsetY = (Screen.height - ReferenceHeight * uiScale) * 0.5f;
            return new Vector2((screenX - offsetX) / uiScale, (screenY - offsetY) / uiScale);
        }

        void DrawCharacterMessage(Club club)
        {
            if (club == null || _crowd == null || !_miniEvents.Active) return;
            Vector2 head;
            if (!_crowd.TryHeadPoint(_miniEvents.SpeakerId, out head)) return;
            _miniEvents.Draw(
                WorldToChrome(head),
                _font,
                AvatarLibrary.UniformColor(club.EmblemIndex),
                AvatarLibrary.NumberColor(club.EmblemIndex));
        }

        /// <summary>上下が隠れているとき、基準座標で上下それぞれ何 px 分か。</summary>
        internal static float VerticalInset
        {
            get
            {
                if (Screen.width <= 0) return 0f;
                float scale = Screen.width / (float)ReferenceWidth;
                float visible = Screen.height / scale;
                return Mathf.Max(0f, (ReferenceHeight - visible) * 0.5f);
            }
        }

        static bool _pinChrome = true;

        void UseUiScreen()
        {
            _pinChrome = false;
            GUI.matrix = UiFitMatrix();
        }

        void UseMainWorld()
        {
            _pinChrome = true;
            GUI.matrix = MainViewMatrix();
        }

        void UseMainChrome()
        {
            _pinChrome = false;
            GUI.matrix = UiFitMatrix();
        }

        /// <summary>
        /// 背景を画面の左右端へ合わせたとき、UI の中央寄せとの差。16:9 ちょうどでは 0。
        /// </summary>
        internal static float VerticalEdgeInset
        {
            get
            {
                if (Screen.width <= 0 || Screen.height <= 0) return 0f;
                float uiScale = Mathf.Min(Screen.width / (float)ReferenceWidth, Screen.height / (float)ReferenceHeight);
                if (uiScale <= 0f) return 0f;
                float oy = (Screen.height - ReferenceHeight * uiScale) * 0.5f;
                return Mathf.Max(0f, oy / uiScale);
            }
        }

        internal static float HorizontalEdgeInset
        {
            get
            {
                if (Screen.width <= 0 || Screen.height <= 0) return 0f;
                float uiScale = Mathf.Min(Screen.width / (float)ReferenceWidth, Screen.height / (float)ReferenceHeight);
                if (uiScale <= 0f) return 0f;
                float ox = (Screen.width - ReferenceWidth * uiScale) * 0.5f;
                return Mathf.Max(0f, ox / uiScale);
            }
        }

        static Rect ShiftLeft(Rect rect)
        {
            float inset = HorizontalEdgeInset;
            return inset <= 0f ? rect : new Rect(rect.x - inset, rect.y, rect.width, rect.height);
        }

        static Rect ShiftRight(Rect rect)
        {
            float inset = HorizontalEdgeInset;
            return inset <= 0f ? rect : new Rect(rect.x + inset, rect.y, rect.width, rect.height);
        }

        internal static Rect PinTop(Rect rect)
        {
            float inset = _pinChrome ? VerticalInset : 0f;
            return inset <= 0f ? rect : new Rect(rect.x, rect.y + inset, rect.width, rect.height);
        }

        internal static Rect PinBottom(Rect rect)
        {
            float inset = _pinChrome ? VerticalInset : 0f;
            return inset <= 0f ? rect : new Rect(rect.x, rect.y - inset, rect.width, rect.height);
        }

        static Rect KeepOnScreen(Rect rect)
        {
            if (!_pinChrome) return rect;
            float top = VerticalInset;
            float bottom = ReferenceHeight - top;
            if (rect.y >= top && rect.yMax <= bottom) return rect;
            float y = rect.y;
            if (rect.yMax > bottom) y -= rect.yMax - bottom;
            if (y < top) y = top;
            return new Rect(rect.x, y, rect.width, rect.height);
        }

        void DrawEmblem(Club club)
        {
            if (club == null) return;
            var texture = _clubSelect.EmblemTexture(club.EmblemIndex - 1);
            if (texture == null) return;
            GUI.DrawTexture(EmblemRect, texture, ScaleMode.StretchToFill, true);
        }

        void DrawInfoPlates(Club club)
        {
            DrawDatePlate();
            DrawRatingPlate(club);
            DrawFundsPlate(club);
            DrawSalaryPlate(club);
        }

        void DrawDatePlate()
        {
            var date = Session.Time.CurrentDate;
            bool winterBreak = WinterBreak.Contains(Session.CurrentSeasonStart, date);
            string heading = winterBreak ? "ウィンターブレイク" : ClubYear(date) + "年目";
            heading += "  LV" + Session.LeagueLevel;
            DrawInfoFrame(DatePlateRect);
            DrawInfoFrameText(DatePlateRect, heading, GameSession.FormatCalendarDate(date));
        }

        void DrawRatingPlate(Club club)
        {
            DrawInfoFrame(RatingPlateRect);
            var plate = RatingPlateRect;
            int count = SquadSize(club);
            GUI.Label(new Rect(plate.x + 64f, plate.y + 10f, 90f, 29f), "総合力", _plateLabelRight);
            GUI.Label(new Rect(plate.x + 154f, plate.y + 10f, 40f, 29f), count.ToString(CultureInfo.InvariantCulture), _plateLabelRight);
            GUI.Label(new Rect(plate.x + 194f, plate.y + 10f, 120f, 29f), " / " + ClubMath.SquadLimit.ToString(CultureInfo.InvariantCulture) + "人", _plateLabelLeft);
            GUI.Label(new Rect(plate.x + 22f, plate.y + 44f, 305f, 39f), ClubDialogView.FormatRating(club), _plateValueStyle);
        }

        void DrawFundsPlate(Club club)
        {
            DrawInfoFrame(FundsPlateRect);
            DrawInfoFrameText(FundsPlateRect, "資金", MoneyText.Format(club.Coins));
        }

        void DrawSalaryPlate(Club club)
        {
            DrawInfoFrame(SalaryPlateRect);
            var plate = SalaryPlateRect;
            GUI.Label(new Rect(plate.x + 22f, plate.y + 10f, 305f, 29f), "総年俸", _plateLabelStyle);
            var payroll = _holdPayroll ? _payrollBeforeGrowth : ClubMath.RenewalAnnual(club);
            GUI.Label(new Rect(plate.x + 22f, plate.y + 44f, 305f, 39f), MoneyText.Format(payroll), _plateValueStyle);
        }

        static int SquadSize(Club club)
        {
            if (club == null || club.Squad == null) return 0;
            int count = 0;
            for (int i = 0; i < club.Squad.Count; i++)
            {
                if (club.Squad[i] != null) count++;
            }

            return count;
        }

        void DrawInfoFrame(Rect rect)
        {
            if (_infoFrame == null) return;
            GUI.DrawTexture(rect, _infoFrame, ScaleMode.StretchToFill, true);
        }

        void DrawInfoFrameText(Rect plate, string label, string value)
        {
            const float labelHeight = 30f;
            const float valueHeight = 38f;
            const float pad = 24f;
            float y = plate.y + (plate.height - labelHeight - valueHeight) * 0.5f;
            var labelRect = new Rect(plate.x + pad, y, plate.width - pad * 2f, labelHeight);
            var valueRect = new Rect(plate.x + pad, y + labelHeight, plate.width - pad * 2f, valueHeight);
            GUI.Label(labelRect, label, _plateLabelStyle);
            GUI.Label(valueRect, value, _plateValueStyle);
        }

        void DrawSeasonNotice()
        {
            if (Session == null || string.IsNullOrEmpty(Session.SeasonNotice) || _noticeStyle == null) return;
            var rect = new Rect(360f, 136f, 1200f, 52f);
            GUI.color = new Color(0.05f, 0.16f, 0.10f, 0.94f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(rect, Session.SeasonNotice, _noticeStyle);
        }

        void DrawMenuButton()
        {
            var mouse = MouseInReference();
            bool over = InCircle(MenuButtonRect, mouse);
            var rim = Expand(MenuButtonRect, 8f);
            GUI.color = new Color(0.22f, 0.16f, 0.05f, 1f);
            GUI.DrawTexture(rim, _circleTex);
            GUI.color = over
                ? new Color(1f, 0.92f, 0.28f, 1f)
                : new Color(1f, 0.84f, 0.1f, 1f);
            GUI.DrawTexture(MenuButtonRect, _circleTex);
            GUI.color = Color.white;
            GUI.Label(MenuButtonRect, "MENU", _dayButtonStyle);

            var click = Event.current;
            if (over && click.type == EventType.MouseUp && click.button == 0)
            {
                if (_dialogs.Current == ClubDialogView.Kind.Menu)
                    _dialogs.Close();
                else
                    OpenPlace(ClubDialogView.Kind.Menu);
                click.Use();
            }
        }

        void DrawDayControl()
        {
            if (_dayElapsed >= 0f)
                DrawDayGauge(MainGaugeRect, Mathf.Clamp01(_dayElapsed / PaceSeconds()));

            var fixture = PlayerFixtureToday();
            if (fixture != null && fixture.Result == null)
                DrawGameDayButton(fixture);
            else
                DrawNextDayButton(MainDayButtonRect);

            if (_dialogs.IsOpen || _autoPlay) return;

            var click = Event.current;
            if (HitsMainDayButton(MouseInReference()) && click.type == EventType.MouseUp && click.button == 0)
            {
                BeginDayAdvance();
                click.Use();
            }
        }

        void DrawGameDayButton(Fixture fixture)
        {
            var button = MainDayButtonRect;
            if (_gameDayButton != null)
                GUI.DrawTexture(button, _gameDayButton, ScaleMode.StretchToFill, true);

            float sx = button.width / GameDayDesignWidth;
            float sy = button.height / GameDayDesignHeight;
            var emblemRect = new Rect(button.x, button.y, 147f * sx, 110f * sy);
            var club = Session.PlayerClub;
            var opponent = fixture.Home == club ? fixture.Away : fixture.Home;
            if (opponent != null)
            {
                var emblem = _clubSelect.EmblemTexture(opponent.EmblemIndex - 1);
                if (emblem != null)
                    GUI.DrawTexture(emblemRect, emblem, ScaleMode.StretchToFill, true);
            }

            _gameDayTitleStyle.fontSize = Mathf.RoundToInt(40f * sy);
            _gameDaySubtitleStyle.fontSize = Mathf.RoundToInt(32f * sy);
            GUI.Label(new Rect(button.x + 147f * sx, button.y + 22f * sy, 160f * sx, 48f * sy), "試合開始", _gameDayTitleStyle);
            GUI.Label(new Rect(button.x + 143f * sx, button.y + 66f * sy, 169f * sx, 39f * sy), "GAME DAY", _gameDaySubtitleStyle);
        }

        Fixture PlayerFixtureToday()
        {
            if (Session == null) return null;
            var date = Session.Time.CurrentDate.Date;
            if (_playerFixtureCached && _playerFixtureDate == date)
                return _playerFixture;

            _playerFixtureDate = date;
            _playerFixtureCached = true;
            _playerFixture = Session.PlayerFixtureOnCurrentDate();
            return _playerFixture;
        }

        void DrawDayGauge(float progress)
        {
            DrawDayGauge(DayBarRect, progress);
        }

        void DrawDayGauge(Rect rect, float progress)
        {
            if (_dayGaugeTrack != null)
                GUI.DrawTexture(rect, _dayGaugeTrack, ScaleMode.StretchToFill, true);
            float scale = rect.width / 599f;
            float origin = 51f * scale;
            float shown = origin + 498f * scale * Mathf.Clamp01(progress);
            if (_dayGaugeFill != null && shown > origin)
            {
                GUI.BeginGroup(new Rect(rect.x, rect.y, shown, rect.height));
                GUI.DrawTexture(new Rect(0f, 0f, rect.width, rect.height), _dayGaugeFill, ScaleMode.StretchToFill, true);
                GUI.EndGroup();
            }

            if (_dayGaugeFrame != null)
                GUI.DrawTexture(rect, _dayGaugeFrame, ScaleMode.StretchToFill, true);
            GUI.color = Color.white;
        }

        void DrawDayOff()
        {
            if (_dayOff == null || _dayOffShown <= 0.001f) return;
            float offset = Mathf.Lerp(-DayOffHeight, 0f, Mathf.SmoothStep(0f, 1f, _dayOffShown));
            GUI.BeginGroup(new Rect(0f, 0f, ReferenceWidth, ReferenceHeight));
            GUI.DrawTexture(new Rect(DayOffX, offset, DayOffWidth, DayOffHeight), _dayOff, ScaleMode.StretchToFill, true);
            if (_dayOffTitleStyle != null)
                GUI.Label(MoveY(DayOffTitleRect, offset), "休日", _dayOffTitleStyle);
            if (_dayOffSubtitleStyle != null)
                GUI.Label(MoveY(DayOffSubtitleRect, offset), "DAY OFF", _dayOffSubtitleStyle);
            GUI.EndGroup();
        }

        static Rect MoveY(Rect rect, float offset)
        {
            return new Rect(rect.x, rect.y + offset, rect.width, rect.height);
        }

        static bool HitsInfoPlate(Vector2 mouse)
        {
            return DatePlateRect.Contains(mouse) || RatingPlateRect.Contains(mouse) || FundsPlateRect.Contains(mouse) || SalaryPlateRect.Contains(mouse);
        }

        static bool HitsDayButton(Vector2 mouse)
        {
            return InCapsule(DayButtonRect, mouse);
        }

        static bool HitsMainDayButton(Vector2 mouse)
        {
            return InCapsule(MainDayButtonRect, mouse);
        }

        static bool HitsMainAutoButton(Vector2 mouse)
        {
            return InCapsule(MainAutoRect, mouse);
        }

        static bool HitsMainFullAutoButton(Vector2 mouse)
        {
            return ShowFullAuto && InCapsule(FullAutoRect(MainFullAutoAnchor), mouse);
        }

        // デバッグビルドだけ。左下の「フル」の右に「デバッグ」ボタンを 1 つ置き、上にメニューを開く。
        static Rect DebugMenuRect
        {
            get
            {
                return DebugButtonRect;
            }
        }

        bool HitsDebugMenu(Vector2 mouse)
        {
            return ShowFullAuto && _debugMenu.Hits(DebugMenuRect, mouse);
        }

        /// <summary>デバッグメニューの項目。増やすときはここに Add する。</summary>
        void SetupDebugMenu()
        {
            var signing = new Color(0.25f, 0.45f, 0.72f, 1f);
            var transfer = new Color(0.72f, 0.42f, 0.22f, 1f);
            var sponsor = new Color(0.22f, 0.58f, 0.40f, 1f);

            _debugMenu.Add("補強・移籍", "補強交渉（仮選手）", signing, DebugOpenSigning);
            _debugMenu.Add("補強・移籍", "移籍オファー（所属選手）", transfer, DebugOpenTransfer);
            _debugMenu.Add("スポンサー", "スポンサー契約", sponsor, DebugOpenSponsorOffer);
        }

        bool DebugMenuUsable => _phase == DayPhase.Idle && _dayElapsed < 0f && !_dialogs.IsOpen;

        void DrawDebugMenu()
        {
            if (!ShowFullAuto) return;
            var picked = _debugMenu.Draw(DebugMenuRect, MouseInReference(), DebugMenuUsable, "日送り中・イベント中は使えません", _font);
            // メニューを開いたら施設ダイアログは閉じる（重なって押せなくなるのを防ぐ）。
            if (_debugMenu.IsOpen && _dialogs.IsOpen) _dialogs.Close();
            if (picked == null || Session == null) return;
            if (!picked.Run())
                _status = "デバッグ: 「" + picked.Label + "」は今は開けません。";
        }

        bool DebugOpenSigning()
        {
            if (!Session.DebugBeginSigning()) return false;
            if (_crowd != null) _crowd.ClearSelection();
            OpenStoryPhase();
            return true;
        }

        bool DebugOpenTransfer()
        {
            if (!Session.DebugBeginTransfer()) return false;
            if (_crowd != null) _crowd.ClearSelection();
            OpenStoryPhase();
            return true;
        }

        bool DebugOpenSponsorOffer()
        {
            if (!Session.DebugCreateSponsorOffer()) return false;
            if (_crowd != null) _crowd.ClearSelection();
            OpenSponsorOffer();
            return true;
        }

        static Rect AutoButtonRect()
        {
            float sx = DayButtonRect.width / GameDayDesignWidth;
            float sy = DayButtonRect.height / GameDayDesignHeight;
            float width = AutoDesignWidth * sx;
            float height = AutoDesignHeight * sy;
            return new Rect(
                DayButtonRect.x - (AutoDesignGap + AutoDesignWidth) * sx,
                DayButtonRect.yMax - height,
                width,
                height);
        }

        void DrawAutoButton()
        {
            DrawAutoButton(AutoButtonRect());
        }

        void DrawNextDayButton(Rect rect)
        {
            if (_nextDayButton == null) return;
            var previous = GUI.color;
            if (_autoPlay)
                GUI.color = new Color(0.45f, 0.45f, 0.45f, 1f);
            GUI.DrawTexture(rect, _nextDayButton, ScaleMode.StretchToFill, true);
            GUI.color = previous;
        }

        void DrawAutoButton(Rect rect)
        {
            var texture = _autoPlay ? _autoOn : _autoOff;
            if (texture != null)
                GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, true);

            var click = Event.current;
            if (InCapsule(rect, MouseInReference()) && click.type == EventType.MouseUp && click.button == 0)
            {
                _autoPlay = !_autoPlay;
                if (!_autoPlay) _fullAuto = false;
                click.Use();
            }
        }

        static bool ShowFullAuto => Debug.isDebugBuild;

        /// <summary>「フル」ボタンの位置。AUTO の横だと邪魔なので、どの画面でも左下に固定する（引数は互換のため残す）。</summary>
        static Rect FullAutoRect(Rect autoRect)
        {
            return DebugFullAutoRect;
        }

        void DrawFullAutoButton(Rect autoRect)
        {
            if (!ShowFullAuto) return;
            var rect = FullAutoRect(autoRect);
            var previous = GUI.color;
            GUI.color = _fullAuto
                ? new Color(0.25f, 0.72f, 0.38f, 1f)
                : new Color(0.35f, 0.38f, 0.36f, 1f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
            if (_dayButtonStyle != null)
                GUI.Label(rect, "フル", _dayButtonStyle);

            var click = Event.current;
            if (click != null && InCapsule(rect, MouseInReference()) && click.type == EventType.MouseUp && click.button == 0)
            {
                _fullAuto = !_fullAuto;
                if (_fullAuto)
                {
                    _autoPlay = true;
                    _dialogs.Close();
                }

                click.Use();
            }
        }

        void DrawLineupChrome()
        {
            float progress = _dayElapsed < 0f ? 0f : Mathf.Clamp01(_dayElapsed / NextSeconds(ReviewSeconds));
            DrawDayGauge(LineupGaugeRect, progress);
            DrawNextDayButton(LineupDayButtonRect);
            DrawAutoButton(LineupAutoRect);
            DrawFullAutoButton(LineupAutoRect);

            var click = Event.current;
            if (!_autoPlay && click.type == EventType.MouseUp && click.button == 0 && InCapsule(LineupDayButtonRect, MouseInReference()))
            {
                ConfirmLineup();
                click.Use();
            }
        }

        static bool HitsAutoButton(Vector2 mouse)
        {
            return InCapsule(AutoButtonRect(), mouse);
        }

        bool HitsDayGauge(Vector2 mouse)
        {
            return _dayElapsed >= 0f && InCapsule(DayBarRect, mouse);
        }

        bool HitsMainDayGauge(Vector2 mouse)
        {
            return _dayElapsed >= 0f && InCapsule(MainGaugeRect, mouse);
        }

        static bool InCircle(Rect rect, Vector2 point)
        {
            var delta = point - rect.center;
            float radius = rect.width * 0.5f;
            return delta.sqrMagnitude <= radius * radius;
        }

        static bool InCapsule(Rect rect, Vector2 point)
        {
            float radius = rect.height * 0.5f;
            float dx = Mathf.Max(Mathf.Abs(point.x - rect.center.x) - (rect.width * 0.5f - radius), 0f);
            float dy = point.y - rect.center.y;
            return dx * dx + dy * dy <= radius * radius;
        }

        static Rect Expand(Rect rect, float amount)
        {
            return new Rect(rect.x - amount, rect.y - amount, rect.width + amount * 2f, rect.height + amount * 2f);
        }

        void BeginDayAdvance()
        {
            if (Session == null || Session.GameOver || _phase != DayPhase.Idle || _dayElapsed >= 0f) return;
            if (Session.CurrentStory != null || Session.TryOpenMorningEvent())
            {
                OpenStoryPhase();
                return;
            }

            if (_fullAuto)
            {
                var spent = Session.SpendFullAuto();
                if (!string.IsNullOrEmpty(spent))
                    Debug.Log("SoccerClub full auto: " + spent);
            }

            var today = PlayerFixtureToday();
            if (today != null && today.Result == null)
            {
                OpenLineup();
                return;
            }

            StartDayAdvance();
        }

        void OpenLineup()
        {
            _phase = DayPhase.Lineup;
            _dayElapsed = _autoPlay ? 0f : -1f;
            _growthCursor = 0f;
            _matchScreen = false;
            _lineupSheet = new ManagerLineupAi().BuildSheet(Session.PlayerClub);
            _status = "スタメン紹介";
        }

        void ConfirmLineup()
        {
            if (_phase != DayPhase.Lineup) return;
            _lineupSheet = null;
            _phase = DayPhase.Idle;
            _dayElapsed = -1f;
            StartDayAdvance();
        }

        void StartDayAdvance()
        {
            if (Session == null || Session.GameOver || _phase != DayPhase.Idle || _dayElapsed >= 0f) return;
            Session.ClearSeasonNotice();
            _payrollBeforeGrowth = ClubMath.RenewalAnnual(Session.PlayerClub);
            _holdPayroll = true;
            _dayElapsed = 0f;
            _growthCursor = 0f;
            _phase = DayPhase.Advancing;
            Session.RollDailyGrowth();
            Session.CopyDailyDeltas(_dayDeltas);
            _liveFixture = Session.ResolveTodayMatches();
            _matchScreen = _liveFixture != null && _liveFixture.Result != null;
            // 自クラブの試合を挟んだら、メイン画面のログは空から始める。
            if (_matchScreen) _growthLog.Clear();
            if (_crowd != null) _crowd.SetGrowthDeltas(_dayDeltas, AdvanceSeconds());
            QueueGrowthLog();
            _status = _matchScreen ? "試合が行われています。" : "1日を進めています。";
        }

        void BeginStats()
        {
            _phase = DayPhase.Stats;
            _matchScreen = false;
            _matchScreenView.ResetStatsScroll();
            _dayElapsed = _autoPlay ? 0f : -1f;
            _status = "試合スタッツ";
        }

        void ContinuePostMatch()
        {
            if (_postMatchStepFrame == UnityEngine.Time.frameCount) return;
            _postMatchStepFrame = UnityEngine.Time.frameCount;
            if (_phase == DayPhase.Stats)
            {
                CaptureSupporters();
                _phase = DayPhase.Settlement;
                _dayElapsed = _autoPlay ? 0f : -1f;
                _status = "試合リザルト";
                return;
            }

            if (_phase == DayPhase.Settlement)
            {
                FinishAdvancedDay();
                return;
            }

            if (_phase == DayPhase.MonthReport)
                DismissMonthReport();
            else if (_phase == DayPhase.SponsorCut)
                DismissSponsorCut();
            else if (_phase == DayPhase.SponsorOffer)
                AcceptSponsorOffer();
        }

        void CaptureSupporters()
        {
            _coreDelta = 0;
            _lightDelta = 0;
            var fixture = _liveFixture;
            var club = Session != null ? Session.PlayerClub : null;
            if (fixture == null || club == null || fixture.Result == null) return;
            bool home = fixture.Home == club;
            int mine = home ? fixture.Result.HomeGoals : fixture.Result.AwayGoals;
            int theirs = home ? fixture.Result.AwayGoals : fixture.Result.HomeGoals;
            ClubMath.PreviewMatchSupporters(
                club,
                mine > theirs,
                mine < theirs,
                home,
                fixture.OperatingPaid,
                fixture.OperatingRequired,
                out _coreDelta,
                out _lightDelta);
        }

        /// <summary>
        /// 自クラブの所属選手のうち、今日能力が動く人をログの予定に積む。
        /// グラウンドにいる人はポップと同じ時刻、いない人は日送りの途中のどこか。
        /// </summary>
        void QueueGrowthLog()
        {
            _growthLogQueue.Clear();
            var club = Session != null ? Session.PlayerClub : null;
            if (club == null || club.Squad == null) return;
            float latest = Mathf.Max(0.15f, AdvanceSeconds() - 1.2f);
            for (int i = 0; i < club.Squad.Count; i++)
            {
                var player = club.Squad[i];
                if (player == null || string.IsNullOrEmpty(player.Id)) continue;
                if (!Session.TryGetDailyStat(player.Id, out int stat, out int delta)) continue;
                float at;
                if (_crowd == null || !_crowd.TryGetPopupAt(player.Id, out at))
                    at = UnityEngine.Random.Range(0f, latest);
                _growthLogQueue.Add(new GrowthLogItem
                {
                    At = at,
                    Name = player.Name,
                    Stat = stat == 0 ? GrowthStat.Offense : stat == 1 ? GrowthStat.Defense : GrowthStat.Stamina,
                    Delta = delta
                });
            }

            _growthLogQueue.Sort((a, b) => a.At.CompareTo(b.At));
        }

        /// <summary>予定の時刻 until までに来たものをログへ出す。</summary>
        void FlushGrowthLog(float until)
        {
            int count = 0;
            while (count < _growthLogQueue.Count && _growthLogQueue[count].At <= until)
            {
                var item = _growthLogQueue[count];
                _growthLog.Add(item.Name, item.Stat, item.Delta);
                count++;
            }

            if (count > 0) _growthLogQueue.RemoveRange(0, count);
        }

        // log。Figma main の 31,252 / 405×368。キャラクター詳細を開いているときは重なるので出さない。
        static Vector2 GrowthLogOrigin
        {
            get
            {
                var rect = ShiftLeft(PinTop(new Rect(31f, 252f, 405f, 368f)));
                return new Vector2(rect.x, rect.y);
            }
        }

        void FinishAdvancedDay()
        {
            _phase = DayPhase.Idle;
            _dayElapsed = -1f;
            _growthCursor = 0f;
            _matchScreen = false;
            _liveFixture = null;
            if (_crowd != null) _crowd.ClearGrowthDeltas();
            var played = Session.Time.CurrentDate;
            AdvanceOneDay();
            bool offered = Session != null && Session.TryCreateSponsorOffer(played);
            if (offered && !GameSaveStore.Write(Session))
                _status = "オートセーブに失敗しました。";
            if (Session != null && Session.HasMonthReport)
            {
                _phase = DayPhase.MonthReport;
                _dayElapsed = _autoPlay ? 0f : -1f;
                if (_status != "オートセーブに失敗しました。")
                    _status = "収支報告";
                return;
            }

            if (OpenSponsorNotice(offered)) return;

            _phase = DayPhase.Idle;
        }

        bool OpenSponsorNotice(bool offered)
        {
            if (Session != null && Session.HasSponsorCut)
            {
                OpenSponsorCut();
                return true;
            }

            if (offered || (Session != null && Session.HasSponsorOffer))
            {
                OpenSponsorOffer();
                return true;
            }

            return false;
        }

        void OpenSponsorCut()
        {
            _phase = DayPhase.SponsorCut;
            _dayElapsed = _autoPlay ? 0f : -1f;
            _growthCursor = 0f;
            _matchScreen = false;
            if (_status != "オートセーブに失敗しました。")
                _status = "スポンサー打ち切り";
        }

        void OpenStoryPhase()
        {
            _phase = DayPhase.Story;
            _meetingClock = false;
            _dayElapsed = _autoPlay ? 0f : -1f;
            _growthCursor = 0f;
            _matchScreen = false;
            if (Session != null && Session.CurrentStory != null)
                _status = Session.CurrentStory.Title ?? "イベント";
            if (Session != null && !GameSaveStore.Write(Session))
                _status = "オートセーブに失敗しました。";
        }

        bool IsSigningMeeting()
        {
            var story = Session != null ? Session.CurrentStory : null;
            return story != null
                && story.Kind == ClubStoryKind.Signing
                && story.Negotiates
                && story.Incoming != null;
        }

        bool IsTransferMeeting()
        {
            var story = Session != null ? Session.CurrentStory : null;
            return story != null
                && story.Kind == ClubStoryKind.Transfer
                && !string.IsNullOrEmpty(story.PlayerId);
        }

        bool IsMeetingRoom() => IsSigningMeeting() || IsTransferMeeting();

        bool PredictedMeetingOutcome()
        {
            if (Session == null) return false;
            return IsTransferMeeting() ? Session.WillTransferLeave() : Session.WillSigningSucceed();
        }

        float MeetingResultAt()
        {
            return IsTransferMeeting() ? MeetingRoomView.TransferResultAt : MeetingRoomView.ResultAt;
        }

        float MeetingDuration()
        {
            return IsTransferMeeting()
                ? MeetingRoomView.TransferDuration(_meetingSuccess)
                : MeetingRoomView.Duration(_meetingSuccess);
        }

        void BeginMeetingClock()
        {
            if (_meetingClock) return;
            _meetingClock = true;
            _meetingSuccess = PredictedMeetingOutcome();
            if (_dayElapsed < 0f) _dayElapsed = 0f;
        }

        void FinishMeeting()
        {
            if (_storyStepFrame == UnityEngine.Time.frameCount) return;
            _storyStepFrame = UnityEngine.Time.frameCount;
            _meetingClock = false;
            _meeting.Clear();
            _dayElapsed = -1f;
            if (Session == null)
            {
                _phase = DayPhase.Idle;
                return;
            }

            Session.CloseStory();
            if (Session.TryOpenMorningEvent())
            {
                OpenStoryPhase();
                return;
            }

            _phase = DayPhase.Idle;
            if (!GameSaveStore.Write(Session))
                _status = "オートセーブに失敗しました。";
        }

        void AdvanceOpenStory()
        {
            if (_storyStepFrame == UnityEngine.Time.frameCount) return;
            _storyStepFrame = UnityEngine.Time.frameCount;
            if (Session == null)
            {
                _phase = DayPhase.Idle;
                _dayElapsed = -1f;
                return;
            }

            bool still = Session.ContinueStory();
            if (still)
            {
                _dayElapsed = _autoPlay ? 0f : -1f;
                if (Session.CurrentStory != null)
                    _status = Session.CurrentStory.Title ?? "イベント";
                if (!GameSaveStore.Write(Session))
                    _status = "オートセーブに失敗しました。";
                return;
            }

            _dayElapsed = -1f;
            if (Session.TryOpenMorningEvent())
            {
                OpenStoryPhase();
                return;
            }

            _phase = DayPhase.Idle;
            if (!GameSaveStore.Write(Session))
                _status = "オートセーブに失敗しました。";
        }

        void OpenSponsorOffer()
        {
            _phase = DayPhase.SponsorOffer;
            _dayElapsed = _autoPlay ? 0f : -1f;
            _growthCursor = 0f;
            _matchScreen = false;
            if (_status != "オートセーブに失敗しました。")
                _status = "スポンサー獲得";
        }

        void AcceptSponsorOffer()
        {
            if (Session != null) Session.AcceptSponsorOffer();
            _sponsorOfferView.Clear();
            _phase = DayPhase.Idle;
            _dayElapsed = -1f;
            if (_status != "オートセーブに失敗しました。")
                _status = "スポンサーと契約しました。";
            TryAutoSave();
        }

        void DeclineSponsorOffer()
        {
            if (Session != null) Session.DeclineSponsorOffer();
            _sponsorOfferView.Clear();
            _phase = DayPhase.Idle;
            _dayElapsed = -1f;
            if (_status != "オートセーブに失敗しました。")
                _status = "スポンサーは見送りました。";
            TryAutoSave();
        }

        void DismissMonthReport()
        {
            if (Session != null) Session.DismissMonthReport();
            _growthCursor = 0f;
            _matchScreen = false;
            if (OpenSponsorNotice(false)) return;

            _phase = DayPhase.Idle;
            _dayElapsed = -1f;
            TryAutoSave();
        }

        void DismissSponsorCut()
        {
            if (Session != null) Session.DismissSponsorCut();
            _growthCursor = 0f;
            _matchScreen = false;
            if (Session != null && Session.HasSponsorOffer)
            {
                OpenSponsorOffer();
                if (!GameSaveStore.Write(Session))
                    _status = "オートセーブに失敗しました。";
                return;
            }

            _phase = DayPhase.Idle;
            _dayElapsed = -1f;
            if (_status != "オートセーブに失敗しました。")
                _status = "スポンサー契約を打ち切りました。";
            TryAutoSave();
        }

        void DrawPersonnelAuto()
        {
            if (_autoPlay && _dayElapsed >= 0f)
                DrawDayGauge(MainGaugeRect, Mathf.Clamp01(_dayElapsed / StorySeconds()));
            DrawAutoButton(MainAutoRect);
            DrawFullAutoButton(MainFullAutoAnchor);
        }

        void DrawReviewChrome()
        {
            float wait = _phase == DayPhase.MonthReport || _phase == DayPhase.SponsorCut
                ? NextSeconds(DayAdvanceSeconds)
                : NextSeconds(ReviewSeconds);
            if (_autoPlay && _dayElapsed >= 0f)
                DrawDayGauge(Mathf.Clamp01(_dayElapsed / wait));
            DrawAutoButton();
            DrawFullAutoButton(AutoButtonRect());
            DrawNextDayButton(DayButtonRect);
            if (!_autoPlay)
            {
                var caption = new Rect(DayButtonRect.x, DayButtonRect.y - 44f, DayButtonRect.width, 36f);
                GUI.color = new Color(1f, 0.97f, 0.9f, 0.94f);
                GUI.DrawTexture(caption, Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(caption, "次へ", _dayButtonStyle);
            }

            var click = Event.current;
            if (!_autoPlay && HitsDayButton(MouseInReference()) && click.type == EventType.MouseUp && click.button == 0)
            {
                ContinuePostMatch();
                click.Use();
            }
        }

        static int ClubYear(DateTime date)
        {
            var start = GameSession.SeasonStart;
            int years = date.Year - start.Year;
            if (date.Month < start.Month || (date.Month == start.Month && date.Day < start.Day))
                years--;
            return Math.Max(1, years + 1);
        }

        /// <summary>
        /// OnGUI 中の位置。GUI.matrix 済みの基準座標（1920×1080）なので、逆行列は掛けない。
        /// </summary>
        internal static Vector2 MouseInReference()
        {
            return Event.current.mousePosition;
        }

        /// <summary>
        /// Input.mousePosition（画面左下原点）を、指定した GUI 行列の基準座標へ変換する。
        /// </summary>
        static Vector2 ScreenTo(Matrix4x4 matrix, Vector3 screenBottomLeft)
        {
            var gui = new Vector2(screenBottomLeft.x, Screen.height - screenBottomLeft.y);
            var local = matrix.inverse.MultiplyPoint3x4(gui);
            return new Vector2(local.x, local.y);
        }

        void UpdateViewCamera(float deltaTime)
        {
            bool castPresent = Session != null
                && _crowd != null
                && !Session.IsClosedPeriod(Session.Time.CurrentDate);
            _miniEvents.Tick(Session, _crowd, castPresent, deltaTime);

            Vector2 focus;
            bool follow = _miniEvents.TryCameraFocus(_crowd, out focus);

            float want = follow ? 2f : 1f;
            _viewZoom = Mathf.MoveTowards(_viewZoom, want, deltaTime * 1.4f);
            float zoom = Mathf.Max(1f, _viewZoom);
            float viewW = ReferenceWidth / zoom;
            float viewH = ReferenceHeight / zoom;
            float inset = VerticalInset;
            float maxX = Mathf.Max(0f, ReferenceWidth - viewW);
            float minY = -inset / zoom;
            float maxY = ReferenceHeight - (ReferenceHeight - inset) / zoom;
            var desired = Vector2.zero;
            if (follow)
                desired = focus - new Vector2(viewW, viewH) * 0.5f;
            desired.x = Mathf.Clamp(desired.x, 0f, maxX);
            desired.y = Mathf.Clamp(desired.y, minY, maxY);
            float blend = 1f - Mathf.Exp(-6f * deltaTime);
            _viewOrigin = Vector2.Lerp(_viewOrigin, desired, blend);

            _viewOrigin.x = Mathf.Clamp(_viewOrigin.x, 0f, maxX);
            _viewOrigin.y = Mathf.Clamp(_viewOrigin.y, minY, maxY);
            if (_viewZoom <= 1.001f)
            {
                _viewZoom = 1f;
                _viewOrigin = Vector2.zero;
            }
        }

        bool PointerBlocked(Vector2 chrome, Vector2 world)
        {
            if (_dialogs.IsOpen) return true;
            if (EmblemRect.Contains(chrome) || HitsInfoPlate(chrome) || MenuButtonRect.Contains(chrome) || HitsMainDayButton(chrome) || HitsMainAutoButton(chrome) || HitsMainFullAutoButton(chrome))
                return true;
            if (HitsMainDayGauge(chrome)) return true;
            if (_crowd != null && _crowd.SelectedId != null && DetailRect.Contains(chrome)) return true;
            if (_crowd != null && _crowd.IsPointerBusy(world)) return false;
            return false;
        }

        /// <summary>
        /// キャラクター詳細の開閉と、施設ダイアログの操作は建物クリックへ渡さない。
        /// 判定は HandlePointer が選択を消すより前に行う。
        /// </summary>
        bool ShouldSuppressOwnerOpen(Vector2 chrome, Vector2 world)
        {
            if (_dialogs.IsOpen) return true;
            if (EmblemRect.Contains(chrome) || MenuButtonRect.Contains(chrome) || HitsInfoPlate(chrome) || HitsMainAutoButton(chrome) || HitsMainFullAutoButton(chrome) || HitsMainDayButton(chrome)) return true;
            if (_crowd == null) return false;
            if (_crowd.IsPointerBusy(world)) return true;
            return _crowd.SelectedId != null;
        }

        void DrawCharacterDetail(Club club, string id)
        {
            var player = FindPlayer(club, id);
            if (player != null)
            {
                DrawPlayerDetail(player);
                return;
            }

            var staff = FindStaff(club, id);
            if (staff != null)
                DrawStaffDetail(staff);
        }

        static Player FindPlayer(Club club, string playerId)
        {
            var squad = club.Squad;
            for (int i = 0; i < squad.Count; i++)
            {
                if (squad[i] != null && squad[i].Id == playerId)
                    return squad[i];
            }

            return null;
        }

        static StaffMember FindStaff(Club club, string id)
        {
            var team = club.Team;
            if (team != null)
            {
                var found = MatchStaff(id, team.Manager, team.HeadCoach, team.PhysicalCoach, team.Doctor);
                if (found != null) return found;
                found = MatchStaff(id, team.Coaches) ?? MatchStaff(id, team.Trainers);
                if (found != null) return found;
            }

            var front = club.Front;
            if (front == null) return null;
            var direct = MatchStaff(id, front.Secretary, front.GeneralManager);
            if (direct != null) return direct;
            return MatchStaff(id, front.Strengthening)
                   ?? MatchStaff(id, front.Sales)
                   ?? MatchStaff(id, front.PublicRelations)
                   ?? MatchStaff(id, front.Business)
                   ?? MatchStaff(id, front.Academy);
        }

        static StaffMember MatchStaff(string id, params StaffMember[] staff)
        {
            if (staff == null) return null;
            for (int i = 0; i < staff.Length; i++)
            {
                if (staff[i] != null && staff[i].Id == id)
                    return staff[i];
            }

            return null;
        }

        void DrawPlayerDetail(Player player)
        {
            GUI.color = new Color(0.08f, 0.1f, 0.09f, 0.92f);
            GUI.DrawTexture(DetailRect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            float x = DetailRect.x + 18f;
            float y = DetailRect.y + 14f;
            float w = DetailRect.width - 36f;
            GUI.Label(new Rect(x, y, w - 100f, 40f), player.Name, _titleStyle);
            if (GUI.Button(new Rect(DetailRect.xMax - 110f, y, 92f, 36f), "閉じる", _buttonStyle))
                _crowd.ClearSelection();
            y += 48f;
            GUI.Label(new Rect(x, y, w, 32f), PhaseLabel(player.Phase) + "  " + player.Age + "歳  " + player.PreferredPosition, _bodyStyle);
            y += 34f;
            GUI.Label(new Rect(x, y, w, 32f), player.Rarity + "  /  " + player.Style, _bodyStyle);
            y += 40f;
            GUI.Label(new Rect(x, y, w, 30f), "攻撃  " + player.DisplayOffense.ToString(CultureInfo.InvariantCulture), _smallStyle);
            y += 28f;
            GUI.Label(new Rect(x, y, w, 30f), "守備  " + player.DisplayDefense.ToString(CultureInfo.InvariantCulture), _smallStyle);
            y += 28f;
            GUI.Label(new Rect(x, y, w, 30f), "スタミナ  " + player.DisplayStaminaCurrent.ToString(CultureInfo.InvariantCulture) + " / " + player.DisplayStaminaMax.ToString(CultureInfo.InvariantCulture), _smallStyle);
            y += 28f;
            GUI.Label(new Rect(x, y, w, 30f), "年俸  " + MoneyText.Format(player.AnnualSalary), _smallStyle);
            y += 28f;
            GUI.Label(new Rect(x, y, w, 30f), "月給  " + MoneyText.Format(player.MonthlySalary), _smallStyle);
            y += 28f;
            string condition = "コンディション  " + player.Condition;
            if (player.InjuryDays > 0) condition += "  怪我 " + player.InjuryDays + "日";
            GUI.Label(new Rect(x, y, w, 30f), condition, _smallStyle);
            y += 36f;
            GUI.Label(new Rect(x, y, w, 28f), "適性", _smallStyle);
            y += 28f;
            GUI.Label(new Rect(x, y, w, 120f), FitSummary(player), _smallStyle);
        }

        void DrawStaffDetail(StaffMember staff)
        {
            GUI.color = new Color(0.08f, 0.1f, 0.09f, 0.92f);
            GUI.DrawTexture(DetailRect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            float x = DetailRect.x + 18f;
            float y = DetailRect.y + 14f;
            float w = DetailRect.width - 36f;
            GUI.Label(new Rect(x, y, w - 100f, 40f), staff.Name, _titleStyle);
            if (GUI.Button(new Rect(DetailRect.xMax - 110f, y, 92f, 36f), "閉じる", _buttonStyle))
                _crowd.ClearSelection();
            y += 48f;
            var role = staff.IsFrontOffice
                ? "フロント  " + RoleLabel(staff.FrontRole)
                : "現場  " + TeamRoleLabel(staff.TeamRole);
            GUI.Label(new Rect(x, y, w, 32f), role, _bodyStyle);
            y += 34f;
            GUI.Label(new Rect(x, y, w, 32f), staff.Age + "歳  Lv" + staff.Level, _bodyStyle);
            y += 34f;
            GUI.Label(new Rect(x, y, w, 32f), "年俸  " + MoneyText.Format(staff.AnnualSalary), _bodyStyle);
            y += 40f;
            GUI.Label(new Rect(x, y, w, 220f), StaffNote(staff), _smallStyle);
        }

        static string StaffNote(StaffMember staff)
        {
            if (staff.IsFrontOffice)
            {
                switch (staff.FrontRole)
                {
                    case FrontRole.Secretary:
                        return "施設のレベルアップを提案する";
                    case FrontRole.GeneralManager:
                        return "方針  " + PolicyLabel(staff.Policy)
                               + "\nリストから選手とスタッフを獲得し、契約を満了させる";
                    case FrontRole.Strengthening:
                        return "狙う枠  " + GroupLabel(staff.ScoutedGroup)
                               + "\n選手とスタッフの候補をリストアップする";
                    case FrontRole.Sales:
                        return "スポンサーを獲得する。有能なほど契約金が高い";
                    case FrontRole.PublicRelations:
                        return "レベルに応じて、ホームの試合の広告費が決まる";
                    case FrontRole.Business:
                        return "開幕の1週間前にチケット価格を決める";
                    default:
                        return "育成費でアカデミー選手を育てる";
                }
            }

            switch (staff.TeamRole)
            {
                case TeamStaffRole.Manager:
                    return "得意システム  " + FormationLabel(staff.FavoriteFormation)
                           + "\n得意戦術  " + StyleLabel(staff.FavoriteStyle)
                           + "\n起用  " + PriorityLabel(staff.LineupPriority)
                           + "\nメンバーと采配を決める。合う選手の練習にバフ";
                case TeamStaffRole.HeadCoach:
                    return "全員の攻撃、守備、スタミナの練習にバフ";
                case TeamStaffRole.Coach:
                    double coachPercent = ClubMath.TrainingBonusPercent(staff.Level);
                    double specialtyPercent = ClubMath.CoachBonusPercent(staff.Level, true);
                    return "担当  " + GroupLabel(staff.ScoutedGroup)
                           + "\n全員の攻撃と守備  " + FormatPercent(coachPercent)
                           + "\n担当ポジション  " + FormatPercent(specialtyPercent);
                case TeamStaffRole.PhysicalCoach:
                    return "全員のスタミナの練習にバフ";
                case TeamStaffRole.Trainer:
                    return "練習後と試合後に、調子の低い選手から順に回復する\n回復量の合計はトレーナーのレベル";
                default:
                    return "怪我の発生と日数を下げる";
            }
        }

        static string PolicyLabel(ReinforcementPolicy policy)
        {
            switch (policy)
            {
                case ReinforcementPolicy.Youth: return "若手重視";
                case ReinforcementPolicy.Manager: return "監督重視";
                default: return "実力重視";
            }
        }

        static string FormatPercent(double percent)
        {
            return "+" + percent.ToString("0.0", CultureInfo.InvariantCulture) + "%";
        }

        static string GroupLabel(PositionGroup group)
        {
            switch (group)
            {
                case PositionGroup.GK: return "GK";
                case PositionGroup.DF: return "DF";
                case PositionGroup.MF: return "MF";
                default: return "FW";
            }
        }

        static string PriorityLabel(LineupPriority priority)
        {
            return priority == LineupPriority.Condition ? "コンディション優先" : "パラメータ優先";
        }

        static string StyleLabel(PlayingStyle style)
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

        static string FormationLabel(FormationType formation)
        {
            return FormationCatalog.Get(formation).DisplayName;
        }

        static string TeamRoleLabel(TeamStaffRole role)
        {
            switch (role)
            {
                case TeamStaffRole.Manager: return "監督";
                case TeamStaffRole.HeadCoach: return "ヘッドコーチ";
                case TeamStaffRole.Coach: return "コーチ";
                case TeamStaffRole.PhysicalCoach: return "フィジカルコーチ";
                case TeamStaffRole.Trainer: return "トレーナー";
                default: return "ドクター";
            }
        }

        static string PhaseLabel(GrowthPhase phase)
        {
            switch (phase)
            {
                case GrowthPhase.Youth: return "若手";
                case GrowthPhase.Prime: return "全盛期";
                default: return "ベテラン";
            }
        }

        static string FitSummary(Player player)
        {
            var text = "";
            foreach (Position position in System.Enum.GetValues(typeof(Position)))
            {
                var fit = player.SuitabilityAt(position);
                if (fit < Suitability.Good) continue;
                if (text.Length > 0) text += "  ";
                text += position + (fit == Suitability.Excellent ? "◎" : "○");
            }

            return text.Length == 0 ? "なし" : text;
        }

        void AdvanceOneDay()
        {
            var fixtures = Session.AdvanceDay();
            var club = Session.PlayerClub;
            if (fixtures.Count == 0)
            {
                _status = "この日は試合なし。";
            }
            else
            {
                _status = fixtures.Count + "試合を消化。";
                for (int i = 0; i < fixtures.Count; i++)
                {
                    var f = fixtures[i];
                    if (f.Home != club && f.Away != club) continue;
                    _status = FormatFixture(f);
                    break;
                }
            }

            if (!GameSaveStore.Write(Session))
                _status = "オートセーブに失敗しました。";
            else if (!string.IsNullOrEmpty(Session.SeasonNotice))
                _status = Session.SeasonNotice;
        }

        static string FormatFixture(Fixture fixture)
        {
            if (fixture.Result == null) return fixture.Home.Name + " vs " + fixture.Away.Name;
            var kind = fixture.IsLeague ? "リーグ" : "カップ";
            return kind + " " + fixture.Home.Name + " " + fixture.Result.HomeGoals
                   + " - " + fixture.Result.AwayGoals + " " + fixture.Away.Name;
        }

        static string RoleLabel(FrontRole role)
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

        void EnsureStyles()
        {
            if (_titleStyle != null) return;
            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 36,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            _bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                wordWrap = true,
                normal = { textColor = Color.white }
            };
            _smallStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                wordWrap = true,
                normal = { textColor = Color.white }
            };
            _buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 22 };
            var platePad = new RectOffset(0, 0, 0, 0);
            _plateLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                padding = platePad,
                margin = platePad,
                clipping = TextClipping.Overflow,
                wordWrap = false,
                normal = { textColor = Color.white }
            };
            _plateLabelLeft = new GUIStyle(_plateLabelStyle) { alignment = TextAnchor.MiddleLeft };
            _plateLabelRight = new GUIStyle(_plateLabelStyle) { alignment = TextAnchor.MiddleRight };
            _plateValueStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 32,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                padding = platePad,
                margin = platePad,
                clipping = TextClipping.Overflow,
                wordWrap = false,
                richText = true,
                normal = { textColor = Color.white }
            };
            _noticeStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 26,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
                clipping = TextClipping.Overflow,
                normal = { textColor = Color.white }
            };
            _dayButtonStyle = new GUIStyle(GUIStyle.none)
            {
                fontSize = 30,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.16f, 0.15f, 0.12f, 1f) }
            };
            var gameDayPad = new RectOffset(0, 0, 0, 0);
            _gameDayTitleStyle = new GUIStyle(GUIStyle.none)
            {
                fontSize = 40,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperLeft,
                padding = gameDayPad,
                margin = gameDayPad,
                clipping = TextClipping.Overflow,
                wordWrap = false,
                normal = { textColor = GameDayTextColor }
            };
            _gameDaySubtitleStyle = new GUIStyle(GUIStyle.none)
            {
                fontSize = 32,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperLeft,
                padding = gameDayPad,
                margin = gameDayPad,
                clipping = TextClipping.Overflow,
                wordWrap = false,
                normal = { textColor = GameDayTextColor }
            };
            var dayOffPad = new RectOffset(0, 0, 0, 0);
            _dayOffTitleStyle = new GUIStyle(GUIStyle.none)
            {
                fontSize = 128,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                padding = dayOffPad,
                margin = dayOffPad,
                clipping = TextClipping.Overflow,
                wordWrap = false,
                normal = { textColor = Color.black }
            };
            _dayOffSubtitleStyle = new GUIStyle(GUIStyle.none)
            {
                fontSize = 64,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                padding = dayOffPad,
                margin = dayOffPad,
                clipping = TextClipping.Overflow,
                wordWrap = false,
                normal = { textColor = Color.black }
            };
            _volumeLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
                normal = { textColor = Color.white }
            };
            _circleTex = MakeRounded(156, 156, 78f);
            if (_font != null)
            {
                _titleStyle.font = _font;
                _bodyStyle.font = _font;
                _smallStyle.font = _font;
                _buttonStyle.font = _font;
                _plateLabelStyle.font = _font;
                _plateLabelLeft.font = _font;
                _plateLabelRight.font = _font;
                _plateValueStyle.font = _font;
                _noticeStyle.font = _font;
                _dayButtonStyle.font = _font;
                _gameDayTitleStyle.font = _font;
                _gameDaySubtitleStyle.font = _font;
                _dayOffTitleStyle.font = _font;
                _dayOffSubtitleStyle.font = _font;
                _volumeLabelStyle.font = _font;
            }
        }

        static Texture2D MakeRounded(int width, int height, float radius)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[width * height];
            float halfW = width * 0.5f;
            float halfH = height * 0.5f;
            float innerX = halfW - radius;
            float innerY = halfH - radius;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float px = x + 0.5f - halfW;
                    float py = y + 0.5f - halfH;
                    float qx = Mathf.Abs(px) - innerX;
                    float qy = Mathf.Abs(py) - innerY;
                    float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
                    float inside = Mathf.Min(Mathf.Max(qx, qy), 0f);
                    float dist = outside + inside - radius;
                    byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(0.6f - dist) * 255f);
                    pixels[y * width + x] = new Color32(255, 255, 255, alpha);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return tex;
        }

    }
}
