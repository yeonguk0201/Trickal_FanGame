using System;
using System.Linq;
using TrickalFanGame.Data;
using TrickalFanGame.Network;

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
