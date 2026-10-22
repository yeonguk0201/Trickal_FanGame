using System;
using System.Linq;
using TrickalFanGame.Data;
using TrickalFanGame.Network;

namespace TrickalFanGame.Meta
{
    // Elif-3: sends a finished Run's save request. A Run that bought shop upgrade levels must reach the server after
    // the earlier pending Runs of the same profile, or the server refuses a level that builds on one it has not
    // seen yet (ELIF_UPGRADE_LEVEL_GAP, contract 27 §11). Such a request joins the pending queue and the queue is sent
    // oldest first; every other request is sent at once as before. A failure leaves the rest queued for the title's
    // recovery, so offline play never loses a Run.
    public static class RunSubmission
    {
        public static bool NeedsOrderedDelivery(CreateRunRequest request) =>
            request?.elif?.upgradePurchases != null && request.elif.upgradePurchases.Length > 0 &&
            LocalPendingRunStorage.LoadAll(request.userId).Any(pending => pending.clientRunId != request.clientRunId);

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
            PostOldest(client, request, nickname, onSuccess, onError);
        }

        private static void PostOldest(IGameApiClient client, CreateRunRequest request, string nickname,
            Action<CreateRunResponse> onSuccess, Action<string> onError)
        {
            CreateRunRequest oldest = LocalPendingRunStorage.Load(request.userId);
            if (oldest == null || oldest.clientRunId == request.clientRunId)
            {
                client.PostRun(request, onSuccess, onError);
                return;
            }

            client.PostRun(oldest, response =>
            {
                if (response?.data == null)
                {
                    onError?.Invoke($"Pending Run {oldest.clientRunId} returned no data.");
                    return;
                }

                LocalPendingRunStorage.Remove(oldest.clientRunId);
                PlayerProgressClient.StoreConfirmedProgress(nickname, response.data.progress);
                PlayerProgressClient.StoreConfirmedElifMeta(nickname, response.data.meta);
                PostOldest(client, request, nickname, onSuccess, onError);
            }, onError);
        }
    }
}
