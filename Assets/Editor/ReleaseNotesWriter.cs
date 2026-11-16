#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SoccerClubSim.EditorTools
{
    /// <summary>
    /// Android の APK ビルドが成功したら、リリースノートを作って Google ドライブにも置く。
    /// ・更新内容 : プロジェクト直下の CHANGES_NEXT.md に書かれたメモ（「- 」で始まる行）
    /// ・変更ファイル : 前回の APK 以降に更新された Assets / Packages / ProjectSettings のファイル
    /// ・出力 : Builds/RELEASE_NOTES.txt の先頭に新しい版を追記し、ドライブの APK フォルダにコピー
    /// ・書き終わったら CHANGES_NEXT.md を空のひな形に戻す
    /// </summary>
    public class ReleaseNotesWriter : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        // ApkDriveUploader と同じ設定を使う
        const string PrefKeyFolder = "SoccerClubSim.ApkDriveUploader.Folder";
        const string DefaultFolder = @"G:\マイドライブ\game\soccer_life\APK";

        const string NotesFileName = "RELEASE_NOTES.txt";
        const string MemoFileName = "CHANGES_NEXT.md";
        const string SessionKeyStart = "SoccerClubSim.ReleaseNotesWriter.BuildStartTicks";
        const string SessionKeyPrev = "SoccerClubSim.ReleaseNotesWriter.PrevBuildTicks";
        const int MaxFilesPerGroup = 30;

        const string MemoTemplate =
            "# 次の APK に入る変更メモ\n" +
            "# 変更したら「- 」で始まる行で1つずつ追記してください。\n" +
            "# APK ができるとリリースノートに移され、このファイルは空に戻ります。\n" +
            "# 例: - 選手の性別とパートナー設定を追加\n";

        // ApkDriveUploader（1000）の後に動かす
        public int callbackOrder => 1100;

        static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;
        static string BuildsDir => Path.Combine(ProjectRoot, "Builds");
        static string MemoPath => Path.Combine(ProjectRoot, MemoFileName);
        static string LocalNotesPath => Path.Combine(BuildsDir, NotesFileName);

        // ---------- ビルドの前後 ----------

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android) return;

            SessionState.SetString(SessionKeyStart, DateTime.UtcNow.Ticks.ToString());
            SessionState.SetString(SessionKeyPrev, PreviousBuildTimeUtc().Ticks.ToString());
            EnsureMemoFile();
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android) return;

            try
            {
                Write(report.summary.outputPath);
            }
            catch (Exception e)
            {
                // ノートが書けなくてもビルドは失敗させない
                Debug.LogError($"[ReleaseNotesWriter] リリースノートを作れませんでした: {e}");
            }
        }

        static void Write(string apkPath)
        {
            string ext = Path.GetExtension(apkPath).ToLowerInvariant();
            if (ext != ".apk" && ext != ".aab") return;
            if (!File.Exists(apkPath)) return;

            var start = new DateTime(ReadTicks(SessionKeyStart), DateTimeKind.Utc);
            if (File.GetLastWriteTimeUtc(apkPath) < start) return; // 今回のビルドでできた APK ではない

            var prev = new DateTime(ReadTicks(SessionKeyPrev), DateTimeKind.Utc);

            string entry = BuildEntry(prev);

            // 新しい版を先頭に追記
            Directory.CreateDirectory(BuildsDir);
            string old = File.Exists(LocalNotesPath) ? File.ReadAllText(LocalNotesPath, Encoding.UTF8) : "";
            string header = "soccer_life リリースノート\n=========================\n\n";
            if (old.StartsWith(header)) old = old.Substring(header.Length);
            File.WriteAllText(LocalNotesPath, header + entry + "\n" + old, new UTF8Encoding(true));

            // ドライブへコピー
            string folder = EditorPrefs.GetString(PrefKeyFolder, DefaultFolder);
            if (!string.IsNullOrEmpty(folder))
            {
                if (!Directory.Exists(folder))
                {
                    string parent = Path.GetDirectoryName(folder);
                    if (!string.IsNullOrEmpty(parent) && Directory.Exists(parent))
                        Directory.CreateDirectory(folder);
                }

                if (Directory.Exists(folder))
                {
                    File.Copy(LocalNotesPath, Path.Combine(folder, NotesFileName), overwrite: true);
                    Debug.Log($"[ReleaseNotesWriter] リリースノートをドライブへコピーしました: {Path.Combine(folder, NotesFileName)}");
                }
                else
                {
                    Debug.LogWarning($"[ReleaseNotesWriter] ドライブのフォルダが見つかりません: {folder}");
                }
            }

            // メモを空に戻す
            File.WriteAllText(MemoPath, MemoTemplate, new UTF8Encoding(true));
        }

        static string BuildEntry(DateTime prevUtc)
        {
            var sb = new StringBuilder();
            sb.Append($"■ v{PlayerSettings.bundleVersion}（{DateTime.Now:yyyy-MM-dd HH:mm}）\n");
            sb.Append($"  versionCode: {PlayerSettings.Android.bundleVersionCode}\n\n");

            // 更新内容（メモ）
            sb.Append("【更新内容】\n");
            List<string> memo = ReadMemo();
            if (memo.Count == 0)
                sb.Append("  （メモなし）\n");
            else
                foreach (string line in memo) sb.Append("  ").Append(line).Append('\n');
            sb.Append('\n');

            // 変更ファイル
            List<string> changed = ChangedFiles(prevUtc);
            string since = prevUtc == DateTime.MinValue ? "初回" : $"{prevUtc.ToLocalTime():MM/dd HH:mm} 以降";
            sb.Append($"【変更ファイル】{changed.Count} 件（{since}）\n");
            if (changed.Count == 0)
            {
                sb.Append("  （なし）\n");
            }
            else
            {
                foreach (var group in changed.GroupBy(Category).OrderBy(g => g.Key))
                {
                    var files = group.OrderBy(f => f).ToList();
                    sb.Append($"  ◇ {group.Key.Substring(2)}（{files.Count} 件）\n");
                    foreach (string f in files.Take(MaxFilesPerGroup)) sb.Append("    ").Append(f).Append('\n');
                    if (files.Count > MaxFilesPerGroup)
                        sb.Append($"    ほか {files.Count - MaxFilesPerGroup} 件\n");
                }
            }

            sb.Append("\n-------------------------\n");
            return sb.ToString();
        }

        // 並び順を決めるため先頭に番号を付ける（表示時に外す）
        static string Category(string path)
        {
            string p = path.Replace('\\', '/');
            if (p.StartsWith("Assets/Editor/")) return "5 エディタ用ツール";
            if (p.EndsWith(".cs")) return "1 スクリプト";
            if (p.EndsWith(".unity")) return "2 シーン";
            if (p.StartsWith("Assets/Resources/") || p.StartsWith("Assets/StreamingAssets/")) return "3 データ・素材";
            if (p.StartsWith("Packages/") || p.StartsWith("ProjectSettings/")) return "6 設定・パッケージ";
            return "4 その他のアセット";
        }

        static List<string> ReadMemo()
        {
            var lines = new List<string>();
            if (!File.Exists(MemoPath)) return lines;

            foreach (string raw in File.ReadAllLines(MemoPath, Encoding.UTF8))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                if (!line.StartsWith("-") && !line.StartsWith("・")) line = "- " + line;
                lines.Add(line);
            }
            return lines;
        }

        static List<string> ChangedFiles(DateTime sinceUtc)
        {
            var result = new List<string>();
            foreach (string dir in new[] { "Assets", "Packages", "ProjectSettings" })
            {
                string full = Path.Combine(ProjectRoot, dir);
                if (!Directory.Exists(full)) continue;

                foreach (string file in Directory.EnumerateFiles(full, "*", SearchOption.AllDirectories))
                {
                    if (file.EndsWith(".meta")) continue;
                    // バージョン番号の更新でいつも変わるので除く
                    if (file.EndsWith(Path.Combine("ProjectSettings", "ProjectSettings.asset"))) continue;
                    if (File.GetLastWriteTimeUtc(file) > sinceUtc)
                        result.Add(file.Substring(ProjectRoot.Length + 1).Replace('\\', '/'));
                }
            }
            return result;
        }

        static DateTime PreviousBuildTimeUtc()
        {
            if (!Directory.Exists(BuildsDir)) return DateTime.MinValue;
            var apks = Directory.GetFiles(BuildsDir, "*.apk");
            return apks.Length == 0 ? DateTime.MinValue : apks.Max(File.GetLastWriteTimeUtc);
        }

        static long ReadTicks(string key)
        {
            return long.TryParse(SessionState.GetString(key, "0"), out long t) ? t : 0;
        }

        static void EnsureMemoFile()
        {
            if (!File.Exists(MemoPath))
                File.WriteAllText(MemoPath, MemoTemplate, new UTF8Encoding(true));
        }

        [MenuItem("SoccerClubSim/APKアップロード/変更メモを開く", priority = 40)]
        static void OpenMemo()
        {
            EnsureMemoFile();
            EditorUtility.OpenWithDefaultApp(MemoPath);
        }
    }
}
#endif
