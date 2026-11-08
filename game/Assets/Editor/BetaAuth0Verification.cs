using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using TrickalFanGame.Network;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    // Auth-0 (docs/30 §3.1 S1): write requests carry the device key. Checks that the Unity header name matches the
    // Backend contract and that both write requests (Run save, skill upgrade) send it.
    public static class BetaAuth0Verification
    {
        [MenuItem("Trickal Fan Game/Beta/Verify Auth-0 Profile Key")]
        public static void Verify()
        {
            // contract-drift-check: backend/src/common/auth/client-profile-key.ts CLIENT_PROFILE_KEY_HEADER.
            string backendPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "backend", "src",
                "common", "auth", "client-profile-key.ts"));
            Assert(File.Exists(backendPath), $"The Backend profile key contract is missing at {backendPath}.");
            Match match = Regex.Match(File.ReadAllText(backendPath, Encoding.UTF8),
                @"CLIENT_PROFILE_KEY_HEADER\s*=\s*'([^']+)'");
            Assert(match.Success, "CLIENT_PROFILE_KEY_HEADER was not found in the Backend contract.");
            Assert(string.Equals(match.Groups[1].Value, ApiClient.ProfileKeyHeader, StringComparison.OrdinalIgnoreCase),
                $"Unity header '{ApiClient.ProfileKeyHeader}' must match the Backend header '{match.Groups[1].Value}'.");

            string clientPath = Path.Combine(Application.dataPath, "Scripts", "Network", "ApiClient.cs");
            string client = File.ReadAllText(clientPath, Encoding.UTF8);
            int sent = Regex.Matches(client, @"SetRequestHeader\(ProfileKeyHeader,\s*LocalProfile\.ClientProfileId\)").Count;
            Assert(sent == 2, $"Run save and skill upgrade must both send the profile key; found {sent} request(s).");

            Debug.Log("Beta Auth-0 verification passed: the profile key header matches the Backend and both write " +
                      "requests (Run save, skill upgrade) send it.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException($"Beta Auth-0 verification failed: {message}");
            }
        }
    }
}
