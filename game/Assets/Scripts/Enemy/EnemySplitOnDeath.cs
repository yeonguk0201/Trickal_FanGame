using System;
using System.Collections.Generic;
using TrickalFanGame.Combat;
using TrickalFanGame.Room;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    // Enemy-9: an enemy that splits when it dies (누루링-요정전사). Whatever killed it, the split enemies appear to
    // its left and right and join its room as extra enemies, so the wave and the room clear only after they die too.
    // The splits wait out the shared spawn delay before acting and never split again.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public sealed class EnemySplitOnDeath : MonoBehaviour
    {
        public const int DefaultSplitCount = 2;
        public const float DefaultSideOffset = 0.5f;

        [SerializeField] private GameObject splitPrefab;
        [SerializeField, Min(1)] private int splitCount = DefaultSplitCount;
        [SerializeField, Min(0f)] private float sideOffset = DefaultSideOffset;

        private Health health;
        private bool hasSplit;

        public GameObject SplitPrefab => splitPrefab;
        public int SplitCount => splitCount;
        public float SideOffset => sideOffset;
        public IReadOnlyList<Health> LastSplits { get; private set; } = Array.Empty<Health>();
        public event Action<IReadOnlyList<Health>> SplitSpawned;

        public void Configure(GameObject configuredSplitPrefab, int configuredSplitCount, float configuredSideOffset)
        {
            splitPrefab = configuredSplitPrefab;
            splitCount = Mathf.Max(1, configuredSplitCount);
            sideOffset = Mathf.Max(0f, configuredSideOffset);
        }

        // Spread evenly along the horizontal line through the body: two splits sit sideOffset to the left and right.
        public static Vector3 SplitOffset(int index, int count, float offset)
        {
            if (count <= 1) return Vector3.zero;
            float step = offset * 2f / (count - 1);
            return Vector3.right * (-offset + step * index);
        }

        // Subscribes before the room registers this enemy (the room does so after Instantiate), so the splits join
        // the room before the room counts this death.
        private void Awake()
        {
            health = GetComponent<Health>();
            health.Died -= OnDied;
            health.Died += OnDied;
        }

        private void OnDestroy()
        {
            if (health != null) health.Died -= OnDied;
        }

        private void OnDied()
        {
            if (hasSplit || splitPrefab == null) return;
            hasSplit = true;
            RoomController room = GetComponentInParent<RoomController>();
            EnemyBehaviorContext own = GetComponent<EnemyBehaviorContext>();
            Transform target = own != null ? own.Target : null;
            List<Health> splits = new(splitCount);
            for (int index = 0; index < splitCount; index++)
            {
                Vector3 position = transform.position + SplitOffset(index, splitCount, sideOffset);
                Health split = room != null
                    ? room.SpawnExtraEnemy(splitPrefab, position)
                    : SpawnWithoutRoom(position);
                if (split == null) continue;
                // A split never splits again, even if its Prefab was given this component by mistake.
                EnemySplitOnDeath nested = split.GetComponent<EnemySplitOnDeath>();
                if (nested != null) nested.hasSplit = true;
                Engage(split, target);
                splits.Add(split);
            }

            LastSplits = splits;
            SplitSpawned?.Invoke(LastSplits);
        }

        // Outside a room (a test or a loose spawn) the splits keep this enemy's floor.
        private Health SpawnWithoutRoom(Vector3 position)
        {
            GameObject instance = Instantiate(splitPrefab, position, Quaternion.identity, transform.parent);
            Health split = instance.GetComponent<Health>();
            if (split == null)
            {
                Debug.LogError($"{name}: Split Prefab must have a Health component.", instance);
                if (Application.isPlaying) Destroy(instance);
                else DestroyImmediate(instance);
                return null;
            }

            FloorDifficultyScaler.ApplyScaling(instance, EnemyFloorLevel.Resolve(gameObject));
            return split;
        }

        // The splits chase this enemy's target at once, but stand still and harmless through the shared spawn delay
        // (0.3 s before acting). The room already did this during play; repeating it is harmless and also covers a
        // split that has not run Awake yet.
        private static void Engage(Health split, Transform target)
        {
            EnemyBehaviorContext behavior = split.GetComponent<EnemyBehaviorContext>();
            if (behavior == null) return;
            behavior.Initialize();
            if (!behavior.IsAlerted) behavior.BeginCombat(target);
            if (!behavior.IsAlerted) behavior.TryAcquireOrAlert(float.PositiveInfinity);
            behavior.BeginSpawnGrace(Time.time);
        }
    }
}
