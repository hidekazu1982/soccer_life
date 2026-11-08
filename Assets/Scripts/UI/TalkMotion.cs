using UnityEngine;

namespace SoccerClub.UI
{
    /// <summary>
    /// 話している（メッセージウィンドウを出している）キャラの、ふわふわした伸び縮み。
    /// 足元の中央を固定し、横に伸びると縦に縮む（面積がおおよそ変わらない）。
    /// </summary>
    public static class TalkMotion
    {
        /// <summary>1 往復の秒数。</summary>
        public const float Period = 1.1f;

        /// <summary>伸び縮みの幅。0.025 で ±2.5%。</summary>
        public const float Amount = 0.025f;

        /// <summary>話しているときは伸び縮みさせた矩形、そうでなければ元の矩形を返す。</summary>
        public static Rect Apply(Rect rect, bool talking)
        {
            if (!talking) return rect;
            float wave = Mathf.Sin(UnityEngine.Time.unscaledTime * (Mathf.PI * 2f / Period));
            float sx = 1f + Amount * wave;
            float sy = 1f - Amount * wave;
            float width = rect.width * sx;
            float height = rect.height * sy;
            return new Rect(rect.center.x - width * 0.5f, rect.yMax - height, width, height);
        }
    }
}
