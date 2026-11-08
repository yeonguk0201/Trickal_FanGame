using System;
using System.Collections.Generic;
using TrickalFanGame.Data;
using TrickalFanGame.Network;
using UnityEngine;

namespace TrickalFanGame.Meta
{
    public enum PendingSyncOutcome
    {
        Completed,   // Nothing is left in this profile's queue.
        Stopped,     // A write could not be sent (no server, no account, network or server error); it stays queued.
        Busy,        // Another flush is already running.
        Unavailable  // No API client.
    }

    // Offline-1 (docs/30 §3.1 S4): sends this profile's queued writes oldest first, Runs and skill upgrades alike,
    // so the server sees them in the order they happened. Each confirmed write leaves the queue and its returned
    // progress becomes the confirmed cache. A skill upgrade the server refuses on its rules (no point, maximum level,
    // stale target, no progress) is dropped and the server value is kept. Any other failure stops the flush and
    // leaves the rest queued for the next try. Repeated sends are safe: Runs by clientRunId, upgrades by target level.
    public static class PendingSync
    {
        private static bool flushInFlight;

        public static bool IsFlushing => flushInFlight;

        // The title (and the skill screen after an offline upgrade) sends everything waiting for this profile.
        public static bool Flush(IGameApiClient runClient, ISkillProgressApiClient skillClient,
            Action<PendingSyncOutcome> onDone)
        {
            if (flushInFlight)
            {
                onDone?.Invoke(PendingSyncOutcome.Busy);
                return false;
            }
            if (runClient == null)
            {
                onDone?.Invoke(PendingSyncOutcome.Unavailable);
                return false;
            }
            if (!LocalProfile.HasServerAccount)
            {
                // The server cannot own these writes until it registers this profile (Offline-0).
                onDone?.Invoke(PendingSyncOutcome.Stopped);
                return false;
            }

            flushInFlight = true;
            SendNext(new Context(runClient, skillClient ?? runClient as ISkillProgressApiClient, null,
                LocalProfile.Nickname, null, null, outcome =>
                {
                    flushInFlight = false;
                    onDone?.Invoke(outcome);
                }));
            return true;
        }

        // RunSubmission: sends the queue up to a Run already queued, then that Run. Its response (or the first
        // failure) goes to the caller, which removes it from the queue and stores the result as before.
        public static void SendThrough(IGameApiClient runClient, ISkillProgressApiClient skillClient,
            CreateRunRequest target, string nickname, Action<CreateRunResponse> onSuccess, Action<string> onError)
        {
            SendNext(new Context(runClient, skillClient ?? runClient as ISkillProgressApiClient, target, nickname,
                onSuccess, onError, null));
        }

        public static bool IsRejectedByRules(SkillUpgradeError error) =>
            error.Type == SkillUpgradeErrorType.NotEnoughPoints ||
            error.Type == SkillUpgradeErrorType.MaximumLevel ||
            error.Type == SkillUpgradeErrorType.Conflict ||
            error.Code == "CHARACTER_PROGRESS_NOT_FOUND" ||
            error.Code == "INVALID_SKILL_TYPE";

        private static void SendNext(Context context)
        {
            PendingSyncItem item = FirstItem(context.Target);
            if (item == null)
            {
                if (context.Target != null) context.RunClient.PostRun(context.Target, context.OnTargetSaved, context.Stop);
                else context.Finish(PendingSyncOutcome.Completed);
                return;
            }

            if (item.IsRun) SendRun(context, item.Run);
            else SendSkillUpgrade(context, item.SkillUpgrade);
        }

        private static void SendRun(Context context, CreateRunRequest request)
        {
            if (context.Target != null && request.clientRunId == context.Target.clientRunId)
            {
                context.RunClient.PostRun(context.Target, context.OnTargetSaved, context.Stop);
                return;
            }

            context.RunClient.PostRun(request, response =>
            {
                if (response?.data == null)
                {
                    context.Stop($"Pending Run {request.clientRunId} returned no data.");
                    return;
                }

                LocalPendingRunStorage.Remove(request.clientRunId);
                PlayerProgressClient.StoreConfirmedProgress(context.Nickname, response.data.progress);
                PlayerProgressClient.StoreConfirmedElifMeta(context.Nickname, response.data.meta);
                Debug.Log($"[PendingSync] Pending Run saved: {response.data.runId}");
                SendNext(context);
            }, context.Stop);
        }

        private static void SendSkillUpgrade(Context context, PendingSkillUpgrade upgrade)
        {
            if (context.SkillClient == null || !LocalProfile.HasServerAccount)
            {
                context.Stop("The skill upgrade waits until the server registers this profile.");
                return;
            }

            context.SkillClient.UpgradeSkill(LocalProfile.Nickname, upgrade.characterId, upgrade.skillType,
                upgrade.targetLevel,
                progress =>
                {
                    LocalPendingRunStorage.RemoveSkillUpgrade(upgrade.upgradeId);
                    PlayerProgressClient.StoreConfirmedProgress(LocalProfile.Nickname, progress);
                    Debug.Log($"[PendingSync] Pending skill upgrade saved: {upgrade.characterId} " +
                              $"{upgrade.skillType} Lv.{upgrade.targetLevel}");
                    SendNext(context);
                },
                error =>
                {
                    if (!IsRejectedByRules(error))
                    {
                        context.Stop($"Skill upgrade {upgrade.upgradeId} failed: {error.Code} {error.Message}");
                        return;
                    }

                    // The server's progress differs from this PC's calculation; its value wins.
                    LocalPendingRunStorage.RemoveSkillUpgrade(upgrade.upgradeId);
                    Debug.LogWarning($"[PendingSync] Dropped skill upgrade {upgrade.characterId} {upgrade.skillType} " +
                                     $"Lv.{upgrade.targetLevel}: the server refused it ({error.Code}).");
                    SendNext(context);
                });
        }

        private static PendingSyncItem FirstItem(CreateRunRequest target)
        {
            // A Run sent through the queue waits only for writes of its own profile.
            var owners = new List<string>(LocalProgression.PendingOwners());
            if (target != null && !owners.Contains(target.userId)) owners = new List<string> { target.userId };
            List<PendingSyncItem> queue = LocalPendingRunStorage.LoadQueue(owners.ToArray());
            return queue.Count > 0 ? queue[0] : null;
        }

        private sealed class Context
        {
            private readonly Action<CreateRunResponse> onTargetSaved;
            private readonly Action<string> onTargetFailed;
            private readonly Action<PendingSyncOutcome> onFlushDone;

            public Context(IGameApiClient runClient, ISkillProgressApiClient skillClient, CreateRunRequest target,
                string nickname, Action<CreateRunResponse> onTargetSaved, Action<string> onTargetFailed,
                Action<PendingSyncOutcome> onFlushDone)
            {
                RunClient = runClient;
                SkillClient = skillClient;
                Target = target;
                Nickname = nickname;
                this.onTargetSaved = onTargetSaved;
                this.onTargetFailed = onTargetFailed;
                this.onFlushDone = onFlushDone;
            }

            public IGameApiClient RunClient { get; }
            public ISkillProgressApiClient SkillClient { get; }
            public CreateRunRequest Target { get; }
            public string Nickname { get; }

            public void OnTargetSaved(CreateRunResponse response) => onTargetSaved?.Invoke(response);

            public void Stop(string error)
            {
                Debug.LogWarning($"[PendingSync] Stopped: {error}");
                if (Target != null) onTargetFailed?.Invoke(error);
                else Finish(PendingSyncOutcome.Stopped);
            }

            public void Finish(PendingSyncOutcome outcome) => onFlushDone?.Invoke(outcome);
        }
    }
}
