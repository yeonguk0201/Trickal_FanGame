using System.Collections.Generic;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Frontend;
using TrickalFanGame.Player;
using TrickalFanGame.Run;
using UnityEngine;

namespace TrickalFanGame.Room
{
    public enum RoomTrapKind
    {
        FloorSpikes,
        PeriodicSpikes,
        Fire,
        DamagePuddle,
        Spiderweb,
        SpikedRock,
        PoisonMushroom,
        // Trap-2: appended so the serialized values of the seven kinds above stay the same.
        MudCake,
    }

    public enum PeriodicSpikeState
    {
        Lowered,
        Raised,
    }

    // Trap-0: one authored room trap on a 1x1 cell. Every trap hit takes half a heart from the player and shares the
    // player's post-hit invulnerability window, so a hit from an enemy and a trap never chain.
    // - Floor kinds (바닥 가시, 주기 가시, 장판, 거미줄) have no collider. The hit area is the ellipse the drawing
    //   covers, which is smaller than the cell. The player stands on one when any part of the line from the feet
    //   center down to the soles is inside it, so stepping onto the drawing from above hits as soon as the drawn
    //   feet are on it. An enemy uses the same line from its feet (EnemyFeet.GroundCircle). Flying bodies skip it.
    // - Obstacle kinds (불, 가시 돌, 독버섯) sit on a fixed-kind DestructibleObstacle, which gives them the hit count,
    //   the Environment collider that enemies path around and the broken state kept across revisits. 불 and 가시 돌
    //   hurt only the player, on contact.
    // - 진흙케이크 (Trap-2) is a one-use floor kind. The first grounded body whose feet reach the drawing bursts it:
    //   everyone on the cake then takes the trap hit once, and the burst leaves a mud puddle (the last burst frame)
    //   that slows grounded bodies on it until it fades. A bomb or one player attack bursts it without the hit. A
    //   burst cake never returns on a revisit.
    // Traps keep working after the room is cleared.
    [DisallowMultipleComponent]
    public sealed class RoomTrap : MonoBehaviour
    {
        public const float CellSize = 1f;
        // The same trap hurts the same body again after this long.
        public const float HitInterval = 1f;
        // Fixed, regardless of the floor's enemy health scaling.
        public const float EnemyDamage = 5f;
        public const float PeriodicLoweredSeconds = 2f;
        public const float PeriodicRaisedSeconds = 2f;
        public const float WebSpeedMultiplier = 0.6f;
        // The slow is renewed every tick and ends this long after the body leaves the web.
        public const float WebSlowLinger = 0.15f;
        public const int FireHits = 3;
        public const int SpikedRockHits = DestructibleObstacle.DefaultRequiredHits;
        public const int PoisonMushroomHits = 2;
        // Effect-0: bubbles rising in a 독 장판 cell (visual only).
        public const int PuddleBubbleCount = 3;
        public const string FireVariantId = "fire";
        public const string SpikedRockVariantId = "spiked-rock";
        public const string PoisonMushroomVariantId = "poison-mushroom";
        // Trap-2 진흙케이크 (user decisions 2026-11-16). The cake and its first three burst frames are drawn narrower
        // than the cell, but stepping is judged on the whole cell. The last burst frame (the puddle) then spreads
        // around its own center until it is drawn as if fitted one cell wide, and the slow area is that grown
        // puddle. Trap2MudCakeVerification measures the puddle against the sheet.
        public const float MudCakeWidth = 0.65f;
        public static readonly Vector2 MudCakeHitSize = Vector2.one * CellSize;
        public const float MudSpeedMultiplier = 0.7f;
        public const float MudPuddleSeconds = 4f;
        public const float MudPuddleFadeSeconds = 0.5f;
        public const float MudPuddleGrowSeconds = 0.3f;
        // The puddle drawn per unit of drawing width: its visible size and center under the cell center.
        public static readonly Vector2 MudPuddleSizePerWidth = new(1.08f, 0.67f);
        public static readonly Vector2 MudPuddleOffsetPerWidth = new(0f, -0.26f);
        // The grown puddle keeps the center it had at the cake's width and reaches the one-cell-wide size.
        public static Vector2 MudPuddleSize => MudPuddleSizePerWidth;
        public static Vector2 MudPuddleOffset => MudPuddleOffsetPerWidth * MudCakeWidth;
        public static float MudPuddleGrownScale => 1f / MudCakeWidth;
        public const string DeathReason = "HAZARD";
        public const string EnemyLayerName = "Enemy";

        [SerializeField] private string trapId = "trap-01";
        [SerializeField] private RoomTrapKind kind;
        [Tooltip("주기 가시의 B 위상입니다. 켜면 A 위상과 반 주기 어긋나 올라온 상태로 시작합니다.")]
        [SerializeField] private bool secondPhase;
        [SerializeField] private SpriteRenderer visual;
        [Tooltip("바닥형 함정의 그림이 차지하는 크기(칸 단위)입니다. 이 크기에 내접한 타원 안에 발 중심이 들어오면 밟은 것입니다.")]
        [SerializeField] private Vector2 hitSize = Vector2.one;

        private readonly Dictionary<Health, float> nextHitTimes = new();
        private readonly List<Collider2D> enemyHits = new();
        private readonly HashSet<Health> tickedEnemies = new();
        private RoomRunState runState;
        private PlayerMovement player;
        private Health playerHealth;
        private DestructibleObstacle obstacle;
        private float cycleStartTime;
        private float burstTime = float.NaN;

        public string TrapId => trapId;
        public RoomTrapKind Kind => kind;
        public bool SecondPhase => secondPhase;
        public SpriteRenderer Visual => visual;
        public Vector2 HitSize => hitSize;
        public bool IsFloorTrap => IsFloorKind(kind);
        // A 거미줄 a bomb removed.
        public bool IsRemoved { get; private set; }
        // The gas the last break of this 독버섯 left, until it ends.
        public PoisonGasCloud LastGas { get; private set; }
        public ExtinguishedFireArtwork FireRemnant { get; private set; }
        // 진흙케이크: burst (by a step or a bomb) and when, for the burst frames and the puddle.
        public bool HasBurst => !float.IsNaN(burstTime);
        public float BurstTime => burstTime;

        public static bool IsFloorKind(RoomTrapKind kind) =>
            kind is RoomTrapKind.FloorSpikes or RoomTrapKind.PeriodicSpikes or RoomTrapKind.DamagePuddle or
                RoomTrapKind.Spiderweb or RoomTrapKind.MudCake;

        public static string ArtworkIdOf(RoomTrapKind kind) => kind switch
        {
            RoomTrapKind.FloorSpikes => "trap-floor-spikes",
            RoomTrapKind.PeriodicSpikes => "trap-periodic-spikes",
            RoomTrapKind.Fire => "trap-fire",
            RoomTrapKind.DamagePuddle => "trap-damage-puddle",
            RoomTrapKind.Spiderweb => "trap-spiderweb",
            RoomTrapKind.SpikedRock => "trap-spiked-rock",
            RoomTrapKind.MudCake => "trap-mud-cake",
            _ => "trap-poison-mushroom",
        };

        // How wide the drawing is fitted on its cell. Every kind fills the cell except the smaller 진흙케이크.
        public static float ArtworkWidthOf(RoomTrapKind kind) => kind == RoomTrapKind.MudCake ? MudCakeWidth : CellSize;

        // The fixed obstacle kind under an obstacle trap; null for a floor trap.
        public static string ObstacleVariantIdOf(RoomTrapKind kind) => kind switch
        {
            RoomTrapKind.Fire => FireVariantId,
            RoomTrapKind.SpikedRock => SpikedRockVariantId,
            RoomTrapKind.PoisonMushroom => PoisonMushroomVariantId,
            _ => null,
        };

        public static int RequiredHitsOf(RoomTrapKind kind) => kind switch
        {
            RoomTrapKind.Fire => FireHits,
            RoomTrapKind.SpikedRock => SpikedRockHits,
            RoomTrapKind.PoisonMushroom => PoisonMushroomHits,
            _ => 0,
        };

        // 불 stops a flying player; 가시 돌 and 독버섯 are flown over.
        public static bool BlocksFlightOf(RoomTrapKind kind) => kind == RoomTrapKind.Fire;

        public void Configure(string configuredTrapId, RoomTrapKind configuredKind, bool configuredSecondPhase,
            SpriteRenderer configuredVisual)
        {
            trapId = configuredTrapId;
            kind = configuredKind;
            secondPhase = configuredSecondPhase;
            visual = configuredVisual;
            SubscribeFireBreak();
        }

        // The size of the drawing on its cell, so the hit area follows the artwork.
        public void ConfigureHitSize(Vector2 configuredHitSize) => hitSize = configuredHitSize;

        // A placed copy of a trap Prefab: its own ID (shared with its obstacle) and, for 주기 가시, its phase.
        public void ConfigurePlacement(string configuredTrapId, bool configuredSecondPhase)
        {
            trapId = configuredTrapId;
            secondPhase = configuredSecondPhase;
            if (TryGetComponent(out DestructibleObstacle ownObstacle)) ownObstacle.ConfigureObstacleId(configuredTrapId);
        }

        public bool TryValidate(out string error)
        {
            if (!StableRoomId.TryValidate(trapId, "Trap", out error)) return false;
            DestructibleObstacle ownObstacle = GetComponent<DestructibleObstacle>();
            if (IsFloorKind(kind))
            {
                if (ownObstacle != null || GetComponentInChildren<Collider2D>(true) != null)
                {
                    error = $"Floor trap '{trapId}' must not carry a collider or an obstacle; it never blocks movement.";
                    return false;
                }

                if (hitSize.x <= 0f || hitSize.y <= 0f || hitSize.x > CellSize + 0.001f || hitSize.y > CellSize + 0.001f)
                {
                    error = $"Floor trap '{trapId}' needs a hit size inside its cell, not {hitSize}.";
                    return false;
                }

                error = null;
                return true;
            }

            if (ownObstacle == null || ownObstacle.ObstacleId != trapId ||
                ownObstacle.VariantId != ObstacleVariantIdOf(kind) || ownObstacle.RequiredHits != RequiredHitsOf(kind) ||
                ownObstacle.BlocksFlight != BlocksFlightOf(kind) || ownObstacle.BurnHits != 0)
            {
                error = $"Obstacle trap '{trapId}' ({kind}) needs a DestructibleObstacle with the same ID, the kind " +
                        $"'{ObstacleVariantIdOf(kind)}', {RequiredHitsOf(kind)} hits and its flight rule.";
                return false;
            }

            if (kind != RoomTrapKind.SpikedRock && ownObstacle.DropTable != null)
            {
                error = $"Obstacle trap '{trapId}' ({kind}) must not drop anything.";
                return false;
            }

            return ownObstacle.TryValidate(out error);
        }

        // Called when the room is built, after its obstacles are bound. A web removed or a 진흙케이크 burst on an
        // earlier visit stays gone.
        public void Bind(RoomRunState configuredState, PlayerMovement configuredPlayer, float currentTime)
        {
            runState = configuredState;
            player = configuredPlayer;
            playerHealth = configuredPlayer != null ? configuredPlayer.GetComponent<Health>() : null;
            obstacle = GetComponent<DestructibleObstacle>();
            SubscribeFireBreak();
            RegisterMudCake();
            if (kind == RoomTrapKind.Fire && obstacle != null && obstacle.IsBroken)
                EnsureFireRemnant(currentTime, true);
            RestartCycle(currentTime);
            if (kind is RoomTrapKind.Spiderweb or RoomTrapKind.MudCake && runState != null &&
                runState.IsTrapRemoved(trapId))
            {
                IsRemoved = true;
                gameObject.SetActive(false);
            }

            if (kind == RoomTrapKind.PoisonMushroom && obstacle != null)
            {
                obstacle.Broken -= OnMushroomBroken;
                obstacle.Broken += OnMushroomBroken;
            }
        }

        // The room content is switched off while the player is elsewhere, so entering the room restarts the cycle.
        private void OnEnable()
        {
            GroundShadow.AttachDuringPlay(gameObject);
            if (kind == RoomTrapKind.DamagePuddle)
                PoisonPuddleBubbles.Attach(transform, hitSize * 0.5f, PuddleBubbleCount, visual);
            RestartCycle(Time.time);
            SubscribeFireBreak();
            RegisterMudCake();
        }

        private void SubscribeFireBreak()
        {
            if (obstacle == null) obstacle = GetComponent<DestructibleObstacle>();
            if (obstacle == null) return;
            obstacle.Broken -= OnFireBroken;
            if (kind == RoomTrapKind.Fire) obstacle.Broken += OnFireBroken;
        }

        private void OnDestroy()
        {
            LiveMudCakes.Remove(this);
            if (obstacle != null) obstacle.Broken -= OnMushroomBroken;
            if (obstacle != null) obstacle.Broken -= OnFireBroken;
            if (FireRemnant != null)
            {
                if (Application.isPlaying) Destroy(FireRemnant.gameObject);
                else DestroyImmediate(FireRemnant.gameObject);
            }
        }

        public void RestartCycle(float currentTime)
        {
            cycleStartTime = currentTime;
            nextHitTimes.Clear();
        }

        public PeriodicSpikeState StateAt(float currentTime)
        {
            if (kind != RoomTrapKind.PeriodicSpikes) return PeriodicSpikeState.Raised;
            float elapsed = Mathf.Max(0f, currentTime - cycleStartTime) + (secondPhase ? PeriodicLoweredSeconds : 0f);
            float phase = Mathf.Repeat(elapsed, PeriodicLoweredSeconds + PeriodicRaisedSeconds);
            return phase >= PeriodicLoweredSeconds ? PeriodicSpikeState.Raised : PeriodicSpikeState.Lowered;
        }

        private void Update()
        {
            if (Time.timeScale <= 0f) return;
            Tick(Time.time);
        }

        // One step of a floor trap: everything standing on the cell is hurt or slowed. Obstacle traps act on contact.
        public void Tick(float currentTime)
        {
            if (IsRemoved || !IsFloorKind(kind)) return;
            if (kind == RoomTrapKind.MudCake)
            {
                TickMudCake(currentTime);
                return;
            }

            PeriodicSpikeState state = StateAt(currentTime);
            RefreshPeriodicArtwork(state);
            if (state != PeriodicSpikeState.Raised) return;

            Vector2 center = transform.position;
            if (player != null && playerHealth != null && !IsFlying(player) && ContainsFeet(player.FeetPosition))
            {
                Affect(playerHealth, true, currentTime);
            }

            ContactFilter2D filter = new()
            {
                useLayerMask = true,
                layerMask = LayerMask.GetMask(EnemyLayerName),
                useTriggers = true,
            };
            Physics2D.OverlapBox(center, hitSize, 0f, filter, enemyHits);
            tickedEnemies.Clear();
            foreach (Collider2D hit in enemyHits)
            {
                Health enemy = hit != null ? hit.GetComponentInParent<Health>() : null;
                if (enemy == null || enemy == playerHealth || enemy.IsDead || !tickedEnemies.Add(enemy) ||
                    enemy.GetComponent<EnemyFlight>() != null)
                {
                    continue;
                }

                // Like the player: the line from the feet center down to the drawn soles.
                EnemyFeet.GroundCircle(enemy.gameObject, out Vector2 feetCenter, out float feetRadius);
                if (!ContainsFeet(feetCenter, feetRadius)) continue;

                Affect(enemy, false, currentTime);
            }
        }

        private void TickMudCake(float currentTime)
        {
            if (!HasBurst)
            {
                if (IsPlayerOn(transform.position, hitSize) || GroundedEnemiesOn(transform.position, hitSize).Count > 0)
                    Burst(currentTime, true);
                return;
            }

            float elapsed = currentTime - burstTime;
            if (elapsed >= MudPuddleSeconds)
            {
                IsRemoved = true;
                gameObject.SetActive(false);
                return;
            }

            if (visual != null)
            {
                Color color = visual.color;
                color.a = Mathf.Clamp01((MudPuddleSeconds - elapsed) / MudPuddleFadeSeconds);
                visual.color = color;
                if (visual.transform != transform)
                {
                    float scale = MudPuddleScaleAt(elapsed);
                    visual.transform.localScale = new Vector3(scale, scale, 1f);
                    visual.transform.localPosition = (Vector3)(MudPuddleOffset * (1f - scale));
                }
            }

            Vector2 puddle = (Vector2)transform.position + MudPuddleOffset;
            float until = currentTime + WebSlowLinger;
            if (IsPlayerOn(puddle, MudPuddleSize))
                EnvironmentSlow.Apply(player.gameObject, MudSpeedMultiplier, currentTime, until);
            foreach (Health enemy in GroundedEnemiesOn(puddle, MudPuddleSize))
                EnvironmentSlow.Apply(enemy.gameObject, MudSpeedMultiplier, currentTime, until);
        }

        // The drawing's scale after the burst: 1 through the first three frames, then the puddle frame eases out to
        // its grown size.
        public static float MudPuddleScaleAt(float elapsed)
        {
            float start = (EnvironmentHazardArtwork.MudCakeBurstFrames - 1) * EnvironmentHazardArtwork.MudCakeBurstFrameSeconds;
            float t = Mathf.Clamp01((elapsed - start) / MudPuddleGrowSeconds);
            t = 1f - (1f - t) * (1f - t);
            return Mathf.Lerp(1f, MudPuddleGrownScale, t);
        }

        // Player attacks (2026-11-21): like an obstacle, one hit of a basic attack, a shot, a skill explosion or the
        // ultimate impact bursts an intact cake, without the trap hit (as a bomb). The attack area reaches the cake
        // when it overlaps the cake's hit ellipse grown by the area's radius. A shot is used up, as on an obstacle.
        public static int BurstMudCakesByAttackInCircle(Vector2 center, float radius, float currentTime)
        {
            if (LiveMudCakes.Count == 0) return 0;
            LiveMudCakes.RemoveWhere(trap => trap == null);
            int burst = 0;
            // Burst never changes the set.
            foreach (RoomTrap trap in LiveMudCakes)
            {
                if (!trap.isActiveAndEnabled || trap.IsRemoved || trap.HasBurst) continue;
                Vector2 offset = center - (Vector2)trap.transform.position;
                float x = offset.x / (trap.hitSize.x * 0.5f + radius);
                float y = offset.y / (trap.hitSize.y * 0.5f + radius);
                if (x * x + y * y > 1f) continue;
                if (trap.Burst(currentTime, false)) burst++;
            }

            return burst;
        }

        private static readonly HashSet<RoomTrap> LiveMudCakes = new();

        private void RegisterMudCake()
        {
            if (kind == RoomTrapKind.MudCake) LiveMudCakes.Add(this);
            else LiveMudCakes.Remove(this);
        }

        // Bursts a 진흙케이크 once. A step hurts everything standing on the cake; a bomb only bursts it.
        public bool Burst(float currentTime, bool hurt)
        {
            if (kind != RoomTrapKind.MudCake || IsRemoved || HasBurst) return false;
            burstTime = currentTime;
            runState?.TryMarkTrapRemoved(trapId);
            if (!hurt) return true;
            if (IsPlayerOn(transform.position, hitSize)) TryDamagePlayer(playerHealth, currentTime);
            foreach (Health enemy in new List<Health>(GroundedEnemiesOn(transform.position, hitSize)))
                TryDamageEnemy(enemy, currentTime);
            return true;
        }

        private bool IsPlayerOn(Vector2 center, Vector2 size) =>
            player != null && playerHealth != null && !playerHealth.IsDead && !IsFlying(player) &&
            ContainsFeetIn(player.FeetPosition, PlayerFeet.Radius, center, size);

        // Living grounded enemies whose feet line reaches the ellipse inscribed in size around center.
        private HashSet<Health> GroundedEnemiesOn(Vector2 center, Vector2 size)
        {
            ContactFilter2D filter = new()
            {
                useLayerMask = true,
                layerMask = LayerMask.GetMask(EnemyLayerName),
                useTriggers = true,
            };
            Physics2D.OverlapBox(center, size + Vector2.one, 0f, filter, enemyHits);
            tickedEnemies.Clear();
            foreach (Collider2D hit in enemyHits)
            {
                Health enemy = hit != null ? hit.GetComponentInParent<Health>() : null;
                if (enemy == null || enemy == playerHealth || enemy.IsDead || tickedEnemies.Contains(enemy) ||
                    enemy.GetComponent<EnemyFlight>() != null)
                {
                    continue;
                }

                EnemyFeet.GroundCircle(enemy.gameObject, out Vector2 feetCenter, out float feetRadius);
                if (ContainsFeetIn(feetCenter, feetRadius, center, size)) tickedEnemies.Add(enemy);
            }

            return tickedEnemies;
        }

        private void Affect(Health target, bool isPlayer, float currentTime)
        {
            if (kind == RoomTrapKind.Spiderweb)
            {
                EnvironmentSlow.Apply(target.gameObject, WebSpeedMultiplier, currentTime,
                    currentTime + WebSlowLinger);
                return;
            }

            if (isPlayer) TryDamagePlayer(target, currentTime);
            else TryDamageEnemy(target, currentTime);
        }

        // Half a heart. A player inside the post-hit window is skipped without starting this trap's interval, so
        // the trap hurts as soon as the window ends.
        public bool TryDamagePlayer(Health target, float currentTime)
        {
            if (!isActiveAndEnabled || IsRemoved ||
                (TryGetComponent(out DestructibleObstacle ownObstacle) && ownObstacle.IsBroken)) return false;
            if (target == null || target.IsDead || target.IsInvulnerable || IsWaiting(target, currentTime))
                return false;
            float before = target.CurrentHealth + target.CurrentShield;
            target.GetComponent<PlayerDeathReason>()?.SetReason(DeathReason);
            target.TakeDamage(CreatePlayerDamage(gameObject));
            nextHitTimes[target] = currentTime + HitInterval;
            return target.CurrentHealth + target.CurrentShield < before;
        }

        // The trap is the damage source, so a kill counts for the room but is not credited to the player.
        public bool TryDamageEnemy(Health target, float currentTime)
        {
            if (target == null || target.IsDead || IsWaiting(target, currentTime)) return false;
            target.TakeDamage(new DamageContext(gameObject, DamageSourceType.Environment, EnemyDamage));
            nextHitTimes[target] = currentTime + HitInterval;
            return true;
        }

        public static DamageContext CreatePlayerDamage(GameObject source) =>
            new(source, DamageSourceType.Environment, HealthUnits.HalfHeartDamageUnits, allowsHalfHeart: true);

        private bool IsWaiting(Health target, float currentTime) =>
            nextHitTimes.TryGetValue(target, out float nextTime) && currentTime < nextTime;

        private void OnCollisionEnter2D(Collision2D collision) => HandlePlayerContact(collision);

        private void OnCollisionStay2D(Collision2D collision) => HandlePlayerContact(collision);

        // 불 and 가시 돌 hurt the player who touches them. A flying player never collides with a 가시 돌.
        private void HandlePlayerContact(Collision2D collision)
        {
            if (kind is not (RoomTrapKind.Fire or RoomTrapKind.SpikedRock)) return;
            PlayerMovement touching = collision.collider.GetComponentInParent<PlayerMovement>();
            if (touching != null) TryDamagePlayer(touching.GetComponent<Health>(), Time.time);
        }

        // 샤샤의 항아리: one water stream hit puts a 불 out, whatever its remaining hits.
        public bool TryExtinguish()
        {
            if (kind != RoomTrapKind.Fire) return false;
            if (obstacle == null) obstacle = GetComponent<DestructibleObstacle>();
            return obstacle != null && obstacle.TryDestroyByBomb();
        }

        private void OnMushroomBroken(DestructibleObstacle broken)
        {
            LastGas = PoisonGasCloud.Spawn(transform.position, transform.parent, player, Time.time);
        }

        private void OnFireBroken(DestructibleObstacle broken) => EnsureFireRemnant(Time.time, false);

        private void EnsureFireRemnant(float currentTime, bool alreadyCold)
        {
            if (FireRemnant != null) return;
            float width = TryGetComponent(out FairyKingdomArtworkView view) ? view.LocalWidth : CellSize;
            FireRemnant = ExtinguishedFireArtwork.Spawn(transform, visual, width, currentTime, alreadyCold);
        }

        // A 거미줄 survives attacks and being walked through; only a bomb removes it. The same blast bursts a
        // 진흙케이크 without its hit (not counted).
        public static int RemoveWebsByBombInCircle(Vector2 center, float radius)
        {
            int removed = 0;
            foreach (RoomTrap trap in FindObjectsByType<RoomTrap>(FindObjectsSortMode.None))
            {
                if (trap.kind == RoomTrapKind.MudCake)
                {
                    if (CircleOverlapsCell(trap.transform.position, center, radius)) trap.Burst(Time.time, false);
                    continue;
                }

                if (trap.kind != RoomTrapKind.Spiderweb || trap.IsRemoved ||
                    !CircleOverlapsCell(trap.transform.position, center, radius))
                {
                    continue;
                }

                trap.runState?.TryMarkTrapRemoved(trap.trapId);
                trap.IsRemoved = true;
                trap.gameObject.SetActive(false);
                removed++;
            }

            return removed;
        }

        public void RefreshPeriodicArtwork(PeriodicSpikeState state)
        {
            if (kind != RoomTrapKind.PeriodicSpikes || visual == null) return;
            float width = TryGetComponent(out FairyKingdomArtworkView view) ? view.LocalWidth : CellSize;
            Sprite sprite = state == PeriodicSpikeState.Raised
                ? FairyKingdomArtwork.Fit(ArtworkIdOf(kind), width)
                : EnvironmentHazardArtwork.Fit(EnvironmentHazardArtwork.Spikes, 0f, width);
            if (sprite != null) visual.sprite = sprite;
            visual.color = Color.white;
        }

        private static bool IsFlying(PlayerMovement body) =>
            body.TryGetComponent(out PlayerFlight flight) && flight.IsFlying;

        // The player's sprite stands on the bottom of the feet circle, a feet radius under the feet center. Either
        // point on the drawing counts, and so does the drawing passing between them.
        public bool ContainsFeet(Vector2 feetCenter) => ContainsFeet(feetCenter, PlayerFeet.Radius);

        public bool ContainsFeet(Vector2 feetCenter, float feetRadius) =>
            ContainsFeetIn(feetCenter, feetRadius, transform.position, hitSize);

        // The same feet line against any ellipse; 진흙케이크 uses it for its puddle.
        public static bool ContainsFeetIn(Vector2 feetCenter, float feetRadius, Vector2 center, Vector2 size)
        {
            Vector2 offset = feetCenter - center;
            float x = offset.x / (size.x * 0.5f);
            if (Mathf.Abs(x) > 1f) return false;
            float halfHeight = size.y * 0.5f * Mathf.Sqrt(1f - x * x);
            return offset.y - feetRadius <= halfHeight && offset.y >= -halfHeight;
        }

        // Whether a ground point is on the drawing: inside the ellipse inscribed in the hit size.
        public bool Contains(Vector2 point)
        {
            Vector2 offset = point - (Vector2)transform.position;
            float x = offset.x / (hitSize.x * 0.5f);
            float y = offset.y / (hitSize.y * 0.5f);
            return x * x + y * y <= 1f;
        }

        public static bool CircleOverlapsCell(Vector2 cellCenter, Vector2 circleCenter, float radius)
        {
            float x = Mathf.Max(0f, Mathf.Abs(circleCenter.x - cellCenter.x) - CellSize * 0.5f);
            float y = Mathf.Max(0f, Mathf.Abs(circleCenter.y - cellCenter.y) - CellSize * 0.5f);
            return x * x + y * y <= radius * radius;
        }
    }
}
