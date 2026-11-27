using System.Globalization;
using System.Numerics;
using SoccerClub.Core;
using SoccerClub.Data;
using SoccerClub.Economy;
using UnityEngine;
using Vector4 = UnityEngine.Vector4;

namespace SoccerClub.UI
{
    /// <summary>
    /// 月初の収支報告（版 1.65。Figma の month_report）。試合開催情報と同じ枠（ロッカールームの背景、ダイアログ、看板）。
    /// 左上に「YYYY年 M月の収支」、右に「毎月1日に前の月をお知らせ　リーグLV n」。
    /// 左の「収入」（スポンサー料・グッズ売上・入場料・放映権料・賞金・その他）と右の「支出」（7 項目）を並べ、
    /// それぞれ下に合計（黄色の縁）。いちばん下に「今月の収支」（黄色の縁）と「資金」（クリームの縁）。
    /// </summary>
    public sealed partial class ClubDialogView
    {
        static readonly Rect MrIncome = new Rect(192f, 326f, 740f, 500f);
        static readonly Rect MrExpense = new Rect(964f, 326f, 764f, 500f);
        const float MrRowsTop = 62f;
        const float MrRowHeight = 44f;
        const float MrRowStride = 48f;
        const float MrTotalHeight = 72f;
        const float MrFootY = 840f;
        const float MrFootHeight = 84f;

        public void DrawMonthReport(GameSession session, MonthLedger report, Font font)
        {
            EnsurePmStyles(font);
            DrawPmFrame("収支報告");
            if (report == null) return;

            // 左上の帯：「YYYY年 M月の収支」
            string month = report.Year.ToString(CultureInfo.InvariantCulture) + "年 " + report.Month.ToString(CultureInfo.InvariantCulture) + "月の収支";
            var bold = _ncCellLeft;
            int previousSize = bold.fontSize;
            bold.fontSize = 22;
            float w = bold.CalcSize(new GUIContent(month)).x;
            bold.fontSize = previousSize;
            var strip = new Rect(PmStrip.x, PmStrip.y, w + 44f, PmStrip.height);
            NcBox(strip, PmBand, NcRowLine, 2f, 24f);
            PmSmall(new Rect(strip.x + 22f, strip.y, w + 4f, strip.height), month, NcHeadBrown, 22);
            string info = "毎月1日に前の月をお知らせ";
            if (session != null) info += "　リーグLV " + session.LeagueLevel.ToString(CultureInfo.InvariantCulture);
            DrawPmStripInfo(info);

            DrawMrPanel(MrIncome, "収入", true, new[]
            {
                new MrLine("スポンサー料", report.Sponsor),
                new MrLine("グッズ売上", report.Goods),
                new MrLine("入場料", report.Gate),
                new MrLine("放映権料", report.Broadcast),
                new MrLine("賞金", report.Prize),
                new MrLine("その他", report.Other)
            }, "収入合計", report.Income);

            DrawMrPanel(MrExpense, "支出", false, new[]
            {
                new MrLine("運営費", report.Operating),
                new MrLine("広告費", report.Advertising),
                new MrLine("育成費", report.Development),
                new MrLine("施設強化", report.Facilities),
                new MrLine("年俸", report.SquadWages),
                new MrLine("フロント年俸", report.FrontWages),
                new MrLine("強化費", report.Reinforcement)
            }, "支出合計", report.Expense);

            var balance = new Rect(MrIncome.x, MrFootY, MrIncome.width, MrFootHeight);
            DrawMrTotal(balance, "今月の収支", report.Balance, NcYellow, true);
            var coins = new Rect(MrExpense.x, MrFootY, MrExpense.width, MrFootHeight);
            NcRound(new Rect(coins.x, coins.y + 4f, coins.width, coins.height), NcShade(0.15f), 16f);
            NcRound(coins, Color.white, 16f);
            NcOutline(coins, NcRowLine, 4f, 16f);
            PmSmall(new Rect(coins.x + 24f, coins.y, 200f, coins.height), "資金", NcDarkBrown, 30);
            var big = _pmMoney;
            int previous = big.fontSize;
            big.fontSize = 44;
            NcLabel(new Rect(coins.x + 200f, coins.y, coins.width - 224f, coins.height), MoneyText.Format(report.Coins), big, NcDarkBrown);
            big.fontSize = previous;
        }

        struct MrLine
        {
            public readonly string Label;
            public readonly BigInteger Amount;

            public MrLine(string label, BigInteger amount)
            {
                Label = label;
                Amount = amount;
            }
        }

        void DrawMrPanel(Rect panel, string title, bool income, MrLine[] lines, string totalLabel, BigInteger total)
        {
            DrawPmPanel(panel, title);
            float x = panel.x + 32f;
            float width = panel.width - 64f;
            float y = panel.y + MrRowsTop;
            for (int i = 0; i < lines.Length; i++)
            {
                var row = new Rect(x, y, width, MrRowHeight);
                NcRound(row, NcListRow, 12f);
                NcOutline(row, NcRowLine, 2f, 12f);
                PmSmall(new Rect(row.x + 20f, row.y, 260f, row.height), lines[i].Label, NcText, 22);
                var value = income ? lines[i].Amount : -lines[i].Amount;
                var tint = value > 0 ? PmPlus : value < 0 ? PmMinus : NcText;
                var money = _pmMoney;
                int previous = money.fontSize;
                money.fontSize = 26;
                NcLabel(new Rect(row.x + 280f, row.y, row.width - 300f, row.height), SignedMoney(value, true), money, tint);
                money.fontSize = previous;
                y += MrRowStride;
            }

            var box = new Rect(x, panel.yMax - 16f - MrTotalHeight, width, MrTotalHeight);
            DrawMrTotal(box, totalLabel, income ? total : -total, NcYellow, false);
        }

        void DrawMrTotal(Rect box, string label, BigInteger value, Color line, bool shadow)
        {
            if (shadow) NcRound(new Rect(box.x, box.y + 4f, box.width, box.height), NcShade(0.15f), 16f);
            NcRound(box, Color.white, 16f);
            NcOutline(box, line, 4f, 16f);
            PmSmall(new Rect(box.x + 24f, box.y, 240f, box.height), label, NcDarkBrown, 28);
            var big = _pmMoney;
            int previous = big.fontSize;
            big.fontSize = box.height >= 80f ? 44 : 38;
            var tint = value > 0 ? PmPlus : value < 0 ? PmMinus : NcText;
            NcLabel(new Rect(box.x + 240f, box.y, box.width - 264f, box.height), SignedMoney(value, true), big, tint);
            big.fontSize = previous;
        }
    }
}
