using SoccerClub.Core;
using SoccerClub.Data;
using UnityEngine;

namespace SoccerClub.UI
{
    /// <summary>
    /// メイン画面で強制的に起きるイベント（友達・親友・恋人・夫婦）の見せ方。Figma の main の mini_event_sp。
    /// 歩いているキャラにはズームせず、2人の立ち絵を左右に大きく出して話させる。
    /// 1つ目は左の人の話しかけ、2つ目は右の人の返事（左のセリフは残す）、3つ目は結果を中央の枠に出す。
    /// </summary>
    public sealed class MiniEventSpotlight
    {
        // character（200:35 / 200:19）。1218 角の character3 を -178,42 と 879,42 に置く。
        // シートは 2048 角の中の 212,46 / 1622×1998 なので、1218/2048 倍した位置。
        const float Scale = 1218f / 2048f;
        static readonly Rect LeftCharacter = Sheet(-178f, 42f);
        static readonly Rect RightCharacter = Sheet(879f, 42f);

        // message_facility（200:2 / 200:10）。枠 800×200、名前 400×60、所属 200×60、本文 720×120。
        const float LeftBoxX = 31f;
        const float RightBoxX = 1088f;
        const float BoxY = 848f;
        const float BoxWidth = 800f;
        const float BoxHeight = 200f;
        const float PlateY = 818f;
        const float PlateHeight = 60f;
        const float NamePlateOffset = 279f;
        const float NamePlateWidth = 400f;
        const float NameTextOffset = 329f;
        const float NameTextWidth = 300f;
        const float DepartmentOffset = 121f;
        const float DepartmentWidth = 200f;
        const float DepartmentTextOffset = 150f;
        const float DepartmentTextWidth = 160f;
        const float TextOffsetX = 40f;
        const float TextOffsetY = 48f;
        const float TextWidth = 720f;
        const float TextHeight = 120f;
        // 3つ目（結果）は Figma に無いので、同じ大きさの枠を下の中央に出す。
        const float ResultBoxX = (GameRunner.ReferenceWidth - BoxWidth) * 0.5f;

        const float Radius = 10f;
        const float BoxAlpha = 0.9f;
        static readonly Color NamePlateColor = new Color(0xD1 / 255f, 0xD1 / 255f, 0xD6 / 255f, 1f);

        readonly AvatarLibrary _avatars = new AvatarLibrary();
        bool _loaded;
        Texture2D _left;
        Texture2D _right;
        string _leftKey;
        string _rightKey;

        Font _font;
        GUIStyle _message;
        GUIStyle _name;
        GUIStyle _department;

        /// <summary>
        /// beat は 0 から 2。left は話しかけた人、right は返事をした人。
        /// 座標はメイン画面の基準座標（1920×1080）。
        /// </summary>
        public void Draw(GameSession session, MiniEventDirector.SpotlightCue cue, Font font)
        {
            if (session == null || cue == null) return;
            EnsureStyles(font);
            var club = session.PlayerClub;
            Player left;
            Player right;
            StaffMember unused;
            session.TryClubPerson(cue.LeftId, out left, out unused);
            session.TryClubPerson(cue.RightId, out right, out unused);
            EnsurePortrait(ref _left, ref _leftKey, cue.LeftId, left, club);
            EnsurePortrait(ref _right, ref _rightKey, cue.RightId, right, club);

            var previous = GUI.color;
            GUI.color = Color.white;
            bool result = cue.Beat >= 2;
            if (_left != null)
                GUI.DrawTexture(TalkMotion.Apply(LeftCharacter, cue.Beat == 0 || result), AvatarLibrary.Face(_left, cue.Beat == 0 || result), ScaleMode.StretchToFill, true);
            if (_right != null)
                GUI.DrawTexture(TalkMotion.Apply(RightCharacter, cue.Beat == 1 || result), AvatarLibrary.Face(_right, cue.Beat == 1 || result), ScaleMode.StretchToFill, true);

            if (result)
            {
                Fill(new Rect(ResultBoxX, BoxY, BoxWidth, BoxHeight), new Color(1f, 1f, 1f, BoxAlpha), Radius);
                GUI.Label(new Rect(ResultBoxX + TextOffsetX, BoxY + TextOffsetY, TextWidth, TextHeight), cue.Result ?? "", _message);
            }
            else
            {
                DrawBox(LeftBoxX, cue.LeftName, DepartmentOf(left), cue.LeftLine);
                if (cue.Beat >= 1)
                    DrawBox(RightBoxX, cue.RightName, DepartmentOf(right), cue.RightLine);
            }

            GUI.color = previous;
        }

        /// <summary>イベントが終わったら立ち絵を捨てる。</summary>
        public void Clear()
        {
            Release(ref _left, ref _leftKey);
            Release(ref _right, ref _rightKey);
        }

        void DrawBox(float x, string name, string department, string line)
        {
            Fill(new Rect(x, BoxY, BoxWidth, BoxHeight), new Color(1f, 1f, 1f, BoxAlpha), Radius);
            GUI.Label(new Rect(x + TextOffsetX, BoxY + TextOffsetY, TextWidth, TextHeight), line ?? "", _message);
            Fill(new Rect(x + NamePlateOffset, PlateY, NamePlateWidth, PlateHeight), NamePlateColor, PlateHeight * 0.5f);
            GUI.Label(new Rect(x + NameTextOffset, PlateY, NameTextWidth, PlateHeight), name ?? "", _name);
            Fill(new Rect(x + DepartmentOffset, PlateY, DepartmentWidth, PlateHeight), Color.black, PlateHeight * 0.5f);
            GUI.Label(new Rect(x + DepartmentTextOffset, PlateY, DepartmentTextWidth, PlateHeight), department, _department);
        }

        /// <summary>所属の枠には、選手なら得意なポジション（CF、CB など）を出す。</summary>
        static string DepartmentOf(Player player)
        {
            return player != null ? player.BestPosition().ToString() : "選手";
        }

        void EnsurePortrait(ref Texture2D texture, ref string key, string id, Player player, Club club)
        {
            string want = (id ?? "") + ":" + (club != null ? club.EmblemIndex : -1);
            if (key == want) return;
            Release(ref texture, ref key);
            key = want;
            if (string.IsNullOrEmpty(id) || club == null) return;
            if (!_loaded)
            {
                _loaded = true;
                _avatars.Load();
            }

            if (!_avatars.Ready) return;
            // 選手の髪はメイン画面と同じく、ポジションのグループの色。
            var hair = player != null
                ? PositionUtility.GroupColor(PositionUtility.GroupOf(player.BestPosition()))
                : AvatarLibrary.NaturalHair(id);
            texture = _avatars.CreateStand(id, AvatarLibrary.Kit(club.EmblemIndex), hair, AvatarLibrary.SheetHeight, talkFace: true, gender: player != null ? player.Gender : (Gender?)null);
        }

        static void Release(ref Texture2D texture, ref string key)
        {
            key = null;
            if (texture == null) return;
            Object.Destroy(texture);
            texture = null;
        }

        static Rect Sheet(float frameX, float frameY)
        {
            return new Rect(frameX + 212f * Scale, frameY + 46f * Scale, 1622f * Scale, 1998f * Scale);
        }

        static void Fill(Rect rect, Color color, float radius)
        {
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, color, 0f, radius);
        }

        void EnsureStyles(Font font)
        {
            if (_message != null && _font == font) return;
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
            _message = new GUIStyle(label) { fontSize = 32, alignment = TextAnchor.MiddleLeft, wordWrap = true };
            _name = new GUIStyle(label) { fontSize = 32 };
            _department = new GUIStyle(label) { fontSize = 32, normal = { textColor = Color.white } };
        }
    }
}
