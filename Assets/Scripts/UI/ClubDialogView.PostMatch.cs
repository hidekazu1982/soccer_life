using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using SoccerClub.Competition;
using SoccerClub.Core;
using SoccerClub.Data;
using SoccerClub.Match;
using UnityEngine;
using Vector4 = UnityEngine.Vector4;

namespace SoccerClub.UI
{
    /// <summary>
    /// 試合後の 3 画面（Figma の post_match_rating / post_match_ranking / post_match_info。仕様書 版 1.50・1.53）。
    /// ロッカールームの背景の上に、スタジアムダイアログと同じ枠（NcDialog）と看板を置く。
    /// 試合結果：スコアの帯と、出場選手の評価を 2 列 × 7 行。
    /// 順位表：今日の試合の帯と、スタジアムの順位タブと同じ順位表・日程（今日の試合を強調）。
    /// 試合開催情報：今日の試合の帯と、左に観客、右に収支。
    /// </summary>
    public sealed partial class ClubDialogView
    {
        // ---- 試合後の画面の配置（基準 1920×1080）
        const float PmLeft = 192f;
        const float PmWidth = 1536f;
        const float PmTop = 250f;
        static readonly Rect PmStrip = new Rect(192f, 262f, 1536f, 48f);

        // 試合結果
        static readonly Rect PmScoreBand = new Rect(192f, 250f, 1536f, 150f);
        const float PmListTitleY = 416f;
        const float PmRowsY = 466f;
        // 交代は最大 5 人なので、先発 11 人＋5 人＝16 人を 2 列 × 8 行で出す。
        const float PmRowHeight = 50f;
        const float PmRowStride = 54f;
        const float PmColumnWidth = 752f;
        const float PmColumnStep = 784f;
        const int PmRowsPerColumn = 8;

        // 試合開催情報
        static readonly Rect PmSpectators = new Rect(192f, 330f, 740f, 570f);
        static readonly Rect PmBalance = new Rect(964f, 330f, 764f, 570f);

        static readonly Color PmBand = Hex(0xF6EBD0);
        static readonly Color PmWin = Hex(0x9CC8FF);
        static readonly Color PmDraw = Hex(0xFFE27A);
        static readonly Color PmLoss = Hex(0xFFA8A8);
        static readonly Color PmPlus = Hex(0x2F5E9E);
        static readonly Color PmMinus = Hex(0xB03A68);
        static readonly Color PmNote = Hex(0x8C8073);

        GUIStyle _pmScore;
        GUIStyle _pmBig;
        GUIStyle _pmRating;
        GUIStyle _pmMoney;
        GUIStyle _pmHit;
        Font _pmFont;

        /// <summary>日程で強調する日（試合後の順位表では今日の試合）。null ならスタジアムと同じく次の試合。</summary>
        DateTime? _nsHighlightDate;

        sealed class PmRatingLine
        {
            public PlayerMatchRating Rating;
            public Position Position;
            public bool Substitute;
            public int Order;
        }

        readonly List<PmRatingLine> _pmLines = new List<PmRatingLine>();
        string _pmLinesKey;

        /// <summary>試合後の画面に入るとき。順位表と日程のスクロールを、自クラブと今日の試合に合わせ直す。</summary>
        public void ResetPostMatch()
        {
            _nsScrollPlaced = false;
            _nsDataKey = null;
            _pmLinesKey = null;
        }

        // =========================================================== 試合結果

        public void DrawPostMatchResult(GameSession session, Fixture fixture, Club club, Font font)
        {
            EnsurePmStyles(font);
            DrawPmFrame("試合結果");
            if (fixture == null || fixture.Result == null) return;
            DrawPmScoreBand(session, fixture, club);

            CollectPmLines(fixture, club);
            for (int column = 0; column < 2; column++)
                DrawPmListTitle(PmLeft + column * PmColumnStep);

            if (_pmLines.Count == 0)
            {
                NcLabel(new Rect(PmLeft, PmRowsY, PmWidth, 56f), "出場した選手はいません。", _ncCell, NcText);
                return;
            }

            PlayerMatchRating mom = null;
            for (int i = 0; i < _pmLines.Count; i++)
                if (mom == null || _pmLines[i].Rating.Score > mom.Score) mom = _pmLines[i].Rating;

            int shown = Mathf.Min(_pmLines.Count, PmRowsPerColumn * 2);
            for (int i = 0; i < shown; i++)
            {
                int column = i / PmRowsPerColumn;
                int row = i % PmRowsPerColumn;
                var rect = new Rect(PmLeft + column * PmColumnStep, PmRowsY + row * PmRowStride, PmColumnWidth, PmRowHeight);
                DrawPmRatingRow(rect, _pmLines[i], _pmLines[i].Rating == mom);
            }
        }

        /// <summary>スコアの帯：左右にエンブレム・HOME/AWAY・クラブ名、中央にスコア、大会名と勝敗の札。</summary>
        void DrawPmScoreBand(GameSession session, Fixture fixture, Club club)
        {
            var result = fixture.Result;
            NcBox(PmScoreBand, PmBand, NcRowLine, 2f, 20f);

            // ホーム（左）
            DrawNsEmblem(new Rect(216f, 265f, 120f, 120f), fixture.Home);
            DrawPmSideTag(new Rect(356f, 284f, 104f, 36f), true);
            FitNcLabel(new Rect(356f, 326f, 340f, 44f), fixture.Home != null ? fixture.Home.Name ?? "" : "", _ncCellLeft, 30);

            // アウェイ（右）
            DrawNsEmblem(new Rect(1584f, 265f, 120f, 120f), fixture.Away);
            DrawPmSideTag(new Rect(1460f, 284f, 104f, 36f), false);
            FitNcLabel(new Rect(1224f, 326f, 340f, 44f), fixture.Away != null ? fixture.Away.Name ?? "" : "", _ncCellRight, 30);

            // スコア
            string score = result.HomeGoals.ToString(CultureInfo.InvariantCulture) + " - " + result.AwayGoals.ToString(CultureInfo.InvariantCulture);
            NcLabel(new Rect(712f, 244f, 496f, 112f), score, _pmScore, NcDarkBrown);

            // 大会名（PK 戦はその結果も）と勝敗の札
            string competition = CompetitionLabel(session, fixture, club);
            string pk = result.PenaltyText(fixture.Home);
            if (!string.IsNullOrEmpty(pk)) competition += "　" + pk;
            var compStyle = _ncCellRight;
            var compColor = compStyle.normal.textColor;
            compStyle.normal.textColor = NcHeadBrown;
            FitNcLabel(new Rect(600f, 354f, 360f, 40f), competition, compStyle, 22);
            compStyle.normal.textColor = compColor;
            DrawPmOutcome(new Rect(976f, 356f, 132f, 36f), result, club, 22);
        }

        void DrawPmSideTag(Rect rect, bool home)
        {
            NcRound(rect, home ? NcHeadBrown : NcMaleFill, rect.height * 0.5f);
            NsSmallLabel(rect, home ? "HOME" : "AWAY", 20, home ? NcCream : NcMaleText);
        }

        /// <summary>勝敗の札。勝ち WIN（青）、分け DRAW（黄）、負け DEFEAT（赤）。PK 戦で決めたら頭に PK。</summary>
        void DrawPmOutcome(Rect rect, MatchResult result, Club club, int size)
        {
            int sign = club != null ? result.OutcomeFor(club) : 0;
            string text = sign > 0 ? "WIN" : sign < 0 ? "DEFEAT" : "DRAW";
            if (result.DecidedByPenalty && sign != 0) text = "PK " + text;
            var fill = sign > 0 ? PmWin : sign < 0 ? PmLoss : PmDraw;
            NcBox(rect, fill, NcDarkBrown, 3f, rect.height * 0.5f);
            int previous = _ncPosText.fontSize;
            _ncPosText.fontSize = size;
            var content = new GUIContent(text);
            while (_ncPosText.fontSize > 12 && _ncPosText.CalcSize(content).x > rect.width - 12f)
                _ncPosText.fontSize -= 2;
            NcLabel(rect, text, _ncPosText, NcDarkBrown);
            _ncPosText.fontSize = previous;
        }

        void DrawPmListTitle(float x)
        {
            var bar = new Rect(x, PmListTitleY, PmColumnWidth, 44f);
            NcRound(bar, NcHeadBrown, 10f);
            FitNcLabel(new Rect(x + 12f, bar.y, 64f, bar.height), "POS", _ncHeadText, 22);
            var left = _ncCellLeft;
            var color = left.normal.textColor;
            left.normal.textColor = NcCream;
            FitNcLabel(new Rect(x + 88f, bar.y, 200f, bar.height), "選手", left, 22);
            left.normal.textColor = color;
            var right = _ncCellRight;
            color = right.normal.textColor;
            right.normal.textColor = NcCream;
            FitNcLabel(new Rect(x + 520f, bar.y, 80f, bar.height), "得点", right, 22);
            FitNcLabel(new Rect(x + 620f, bar.y, 112f, bar.height), "評価", right, 22);
            right.normal.textColor = color;
        }

        /// <summary>評価の 1 行（Figma の rating_row）。MOM の行は白地に黄色の枠。</summary>
        void DrawPmRatingRow(Rect row, PmRatingLine line, bool mom)
        {
            NcRound(row, mom ? Color.white : NcListRow, 12f);
            if (!mom) NcOutline(row, NcRowLine, 2f, 12f);

            var badge = new Rect(row.x + 12f, row.y + 7f, 64f, 36f);
            Color32 group = PositionUtility.GroupColor(PositionUtility.GroupOf(line.Position));
            NcRound(badge, group, 10f);
            NsSmallLabel(badge, line.Position.ToString(), 20, Color.white);

            var player = line.Rating.Player;
            FitNcLabel(new Rect(row.x + 88f, row.y, 300f, row.height), player != null ? player.Name ?? "" : "", _ncCellLeft, 24);

            string tag = mom ? "MOM" : line.Substitute ? "途中出場" : null;
            if (tag != null)
            {
                var pill = new Rect(row.x + 400f, row.y + 10f, 96f, 30f);
                NcRound(pill, mom ? NcYellow : NcRowLine, 15f);
                NsSmallLabel(pill, tag, 18, NcDarkBrown);
            }

            if (line.Rating.Goals > 0)
            {
                var goals = _ncCellRight;
                var color = goals.normal.textColor;
                goals.normal.textColor = NcText;
                FitNcLabel(new Rect(row.x + 520f, row.y, 80f, row.height), line.Rating.Goals.ToString(CultureInfo.InvariantCulture) + "点", goals, 22);
                goals.normal.textColor = color;
            }

            double score = line.Rating.Score;
            var tint = score >= 7d ? PmPlus : score < 6d ? PmMinus : NcText;
            NcLabel(new Rect(row.x + 620f, row.y, 112f, row.height), score.ToString("0.0", CultureInfo.InvariantCulture), _pmRating, tint);

            if (mom) NcOutline(row, NcYellow, 4f, 12f);
        }

        /// <summary>
        /// 評価の並び：先発（GK・DF・MF・FW の順、同じ群は先発の並び順）、そのあとに途中出場（入った順）。
        /// ポジションは先発の枠、途中出場は入った枠のもの（わからなければ得意ポジション）。
        /// </summary>
        void CollectPmLines(Fixture fixture, Club club)
        {
            string key = (fixture.Id ?? "") + "|" + (club != null ? club.Id : "");
            if (_pmLinesKey == key) return;
            _pmLinesKey = key;
            _pmLines.Clear();
            if (club == null) return;

            var result = fixture.Result;
            bool home = club == fixture.Home;
            var xi = home ? result.HomeXi : result.AwayXi;
            var ratings = MatchRatings.ForClub(fixture, club);
            var byPlayer = new Dictionary<Player, PlayerMatchRating>();
            for (int i = 0; i < ratings.Count; i++)
                if (ratings[i].Player != null) byPlayer[ratings[i].Player] = ratings[i];

            var used = new HashSet<Player>();
            if (xi != null)
            {
                for (int i = 0; i < xi.Length; i++)
                {
                    var slot = xi[i];
                    if (slot == null || slot.Player == null || used.Contains(slot.Player)) continue;
                    PlayerMatchRating rating;
                    if (!byPlayer.TryGetValue(slot.Player, out rating)) continue;
                    used.Add(slot.Player);
                    _pmLines.Add(new PmRatingLine
                    {
                        Rating = rating,
                        Position = slot.Slot,
                        Substitute = false,
                        Order = (int)PositionUtility.GroupOf(slot.Slot) * 100 + i
                    });
                }
            }

            int order = 10000;
            if (result.Substitutions != null)
            {
                for (int i = 0; i < result.Substitutions.Count; i++)
                {
                    var sub = result.Substitutions[i];
                    if (sub == null || sub.Home != home || sub.In == null || used.Contains(sub.In)) continue;
                    PlayerMatchRating rating;
                    if (!byPlayer.TryGetValue(sub.In, out rating)) continue;
                    used.Add(sub.In);
                    var position = sub.In.PreferredPosition;
                    if (xi != null && sub.SlotIndex >= 0 && sub.SlotIndex < xi.Length && xi[sub.SlotIndex] != null)
                        position = xi[sub.SlotIndex].Slot;
                    _pmLines.Add(new PmRatingLine { Rating = rating, Position = position, Substitute = true, Order = order++ });
                }
            }

            // 先発にも交代にも無い出場者（古いデータなど）は最後に。
            for (int i = 0; i < ratings.Count; i++)
            {
                var player = ratings[i].Player;
                if (player == null || used.Contains(player)) continue;
                used.Add(player);
                _pmLines.Add(new PmRatingLine { Rating = ratings[i], Position = player.PreferredPosition, Substitute = true, Order = order++ });
            }

            _pmLines.Sort((a, b) => a.Order.CompareTo(b.Order));
        }

        // =========================================================== 順位表

        public void DrawPostMatchRanking(GameSession session, Fixture fixture, Club club, Font font)
        {
            EnsurePmStyles(font);
            DrawPmFrame("順位表");
            if (club == null) return;
            DrawPmTodayStrip(session, fixture, club);
            string info = "リーグLV " + session.LeagueLevel.ToString(CultureInfo.InvariantCulture);
            DrawPmStripInfo(info);

            var page = _stadiumPage;
            _stadiumPage = StadiumPage.Table;
            _nsHighlightDate = fixture != null ? fixture.Date.Date : (DateTime?)null;
            CollectNsData(session, club);
            DrawNsTable(club);
            DrawNsSchedule();
            _nsHighlightDate = null;
            _stadiumPage = page;
        }

        // =========================================================== 試合開催情報

        public void DrawPostMatchInfo(GameSession session, Fixture fixture, Club club, int coreDelta, int lightDelta, Font font)
        {
            EnsurePmStyles(font);
            DrawPmFrame("試合開催情報");
            if (fixture == null || club == null) return;
            bool home = fixture.Home == club;
            DrawPmTodayStrip(session, fixture, club);
            DrawPmStripInfo(CompetitionLabel(session, fixture, club) + "　" + fixture.Date.ToString("MM/dd", CultureInfo.InvariantCulture)
                            + "　" + (home ? "ホームゲーム" : "アウェイゲーム"));
            DrawPmSpectators(fixture, club, home, coreDelta, lightDelta);
            DrawPmBalance(fixture, home);
        }

        void DrawPmPanel(Rect panel, string title)
        {
            NcBox(panel, PmBand, NcRowLine, 2f, 20f);
            var head = new Rect(panel.x, panel.y, panel.width, 52f);
            NcRound(head, NcHeadBrown, new Vector4(20f, 20f, 0f, 0f));
            var style = _ncCellLeft;
            var color = style.normal.textColor;
            style.normal.textColor = NcCream;
            FitNcLabel(new Rect(panel.x + 24f, head.y, panel.width - 48f, head.height), title, style, 26);
            style.normal.textColor = color;
        }

        /// <summary>左の「観客」：入場者数、チケット代、収容率のバー、サポーターの増減。アウェイは入場者の欄を出さない。</summary>
        void DrawPmSpectators(Fixture fixture, Club club, bool home, int coreDelta, int lightDelta)
        {
            var p = PmSpectators;
            DrawPmPanel(p, "観客");
            float x = p.x + 32f;
            float inner = p.width - 64f;
            if (home && fixture.StadiumCapacity > 0)
            {
                PmSmall(new Rect(x, p.y + 64f, 300f, 32f), "入場者数", NcHeadBrown, 22);
                string attendance = fixture.Attendance.ToString("N0", CultureInfo.InvariantCulture);
                var size = _pmBig.CalcSize(new GUIContent(attendance));
                NcLabel(new Rect(x, p.y + 90f, size.x + 4f, 96f), attendance, _pmBig, NcDarkBrown);
                PmSmall(new Rect(x + size.x + 12f, p.y + 128f, 60f, 44f), "人", NcDarkBrown, 30);

                PmSmall(new Rect(p.x + 468f, p.y + 64f, 240f, 32f), "チケット代", NcHeadBrown, 22);
                PmSmall(new Rect(p.x + 468f, p.y + 100f, 240f, 56f), club.TicketPrice.ToString("N0", CultureInfo.InvariantCulture) + "円", NcDarkBrown, 40);

                float rate = Mathf.Clamp01(fixture.Attendance / (float)fixture.StadiumCapacity);
                var bar = new Rect(x, p.y + 206f, inner, 28f);
                NcBox(bar, NcListRow, NcRowLine, 2f, 14f);
                if (rate > 0f)
                {
                    var fill = new Rect(bar.x, bar.y, Mathf.Max(28f, bar.width * rate), bar.height);
                    NcBox(fill, NcYellow, NcDarkBrown, 2f, 14f);
                }

                string capacity = "収容 " + fixture.StadiumCapacity.ToString("N0", CultureInfo.InvariantCulture) + "人　収容率 "
                                  + (rate * 100f).ToString("0", CultureInfo.InvariantCulture) + "%";
                var right = _ncSmallRight;
                var color = right.normal.textColor;
                right.normal.textColor = NcText;
                FitNcLabel(new Rect(x, p.y + 240f, inner, 32f), capacity, right, 22);
                right.normal.textColor = color;
            }
            else
            {
                PmSmall(new Rect(x, p.y + 90f, inner, 40f), "アウェイの試合なので、入場者と入場料はありません。", NcHeadBrown, 22);
            }

            PmSmall(new Rect(x, p.y + 290f, 300f, 32f), "サポーター", NcHeadBrown, 22);
            DrawPmSupporter(new Rect(x, p.y + 330f, inner, 96f), "コア", club.CoreSupporters + coreDelta, coreDelta);
            DrawPmSupporter(new Rect(x, p.y + 442f, inner, 96f), "ライト", club.LightSupporters + lightDelta, lightDelta);
        }

        void DrawPmSupporter(Rect row, string label, int count, int delta)
        {
            NcRound(row, NcListRow, 16f);
            NcOutline(row, NcRowLine, 2f, 16f);
            var band = new Rect(row.x, row.y, 160f, row.height);
            NcRound(band, NcHeadBrown, new Vector4(16f, 0f, 0f, 16f));
            NcLabel(band, label, _ncHeadText, NcCream);
            var right = _ncCellRight;
            var color = right.normal.textColor;
            right.normal.textColor = NcText;
            FitNcLabel(new Rect(row.x + 176f, row.y, 260f, row.height), Math.Max(0, count).ToString("N0", CultureInfo.InvariantCulture), right, 34);
            right.normal.textColor = color;
            PmSmall(new Rect(row.x + 444f, row.y + 26f, 40f, 44f), "人", NcText, 22);
            string diff = delta > 0 ? "▲ " + delta.ToString("N0", CultureInfo.InvariantCulture)
                : delta < 0 ? "▼ " + (-delta).ToString("N0", CultureInfo.InvariantCulture)
                : "± 0";
            var tint = delta > 0 ? PmPlus : delta < 0 ? PmMinus : NcHeadBrown;
            NcLabel(new Rect(row.x + 476f, row.y, row.width - 496f, row.height), diff, _pmMoney, tint);
        }

        /// <summary>右の「収支」：入金は青で＋、出金は赤で−。必要運営費は参考（灰色）。線の下に収支。</summary>
        void DrawPmBalance(Fixture fixture, bool home)
        {
            var p = PmBalance;
            DrawPmPanel(p, "収支");
            var appearance = home ? fixture.HomeAppearancePaid : fixture.AwayAppearancePaid;
            var win = home ? fixture.HomeWinPaid : fixture.AwayWinPaid;
            float x = p.x + 32f;
            float width = p.width - 64f;
            float y = p.y + 64f;
            BigInteger net;
            if (home)
            {
                y = PmMoneyRow(x, y, width, "入場料", fixture.GateIncome, true);
                y = PmMoneyRow(x, y, width, "広告費", fixture.AdvertisingSpent, false);
                y = PmMoneyRow(x, y, width, "運営費", fixture.OperatingPaid, false);
                y = PmMoneyRow(x, y, width, "出場給", appearance, false);
                y = PmMoneyRow(x, y, width, "勝利給", win, false);
                bool short_ = fixture.OperatingPaid < fixture.OperatingRequired;
                string note = short_ ? "必要運営費（不足：サポーターが減ります）" : "必要運営費（参考）";
                PmSmall(new Rect(x + 20f, y, width - 300f, PmRowHeight), note, short_ ? PmMinus : PmNote, 22);
                var right = _ncCellRight;
                var color = right.normal.textColor;
                right.normal.textColor = short_ ? PmMinus : PmNote;
                FitNcLabel(new Rect(x + width - 340f, y, 320f, PmRowHeight), MoneyText.Format(BigInteger.Abs(fixture.OperatingRequired)), right, 24);
                right.normal.textColor = color;
                y += PmRowStride;
                net = fixture.GateIncome - fixture.AdvertisingSpent - fixture.OperatingPaid - appearance - win;
            }
            else
            {
                y = PmMoneyRow(x, y, width, "出場給", appearance, false);
                y = PmMoneyRow(x, y, width, "勝利給", win, false);
                net = -appearance - win;
            }

            NcRound(new Rect(x, p.y + 448f, width, 3f), NcHeadBrown, 0f);
            var total = new Rect(x, p.y + 466f, width, 96f);
            NcRound(total, Color.white, 16f);
            NcOutline(total, NcYellow, 4f, 16f);
            PmSmall(new Rect(total.x + 24f, total.y, 200f, total.height), "収支", NcDarkBrown, 30);
            var big = _pmMoney;
            int previous = big.fontSize;
            big.fontSize = 48;
            var tint = net > 0 ? PmPlus : net < 0 ? PmMinus : NcText;
            NcLabel(new Rect(total.x + 220f, total.y, total.width - 244f, total.height), SignedMoney(net, true), big, tint);
            big.fontSize = previous;
        }

        float PmMoneyRow(float x, float y, float width, string label, BigInteger amount, bool income)
        {
            var row = new Rect(x, y, width, PmRowHeight);
            NcRound(row, NcListRow, 12f);
            NcOutline(row, NcRowLine, 2f, 12f);
            PmSmall(new Rect(row.x + 20f, row.y, 260f, row.height), label, NcText, 24);
            var value = income ? amount : -amount;
            var tint = value > 0 ? PmPlus : value < 0 ? PmMinus : NcText;
            NcLabel(new Rect(row.x + 280f, row.y, row.width - 300f, row.height), SignedMoney(value, false), _pmMoney, tint);
            return y + PmRowStride;
        }

        static string SignedMoney(BigInteger value, bool zeroPlain)
        {
            if (value == 0) return zeroPlain ? "0円" : "0";
            string digits = MoneyText.Format(BigInteger.Abs(value));
            return (value > 0 ? "+" : "-") + digits;
        }

        // =========================================================== 共通

        /// <summary>ロッカールームの背景を少し暗くし、スタジアムと同じ枠と看板を描く。</summary>
        void DrawPmFrame(string title)
        {
            MatchBackdrop.Draw(new Color(0f, 0f, 0f, 0.35f));
            DrawNcFrame(title);
        }

        /// <summary>今日の試合の帯：「今日の試合」、HOME/AWAY、相手のエンブレムと名前、スコア、勝敗の札。</summary>
        void DrawPmTodayStrip(GameSession session, Fixture fixture, Club club)
        {
            if (fixture == null || fixture.Result == null || club == null) return;
            var result = fixture.Result;
            bool home = fixture.Home == club;
            var opponent = home ? fixture.Away : fixture.Home;
            int mine = home ? result.HomeGoals : result.AwayGoals;
            int theirs = home ? result.AwayGoals : result.HomeGoals;
            string score = mine.ToString(CultureInfo.InvariantCulture) + "-" + theirs.ToString(CultureInfo.InvariantCulture);
            string pk = result.PenaltyText(club);
            if (!string.IsNullOrEmpty(pk)) score += "（" + pk + "）";
            string name = opponent != null ? opponent.Name ?? "" : "";

            var bold = _ncCellLeft;
            int previousSize = bold.fontSize;
            bold.fontSize = 22;
            float wLabel = bold.CalcSize(new GUIContent("今日の試合")).x;
            float wName = Mathf.Min(360f, bold.CalcSize(new GUIContent(name)).x);
            bold.fontSize = 28;
            float wScore = bold.CalcSize(new GUIContent(score)).x;
            bold.fontSize = previousSize;

            float width = 20f + wLabel + 16f + 84f + 16f + 40f + 12f + wName + 16f + wScore + 16f + 104f + 24f;
            var strip = new Rect(PmStrip.x, PmStrip.y, width, PmStrip.height);
            NcBox(strip, PmBand, NcRowLine, 2f, 24f);
            float x = strip.x + 20f;
            PmSmall(new Rect(x, strip.y, wLabel + 4f, strip.height), "今日の試合", NcHeadBrown, 22);
            x += wLabel + 16f;
            DrawPmSideTag(new Rect(x, strip.y + 9f, 84f, 30f), home);
            x += 84f + 16f;
            DrawNsEmblem(new Rect(x, strip.y + 4f, 40f, 40f), opponent);
            x += 40f + 12f;
            PmSmall(new Rect(x, strip.y, wName + 4f, strip.height), name, NcText, 22);
            x += wName + 16f;
            PmSmall(new Rect(x, strip.y, wScore + 4f, strip.height), score, NcDarkBrown, 28);
            x += wScore + 16f;
            DrawPmOutcome(new Rect(x, strip.y + 7f, 104f, 34f), result, club, 18);
        }

        void DrawPmStripInfo(string text)
        {
            var right = _ncCellRight;
            var color = right.normal.textColor;
            right.normal.textColor = NcHeadBrown;
            FitNcLabel(new Rect(1088f, PmStrip.y, 640f, PmStrip.height), text, right, 24);
            right.normal.textColor = color;
        }

        /// <summary>「リーグ第N節」（自クラブのリーグ戦を日付順に数える）、カップは「カップN」。</summary>
        static string CompetitionLabel(GameSession session, Fixture fixture, Club club)
        {
            if (fixture == null) return "";
            if (!fixture.IsLeague)
                return "カップ" + (fixture.CupRound > 0 ? fixture.CupRound.ToString(CultureInfo.InvariantCulture) : "");
            int section = 0;
            if (session != null && session.League != null && club != null)
            {
                var list = session.League.Fixtures;
                for (int i = 0; i < list.Count; i++)
                {
                    var f = list[i];
                    if (f == null || (f.Home != club && f.Away != club)) continue;
                    if (f.Date.Date <= fixture.Date.Date) section++;
                }
            }

            return section > 0 ? "リーグ第" + section.ToString(CultureInfo.InvariantCulture) + "節" : "リーグ";
        }

        void PmSmall(Rect rect, string text, Color color, int size)
        {
            var style = _ncCellLeft;
            var previous = style.normal.textColor;
            style.normal.textColor = color;
            FitNcLabel(rect, text, style, size);
            style.normal.textColor = previous;
        }

        /// <summary>試合後の画面の「次へ」ボタン（Figma の button_next）。黄色の角丸に焦げ茶の線。</summary>
        public bool DrawPostMatchNext(Rect rect, Font font)
        {
            EnsurePmStyles(font);
            NcRound(new Rect(rect.x, rect.y + 5f, rect.width, rect.height), NcShade(0.4f), rect.height * 0.5f);
            NcBox(rect, NcYellow, NcDarkBrown, 5f, rect.height * 0.5f);
            int previous = _ncBold.fontSize;
            _ncBold.fontSize = 32;
            NcLabel(rect, "次へ ▶", _ncBold, NcDarkBrown);
            _ncBold.fontSize = previous;
            return GUI.Button(rect, GUIContent.none, _pmHit);
        }

        void EnsurePmStyles(Font font)
        {
            if (font != null) _font = font;
            EnsureNcStyles();
            if (_pmScore != null && _pmFont == _font) return;
            _pmFont = _font;
            _pmScore = new GUIStyle(_ncBold) { fontSize = 96, alignment = TextAnchor.MiddleCenter };
            _pmBig = new GUIStyle(_ncBold) { fontSize = 72, alignment = TextAnchor.MiddleLeft };
            _pmRating = new GUIStyle(_ncBold) { fontSize = 32, alignment = TextAnchor.MiddleRight };
            _pmMoney = new GUIStyle(_ncBold) { fontSize = 28, alignment = TextAnchor.MiddleRight };
            _pmHit = new GUIStyle(GUIStyle.none);
        }
    }
}
