using UnityEngine;

namespace SoccerClub.UI
{
    /// <summary>
    /// 試合前（スタメン紹介）と試合後（スタッツ・順位・リザルト）の背景。ロッカールームの絵（bg_005）。
    /// 絵の上に画面ごとの色を重ねて、文字や表を読みやすくする。絵が読めないときは重ねる色をそのまま不透明で塗る。
    /// </summary>
    public static class MatchBackdrop
    {
        const string Path = "MainScreen/bg_locker_room";

        static Texture2D _texture;
        static bool _loaded;

        /// <summary>画面全体（セーフエリアの外側の余白も含む）に背景を描き、overlay を重ねる。</summary>
        public static void Draw(Color overlay)
        {
            var screen = FullScreen();
            var previous = GUI.color;
            GUI.color = Color.white;
            var texture = Texture;
            if (texture != null)
            {
                GUI.DrawTexture(screen, texture, ScaleMode.ScaleAndCrop, false);
            }
            else
            {
                overlay.a = 1f;
            }

            if (overlay.a > 0f)
                GUI.DrawTexture(screen, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, overlay, 0f, 0f);
            GUI.color = previous;
        }

        static Texture2D Texture
        {
            get
            {
                if (!_loaded)
                {
                    _loaded = true;
                    _texture = Resources.Load<Texture2D>(Path);
                    if (_texture == null) Debug.LogWarning("[MatchBackdrop] Resources/" + Path + " が見つかりません。");
                }

                return _texture;
            }
        }

        static Rect FullScreen()
        {
            float ix = SoccerClub.Core.GameRunner.HorizontalEdgeInset;
            float iy = SoccerClub.Core.GameRunner.VerticalEdgeInset;
            return new Rect(-ix, -iy, SoccerClub.Core.GameRunner.ReferenceWidth + ix * 2f, SoccerClub.Core.GameRunner.ReferenceHeight + iy * 2f);
        }
    }
}
