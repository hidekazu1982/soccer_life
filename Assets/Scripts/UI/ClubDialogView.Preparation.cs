using System.Collections.Generic;
using System.Globalization;
using SoccerClub.Competition;
using SoccerClub.Core;
using SoccerClub.Data;
using SoccerClub.Match;
using UnityEngine;
using Vector4 = UnityEngine.Vector4;

namespace SoccerClub.UI
{
    /// <summary>
    /// 試合前のスタメン（版 1.65。Figma の match_preparation_redesign）。試合後の画面と同じ枠に看板「スタメン」。
    /// 左：スタメンの 11 人を選手カード（match_player_card。パーティのカードの名前の下に総合力）で、
    /// 上から FW・MF・DF・GK の 4 段に並べる（段の中はフォーメーションの左右の順）。
    /// 右上：「今日の試合」（節と日付、自クラブを黄色の縁で上、VS、相手を下。エンブレム・HOME/AWAY・名前・スタメン総合力）。
    /// 右下：「ベンチ」（同じカードを小さくして 4 列）。
    /// </summary>
    public sealed partial class ClubDialogView
    {
        // 選手カードの基準（144×184。上 112 が絵、下 72 が名前と総合力）。
        const float PcWidth = 144f;
        const float PcHeight = 184f;
        const float PcAvatar = 112f;
        // スタメンは 0.9 倍で、4 段を 176 間隔。
        const float PcXiScale = 0.9f;
        const float PcXiTop = 232f;
        const float PcXiRowStep = 176f;
        const float PcXiColumnStep = 142f;
        const float PcXiCenterX = 721f;
        // 右の枠。
        static readonly Rect PcMatchPanel = new Rect(1270f, 232f, 450f, 254f);
        const float PcBenchTop = 500f;
        const float PcBenchScale = 0.72f;
        const float PcBenchGap = 8f;
        const int PcBenchColumns = 4;

        readonly List<int>[] _pcRows = { new List<int>(), new List<int>(), new List<int>(), new List<int>() };
        GUIStyle _pcName;

        public void DrawMatchPreparation(GameSession session, Fixture fixture, Club club, TeamSheet sheet, TeamSheet opponentSheet, GroundCrowd crowd, Font font)
        {
            EnsurePmStyles(font);
            if (_pcName == null) _pcName = new GUIStyle(_ncBold) { alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Overflow, wordWrap = false };
            DrawPmFrame("スタメン");
            if (club == null || sheet == null || sheet.Formation == null) return;

            DrawPcEleven(sheet, crowd);
            DrawPcMatchPanel(session, fixture, club, sheet, opponentSheet);
            DrawPcBench(sheet, crowd);
        }

        void DrawPcEleven(TeamSheet sheet, GroundCrowd crowd)
        {
            for (int r = 0; r < _pcRows.Length; r++) _pcRows[r].Clear();
            var slots = sheet.Formation.Slots;
            int count = sheet.StartingXi != null ? sheet.StartingXi.Length : 0;
            for (int i = 0; i < count && i < slots.Length; i++)
            {
                var lineup = sheet.StartingXi[i];
                var position = lineup != null ? lineup.Slot : slots[i];
                int row;
                switch (PositionUtility.GroupOf(position))
                {
                    case PositionGroup.FW: row = 0; break;
                    case PositionGroup.MF: row = 1; break;
                    case PositionGroup.DF: row = 2; break;
                    default: row = 3; break;
                }

                _pcRows[row].Add(i);
            }

            var type = sheet.Formation.Type;
            float w = PcWidth * PcXiScale;
            for (int r = 0; r < _pcRows.Length; r++)
            {
                var list = _pcRows[r];
                // 段の中は、今までのフォーメーション図の左右の順。
                list.Sort((a, b) => FormationLayout.Card(type, a).x.CompareTo(FormationLayout.Card(type, b).x));
                float left = PcXiCenterX - (list.Count - 1) * PcXiColumnStep * 0.5f - w * 0.5f;
                for (int c = 0; c < list.Count; c++)
                {
                    int index = list[c];
                    var lineup = sheet.StartingXi[index];
                    var position = lineup != null ? lineup.Slot : slots[index];
                    var rect = new Rect(left + c * PcXiColumnStep, PcXiTop + r * PcXiRowStep, w, PcHeight * PcXiScale);
                    DrawPcCard(rect, position, lineup != null ? lineup.Player : null, crowd);
                }
            }
        }

        /// <summary>選手カード。rect の大きさに合わせて中身を縮める（基準 144×184）。</summary>
        void DrawPcCard(Rect card, Position position, Player player, GroundCrowd crowd)
        {
            float k = card.width / PcWidth;
            float radius = 16f * k;
            NcRound(new Rect(card.x, card.y + 3f * k, card.width, card.height), NcShade(0.3f), radius);
            NcRound(card, NcListRow, radius);
            var avatar = new Rect(card.x, card.y, card.width, PcAvatar * k);
            GUI.DrawTexture(avatar, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f,
                new Color(0.86f, 0.91f, 0.94f, 1f), Vector4.zero, new Vector4(radius, radius, 0f, 0f));

            var texture = player != null && crowd != null ? crowd.WalkSide(player.Id) : null;
            if (texture != null)
            {
                // 頭と肩が絵の枠に収まるように、全身の絵を枠の高さの 1.8 倍で上詰めに描き、下を切る。
                float h = PcAvatar * k * 1.8f;
                float aw = h * (texture.width / (float)texture.height);
                GUI.BeginGroup(avatar);
                GUI.DrawTexture(new Rect((avatar.width - aw) * 0.5f, 2f * k, aw, h), texture, ScaleMode.StretchToFill, true);
                GUI.EndGroup();
            }

            NcOutline(card, NcRowLine, 2f, radius);

            var badge = new Rect(card.x + 6f * k, card.y + 6f * k, 44f * k, 26f * k);
            var group = PositionUtility.GroupOf(position);
            NcRound(badge, PositionUtility.GroupColor(group), 8f * k);
            NsSmallLabel(badge, position.ToString(), Mathf.RoundToInt(17f * k), group == PositionGroup.MF ? NcText : Color.white);

            if (player == null) return;
            var nameRect = new Rect(card.x + 6f * k, card.y + PcAvatar * k + 4f * k, card.width - 12f * k, 34f * k);
            var color = _pcName.normal.textColor;
            _pcName.normal.textColor = NcText;
            FitNcLabel(nameRect, player.Name ?? "", _pcName, Mathf.RoundToInt(20f * k));
            _pcName.normal.textColor = NcHeadBrown;
            FitNcLabel(new Rect(nameRect.x, nameRect.yMax, nameRect.width, 26f * k),
                "総合力 " + player.DisplayOverall.ToString("N0", CultureInfo.InvariantCulture), _pcName, Mathf.RoundToInt(17f * k));
            _pcName.normal.textColor = color;
        }

        void DrawPcMatchPanel(GameSession session, Fixture fixture, Club club, TeamSheet sheet, TeamSheet opponentSheet)
        {
            var p = PcMatchPanel;
            string title = "今日の試合";
            if (fixture != null)
                title += "　" + CompetitionLabel(session, fixture, club) + "　" + fixture.Date.ToString("MM/dd", CultureInfo.InvariantCulture);
            DrawPmPanel(p, title);
            bool home = fixture == null || fixture.Home == club;
            var opponent = fixture == null ? null : home ? fixture.Away : fixture.Home;

            var mine = new Rect(p.x + 16f, p.y + 66f, p.width - 32f, 68f);
            DrawPcTeamRow(mine, club, home, XiOverall(sheet), true);
            NsSmallLabel(new Rect(p.x, mine.yMax, p.width, 34f), "VS", 22, NcHeadBrown);
            var theirs = new Rect(mine.x, mine.yMax + 34f, mine.width, mine.height);
            DrawPcTeamRow(theirs, opponent, !home, XiOverall(opponentSheet), false);
        }

        void DrawPcTeamRow(Rect row, Club team, bool home, int overall, bool mine)
        {
            NcRound(row, Color.white, 14f);
            NcOutline(row, mine ? NcYellow : NcRowLine, mine ? 4f : 2f, 14f);
            DrawNsEmblem(new Rect(row.x + 10f, row.y + 10f, 48f, 48f), team);
            DrawPmSideTag(new Rect(row.x + 68f, row.y + 8f, 70f, 22f), home);
            PmSmall(new Rect(row.x + 68f, row.y + 32f, 190f, 32f), team != null ? team.Name ?? "" : "", NcDarkBrown, 22);
            var right = _ncSmallRight;
            var color = right.normal.textColor;
            right.normal.textColor = NcHeadBrown;
            FitNcLabel(new Rect(row.xMax - 150f, row.y + 4f, 136f, 22f), "スタメン総合力", right, 14);
            right.normal.textColor = color;
            var money = _pmMoney;
            int previous = money.fontSize;
            money.fontSize = 32;
            NcLabel(new Rect(row.xMax - 170f, row.y + 24f, 156f, 42f), overall.ToString("N0", CultureInfo.InvariantCulture), money, mine ? PmPlus : NcDarkBrown);
            money.fontSize = previous;
        }

        static int XiOverall(TeamSheet sheet)
        {
            if (sheet == null || sheet.StartingXi == null) return 0;
            double sum = 0d;
            for (int i = 0; i < sheet.StartingXi.Length; i++)
            {
                var slot = sheet.StartingXi[i];
                if (slot != null && slot.Player != null) sum += slot.Player.Overall;
            }

            return Player.ToDisplay(sum);
        }

        void DrawPcBench(TeamSheet sheet, GroundCrowd crowd)
        {
            var bench = sheet.Bench ?? new Player[0];
            float w = PcWidth * PcBenchScale;
            float h = PcHeight * PcBenchScale;
            int rows = Mathf.Max(1, (bench.Length + PcBenchColumns - 1) / PcBenchColumns);
            var panel = new Rect(PcMatchPanel.x, PcBenchTop, PcMatchPanel.width, 52f + 12f + rows * h + (rows - 1) * PcBenchGap + 14f);
            DrawPmPanel(panel, "ベンチ");
            if (bench.Length == 0)
            {
                PmSmall(new Rect(panel.x + 24f, panel.y + 64f, panel.width - 48f, 40f), "ベンチの選手はいません", NcHeadBrown, 22);
                return;
            }

            float gridWidth = PcBenchColumns * w + (PcBenchColumns - 1) * PcBenchGap;
            float left = panel.x + (panel.width - gridWidth) * 0.5f;
            for (int i = 0; i < bench.Length; i++)
            {
                var player = bench[i];
                if (player == null) continue;
                int c = i % PcBenchColumns;
                int r = i / PcBenchColumns;
                var rect = new Rect(left + c * (w + PcBenchGap), panel.y + 64f + r * (h + PcBenchGap), w, h);
                DrawPcCard(rect, player.BestPosition(), player, crowd);
            }
        }
    }
}
