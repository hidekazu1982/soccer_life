using SoccerClub.Core;
using SoccerClub.Data;
using UnityEngine;

namespace SoccerClub.UI
{
    /// <summary>
    /// 会議室での補強と移籍。Figma の meeting_event。GMは右に立ち、強化部と選手が左から入る。
    /// 立ち絵は character3（1218 角）を左 -97,0 / 右 820,0 に置き、セリフは立ち絵の下の枠に出す。
    /// </summary>
    public sealed class MeetingRoomView
    {
        public const float SlideSeconds = 0.6f;
        public const float ScoutLineSeconds = 2.6f;
        public const float LineSeconds = 2.0f;
        public const float EllipsisSeconds = 1.8f;
        public const float CenterSeconds = 1.6f;
        public const float DialogSeconds = 3.4f;

        // character_l / character_r（203:10 / 172:92）。1218 角を -97,0 と 820,0 に置く。
        // シートは 2048 角の中の 212,46 / 1622×1998 なので、1218/2048 倍した位置（SponsorOfferView と同じ）。
        const float SheetScale = 1218f / 2048f;
        const float LeftFrameX = -97f;
        const float RightFrameX = 820f;
        const float FrameY = 0f;
        // 画面外の待機位置（1218 角の枠の左端）。16:9 より横長の端末では背景が左右に広がるので、その分さらに外へ出す。
        const float OffXBase = -1220f;
        static float OffX => OffXBase - GameRunner.HorizontalEdgeInset;

        // character_message_l / character_message_r（203:2 / 184:40）。枠 800×200（白 90%・角丸 10）、
        // 上辺に部署 200×60（黒）と名前 400×60（#D1D1D6）を重ね、本文は 720×120 の左寄せ。
        static readonly Rect LeftBubble = new Rect(112f, 848f, 800f, 200f);
        static readonly Rect RightBubble = new Rect(1029f, 848f, 800f, 200f);
        const float PlateY = 818f;
        const float PlateHeight = 60f;
        const float DepartmentPlateOffset = 121f;
        const float DepartmentPlateWidth = 200f;
        const float DepartmentTextOffset = 150f;
        const float DepartmentTextWidth = 160f;
        const float NamePlateOffset = 279f;
        const float NamePlateWidth = 400f;
        const float NameTextOffset = 329f;
        const float NameTextWidth = 300f;
        const float LineOffsetX = 40f;
        const float LineOffsetY = 48f;
        const float LineWidth = 720f;
        const float LineHeight = 120f;
        const float BoxRadius = 10f;
        const float BoxAlpha = 0.9f;
        static readonly Color NamePlateColor = new Color(0xD1 / 255f, 0xD1 / 255f, 0xD6 / 255f, 1f);
        const string ScoutDepartment = "強化部";
        const string GmDepartment = "GM";
        const string PlayerDepartment = "選手";

        // text（127:143）。上の中央、128px。
        static readonly Rect Banner = new Rect(640f, 32f, 640f, 155f);
        // contract_payment（203:40）。枠 1200×280、文は上から 2 行、金額は 2 段（ラベル 200×60 黒、金額 400×60 灰）。
        static readonly Rect ResultPanel = new Rect(360f, 767f, 1200f, 280f);
        static readonly Rect ResultMessage = new Rect(360f, 788f, 1200f, 78f);
        const float AmountLabelX = 681f;
        const float AmountLabelWidth = 200f;
        const float AmountLabelTextX = 710f;
        const float AmountLabelTextWidth = 160f;
        const float AmountValueX = 839f;
        const float AmountValueWidth = 400f;
        const float AmountValueTextX = 909f;
        const float AmountValueTextWidth = 320f;
        const float AmountHeight = 60f;
        const float FirstAmountY = 882f;
        const float SecondAmountY = 958f;
        // 移籍金は 1 段だけなので、2 段の真ん中に置く。
        const float SingleAmountY = 920f;

        public struct Marks
        {
            public float ScoutInEnd;
            public float ScoutTalkEnd;
            public float GmTalkEnd;
            public float ScoutOutEnd;
            public float PlayerInEnd;
            public float GreetEnd;
            public float EllipsisEnd;
            public float NegotiateEnd;
            public float ResultEnd;
            public float PlayerExitStart;
            public float GmGreetEnd;
            public float GmResultEnd;
            public float PlayerReactEnd;
            public float GmReactEnd;
            public float End;
        }

        readonly AvatarLibrary _avatars = new AvatarLibrary();
        Texture2D _background;
        Texture2D _scoutPortrait;
        Texture2D _gmPortrait;
        Texture2D _playerPortrait;
        StaffMember _scout;
        StaffMember _gm;
        Player _focus;
        bool _onSheet;
        string _castKey;
        // 吹き出しを出しているキャラをふわふわさせる。吹き出しはキャラの後に描くので、直前の描画の結果を使う。
        bool _leftTalking;
        bool _rightTalking;
        bool _leftTalkingNext;
        bool _rightTalkingNext;
        bool _libraryLoaded;

        GUIStyle _line;
        GUIStyle _plateText;
        GUIStyle _department;
        GUIStyle _banner;
        GUIStyle _message;
        Font _font;

        public static Marks Timeline(bool success)
        {
            var marks = new Marks();
            marks.ScoutInEnd = SlideSeconds;
            marks.ScoutTalkEnd = marks.ScoutInEnd + ScoutLineSeconds;
            marks.GmTalkEnd = marks.ScoutTalkEnd + LineSeconds;
            marks.ScoutOutEnd = marks.GmTalkEnd + SlideSeconds;
            marks.PlayerInEnd = marks.ScoutOutEnd + SlideSeconds;
            marks.GreetEnd = marks.PlayerInEnd + LineSeconds;
            marks.EllipsisEnd = marks.GreetEnd + EllipsisSeconds;
            marks.NegotiateEnd = marks.EllipsisEnd + CenterSeconds;
            marks.ResultEnd = marks.NegotiateEnd + CenterSeconds;
            marks.PlayerExitStart = marks.ResultEnd + LineSeconds;
            marks.PlayerReactEnd = success ? marks.PlayerExitStart : marks.PlayerExitStart + SlideSeconds;
            marks.GmReactEnd = marks.PlayerReactEnd + LineSeconds;
            marks.End = success ? marks.GmReactEnd + DialogSeconds : marks.GmReactEnd;
            return marks;
        }

        /// <summary>結果の文字が出る時刻。成否で変わらない。</summary>
        public static float ResultAt => Timeline(false).NegotiateEnd;

        public static float Duration(bool success) => Timeline(success).End;

        public static Marks TransferTimeline(bool leaves)
        {
            var marks = new Marks();
            marks.ScoutInEnd = SlideSeconds;
            marks.ScoutTalkEnd = marks.ScoutInEnd + LineSeconds;
            marks.GmTalkEnd = marks.ScoutTalkEnd + LineSeconds + 0.6f;
            marks.ScoutOutEnd = marks.GmTalkEnd + SlideSeconds;
            marks.PlayerInEnd = marks.ScoutOutEnd + SlideSeconds;
            marks.GreetEnd = marks.PlayerInEnd + LineSeconds;
            marks.GmGreetEnd = marks.GreetEnd + LineSeconds;
            marks.EllipsisEnd = marks.GmGreetEnd + EllipsisSeconds;
            marks.NegotiateEnd = marks.EllipsisEnd + CenterSeconds;
            marks.ResultEnd = marks.NegotiateEnd + CenterSeconds;
            marks.GmResultEnd = marks.ResultEnd + LineSeconds;
            marks.PlayerExitStart = marks.GmResultEnd + LineSeconds;
            marks.PlayerReactEnd = leaves ? marks.PlayerExitStart + SlideSeconds : marks.PlayerExitStart;
            marks.GmReactEnd = marks.PlayerReactEnd;
            marks.End = leaves ? marks.PlayerReactEnd + DialogSeconds : marks.PlayerReactEnd;
            return marks;
        }

        public static float TransferResultAt => TransferTimeline(false).NegotiateEnd;

        /// <summary>
        /// 契約満了。GM が呼び出し（選手はまだいない）→ 選手が入る → あいさつ → GM が切り出す → 満了を伝える（「契約満了」）
        /// → 選手の返事 → GM の見送り → 選手が出ていく。
        /// </summary>
        public static Marks ExpiryTimeline()
        {
            var marks = new Marks();
            marks.ScoutTalkEnd = LineSeconds + 0.4f;                       // call
            marks.PlayerInEnd = marks.ScoutTalkEnd + SlideSeconds;
            marks.GreetEnd = marks.PlayerInEnd + LineSeconds;              // greet
            marks.GmGreetEnd = marks.GreetEnd + LineSeconds + 0.6f;        // notice
            marks.ResultEnd = marks.GmGreetEnd + LineSeconds + 0.6f;       // verdict
            marks.PlayerReactEnd = marks.ResultEnd + LineSeconds + 0.8f;   // react
            marks.GmReactEnd = marks.PlayerReactEnd + LineSeconds;         // farewell
            marks.PlayerExitStart = marks.GmReactEnd;
            marks.End = marks.PlayerExitStart + SlideSeconds + 0.4f;
            return marks;
        }

        /// <summary>契約満了の文字が出る時刻。</summary>
        public static float ExpiryResultAt => ExpiryTimeline().GmGreetEnd;

        public static float ExpiryDuration => ExpiryTimeline().End;

        string[] _expiryLines;

        /// <summary>新年あいさつ。秘書が左から入り、あいさつ・チームの話・抱負を話す。</summary>
        public static Marks NewYearTimeline()
        {
            var marks = new Marks();
            marks.PlayerInEnd = SlideSeconds;
            marks.GreetEnd = marks.PlayerInEnd + LineSeconds + 0.8f;   // greet
            marks.GmGreetEnd = marks.GreetEnd + LineSeconds + 0.8f;    // review
            marks.ResultEnd = marks.GmGreetEnd + LineSeconds + 0.8f;   // wish
            marks.End = marks.ResultEnd + 0.6f;
            return marks;
        }

        public static float NewYearDuration => NewYearTimeline().End;

        static readonly string[] NewYearRoles = { "greet", "review", "wish" };

        static readonly RoleLineCatalog NewYearLines = new RoleLineCatalog(
            "NewYear.csv",
            NewYearRoles,
            new System.Collections.Generic.Dictionary<string, string>
            {
                { "greet", "オーナー、あけましておめでとうございます！" },
                { "review", "前半戦、選手たちはよく頑張っていました。" },
                { "wish", "今年もよろしくお願いします！" }
            });

        /// <summary>
        /// 季節のイベント。選手（2人なら左右から同時に）が入り、line1（A）、line2（B。1人なら A）、line3（A）の順に話す。
        /// line3 が無い会話は2つで終わる。
        /// </summary>
        public static Marks SeasonalTimeline(int lineCount)
        {
            var marks = new Marks();
            marks.PlayerInEnd = SlideSeconds;
            marks.GreetEnd = marks.PlayerInEnd + LineSeconds + 1.0f;      // line1
            marks.GmGreetEnd = marks.GreetEnd + LineSeconds + 1.0f;       // line2
            marks.ResultEnd = lineCount > 2 ? marks.GmGreetEnd + LineSeconds + 1.0f : marks.GmGreetEnd; // line3
            marks.End = marks.ResultEnd + 0.6f;
            return marks;
        }

        public static float SeasonalDuration(ClubStory story)
        {
            int season;
            var script = SeasonalScript(story, out season);
            return SeasonalTimeline(script != null ? script.LineCount : 2).End;
        }

        /// <summary>WindowKey「季節の id|会話の番号|B の id」から会話を引く。</summary>
        static SeasonalCatalog.Script SeasonalScript(ClubStory story, out int index)
        {
            index = -1;
            if (story == null || string.IsNullOrEmpty(story.WindowKey)) return null;
            var parts = story.WindowKey.Split('|');
            if (parts.Length < 2 || !int.TryParse(parts[1], out index)) return null;
            var script = SeasonalCatalog.ScriptAt(index);
            return script != null && script.SeasonId == parts[0] ? script : null;
        }

        static string SeasonalSeasonId(ClubStory story)
        {
            if (story == null || string.IsNullOrEmpty(story.WindowKey)) return null;
            return story.WindowKey.Split('|')[0];
        }

        static string SeasonalPartnerId(ClubStory story)
        {
            if (story == null || string.IsNullOrEmpty(story.WindowKey)) return null;
            var parts = story.WindowKey.Split('|');
            return parts.Length >= 3 && parts[2].Length > 0 ? parts[2] : null;
        }

        // 右の画面外の待機位置（1218 角の枠の左端）。
        static float OffRightX => GameRunner.ReferenceWidth + GameRunner.HorizontalEdgeInset;

        // ---- 会議イベントにしたお知らせ（ウィンターブレイク・キャンプ・チケット代） ----

        /// <summary>会議室で見せるお知らせの種類か。</summary>
        public static bool IsNoticeKind(ClubStoryKind kind)
        {
            return kind == ClubStoryKind.WinterBreak || kind == ClubStoryKind.Camp || kind == ClubStoryKind.Ticket;
        }

        /// <summary>お知らせを話す人。ウィンターブレイクは秘書、キャンプは監督、チケット代は事業部（いちばんレベルの高い人）。</summary>
        public static StaffMember NoticeSpeaker(GameSession session, ClubStoryKind kind, out string department)
        {
            department = "";
            var club = session != null ? session.PlayerClub : null;
            if (club == null) return null;
            switch (kind)
            {
                case ClubStoryKind.WinterBreak:
                    department = "秘書";
                    return club.Front != null ? club.Front.Secretary : null;
                case ClubStoryKind.Camp:
                    department = "監督";
                    return club.Team != null ? club.Team.Manager : null;
                case ClubStoryKind.Ticket:
                    department = "事業部";
                    StaffMember best = null;
                    var slots = club.Front != null ? club.Front.Business : null;
                    if (slots != null)
                    {
                        for (int i = 0; i < slots.Length; i++)
                        {
                            if (slots[i] != null && (best == null || slots[i].Level > best.Level)) best = slots[i];
                        }
                    }

                    return best;
                default:
                    return null;
            }
        }

        public static Marks NoticeTimeline()
        {
            var marks = new Marks();
            marks.PlayerInEnd = SlideSeconds;
            marks.GreetEnd = marks.PlayerInEnd + LineSeconds + 1.0f;   // 1
            marks.GmGreetEnd = marks.GreetEnd + LineSeconds + 1.2f;    // 2
            marks.End = marks.GmGreetEnd + 0.6f;
            return marks;
        }

        public static float NoticeDuration => NoticeTimeline().End;

        static readonly RoleLineCatalog NoticeLines = new RoleLineCatalog(
            "MeetingNotice.csv",
            new[] { "winter.1", "winter.2", "camp.1", "camp.2", "ticket.1", "ticket.2" },
            new System.Collections.Generic.Dictionary<string, string>
            {
                { "winter.1", "オーナー、ウィンターブレイクに入りました。" },
                { "winter.2", "{start}から{end}までは試合がありません。" },
                { "camp.1", "オーナー、開幕に向けたキャンプが始まりました。" },
                { "camp.2", "1週間、しっかり鍛えて開幕を迎えます。" },
                { "ticket.1", "オーナー、今シーズンのチケット代を決めました。" },
                { "ticket.2", "{price}にしました。" }
            });

        StaffMember _noticeSpeaker;
        string _noticeDepartment;
        Texture2D _noticePortrait;
        string[] _noticeLines;

        void DrawNotice(ClubStory story, GameSession session, float time)
        {
            var marks = NoticeTimeline();
            float leftX = time < marks.PlayerInEnd ? Move(OffX, LeftFrameX, 0f, time) : LeftFrameX;
            DrawFigure(_noticePortrait, leftX, _leftTalking);
            string name = _noticeSpeaker != null && !string.IsNullOrEmpty(_noticeSpeaker.Name) ? _noticeSpeaker.Name : _noticeDepartment;
            if (_noticeLines == null)
                _noticeLines = BuildNoticeLines(story.Kind, session, name);
            if (time >= marks.PlayerInEnd && time < marks.GreetEnd)
                DrawBubble(LeftBubble, _noticeDepartment, name, _noticeLines[0]);
            else if (time >= marks.GreetEnd && time < marks.End)
                DrawBubble(LeftBubble, _noticeDepartment, name, _noticeLines[1]);

            switch (story.Kind)
            {
                case ClubStoryKind.WinterBreak:
                    DrawBanner("ウィンターブレイク");
                    break;
                case ClubStoryKind.Camp:
                    DrawBanner("キャンプ");
                    break;
                case ClubStoryKind.Ticket:
                    DrawBanner("チケット代");
                    // 金額は 2 つ目の台詞のあいだ、右側に 1 段で出す。
                    if (time >= marks.GreetEnd && session != null && session.PlayerClub != null)
                        DrawAmountAt(1180f, 640f, "チケット代", new System.Numerics.BigInteger(session.PlayerClub.TicketPrice));
                    break;
            }
        }

        static string[] BuildNoticeLines(ClubStoryKind kind, GameSession session, string speaker)
        {
            string prefix = kind == ClubStoryKind.WinterBreak ? "winter" : kind == ClubStoryKind.Camp ? "camp" : "ticket";
            var club = session != null ? session.PlayerClub : null;
            int rank = 0;
            int teams = 0;
            if (session != null && session.League != null && club != null)
            {
                var table = session.League.Table();
                teams = table.Count;
                rank = table.IndexOf(club) + 1;
            }

            string start = "";
            string end = "";
            if (session != null)
            {
                var season = session.CurrentSeasonStart;
                start = SoccerClub.Time.WinterBreak.Start(season).ToString("MM/dd", System.Globalization.CultureInfo.InvariantCulture);
                end = SoccerClub.Time.WinterBreak.End(season).ToString("MM/dd", System.Globalization.CultureInfo.InvariantCulture);
            }

            int seasonNo = session != null ? session.CurrentSeasonStart.Year - GameSession.SeasonStart.Year + 1 : 1;
            bool first = seasonNo <= 1;
            bool champion = session != null && session.TitleStreak > 0 && !first;
            var tokens = new System.Collections.Generic.Dictionary<string, string>
            {
                { "speaker", speaker ?? "" },
                { "club", club != null ? club.Name ?? "" : "" },
                { "start", start },
                { "end", end },
                { "price", club != null ? MoneyText.Format(club.TicketPrice) : "" },
                { "rank", rank.ToString() }
            };
            System.Func<string, bool> matches = condition =>
            {
                switch (condition)
                {
                    case "first_season": return first;
                    case "champion": return champion;
                    case "top3": return rank >= 1 && rank <= 3;
                    case "lower": return rank >= 1 && teams > 0 && rank > teams / 2;
                    default: return false;
                }
            };

            var all = NoticeLines.Lines(matches, tokens, new System.Random(seasonNo * 17 + rank));
            var roles = NoticeLines.Roles;
            var result = new string[2];
            for (int i = 0; i < roles.Count; i++)
            {
                if (roles[i] == prefix + ".1") result[0] = all[i];
                else if (roles[i] == prefix + ".2") result[1] = all[i];
            }

            return result;
        }

        /// <summary>金額の 1 段を任意の位置に出す（ラベル 200×60 黒、金額 400×60 灰）。</summary>
        void DrawAmountAt(float x, float y, string caption, System.Numerics.BigInteger amount)
        {
            float valueX = x + (AmountValueX - AmountLabelX);
            Fill(new Rect(valueX, y, AmountValueWidth, AmountHeight), NamePlateColor, AmountHeight * 0.5f);
            GUI.Label(new Rect(valueX + (AmountValueTextX - AmountValueX), y, AmountValueTextWidth, AmountHeight), MoneyText.Format(amount), _plateText);
            Fill(new Rect(x, y, AmountLabelWidth, AmountHeight), Color.black, AmountHeight * 0.5f);
            GUI.Label(new Rect(x + (AmountLabelTextX - AmountLabelX), y, AmountLabelTextWidth, AmountHeight), caption, _department);
        }

        Player _partner;
        Texture2D _partnerPortrait;
        Texture2D _seasonBackground;
        string _seasonBackgroundPath;
        string[] _seasonalLines;

        StaffMember _secretary;
        Texture2D _secretaryPortrait;
        string[] _newYearLines;

        public static float TransferDuration(bool leaves) => TransferTimeline(leaves).End;

        public void Clear()
        {
            _castKey = null;
            _leftTalking = _rightTalking = _leftTalkingNext = _rightTalkingNext = false;
            _scout = null;
            _gm = null;
            _focus = null;
            _onSheet = false;
            _expiryLines = null;
            _newYearLines = null;
            _seasonalLines = null;
            _noticeLines = null;
            _noticeSpeaker = null;
            DestroyTexture(ref _noticePortrait);
            _partner = null;
            DestroyTexture(ref _partnerPortrait);
            _secretary = null;
            DestroyTexture(ref _secretaryPortrait);
            DestroyTexture(ref _scoutPortrait);
            DestroyTexture(ref _gmPortrait);
            DestroyTexture(ref _playerPortrait);
        }

        public void Draw(ClubStory story, GameSession session, Font font, float elapsed, bool outcome)
        {
            if (story == null) return;
            EnsureStyles(font);
            EnsureBackground();
            EnsureCast(story, session);
            // 背景は 16:9 の枠ではなく実際の画面全体まで広げ、比率を保ったまま覆う。
            var screen = FullScreenRect();
            var background = story.Kind == ClubStoryKind.Seasonal ? SeasonBackground(story) ?? _background : _background;
            if (background != null)
                GUI.DrawTexture(screen, background, ScaleMode.ScaleAndCrop, false);
            else
            {
                GUI.color = new Color(0.12f, 0.14f, 0.16f, 1f);
                GUI.DrawTexture(screen, Texture2D.whiteTexture);
                GUI.color = Color.white;
            }

            bool transfer = story.Kind == ClubStoryKind.Transfer;
            float time = elapsed < 0f ? 0f : elapsed;
            _leftTalking = _leftTalkingNext;
            _rightTalking = _rightTalkingNext;
            _leftTalkingNext = false;
            _rightTalkingNext = false;
            if (story.Kind == ClubStoryKind.ContractExpiry)
            {
                DrawExpiry(session, time);
                return;
            }

            if (story.Kind == ClubStoryKind.NewYear)
            {
                DrawNewYear(session, time);
                return;
            }

            if (story.Kind == ClubStoryKind.Seasonal)
            {
                DrawSeasonal(story, session, time);
                return;
            }

            if (IsNoticeKind(story.Kind))
            {
                DrawNotice(story, session, time);
                return;
            }

            var marks = transfer ? TransferTimeline(outcome) : Timeline(outcome);
            DrawFigures(time, marks, transfer ? outcome : !outcome);
            if (transfer)
                DrawTransferLines(time, marks, outcome);
            else
                DrawLines(story, time, marks, outcome);
            if (transfer && outcome && time >= marks.PlayerReactEnd)
            {
                DrawBanner("移籍決定");
                DrawTransferDialog(story);
            }
            else if (!transfer && outcome && time >= marks.GmReactEnd)
            {
                DrawBanner("獲得成功！");
                DrawSuccess(story);
            }
        }

        void DrawFigures(float time, Marks marks, bool slideOut)
        {
            bool playerOn = time >= marks.ScoutOutEnd;
            float leftX;
            if (!playerOn)
            {
                if (time < marks.ScoutInEnd)
                    leftX = Move(OffX, LeftFrameX, 0f, time);
                else if (time < marks.GmTalkEnd)
                    leftX = LeftFrameX;
                else
                    leftX = Move(LeftFrameX, OffX, marks.GmTalkEnd, time);
            }
            else if (slideOut && time >= marks.PlayerExitStart)
                leftX = Move(LeftFrameX, OffX, marks.PlayerExitStart, time);
            else if (time < marks.PlayerInEnd)
                leftX = Move(OffX, LeftFrameX, marks.ScoutOutEnd, time);
            else
                leftX = LeftFrameX;

            var portrait = playerOn ? _playerPortrait : _scoutPortrait;
            DrawFigure(portrait, leftX, _leftTalking);
            DrawFigure(_gmPortrait, RightFrameX, _rightTalking);
        }

        /// <summary>frameX は 1218 角の枠の左端。シートはその中の 212,46 / 1622×1998（1218/2048 倍）に描く。</summary>
        static void DrawFigure(Texture2D portrait, float frameX, bool talking)
        {
            if (portrait == null) return;
            var rect = new Rect(
                frameX + 212f * SheetScale,
                FrameY + 46f * SheetScale,
                1622f * SheetScale,
                1998f * SheetScale);
            // 実際の画面（背景を広げた範囲）の外にあるときは描かない。
            var visible = FullScreenRect();
            if (rect.xMin >= visible.xMax || rect.xMax <= visible.xMin) return;
            GUI.DrawTexture(TalkMotion.Apply(rect, talking), portrait, ScaleMode.StretchToFill, true);
        }

        void DrawLines(ClubStory story, float time, Marks marks, bool success)
        {
            var player = story.Incoming;
            string scoutName = _scout != null && !string.IsNullOrEmpty(_scout.Name) ? _scout.Name : "強化部";
            string gmName = _gm != null && !string.IsNullOrEmpty(_gm.Name) ? _gm.Name : "GM";
            string playerName = player != null && !string.IsNullOrEmpty(player.Name) ? player.Name : "選手";
            if (time >= marks.ScoutInEnd && time < marks.ScoutTalkEnd)
            {
                string position = player != null ? player.BestPosition().ToString() : "";
                int age = player != null ? player.Age : 0;
                DrawBubble(
                    LeftBubble,
                    ScoutDepartment,
                    scoutName,
                    "新しい選手の情報を持ってきました。\n" + position + " の " + playerName + " 選手、" + age + "歳 です。");
            }
            else if (time >= marks.ScoutTalkEnd && time < marks.GmTalkEnd)
                DrawBubble(RightBubble, GmDepartment, gmName, "獲得の交渉をしてみよう。");
            else if (time >= marks.PlayerInEnd && time < marks.GreetEnd)
                DrawBubble(LeftBubble, PlayerDepartment, playerName, "はじめまして、よろしくお願いします。");
            else if (time >= marks.GreetEnd && time < marks.EllipsisEnd)
            {
                DrawBubble(LeftBubble, PlayerDepartment, playerName, "・・・　・・・　・・・");
                DrawBubble(RightBubble, GmDepartment, gmName, "・・・　・・・　・・・");
            }
            else if (time >= marks.ResultEnd && time < marks.PlayerExitStart)
            {
                DrawBubble(
                    LeftBubble,
                    PlayerDepartment,
                    playerName,
                    success ? "チームの力になれるように頑張ります！" : "残念ですが、また会いましょう。");
            }
            else if (time >= marks.PlayerReactEnd && time < marks.GmReactEnd)
            {
                DrawBubble(
                    RightBubble,
                    GmDepartment,
                    gmName,
                    success ? "期待しているよ！" : "今回は縁がなかったようだ...");
            }

            if (time >= marks.EllipsisEnd && time < marks.NegotiateEnd)
                DrawBanner("交渉中...");
            else if (time >= marks.NegotiateEnd && time < (success ? marks.GmReactEnd : marks.End))
                DrawBanner(success ? "交渉成功！" : "交渉失敗...");
        }

        void DrawTransferLines(float time, Marks marks, bool leaves)
        {
            string scoutName = _scout != null && !string.IsNullOrEmpty(_scout.Name) ? _scout.Name : "強化部";
            string gmName = _gm != null && !string.IsNullOrEmpty(_gm.Name) ? _gm.Name : "GM";
            string playerName = _focus != null && !string.IsNullOrEmpty(_focus.Name) ? _focus.Name : "選手";
            if (time >= marks.ScoutInEnd && time < marks.ScoutTalkEnd)
                DrawBubble(LeftBubble, ScoutDepartment, scoutName, playerName + "にオファーが届いています。");
            else if (time >= marks.ScoutTalkEnd && time < marks.GmTalkEnd)
            {
                string line = _onSheet
                    ? playerName + "は必要な選手です。話をしてみましょう。"
                    : playerName + "は最近出場機会が少ないよね。話をしてみましょう。";
                DrawBubble(RightBubble, GmDepartment, gmName, line);
            }
            else if (time >= marks.PlayerInEnd && time < marks.GreetEnd)
                DrawBubble(LeftBubble, PlayerDepartment, playerName, "お疲れ様です。オファーが来てるって聞いています。");
            else if (time >= marks.GreetEnd && time < marks.GmGreetEnd)
                DrawBubble(RightBubble, GmDepartment, gmName, "お疲れ様。少し話をしようか。");
            else if (time >= marks.GmGreetEnd && time < marks.EllipsisEnd)
            {
                DrawBubble(LeftBubble, PlayerDepartment, playerName, "・・・　・・・　・・・");
                DrawBubble(RightBubble, GmDepartment, gmName, "・・・　・・・　・・・");
            }
            else if (time >= marks.ResultEnd && time < marks.GmResultEnd)
            {
                DrawBubble(
                    RightBubble,
                    GmDepartment,
                    gmName,
                    leaves ? "残念だが、新しいクラブで頑張ってくれ..." : "残留してくれて、ありがとう。");
            }
            else if (time >= marks.GmResultEnd && time < marks.PlayerExitStart)
            {
                DrawBubble(
                    LeftBubble,
                    PlayerDepartment,
                    playerName,
                    leaves ? "今まで本当にお世話になりました。" : "チームの力になれるように頑張ります！");
            }

            if (time >= marks.EllipsisEnd && time < marks.NegotiateEnd)
                DrawBanner("交渉中...");
            else if (time >= marks.NegotiateEnd && time < (leaves ? marks.PlayerReactEnd : marks.End))
                DrawBanner(leaves ? "移籍決定..." : "残留決定！");
        }

        void DrawNewYear(GameSession session, float time)
        {
            var marks = NewYearTimeline();
            float leftX = time < marks.PlayerInEnd ? Move(OffX, LeftFrameX, 0f, time) : LeftFrameX;
            DrawFigure(_secretaryPortrait, leftX, _leftTalking);
            string name = _secretary != null && !string.IsNullOrEmpty(_secretary.Name) ? _secretary.Name : "秘書";
            if (_newYearLines == null)
                _newYearLines = BuildNewYearLines(session, name);
            if (time >= marks.PlayerInEnd && time < marks.GreetEnd)
                DrawBubble(LeftBubble, "秘書", name, _newYearLines[0]);
            else if (time >= marks.GreetEnd && time < marks.GmGreetEnd)
                DrawBubble(LeftBubble, "秘書", name, _newYearLines[1]);
            else if (time >= marks.GmGreetEnd && time < marks.End)
                DrawBubble(LeftBubble, "秘書", name, _newYearLines[2]);
            DrawBanner("謹賀新年");
        }

        void DrawSeasonal(ClubStory story, GameSession session, float time)
        {
            int index;
            var script = SeasonalScript(story, out index);
            var season = SeasonalCatalog.Find(SeasonalSeasonId(story));
            int count = script != null ? script.LineCount : 2;
            var marks = SeasonalTimeline(count);
            bool solo = _partner == null;
            float leftX = time < marks.PlayerInEnd ? Move(OffX, LeftFrameX, 0f, time) : LeftFrameX;
            DrawFigure(_playerPortrait, leftX, _leftTalking);
            if (!solo)
            {
                float rightX = time < marks.PlayerInEnd ? Move(OffRightX, RightFrameX, 0f, time) : RightFrameX;
                DrawFigure(_partnerPortrait, rightX, _rightTalking);
            }

            if (_seasonalLines == null)
            {
                var club = session != null ? session.PlayerClub : null;
                int year = session != null ? session.Time.CurrentDate.Year : System.DateTime.Today.Year;
                _seasonalLines = SeasonalCatalog.Lines(
                    script,
                    new SeasonalCatalog.Cast { A = _focus, B = _partner },
                    club != null ? club.Name : "",
                    year);
            }

            string nameA = _focus != null && !string.IsNullOrEmpty(_focus.Name) ? _focus.Name : "選手";
            string nameB = _partner != null && !string.IsNullOrEmpty(_partner.Name) ? _partner.Name : nameA;
            var lines = _seasonalLines;
            if (lines.Length > 0 && time >= marks.PlayerInEnd && time < marks.GreetEnd)
                DrawBubble(LeftBubble, PlayerDepartment, nameA, lines[0]);
            else if (lines.Length > 1 && time >= marks.GreetEnd && time < marks.GmGreetEnd)
                DrawBubble(solo ? LeftBubble : RightBubble, PlayerDepartment, solo ? nameA : nameB, lines[1]);
            else if (lines.Length > 2 && time >= marks.GmGreetEnd && time < marks.ResultEnd)
                DrawBubble(LeftBubble, PlayerDepartment, nameA, lines[2]);
            if (season != null && !string.IsNullOrEmpty(season.Banner))
                DrawBanner(season.Banner);
        }

        /// <summary>季節の背景。Seasons.csv の background を Resources から読み、無ければ null（会議室の背景を使う）。</summary>
        Texture2D SeasonBackground(ClubStory story)
        {
            var season = SeasonalCatalog.Find(SeasonalSeasonId(story));
            string path = season != null ? season.Background : null;
            if (string.IsNullOrEmpty(path)) return null;
            if (_seasonBackgroundPath != path)
            {
                _seasonBackgroundPath = path;
                _seasonBackground = Resources.Load<Texture2D>(path);
            }

            return _seasonBackground;
        }

        static string[] BuildNewYearLines(GameSession session, string secretaryName)
        {
            var club = session != null ? session.PlayerClub : null;
            int rank = 0;
            int teams = 0;
            if (session != null && session.League != null && club != null)
            {
                var table = session.League.Table();
                teams = table.Count;
                rank = table.IndexOf(club) + 1;
            }

            var today = session != null ? session.Time.CurrentDate.Date : System.DateTime.Today;
            int season = session != null ? session.CurrentSeasonStart.Year - GameSession.SeasonStart.Year + 1 : 1;
            bool firstSeason = season <= 1;
            bool champion = session != null && session.TitleStreak > 0;
            var tokens = new System.Collections.Generic.Dictionary<string, string>
            {
                { "year", today.Year.ToString() },
                { "season", season.ToString() },
                { "rank", rank.ToString() },
                { "club", club != null ? club.Name ?? "" : "" },
                { "secretary", secretaryName ?? "" },
                { "league", session != null ? session.LeagueLevel.ToString() : "1" }
            };

            System.Func<string, bool> matches = condition =>
            {
                switch (condition)
                {
                    case "first_season": return firstSeason;
                    case "top": return rank == 1;
                    case "top3": return rank >= 1 && rank <= 3;
                    case "upper": return rank >= 1 && teams > 0 && rank <= teams / 2;
                    case "lower": return rank >= 1 && teams > 0 && rank > teams / 2;
                    case "bottom3": return rank >= 1 && teams > 0 && rank > teams - 3;
                    case "champion": return champion && !firstSeason;
                    default: return false;
                }
            };

            return NewYearLines.Lines(matches, tokens, new System.Random(today.Year * 31 + rank));
        }

        void DrawExpiry(GameSession session, float time)
        {
            var marks = ExpiryTimeline();
            float leftX;
            if (time < marks.ScoutTalkEnd) leftX = OffX;
            else if (time < marks.PlayerInEnd) leftX = Move(OffX, LeftFrameX, marks.ScoutTalkEnd, time);
            else if (time < marks.PlayerExitStart) leftX = LeftFrameX;
            else leftX = Move(LeftFrameX, OffX, marks.PlayerExitStart, time);
            DrawFigure(_playerPortrait, leftX, _leftTalking);
            DrawFigure(_gmPortrait, RightFrameX, _rightTalking);

            string gmName = _gm != null && !string.IsNullOrEmpty(_gm.Name) ? _gm.Name : "GM";
            string playerName = _focus != null && !string.IsNullOrEmpty(_focus.Name) ? _focus.Name : "選手";
            if (_expiryLines == null)
            {
                string spouse = null;
                if (session != null && session.Relations != null && _focus != null)
                {
                    var partner = session.Relations.SpouseIn(session.PlayerClub, _focus);
                    if (partner != null) spouse = partner.Name;
                }

                _expiryLines = ContractExpiryCatalog.Lines(new ContractExpiryCatalog.Context
                {
                    Player = _focus,
                    GmName = gmName,
                    SpouseName = spouse
                });
            }

            var lines = _expiryLines;
            if (time < marks.ScoutTalkEnd)
                DrawBubble(RightBubble, GmDepartment, gmName, lines[0]);
            else if (time >= marks.PlayerInEnd && time < marks.GreetEnd)
                DrawBubble(LeftBubble, PlayerDepartment, playerName, lines[1]);
            else if (time >= marks.GreetEnd && time < marks.GmGreetEnd)
                DrawBubble(RightBubble, GmDepartment, gmName, lines[2]);
            else if (time >= marks.GmGreetEnd && time < marks.ResultEnd)
                DrawBubble(RightBubble, GmDepartment, gmName, lines[3]);
            else if (time >= marks.ResultEnd && time < marks.PlayerReactEnd)
                DrawBubble(LeftBubble, PlayerDepartment, playerName, lines[4]);
            else if (time >= marks.PlayerReactEnd && time < marks.GmReactEnd)
                DrawBubble(RightBubble, GmDepartment, gmName, lines[5]);

            if (time >= marks.GmGreetEnd && time < marks.End)
                DrawBanner("契約満了");
        }

        /// <summary>
        /// セリフの枠と、その上辺に重ねる「部署名＋名前」の帯。Figma meeting_event の character_message_l / _r。
        /// 名前の帯は灰色に黒文字、左に部署名の黒い帯を重ねる。
        /// </summary>
        void DrawBubble(Rect bubble, string department, string name, string line)
        {
            if (bubble == LeftBubble) _leftTalkingNext = true;
            else if (bubble == RightBubble) _rightTalkingNext = true;
            GUI.color = Color.white;
            Fill(bubble, new Color(1f, 1f, 1f, BoxAlpha), BoxRadius);
            if (!string.IsNullOrEmpty(line))
                GUI.Label(new Rect(bubble.x + LineOffsetX, bubble.y + LineOffsetY, LineWidth, LineHeight), line, _line);

            Fill(new Rect(bubble.x + NamePlateOffset, PlateY, NamePlateWidth, PlateHeight), NamePlateColor, PlateHeight * 0.5f);
            GUI.Label(new Rect(bubble.x + NameTextOffset, PlateY, NameTextWidth, PlateHeight), name ?? "", _plateText);
            Fill(new Rect(bubble.x + DepartmentPlateOffset, PlateY, DepartmentPlateWidth, PlateHeight), Color.black, PlateHeight * 0.5f);
            GUI.Label(new Rect(bubble.x + DepartmentTextOffset, PlateY, DepartmentTextWidth, PlateHeight), department ?? "", _department);
        }

        /// <summary>UiFitMatrix の基準座標で、実際の画面全体を表す矩形。16:9 ちょうどなら 0,0,1920,1080。</summary>
        static Rect FullScreenRect()
        {
            float ix = GameRunner.HorizontalEdgeInset;
            float iy = GameRunner.VerticalEdgeInset;
            return new Rect(-ix, -iy, GameRunner.ReferenceWidth + ix * 2f, GameRunner.ReferenceHeight + iy * 2f);
        }

        static void Fill(Rect rect, Color color, float radius)
        {
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, color, 0f, radius);
        }

        void DrawBanner(string text)
        {
            GUI.Label(Banner, text ?? "", _banner);
        }

        void DrawResultPanel(string message)
        {
            GUI.color = Color.white;
            Fill(ResultPanel, new Color(1f, 1f, 1f, BoxAlpha), BoxRadius);
            GUI.Label(ResultMessage, message, _message);
        }

        void DrawSuccess(ClubStory story)
        {
            var total = story.Fee + story.Salary;
            DrawResultPanel("獲得成功です！\n契約金と年俸、合わせて" + MoneyText.Format(total) + "を強化費から支払います。");
            DrawAmount(FirstAmountY, "契約金", story.Fee);
            DrawAmount(SecondAmountY, "年俸", story.Salary);
        }

        void DrawTransferDialog(ClubStory story)
        {
            DrawResultPanel("移籍が決定しました。\n移籍金が支払われます。");
            DrawAmount(SingleAmountY, "移籍金", story.Fee);
        }

        /// <summary>金額の 1 段。灰色の金額の帯の左端に、黒いラベルの帯を重ねる。</summary>
        void DrawAmount(float y, string caption, System.Numerics.BigInteger amount)
        {
            Fill(new Rect(AmountValueX, y, AmountValueWidth, AmountHeight), NamePlateColor, AmountHeight * 0.5f);
            GUI.Label(new Rect(AmountValueTextX, y, AmountValueTextWidth, AmountHeight), MoneyText.Format(amount), _plateText);
            Fill(new Rect(AmountLabelX, y, AmountLabelWidth, AmountHeight), Color.black, AmountHeight * 0.5f);
            GUI.Label(new Rect(AmountLabelTextX, y, AmountLabelTextWidth, AmountHeight), caption, _department);
        }

        void EnsureCast(ClubStory story, GameSession session)
        {
            var player = FocusPlayer(story, session);
            string id = player != null && !string.IsNullOrEmpty(player.Id)
                ? player.Id
                : story.PlayerId ?? "";
            string key = story.Kind + ":" + id;
            if (_castKey == key) return;
            Clear();
            _castKey = key;
            _focus = player;
            _onSheet = session != null && session.IsMatchSheetPlayer(id);
            if (!_libraryLoaded)
            {
                _libraryLoaded = true;
                _avatars.Load();
            }

            var club = session != null ? session.PlayerClub : null;
            if (club == null || !_avatars.Ready) return;
            var uniform = AvatarLibrary.Kit(club.EmblemIndex);
            var front = club.Front;
            _gm = front != null ? front.GeneralManager : null;
            var position = player != null ? player.BestPosition() : Position.CF;
            _scout = FindScout(front, position);
            _gmPortrait = Portrait(_gm, uniform);
            if (IsNoticeKind(story.Kind))
            {
                _noticeSpeaker = NoticeSpeaker(session, story.Kind, out _noticeDepartment);
                _noticePortrait = Portrait(_noticeSpeaker, uniform);
            }

            if (story.Kind == ClubStoryKind.Seasonal)
            {
                string partnerId = SeasonalPartnerId(story);
                _partner = FindSquadPlayer(session, partnerId);
                if (_partner != null)
                {
                    var partnerHair = PositionUtility.GroupColor(PositionUtility.GroupOf(_partner.BestPosition()));
                    _partnerPortrait = _avatars.CreateStand(_partner.Id, uniform, partnerHair, AvatarLibrary.SheetHeight);
                }
            }

            if (story.Kind == ClubStoryKind.NewYear)
            {
                _secretary = front != null ? front.Secretary : null;
                _secretaryPortrait = Portrait(_secretary, uniform);
            }
            _scoutPortrait = Portrait(_scout, uniform);
            if (!string.IsNullOrEmpty(id))
            {
                var hair = player != null
                    ? PositionUtility.GroupColor(PositionUtility.GroupOf(position))
                    : AvatarLibrary.NaturalHair(id);
                _playerPortrait = _avatars.CreateStand(id, uniform, hair, AvatarLibrary.SheetHeight);
            }
        }

        static Player FindSquadPlayer(GameSession session, string id)
        {
            if (string.IsNullOrEmpty(id) || session == null || session.PlayerClub == null || session.PlayerClub.Squad == null) return null;
            var squad = session.PlayerClub.Squad;
            for (int i = 0; i < squad.Count; i++)
            {
                if (squad[i] != null && squad[i].Id == id) return squad[i];
            }

            return null;
        }

        static Player FocusPlayer(ClubStory story, GameSession session)
        {
            if (story == null) return null;
            if (story.Kind == ClubStoryKind.Signing || story.Kind == ClubStoryKind.ContractExpiry) return story.Incoming;
            if (session == null || session.PlayerClub == null || session.PlayerClub.Squad == null) return null;
            if (string.IsNullOrEmpty(story.PlayerId)) return null;
            var squad = session.PlayerClub.Squad;
            for (int i = 0; i < squad.Count; i++)
            {
                var player = squad[i];
                if (player != null && player.Id == story.PlayerId) return player;
            }

            return null;
        }

        /// <summary>GM とスカウト（強化部）。フロントなのでスーツ。</summary>
        Texture2D Portrait(StaffMember staff, UniformKit uniform)
        {
            if (staff == null || string.IsNullOrEmpty(staff.Id)) return null;
            return _avatars.CreateStand(
                staff.Id, uniform, AvatarLibrary.NaturalHair(staff.Id), AvatarLibrary.SheetHeight, AvatarLibrary.OutfitOf(staff));
        }

        static StaffMember FindScout(FrontOffice front, Position position)
        {
            if (front == null || front.Strengthening == null) return null;
            var group = PositionUtility.GroupOf(position);
            StaffMember any = null;
            for (int i = 0; i < front.Strengthening.Length; i++)
            {
                var scout = front.Strengthening[i];
                if (scout == null) continue;
                if (any == null) any = scout;
                if (scout.ScoutedGroup == group) return scout;
            }

            return any;
        }

        void EnsureBackground()
        {
            if (_background == null)
                _background = Resources.Load<Texture2D>("MainScreen/bg_meeting_room");
        }

        void EnsureStyles(Font font)
        {
            if (_line != null && _font == font) return;
            _font = font;
            var label = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 32,
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Overflow,
                wordWrap = false,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
                normal = { textColor = Color.black }
            };
            _line = new GUIStyle(label) { alignment = TextAnchor.MiddleLeft, wordWrap = true };
            _plateText = new GUIStyle(label);
            _department = new GUIStyle(label) { normal = { textColor = Color.white } };
            _banner = new GUIStyle(label) { fontSize = 128, fontStyle = FontStyle.Bold };
            _message = new GUIStyle(label) { alignment = TextAnchor.UpperCenter };
        }

        static float Move(float from, float to, float start, float time)
        {
            float t = Mathf.Clamp01((time - start) / SlideSeconds);
            return Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t));
        }

        static void DestroyTexture(ref Texture2D texture)
        {
            if (texture == null) return;
            UnityEngine.Object.Destroy(texture);
            texture = null;
        }
    }
}
