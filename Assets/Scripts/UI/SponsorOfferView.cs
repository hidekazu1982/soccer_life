using System.Globalization;
using BigInteger = System.Numerics.BigInteger;
using SoccerClub.Core;
using SoccerClub.Data;
using UnityEngine;

namespace SoccerClub.UI
{
    public enum SponsorChoice
    {
        None,
        Accept,
        Decline
    }

    /// <summary>
    /// 平日のスポンサー獲得。Figma の sponsor フレーム。
    /// 会議室を背景に営業担当が左に立ち、右の契約書で「契約する」か「キャンセル」を選ぶ。
    /// </summary>
    public sealed class SponsorOfferView
    {
        // character3 は 1218 角（2048 キャンバスの 0.5947 倍）を -97,0 に置く。シート 811x999 はその中の 212,46 / 1622x1998。
        static readonly Rect CharacterRect = new Rect(29f, 27f, 965f, 1188f);

        // message
        static readonly Rect MessageBox = new Rect(112f, 848f, 800f, 200f);
        static readonly Rect MessageText = new Rect(152f, 896f, 720f, 120f);
        // 名前の帯（灰色）の左端に、部署名の帯（黒）を重ねる。
        static readonly Rect SpeakerPlate = new Rect(391f, 818f, 400f, 60f);
        static readonly Rect SpeakerText = new Rect(441f, 818f, 300f, 60f);
        static readonly Rect DepartmentPlate = new Rect(233f, 818f, 200f, 60f);
        static readonly Rect DepartmentText = new Rect(262f, 818f, 160f, 60f);
        static readonly Color SpeakerPlateColor = new Color(0xD1 / 255f, 0xD1 / 255f, 0xD6 / 255f, 1f);
        const string SalesDepartment = "営業部";

        // sponsor（契約書）。紙は 2 枚重ね。奥の 1 枚は同じ中心で -5 度（反時計回り）傾ける。
        static readonly Rect Sheet = new Rect(1059.63f, 210f, 800f, 692f);
        const float BackSheetAngle = -5f;
        static readonly Rect SheetTitle = new Rect(1059.63f, 249f, 800f, 68f);
        static readonly Rect SponsorPlate = new Rect(1259.63f, 353f, 400f, 60f);
        const float RowX = 1224.63f;
        const float RowLabelWidth = 200f;
        const float RowBarX = 1334.63f;
        const float RowBarWidth = 360f;
        const float RowHeight = 32f;
        const float RowUnitX = 1653.63f;
        const float UpfrontY = 445f;
        const float MonthlyY = 485f;
        const float PeriodY = 525f;
        const float PeriodRight = 1605.63f;
        static readonly Rect TermsBox = new Rect(1224.63f, 605f, 470f, 160f);
        static readonly Rect TermsPlate = new Rect(1359.63f, 589f, 200f, 32f);
        static readonly Rect CancelButton = new Rect(1288.63f, 818f, 155f, 60f);
        static readonly Rect ContractButton = new Rect(1475.63f, 818f, 155f, 60f);
        // 契約中の件数。「契約する」の上、中央 1554.13。
        static readonly Rect ActiveCount = new Rect(1475.63f, 781f, 157f, 30f);

        const float PanelRadius = 10f;
        const float MessageAlpha = 0.9f;
        static readonly Color RowBarColor = new Color(0f, 0f, 0f, 0.1f);
        // drop shadow: 0 2px 4px 4px rgba(0,0,0,0.25)
        const float ShadowOffsetY = 2f;
        const float ShadowSpread = 4f;
        const float ShadowBlur = 4f;
        const float ShadowAlpha = 0.25f;
        const int ShadowSteps = 4;

        readonly AvatarLibrary _avatars = new AvatarLibrary();
        bool _libraryLoaded;
        Texture2D _background;
        Texture2D _button;
        Texture2D _portrait;
        string _portraitKey;

        GUIStyle _title;
        GUIStyle _plate;
        GUIStyle _speaker;
        GUIStyle _message;
        GUIStyle _rowLabel;
        GUIStyle _rowValue;
        GUIStyle _rowUnit;
        GUIStyle _terms;
        GUIStyle _buttonLabel;
        GUIStyle _count;
        GUIStyle _hit;
        Font _font;

        public SponsorChoice Draw(SponsorOffer offer, GameSession session, Font font)
        {
            if (offer == null) return SponsorChoice.None;
            EnsureStyles(font);
            EnsureTextures();
            var club = session != null ? session.PlayerClub : null;
            var speaker = FindSpeaker(club, offer.StaffName);
            EnsurePortrait(speaker, club);

            GUI.color = Color.white;
            // UI は 1920x1080 を画面内に収めて中央寄せするので、16:9 と比率が違う端末では左右（または上下）が空く。
            // 背景だけは画面全体まで広げ、比率を保ったまま覆う（はみ出した分は上下を少し切る）。
            var screen = FullScreenRect();
            if (_background != null)
                GUI.DrawTexture(screen, _background, ScaleMode.ScaleAndCrop, false);
            else
                Fill(screen, new Color(0.12f, 0.14f, 0.16f, 1f), 0f);
            // 立ち絵は元の解像度（999px）で作り、CharacterRect（1188px）へ引き伸ばす。
            // この画面では担当者がずっと話しているので、常にふわふわさせる。
            if (_portrait != null)
                GUI.DrawTexture(TalkMotion.Apply(CharacterRect, true), _portrait, ScaleMode.StretchToFill, true);

            string speakerName = speaker != null && !string.IsNullOrEmpty(speaker.Name)
                ? speaker.Name
                : string.IsNullOrEmpty(offer.StaffName) ? "担当者" : offer.StaffName;
            DrawMessage(speakerName);
            return DrawSheet(offer);
        }

        /// <summary>画面を閉じたあとに立ち絵を捨てる。</summary>
        public void Clear()
        {
            _portraitKey = null;
            if (_portrait != null)
            {
                Object.Destroy(_portrait);
                _portrait = null;
            }
        }

        void DrawMessage(string speakerName)
        {
            Fill(MessageBox, new Color(1f, 1f, 1f, MessageAlpha), PanelRadius);
            GUI.Label(MessageText, "新しいスポンサーを見つけてきました！\n契約しますか？", _message);
            Fill(SpeakerPlate, SpeakerPlateColor, SpeakerPlate.height * 0.5f);
            GUI.Label(SpeakerText, speakerName, _speaker);
            Fill(DepartmentPlate, Color.black, DepartmentPlate.height * 0.5f);
            GUI.Label(DepartmentText, SalesDepartment, _plate);
        }

        SponsorChoice DrawSheet(SponsorOffer offer)
        {
            DrawPaper();
            GUI.Label(SheetTitle, "契約書", _title);
            Fill(SponsorPlate, Color.black, SponsorPlate.height * 0.5f);
            GUI.Label(SponsorPlate, offer.SponsorName ?? "", _plate);

            DrawMoneyRow(UpfrontY, "契約金", offer.Upfront);
            DrawMoneyRow(MonthlyY, "月額スポンサー料", offer.Monthly);
            DrawRowFrame(PeriodY, "契約期間");
            GUI.Label(new Rect(RowBarX, PeriodY, PeriodRight - RowBarX, RowHeight), PeriodText(offer.Months), _rowValue);

            Fill(TermsBox, RowBarColor, PanelRadius);
            Fill(TermsPlate, Color.black, TermsPlate.height * 0.5f);
            GUI.Label(TermsPlate, "解約条件", _rowLabel);
            GUI.Label(TermsBox, TermsText(offer.CutRank, offer.CutLosses), _terms);

            int cap = offer.ContractCap > 0 ? offer.ContractCap : ClubMath.SponsorContractCap;
            GUI.Label(ActiveCount, offer.ActiveContracts + "/" + cap, _count);

            var choice = SponsorChoice.None;
            if (ImageButton(CancelButton, "キャンセル")) choice = SponsorChoice.Decline;
            if (ImageButton(ContractButton, "契約する")) choice = SponsorChoice.Accept;
            return choice;
        }

        /// <summary>奥の傾いた紙、手前の紙の順に、影つきで描く。</summary>
        static void DrawPaper()
        {
            var matrix = GUI.matrix;
            // RotateAroundPivot は拡大後の画面座標で回すので、基準座標の中心を GUI.matrix で変換して渡す。
            GUIUtility.RotateAroundPivot(BackSheetAngle, matrix.MultiplyPoint3x4(Sheet.center));
            DrawShadow(Sheet);
            Fill(Sheet, Color.white, 0f);
            GUI.matrix = matrix;

            DrawShadow(Sheet);
            Fill(Sheet, Color.white, 0f);
        }

        /// <summary>ぼかしの代わりに、広げた半透明の角丸を何段か重ねる。</summary>
        static void DrawShadow(Rect rect)
        {
            float step = ShadowBlur / ShadowSteps;
            var color = new Color(0f, 0f, 0f, ShadowAlpha / ShadowSteps);
            for (int i = 0; i < ShadowSteps; i++)
            {
                float grow = ShadowSpread - ShadowBlur * 0.5f + step * (i + 1);
                var shadow = new Rect(rect.x - grow, rect.y + ShadowOffsetY - grow, rect.width + grow * 2f, rect.height + grow * 2f);
                Fill(shadow, color, grow);
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
            // Figma は数字と「円」を別の大きさで置く。「円」は RowUnitX から。
            if (text.EndsWith("円")) text = text.Substring(0, text.Length - 1);
            GUI.Label(new Rect(RowBarX, y, RowUnitX - RowBarX, RowHeight), text, _rowValue);
            GUI.Label(new Rect(RowUnitX, y, 30f, RowHeight), "円", _rowUnit);
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

        /// <summary>12 か月単位なら「Y年」、半端があれば「Y年Mカ月」、1 年未満は「Mカ月」。</summary>
        static string PeriodText(int months)
        {
            if (months <= 0) return "-";
            int years = months / 12;
            int rest = months % 12;
            if (years <= 0) return rest.ToString(CultureInfo.InvariantCulture) + "カ月";
            string text = years.ToString(CultureInfo.InvariantCulture) + "年";
            if (rest > 0) text += rest.ToString(CultureInfo.InvariantCulture) + "カ月";
            return text;
        }

        static string TermsText(int cutRank, int cutLosses)
        {
            if (cutRank > 0) return "リーグ戦で " + cutRank + "位以下 になると\n契約は解消されます。";
            if (cutLosses > 0) return cutLosses + "連敗 すると\n契約は解消されます。";
            return "解約条件はありません。";
        }

        /// <summary>話し手は営業部のうち名前が一致する人。いなければレベルが最も高い人。</summary>
        static StaffMember FindSpeaker(Club club, string name)
        {
            var sales = club != null && club.Front != null ? club.Front.Sales : null;
            if (sales == null) return null;
            StaffMember best = null;
            for (int i = 0; i < sales.Length; i++)
            {
                var member = sales[i];
                if (member == null) continue;
                if (!string.IsNullOrEmpty(name) && member.Name == name) return member;
                if (best == null || member.Level > best.Level) best = member;
            }

            return best;
        }

        void EnsurePortrait(StaffMember speaker, Club club)
        {
            string id = speaker != null ? speaker.Id : null;
            int emblem = club != null ? club.EmblemIndex : -1;
            string key = (id ?? "") + ":" + emblem;
            if (_portraitKey == key) return;
            Clear();
            _portraitKey = key;
            if (string.IsNullOrEmpty(id) || club == null) return;
            if (!_libraryLoaded)
            {
                _libraryLoaded = true;
                _avatars.Load();
            }

            if (!_avatars.Ready) return;
            _portrait = _avatars.CreateStand(
                id,
                AvatarLibrary.Kit(club.EmblemIndex),
                AvatarLibrary.NaturalHair(id),
                AvatarLibrary.SheetHeight,
                AvatarLibrary.OutfitOf(speaker));
        }

        void EnsureTextures()
        {
            if (_background == null)
                _background = Resources.Load<Texture2D>("MainScreen/bg_meeting_room");
            if (_button == null)
                _button = Resources.Load<Texture2D>("MainScreen/button_01");
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
            _rowLabel = new GUIStyle(label) { fontSize = 20, normal = { textColor = Color.white } };
            _rowValue = new GUIStyle(label) { fontSize = 24, alignment = TextAnchor.MiddleRight };
            _rowUnit = new GUIStyle(label) { fontSize = 20, alignment = TextAnchor.MiddleLeft };
            _terms = new GUIStyle(label) { fontSize = 24, wordWrap = true };
            _buttonLabel = new GUIStyle(label) { fontSize = 24 };
            _count = new GUIStyle(label) { fontSize = 24 };
            _hit = new GUIStyle();
        }
    }
}
