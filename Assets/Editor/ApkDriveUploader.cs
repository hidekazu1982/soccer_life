#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SoccerClubSim.EditorTools
{
    /// <summary>
    /// Android ビルドが終わったら、APK（または AAB）を Google ドライブの同期フォルダへコピーする。
    /// アップロード自体はパソコン版 Google ドライブが自動で行う。
    /// 設定はメニュー「SoccerClubSim > APKアップロード」から。
    /// 設定値は EditorPrefs（PC ごと）に保存するので、リポジトリには入らない。
    /// </summary>
    [InitializeOnLoad]
    public class ApkDriveUploader : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        // Builds\request-prune-apk.build を置くと、確認なしで「最新版以外のAPKを削除」を行う（Claude から頼むとき用。2026-11-27）
        static string PruneRequestPath =>
            Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Builds", "request-prune-apk.build");
        static double nextPruneCheck;

        static ApkDriveUploader()
        {
            EditorApplication.update += PollPruneRequest;
        }

        static void PollPruneRequest()
        {
            if (EditorApplication.timeSinceStartup < nextPruneCheck) return;
            nextPruneCheck = EditorApplication.timeSinceStartup + 5;
            if (!File.Exists(PruneRequestPath)) return;
            try { File.Delete(PruneRequestPath); } catch (Exception) { return; }
            PruneAllButNewest(false);
        }

        const string PrefKeyFolder = "SoccerClubSim.ApkDriveUploader.Folder";
        const string PrefKeyEnabled = "SoccerClubSim.ApkDriveUploader.Enabled";
        const string SessionKeyStart = "SoccerClubSim.ApkDriveUploader.BuildStartTicks";
        const string MenuRoot = "SoccerClubSim/APKアップロード/";
        const string MenuEnabled = MenuRoot + "ビルド後に自動コピーする";

        // 既定のコピー先（メニューから変更可能）
        const string DefaultFolder = @"G:\マイドライブ\game\soccer_life\APK";

        // ドライブに置くファイル名の接頭辞
        const string FileNamePrefix = "soccer_life";

        // ドライブに APK を残す日数（これより古いものは、最新版を除いて削除）
        const int KeepDays = 14;

        // 他のポストプロセスより後に動かす
        public int callbackOrder => 1000;

        static string DriveFolder
        {
            get => EditorPrefs.GetString(PrefKeyFolder, DefaultFolder);
            set => EditorPrefs.SetString(PrefKeyFolder, value);
        }

        static bool Enabled
        {
            get => EditorPrefs.GetBool(PrefKeyEnabled, true);
            set => EditorPrefs.SetBool(PrefKeyEnabled, value);
        }

        // ---------- ビルドの前後 ----------

        public void OnPreprocessBuild(BuildReport report)
        {
            // 古い APK を間違えてコピーしないよう、ビルド開始時刻を記録しておく
            SessionState.SetString(SessionKeyStart, DateTime.UtcNow.Ticks.ToString());
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (!Enabled) return;
            if (report.summary.platform != BuildTarget.Android) return;

            try
            {
                CopyToDrive(report.summary.outputPath);
            }
            catch (Exception e)
            {
                // コピーに失敗してもビルド自体は失敗させない
                Debug.LogError($"[ApkDriveUploader] Google ドライブへのコピーに失敗しました: {e}");
            }
        }

        static void CopyToDrive(string src)
        {
            string ext = Path.GetExtension(src).ToLowerInvariant();
            if (ext != ".apk" && ext != ".aab")
            {
                // Export Project（Gradle プロジェクト出力）のときは対象外
                return;
            }

            if (!File.Exists(src))
            {
                Debug.LogWarning($"[ApkDriveUploader] ビルドされたファイルが見つかりません: {src}");
                return;
            }

            if (long.TryParse(SessionState.GetString(SessionKeyStart, "0"), out long startTicks) && startTicks > 0)
            {
                var buildStart = new DateTime(startTicks, DateTimeKind.Utc);
                if (File.GetLastWriteTimeUtc(src) < buildStart)
                {
                    Debug.LogWarning("[ApkDriveUploader] APK が今回のビルドで更新されていないため、コピーしませんでした。");
                    return;
                }
            }

            string folder = DriveFolder;
            if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
            {
                // 親フォルダ（Google ドライブ側）があれば、コピー先フォルダを作る
                string parent = Path.GetDirectoryName(folder);
                if (!string.IsNullOrEmpty(parent) && Directory.Exists(parent))
                {
                    Directory.CreateDirectory(folder);
                }
            }

            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                Debug.LogWarning("[ApkDriveUploader] コピー先が未設定か、見つかりません。" +
                                 "メニュー「SoccerClubSim > APKアップロード > コピー先フォルダを設定」で設定してください。");
                return;
            }

            // 例: soccer_life_v1.0_20261109_0211.apk
            // ローカルの APK 名に関係なく、ドライブ上の名前は固定の接頭辞で揃える
            string baseName = FileNamePrefix;
            string version = SanitizeFileName(PlayerSettings.bundleVersion);
            string fileName = $"{baseName}_v{version}_{DateTime.Now:yyyyMMdd_HHmm}{ext}";
            string dst = Path.Combine(folder, fileName);

            File.Copy(src, dst, overwrite: true);

            long sizeMb = new FileInfo(dst).Length / (1024 * 1024);
            Debug.Log($"[ApkDriveUploader] Google ドライブへコピーしました（{sizeMb} MB）: {dst}\n" +
                      "アップロードはパソコン版 Google ドライブがバックグラウンドで行います。");

            DeleteOldApks(folder, dst);
        }

        // 作ってから KeepDays 日を過ぎた APK をドライブから消す。最新版（今コピーしたもの）は必ず残す。
        // パソコン版 Google ドライブで消したファイルは、ドライブのゴミ箱に 30 日間残る。
        static void DeleteOldApks(string folder, string newest)
        {
            DateTime limit = DateTime.Now.AddDays(-KeepDays);
            int deleted = 0;

            foreach (string file in Directory.GetFiles(folder, "*.apk"))
            {
                if (string.Equals(Path.GetFullPath(file), Path.GetFullPath(newest), StringComparison.OrdinalIgnoreCase))
                    continue;
                if (File.GetLastWriteTime(file) >= limit) continue;

                try
                {
                    File.Delete(file);
                    deleted++;
                    Debug.Log($"[ApkDriveUploader] {KeepDays} 日を過ぎた APK を削除しました: {Path.GetFileName(file)}");
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[ApkDriveUploader] 古い APK を削除できませんでした: {Path.GetFileName(file)} ({e.Message})");
                }
            }

            if (deleted > 0)
                Debug.Log($"[ApkDriveUploader] 古い APK を {deleted} 件削除しました（{KeepDays} 日より前のもの）");
        }

        static string SanitizeFileName(string s)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                s = s.Replace(c, '_');
            }
            return s;
        }

        // ---------- メニュー ----------

        [MenuItem(MenuRoot + "コピー先フォルダを設定", priority = 1)]
        static void SelectFolder()
        {
            string current = string.IsNullOrEmpty(DriveFolder) ? "" : DriveFolder;
            string picked = EditorUtility.OpenFolderPanel("APK のコピー先（Google ドライブ内のフォルダ）", current, "");
            if (string.IsNullOrEmpty(picked)) return;

            DriveFolder = picked;
            Debug.Log($"[ApkDriveUploader] コピー先を設定しました: {picked}");
        }

        [MenuItem(MenuRoot + "コピー先フォルダを開く", priority = 2)]
        static void OpenFolder()
        {
            if (string.IsNullOrEmpty(DriveFolder) || !Directory.Exists(DriveFolder))
            {
                EditorUtility.DisplayDialog("APKアップロード", "コピー先フォルダがまだ設定されていません。", "OK");
                return;
            }
            EditorUtility.RevealInFinder(DriveFolder);
        }

        // 日数に関係なく、ドライブの APK を最新版（いちばん新しい 1 つ）だけ残して消す（2026-11-27 追加）。
        // パソコン版 Google ドライブで消したファイルは、ドライブのゴミ箱に 30 日間残る。
        [MenuItem(MenuRoot + "最新版以外のAPKを削除", priority = 3)]
        static void DeleteAllButNewest() => PruneAllButNewest(true);

        static void PruneAllButNewest(bool ask)
        {
            string folder = DriveFolder;
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                Debug.LogWarning("[ApkDriveUploader] コピー先フォルダが見つかりません: " + folder);
                if (ask) EditorUtility.DisplayDialog("APKアップロード", "コピー先フォルダが見つかりません。", "OK");
                return;
            }

            string[] files = Directory.GetFiles(folder, "*.apk");
            if (files.Length <= 1)
            {
                Debug.Log("[ApkDriveUploader] 消せる古い APK はありません");
                if (ask) EditorUtility.DisplayDialog("APKアップロード", "消せる古い APK はありません。", "OK");
                return;
            }

            string newest = files[0];
            foreach (string file in files)
            {
                if (File.GetLastWriteTime(file) > File.GetLastWriteTime(newest)) newest = file;
            }

            if (ask && !EditorUtility.DisplayDialog("APKアップロード",
                    $"最新版の {Path.GetFileName(newest)} だけ残して、ほかの APK {files.Length - 1} 件を削除します。\n" +
                    "（ドライブのゴミ箱に 30 日間残ります）", "削除する", "やめる"))
                return;

            int deleted = 0;
            foreach (string file in files)
            {
                if (file == newest) continue;
                try
                {
                    File.Delete(file);
                    deleted++;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[ApkDriveUploader] APK を削除できませんでした: {Path.GetFileName(file)} ({e.Message})");
                }
            }

            Debug.Log($"[ApkDriveUploader] 最新版（{Path.GetFileName(newest)}）以外の APK を {deleted} 件削除しました");
        }

        [MenuItem(MenuEnabled, priority = 20)]
        static void ToggleEnabled()
        {
            Enabled = !Enabled;
            Debug.Log($"[ApkDriveUploader] 自動コピー: {(Enabled ? "ON" : "OFF")}");
        }

        [MenuItem(MenuEnabled, true)]
        static bool ToggleEnabledValidate()
        {
            Menu.SetChecked(MenuEnabled, Enabled);
            return true;
        }
    }
}
#endif
