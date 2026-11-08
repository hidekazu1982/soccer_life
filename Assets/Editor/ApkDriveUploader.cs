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
    public class ApkDriveUploader : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        const string PrefKeyFolder = "SoccerClubSim.ApkDriveUploader.Folder";
        const string PrefKeyEnabled = "SoccerClubSim.ApkDriveUploader.Enabled";
        const string SessionKeyStart = "SoccerClubSim.ApkDriveUploader.BuildStartTicks";
        const string MenuRoot = "SoccerClubSim/APKアップロード/";
        const string MenuEnabled = MenuRoot + "ビルド後に自動コピーする";

        // 既定のコピー先（メニューから変更可能）
        const string DefaultFolder = @"G:\マイドライブ\game\soccer_life\APK";

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

            // 例: SoccerClubSim_v0.1_20261107_0913.apk
            string baseName = Path.GetFileNameWithoutExtension(src);
            string version = SanitizeFileName(PlayerSettings.bundleVersion);
            string fileName = $"{baseName}_v{version}_{DateTime.Now:yyyyMMdd_HHmm}{ext}";
            string dst = Path.Combine(folder, fileName);

            File.Copy(src, dst, overwrite: true);

            long sizeMb = new FileInfo(dst).Length / (1024 * 1024);
            Debug.Log($"[ApkDriveUploader] Google ドライブへコピーしました（{sizeMb} MB）: {dst}\n" +
                      "アップロードはパソコン版 Google ドライブがバックグラウンドで行います。");
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
