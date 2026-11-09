using System;
using System.Diagnostics;
using System.IO;
using SoccerClub.Core;
using UnityEditor;
using UnityEngine;

namespace SoccerClub.EditorTools
{
    /// <summary>
    /// エディタを直接操作できないとき用。プロジェクト直下の SimulationResults/run.request を置くと、
    /// エディタがシミュレーターを実行し、進み具合を SimulationResults/status.txt に書く。
    /// run.request の中身（省略可、1行に key=value）: club=0 / seed=1 / years=100 / fillEmpty=1
    /// </summary>
    [InitializeOnLoad]
    static class SimulatorFileTrigger
    {
        const double PollSeconds = 2d;
        const double FrameBudgetMs = 200d;

        static double _nextPoll;
        static HeadlessSimulator _sim;
        static Stopwatch _watch;
        static int _lastStatusSeason = -1;
        static int _club;
        static int _years;
        static bool _fill;
        static int? _nextSeed;
        static int _runsLeft;
        static readonly System.Text.StringBuilder _log = new System.Text.StringBuilder();

        static string Dir => Path.Combine(Directory.GetParent(Application.dataPath).FullName, "SimulationResults");
        static string RequestPath => Path.Combine(Dir, "run.request");
        static string StatusPath => Path.Combine(Dir, "status.txt");

        static SimulatorFileTrigger()
        {
            EditorApplication.update += Update;
        }

        static void Update()
        {
            if (_sim != null)
            {
                Step();
                return;
            }

            if (EditorApplication.timeSinceStartup < _nextPoll) return;
            _nextPoll = EditorApplication.timeSinceStartup + PollSeconds;
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!File.Exists(RequestPath)) return;
            Begin();
        }

        static void Begin()
        {
            int club = 0, years = 100, fill = 1, runs = 1;
            int? seed = null;
            try
            {
                foreach (var raw in File.ReadAllLines(RequestPath))
                {
                    var line = raw.Trim();
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    var key = line.Substring(0, eq).Trim().ToLowerInvariant();
                    int value;
                    if (!int.TryParse(line.Substring(eq + 1).Trim(), out value)) continue;
                    if (key == "club") club = value;
                    else if (key == "seed") seed = value;
                    else if (key == "years") years = value;
                    else if (key == "fillempty") fill = value;
                    else if (key == "runs") runs = value;
                }

                File.Delete(RequestPath);
                _club = club;
                _years = years;
                _fill = fill != 0;
                _nextSeed = seed;
                _runsLeft = Math.Max(1, runs);
                _log.Length = 0;
                StartNext();
            }
            catch (Exception e)
            {
                _sim = null;
                WriteStatus("error at start: " + e);
                UnityEngine.Debug.LogException(e);
            }
        }

        static void StartNext()
        {
            _runsLeft--;
            _sim = new HeadlessSimulator(HundredYearSimulatorWindow.MakeSettings(_club, _nextSeed, _years, _fill));
            if (_nextSeed.HasValue) _nextSeed = _nextSeed.Value + 1;
            _watch = Stopwatch.StartNew();
            _lastStatusSeason = -1;
            WriteStatus(_log + "running 0/" + _years);
        }

        static void Finish(string text)
        {
            _log.AppendLine(text);
            _sim = null;
            if (_runsLeft > 0)
            {
                StartNext();
                return;
            }

            WriteStatus(_log + "all done");
        }

        static void Step()
        {
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
                var path = HundredYearSimulatorWindow.SaveCsv(_sim);
                UnityEngine.Debug.LogException(e);
                Finish("error seed=" + _sim.Settings.Seed + " " + _sim.Session.Time.CurrentDate.ToString("yyyy/MM/dd") + "\n" + e + "\npartial csv: " + path);
                return;
            }

            if (_sim.Finished)
            {
                var path = HundredYearSimulatorWindow.SaveCsv(_sim);
                Finish("done seed=" + _sim.Settings.Seed + " " + _watch.Elapsed.TotalSeconds.ToString("0.0") + "s\n" + _sim.BuildSummary() + "csv: " + path);
                return;
            }

            if (_sim.Records.Count != _lastStatusSeason)
            {
                _lastStatusSeason = _sim.Records.Count;
                WriteStatus(_log + "running seed=" + _sim.Settings.Seed + " " + _sim.Records.Count + "/" + _sim.Settings.Seasons + "  "
                    + _sim.Session.Time.CurrentDate.ToString("yyyy/MM/dd") + "  "
                    + _watch.Elapsed.TotalSeconds.ToString("0") + "s");
            }
        }

        static void WriteStatus(string text)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllText(StatusPath, text);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning("simulator status write failed: " + e.Message);
            }
        }
    }
}
