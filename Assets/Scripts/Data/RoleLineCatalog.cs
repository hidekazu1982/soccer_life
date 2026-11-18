using System;
using System.Collections.Generic;

namespace SoccerClub.Data
{
    /// <summary>
    /// 「役（会話の順番）・条件・重み・台詞」の4列の CSV を読む、使い回し用の台詞集。
    /// 条件の意味は使う側が matches で渡す。条件を満たす行から weight の重みで1つ選び、無ければ fallback の行を使う。
    /// </summary>
    public sealed class RoleLineCatalog
    {
        public const string FallbackCondition = "fallback";

        sealed class Entry
        {
            public string Role;
            public string Condition;
            public int Weight;
            public string Text;
        }

        readonly string _fileName;
        readonly string[] _roles;
        readonly Dictionary<string, string> _builtIn;
        readonly List<Entry> _entries = new List<Entry>();
        bool _loaded;

        /// <param name="builtIn">CSV が無いときに使う、役ごとの組み込みの1行。</param>
        public RoleLineCatalog(string fileName, string[] roles, Dictionary<string, string> builtIn)
        {
            _fileName = fileName;
            _roles = roles;
            _builtIn = builtIn ?? new Dictionary<string, string>();
        }

        public IReadOnlyList<string> Roles => _roles;

        /// <summary>役の順に、差し込みを埋めた台詞を返す。</summary>
        public string[] Lines(Func<string, bool> matches, Dictionary<string, string> tokens, Random random)
        {
            Ensure();
            var result = new string[_roles.Length];
            for (int r = 0; r < _roles.Length; r++)
            {
                var picked = Pick(_roles[r], matches, false, random) ?? Pick(_roles[r], matches, true, random);
                result[r] = picked != null ? RelationEventCatalog.Fill(picked.Text, tokens) : "";
            }

            return result;
        }

        Entry Pick(string role, Func<string, bool> matches, bool fallback, Random random)
        {
            int total = 0;
            for (int i = 0; i < _entries.Count; i++)
            {
                if (Usable(_entries[i], role, matches, fallback)) total += _entries[i].Weight;
            }

            if (total <= 0) return null;
            int roll = random != null ? random.Next(total) : 0;
            for (int i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                if (!Usable(e, role, matches, fallback)) continue;
                roll -= e.Weight;
                if (roll < 0) return e;
            }

            return null;
        }

        static bool Usable(Entry e, string role, Func<string, bool> matches, bool fallback)
        {
            if (e.Role != role) return false;
            bool isFallback = e.Condition == FallbackCondition;
            if (isFallback != fallback) return false;
            if (fallback) return true;
            if (e.Condition == "any") return true;
            return matches != null && matches(e.Condition);
        }

        public void Reload()
        {
            _loaded = false;
            Ensure();
        }

        void Ensure()
        {
            if (_loaded) return;
            _loaded = true;
            _entries.Clear();
            var rows = MasterCsv.Rows(_fileName);
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row == null || row.Length < 4) continue;
                string role = (row[0] ?? "").Trim();
                if (Array.IndexOf(_roles, role) < 0) continue;
                string condition = (row[1] ?? "").Trim();
                if (condition.Length == 0) condition = "any";
                int weight;
                if (!int.TryParse((row[2] ?? "").Trim(), out weight)) weight = 1;
                if (weight <= 0) continue;
                string text = (row[3] ?? "").Trim();
                if (text.Length == 0) continue;
                _entries.Add(new Entry { Role = role, Condition = condition, Weight = weight, Text = text });
            }

            foreach (var pair in _builtIn)
            {
                bool has = false;
                for (int i = 0; i < _entries.Count; i++)
                {
                    if (_entries[i].Role == pair.Key && _entries[i].Condition == FallbackCondition) has = true;
                }

                if (!has)
                    _entries.Add(new Entry { Role = pair.Key, Condition = FallbackCondition, Weight = 1, Text = pair.Value });
            }
        }
    }
}
