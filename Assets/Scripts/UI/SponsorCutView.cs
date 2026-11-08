using SoccerClub.Core;
using SoccerClub.Data;
using UnityEngine;

namespace SoccerClub.UI
{
    /// <summary>成績不振でスポンサー契約が打ち切られたときの通知。</summary>
    public sealed class SponsorCutView
    {
        static readonly Rect Panel = new Rect(460f, 220f, 1000f, 640f);

        GUIStyle _title;
        GUIStyle _body;
        Font _font;

        public void Draw(SponsorCutNotice notice, Font font)
        {
            if (notice == null) return;
            EnsureStyles(font);

            GUI.color = new Color(0.04f, 0.06f, 0.05f, 0.55f);
            GUI.DrawTexture(new Rect(0f, 0f, GameRunner.ReferenceWidth, GameRunner.ReferenceHeight), Texture2D.whiteTexture);
            GUI.color = new Color(0.10f, 0.16f, 0.14f, 0.98f);
            GUI.DrawTexture(Panel, Texture2D.whiteTexture);
            GUI.color = Color.white;

            float x = Panel.x + 64f;
            float width = Panel.width - 128f;
            float y = Panel.y + 40f;
            GUI.Label(new Rect(x, y, width, 48f), "スポンサー打ち切り", _title);
            y += 64f;
            GUI.Label(new Rect(x, y, width, 72f), "解消条件を満たしたスポンサーが契約を終えました。", _body);
            y += 80f;
            var contracts = notice.Contracts;
            for (int i = 0; i < contracts.Count; i++)
            {
                var contract = contracts[i];
                if (contract == null) continue;
                GUI.Label(
                    new Rect(x, y, width, 40f),
                    contract.Name + "    " + ClubMath.SponsorCutLabel(contract.CutRank, contract.CutLosses),
                    _body);
                y += 44f;
            }
        }

        void EnsureStyles(Font font)
        {
            if (_title != null && _font == font) return;
            _font = font;
            _title = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 36,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            _body = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 26,
                wordWrap = true,
                normal = { textColor = Color.white }
            };
        }
    }
}
