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

        private float nextTransitionTime;

        public RoomNode CurrentNode { get; private set; }
        public IReadOnlyList<RoomNode> Nodes => nodes;

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
            if (!TryValidateConfiguration(out string error))
            {
                Debug.LogError($"{name}: Invalid room graph. {error}", this);
                enabled = false;
                return;
            }

            ActivateOnly(startingNode);
            CurrentNode = startingNode;
            CurrentNode.MarkVisited();
            roomCamera?.ShowRoom(CurrentNode.CameraAnchor);
            runProgress?.RecordRoomEntry(CurrentNode.FloorNumber, CurrentNode.RoomNumber);
        }

        public bool TryTransition(
            RoomNode source,
            RoomNode destination,
            Transform destinationEntryPoint,
            PlayerMovement transitioningPlayer)
        {
            if (!enabled || source == null || destination == null || transitioningPlayer == null ||
                source != CurrentNode || !source.HasConnectionTo(destination) ||
                transitioningPlayer.GetComponent<PlayerActionState>()?.CanTransition == false ||
                Time.unscaledTime < nextTransitionTime)
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

            ClearTransientProjectiles();
            destination.SetVisible(true);
            MovePlayer(transitioningPlayer, entryPoint.position);
            source.SetVisible(false);

            CurrentNode = destination;
            CurrentNode.MarkVisited();
            roomCamera?.ShowRoom(CurrentNode.CameraAnchor);
            runProgress?.RecordRoomEntry(CurrentNode.FloorNumber, CurrentNode.RoomNumber);
            nextTransitionTime = Time.unscaledTime + transitionCooldown;
            return true;
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

                    if (!doorway.Destination.HasConnectionTo(node))
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
            Rigidbody2D body = movingPlayer.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.position = destination;
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
        }
    }
}
