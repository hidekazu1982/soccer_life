using UnityEngine;

namespace SoccerClub.UI
{
    /// <summary>
    /// 端末内蔵の CJK フォントは Android 上でネイティブクラッシュすることがある。
    /// 同梱フォントを使い、起動時に OS フォントを開かない。
    /// </summary>
    public static class JapaneseFont
    {
        public static Font Resolve()
        {
            var bundled = Resources.Load<Font>("Fonts/MPLUSRounded1c-Regular");
            if (bundled != null)
                return bundled;

#if !UNITY_ANDROID || UNITY_EDITOR
            var names = new[] { "Yu Gothic UI", "Yu Gothic", "Meiryo UI", "Meiryo", "MS Gothic" };
            for (int i = 0; i < names.Length; i++)
            {
                try
                {
                    var font = Font.CreateDynamicFontFromOSFont(names[i], 22);
                    if (font != null && font.fontNames != null && font.fontNames.Length > 0)
                        return font;
                }
                catch (System.Exception)
                {
                }
            }
#endif
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }
}
