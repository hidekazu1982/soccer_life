using System;
using System.Globalization;
using BigInteger = System.Numerics.BigInteger;
using SoccerClub.Core;
using SoccerClub.Data;
using UnityEngine;

namespace SoccerClub.UI
{
    /// <summary>ウィンターブレイク、年始、移籍、補強のイベント。</summary>
    public sealed class ClubEventView
    {
        static readonly Rect Panel = new Rect(460f, 180f, 1000f, 720f);

        GUIStyle _title;
        GUIStyle _body;
        GUIStyle _button;
        Font _font;

        public bool Draw(ClubStory story, GameSession session, Font font)
        {
            if (story == null) return false;
            EnsureStyles(font);

            GUI.color = new Color(0.04f, 0.06f, 0.05f, 0.55f);
            GUI.DrawTexture(new Rect(0f, 0f, GameRunner.ReferenceWidth, GameRunner.ReferenceHeight), Texture2D.whiteTexture);
            GUI.color = new Color(0.10f, 0.16f, 0.14f, 0.98f);
            GUI.DrawTexture(Panel, Texture2D.whiteTexture);
            GUI.color = Color.white;

            float x = Panel.x + 64f;
            float width = Panel.width - 128f;
            float y = Panel.y + 40f;
            GUI.Label(new Rect(x, y, width, 52f), story.Title ?? "", _title);
            y += 72f;
            float bodyHeight = story.Budget ? 280f : 460f;
            GUI.Label(new Rect(x, y, width, bodyHeight), story.Body ?? "", _body);

            if (story.Budget && session != null && session.PlayerClub != null)
            {
                var club = session.PlayerClub;
                int percent = story.BudgetPercent;
                if (percent < 0) percent = 0;
                if (percent > 100) percent = 100;
                var amount = club.Coins * percent / 100;
                float moneyY = Panel.y + 360f;
                GUI.Label(new Rect(x, moneyY, width, 40f), "資産  " + Money(club.Coins), _body);
                GUI.Label(new Rect(x, moneyY + 48f, width, 40f), "割合  " + percent + "%    強化費へ  " + Money(amount), _body);
                GUI.Label(new Rect(x, moneyY + 96f, width, 40f), "いまの強化費  " + Money(club.Reinforcement), _body);
                if (GUI.Button(new Rect(x, moneyY + 148f, 180f, 56f), "-10%", _button))
                    story.BudgetPercent = Math.Max(0, percent - 10);
                if (GUI.Button(new Rect(x + 200f, moneyY + 148f, 180f, 56f), "+10%", _button))
                    story.BudgetPercent = Math.Min(100, percent + 10);
            }

            if (story.Kind == ClubStoryKind.Stadium)
            {
                float buttonY = Panel.yMax - 110f;
                float half = (width - 16f) * 0.5f;
                if (GUI.Button(new Rect(x, buttonY, half, 64f), "レベルアップする", _button))
                {
                    if (session != null && session.OwnerApprovesFacilityUpgrade(FacilityKind.Stadium))
                        return true;
                    story.Pages[0] = "資金が足りないため、スタジアムを拡張できません。";
                    story.Body = story.Pages[0];
                    return false;
                }

                if (GUI.Button(new Rect(x + half + 16f, buttonY, half, 64f), "見送る", _button))
                    return true;
                return false;
            }

            string label = story.Budget ? "この予算で進める" : (story.FinalStep ? "確認" : "次へ");
            return GUI.Button(new Rect(x, Panel.yMax - 110f, width, 64f), label, _button);
        }

        static string Money(BigInteger amount)
        {
            return MoneyText.Format(amount);
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
            _button = new GUIStyle(GUI.skin.button)
            {
                font = font,
                fontSize = 28,
                fontStyle = FontStyle.Bold
            };
        }
    }
}
