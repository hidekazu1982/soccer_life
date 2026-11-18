using System;
using System.Collections.Generic;

namespace SoccerClub.Data
{
    /// <summary>
    /// 居残り練習の台詞。Resources/Master/ExtraPractice.csv を読む。
    /// 列: stat（offense / defense / stamina / any）, condition（RelationEventCatalog.Matches と同じ）, weight, line_a, line_b, result。
    /// 伸びる能力と条件が合う行から weight の重みで1つ選び、無ければ condition=fallback の行を使う。
    /// </summary>
    public static class ExtraPracticeCatalog
    {
        sealed class Entry
        {
            public int Stat; // -1 は any
            public string Condition;
            public int Weight;
            public string LineA;
            public string LineB;
            public string Result;
        }

        static readonly List<Entry> _entries = new List<Entry>();
        static bool _loaded;

        /// <summary>stat は 0 攻撃、1 守備、2 スタミナ。a が話しかけ、b が答える。</summary>
        public static bool TryPick(int stat, Player a, Player b, Func<int, int> random, out string lineA, out string lineB, out string result)
        {
            Ensure();
            lineA = null;
            lineB = null;
            result = null;
            if (a == null || b == null) return false;
            var picked = Pick(stat, a, b, false, random) ?? Pick(stat, a, b, true, random);
            if (picked == null) return false;

            var tokens = RelationEventCatalog.Tokens(a, b);
            tokens["stat"] = StatLabel(stat);
            lineA = RelationEventCatalog.Fill(picked.LineA, tokens);
            lineB = RelationEventCatalog.Fill(picked.LineB, tokens);
            result = RelationEventCatalog.Fill(string.IsNullOrEmpty(picked.Result) ? "{nameA}と{nameB}の{stat}が上がった！" : picked.Result, tokens);
            return true;
        }

        static Entry Pick(int stat, Player a, Player b, bool fallback, Func<int, int> random)
        {
            int total = 0;
            for (int i = 0; i < _entries.Count; i++)
            {
                if (Usable(_entries[i], stat, a, b, fallback)) total += _entries[i].Weight;
            }

            if (total <= 0) return null;
            int roll = random != null ? random(total) : 0;
            for (int i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                if (!Usable(e, stat, a, b, fallback)) continue;
                roll -= e.Weight;
                if (roll < 0) return e;
            }

            return null;
        }

        static bool Usable(Entry e, int stat, Player a, Player b, bool fallback)
        {
            if (e.Stat >= 0 && e.Stat != stat) return false;
            bool isFallback = e.Condition == RelationEventCatalog.FallbackCondition;
            if (isFallback != fallback) return false;
            return fallback || RelationEventCatalog.Matches(e.Condition, a, b);
        }

        static string StatLabel(int stat)
        {
            switch (stat)
            {
                case 0: return "攻撃";
                case 1: return "守備";
                default: return "スタミナ";
            }
        }

        static bool TryParseStat(string text, out int stat)
        {
            switch ((text ?? "").Trim())
            {
                case "offense": stat = 0; return true;
                case "defense": stat = 1; return true;
                case "stamina": stat = 2; return true;
                case "any": stat = -1; return true;
                default: stat = -1; return false;
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
            _entries.Clear();
            var rows = MasterCsv.Rows("ExtraPractice.csv");
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row == null || row.Length < 5) continue;
                if (!TryParseStat(row[0], out var stat)) continue;
                string condition = (row[1] ?? "").Trim();
                if (condition.Length == 0) condition = "any";
                int weight;
                if (!int.TryParse((row[2] ?? "").Trim(), out weight)) weight = 1;
                if (weight <= 0) continue;
                string lineA = (row[3] ?? "").Trim();
                string lineB = (row[4] ?? "").Trim();
                if (lineA.Length == 0 || lineB.Length == 0) continue;
                _entries.Add(new Entry
                {
                    Stat = stat,
                    Condition = condition,
                    Weight = weight,
                    LineA = lineA,
                    LineB = lineB,
                    Result = row.Length > 5 ? (row[5] ?? "").Trim() : ""
                });
            }

            // CSV が無くても止まらないよう、組み込みの1行を足す。
            bool hasFallback = false;
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Stat < 0 && _entries[i].Condition == RelationEventCatalog.FallbackCondition) hasFallback = true;
            }

            if (!hasFallback)
            {
                _entries.Add(new Entry
                {
                    Stat = -1,
                    Condition = RelationEventCatalog.FallbackCondition,
                    Weight = 1,
                    LineA = "もう少しだけ、居残り練習していかない？",
                    LineB = "いいね。付き合うよ！",
                    Result = ""
                });
            }
        }
    }
}
