using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace TrickalFanGame.Editor
{
    // Verify-0 (docs/30 §3): runs every editor verifier's Verify() one by one and writes a pass/fail list.
    // Each verifier starts from an empty scene so the order does not change the result. A verifier passes only if it
    // returns without throwing and without logging an error or exception.
    public static class BetaVerify0Runner
    {
        public const string ReportFolder = "Logs/Verify0";
        // Comma-separated parts of verifier class names, e.g. -verify0Filter UserArtwork,Week16Reward0
        private const string FilterArgument = "-verify0Filter";

        // Verifiers that read the active scene instead of opening one. Everything else starts from an empty scene.
        private static readonly Dictionary<string, string> RequiredScenes = new()
        {
            ["Week13FrontendVerification"] = Week13FrontendSetup.ScenePath,
            ["Week7HighGradeSkillVerification"] = Week13FrontendSetup.GameScenePath,
            ["Week7ChaserMonsterVerification"] = Week13FrontendSetup.GameScenePath,
        };

        // Error logs a verifier causes on purpose. Any other error log fails the verifier.
        private static readonly Dictionary<string, string[]> ExpectedErrors = new()
        {
            // Checks that PlayerInventory rejects single-use spells.
            ["Week21Slot0Verification"] = new[] { "must be held in the spell slot." },
            // Checks that PlayerInventory rejects 권능.
            ["Week27Power1Verification"] = new[] { "must be held in the 권능 slot." },
            // Sets EventSystem.current in Edit Mode, where the EventSystem is not registered yet.
            ["Week16Reward2Verification"] = new[] { "Failed setting EventSystem.current to unknown EventSystem" },
            ["Week13Hud7BVerification"] = new[] { "Failed setting EventSystem.current to unknown EventSystem" },
        };

        [MenuItem("Trickal Fan Game/Beta/Run Verify-0 All Verifiers")]
        public static void RunAll() => Run(ReadFilter(), exitWhenDone: false);

        public static void RunAllBatch() => Run(ReadFilter(), exitWhenDone: true);

        public static Type[] FindVerifiers(string filter) =>
            typeof(BetaVerify0Runner).Assembly.GetTypes()
                .Where(type => type.IsClass && type.IsAbstract && type.IsSealed && type.Name.EndsWith("Verification"))
                .Where(type => VerifyMethod(type) != null)
                .Where(type => string.IsNullOrEmpty(filter) || filter.Split(',').Any(part =>
                    part.Trim().Length > 0 && type.Name.IndexOf(part.Trim(), StringComparison.OrdinalIgnoreCase) >= 0))
                .OrderBy(type => type.Name, StringComparer.Ordinal)
                .ToArray();

        private static MethodInfo VerifyMethod(Type type) =>
            type.GetMethod("Verify", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);

        private static void Run(string filter, bool exitWhenDone)
        {
            Type[] verifiers = FindVerifiers(filter);
            var results = new List<Result>();
            Stopwatch total = Stopwatch.StartNew();
            for (int i = 0; i < verifiers.Length; i++)
            {
                Debug.Log($"[Verify-0] ({i + 1}/{verifiers.Length}) {verifiers[i].Name}");
                results.Add(RunOne(verifiers[i]));
            }

            string path = WriteReport(results, filter, total.Elapsed.TotalSeconds);
            int failed = results.Count(result => result.Status != "PASS");
            Debug.Log($"[Verify-0] Done: {results.Count - failed} passed, {failed} failed of {results.Count}. Report: {path}");
            if (exitWhenDone) EditorApplication.Exit(failed == 0 ? 0 : 1);
        }

        private static Result RunOne(Type type)
        {
            var errors = new List<string>();
            string[] expected = ExpectedErrors.TryGetValue(type.Name, out string[] parts) ? parts : Array.Empty<string>();
            Application.LogCallback capture = (message, stackTrace, logType) =>
            {
                if (logType != LogType.Error && logType != LogType.Exception && logType != LogType.Assert) return;
                if (logType == LogType.Error && expected.Any(part => message.Contains(part))) return;
                errors.Add(message);
            };

            if (RequiredScenes.TryGetValue(type.Name, out string scenePath))
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            else
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Time.timeScale = 1f;
            Stopwatch watch = Stopwatch.StartNew();
            string failure = null;
            Application.logMessageReceived += capture;
            try
            {
                VerifyMethod(type).Invoke(null, null);
            }
            catch (TargetInvocationException exception)
            {
                Exception inner = exception.InnerException ?? exception;
                failure = inner.GetType().Name + ": " + inner.Message;
            }
            catch (Exception exception)
            {
                failure = exception.GetType().Name + ": " + exception.Message;
            }
            finally
            {
                Application.logMessageReceived -= capture;
                Time.timeScale = 1f;
            }

            string status = failure != null ? "FAIL" : errors.Count > 0 ? "ERRLOG" : "PASS";
            string detail = failure ?? errors.FirstOrDefault() ?? string.Empty;
            return new Result(type.Name, status, watch.Elapsed.TotalSeconds, detail);
        }

        private static string WriteReport(List<Result> results, string filter, double seconds)
        {
            string folder = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", ReportFolder);
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "verify-all.md");

            var text = new StringBuilder();
            text.AppendLine("# Verify-0 전체 검증 결과");
            text.AppendLine();
            text.AppendLine($"- 실행: {DateTime.Now:yyyy-MM-dd HH:mm}, {seconds:0}초");
            if (!string.IsNullOrEmpty(filter)) text.AppendLine($"- 필터: `{filter}`");
            foreach (string status in new[] { "PASS", "FAIL", "ERRLOG" })
                text.AppendLine($"- {status}: {results.Count(result => result.Status == status)}");
            text.AppendLine();
            text.AppendLine("| 검증기 | 결과 | 초 | 메시지 |");
            text.AppendLine("| --- | --- | --- | --- |");
            foreach (Result result in results.OrderBy(result => result.Status == "PASS").ThenBy(result => result.Name))
                text.AppendLine($"| {result.Name} | {result.Status} | {result.Seconds:0.0} | {Cell(result.Detail)} |");

            File.WriteAllText(path, text.ToString(), new UTF8Encoding(false));
            return path;
        }

        private static string Cell(string value)
        {
            string line = (value ?? string.Empty).Split('\n')[0].Trim().Replace("|", "\\|");
            return line.Length > 300 ? line.Substring(0, 300) + "…" : line;
        }

        private static string ReadFilter()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, FilterArgument);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }

        private readonly struct Result
        {
            public readonly string Name;
            public readonly string Status;
            public readonly double Seconds;
            public readonly string Detail;

            public Result(string name, string status, double seconds, string detail)
            {
                Name = name;
                Status = status;
                Seconds = seconds;
                Detail = detail;
            }
        }
    }
}
