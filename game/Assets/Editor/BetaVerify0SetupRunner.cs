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
    // Verify-0 (docs/30 §3): reruns every editor Setup() on the committed project, one at a time, and lists the files each
    // one changes. A Setup that preserves content changes nothing. After each Setup the changed files are restored with
    // git so the next one starts from the state it found. Files that were already uncommitted are backed up first and
    // restored from that backup, so it never loses anyone's work. Retired Setups (RetiredSetup) are listed as RETIRED.
    public static class BetaVerify0SetupRunner
    {
        private const string FilterArgument = "-verify0Filter";

        [MenuItem("Trickal Fan Game/Beta/Run Verify-0 Setup Rerun Check")]
        public static void RunAll() => Run(ReadFilter(), exitWhenDone: false);

        public static void RunAllBatch() => Run(ReadFilter(), exitWhenDone: true);

        private static string GameRoot => Path.GetDirectoryName(Application.dataPath) ?? ".";

        // git paths are relative to the repository root (game/Assets/...).
        private static string RepoRoot => Path.GetFullPath(Path.Combine(GameRoot, ".."));

        private static void Run(string filter, bool exitWhenDone)
        {
            // Uncommitted files at the start are the baseline: backed up now, restored from the backup later.
            List<string> baselineEntries = ChangedFiles();
            backups.Clear();
            foreach (string entry in baselineEntries)
            {
                string full = Path.Combine(RepoRoot, entry.Substring(3));
                backups[entry.Substring(3)] = File.Exists(full) ? File.ReadAllBytes(full) : null;
            }

            Type[] setups = typeof(BetaVerify0SetupRunner).Assembly.GetTypes()
                .Where(type => type.IsClass && type.IsAbstract && type.IsSealed && type.Name.EndsWith("Setup"))
                .Where(type => SetupMethod(type) != null)
                .Where(type => string.IsNullOrEmpty(filter) || filter.Split(',').Any(part =>
                    part.Trim().Length > 0 && type.Name.IndexOf(part.Trim(), StringComparison.OrdinalIgnoreCase) >= 0))
                .OrderBy(type => type.Name, StringComparer.Ordinal)
                .ToArray();

            var results = new List<Result>();
            Stopwatch total = Stopwatch.StartNew();
            for (int i = 0; i < setups.Length; i++)
            {
                Debug.Log($"[Verify-0] Setup ({i + 1}/{setups.Length}) {setups[i].Name}");
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                string failure = null;
                bool retired = false;
                try
                {
                    SetupMethod(setups[i]).Invoke(null, null);
                    AssetDatabase.SaveAssets();
                }
                catch (Exception exception)
                {
                    Exception inner = exception is TargetInvocationException { InnerException: not null } wrapped
                        ? wrapped.InnerException
                        : exception;
                    retired = inner is RetiredSetupException;
                    failure = retired ? null : inner.GetType().Name + ": " + inner.Message;
                }

                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                List<string> changed = ChangedSinceBaseline();
                List<string> real = changed.Where(entry => !IsWhitespaceOnly(entry)).ToList();
                bool idOnly = real.Count > 0 && real.All(IsIdOnly);
                SaveDiff(setups[i].Name, real);
                Restore(changed);
                List<string> left = ChangedSinceBaseline();
                if (left.Count > 0)
                {
                    results.Add(new Result(setups[i].Name, "FAIL", left, "변경을 되돌리지 못해 검사를 멈췄다."));
                    Debug.LogError("[Verify-0] Could not restore the files changed by " + setups[i].Name + ".");
                    break;
                }

                string status = failure != null ? "FAIL" : idOnly ? "IDONLY" : real.Count > 0 ? "CHANGED" :
                    changed.Count > 0 ? "NOISE" : retired ? "RETIRED" : "KEEP";
                results.Add(new Result(setups[i].Name, status, real.Count > 0 ? real : changed, failure ?? string.Empty));
            }

            List<string> remaining = ChangedSinceBaseline();
            string path = WriteReport(results, filter, total.Elapsed.TotalSeconds, remaining);
            int notKept = results.Count(result => result.Status is "CHANGED" or "FAIL");
            Debug.Log($"[Verify-0] Setup rerun done: {results.Count - notKept} kept, {notKept} changed or failed of " +
                      $"{results.Count}. Report: {path}");
            if (exitWhenDone) EditorApplication.Exit(remaining.Count > 0 ? 3 : notKept == 0 ? 0 : 1);
        }

        private static readonly Dictionary<string, byte[]> backups = new(StringComparer.Ordinal);

        // Files whose content differs from the start: new git changes, or baseline files whose bytes changed.
        private static List<string> ChangedSinceBaseline() =>
            ChangedFiles().Where(entry =>
            {
                string file = entry.Substring(3);
                if (!backups.TryGetValue(file, out byte[] original)) return true;
                string full = Path.Combine(RepoRoot, file);
                byte[] now = File.Exists(full) ? File.ReadAllBytes(full) : null;
                return original == null ? now != null : now == null || !original.AsSpan().SequenceEqual(now);
            }).ToList();

        private static MethodInfo SetupMethod(Type type) =>
            type.GetMethod("Setup", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);

        // Changed or new files under game/ as "XY path", ignoring C# scripts (Setups never write them) and logs.
        private static List<string> ChangedFiles()
        {
            string output = Git("status --porcelain=v1 -z -uall -- game");
            return output.Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(entry => entry.Length > 3)
                .Where(entry => !entry.EndsWith(".cs") && !entry.EndsWith(".cs.meta") &&
                                !entry.Substring(3).StartsWith("game/Logs/"))
                .ToList();
        }

        // Unity sometimes rewrites a file with only trailing-space differences (e.g. `customData: `).
        private static bool IsWhitespaceOnly(string entry)
        {
            if (entry.StartsWith("??") || entry.Substring(0, 2).Contains('D') || backups.ContainsKey(entry.Substring(3)))
                return false;
            string numstat = Git("diff -w --numstat -- " + Quote(entry.Substring(3))).Trim();
            return numstat.Length == 0 || numstat.StartsWith("0\t0\t");
        }

        // Keeps what each Setup changed in Logs/Verify0/setup-diffs/<Setup>.diff for review.
        private static void SaveDiff(string setupName, List<string> real)
        {
            string folder = Path.Combine(GameRoot, BetaVerify0Runner.ReportFolder, "setup-diffs");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, setupName + ".diff");
            string[] tracked = real.Where(entry => !entry.StartsWith("??") && !backups.ContainsKey(entry.Substring(3)))
                .Select(entry => Quote(entry.Substring(3))).ToArray();
            if (tracked.Length == 0)
            {
                if (File.Exists(path)) File.Delete(path);
                return;
            }

            File.WriteAllText(path, Git("diff -w -- " + string.Join(" ", tracked)), new UTF8Encoding(false));
        }

        // Unity recreated objects with new fileIDs, reordered them or changed indentation, but every object reads the
        // same once IDs and surrounding spaces are hidden.
        private static bool IsIdOnly(string entry)
        {
            if (entry.StartsWith("??") || entry.Substring(0, 2).Contains('D')) return false;
            string file = entry.Substring(3);
            string full = Path.Combine(RepoRoot, file);
            string before = backups.TryGetValue(file, out byte[] original)
                ? original == null ? null : Encoding.UTF8.GetString(original)
                : Git("show HEAD:" + Quote(file));
            return before != null && File.Exists(full) &&
                   NormalizedObjects(before).SequenceEqual(NormalizedObjects(File.ReadAllText(full, Encoding.UTF8)));
        }

        private static IEnumerable<string> NormalizedObjects(string yaml) =>
            yaml.Split(new[] { "--- !u!" }, StringSplitOptions.None)
                .Select(block => System.Text.RegularExpressions.Regex.Replace(block, @"(fileID: |&)-?\d+", "$1N"))
                .Select(block => string.Join("\n", block.Split('\n').Select(line => line.Trim())))
                .OrderBy(block => block, StringComparer.Ordinal);

        private static void Restore(List<string> changed)
        {
            if (changed.Count == 0) return;
            foreach (string entry in changed)
            {
                string file = entry.Substring(3);
                if (backups.TryGetValue(file, out byte[] original))
                {
                    string full = Path.Combine(RepoRoot, file);
                    if (original != null) File.WriteAllBytes(full, original);
                    else if (File.Exists(full)) File.Delete(full);
                }
                else if (entry.StartsWith("??"))
                {
                    string full = Path.Combine(RepoRoot, file);
                    if (File.Exists(full)) File.Delete(full);
                    RemoveEmptyFolders(Path.GetDirectoryName(full));
                }
                else
                {
                    Git("checkout -- " + Quote(file));
                }
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static void RemoveEmptyFolders(string folder)
        {
            string assets = Path.GetFullPath(Application.dataPath);
            while (!string.IsNullOrEmpty(folder) && Path.GetFullPath(folder).StartsWith(assets) &&
                   Path.GetFullPath(folder) != assets && Directory.Exists(folder) &&
                   !Directory.EnumerateFileSystemEntries(folder).Any())
            {
                Directory.Delete(folder);
                string meta = folder.TrimEnd('/', '\\') + ".meta";
                string relative = Path.GetRelativePath(RepoRoot, meta).Replace('\\', '/');
                if (File.Exists(meta) && Git("ls-files -- " + Quote(relative)).Length == 0) File.Delete(meta);
                folder = Path.GetDirectoryName(folder);
            }
        }

        private static string Quote(string path) => "\"" + path + "\"";

        private static string Git(string arguments)
        {
            var start = new ProcessStartInfo("git", "-c core.quotepath=off " + arguments)
            {
                WorkingDirectory = RepoRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
            };
            using Process process = Process.Start(start);
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0) throw new InvalidOperationException($"git {arguments} failed: {error}");
            return output;
        }

        private static string WriteReport(List<Result> results, string filter, double seconds, List<string> left)
        {
            string folder = Path.Combine(GameRoot, BetaVerify0Runner.ReportFolder);
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "setup-rerun.md");
            var text = new StringBuilder();
            text.AppendLine("# Verify-0 Setup 재실행 보존 결과");
            text.AppendLine();
            text.AppendLine($"- 실행: {DateTime.Now:yyyy-MM-dd HH:mm}, {seconds:0}초");
            if (!string.IsNullOrEmpty(filter)) text.AppendLine($"- 필터: `{filter}`");
            text.AppendLine("- KEEP: 바뀐 파일 없음, RETIRED: 은퇴해 실행 안 함, NOISE: 공백만 바뀜, IDONLY: 내부 ID·순서만 바뀜, CHANGED: 내용이 바뀜, FAIL: 예외");
            foreach (string status in new[] { "KEEP", "RETIRED", "NOISE", "IDONLY", "CHANGED", "FAIL" })
                text.AppendLine($"- {status}: {results.Count(result => result.Status == status)}");
            if (left.Count > 0) text.AppendLine($"- 되돌리지 못한 파일: {string.Join(", ", left)}");
            text.AppendLine();
            text.AppendLine("| Setup | 결과 | 바뀐 파일 | 메시지 |");
            text.AppendLine("| --- | --- | --- | --- |");
            foreach (Result result in results.OrderBy(result => result.Status is "KEEP" or "NOISE" or "RETIRED" or "IDONLY")
                         .ThenBy(result => result.Status).ThenBy(result => result.Name))
            {
                string files = string.Join("<br>", result.Files.Take(8).Select(file => "`" + file + "`")) +
                               (result.Files.Count > 8 ? $"<br>외 {result.Files.Count - 8}개" : string.Empty);
                string detail = result.Detail.Split('\n')[0].Replace("|", "\\|");
                text.AppendLine($"| {result.Name} | {result.Status} | {files} | {detail} |");
            }

            File.WriteAllText(path, text.ToString(), new UTF8Encoding(false));
            return path;
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
            public readonly List<string> Files;
            public readonly string Detail;

            public Result(string name, string status, List<string> files, string detail)
            {
                Name = name;
                Status = status;
                Files = files;
                Detail = detail;
            }
        }
    }
}
