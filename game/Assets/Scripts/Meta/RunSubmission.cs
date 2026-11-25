using System;
using System.Linq;
using TrickalFanGame.Data;
using TrickalFanGame.Network;
using UnityEngine;

namespace TrickalFanGame.Meta
{
    // Elif-3: sends a finished Run's save request. A Run that bought shop upgrade levels must reach the server after
    // the earlier pending Runs of the same profile, or the server refuses a level that builds on one it has not
    // seen yet (ELIF_UPGRADE_LEVEL_GAP, contract 27 §11). Offline-1: a Run also waits behind skill upgrades queued
    // before it, so the server sees growth in the order it happened (docs/30 §3.1 S4). Such a request joins the
    // pending queue and the queue is sent oldest first (PendingSync); every other request is sent at once as before.
    // A failure leaves the rest queued for the title's recovery, so offline play never loses a Run.
    public static class RunSubmission
    {
        public static bool NeedsOrderedDelivery(CreateRunRequest request)
        {
            if (request == null) return false;
            bool hasEarlierRun = LocalPendingRunStorage.LoadAll(request.userId)
                .Any(pending => pending.clientRunId != request.clientRunId);
            bool boughtUpgrades = request.elif?.upgradePurchases != null && request.elif.upgradePurchases.Length > 0;
            return (boughtUpgrades && hasEarlierRun) ||
                   LocalPendingRunStorage.HasPendingSkillUpgrades(OwnersOf(request.userId));
        }

        private static string[] OwnersOf(string userId)
        {
            string[] owners = LocalProgression.PendingOwners();
            return owners.Contains(userId) ? owners : new[] { userId };
        }

        // Balance-1: GAME OVER and VICTORY start the save while their presentation plays, so the result screen
        // usually finds the answer waiting instead of saying "잠시만 기다려 주세요". The result screen (or the Game
        // Scene's own fallback) then attaches to that same request instead of sending it again. The server keys a
        // Run by clientRunId, so even a second send could not save it twice.
        private sealed class EarlySubmission
        {
            public EarlySubmission(string clientRunId) => ClientRunId = clientRunId;
            public string ClientRunId { get; }
            public bool IsDone;
            public CreateRunResponse Response;
            public string Error;
            public Action<CreateRunResponse> OnSuccess;
            public Action<string> OnError;
        }

        private static EarlySubmission early;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => early = null;

        public static bool HasEarlySubmission(string clientRunId) =>
            early != null && early.ClientRunId == clientRunId;

        public static bool BeginEarly(IGameApiClient client, CreateRunRequest request, string nickname)
        {
            if (client == null || request == null || string.IsNullOrWhiteSpace(request.clientRunId)) return false;
            if (HasEarlySubmission(request.clientRunId)) return true;
            EarlySubmission submission = new(request.clientRunId);
            early = submission;
            Post(client, request, nickname,
                response => FinishEarly(submission, response, null),
                error => FinishEarly(submission, null, string.IsNullOrEmpty(error) ? "Run save failed." : error));
            return true;
        }

        // Hands the early request's outcome to the callbacks: at once when it has already arrived, otherwise when
        // it does. Returns false when no early request for this Run exists, so the caller sends it itself.
        public static bool TryAttachEarly(string clientRunId, Action<CreateRunResponse> onSuccess,
            Action<string> onError)
        {
            if (!HasEarlySubmission(clientRunId)) return false;
            EarlySubmission submission = early;
            if (!submission.IsDone)
            {
                submission.OnSuccess = onSuccess;
                submission.OnError = onError;
                return true;
            }

            early = null;
            Deliver(submission.Response, submission.Error, onSuccess, onError);
            return true;
        }

        private static void FinishEarly(EarlySubmission submission, CreateRunResponse response, string error)
        {
            submission.IsDone = true;
            submission.Response = response;
            submission.Error = error;
            if (submission.OnSuccess == null && submission.OnError == null) return;
            if (early == submission) early = null;
            Deliver(response, error, submission.OnSuccess, submission.OnError);
        }

        private static void Deliver(CreateRunResponse response, string error, Action<CreateRunResponse> onSuccess,
            Action<string> onError)
        {
            if (error == null) onSuccess?.Invoke(response);
            else onError?.Invoke(error);
        }

        public static void Post(IGameApiClient client, CreateRunRequest request, string nickname,
            Action<CreateRunResponse> onSuccess, Action<string> onError)
        {
            if (client == null)
            {
                onError?.Invoke("ApiClient is unavailable.");
                return;
            }

            if (!NeedsOrderedDelivery(request))
            {
                client.PostRun(request, onSuccess, onError);
                return;
            }

            LocalPendingRunStorage.Save(request);
            PendingSync.SendThrough(client, client as ISkillProgressApiClient ?? ApiClient.Instance, request, nickname,
                onSuccess, onError);
        }
    }
}
