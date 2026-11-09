using System.Globalization;
using BigInteger = System.Numerics.BigInteger;
using SoccerClub.Core;
using SoccerClub.Data;
using SoccerClub.Economy;
using UnityEngine;

namespace SoccerClub.UI
{
    /// <summary>月末に出す、自クラブの収支報告。</summary>
    public sealed class MonthReportView
    {
        static readonly Rect Panel = new Rect(250f, 90f, 1420f, 800f);

        GUIStyle _title;
        GUIStyle _body;
        GUIStyle _amount;
        Font _font;

        public void Draw(MonthLedger report, Font font)
        {
            if (report == null) return;
            EnsureStyles(font);

            GUI.color = new Color(0.04f, 0.06f, 0.05f, 0.55f);
            GUI.DrawTexture(new Rect(0f, 0f, GameRunner.ReferenceWidth, GameRunner.ReferenceHeight), Texture2D.whiteTexture);
            GUI.color = new Color(0.10f, 0.16f, 0.14f, 0.98f);
            GUI.DrawTexture(Panel, Texture2D.whiteTexture);
            GUI.color = Color.white;

            float x = Panel.x + 64f;
            float y = Panel.y + 36f;
            GUI.Label(new Rect(x, y, 700f, 48f), "収支報告", _title);
            GUI.Label(new Rect(x + 760f, y, 520f, 48f), report.Year + "年 " + report.Month + "月", _title);

            float left = x;
            float right = x + 700f;
            float top = y + 88f;
            Section(left, top, "収入");
            float ly = top + 52f;
            ly = Line(left, ly, "スポンサー料", report.Sponsor, true);
            ly = Line(left, ly, "グッズ売上", report.Goods, true);
            ly = Line(left, ly, "入場料", report.Gate, true);
            ly = Line(left, ly, "放映権料", report.Broadcast, true);
            ly = Line(left, ly, "賞金", report.Prize, true);
            Line(left, ly + 8f, "収入合計", report.Income, true);

            Section(right, top, "支出");
            float ry = top + 52f;
            ry = Line(right, ry, "運営費", report.Operating, false);
            ry = Line(right, ry, "広告費", report.Advertising, false);
            ry = Line(right, ry, "育成費", report.Development, false);
            ry = Line(right, ry, "施設強化", report.Facilities, false);
            ry = Line(right, ry, "年俸", report.SquadWages, false);
            ry = Line(right, ry, "フロント年俸", report.FrontWages, false);
            ry = Line(right, ry, "強化費", report.Reinforcement, false);
            Line(right, ry + 8f, "支出合計", report.Expense, false);

            float foot = Panel.yMax - 150f;
            Line(left, foot, "収支", report.Balance, report.Balance >= 0);
            GUI.Label(new Rect(right, foot, 280f, 44f), "資金", _body);
            GUI.Label(new Rect(right + 280f, foot, 360f, 44f), MoneyText.Format(report.Coins), _amount);
        }

        void Section(float x, float y, string label)
        {
            GUI.Label(new Rect(x, y, 600f, 40f), label, _title);
        }

        float Line(float x, float y, string label, BigInteger amount, bool income)
        {
            GUI.Label(new Rect(x, y, 280f, 44f), label, _body);
            GUI.Label(new Rect(x + 280f, y, 360f, 44f), Signed(amount, income), _amount);
            return y + 48f;
        }

        static string Signed(BigInteger amount, bool income)
        {
            if (amount == 0) return "0円";
            string digits = Money(BigInteger.Abs(amount));
            if (income && amount > 0) return "+" + digits;
            return "-" + digits;
        }

        static string Money(BigInteger amount)
        {
            if (amount < 0) amount = -amount;
            return MoneyText.Format(amount);
        }

        void EnsureStyles(Font font)
        {
            if (_title != null && _font == font) return;
            _font = font;
            _title = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 32,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            _body = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 24,
                normal = { textColor = Color.white }
            };
            _amount = new GUIStyle(_body)
            {
                fontSize = 26,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
        }
    }
}
