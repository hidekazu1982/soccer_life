using SoccerClub.Core;
using SoccerClub.Data;
using UnityEngine;

namespace SoccerClub.UI
{
    /// <summary>会議室での補強と移籍。GMは右に立ち、強化部と選手が左から入る。</summary>
    public sealed class MeetingRoomView
    {
        public const float SlideSeconds = 0.6f;
        public const float ScoutLineSeconds = 2.6f;
        public const float LineSeconds = 2.0f;
        public const float EllipsisSeconds = 1.8f;
        public const float CenterSeconds = 1.6f;
        public const float DialogSeconds = 3.4f;

        const float CharacterY = 247f;
        const float CharacterW = 692f;
        const float CharacterH = 971f;
        const float LeftRestX = 200f;
        const float RightX = 1028f;
        // 画面外の待機位置。16:9 より横長の端末では背景が左右に広がるので、その分さらに外へ出す。
        const float OffXBase = -740f;
        static float OffX => OffXBase - GameRunner.HorizontalEdgeInset;

        static readonly Rect LeftBubble = new Rect(143f, 88f, 800f, 160f);
        // 吹き出しの帯。Figma sponsor の message（吹き出し 112,848）から、部署 233 / 名前 391、高さ 60、上辺の中央に重ねる。
        const float PlateHeight = 60f;
        const float DepartmentPlateOffset = 121f;
        const float DepartmentPlateWidth = 200f;
        const float NamePlateOffset = 279f;
        const float NamePlateWidth = 400f;
        static readonly Color NamePlateColor = new Color(0xD1 / 255f, 0xD1 / 255f, 0xD6 / 255f, 1f);
        const string ScoutDepartment = "強化部";
        const string GmDepartment = "GM";
        const string PlayerDepartment = "選手";
        static readonly Rect RightBubble = new Rect(971f, 87f, 800f, 160f);
        static readonly Rect Banner = new Rect(640f, 309f, 640f, 155f);
        static readonly Rect SuccessPanel = new Rect(480f, 466f, 960f, 240f);
        static readonly Rect SuccessMessage = new Rect(534f, 499f, 852f, 78f);
        static readonly Rect FeeBar = new Rect(780f, 593f, 360f, 32f);
        static readonly Rect FeeLabel = new Rect(780f, 593f, 92f, 32f);
        static readonly Rect SalaryBar = new Rect(780f, 641f, 360f, 32f);
        static readonly Rect SalaryLabel = new Rect(780f, 641f, 92f, 32f);

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
        Texture2D _disc;
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

        GUIStyle _lineCenter;
        GUIStyle _lineRight;
        GUIStyle _name;
        GUIStyle _banner;
        GUIStyle _message;
        GUIStyle _pillLabel;
        GUIStyle _pillValue;
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

        public static float TransferDuration(bool leaves) => TransferTimeline(leaves).End;

        public void Clear()
        {
            _castKey = null;
            _leftTalking = _rightTalking = _leftTalkingNext = _rightTalkingNext = false;
            _scout = null;
            _gm = null;
            _focus = null;
            _onSheet = false;
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
            if (_background != null)
                GUI.DrawTexture(screen, _background, ScaleMode.ScaleAndCrop, false);
            else
            {
                GUI.color = new Color(0.12f, 0.14f, 0.16f, 1f);
                GUI.DrawTexture(screen, Texture2D.whiteTexture);
                GUI.color = Color.white;
            }

            bool transfer = story.Kind == ClubStoryKind.Transfer;
            var marks = transfer ? TransferTimeline(outcome) : Timeline(outcome);
            float time = elapsed < 0f ? 0f : elapsed;
            _leftTalking = _leftTalkingNext;
            _rightTalking = _rightTalkingNext;
            _leftTalkingNext = false;
            _rightTalkingNext = false;
            DrawFigures(time, marks, transfer ? outcome : !outcome);
            if (transfer)
                DrawTransferLines(time, marks, outcome);
            else
                DrawLines(story, time, marks, outcome);
            if (transfer && outcome && time >= marks.PlayerReactEnd)
                DrawTransferDialog(story);
            else if (!transfer && outcome && time >= marks.GmReactEnd)
                DrawSuccess(story);
        }

        void DrawFigures(float time, Marks marks, bool slideOut)
        {
            bool playerOn = time >= marks.ScoutOutEnd;
            float leftX;
            if (!playerOn)
            {
                if (time < marks.ScoutInEnd)
                    leftX = Move(OffX, LeftRestX, 0f, time);
                else if (time < marks.GmTalkEnd)
                    leftX = LeftRestX;
                else
                    leftX = Move(LeftRestX, OffX, marks.GmTalkEnd, time);
            }
            else if (slideOut && time >= marks.PlayerExitStart)
                leftX = Move(LeftRestX, OffX, marks.PlayerExitStart, time);
            else if (time < marks.PlayerInEnd)
                leftX = Move(OffX, LeftRestX, marks.ScoutOutEnd, time);
            else
                leftX = LeftRestX;

            var portrait = playerOn ? _playerPortrait : _scoutPortrait;
            DrawFigure(portrait, leftX, _leftTalking);
            DrawFigure(_gmPortrait, RightX, _rightTalking);
        }

        static void DrawFigure(Texture2D portrait, float x, bool talking)
        {
            if (portrait == null) return;
            // シートは髪型のぶん左右に広い。高さを CharacterH に合わせ、体の中心を CharacterW の中央に置く。
            float width = CharacterH * (portrait.width / (float)Mathf.Max(1, portrait.height));
            float left = x + (CharacterW - width) * 0.5f;
            // 体の枠（CharacterW）が実際の画面（背景を広げた範囲）の外にあるときは描かない。
            var visible = FullScreenRect();
            if (x >= visible.xMax || x + CharacterW <= visible.xMin) return;
            var rect = TalkMotion.Apply(new Rect(left, CharacterY, width, CharacterH), talking);
            GUI.DrawTexture(rect, portrait, ScaleMode.StretchToFill, true);
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
                DrawBubble(LeftBubble, ScoutDepartment, scoutName, null);
                GUI.Label(new Rect(163f, 129f, 760f, 39f), "新しい選手情報をもってきました。", _lineCenter);
                GUI.Label(
                    new Rect(163f, 168f, 760f, 39f),
                    position + " の " + playerName + " 選手、" + age + "歳 です。",
                    _lineRight);
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

        /// <summary>
        /// 吹き出しと、その上辺に重ねる「部署名＋名前」の帯。スポンサー獲得（Figma sponsor の message）と同じ形。
        /// 名前の帯は灰色に黒文字、左端に部署名の黒い帯を重ねる。
        /// </summary>
        void DrawBubble(Rect bubble, string department, string name, string line)
        {
            if (bubble == LeftBubble) _leftTalkingNext = true;
            else if (bubble == RightBubble) _rightTalkingNext = true;
            GUI.color = Color.white;
            GUI.DrawTexture(bubble, Texture2D.whiteTexture);

            float top = bubble.y - PlateHeight * 0.5f;
            var namePlate = new Rect(bubble.x + NamePlateOffset, top, NamePlateWidth, PlateHeight);
            var deptPlate = new Rect(bubble.x + DepartmentPlateOffset, top, DepartmentPlateWidth, PlateHeight);
            FillPill(namePlate, NamePlateColor);
            GUI.Label(new Rect(namePlate.x + 50f, top, namePlate.width - 100f, PlateHeight), name ?? "", _lineCenter);
            FillPill(deptPlate, Color.black);
            GUI.Label(new Rect(deptPlate.x + 29f, top, deptPlate.width - 40f, PlateHeight), department ?? "", _name);

            if (!string.IsNullOrEmpty(line))
                GUI.Label(bubble, line, _lineCenter);
        }

        /// <summary>UiFitMatrix の基準座標で、実際の画面全体を表す矩形。16:9 ちょうどなら 0,0,1920,1080。</summary>
        static Rect FullScreenRect()
        {
            float ix = GameRunner.HorizontalEdgeInset;
            float iy = GameRunner.VerticalEdgeInset;
            return new Rect(-ix, -iy, GameRunner.ReferenceWidth + ix * 2f, GameRunner.ReferenceHeight + iy * 2f);
        }

        static void FillPill(Rect rect, Color color)
        {
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, color, 0f, rect.height * 0.5f);
        }

        void DrawBanner(string text)
        {
            _banner.fontSize = text != null && text.Length >= 7 ? 84 : 96;
            GUI.Label(Banner, text ?? "", _banner);
        }

        void DrawSuccess(ClubStory story)
        {
            GUI.color = Color.white;
            GUI.DrawTexture(SuccessPanel, Texture2D.whiteTexture);
            var total = story.Fee + story.Salary;
            GUI.Label(
                SuccessMessage,
                "獲得成功です！\n契約金と年俸、合わせて" + MoneyText.Format(total) + "を強化費から支払います。",
                _message);
            DrawMoney(FeeBar, FeeLabel, "契約金", story.Fee);
            DrawMoney(SalaryBar, SalaryLabel, "年俸", story.Salary);
        }

        void DrawTransferDialog(ClubStory story)
        {
            GUI.color = Color.white;
            GUI.DrawTexture(SuccessPanel, Texture2D.whiteTexture);
            GUI.Label(SuccessMessage, "移籍が決定しました。\n移籍金が支払われます。", _message);
            var fee = new Rect(780f, 610f, 360f, 32f);
            var label = new Rect(780f, 610f, 92f, 32f);
            DrawMoney(fee, label, "移籍金", story.Fee);
        }

        void DrawMoney(Rect bar, Rect label, string caption, System.Numerics.BigInteger amount)
        {
            DrawPlate(bar, 0.5f);
            DrawPlate(label, 0.5f);
            GUI.Label(label, caption, _pillLabel);
            GUI.Label(new Rect(bar.x + 100f, bar.y, bar.width - 112f, bar.height), MoneyText.Format(amount), _pillValue);
        }

        void DrawPlate(Rect rect, float alpha)
        {
            EnsureDisc();
            var previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, alpha);
            float d = rect.height;
            GUI.DrawTexture(new Rect(rect.x, rect.y, d, d), _disc, ScaleMode.StretchToFill, true);
            GUI.DrawTexture(new Rect(rect.xMax - d, rect.y, d, d), _disc, ScaleMode.StretchToFill, true);
            if (rect.width > d)
                GUI.DrawTexture(new Rect(rect.x + d * 0.5f, rect.y, rect.width - d, rect.height), Texture2D.whiteTexture);
            GUI.color = previous;
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
            _scoutPortrait = Portrait(_scout, uniform);
            if (!string.IsNullOrEmpty(id))
            {
                var hair = player != null
                    ? PositionUtility.GroupColor(PositionUtility.GroupOf(position))
                    : AvatarLibrary.NaturalHair(id);
                _playerPortrait = _avatars.CreateStand(id, uniform, hair, AvatarLibrary.SheetHeight);
            }
        }

        static Player FocusPlayer(ClubStory story, GameSession session)
        {
            if (story == null) return null;
            if (story.Kind == ClubStoryKind.Signing) return story.Incoming;
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

        void EnsureDisc()
        {
            if (_disc != null) return;
            const int size = 32;
            _disc = new Texture2D(size, size, TextureFormat.RGBA32, false);
            _disc.hideFlags = HideFlags.HideAndDontSave;
            var pixels = new Color32[size * size];
            float radius = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - radius;
                    float dy = y - radius;
                    if (dx * dx + dy * dy <= radius * radius)
                        pixels[y * size + x] = new Color32(255, 255, 255, 255);
                }
            }

            _disc.SetPixels32(pixels);
            _disc.Apply(false, true);
        }

        void EnsureStyles(Font font)
        {
            if (_lineCenter != null && _font == font) return;
            _font = font;
            _lineCenter = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 32,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                clipping = TextClipping.Overflow,
                normal = { textColor = Color.black }
            };
            _lineRight = new GUIStyle(_lineCenter) { alignment = TextAnchor.MiddleRight };
            _name = new GUIStyle(_lineCenter)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            _banner = new GUIStyle(_lineCenter)
            {
                fontSize = 96,
                alignment = TextAnchor.MiddleCenter
            };
            _message = new GUIStyle(_lineCenter)
            {
                wordWrap = true,
                alignment = TextAnchor.MiddleCenter
            };
            _pillLabel = new GUIStyle(_lineCenter)
            {
                fontSize = 20,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            _pillValue = new GUIStyle(_lineCenter)
            {
                fontSize = 24,
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = Color.white }
            };
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
