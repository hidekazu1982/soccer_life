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
            if (story.Kind == ClubStoryKind.Party)
                return DrawPartyInvite(story, session);

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

            string label = story.Budget ? "この予算で進める" : (story.FinalStep ? "確認" : "次へ");
            return GUI.Button(new Rect(x, Panel.yMax - 110f, width, 64f), label, _button);
        }

        // ---- パーティの招待（仮UI。Figma のデザインができたら差し替える） ----

        static readonly Rect PartyPanel = new Rect(160f, 60f, 1600f, 960f);
        GUIStyle _row;
        GUIStyle _rowOn;
        GUIStyle _small;

        bool DrawPartyInvite(ClubStory story, GameSession session)
        {
            var club = session != null ? session.PlayerClub : null;
            var party = session != null ? session.OpenParty : null;
            GUI.color = new Color(0.04f, 0.06f, 0.05f, 0.55f);
            GUI.DrawTexture(new Rect(0f, 0f, GameRunner.ReferenceWidth, GameRunner.ReferenceHeight), Texture2D.whiteTexture);
            GUI.color = new Color(0.10f, 0.16f, 0.14f, 0.98f);
            GUI.DrawTexture(PartyPanel, Texture2D.whiteTexture);
            GUI.color = Color.white;
            if (club == null || party == null)
                return GUI.Button(new Rect(PartyPanel.x + 48f, PartyPanel.yMax - 96f, 400f, 64f), "閉じる", _button);

            float x = PartyPanel.x + 48f;
            float width = PartyPanel.width - 96f;
            float y = PartyPanel.y + 28f;
            GUI.Label(new Rect(x, y, width, 52f), story.Title ?? party.Title, _title);
            y += 60f;
            GUI.Label(new Rect(x, y, width, 70f), story.Body ?? "", _small);
            y += 76f;

            // 選手の一覧。2列。印: ♥ 恋人・夫婦が招待中、★ 友達・親友が招待中。
            var squad = club.Squad;
            const float rowHeight = 40f;
            float colWidth = (width - 24f) * 0.5f;
            int perColumn = Mathf.Max(1, Mathf.CeilToInt(squad.Count / 2f));
            for (int i = 0; i < squad.Count; i++)
            {
                var p = squad[i];
                if (p == null) continue;
                int col = i / perColumn;
                int row = i % perColumn;
                var rect = new Rect(x + col * (colWidth + 24f), y + row * rowHeight, colWidth, rowHeight - 4f);
                bool on = story.Invited.Contains(p.Id);
                string mark = Marks(session, story, p);
                string text = (on ? "● " : "○ ") + p.Name + "  " + p.BestPosition() + "  " + p.Age + "歳  調子" + p.Condition
                    + "  出場" + p.Appearances + (mark.Length > 0 ? "  " + mark : "");
                if (GUI.Button(rect, text, on ? _rowOn : _row))
                    session.TogglePartyGuest(story, p.Id);
            }

            float bottom = PartyPanel.yMax - 170f;
            var cost = session.PartyCost(story);
            bool afford = session.CanAffordParty(story);
            string effect = "効果：調子+" + party.Condition + "、招待した人同士の親密度+" + party.Affinity
                + "（友達+" + party.FriendBonus + "、恋人・夫婦+" + party.PartnerBonus + "）"
                + (party.Stat >= 0 ? "、" + PartyCatalog.StatLabel(party.Stat) + "が練習1日分" : "")
                + (party.YoungMultiplier > 1d ? "、21歳以下は" + party.YoungMultiplier.ToString("0.#", CultureInfo.InvariantCulture) + "倍" : "")
                + "、移籍しにくさ" + party.LeavePercent + "%（今季中）";
            GUI.Label(new Rect(x, bottom, width, 34f), effect, _small);
            GUI.Label(new Rect(x, bottom + 36f, width, 34f),
                "招待 " + story.Invited.Count + " / " + party.Slots + "人　　費用 " + Money(cost) + "　　資金 " + Money(club.Coins)
                + (afford ? "" : "　　資金が足りません"), _small);

            float by = PartyPanel.yMax - 88f;
            if (GUI.Button(new Rect(x, by, 300f, 64f), "おまかせ", _button))
                session.RecommendPartyGuests(story);
            if (GUI.Button(new Rect(x + 320f, by, 300f, 64f), "開かない", _button))
            {
                story.Invited.Clear();
                return true;
            }

            bool ready = afford && story.Invited.Count > 0;
            GUI.enabled = ready;
            bool go = GUI.Button(new Rect(PartyPanel.xMax - 48f - 480f, by, 480f, 64f), "この選手を招待する", _button);
            GUI.enabled = true;
            return go && ready;
        }

        static string Marks(GameSession session, ClubStory story, Player p)
        {
            if (session.Relations == null) return "";
            var list = session.Relations.For(p.Id);
            string mark = "";
            for (int i = 0; list != null && i < list.Count; i++)
            {
                var rel = list[i];
                string other = rel.Other(p.Id);
                if (other == null || !story.Invited.Contains(other)) continue;
                if (rel.Stage == RelationStage.Lover || rel.Stage == RelationStage.Married)
                {
                    if (mark.IndexOf('♥') < 0) mark += "♥";
                }
                else if (rel.Stage == RelationStage.Friend || rel.Stage == RelationStage.BestFriend)
                {
                    if (mark.IndexOf('★') < 0) mark += "★";
                }
            }

            return mark;
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
            _small = new GUIStyle(_body) { fontSize = 22 };
            _row = new GUIStyle(GUI.skin.button)
            {
                font = font,
                fontSize = 20,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(14, 8, 4, 4)
            };
            _rowOn = new GUIStyle(_row);
            _rowOn.normal.textColor = new Color(1f, 0.85f, 0.35f);
            _rowOn.hover.textColor = new Color(1f, 0.85f, 0.35f);
            _rowOn.fontStyle = FontStyle.Bold;
        }
    }
}
