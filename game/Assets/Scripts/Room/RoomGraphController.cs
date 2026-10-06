using System;
using System.Collections.Generic;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Player;
using UnityEngine;

namespace TrickalFanGame.Room
{
    public sealed class RoomGraphController : MonoBehaviour
    {
        [SerializeField] private RoomNode[] nodes = Array.Empty<RoomNode>();
        [SerializeField] private RoomNode startingNode;
        [SerializeField] private PlayerMovement player;
        [SerializeField] private RoomCameraController roomCamera;
        [SerializeField] private RunProgress runProgress;
        [SerializeField, Min(0f)] private float transitionCooldown = 0.2f;
        [SerializeField, Min(0f)] private float doorwayInvulnerabilityDuration = 0.75f;
        [SerializeField, Min(0f)] private float returnDoorwayBlockDuration = 0.4f;

        private float nextTransitionTime;
        private RoomNode returnBlockedDestination;
        private float returnBlockedUntil;

        public RoomNode CurrentNode { get; private set; }
        public RoomNode StartingNode => startingNode;
        public IReadOnlyList<RoomNode> Nodes => nodes;
        public RunProgress Progress => runProgress;
        public PlayerMovement Player => player;
        public RoomCameraController RoomCamera => roomCamera;
        public float DoorwayInvulnerabilityDuration => doorwayInvulnerabilityDuration;
        public float ReturnDoorwayBlockDuration => returnDoorwayBlockDuration;

        public bool IsReturnDoorwayBlocked(RoomNode destination) =>
            destination != null && destination == returnBlockedDestination && Time.unscaledTime < returnBlockedUntil;

        public void Configure(
            RoomNode[] configuredNodes,
            RoomNode configuredStartingNode,
            PlayerMovement configuredPlayer,
            RoomCameraController configuredRoomCamera,
            RunProgress configuredRunProgress)
        {
            nodes = configuredNodes ?? Array.Empty<RoomNode>();
            startingNode = configuredStartingNode;
            player = configuredPlayer;
            roomCamera = configuredRoomCamera;
            runProgress = configuredRunProgress;
        }

        private void Start()
        {
            if (!TryInitializeStartingRoom(out string error))
            {
                Debug.LogError($"{name}: Invalid room graph. {error}", this);
                enabled = false;
            }
        }

        public bool TryTransition(
            RoomNode source,
            RoomNode destination,
            Transform destinationEntryPoint,
            PlayerMovement transitioningPlayer)
        {
            if (source == null || !source.HasConnectionTo(destination) || IsReturnDoorwayBlocked(destination))
            {
                return false;
            }

            Transform entryPoint = destinationEntryPoint != null
                ? destinationEntryPoint
                : destination.DefaultEntryPoint;
            if (entryPoint == null)
            {
                Debug.LogError($"{name}: Destination room {destination.RoomId} has no entry point.", destination);
                return false;
            }

            if (!TryMoveBetweenRooms(source, destination, entryPoint.position, transitioningPlayer)) return false;
            returnBlockedDestination = source;
            returnBlockedUntil = Time.unscaledTime + returnDoorwayBlockDuration;
            return true;
        }

        // Special-3 pit: moves between rooms of the current floor that need not be adjacent, such as a pit into the
        // secret room. It applies the same transition guards, cooldown and invulnerability as a doorway.
        public bool TryTeleport(RoomNode source, RoomNode destination, Vector2 destinationPosition,
            PlayerMovement transitioningPlayer)
        {
            if (!CanTeleport(source, destination, transitioningPlayer)) return false;
            if (!TryMoveBetweenRooms(source, destination, destinationPosition, transitioningPlayer)) return false;
            returnBlockedDestination = null;
            return true;
        }

        // Reports whether TryTeleport would pass its transition guards, so a caller can check before spending an item.
        public bool CanTeleport(RoomNode source, RoomNode destination, PlayerMovement transitioningPlayer) =>
            destination != null && source != destination && Array.IndexOf(nodes, destination) >= 0 &&
            CanMoveBetweenRooms(source, destination, transitioningPlayer);

        private bool CanMoveBetweenRooms(RoomNode source, RoomNode destination, PlayerMovement transitioningPlayer) =>
            enabled && source != null && destination != null && transitioningPlayer != null &&
            source == CurrentNode && runProgress?.IsRewardSelectionPending != true &&
            transitioningPlayer.GetComponent<PlayerActionState>()?.CanTransition != false &&
            Time.unscaledTime >= nextTransitionTime;

        private bool TryMoveBetweenRooms(RoomNode source, RoomNode destination, Vector2 destinationPosition,
            PlayerMovement transitioningPlayer)
        {
            if (!CanMoveBetweenRooms(source, destination, transitioningPlayer))
            {
                return false;
            }

            Vector2 previousPlayerPosition = transitioningPlayer.transform.position;
            ClearTransientProjectiles();
            destination.SetVisible(true);
            MovePlayer(transitioningPlayer, destinationPosition);
            source.SetVisible(false);

            if (!TryShowRoom(destination, transitioningPlayer.transform, out string cameraError))
            {
                source.SetVisible(true);
                destination.SetVisible(false);
                MovePlayer(transitioningPlayer, previousPlayerPosition);
                Debug.LogError($"{name}: Cannot frame destination room {destination.RoomId}. {cameraError}", destination);
                return false;
            }

            CurrentNode = destination;
            CurrentNode.MarkVisited();
            runProgress?.RecordRoomEntry(CurrentNode.FloorNumber, CurrentNode.RoomNumber);
            nextTransitionTime = Time.unscaledTime + transitionCooldown;
            transitioningPlayer.GetComponent<DamageInvulnerability>()
                ?.BeginWindow(Time.time, doorwayInvulnerabilityDuration);
            return true;
        }

        public bool TryReplaceFloor(RoomNode[] configuredNodes, RoomNode configuredStart,
            PlayerMovement transitioningPlayer, out string error)
        {
            if (runProgress?.IsRewardSelectionPending == true)
            {
                error = "A pending reward selection blocks floor replacement.";
                return false;
            }

            nodes = configuredNodes ?? Array.Empty<RoomNode>();
            startingNode = configuredStart;
            return TryInitializeStartingRoom(transitioningPlayer, out error);
        }

        public bool TryInitializeStartingRoom(out string error)
        {
            return TryInitializeStartingRoom(player, out error);
        }

        public bool TryInitializeStartingRoom(PlayerMovement transitioningPlayer, out string error)
        {
            if (!TryValidateConfiguration(out error)) return false;
            bool enteredDifferentRoom = CurrentNode != startingNode;
            returnBlockedDestination = null;
            ActivateOnly(startingNode);
            CurrentNode = startingNode;
            CurrentNode.MarkVisited();
            PlayerMovement targetPlayer = transitioningPlayer != null ? transitioningPlayer : player;
            if (targetPlayer != null)
            {
                ClearTransientProjectiles();
                MovePlayer(targetPlayer, startingNode.InitialSpawnPosition);
            }
            if (!TryShowRoom(startingNode, targetPlayer != null ? targetPlayer.transform : null, out error))
            {
                return false;
            }
            if (runProgress != null &&
                (enteredDifferentRoom || runProgress.CurrentFloor != startingNode.FloorNumber ||
                 runProgress.CurrentRoom != startingNode.RoomNumber))
            {
                runProgress.RecordRoomEntry(startingNode.FloorNumber, startingNode.RoomNumber);
            }
            error = null;
            return true;
        }

        private bool TryShowRoom(RoomNode node, Transform followTarget, out string error)
        {
            if (roomCamera == null)
            {
                error = null;
                return true;
            }

            if (node.Profile == null)
            {
                roomCamera.ShowRoom(node.CameraAnchor);
                error = null;
                return true;
            }

            return roomCamera.ShowRoom(node.CameraAnchor, node.Profile, followTarget, out error);
        }

        public bool TryValidateConfiguration(out string error)
        {
            if (nodes == null || nodes.Length == 0)
            {
                error = "At least one RoomNode is required.";
                return false;
            }

            HashSet<RoomNode> nodeSet = new();
            HashSet<string> ids = new(StringComparer.Ordinal);
            HashSet<(int Floor, int Room)> addresses = new();
            foreach (RoomNode node in nodes)
            {
                if (node == null)
                {
                    error = "The graph contains a missing RoomNode reference.";
                    return false;
                }

                if (!nodeSet.Add(node))
                {
                    error = $"RoomNode {node.name} is registered more than once.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(node.RoomId) || !ids.Add(node.RoomId))
                {
                    error = $"Room ID '{node.RoomId}' is empty or duplicated.";
                    return false;
                }

                if (!addresses.Add((node.FloorNumber, node.RoomNumber)))
                {
                    error = $"Floor/room key ({node.FloorNumber}, {node.RoomNumber}) is duplicated.";
                    return false;
                }

                if (node.ContentRoot == null || node.CameraAnchor == null || node.DefaultEntryPoint == null)
                {
                    error = $"Room {node.RoomId} is missing its content root, camera anchor, or entry point.";
                    return false;
                }
            }

            if (startingNode == null || !nodeSet.Contains(startingNode))
            {
                error = "The starting RoomNode is not part of the graph.";
                return false;
            }

            foreach (RoomNode node in nodes)
            {
                foreach (RoomDoorway doorway in node.Doorways)
                {
                    if (doorway == null || doorway.Source != node || doorway.Destination == null ||
                        !nodeSet.Contains(doorway.Destination) || doorway.DestinationEntryPoint == null)
                    {
                        error = $"Room {node.RoomId} has an invalid doorway connection.";
                        return false;
                    }

                    if (!doorway.AllowsOneWay && !doorway.Destination.HasConnectionTo(node))
                    {
                        error = $"Connection {node.RoomId} -> {doorway.Destination.RoomId} has no return path.";
                        return false;
                    }
                }
            }

            error = null;
            return true;
        }

        private void ActivateOnly(RoomNode activeNode)
        {
            foreach (RoomNode node in nodes)
            {
                node.SetVisible(node == activeNode);
            }
        }

        private static void MovePlayer(PlayerMovement movingPlayer, Vector2 destination)
        {
            // Hitbox-1: entry points are where the player stands; a larger body's root sits above its feet.
            destination = movingPlayer.RootPositionForStanding(destination);
            Rigidbody2D body = movingPlayer.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                // Teleport both physics and presentation. Otherwise interpolation can render the old
                // position for the first destination frame, inside the new camera's edge.
                RigidbodyInterpolation2D interpolation = body.interpolation;
                body.interpolation = RigidbodyInterpolation2D.None;
                body.linearVelocity = Vector2.zero;
                body.position = destination;
                Vector3 position = movingPlayer.transform.position;
                position.x = destination.x;
                position.y = destination.y;
                movingPlayer.transform.position = position;
                body.interpolation = interpolation;
            }
            else
            {
                movingPlayer.transform.position = destination;
            }
        }

        private static void ClearTransientProjectiles()
        {
            foreach (Projectile projectile in FindObjectsByType<Projectile>(FindObjectsSortMode.None))
            {
                if (projectile.IsLaunched)
                {
                    projectile.StopAtBoundary();
                }
            }

            foreach (HomingSkillProjectile projectile in
                     FindObjectsByType<HomingSkillProjectile>(FindObjectsSortMode.None))
            {
                if (projectile.IsLaunched)
                {
                    projectile.StopAtBoundary();
                }
            }

            foreach (BossProjectile projectile in FindObjectsByType<BossProjectile>(FindObjectsSortMode.None))
            {
                if (projectile.IsLaunched)
                {
                    projectile.StopAtBoundary();
                }
            }

            foreach (EnemyProjectile projectile in FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None))
            {
                if (projectile.IsLaunched)
                {
                    projectile.StopAtBoundary();
                }
            }
        }
    }
}
