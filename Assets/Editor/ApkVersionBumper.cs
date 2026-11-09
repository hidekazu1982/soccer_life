#if UNITY_EDITOR
using System;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SoccerClubSim.EditorTools
{
    /// <summary>
    /// Android ビルドの直前に、バージョン（メジャー.マイナー.パッチ）を自動で上げる。
    ///   リリース : +1.0.0（開発中は使わない。上げたいときは Player Settings で手動）
    ///   新機能   : +0.1.0
    ///   機能修正 : +0.0.1
    /// ・Build Profiles から手動でビルドしたとき : ダイアログで「機能修正 / 新機能 / 上げない」を選ぶ
    /// ・AndroidApkBuild.cs やバッチモードの自動ビルド : ダイアログなしで +0.0.1
    /// Android の versionCode もバージョンから計算して設定する（必ず増える）。
    /// </summary>
    public class ApkVersionBumper : IPreprocessBuildWithReport
    {
        // 「1.0」など 3 桁でない値だったときに使う基準バージョン
        const string BaseVersion = "0.1.0";

        // ApkDriveUploader（1000）より先に動かす
        public int callbackOrder => -100;

        enum Bump { None, Patch, Minor }

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android) return;

            string current = PlayerSettings.bundleVersion;
            if (!TryParse(current, out int major, out int minor, out int patch))
            {
                TryParse(BaseVersion, out major, out minor, out patch);
                Debug.Log($"[ApkVersionBumper] バージョン「{current}」を基準の {BaseVersion} に置き換えます。");
            }

            Bump bump = IsAutomatedBuild() ? Bump.Patch : AskBump(major, minor, patch);

            switch (bump)
            {
                case Bump.Minor:
                    minor++;
                    patch = 0;
                    break;
                case Bump.Patch:
                    patch++;
                    break;
            }

            string next = $"{major}.{minor}.{patch}";
            int versionCode = major * 1000000 + minor * 1000 + patch;

            PlayerSettings.bundleVersion = next;
            PlayerSettings.Android.bundleVersionCode = versionCode;
            AssetDatabase.SaveAssets();

            string label = bump == Bump.Minor ? "新機能 +0.1.0" : bump == Bump.Patch ? "機能修正 +0.0.1" : "上げない";
            Debug.Log($"[ApkVersionBumper] {current} → {next}（{label}） versionCode={versionCode}");
        }

        static Bump AskBump(int major, int minor, int patch)
        {
            string fix = $"{major}.{minor}.{patch + 1}";
            string feature = $"{major}.{minor + 1}.0";
            string same = $"{major}.{minor}.{patch}";

            // 0 = ok, 1 = cancel（Esc・×ボタンもこれ）, 2 = alt
            int choice = EditorUtility.DisplayDialogComplex(
                "APK のバージョン",
                $"今のバージョン: {same}\n\n今回のビルドはどれですか？",
                $"機能修正（{fix}）",
                $"上げない（{same}）",
                $"新機能（{feature}）");

            switch (choice)
            {
                case 0: return Bump.Patch;
                case 2: return Bump.Minor;
                default: return Bump.None;
            }
        }

        // 画面操作なしで動くビルド（AndroidApkBuild.cs 経由・バッチモード）か
        static bool IsAutomatedBuild()
        {
            if (Application.isBatchMode) return true;
            return Environment.StackTrace.Contains("AndroidApkBuild");
        }

        static bool TryParse(string version, out int major, out int minor, out int patch)
        {
            major = minor = patch = 0;
            if (string.IsNullOrEmpty(version)) return false;

            var m = Regex.Match(version.Trim(), @"^(\d+)\.(\d+)\.(\d+)$");
            if (!m.Success) return false;

            major = int.Parse(m.Groups[1].Value);
            minor = int.Parse(m.Groups[2].Value);
            patch = int.Parse(m.Groups[3].Value);
            return true;
        }
    }
}
#endif
