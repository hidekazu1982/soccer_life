using System.Collections.Generic;

namespace SoccerClub.Data
{
    /// <summary>
    /// 姓と名を分けない名前呼び。中身は Resources/Master/Names.csv。
    /// </summary>
    public static class NameCatalog
    {
        static string[] _names;

        public static string[] Names
        {
            get
            {
                Ensure();
                return _names;
            }
        }

        public static bool Contains(string name)
        {
            Ensure();
            if (string.IsNullOrEmpty(name)) return false;
            for (int i = 0; i < _names.Length; i++)
            {
                if (_names[i] == name) return true;
            }

            return false;
        }

        public static bool IsCallingName(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length > 8) return false;
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                bool hira = c >= 'ぁ' && c <= 'ゖ';
                bool kata = c >= 'ァ' && c <= 'ヶ';
                bool longMark = c == 'ー';
                if (!hira && !kata && !longMark) return false;
            }

            return true;
        }

        public static string ForId(string id)
        {
            Ensure();
            if (_names.Length == 0) return "ななし";
            int hash = 23;
            if (!string.IsNullOrEmpty(id))
            {
                for (int i = 0; i < id.Length; i++)
                    hash = unchecked(hash * 31 + id[i]);
            }

            int index = (hash & int.MaxValue) % _names.Length;
            return _names[index];
        }

        static void Ensure()
        {
            if (_names != null) return;
            var rows = MasterCsv.Rows("Names.csv");
            var list = new List<string>();
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i] == null || rows[i].Length == 0) continue;
                string name = (rows[i][0] ?? "").Trim();
                if (name.Length == 0 || name == "name") continue;
                if (!IsCallingName(name)) continue;
                list.Add(name);
            }

            _names = list.Count > 0 ? list.ToArray() : new[] { "ななし" };
        }
    }
}
