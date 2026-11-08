using System;
using UnityEngine;

namespace TrickalFanGame.Network
{
    // Deploy-0 (docs/30 §3.1 S6): which Backend the game talks to.
    // Order: `-apiUrl <url>` command-line override → Editor (local server, or production when the Editor menu
    // toggle is on) → player builds always use the production server.
    public static class ApiEndpoint
    {
        public const string LocalBaseUrl = "http://localhost:3001/api";
        // The Render service URL from render.yaml (`trickal-fan-game-api`). Update it if Render assigns another name.
        public const string ProductionBaseUrl = "https://trickal-fan-game-api.onrender.com/api";
        public const string CommandLineArgument = "-apiUrl";
        public const string EditorUseProductionKey = "TrickalFanGame.Network.UseProductionApiInEditor";

        public static string ResolveCurrent(string serializedUrl)
        {
#if UNITY_EDITOR
            bool isEditor = true;
            bool editorUseProduction = UnityEditor.EditorPrefs.GetBool(EditorUseProductionKey, false);
#else
            bool isEditor = false;
            bool editorUseProduction = false;
#endif
            return Resolve(serializedUrl, Environment.GetCommandLineArgs(), isEditor, editorUseProduction);
        }

        public static string Resolve(string serializedUrl, string[] commandLineArgs, bool isEditor,
            bool editorUseProduction)
        {
            string overrideUrl = FindCommandLineOverride(commandLineArgs);
            if (overrideUrl != null)
            {
                return Normalize(overrideUrl);
            }

            if (isEditor && !editorUseProduction)
            {
                return Normalize(string.IsNullOrWhiteSpace(serializedUrl) ? LocalBaseUrl : serializedUrl);
            }

            return Normalize(ProductionBaseUrl);
        }

        public static bool IsProduction(string resolvedUrl)
        {
            return string.Equals(Normalize(resolvedUrl), Normalize(ProductionBaseUrl), StringComparison.OrdinalIgnoreCase);
        }

        private static string FindCommandLineOverride(string[] args)
        {
            if (args == null)
            {
                return null;
            }

            string prefix = CommandLineArgument + "=";
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (string.IsNullOrEmpty(arg))
                {
                    continue;
                }

                if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    string value = arg.Substring(prefix.Length);
                    return string.IsNullOrWhiteSpace(value) ? null : value;
                }

                if (string.Equals(arg, CommandLineArgument, StringComparison.OrdinalIgnoreCase) &&
                    i + 1 < args.Length && !string.IsNullOrWhiteSpace(args[i + 1]) && !args[i + 1].StartsWith("-"))
                {
                    return args[i + 1];
                }
            }

            return null;
        }

        private static string Normalize(string url)
        {
            return (url ?? string.Empty).Trim().TrimEnd('/');
        }
    }
}
