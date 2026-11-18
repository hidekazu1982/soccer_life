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
                if (File.Exists(RequestPath))
                {
                    // 未コンパイルの変更があるとビルドが失敗するので、先に取り込んでコンパイルを待つ
                    if (!IsBusy) AssetDatabase.Refresh();
                    if (!IsBusy) HandleRequest();
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

            // 未コンパイルの変更を取り込む。コンパイルが始まったら次の確認まで待つ
            AssetDatabase.Refresh();
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

            int autoBump = DecideBumpFromMemo(out string reason);
            WriteStatus($"START 変更あり（例: {changed}） → {BumpLabel(autoBump)}（{reason}）");
            RunBuild(autoBump);
        }

        // ---------- バージョンの自動判定 ----------
        // CHANGES_NEXT.md（リリースノートの元になる変更メモ）を見て決める。
        //   新機能 +0.1.0 : 「追加」「新設」「導入」「新機能」を含む行がある（台詞・文言などの追加は除く）
        //   機能修正 +0.0.1 : それ以外（メモがない場合も含む）
        static readonly string[] MinorWords = { "追加", "新設", "導入", "新機能" };
        static readonly string[] MinorExemptWords = { "台詞", "セリフ", "文言", "テキスト", "パターン", "翻訳" };

        static string MemoPath => Path.Combine(ProjectRoot, "CHANGES_NEXT.md");

        static int DecideBumpFromMemo(out string reason)
        {
            if (!File.Exists(MemoPath))
            {
                reason = "変更メモなし";
                return 1;
            }

            int lines = 0;
            foreach (string raw in File.ReadAllLines(MemoPath, System.Text.Encoding.UTF8))
            {
                string line = raw.Trim().TrimStart('﻿');
                if (line.Length == 0 || line.StartsWith("#")) continue;
                lines++;

                bool hasMinor = MinorWords.Any(w => line.Contains(w));
                bool exempt = MinorExemptWords.Any(w => line.Contains(w));
                if (hasMinor && !exempt)
                {
                    string shortLine = line.Length > 40 ? line.Substring(0, 40) + "…" : line;
                    reason = $"新機能の行あり: {shortLine}";
                    return 2;
                }
            }

            reason = lines == 0 ? "変更メモなし" : $"修正・調整のみ（{lines} 行）";
            return 1;
        }

        static string BumpLabel(int bump) =>
            bump == 2 ? "新機能 +0.1.0" : bump == 0 ? "上げない" : "機能修正 +0.0.1";

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

            // 中身: auto（または空）= 変更メモから自動判定 / minor / patch / none
            int bump;
            string reason = "指定";
            if (text == "minor") bump = 2;
            else if (text == "patch") bump = 1;
            else if (text == "none") bump = 0;
            else bump = DecideBumpFromMemo(out reason);

            WriteStatus($"START 依頼ビルド（{BumpLabel(bump)}、{reason}）");
            RunBuild(bump);
        }

        // bump: 0 = 上げない、1 = +0.0.1、2 = +0.1.0
        static void RunBuild(int bump)
        {
            running = true;

            // 失敗したら番号を戻せるように覚えておく
            string prevVersion = PlayerSettings.bundleVersion;
            int prevCode = PlayerSettings.Android.bundleVersionCode;
            bool succeeded = false;

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

                succeeded = report.summary.result == BuildResult.Succeeded;
                if (succeeded)
                    WriteStatus($"SUCCESS v{PlayerSettings.bundleVersion} {OutputPath}");
                else
                    WriteStatus($"FAIL {report.summary.result} errors={report.summary.totalErrors}（番号は v{prevVersion} に戻しました）");
            }
            catch (Exception e)
            {
                WriteStatus("FAIL " + e.Message);
                Debug.LogException(e);
            }
            finally
            {
                if (!succeeded && PlayerSettings.bundleVersion != prevVersion)
                {
                    PlayerSettings.bundleVersion = prevVersion;
                    PlayerSettings.Android.bundleVersionCode = prevCode;
                    AssetDatabase.SaveAssets();
                }
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
