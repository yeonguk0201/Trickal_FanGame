using System;
using System.Linq;
using TrickalFanGame.Data;
using TrickalFanGame.Frontend;
using TrickalFanGame.Network;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    public static class Week13SkillUpgradeVerification
    {
        [MenuItem("Trickal Fan Game/Week 13/Verify Skill-1 and Skill-2")]
        public static void Verify()
        {
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.ScenePath, OpenSceneMode.Single);
            FrontendHomeView home = FindSingle<FrontendHomeView>(scene);
            FrontendCharacterSelectionView selection = home.CharacterSelectionView;
            FrontendSkillUpgradeView skill = home.SkillUpgradeView;
            Assert(selection.CardTemplate.GetComponent<RectTransform>().sizeDelta == new Vector2(320, 520),
                "Character picker must keep the tall card prepared for future horizontal paging.");
            Assert(skill != null, "Skill upgrade view is missing.");

            string oldUserId = LocalProfile.UserId;
            string oldNickname = LocalProfile.Nickname;
            try
            {
                LocalProfile.SaveProfile("skill-verification-user", "SkillTester");
                var profile = new FakeProfileClient(CreateProgress(points: 2, low: 1, high: 10));
                var upgrades = new FakeSkillClient();
                skill.SetApiClients(profile, upgrades);
                skill.Show(selection.CardTemplate.Character ?? LoadErpin());

                Assert(profile.GetUserCalls == 1 && skill.Progress != null,
                    "Skill-1 must load server progress once after character confirmation.");
                Assert(skill.CharacterLevelText.text == "캐릭터 레벨  Lv. 3 / 19" &&
                    skill.ExperienceText.text == "경험치  120 / 600" &&
                    skill.SkillPointsText.text == "남은 스킬 포인트  2" &&
                    skill.LowGradeLevelText.text == "Lv. 1 / 10" && skill.HighGradeLevelText.text == "Lv. 10 / 10",
                    "Skill-1 did not render level, experience, points and both skill levels.");
                Assert(skill.LowGradeUpgradeButton.interactable && !skill.HighGradeUpgradeButton.interactable,
                    "Available and maximum-level skills must have distinct interaction states.");

                upgrades.NextProgress = CreateProgress(points: 1, low: 2, high: 10);
                upgrades.Defer = true;
                skill.UpgradeLowGrade();
                skill.UpgradeLowGrade();
                Assert(upgrades.CallCount == 1 && upgrades.LastTargetLevel == 2 && skill.RequestInFlight,
                    "Repeated input while a request is pending must not submit twice.");
                upgrades.CompleteSuccess();
                upgrades.Defer = false;
                Assert(skill.Progress.skillPoints == 1 &&
                    skill.Progress.lowGradeSkillLevel == 2,
                    "Skill-2 must submit one target-level request and apply the returned point/level snapshot.");

                upgrades.NextError = SkillUpgradeError.FromApiError(422, "INVALID_SKILL_TARGET_LEVEL", "conflict");
                skill.UpgradeLowGrade();
                Assert(skill.StatusText.text == FrontendSkillUpgradeView.ConflictMessage,
                    "Stale target conflicts need a dedicated message.");

                upgrades.NextError = SkillUpgradeError.FromResponse(0, UnityWebRequest.Result.ConnectionError,
                    "offline", string.Empty);
                skill.UpgradeLowGrade();
                Assert(skill.StatusText.text.Contains("네트워크"), "Network failures need a dedicated message.");

                skill.Progress.skillPoints = 0;
                skill.UpgradeLowGrade();
                Assert(skill.StatusText.text == FrontendSkillUpgradeView.NotEnoughPointsMessage,
                    "Point shortage needs a dedicated message without issuing a request.");
                skill.Progress.skillPoints = 1;
                skill.UpgradeHighGrade();
                Assert(skill.StatusText.text == FrontendSkillUpgradeView.MaximumLevelMessage,
                    "Maximum level needs a dedicated message without issuing a request.");

                var empty = new FakeProfileClient((CharacterProgressDto)null);
                skill.SetApiClients(empty, upgrades);
                skill.Show(LoadErpin());
                Assert(skill.Progress == null && skill.StatusText.text == FrontendSkillUpgradeView.EmptyMessage &&
                    skill.RetryButton.gameObject.activeSelf, "Skill-1 must distinguish an empty progress state.");

                var failed = new FakeProfileClient("offline");
                skill.SetApiClients(failed, upgrades);
                skill.Show(LoadErpin());
                Assert(skill.Progress == null && skill.StatusText.text == FrontendSkillUpgradeView.LoadFailedMessage &&
                    skill.RetryButton.gameObject.activeSelf, "Skill-1 must distinguish a lookup failure state.");
            }
            finally
            {
                skill.Hide();
                if (string.IsNullOrEmpty(oldUserId) || string.IsNullOrEmpty(oldNickname)) LocalProfile.ClearProfile();
                else LocalProfile.SaveProfile(oldUserId, oldNickname);
            }

            Debug.Log("Skill-1/2 verification passed: tall data-driven picker, loading/empty/failure states, progress rendering, idempotent target-level request, immediate refresh, and categorized failures.");
        }

        public static void SetupAndVerifyBatch()
        {
            string guid = AssetDatabase.AssetPathToGUID(Week13FrontendSetup.ScenePath);
            Week13FrontendSetup.Setup();
            Week13FrontendSetup.Setup();
            Assert(guid == AssetDatabase.AssetPathToGUID(Week13FrontendSetup.ScenePath),
                "Repeated setup changed the Frontend Scene GUID.");
            Verify();
            Week13FrontendVerification.Verify();
        }

        private static CharacterProgressDto CreateProgress(int points, int low, int high) => new()
        {
            characterId = "erpin",
            level = 3,
            maxLevel = 19,
            experience = 120,
            experienceToNextLevel = 600,
            skillPoints = points,
            lowGradeSkillLevel = low,
            highGradeSkillLevel = high,
            maxSkillLevel = 10
        };

        private static TrickalFanGame.Character.CharacterDefinition LoadErpin()
        {
            var character = AssetDatabase.LoadAssetAtPath<TrickalFanGame.Character.CharacterDefinition>(
                "Assets/Characters/erpin.asset");
            Assert(character != null, "Erpin CharacterDefinition is missing.");
            return character;
        }

        private static T FindSingle<T>(Scene scene) where T : Component
        {
            T[] matches = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
            Assert(matches.Length == 1, $"Expected one {typeof(T).Name}; found {matches.Length}.");
            return matches[0];
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private sealed class FakeProfileClient : IGameApiClient
        {
            private readonly CharacterProgressDto progress;
            private readonly string error;
            public int GetUserCalls { get; private set; }

            public FakeProfileClient(CharacterProgressDto result) { progress = result; }
            public FakeProfileClient(string failure) { error = failure; }

            public void GetUser(string nickname, Action<UserProfileResponse> onSuccess, Action<string> onError)
            {
                GetUserCalls++;
                if (error != null) { onError(error); return; }
                onSuccess(new UserProfileResponse
                {
                    success = true,
                    data = new UserProfileData
                    {
                        id = "skill-verification-user",
                        nickname = nickname,
                        characterProgress = progress == null ? Array.Empty<CharacterProgressDto>() : new[] { progress }
                    }
                });
            }

            public void PostRun(CreateRunRequest request, Action<CreateRunResponse> onSuccess, Action<string> onError) =>
                throw new NotSupportedException();
            public void PostUser(CreateUserRequest request, Action<CreateUserResponse> onSuccess, Action<string> onError) =>
                throw new NotSupportedException();
        }

        private sealed class FakeSkillClient : ISkillProgressApiClient
        {
            public int CallCount { get; private set; }
            public int LastTargetLevel { get; private set; }
            public CharacterProgressDto NextProgress { get; set; }
            public SkillUpgradeError? NextError { get; set; }
            public bool Defer { get; set; }
            private Action<CharacterProgressDto> pendingSuccess;
            private Action<SkillUpgradeError> pendingError;

            public void UpgradeSkill(string nickname, string characterId, SkillType skillType, int targetLevel,
                Action<CharacterProgressDto> onSuccess, Action<SkillUpgradeError> onError)
            {
                CallCount++;
                LastTargetLevel = targetLevel;
                if (Defer)
                {
                    pendingSuccess = onSuccess;
                    pendingError = onError;
                    return;
                }
                Respond(onSuccess, onError);
            }

            public void CompleteSuccess()
            {
                Action<CharacterProgressDto> callback = pendingSuccess;
                pendingSuccess = null;
                pendingError = null;
                callback(NextProgress);
                NextProgress = null;
            }

            private void Respond(Action<CharacterProgressDto> onSuccess, Action<SkillUpgradeError> onError)
            {
                if (NextError.HasValue)
                {
                    SkillUpgradeError error = NextError.Value;
                    NextError = null;
                    onError(error);
                    return;
                }
                CharacterProgressDto result = NextProgress;
                NextProgress = null;
                onSuccess(result);
            }
        }
    }
}
