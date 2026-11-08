using SoccerClub.Core;
using SoccerClub.Data;
using UnityEngine;

namespace SoccerClub.UI
{
    /// <summary>
    /// Figma の main2（1920×1080、16:9）。背景は同じ枠いっぱいに敷く。
    /// その上にスタジアム・練習場・クラブハウスを重ね、施設レベルに応じて 01〜03 の見た目へ切り替える。
    /// Lv 1〜19 が 1、Lv 20〜50 が 2、Lv 51 以上が 3。
    /// </summary>
    public sealed class MainScreenView
    {
        public const int Grade2MinLevel = 20;
        public const int Grade3MinLevel = 51;

        static readonly Rect BackgroundRect = new Rect(0f, 0f, 1920f, 1080f);
        static readonly Rect StadiumRect = new Rect(328f, 252f, 499f, 320f);
        static readonly Rect TrainingRect = new Rect(1222f, 300f, 427f, 240f);
        public static readonly Rect ClubhouseRect = new Rect(1203f, 617f, 446f, 300f);

        Texture2D _background;
        readonly Texture2D[] _stadium = new Texture2D[3];
        readonly Texture2D[] _training = new Texture2D[3];
        readonly Texture2D[] _clubhouse = new Texture2D[3];
        GUIStyle _hitStyle;
        GUIStyle _captionStyle;
        Font _font;

        public static int GradeForLevel(int level)
        {
            if (level >= Grade3MinLevel) return 3;
            if (level >= Grade2MinLevel) return 2;
            return 1;
        }

        public void Load()
        {
            _background = Resources.Load<Texture2D>("MainScreen/main_bg");
            for (int grade = 1; grade <= 3; grade++)
            {
                var suffix = grade.ToString("00");
                _stadium[grade - 1] = Resources.Load<Texture2D>("MainScreen/stadium_" + suffix);
                _training[grade - 1] = Resources.Load<Texture2D>("MainScreen/training_" + suffix);
                _clubhouse[grade - 1] = Resources.Load<Texture2D>("MainScreen/clubhouse_" + suffix);
            }
        }

        /// <summary>
        /// 背景と施設を描く。キャラクターはこの後、建物のクリック判定より前に重ねる。
        /// </summary>
        public void DrawScenery(Club club)
        {
            if (_background == null) return;
            var previous = GUI.color;
            GUI.BeginGroup(new Rect(0f, 0f, GameRunner.ReferenceWidth, GameRunner.ReferenceHeight));
            GUI.DrawTexture(BackgroundRect, _background, ScaleMode.StretchToFill, true);
            GUI.EndGroup();
            DrawBuildingImage(StadiumRect, _stadium, LevelOf(club, FacilityKind.Stadium));
            DrawBuildingImage(TrainingRect, _training, LevelOf(club, FacilityKind.TrainingGround));
            GUI.color = previous;
        }

        /// <summary>area_player_01 の歩行者より手前に出す。</summary>
        public void DrawClubhouse(Club club)
        {
            var previous = GUI.color;
            GUI.color = Color.white;
            DrawBuildingImage(ClubhouseRect, _clubhouse, LevelOf(club, FacilityKind.Clubhouse));
            GUI.color = previous;
        }

        /// <summary>建物がクリックされたとき true。</summary>
        public bool DrawBuildingHits(Club club, out FacilityKind clicked)
        {
            clicked = FacilityKind.Stadium;
            if (_background == null) return false;
            EnsureStyles();
            var mouse = MouseInReference();
            bool stadium = DrawBuildingHit(StadiumRect, LevelOf(club, FacilityKind.Stadium), "スタジアム", mouse);
            bool training = DrawBuildingHit(TrainingRect, LevelOf(club, FacilityKind.TrainingGround), "練習場", mouse);
            bool clubhouse = DrawBuildingHit(ClubhouseRect, LevelOf(club, FacilityKind.Clubhouse), "クラブハウス", mouse);
            if (stadium) clicked = FacilityKind.Stadium;
            else if (training) clicked = FacilityKind.TrainingGround;
            else if (clubhouse) clicked = FacilityKind.Clubhouse;
            return stadium || training || clubhouse;
        }

        static int LevelOf(Club club, FacilityKind kind)
        {
            if (club == null) return 1;
            return club.Facilities.Get(kind).Level;
        }

        static void DrawBuildingImage(Rect rect, Texture2D[] grades, int level)
        {
            var texture = grades[GradeForLevel(level) - 1];
            if (texture != null)
                GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, true);
        }

        bool DrawBuildingHit(Rect rect, int level, string caption, Vector2 mouse)
        {
            int grade = GradeForLevel(level);
            bool hover = rect.Contains(mouse);
            if (hover)
            {
                var plate = new Rect(rect.center.x - 120f, rect.y + 12f, 240f, 40f);
                GUI.color = new Color(1f, 0.98f, 0.94f, 0.92f);
                GUI.DrawTexture(plate, Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(plate, caption + "  グレード" + grade, _captionStyle);
            }

            return GUI.Button(rect, GUIContent.none, _hitStyle);
        }

        void EnsureStyles()
        {
            if (_hitStyle != null) return;
            _font = JapaneseFont.Resolve();
            var empty = new RectOffset(0, 0, 0, 0);
            _hitStyle = new GUIStyle
            {
                margin = empty,
                padding = empty,
                border = empty
            };
            _captionStyle = new GUIStyle(GUI.skin.label)
            {
                font = _font,
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.16f, 0.2f, 0.18f, 1f) }
            };
        }

        static Vector2 MouseInReference()
        {
            return GameRunner.MouseInReference();
        }

    }
}
