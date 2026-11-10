using System.Globalization;
using BigInteger = System.Numerics.BigInteger;
using SoccerClub.Core;
using SoccerClub.Data;
using UnityEngine;

namespace SoccerClub.UI
{
    public enum SupportChoice
    {
        None,
        Accept,
        Cancel
    }

    /// <summary>
    /// サポートキャラ。Figma main の support（左下で「オーナー お話があります！」）と、
    /// タップで開く support_event（会議室で施設のレベルアップかフロント人事を確認する）。
    /// レイアウトは sponsor と同じ。
    /// </summary>
    public sealed class SupportEventView
    {
        // ---- main の support（184:39）。480 角の character3 を 0,740 に置く。シートはその中の 212,46 / 1622x1998（480/2048 倍）。
        static readonly Rect CalloutCharacter = new Rect(49.69f, 750.78f, 380.16f, 468.28f);
        static readonly Rect CalloutBubble = new Rect(120f, 640f, 240f, 100f);
        static readonly Rect CalloutPointer = new Rect(230f, 722f, 20f, 40f);
        static readonly Rect CalloutText = new Rect(120f, 652f, 240f, 76f);
        // 当たり判定。吹き出しと、画面内に見えている上半身。
        static readonly Rect CalloutHit = new Rect(49.69f, 640f, 380.16f, 440f);
        const int CalloutPixelHeight = 468;

        // ---- support_event（172:2）。sponsor と同じ配置。
        static readonly Rect CharacterRect = new Rect(29f, 27f, 965f, 1188f);
        static readonly Rect MessageBox = new Rect(112f, 848f, 800f, 200f);
        static readonly Rect MessageText = new Rect(152f, 896f, 720f, 120f);
        static readonly Rect SpeakerPlate = new Rect(391f, 818f, 400f, 60f);
        static readonly Rect SpeakerText = new Rect(441f, 818f, 300f, 60f);
        static readonly Rect DepartmentPlate = new Rect(233f, 818f, 200f, 60f);
        static readonly Rect DepartmentText = new Rect(262f, 818f, 160f, 60f);
        static readonly Color SpeakerPlateColor = new Color(0xD1 / 255f, 0xD1 / 255f, 0xD6 / 255f, 1f);

        static readonly Rect Sheet = new Rect(1059.63f, 210f, 800f, 692f);
        const float BackSheetAngle = -5f;
        static readonly Rect SheetTitle = new Rect(1059.63f, 249f, 800f, 68f);
        static readonly Rect TitlePlate = new Rect(1259.63f, 353f, 400f, 60f);
        const float RowX = 1224.63f;
        const float RowLabelWidth = 200f;
        const float RowBarX = 1334.63f;
        const float RowBarWidth = 360f;
        const float RowHeight = 32f;
        const float RowUnitX = 1653.63f;
        const float RowTextRight = 1680.63f;
        const float FirstRowY = 445f;
        const float SecondRowY = 485f;
        const float ThirdRowY = 525f;
        static readonly Rect EffectBox = new Rect(1224.63f, 605f, 470f, 160f);
        static readonly Rect EffectPlate = new Rect(1359.63f, 589f, 200f, 32f);
        static readonly Rect CancelButton = new Rect(1288.63f, 818f, 155f, 60f);
        static readonly Rect AcceptButton = new Rect(1475.63f, 818f, 155f, 60f);

        const float PanelRadius = 10f;
        const float MessageAlpha = 0.9f;
        static readonly Color RowBarColor = new Color(0f, 0f, 0f, 0.1f);
        // 契約書の影: 0 2px 4px 4px rgba(0,0,0,0.25)。吹き出しの影: 0 4px 4px rgba(0,0,0,0.25)。
        const float ShadowAlpha = 0.25f;
        const int ShadowSteps = 4;

        readonly AvatarLibrary _avatars = new AvatarLibrary();
        bool _libraryLoaded;
        Texture2D _background;
        Texture2D _button;
        Texture2D _pointer;
        Texture2D _portrait;
        string _portraitKey;
        Texture2D _callout;
        string _calloutKey;

        GUIStyle _title;
        GUIStyle _plate;
        GUIStyle _speaker;
        GUIStyle _message;
        GUIStyle _bubble;
        GUIStyle _rowLabel;
        GUIStyle _rowValue;
        GUIStyle _rowText;
        GUIStyle _rowUnit;
        GUIStyle _effect;
        GUIStyle _buttonLabel;
        GUIStyle _hit;
        Font _font;

        /// <summary>main の基準座標で、サポートキャラの当たり判定。rect は GameRunner が端寄せした原点を足したもの。</summary>
        public static Rect CalloutBounds(Vector2 origin)
        {
            return Offset(CalloutHit, origin);
        }

        /// <summary>main の左下に、話しかけてくるサポートキャラを描く。タップされたら true。</summary>
        public bool DrawCallout(SupportSuggestion suggestion, GameSession session, Font font, Vector2 origin)
        {
            if (suggestion == null) return false;
            EnsureStyles(font);
            EnsureTextures();
            var club = session != null ? session.PlayerClub : null;
            var speaker = FindSpeaker(club, suggestion);
            EnsureCallout(speaker, club);

            GUI.color = Color.white;
            if (_callout != null)
                GUI.DrawTexture(TalkMotion.Apply(Offset(CalloutCharacter, origin), true), _callout, ScaleMode.StretchToFill, true);

            var bubble = Offset(CalloutBubble, origin);
            DrawShadow(bubble, 4f, 0f, 4f, bubble.height * 0.5f);
            if (_pointer != null)
                GUI.DrawTexture(Offset(CalloutPointer, origin), _pointer, ScaleMode.StretchToFill, true);
            Fill(bubble, Color.white, bubble.height * 0.5f);
            GUI.Label(Offset(CalloutText, origin), "オーナー\nお話があります！", _bubble);

            return GUI.Button(Offset(CalloutHit, origin), GUIContent.none, _hit);
        }

        /// <summary>support_event。キャンセルかレベルアップ（契約する）を返す。</summary>
        public SupportChoice Draw(SupportSuggestion suggestion, GameSession session, Font font)
        {
            if (suggestion == null) return SupportChoice.None;
            EnsureStyles(font);
            EnsureTextures();
            var club = session != null ? session.PlayerClub : null;
            var speaker = FindSpeaker(club, suggestion);
            EnsurePortrait(speaker, club);

            GUI.color = Color.white;
            var screen = FullScreenRect();
            if (_background != null)
                GUI.DrawTexture(screen, _background, ScaleMode.ScaleAndCrop, false);
            else
                Fill(screen, new Color(0.12f, 0.14f, 0.16f, 1f), 0f);
            if (_portrait != null)
                GUI.DrawTexture(TalkMotion.Apply(CharacterRect, true), _portrait, ScaleMode.StretchToFill, true);

            DrawMessage(suggestion, speaker);
            return suggestion.Kind == SupportKind.Facility
                ? DrawFacilitySheet(suggestion, club)
                : DrawFrontSheet(suggestion);
        }

        /// <summary>画面を閉じたあとに立ち絵を捨てる。main の小さい立ち絵は残す。</summary>
        public void Clear()
        {
            _portraitKey = null;
            if (_portrait != null)
            {
                Object.Destroy(_portrait);
                _portrait = null;
            }
        }

        void DrawMessage(SupportSuggestion suggestion, StaffMember speaker)
        {
            Fill(MessageBox, new Color(1f, 1f, 1f, MessageAlpha), PanelRadius);
            GUI.Label(MessageText, MessageFor(suggestion), _message);
            Fill(SpeakerPlate, SpeakerPlateColor, SpeakerPlate.height * 0.5f);
            string name = speaker != null && !string.IsNullOrEmpty(speaker.Name) ? speaker.Name : "スタッフ";
            GUI.Label(SpeakerText, name, _speaker);
            Fill(DepartmentPlate, Color.black, DepartmentPlate.height * 0.5f);
            string department = speaker != null ? RoleLabel(speaker.FrontRole) : "フロント";
            GUI.Label(DepartmentText, department, _plate);
        }

        static string MessageFor(SupportSuggestion suggestion)
        {
            if (suggestion.Kind == SupportKind.Facility)
                return FacilityLabel(suggestion.Facility) + "のレベルアップを\n検討しませんか？";
            string role = RoleLabel(suggestion.Role);
            if (suggestion.Current == null)
                return role + "に空きがあります。\n契約を検討しませんか？";
            return role + "の交代を\n検討しませんか？";
        }

        SupportChoice DrawFacilitySheet(SupportSuggestion suggestion, Club club)
        {
            DrawPaper();
            GUI.Label(SheetTitle, FacilityLabel(suggestion.Facility), _title);
            Fill(TitlePlate, Color.black, TitlePlate.height * 0.5f);
            GUI.Label(TitlePlate, "LV " + suggestion.CurrentLevel + " → " + suggestion.TargetLevel, _plate);

            DrawMoneyRow(FirstRowY, "費用", suggestion.Cost);
            if (club != null) DrawMoneyRow(SecondRowY, "資金", club.Coins);

            DrawBox("施設効果", FacilityEffect(suggestion, club));
            return DrawButtons("レベルアップ");
        }

        SupportChoice DrawFrontSheet(SupportSuggestion suggestion)
        {
            DrawPaper();
            GUI.Label(SheetTitle, "フロント人事", _title);
            Fill(TitlePlate, Color.black, TitlePlate.height * 0.5f);
            GUI.Label(TitlePlate, RoleLabel(suggestion.Role), _plate);

            DrawTextRow(FirstRowY, "候補", StaffText(suggestion.Candidate));
            DrawTextRow(SecondRowY, "現在", suggestion.Current != null ? StaffText(suggestion.Current) : "空き");
            DrawMoneyRow(ThirdRowY, "費用", suggestion.Cost);

            string detail = suggestion.Current != null
                ? "年俸 " + MoneyText.Format(ClubMath.StaffAnnual(suggestion.Current)) + " → " + MoneyText.Format(ClubMath.StaffAnnual(suggestion.Candidate))
                : "年俸 " + MoneyText.Format(ClubMath.StaffAnnual(suggestion.Candidate));
            detail += "\n" + (suggestion.Current != null
                ? suggestion.Current.Name + " は契約を解除します。"
                : "空いている枠と契約します。");
            DrawBox("契約内容", detail);
            return DrawButtons("契約する");
        }

        SupportChoice DrawButtons(string accept)
        {
            var choice = SupportChoice.None;
            if (ImageButton(CancelButton, "キャンセル")) choice = SupportChoice.Cancel;
            if (ImageButton(AcceptButton, accept)) choice = SupportChoice.Accept;
            return choice;
        }

        void DrawBox(string caption, string text)
        {
            Fill(EffectBox, RowBarColor, PanelRadius);
            Fill(EffectPlate, Color.black, EffectPlate.height * 0.5f);
            GUI.Label(EffectPlate, caption, _rowLabel);
            GUI.Label(EffectBox, text, _effect);
        }

        /// <summary>今のレベルと、上げたあとの効果を並べる。</summary>
        static string FacilityEffect(SupportSuggestion suggestion, Club club)
        {
            int now = suggestion.CurrentLevel;
            int next = suggestion.TargetLevel;
            switch (suggestion.Facility)
            {
                case FacilityKind.TrainingGround:
                    return "攻守の練習効果\n" + Bonus(now) + " → " + Bonus(next);
                case FacilityKind.TrainingFacility:
                    return "スタミナの練習効果\n" + Bonus(now) + " → " + Bonus(next);
                case FacilityKind.Stadium:
                    return "収容人数\n" + Capacity(now) + " → " + Capacity(next);
                case FacilityKind.Clubhouse:
                    return "採用できるスタッフ\n" + MarketRange(now) + " → " + MarketRange(next);
                default:
                    return "";
            }
        }

        static string Bonus(int level)
        {
            return "+" + ClubMath.TrainingBonusPercent(level).ToString("0.0", CultureInfo.InvariantCulture) + "%";
        }

        static string Capacity(int level)
        {
            return (5000 + level * 2000).ToString("N0", CultureInfo.InvariantCulture) + "人";
        }

        /// <summary>ClubFacilities.MarketLevelCap / MarketLevelFloor と同じ式。</summary>
        static string MarketRange(int clubhouseLevel)
        {
            int max = Mathf.Clamp(clubhouseLevel, 1, Facility.MaxLevel);
            int min = Mathf.Max(1, max - 5);
            return min == max ? "Lv" + max : "Lv" + min + "〜" + max;
        }

        static string StaffText(StaffMember staff)
        {
            if (staff == null) return "-";
            return (staff.Name ?? "") + "  Lv" + staff.Level;
        }

        /// <summary>奥の傾いた紙、手前の紙の順に、影つきで描く。</summary>
        static void DrawPaper()
        {
            var matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(BackSheetAngle, matrix.MultiplyPoint3x4(Sheet.center));
            DrawShadow(Sheet, 2f, 4f, 4f, 0f);
            Fill(Sheet, Color.white, 0f);
            GUI.matrix = matrix;

            DrawShadow(Sheet, 2f, 4f, 4f, 0f);
            Fill(Sheet, Color.white, 0f);
        }

        /// <summary>ぼかしの代わりに、広げた半透明の角丸を何段か重ねる。</summary>
        static void DrawShadow(Rect rect, float offsetY, float spread, float blur, float radius)
        {
            float step = blur / ShadowSteps;
            var color = new Color(0f, 0f, 0f, ShadowAlpha / ShadowSteps);
            for (int i = 0; i < ShadowSteps; i++)
            {
                float grow = spread - blur * 0.5f + step * (i + 1);
                var shadow = new Rect(rect.x - grow, rect.y + offsetY - grow, rect.width + grow * 2f, rect.height + grow * 2f);
                Fill(shadow, color, Mathf.Max(0f, radius + grow));
            }
        }

        void DrawRowFrame(float y, string caption)
        {
            Fill(new Rect(RowBarX, y, RowBarWidth, RowHeight), RowBarColor, RowHeight * 0.5f);
            var label = new Rect(RowX, y, RowLabelWidth, RowHeight);
            Fill(label, Color.black, RowHeight * 0.5f);
            GUI.Label(label, caption, _rowLabel);
        }

        void DrawMoneyRow(float y, string caption, BigInteger amount)
        {
            DrawRowFrame(y, caption);
            string text = MoneyText.Format(amount);
            if (text.EndsWith("円")) text = text.Substring(0, text.Length - 1);
            GUI.Label(new Rect(RowBarX, y, RowUnitX - RowBarX, RowHeight), text, _rowValue);
            GUI.Label(new Rect(RowUnitX, y, 30f, RowHeight), "円", _rowUnit);
        }

        void DrawTextRow(float y, string caption, string text)
        {
            DrawRowFrame(y, caption);
            GUI.Label(new Rect(RowBarX, y, RowTextRight - RowBarX, RowHeight), text, _rowText);
        }

        bool ImageButton(Rect rect, string label)
        {
            if (_button != null)
                GUI.DrawTexture(rect, _button, ScaleMode.StretchToFill, true);
            else
                Fill(rect, new Color(1f, 0.8f, 0.2f, 1f), rect.height * 0.5f);
            GUI.Label(rect, label, _buttonLabel);
            return GUI.Button(rect, GUIContent.none, _hit);
        }

        /// <summary>施設は秘書、フロント人事は GM が話す。いなければもう一方。</summary>
        static StaffMember FindSpeaker(Club club, SupportSuggestion suggestion)
        {
            var front = club != null ? club.Front : null;
            if (front == null) return null;
            if (suggestion.Kind == SupportKind.Front)
                return front.GeneralManager ?? front.Secretary;
            return front.Secretary ?? front.GeneralManager;
        }

        void EnsurePortrait(StaffMember speaker, Club club)
        {
            string key = PortraitKey(speaker, club);
            if (_portraitKey == key) return;
            Clear();
            _portraitKey = key;
            _portrait = CreateStand(speaker, club, AvatarLibrary.SheetHeight);
        }

        void EnsureCallout(StaffMember speaker, Club club)
        {
            string key = PortraitKey(speaker, club);
            if (_calloutKey == key) return;
            _calloutKey = key;
            if (_callout != null)
            {
                Object.Destroy(_callout);
                _callout = null;
            }

            _callout = CreateStand(speaker, club, CalloutPixelHeight);
        }

        static string PortraitKey(StaffMember speaker, Club club)
        {
            return (speaker != null ? speaker.Id : "") + ":" + (club != null ? club.EmblemIndex : -1);
        }

        Texture2D CreateStand(StaffMember speaker, Club club, int height)
        {
            string id = speaker != null ? speaker.Id : null;
            if (string.IsNullOrEmpty(id) || club == null) return null;
            if (!_libraryLoaded)
            {
                _libraryLoaded = true;
                _avatars.Load();
            }

            if (!_avatars.Ready) return null;
            return _avatars.CreateStand(id, AvatarLibrary.Kit(club.EmblemIndex), AvatarLibrary.NaturalHair(id), height);
        }

        void EnsureTextures()
        {
            if (_background == null)
                _background = Resources.Load<Texture2D>("MainScreen/bg_meeting_room");
            if (_button == null)
                _button = Resources.Load<Texture2D>("MainScreen/button_01");
            if (_pointer == null)
                _pointer = CreatePointer();
        }

        /// <summary>吹き出しの下向きの三角。20x40 を 4 倍の解像度で作り、端は 1px ぼかす。</summary>
        static Texture2D CreatePointer()
        {
            const int width = 80;
            const int height = 160;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                // テクスチャの y=0 は下。下端が先、上端が幅いっぱい。
                float half = width * 0.5f * (y + 0.5f) / height;
                for (int x = 0; x < width; x++)
                {
                    float distance = half - Mathf.Abs(x + 0.5f - width * 0.5f);
                    byte alpha = (byte)(Mathf.Clamp01(distance + 0.5f) * 255f);
                    pixels[y * width + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
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

        static string RoleLabel(FrontRole role)
        {
            switch (role)
            {
                case FrontRole.Secretary: return "秘書";
                case FrontRole.GeneralManager: return "GM";
                case FrontRole.Strengthening: return "強化部";
                case FrontRole.Sales: return "営業部";
                case FrontRole.PublicRelations: return "広報部";
                case FrontRole.Business: return "事業部";
                case FrontRole.Academy: return "アカデミー";
                default: return "フロント";
            }
        }

        static Rect Offset(Rect rect, Vector2 origin)
        {
            return new Rect(rect.x + origin.x, rect.y + origin.y, rect.width, rect.height);
        }

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

        void EnsureStyles(Font font)
        {
            if (_title != null && _font == font) return;
            _font = font;
            var label = new GUIStyle(GUI.skin.label)
            {
                font = font,
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Overflow,
                wordWrap = false,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
                normal = { textColor = Color.black }
            };
            _title = new GUIStyle(label) { fontSize = 48, fontStyle = FontStyle.Bold };
            _plate = new GUIStyle(label) { fontSize = 32, normal = { textColor = Color.white } };
            _speaker = new GUIStyle(label) { fontSize = 32 };
            _message = new GUIStyle(label) { fontSize = 32, alignment = TextAnchor.MiddleLeft, wordWrap = true };
            _bubble = new GUIStyle(label) { fontSize = 24 };
            _rowLabel = new GUIStyle(label) { fontSize = 20, normal = { textColor = Color.white } };
            _rowValue = new GUIStyle(label) { fontSize = 24, alignment = TextAnchor.MiddleRight };
            _rowText = new GUIStyle(label) { fontSize = 24, alignment = TextAnchor.MiddleRight };
            _rowUnit = new GUIStyle(label) { fontSize = 20, alignment = TextAnchor.MiddleLeft };
            _effect = new GUIStyle(label) { fontSize = 24, wordWrap = true };
            _buttonLabel = new GUIStyle(label) { fontSize = 20 };
            _hit = new GUIStyle();
        }
    }
}
