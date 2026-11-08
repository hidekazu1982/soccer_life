using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace SoccerClub.Data
{
    /// <summary>
    /// Resources/Master の CSV。APK にも同梱される。先頭が # の行は説明なので読み飛ばす。
    /// </summary>
    public static class MasterCsv
    {
        public static List<string[]> Rows(string fileName)
        {
            string resource = "Master/" + Path.GetFileNameWithoutExtension(fileName);
            var asset = Resources.Load<TextAsset>(resource);
            if (asset == null || string.IsNullOrEmpty(asset.text))
            {
                Debug.LogError("マスタが見つからない: " + resource);
                return new List<string[]>();
            }

            string text = asset.text;
            if (text.Length > 0 && text[0] == '\uFEFF')
                text = text.Substring(1);
            return Parse(text);
        }

        public static List<string[]> Parse(string text)
        {
            var rows = new List<string[]>();
            if (string.IsNullOrEmpty(text)) return rows;
            var field = new StringBuilder();
            var row = new List<string>();
            bool quoted = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (quoted)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"')
                        {
                            field.Append('"');
                            i++;
                        }
                        else
                        {
                            quoted = false;
                        }
                    }
                    else
                    {
                        field.Append(c);
                    }

                    continue;
                }

                if (c == '"')
                {
                    quoted = true;
                    continue;
                }

                if (c == ',')
                {
                    row.Add(field.ToString());
                    field.Length = 0;
                    continue;
                }

                if (c == '\r') continue;
                if (c == '\n')
                {
                    row.Add(field.ToString());
                    field.Length = 0;
                    Add(rows, row);
                    row.Clear();
                    continue;
                }

                field.Append(c);
            }

            if (field.Length > 0 || row.Count > 0)
            {
                row.Add(field.ToString());
                Add(rows, row);
            }

            return rows;
        }

        static void Add(List<string[]> rows, List<string> row)
        {
            if (row.Count == 0) return;
            bool blank = true;
            for (int i = 0; i < row.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(row[i]))
                {
                    blank = false;
                    break;
                }
            }

            if (blank) return;
            if (row[0].TrimStart().StartsWith("#")) return;
            rows.Add(row.ToArray());
        }
    }
}
