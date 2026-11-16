#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SoccerClubSim.EditorTools
{
    /// <summary>
    /// 毎日 4:00〜4:30 の間に、Unity エディタが開いていれば APK を自動でビルドする。
    /// ・前回の APK より新しい変更（Assets / Packages / ProjectSettings）がなければビルドしない
    /// ・PC が切れている、スリープ中、Unity が閉じている場合は何もしない（その日はスキップ）
    /// ・バージョンは ApkVersionBumper が自動で +0.0.1 する
    /// ・ドライブへのコピーは ApkDriveUploader が行う
    /// 結果は Builds/nightly-status.txt に追記する。
    /// </summary>
    [InitializeOnLoad]
    public static class NightlyApkBuilder
    {
        const int StartHour = 4;
        const int WindowMinutes = 30;              // 4:00〜4:30 の間だけ動く
        const double CheckIntervalSeconds = 60;    // 1 分ごとに確認
        const string PrefKeyLastDate = "SoccerClubSim.NightlyApkBuilder.LastDate";
        const string PrefKeyEnabled = "SoccerClubSim.NightlyApkBuilder.Enabled";
        const string MenuEnabled = "SoccerClubSim/APKアップロード/毎日4時に自動ビルド";

        static double nextCheck;
        static double nextRequestCheck;
        static bool running;

        static NightlyApkBuilder()
        {
            EditorApplication.update += Tick;
        }

        static bool Enabled
        {
            get => EditorPrefs.GetBool(PrefKeyEnabled, true);
            set => EditorPrefs.SetBool(PrefKeyEnabled, value);
        }

        static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;
        static string BuildsDir => Path.Combine(ProjectRoot, "Builds");
        static string OutputPath => Path.Combine(BuildsDir, "soccer_life.apk");
        static string StatusPath => Path.Combine(BuildsDir, "nightly-status.txt");

        // このファイルを置くと、時間に関係なくすぐ通常ビルドする（中身: minor / patch / none）
        static string RequestPath => Path.Combine(BuildsDir, "request-soccer_life.build");

        static bool IsBusy =>
            EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer;

        static void Tick()
        {
            if (running) return;

            if (EditorApplication.timeSinceStartup >= nextRequestCheck)
            {
                nextRequestCheck = EditorApplication.timeSinceStartup + 5;
                if (File.Exists(RequestPath) && !IsBusy)
                {
                    HandleRequest();
                    return;
                }
            }

            if (!Enabled) return;
            if (EditorApplication.timeSinceStartup < nextCheck) return;
            nextCheck = EditorApplication.timeSinceStartup + CheckIntervalSeconds;

            DateTime now = DateTime.Now;
            DateTime windowStart = now.Date.AddHours(StartHour);
            if (now < windowStart || now >= windowStart.AddMinutes(WindowMinutes)) return;

            string today = now.ToString("yyyy-MM-dd");
            if (EditorPrefs.GetString(PrefKeyLastDate, "") == today) return;

            // 作業中・コンパイル中・プレイ中は次の確認まで待つ（30 分の枠内で再挑戦）
            if (IsBusy) return;

            EditorPrefs.SetString(PrefKeyLastDate, today);

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                WriteStatus("SKIP ビルド対象が Android ではありません");
                return;
            }

            DateTime lastBuild = LastBuildTimeUtc();
            if (!HasChangesSince(lastBuild, out string changed))
            {
                WriteStatus($"SKIP 前回のビルド（{lastBuild.ToLocalTime():MM/dd HH:mm}）から変更がありません");
                return;
            }

            WriteStatus($"START 変更あり（例: {changed}）");
            RunBuild(1);
        }

        static void HandleRequest()
        {
            string text = "";
            try
            {
                text = File.ReadAllText(RequestPath).Trim().ToLowerInvariant();
                File.Delete(RequestPath);
            }
            catch (Exception e)
            {
                WriteStatus("FAIL 依頼ファイルを読めませんでした: " + e.Message);
                return;
            }

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                WriteStatus("SKIP 依頼ビルド: ビルド対象が Android ではありません");
                return;
            }

            int bump = text == "minor" ? 2 : text == "none" ? 0 : 1;
            string label = bump == 2 ? "新機能 +0.1.0" : bump == 0 ? "上げない" : "機能修正 +0.0.1";
            WriteStatus($"START 依頼ビルド（{label}）");
            RunBuild(bump);
        }

        // bump: 0 = 上げない、1 = +0.0.1、2 = +0.1.0
        static void RunBuild(int bump)
        {
            running = true;
            try
            {
                string[] scenes = EditorBuildSettings.scenes
                    .Where(s => s.enabled)
                    .Select(s => s.path)
                    .ToArray();

                if (scenes.Length == 0)
                {
                    WriteStatus("FAIL Scene List にシーンがありません");
                    return;
                }

                Directory.CreateDirectory(BuildsDir);
                EditorUserBuildSettings.buildAppBundle = false;

                // バージョン確認ダイアログを出さずに +0.0.1 させる
                ApkVersionBumper.ForceAutomated = true;
                ApkVersionBumper.ForcedBump = bump;

                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = OutputPath,
                    target = BuildTarget.Android,
                    options = BuildOptions.None
                });

                if (report.summary.result == BuildResult.Succeeded)
                    WriteStatus($"SUCCESS v{PlayerSettings.bundleVersion} {OutputPath}");
                else
                    WriteStatus($"FAIL {report.summary.result} errors={report.summary.totalErrors}");
            }
            catch (Exception e)
            {
                WriteStatus("FAIL " + e.Message);
                Debug.LogException(e);
            }
            finally
            {
                ApkVersionBumper.ForceAutomated = false;
                ApkVersionBumper.ForcedBump = -1;
                running = false;
            }
        }

        // 前回のビルド時刻 = Builds 内の APK のうち一番新しいものの更新時刻
        static DateTime LastBuildTimeUtc()
        {
            if (!Directory.Exists(BuildsDir)) return DateTime.MinValue;

            var apks = Directory.GetFiles(BuildsDir, "*.apk");
            if (apks.Length == 0) return DateTime.MinValue;

            return apks.Max(File.GetLastWriteTimeUtc);
        }

        static bool HasChangesSince(DateTime sinceUtc, out string changedFile)
        {
            changedFile = null;
            foreach (string dir in new[] { "Assets", "Packages", "ProjectSettings" })
            {
                string full = Path.Combine(ProjectRoot, dir);
                if (!Directory.Exists(full)) continue;

                foreach (string file in Directory.EnumerateFiles(full, "*", SearchOption.AllDirectories))
                {
                    if (File.GetLastWriteTimeUtc(file) > sinceUtc)
                    {
                        changedFile = file.Substring(ProjectRoot.Length + 1);
                        return true;
                    }
                }
            }
            return false;
        }

        static void WriteStatus(string message)
        {
            string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}";
            Debug.Log("[NightlyApkBuilder] " + line);
            try
            {
                Directory.CreateDirectory(BuildsDir);
                File.AppendAllText(StatusPath, line + Environment.NewLine);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[NightlyApkBuilder] ログを書けませんでした: " + e.Message);
            }
        }

        // ---------- メニュー ----------

        [MenuItem(MenuEnabled, priority = 30)]
        static void ToggleEnabled()
        {
            Enabled = !Enabled;
            Debug.Log($"[NightlyApkBuilder] 毎日4時の自動ビルド: {(Enabled ? "ON" : "OFF")}");
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
