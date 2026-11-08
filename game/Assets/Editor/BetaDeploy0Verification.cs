using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using TrickalFanGame.Network;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    // Deploy-0 (docs/30 §3.1 S6): the Editor talks to the local server, player builds to the production server,
    // and `-apiUrl` overrides both. Also checks that the production URL follows the Render service in render.yaml.
    public static class BetaDeploy0Verification
    {
        private const string ToggleMenu = "Trickal Fan Game/Beta/Use Production API in Editor";

        [MenuItem(ToggleMenu, priority = 200)]
        private static void ToggleProductionApi()
        {
            bool next = !EditorPrefs.GetBool(ApiEndpoint.EditorUseProductionKey, false);
            EditorPrefs.SetBool(ApiEndpoint.EditorUseProductionKey, next);
            Debug.Log(next
                ? $"Editor Play now uses the production API ({ApiEndpoint.ProductionBaseUrl}). " +
                  "Use DEV: RESET LOCAL PROFILE → NEW PLAYER so local-server profiles are not mixed in."
                : "Editor Play now uses the local API (ApiClient baseUrl).");
        }

        [MenuItem(ToggleMenu, true)]
        private static bool ToggleProductionApiValidate()
        {
            Menu.SetChecked(ToggleMenu, EditorPrefs.GetBool(ApiEndpoint.EditorUseProductionKey, false));
            return true;
        }

        [MenuItem("Trickal Fan Game/Beta/Verify Deploy-0 API Endpoint")]
        public static void Verify()
        {
            const string local = "http://localhost:3001/api";
            string production = ApiEndpoint.ProductionBaseUrl.TrimEnd('/');
            string[] noArgs = { "Game.exe" };

            Expect(ApiEndpoint.Resolve(local + "/", noArgs, true, false), local, "Editor uses the serialized URL");
            Expect(ApiEndpoint.Resolve("", noArgs, true, false), ApiEndpoint.LocalBaseUrl,
                "Editor falls back to the local URL");
            Expect(ApiEndpoint.Resolve(local, noArgs, true, true), production, "Editor toggle uses production");
            Expect(ApiEndpoint.Resolve(local, noArgs, false, false), production,
                "player builds ignore the serialized localhost URL");
            Expect(ApiEndpoint.Resolve(local, new[] { "Game.exe", "-apiUrl", "http://10.0.0.2:3001/api/" }, false, false),
                "http://10.0.0.2:3001/api", "-apiUrl <url> overrides a player build");
            Expect(ApiEndpoint.Resolve(local, new[] { "Game.exe", "-apiUrl=https://staging.example/api" }, true, true),
                "https://staging.example/api", "-apiUrl=<url> overrides the Editor toggle");
            Expect(ApiEndpoint.Resolve(local, new[] { "Game.exe", "-apiUrl", "-batchmode" }, false, false), production,
                "a missing -apiUrl value is ignored");
            Assert(ApiEndpoint.IsProduction(production + "/"), "IsProduction must match the production URL.");

            Assert(production.StartsWith("https://", StringComparison.Ordinal),
                $"The production URL must use HTTPS (player builds block plain HTTP): {production}");
            Assert(production.EndsWith("/api", StringComparison.Ordinal),
                $"The production URL must include the Backend global prefix /api: {production}");

            string renderPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "render.yaml"));
            Assert(File.Exists(renderPath), $"render.yaml is missing at {renderPath}.");
            Match service = Regex.Match(File.ReadAllText(renderPath, Encoding.UTF8), @"^\s*name:\s*(\S+)",
                RegexOptions.Multiline);
            Assert(service.Success, "render.yaml has no service name.");
            string expectedHost = $"https://{service.Groups[1].Value}";
            Assert(production.StartsWith(expectedHost, StringComparison.OrdinalIgnoreCase),
                $"ProductionBaseUrl '{production}' must point at the render.yaml service '{service.Groups[1].Value}'.");

            string clientPath = Path.Combine(Application.dataPath, "Scripts", "Network", "ApiClient.cs");
            string client = File.ReadAllText(clientPath, Encoding.UTF8);
            Assert(!client.Contains("{baseUrl}"),
                "ApiClient must build request URLs from BaseUrl (resolved), not the serialized baseUrl.");

            Debug.Log("Beta Deploy-0 verification passed: Editor → local, player build → " + production +
                      ", -apiUrl override, render.yaml service match.");
        }

        private static void Expect(string actual, string expected, string label)
        {
            Assert(actual == expected, $"{label}: expected '{expected}', got '{actual}'.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException($"Beta Deploy-0 verification failed: {message}");
            }
        }
    }
}
