using System;
using System.Collections.Generic;
using System.Linq;
using TrickalFanGame.Room;
using UnityEngine;
using UnityEngine.UI;

namespace TrickalFanGame.Frontend
{
    // UX-1: with a content root the map keeps its real spacing and slides so the current room sits in the middle
    // of the masked viewport; rooms beyond the edge are cut off. Without one (older scenes, tests) the whole
    // explored graph is shrunk to fit as before.
    public sealed class GameMinimapView : MonoBehaviour
    {
        public const float DefaultFollowSharpness = 9f;

        [SerializeField] private RunProgress runProgress;
        [SerializeField] private RectTransform mapRoot;
        [SerializeField] private MinimapRoomMarkerView markerTemplate;
        [SerializeField] private Image connectionTemplate;
        [SerializeField, Min(1f)] private float roomSpacing = 48f;
        [SerializeField] private RectTransform content;
        [SerializeField, Min(0.1f)] private float followSharpness = DefaultFollowSharpness;

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
        public bool IsFloorRevealed { get; private set; }
        public float EffectiveSpacing { get; private set; }
        public RectTransform Content => content;
        public bool CentersOnCurrentRoom => content != null;
        // Where the content is heading so that the current room is at the viewport centre.
        public Vector2 ContentTargetPosition { get; private set; }
        private int centeredFloor = -1;
        private GeneratedFloorGraph centeredGraph;

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

        // UX-1: markers and lines go under this child of the map root, which moves instead of the rooms.
        public void ConfigureViewport(RectTransform configuredContent, float configuredFollowSharpness)
        {
            content = configuredContent;
            followSharpness = Mathf.Max(0.1f, configuredFollowSharpness);
            centeredFloor = -1;
            RefreshNow();
        }

        // Jumps straight to the current room, e.g. when a floor starts or for editor checks.
        public void SnapToCurrentRoom()
        {
            if (content != null) content.anchoredPosition = ContentTargetPosition;
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void Update()
        {
            if (content == null) return;
            Vector2 position = content.anchoredPosition;
            if ((position - ContentTargetPosition).sqrMagnitude < 0.01f)
            {
                if (position != ContentTargetPosition) content.anchoredPosition = ContentTargetPosition;
                return;
            }

            // Unscaled so the map still settles while the game is paused.
            float blend = 1f - Mathf.Exp(-followSharpness * Time.unscaledDeltaTime);
            content.anchoredPosition = Vector2.Lerp(position, ContentTargetPosition, blend);
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
            IsFloorRevealed = false;
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
            // Spell-6 (네티의 지도): every room shows, the secret room by its position only.
            bool isRevealed = runProgress.IsFloorMapRevealed(floor.FloorNumber);
            IsFloorRevealed = isRevealed;
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
                else if (isRevealed)
                {
                    visibleIds.Add(node.RoomId);
                }
            }
            foreach (GeneratedRoomNode node in floor.Nodes)
            {
                if (!visitedIds.Contains(node.RoomId)) continue;
                foreach (GeneratedRoomConnection connection in node.DirectionalConnections)
                    if (IsPassageKnown(node, connection)) visibleIds.Add(connection.DestinationRoomId);
            }

            List<GeneratedRoomNode> visibleNodes = new();
            foreach (GeneratedRoomNode node in floor.Nodes)
                if (visibleIds.Contains(node.RoomId)) visibleNodes.Add(node);
            if (visibleNodes.Count == 0) return;

            float visualScale = 1f;
            Vector2 gridCenter = Vector2.zero;
            if (!CentersOnCurrentRoom)
            {
                int minimumX = visibleNodes.Min(node => node.GridPosition.X);
                int maximumX = visibleNodes.Max(node => node.GridPosition.X);
                int minimumY = visibleNodes.Min(node => node.GridPosition.Y);
                int maximumY = visibleNodes.Max(node => node.GridPosition.Y);
                float spanX = maximumX - minimumX;
                float spanY = maximumY - minimumY;
                float fitX = spanX > 0f ? Mathf.Max(1f, mapRoot.rect.width - 34f) / (spanX * roomSpacing + 30f) : 1f;
                float fitY = spanY > 0f ? Mathf.Max(1f, mapRoot.rect.height - 34f) / (spanY * roomSpacing + 30f) : 1f;
                visualScale = Mathf.Min(1f, fitX, fitY);
                gridCenter = new((minimumX + maximumX) * 0.5f, (minimumY + maximumY) * 0.5f);
            }
            EffectiveSpacing = roomSpacing * visualScale;
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
                    if (!visibleIds.Contains(connection.DestinationRoomId) || !IsPassageKnown(node, connection) ||
                        (!isRevealed && !visitedIds.Contains(node.RoomId) &&
                         !visitedIds.Contains(connection.DestinationRoomId)))
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

            if (!CentersOnCurrentRoom) return;
            // Room positions stay fixed in content space, so moving between rooms glides instead of jumping.
            ContentTargetPosition = -positions[current.RoomId];
            // A new floor or a new Run starts on its room instead of sliding across the map.
            if (centeredFloor != floor.FloorNumber || centeredGraph != runProgress.GeneratedGraph)
            {
                centeredFloor = floor.FloorNumber;
                centeredGraph = runProgress.GeneratedGraph;
                SnapToCurrentRoom();
            }
        }

        private RectTransform InstanceParent => content != null ? content : mapRoot;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Development panel only: shows secret rooms and hidden passages before they are discovered.
        public static bool DevelopmentRevealSecrets { get; set; }
#endif

        // Special-3: a hidden passage (and the secret room behind it) appears only once it is open.
        private bool IsPassageKnown(GeneratedRoomNode node, GeneratedRoomConnection connection)
        {
            if (!connection.IsSecret) return true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (DevelopmentRevealSecrets) return true;
#endif
            bool fromSecret = node.Role == GeneratedRoomRole.Secret;
            RoomRunState secretState = runProgress.GetRoomState(fromSecret ? node.RoomId : connection.DestinationRoomId);
            return secretState != null &&
                   secretState.IsSecretPassageOpen(fromSecret ? connection.DestinationRoomId : node.RoomId);
        }

        private void Subscribe()
        {
            if (runProgress != null)
            {
                runProgress.RoomChanged -= HandleRoomChanged;
                runProgress.RoomChanged += HandleRoomChanged;
                runProgress.MapChanged -= RefreshNow;
                runProgress.MapChanged += RefreshNow;
            }
            SyncStateSubscriptions();
        }

        private void Unsubscribe()
        {
            if (runProgress != null)
            {
                runProgress.RoomChanged -= HandleRoomChanged;
                runProgress.MapChanged -= RefreshNow;
            }
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
            MinimapRoomMarkerView marker = Instantiate(markerTemplate, InstanceParent);
            marker.gameObject.SetActive(true);
            RectTransform rect = marker.GetComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.localScale = Vector3.one * visualScale;
            marker.Bind(node, state, isCurrent);
            visibleMarkers.Add(marker);
        }

        private void AddConnection(Vector2 source, Vector2 destination, float visualScale)
        {
            Image connection = Instantiate(connectionTemplate, InstanceParent);
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
