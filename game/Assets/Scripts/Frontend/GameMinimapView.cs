using System;
using System.Collections.Generic;
using System.Linq;
using TrickalFanGame.Room;
using UnityEngine;
using UnityEngine.UI;

namespace TrickalFanGame.Frontend
{
    public sealed class GameMinimapView : MonoBehaviour
    {
        [SerializeField] private RunProgress runProgress;
        [SerializeField] private RectTransform mapRoot;
        [SerializeField] private MinimapRoomMarkerView markerTemplate;
        [SerializeField] private Image connectionTemplate;
        [SerializeField, Min(1f)] private float roomSpacing = 48f;

        private readonly List<MinimapRoomMarkerView> visibleMarkers = new();
        private readonly List<Image> visibleConnections = new();
        private readonly List<RoomRunState> observedRoomStates = new();

        public RunProgress Progress => runProgress;
        public RectTransform MapRoot => mapRoot;
        public MinimapRoomMarkerView MarkerTemplate => markerTemplate;
        public Image ConnectionTemplate => connectionTemplate;
        public float RoomSpacing => roomSpacing;
        public IReadOnlyList<MinimapRoomMarkerView> VisibleMarkers => visibleMarkers;
        public int VisibleConnectionCount => visibleConnections.Count;
        public string CurrentRoomId { get; private set; }
        public float EffectiveSpacing { get; private set; }

        public void Configure(RunProgress configuredProgress, RectTransform configuredMapRoot,
            MinimapRoomMarkerView configuredMarkerTemplate, Image configuredConnectionTemplate,
            float configuredRoomSpacing = 48f)
        {
            Unsubscribe();
            runProgress = configuredProgress;
            mapRoot = configuredMapRoot;
            markerTemplate = configuredMarkerTemplate;
            connectionTemplate = configuredConnectionTemplate;
            roomSpacing = Mathf.Max(1f, configuredRoomSpacing);
            Subscribe();
            RefreshNow();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void Start()
        {
            RefreshNow();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void RefreshNow()
        {
            ClearInstances();
            CurrentRoomId = null;
            EffectiveSpacing = roomSpacing;
            if (runProgress == null || mapRoot == null || markerTemplate == null || connectionTemplate == null ||
                runProgress.GeneratedGraph == null || runProgress.CurrentFloor < 1 || runProgress.CurrentRoom < 1)
                return;

            SyncStateSubscriptions();
            GeneratedFloor floor = runProgress.GeneratedGraph.FindFloor(runProgress.CurrentFloor);
            if (floor == null) return;

            string roomId = FloorGenerator.BuildRoomId(runProgress.CurrentFloor, runProgress.CurrentRoom);
            GeneratedRoomNode current = FindRoom(floor, roomId);
            if (current == null) return;

            CurrentRoomId = current.RoomId;
            HashSet<string> visitedIds = new(StringComparer.Ordinal);
            HashSet<string> visibleIds = new(StringComparer.Ordinal);
            foreach (GeneratedRoomNode node in floor.Nodes)
            {
                RoomRunState state = runProgress.GetRoomState(node.RoomId);
                if (node.RoomId == current.RoomId || state?.HasVisited == true)
                {
                    visitedIds.Add(node.RoomId);
                    visibleIds.Add(node.RoomId);
                }
            }
            foreach (GeneratedRoomNode node in floor.Nodes)
            {
                if (!visitedIds.Contains(node.RoomId)) continue;
                foreach (GeneratedRoomConnection connection in node.DirectionalConnections)
                    visibleIds.Add(connection.DestinationRoomId);
            }

            List<GeneratedRoomNode> visibleNodes = new();
            foreach (GeneratedRoomNode node in floor.Nodes)
                if (visibleIds.Contains(node.RoomId)) visibleNodes.Add(node);
            if (visibleNodes.Count == 0) return;

            int minimumX = visibleNodes.Min(node => node.GridPosition.X);
            int maximumX = visibleNodes.Max(node => node.GridPosition.X);
            int minimumY = visibleNodes.Min(node => node.GridPosition.Y);
            int maximumY = visibleNodes.Max(node => node.GridPosition.Y);
            float spanX = maximumX - minimumX;
            float spanY = maximumY - minimumY;
            float fitX = spanX > 0f ? Mathf.Max(1f, mapRoot.rect.width - 34f) / (spanX * roomSpacing + 30f) : 1f;
            float fitY = spanY > 0f ? Mathf.Max(1f, mapRoot.rect.height - 34f) / (spanY * roomSpacing + 30f) : 1f;
            float visualScale = Mathf.Min(1f, fitX, fitY);
            EffectiveSpacing = roomSpacing * visualScale;
            Vector2 gridCenter = new((minimumX + maximumX) * 0.5f, (minimumY + maximumY) * 0.5f);
            Dictionary<string, Vector2> positions = new(StringComparer.Ordinal);
            foreach (GeneratedRoomNode node in visibleNodes)
            {
                Vector2 position = new((node.GridPosition.X - gridCenter.x) * EffectiveSpacing,
                    (node.GridPosition.Y - gridCenter.y) * EffectiveSpacing);
                positions.Add(node.RoomId, position);
            }

            HashSet<string> drawnConnections = new(StringComparer.Ordinal);
            foreach (GeneratedRoomNode node in visibleNodes)
            {
                foreach (GeneratedRoomConnection connection in node.DirectionalConnections)
                {
                    if (!visibleIds.Contains(connection.DestinationRoomId) ||
                        (!visitedIds.Contains(node.RoomId) && !visitedIds.Contains(connection.DestinationRoomId)))
                        continue;
                    string key = string.CompareOrdinal(node.RoomId, connection.DestinationRoomId) < 0
                        ? node.RoomId + "|" + connection.DestinationRoomId
                        : connection.DestinationRoomId + "|" + node.RoomId;
                    if (drawnConnections.Add(key))
                        AddConnection(positions[node.RoomId], positions[connection.DestinationRoomId], visualScale);
                }
            }
            foreach (GeneratedRoomNode node in visibleNodes)
            {
                AddMarker(node, runProgress.GetRoomState(node.RoomId), positions[node.RoomId],
                    node.RoomId == current.RoomId, visualScale);
            }
        }

        private void Subscribe()
        {
            if (runProgress != null)
            {
                runProgress.RoomChanged -= HandleRoomChanged;
                runProgress.RoomChanged += HandleRoomChanged;
            }
            SyncStateSubscriptions();
        }

        private void Unsubscribe()
        {
            if (runProgress != null) runProgress.RoomChanged -= HandleRoomChanged;
            foreach (RoomRunState state in observedRoomStates) state.Changed -= HandleRoomStateChanged;
            observedRoomStates.Clear();
        }

        private void HandleRoomChanged(int _, int __)
        {
            RefreshNow();
        }

        private void HandleRoomStateChanged()
        {
            RefreshNow();
        }

        private void SyncStateSubscriptions()
        {
            foreach (RoomRunState state in observedRoomStates) state.Changed -= HandleRoomStateChanged;
            observedRoomStates.Clear();
            if (runProgress?.GeneratedGraph == null) return;
            foreach (RoomRunState state in runProgress.RoomStates.Values)
            {
                if (state == null) continue;
                state.Changed += HandleRoomStateChanged;
                observedRoomStates.Add(state);
            }
        }

        private void AddMarker(GeneratedRoomNode node, RoomRunState state, Vector2 position, bool isCurrent,
            float visualScale)
        {
            MinimapRoomMarkerView marker = Instantiate(markerTemplate, mapRoot);
            marker.gameObject.SetActive(true);
            RectTransform rect = marker.GetComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.localScale = Vector3.one * visualScale;
            marker.Bind(node, state, isCurrent);
            visibleMarkers.Add(marker);
        }

        private void AddConnection(Vector2 source, Vector2 destination, float visualScale)
        {
            Image connection = Instantiate(connectionTemplate, mapRoot);
            connection.gameObject.SetActive(true);
            RectTransform rect = connection.rectTransform;
            Vector2 delta = destination - source;
            rect.anchoredPosition = (source + destination) * 0.5f;
            float lineLength = Mathf.Max(2f, delta.magnitude - 30f * visualScale);
            rect.sizeDelta = Mathf.Abs(delta.x) > Mathf.Abs(delta.y)
                ? new Vector2(lineLength, Mathf.Max(2f, 4f * visualScale))
                : new Vector2(Mathf.Max(2f, 4f * visualScale), lineLength);
            rect.localScale = Vector3.one;
            rect.SetAsFirstSibling();
            visibleConnections.Add(connection);
        }

        private void ClearInstances()
        {
            foreach (MinimapRoomMarkerView marker in visibleMarkers) DestroyObject(marker?.gameObject);
            foreach (Image connection in visibleConnections) DestroyObject(connection?.gameObject);
            visibleMarkers.Clear();
            visibleConnections.Clear();
        }

        private static GeneratedRoomNode FindRoom(GeneratedFloor floor, string roomId)
        {
            foreach (GeneratedRoomNode node in floor.Nodes)
                if (node != null && string.Equals(node.RoomId, roomId, StringComparison.Ordinal)) return node;
            return null;
        }

        private static void DestroyObject(GameObject target)
        {
            if (target == null) return;
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }
    }
}
