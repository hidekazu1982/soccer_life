using System;
using System.Collections.Generic;

namespace SoccerClub.Data
{
    /// <summary>
    /// 季節のパーティ（花見・バーベキュー・ハロウィン・クリスマス）。Resources/Master/Parties.csv を読む。
    /// 秘書が開催を知らせ、プレイヤーが招待する選手を選ぶ。会話は SeasonTalk.csv の season 列にパーティの id を書く。
    /// </summary>
    public static class PartyCatalog
    {
        public sealed class Party
        {
            public string Id;
            public int Month;
            public int Day;
            public string Title;
            public string Banner;
            public string Background;

            /// <summary>招待できる人数。</summary>
            public int Slots = 6;

            /// <summary>1人あたりの費用（年俸の%）。</summary>
            public double CostPercent = 3d;

            /// <summary>招待した人同士の全ペアに足す親密度。</summary>
            public int Affinity = 3;

            /// <summary>招待した人の調子に足す値。</summary>
            public int Condition = 10;

            /// <summary>伸びる能力。-1 なし、0 攻撃、1 守備、2 スタミナ（練習1日分）。</summary>
            public int Stat = -1;

            /// <summary>21歳以下への効果の倍率（親密度は2人とも21歳以下の組）。</summary>
            public double YoungMultiplier = 1d;

            /// <summary>恋人・夫婦を2人とも招待したときに足す親密度。</summary>
            public int PartnerBonus = 5;

            /// <summary>友達・親友を2人とも招待したときに足す親密度。</summary>
            public int FriendBonus = 5;

            /// <summary>招待した選手が移籍しにくくなる割合（%）。そのシーズンの終わりまで。</summary>
            public int LeavePercent = 30;
        }

        static readonly List<Party> _parties = new List<Party>();
        static bool _loaded;

        /// <summary>読み込んだパーティ（CSV の並び順）。デバッグメニューなどで使う。</summary>
        public static IReadOnlyList<Party> All
        {
            get
            {
                Ensure();
                return _parties;
            }
        }

        public static Party Find(string id)
        {
            Ensure();
            for (int i = 0; i < _parties.Count; i++)
            {
                if (_parties[i].Id == id) return _parties[i];
            }

            return null;
        }

        public static Party On(DateTime date)
        {
            Ensure();
            for (int i = 0; i < _parties.Count; i++)
            {
                var p = _parties[i];
                if (p.Month == date.Month && p.Day == date.Day) return p;
            }

            return null;
        }

        public static string StatLabel(int stat)
        {
            switch (stat)
            {
                case 0: return "攻撃";
                case 1: return "守備";
                case 2: return "スタミナ";
                default: return "";
            }
        }

        public static void Reload()
        {
            _loaded = false;
            Ensure();
        }

        static void Ensure()
        {
            if (_loaded) return;
            _loaded = true;
            _parties.Clear();
            // 列: id, month, day, title, banner, background, slots, cost_percent, affinity, condition, stat,
            //     young_mult, partner_bonus, friend_bonus, leave_percent
            var rows = MasterCsv.Rows("Parties.csv");
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row == null || row.Length < 6) continue;
                string id = (row[0] ?? "").Trim();
                if (id.Length == 0 || id == "id") continue;
                int month;
                int day;
                if (!int.TryParse(Cell(row, 1), out month) || month < 1 || month > 12) continue;
                if (!int.TryParse(Cell(row, 2), out day) || day < 1 || day > 31) continue;
                if (month == 1 && day == 1) continue;
                var p = new Party
                {
                    Id = id,
                    Month = month,
                    Day = day,
                    Title = Cell(row, 3),
                    Banner = Cell(row, 4),
                    Background = Cell(row, 5)
                };
                p.Slots = Int(row, 6, p.Slots, 1, 30);
                p.CostPercent = Double(row, 7, p.CostPercent);
                p.Affinity = Int(row, 8, p.Affinity, 0, 100);
                p.Condition = Int(row, 9, p.Condition, 0, 100);
                p.Stat = StatOf(Cell(row, 10));
                p.YoungMultiplier = Double(row, 11, p.YoungMultiplier);
                p.PartnerBonus = Int(row, 12, p.PartnerBonus, 0, 100);
                p.FriendBonus = Int(row, 13, p.FriendBonus, 0, 100);
                p.LeavePercent = Int(row, 14, p.LeavePercent, 0, 100);
                if (string.IsNullOrEmpty(p.Title)) p.Title = "パーティー";
                _parties.Add(p);
            }
        }

        static string Cell(string[] row, int index)
        {
            return index < row.Length ? (row[index] ?? "").Trim() : "";
        }

        static int Int(string[] row, int index, int fallback, int min, int max)
        {
            int value;
            if (!int.TryParse(Cell(row, index), out value)) return fallback;
            return Math.Max(min, Math.Min(max, value));
        }

        static double Double(string[] row, int index, double fallback)
        {
            double value;
            if (!double.TryParse(Cell(row, index), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value)) return fallback;
            return value < 0d ? 0d : value;
        }

        static int StatOf(string text)
        {
            switch (text)
            {
                case "offense": return 0;
                case "defense": return 1;
                case "stamina": return 2;
                default: return -1;
            }
        }
    }
}
