using System;
using System.Collections.Generic;

namespace SoccerClub.Data
{
    /// <summary>
    /// 契約満了の会議室の台詞。Resources/Master/ContractExpiry.csv を読む。
    /// 列: role, condition, weight, text。role は会話の順番（call / greet / notice / verdict / react / farewell）。
    /// 役ごとに、条件を満たす行から weight の重みで1つ選び、無ければ condition=fallback の行を使う。
    /// 同じ選手には毎回同じ台詞になる（選手 id から乱数を作る）。
    /// </summary>
    public static class ContractExpiryCatalog
    {
        public static readonly string[] Roles = { "call", "greet", "notice", "verdict", "react", "farewell" };

        /// <summary>台詞の条件に使う、選手の状況。</summary>
        public struct Context
        {
            public Player Player;
            public string GmName;

            /// <summary>配偶者が同じクラブに残っている。</summary>
            public string SpouseName;
        }

        sealed class Entry
        {
            public string Role;
            public string Condition;
            public int Weight;
            public string Text;
        }

        static readonly List<Entry> _entries = new List<Entry>();
        static bool _loaded;

        /// <summary>Roles の順に、埋めた台詞を返す。</summary>
        public static string[] Lines(Context context)
        {
            Ensure();
            var result = new string[Roles.Length];
            var random = new Random(Seed(context.Player != null ? context.Player.Id : ""));
            var tokens = new Dictionary<string, string>
            {
                { "name", context.Player != null ? context.Player.Name ?? "" : "" },
                { "gm", context.GmName ?? "" },
                { "age", context.Player != null ? context.Player.Age.ToString() : "" },
                { "spouse", context.SpouseName ?? "" },
                { "me", context.Player != null ? CharacterLife.Pronoun(context.Player.Id) : "" },
                { "parent", context.Player != null && context.Player.Gender == SoccerClub.Core.Gender.Female ? "ママ" : "パパ" }
            };

            for (int r = 0; r < Roles.Length; r++)
            {
                var picked = Pick(Roles[r], context, false, random) ?? Pick(Roles[r], context, true, random);
                result[r] = picked != null ? RelationEventCatalog.Fill(picked.Text, tokens) : "";
            }

            return result;
        }

        static Entry Pick(string role, Context context, bool fallback, Random random)
        {
            int total = 0;
            for (int i = 0; i < _entries.Count; i++)
            {
                if (Usable(_entries[i], role, context, fallback)) total += _entries[i].Weight;
            }

            if (total <= 0) return null;
            int roll = random.Next(total);
            for (int i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                if (!Usable(e, role, context, fallback)) continue;
                roll -= e.Weight;
                if (roll < 0) return e;
            }

            return null;
        }

        static bool Usable(Entry e, string role, Context context, bool fallback)
        {
            if (e.Role != role) return false;
            bool isFallback = e.Condition == RelationEventCatalog.FallbackCondition;
            if (isFallback != fallback) return false;
            return fallback || Matches(e.Condition, context);
        }

        /// <summary>条件名は CSV の condition 列。新しい条件はここに case を足す。</summary>
        static bool Matches(string condition, Context context)
        {
            var p = context.Player;
            if (p == null) return condition == "any";
            switch (condition)
            {
                case "any": return true;
                case "old": return p.Age >= 35;
                case "under35": return p.Age < 35;
                case "married": return p.Married;
                case "spouse_here": return !string.IsNullOrEmpty(context.SpouseName);
                case "children": return p.Children > 0;
                default: return false;
            }
        }

        static int Seed(string id)
        {
            int hash = 23;
            if (!string.IsNullOrEmpty(id))
            {
                for (int i = 0; i < id.Length; i++)
                    hash = unchecked(hash * 31 + id[i]);
            }

            return hash & int.MaxValue;
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
            var rows = MasterCsv.Rows("ContractExpiry.csv");
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row == null || row.Length < 4) continue;
                string role = (row[0] ?? "").Trim();
                if (Array.IndexOf(Roles, role) < 0) continue;
                string condition = (row[1] ?? "").Trim();
                if (condition.Length == 0) condition = "any";
                int weight;
                if (!int.TryParse((row[2] ?? "").Trim(), out weight)) weight = 1;
                if (weight <= 0) continue;
                string text = (row[3] ?? "").Trim();
                if (text.Length == 0) continue;
                _entries.Add(new Entry { Role = role, Condition = condition, Weight = weight, Text = text });
            }

            // CSV が無くても会話が途切れないよう、役ごとに組み込みの1行を足す。
            AddFallback("call", "{name}を呼んでくれ。大事な話がある。");
            AddFallback("greet", "失礼します。お呼びですか？");
            AddFallback("notice", "今シーズン、きみに出場の機会を作ってあげられなかった。");
            AddFallback("verdict", "話し合った結果、契約は今季で満了とすることになった。");
            AddFallback("react", "……わかりました。このクラブでプレーできて幸せでした。");
            AddFallback("farewell", "今まで本当にありがとう。これからの活躍を祈っているよ。");
        }

        static void AddFallback(string role, string text)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Role == role && _entries[i].Condition == RelationEventCatalog.FallbackCondition) return;
            }

            _entries.Add(new Entry { Role = role, Condition = RelationEventCatalog.FallbackCondition, Weight = 1, Text = text });
        }
    }
}
