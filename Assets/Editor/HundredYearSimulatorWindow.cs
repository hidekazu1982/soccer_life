using System;
using System.Diagnostics;
using System.IO;
using SoccerClub.Core;
using SoccerClub.World;
using UnityEditor;
using UnityEngine;

namespace SoccerClub.EditorTools
{
    /// <summary>
    /// 画面を通さず、ゲーム内の処理だけで100年分を回すシミュレーター。
    /// メニュー SoccerClub/100年シミュレーター から開く。結果はプロジェクト直下の SimulationResults/ に CSV で出す。
    /// バッチ実行: Unity.exe -batchmode -quit -projectPath C:\project\Soccer_life
    ///   -executeMethod SoccerClub.EditorTools.HundredYearSimulatorWindow.RunFromCommandLine [-simClub 5] [-simSeed 1] [-simYears 100]
    /// </summary>
    public sealed class HundredYearSimulatorWindow : EditorWindow
    {
        const double FrameBudgetMs = 200d;

        int _clubIndex;
        int _seasons = 100;
        bool _useSeed = true;
        int _seed = 1;
        bool _fillEmptyFront = true;

        HeadlessSimulator _sim;
        Stopwatch _watch;
        string _summary = "";
        string _lastPath = "";
        Vector2 _scroll;

        [MenuItem("SoccerClub/100年シミュレーター")]
        static void Open()
        {
            GetWindow<HundredYearSimulatorWindow>("100年シミュレーター");
        }

        void OnDisable()
        {
            Stop();
        }

        void OnGUI()
        {
            bool running = _sim != null;
            using (new EditorGUI.DisabledScope(running))
            {
                var names = WorldFactory.ClubNames;
                if (names != null && names.Length > 0)
                    _clubIndex = EditorGUILayout.Popup("自クラブ", Mathf.Clamp(_clubIndex, 0, names.Length - 1), names);
                else
                    _clubIndex = EditorGUILayout.IntField("自クラブ番号", _clubIndex);
                _seasons = Mathf.Clamp(EditorGUILayout.IntField("シーズン数", _seasons), 1, 1000);
                _useSeed = EditorGUILayout.Toggle("シードを固定", _useSeed);
                if (_useSeed) _seed = EditorGUILayout.IntField("シード", _seed);
                _fillEmptyFront = EditorGUILayout.Toggle(new GUIContent("フロント空き枠も採用",
                    "空き枠を「レベル0のスタッフがいる」とみなし、資金が総年俸を下回らなければ採用する。"), _fillEmptyFront);
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "・資金が総年俸を下回るときは施設をレベルアップしない\n"
                + "・レベルアップ後に資金が総年俸を下回るならレベルアップしない\n"
                + "・フロントは所属より高レベルの候補がいて、入れ替え後も資金が総年俸を下回らないときに入れ替える",
                MessageType.None);

            if (!running)
            {
                if (GUILayout.Button("実行", GUILayout.Height(28f))) Begin();
            }
            else
            {
                var sim = _sim;
                float progress = sim.Settings.Seasons > 0 ? (float)sim.Records.Count / sim.Settings.Seasons : 0f;
                var rect = GUILayoutUtility.GetRect(18f, 18f, GUILayout.ExpandWidth(true));
                EditorGUI.ProgressBar(rect, progress,
                    sim.Records.Count + " / " + sim.Settings.Seasons + " シーズン  "
                    + sim.Session.Time.CurrentDate.ToString("yyyy/MM/dd"));
                if (GUILayout.Button("中止して途中までを保存")) Finish();
            }

            if (!string.IsNullOrEmpty(_summary))
            {
                EditorGUILayout.Space();
                _scroll = EditorGUILayout.BeginScrollView(_scroll);
                EditorGUILayout.TextArea(_summary);
                EditorGUILayout.EndScrollView();
            }

            if (!string.IsNullOrEmpty(_lastPath) && GUILayout.Button("結果フォルダを開く"))
                EditorUtility.RevealInFinder(_lastPath);
        }

        void Begin()
        {
            _summary = "";
            _lastPath = "";
            try
            {
                _sim = new HeadlessSimulator(MakeSettings(_clubIndex, _useSeed ? (int?)_seed : null, _seasons, _fillEmptyFront));
            }
            catch (Exception e)
            {
                _sim = null;
                _summary = "開始できませんでした: " + e.Message;
                UnityEngine.Debug.LogException(e);
                return;
            }

            _watch = Stopwatch.StartNew();
            EditorApplication.update += Tick;
        }

        void Tick()
        {
            if (_sim == null)
            {
                Stop();
                return;
            }

            var frame = Stopwatch.StartNew();
            try
            {
                while (frame.Elapsed.TotalMilliseconds < FrameBudgetMs)
                {
                    if (!_sim.StepDay()) break;
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogException(e);
                _summary = "途中でエラー: " + e.Message + "\n" + _sim.Session.Time.CurrentDate.ToString("yyyy/MM/dd");
                Finish();
                return;
            }

            if (_sim.Finished) Finish();
            Repaint();
        }

        void Finish()
        {
            if (_sim == null) return;
            var sim = _sim;
            Stop();
            var elapsed = _watch != null ? _watch.Elapsed : TimeSpan.Zero;
            _lastPath = SaveCsv(sim);
            _summary = (string.IsNullOrEmpty(_summary) ? "" : _summary + "\n")
                + sim.BuildSummary()
                + "所要 " + elapsed.TotalSeconds.ToString("0.0") + " 秒\n"
                + "保存先 " + _lastPath;
            UnityEngine.Debug.Log("SoccerClub simulator:\n" + _summary);
            Repaint();
        }

        void Stop()
        {
            EditorApplication.update -= Tick;
            _sim = null;
        }

        internal static SimulationSettings MakeSettings(int club, int? seed, int seasons, bool fillEmpty)
        {
            return new SimulationSettings
            {
                PlayerClubIndex = club,
                Seed = seed,
                Seasons = seasons,
                FillEmptyFront = fillEmpty
            };
        }

        internal static string SaveCsv(HeadlessSimulator sim)
        {
            var dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "SimulationResults");
            Directory.CreateDirectory(dir);
            var s = sim.Settings;
            var name = "sim_" + DateTime.Now.ToString("yyyyMMdd_HHmmss")
                + "_club" + s.PlayerClubIndex
                + (s.Seed.HasValue ? "_seed" + s.Seed.Value : "")
                + (Math.Abs(s.LeagueStep - GameSession.DefaultLeagueLevelStep) > 1e-9
                    ? "_step" + s.LeagueStep.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) : "")
                + ".csv";
            var path = Path.Combine(dir, name);
            // Excel で文字化けしないよう BOM 付き UTF-8。
            File.WriteAllText(path, sim.BuildCsv(), new System.Text.UTF8Encoding(true));
            return path;
        }

        /// <summary>-batchmode -executeMethod から呼ぶ。</summary>
        public static void RunFromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            int club = ArgInt(args, "-simClub", 0);
            int years = ArgInt(args, "-simYears", 100);
            int seedValue = ArgInt(args, "-simSeed", int.MinValue);
            int? seed = seedValue == int.MinValue ? (int?)null : seedValue;
            bool fillEmpty = ArgInt(args, "-simFillEmpty", 1) != 0;

            var watch = Stopwatch.StartNew();
            var sim = new HeadlessSimulator(MakeSettings(club, seed, years, fillEmpty));
            int lastLogged = 0;
            while (sim.StepDay())
            {
                if (sim.Records.Count != lastLogged && sim.Records.Count % 10 == 0)
                {
                    lastLogged = sim.Records.Count;
                    UnityEngine.Debug.Log("SoccerClub simulator: " + lastLogged + " seasons");
                }
            }

            var path = SaveCsv(sim);
            UnityEngine.Debug.Log("SoccerClub simulator done (" + watch.Elapsed.TotalSeconds.ToString("0.0") + "s)\n"
                + sim.BuildSummary() + path);
        }

        static int ArgInt(string[] args, string key, int fallback)
        {
            for (int i = 0; i + 1 < args.Length; i++)
            {
                if (args[i] == key && int.TryParse(args[i + 1], out var value)) return value;
            }

            return fallback;
        }
    }
}
