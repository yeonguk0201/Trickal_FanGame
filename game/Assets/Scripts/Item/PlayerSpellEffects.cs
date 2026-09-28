using System;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using UnityEngine;

namespace TrickalFanGame.Item
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerStats))]
    public sealed class PlayerSpellEffects : MonoBehaviour
    {
        private PlayerStats stats;
        private RunProgress runProgress;
        private RoomRunState observedRoomState;
        private float pendingNextCombatRoomAttackDamagePercent;
        private float activeNextCombatRoomAttackDamagePercent;
        private float bossRoomAttackSpeedPercent;
        private float bossRoomMoveSpeedPercent;

        public float PendingNextCombatRoomAttackDamagePercent => pendingNextCombatRoomAttackDamagePercent;
        public string ActiveNextCombatRoomId { get; private set; }
        public bool IsBossRoomEffectActive { get; private set; }

        private void Awake()
        {
            stats = GetComponent<PlayerStats>();
        }

        private void OnEnable()
        {
            if (runProgress == null)
            {
                ConfigureRunProgress(FindFirstObjectByType<RunProgress>());
            }
        }

        private void OnDisable()
        {
            UnbindRunProgress();
            ClearCurrentRoomEffects();
        }

        public void ConfigureRunProgress(RunProgress configuredRunProgress)
        {
            if (runProgress == configuredRunProgress)
            {
                RefreshCurrentRoom(false);
                return;
            }

            UnbindRunProgress();
            runProgress = configuredRunProgress;
            if (runProgress != null)
            {
                runProgress.RoomChanged += HandleRoomChanged;
            }

            RefreshCurrentRoom(false);
        }

        public void AddNextCombatRoomAttackDamagePercent(float amount)
        {
            if (amount > 0f)
            {
                pendingNextCombatRoomAttackDamagePercent += amount;
            }
        }

        public void AddBossRoomAttackSpeedPercent(float amount)
        {
            if (amount <= 0f) return;
            bossRoomAttackSpeedPercent += amount;
            RefreshCurrentRoom(false);
        }

        public void AddBossRoomMoveSpeedPercent(float amount)
        {
            if (amount <= 0f) return;
            bossRoomMoveSpeedPercent += amount;
            RefreshCurrentRoom(false);
        }

        private void HandleRoomChanged(int _, int __)
        {
            RefreshCurrentRoom(true);
        }

        private void HandleRoomStateChanged()
        {
            RefreshCurrentRoom(false);
        }

        private void RefreshCurrentRoom(bool allowNextCombatConsumption)
        {
            if (stats == null) stats = GetComponent<PlayerStats>();

            GeneratedRoomNode node = ResolveCurrentNode();
            RoomRunState state = node != null ? runProgress?.GetRoomState(node.RoomId) : null;
            BindRoomState(state);

            bool isUnclearedCombatRoom = node != null && state != null && !state.IsCleared &&
                                         node.Role is GeneratedRoomRole.Intermediate or GeneratedRoomRole.Boss;
            if (!string.IsNullOrEmpty(ActiveNextCombatRoomId) &&
                (!string.Equals(ActiveNextCombatRoomId, node?.RoomId, StringComparison.Ordinal) ||
                 !isUnclearedCombatRoom))
            {
                ActiveNextCombatRoomId = null;
                activeNextCombatRoomAttackDamagePercent = 0f;
            }

            if (allowNextCombatConsumption && string.IsNullOrEmpty(ActiveNextCombatRoomId) &&
                pendingNextCombatRoomAttackDamagePercent > 0f && isUnclearedCombatRoom)
            {
                ActiveNextCombatRoomId = node.RoomId;
                activeNextCombatRoomAttackDamagePercent = pendingNextCombatRoomAttackDamagePercent;
                pendingNextCombatRoomAttackDamagePercent = 0f;
            }

            IsBossRoomEffectActive = isUnclearedCombatRoom && node.Role == GeneratedRoomRole.Boss;
            stats?.SetCurrentRoomAttackDamagePercent(activeNextCombatRoomAttackDamagePercent);
            stats?.SetCurrentRoomAttackSpeedPercent(
                IsBossRoomEffectActive ? bossRoomAttackSpeedPercent : 0f);
            stats?.SetCurrentRoomMoveSpeedPercent(
                IsBossRoomEffectActive ? bossRoomMoveSpeedPercent : 0f);
        }

        private GeneratedRoomNode ResolveCurrentNode()
        {
            if (runProgress?.GeneratedGraph == null || runProgress.CurrentFloor <= 0 ||
                runProgress.CurrentRoom <= 0)
            {
                return null;
            }

            string roomId = FloorGenerator.BuildRoomId(runProgress.CurrentFloor, runProgress.CurrentRoom);
            foreach (GeneratedRoomNode node in runProgress.GeneratedGraph.Nodes)
            {
                if (node != null && string.Equals(node.RoomId, roomId, StringComparison.Ordinal))
                {
                    return node;
                }
            }

            return null;
        }

        private void BindRoomState(RoomRunState state)
        {
            if (ReferenceEquals(observedRoomState, state)) return;
            if (observedRoomState != null) observedRoomState.Changed -= HandleRoomStateChanged;
            observedRoomState = state;
            if (observedRoomState != null) observedRoomState.Changed += HandleRoomStateChanged;
        }

        private void UnbindRunProgress()
        {
            if (runProgress != null) runProgress.RoomChanged -= HandleRoomChanged;
            runProgress = null;
            BindRoomState(null);
        }

        private void ClearCurrentRoomEffects()
        {
            ActiveNextCombatRoomId = null;
            activeNextCombatRoomAttackDamagePercent = 0f;
            IsBossRoomEffectActive = false;
            if (stats == null) return;
            stats.SetCurrentRoomAttackDamagePercent(0f);
            stats.SetCurrentRoomAttackSpeedPercent(0f);
            stats.SetCurrentRoomMoveSpeedPercent(0f);
        }
    }
}
