using System;
using System.Collections.Generic;
using System.Threading;
using CodenameVelvet.Audio;
using CodenameVelvet.Cameras;
using CodenameVelvet.Combat;
using CodenameVelvet.Core.Debugging;
using UnityEngine;

namespace CodenameVelvet.Enemies
{
    /// <summary>
    /// Cyclops (boss) brain. States:
    /// <list type="bullet">
    /// <item>Wander: walks to random points inside its zone (a trigger BoxCollider, or a radius around
    /// the spawn) and only picks points with ground below, so it never walks off the world.</item>
    /// <item>Rest: now and then stops and plays one of its idles at random (IdleVariant parameter).</item>
    /// <item>Chase: walks toward a detected player without leaving the zone.</item>
    /// <item>Attack: with a player close, picks one of the available attacks (by range, cooldown and weight),
    /// commits to it (it cannot be interrupted) and waits to recover.</item>
    /// </list>
    /// Same as <see cref="EnemyBrain"/>: one Awaitable loop per enable, cancelled on disable, and every timer
    /// uses Time.time (frozen while paused).
    /// </summary>
    [RequireComponent(typeof(EnemyTargetFinder))]
    public class CyclopsBrain : MonoBehaviour, IKnockbackable
    {
        private const string LogCategory = "Cyclops";

        [Header("Health")]
        [SerializeField, Min(1)] private int maxHp = 20;

        [Header("Zone")]
        [Tooltip("Trigger BoxCollider that bounds where it can walk. Empty = a radius around the spawn.")]
        [SerializeField] private BoxCollider wanderArea;
        [SerializeField, Min(0f)] private float fallbackWanderRadius = 8f;
        [Tooltip("Distance kept from the zone edge (usually about its body radius).")]
        [SerializeField, Min(0f)] private float areaMargin = 1.5f;
        [Tooltip("Layers that count as ground when checking a point is not over the void.")]
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField, Min(1)] private int maxPointAttempts = 12;

        [Header("Wander")]
        [SerializeField, Min(0f)] private float wanderSpeed = 1.8f;
        [Tooltip("Max length of each walk. Short walks = it arrives and rests often, even in a huge zone.")]
        [SerializeField, Min(1f)] private float maxWanderStep = 12f;
        [Tooltip("Chance (0-1) of stopping to play an idle at each point.")]
        [SerializeField, Range(0f, 1f)] private float restChance = 0.6f;
        [SerializeField] private Vector2 restTimeRange = new Vector2(2.5f, 6f);
        [Tooltip("Weight of each idle: index 0 = Idle state, 1 = Idle_V1.")]
        [SerializeField] private float[] idleWeights = { 1f, 1f };

        [Header("Chase")]
        [SerializeField, Min(0f)] private float chaseSpeed = 3.5f;
        [SerializeField, Min(0f)] private float detectionRange = 30f;
        [Tooltip("Stops chasing beyond this distance. Always >= detection range.")]
        [SerializeField, Min(0f)] private float loseInterestRange = 50f;
        [SerializeField, Min(0f)] private float retargetInterval = 0.25f;

        [Header("Attacks")]
        [SerializeField] private List<CyclopsAttack> attacks = new List<CyclopsAttack>();
        [Tooltip("Minimum pause between the end of an attack and the start of the next.")]
        [SerializeField, Min(0f)] private float timeBetweenAttacks = 0.6f;
        [Tooltip("When enabled, the hit lands when the clip fires the 'AnimationEvent_Impact' Animation Event (the windup is the safety limit).")]
        [SerializeField] private bool useAnimationEvents = false;

        [Header("Hits taken")]
        [Tooltip("0 = knockback pushes it fully, 1 = it does not move.")]
        [SerializeField, Range(0f, 1f)] private float knockbackResistance = 0.8f;
        [Tooltip("While attacking (recovery included) it cannot be pushed or stunned (super armor).")]
        [SerializeField] private bool superArmorWhileAttacking = true;
        [Tooltip("Called when it is dazed after its hit (no moving or attacking during the recovery): VFX, sound.")]
        [SerializeField] private UnityEngine.Events.UnityEvent onDazedStart = new UnityEngine.Events.UnityEvent();
        [SerializeField] private UnityEngine.Events.UnityEvent onDazedEnd = new UnityEngine.Events.UnityEvent();

        [Header("Animator and visuals")]
        [SerializeField] private Animator animator;
        [Tooltip("Child flipped with Scale.x to face where it goes.")]
        [SerializeField] private Transform spriteRoot;
        [Tooltip("Enable when the original art faces right.")]
        [SerializeField] private bool artFacesRight = false;

        [Header("Sound")]
        [Tooltip("Looping footsteps, played while walking.")]
        [SerializeField] private AudioSource footsteps;

        [Header("Debug")]
        [SerializeField] private bool debugLogs = true;
        [SerializeField] private bool drawGizmos = true;

        private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
        private static readonly int IsAttackingHash = Animator.StringToHash("IsAttacking");
        private static readonly int IdleVariantHash = Animator.StringToHash("IdleVariant");
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int MoveXHash = Animator.StringToHash("MoveX");
        private static readonly int MoveYHash = Animator.StringToHash("MoveY");

        private IEnemyMover mover;
        private EnemyTargetFinder targetFinder;
        private EnemyHealth health;
        private Collider body;
        private Rigidbody rb;
        private System.Random random;
        private CancellationTokenSource loopCancellation;
        private readonly Collider[] impactHits = new Collider[32];
        private readonly HashSet<IDamageable> alreadyHit = new HashSet<IDamageable>();
        private readonly HashSet<int> animatorParams = new HashSet<int>();

        private Vector3 spawnPosition;
        private bool hasStarted;
        private IDamageable target;
        private Transform targetTransform;
        private Collider targetCollider;
        private float nextSearchTime;
        private float nextAttackTime;
        private float stunnedUntil;
        private bool isAttacking;
        private bool isDazed;
        private bool impactSignaled;
        private Vector3 facing = Vector3.back; // toward the camera
        private bool restBeforeWandering = true; // rests first on start and after losing the player

        public bool IsAttacking => isAttacking;
        /// <summary>True while recovering from an attack: it stays still, no moving or attacking.</summary>
        public bool IsDazed => isDazed;
        /// <summary>True while chasing or attacking a player, i.e. the boss has seen someone.</summary>
        public bool HasTarget => targetTransform != null;

        private void Reset()
        {
            attacks = DefaultAttacks();
            animator = GetComponentInChildren<Animator>();
        }

        private void Awake()
        {
            mover = GetComponent<IEnemyMover>();
            targetFinder = GetComponent<EnemyTargetFinder>();
            health = GetComponent<EnemyHealth>();
            body = GetComponent<Collider>();
            rb = GetComponent<Rigidbody>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (attacks == null || attacks.Count == 0) attacks = DefaultAttacks();
            random = new System.Random(UnityEngine.Random.Range(int.MinValue, int.MaxValue));

            if (animator != null)
                foreach (var p in animator.parameters) animatorParams.Add(p.nameHash);

            if (health != null) health.Initialize(maxHp);
            else Debug.LogWarning($"{name}: CyclopsBrain has no EnemyHealth; it cannot take damage.", this);
        }

        private void Start()
        {
            spawnPosition = transform.position;
            hasStarted = true;
            if (debugLogs)
                VelvetLog.Info(LogCategory, this, $"Ready at {spawnPosition}, HP {maxHp}, zone {(wanderArea != null ? wanderArea.name : $"radius {fallbackWanderRadius}")}, {attacks.Count} attacks");
            StartLoop();
        }

        private void OnEnable() { if (hasStarted) StartLoop(); }

        private void OnValidate() => loseInterestRange = Mathf.Max(detectionRange, loseInterestRange);
        private void OnDisable() => StopLoop();

        private void Update()
        {
            if (animator == null) return;
            Vector3 v = rb != null ? rb.linearVelocity : Vector3.zero;
            v.y = 0f;
            bool moving = !isAttacking && v.sqrMagnitude > 0.01f; // the lunge moves it, but it stays in the attack animation
            SetBool(IsMovingHash, moving);
            footsteps.SetLoopPlaying(moving);
            SetBool(IsAttackingHash, isAttacking);
            SetFloat(SpeedHash, v.magnitude);
            if (moving)
            {
                SetFloat(MoveXHash, v.normalized.x);
                SetFloat(MoveYHash, v.normalized.z);
                Face(v);
            }
        }

        private void StartLoop()
        {
            StopLoop();
            if (mover == null)
            {
                Debug.LogError($"{name}: CyclopsBrain needs an IEnemyMover (e.g. RigidbodyEnemyMover).", this);
                return;
            }
            loopCancellation = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            RunLoop(loopCancellation.Token);
        }

        private void StopLoop()
        {
            if (loopCancellation == null) return;
            loopCancellation.Cancel();
            loopCancellation.Dispose();
            loopCancellation = null;
            ClearTarget();
            isAttacking = false;
            if (mover is Component c && c != null) mover.Stop();
        }

        private async void RunLoop(CancellationToken token)
        {
            try
            {
                while (true)
                {
                    await WanderAsync(token);
                    await CombatAsync(token);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { Debug.LogException(e, this); }
        }

        private async Awaitable WanderAsync(CancellationToken token)
        {
            if (restBeforeWandering)
            {
                restBeforeWandering = false;
                if (await RestAsync(token)) return;
            }
            while (true)
            {
                if (await WalkToWanderPointAsync(token)) return;

                if (random.NextDouble() < restChance && await RestAsync(token))
                    return;
            }
        }

        /// <returns>True when a target was found on the way.</returns>
        private async Awaitable<bool> WalkToWanderPointAsync(CancellationToken token)
        {
            Vector3 point = PickWanderPoint();
            mover.MoveTo(point, wanderSpeed);
            float travel = EnemyAggroRules.PlanarDistance(transform.position, point) / Mathf.Max(0.01f, wanderSpeed);
            float giveUp = Time.time + travel * 2f + 1f;

            while (true)
            {
                if (IsStunned)
                {
                    float s = Time.time;
                    await WaitWhileStunnedAsync(token);
                    giveUp += Time.time - s;
                    mover.MoveTo(point, wanderSpeed);
                }
                if (mover.HasReachedDestination || Time.time >= giveUp) return false;
                if (TryAcquireTarget()) return true;
                await Awaitable.NextFrameAsync(token);
            }
        }

        /// <returns>True when a target was found while resting.</returns>
        private async Awaitable<bool> RestAsync(CancellationToken token)
        {
            mover.Stop();
            int variant = Mathf.Max(0, WeightedRandom.Pick(idleWeights, random));
            SetInt(IdleVariantHash, variant);
            float end = Time.time + Mathf.Lerp(restTimeRange.x, restTimeRange.y, (float)random.NextDouble());
            if (debugLogs) VelvetLog.Info(LogCategory, this, $"Rests with idle {variant} for {end - Time.time:F1}s");

            while (Time.time < end)
            {
                await WaitWhileStunnedAsync(token);
                if (TryAcquireTarget()) return true;
                await Awaitable.NextFrameAsync(token);
            }
            return false;
        }

        private async Awaitable CombatAsync(CancellationToken token)
        {
            while (true)
            {
                await WaitWhileStunnedAsync(token);
                TryAcquireTarget();

                bool alive = targetTransform != null && target.IsAlive;
                float distance = targetTransform != null
                    ? EnemyAggroRules.PlanarDistance(transform.position, targetTransform.position)
                    : float.MaxValue;

                if (EnemyAggroRules.ShouldDropChase(alive, distance, loseInterestRange))
                {
                    if (debugLogs) VelvetLog.Info(LogCategory, this, alive ? $"Loses interest ({distance:F1}u)" : "Target dead or gone");
                    ClearTarget();
                    restBeforeWandering = true;
                    mover.Stop();
                    return;
                }

                float gap = BodyDistance.PlanarGap(transform.position, body, targetTransform.position, targetCollider);
                CyclopsAttack attack = Time.time >= nextAttackTime ? ChooseAttack(gap) : null;

                if (attack != null)
                {
                    await AttackAsync(attack, token);
                }
                else if (gap <= ClosestAttackReach())
                {
                    // Close enough but every attack is on cooldown: stand still facing the player.
                    mover.Stop();
                    Face(targetTransform.position - transform.position);
                }
                else
                {
                    mover.MoveTo(ClampToZone(targetTransform.position), chaseSpeed);
                }

                await Awaitable.NextFrameAsync(token);
            }
        }

        private CyclopsAttack ChooseAttack(float gap)
        {
            float now = Time.time;
            var weights = new float[attacks.Count];
            for (int i = 0; i < attacks.Count; i++)
            {
                var a = attacks[i];
                weights[i] = a != null && a.IsReady(now) && a.CoversGap(gap) ? a.weight : 0f;
            }
            int index = WeightedRandom.Pick(weights, random);
            return index >= 0 ? attacks[index] : null;
        }

        /// <summary>Reach of the shortest attack: it closes in that far even while longer-reach attacks are on cooldown.</summary>
        private float ClosestAttackReach()
        {
            float reach = float.MaxValue;
            foreach (var a in attacks) if (a != null) reach = Mathf.Min(reach, a.maxGap);
            return reach == float.MaxValue ? 0f : reach;
        }

        private async Awaitable AttackAsync(CyclopsAttack attack, CancellationToken token)
        {
            mover.Stop();
            Face(targetTransform.position - transform.position);
            Vector3 lockedFacing = facing; // direction is fixed for the whole attack

            isAttacking = true;
            impactSignaled = false;
            SetBool(IsAttackingHash, true);
            SetTrigger(Animator.StringToHash(attack.animatorTrigger));
            if (debugLogs) VelvetLog.Info(LogCategory, this, $"Attacks: {attack.name}");

            try
            {
                // Windup: until the Animation Event (when used) or until the windup elapses.
                // A lunge starts at lungeStart and arrives exactly at the hit.
                float start = Time.time;
                float impactTime = start + attack.windup;
                float safety = impactTime + 1.5f;
                bool lunging = false;
                while (useAnimationEvents ? (!impactSignaled && Time.time < safety) : Time.time < impactTime)
                {
                    if (!lunging && attack.lungeDistance > 0f && Time.time >= start + attack.windup * attack.lungeStart)
                    {
                        lunging = true;
                        float lungeTime = Mathf.Max(0.05f, impactTime - Time.time);
                        Vector3 destination = ClampToZone(transform.position + lockedFacing * attack.lungeDistance);
                        mover.MoveTo(destination, EnemyAggroRules.PlanarDistance(transform.position, destination) / lungeTime);
                    }
                    await Awaitable.NextFrameAsync(token);
                }
                if (lunging) mover.Stop();

                Impact(attack, lockedFacing);

                // Recovery: dazed (still, no moving or attacking)
                SetDazed(true);
                float recoverEnd = Time.time + attack.recovery;
                while (Time.time < recoverEnd)
                    await Awaitable.NextFrameAsync(token);
            }
            finally
            {
                SetDazed(false);
                isAttacking = false;
                SetBool(IsAttackingHash, false);
                attack.readyTime = Time.time + attack.cooldown;
                nextAttackTime = Time.time + timeBetweenAttacks;
            }
        }

        private void SetDazed(bool value)
        {
            if (isDazed == value) return;
            isDazed = value;
            if (debugLogs) VelvetLog.Info(LogCategory, this, value ? "Dazed after the attack" : "Recovered");
            (value ? onDazedStart : onDazedEnd)?.Invoke();
        }

        /// <summary>Called by an Animation Event on the attack clip, at the hit frame.</summary>
        public void AnimationEvent_Impact() => impactSignaled = true;

        private void Impact(CyclopsAttack attack, Vector3 forward)
        {
            Vector3 center = transform.position + forward * attack.impactForwardOffset;
            alreadyHit.Clear();
            int hitCount = 0;
            int count = Physics.OverlapSphereNonAlloc(center, attack.impactRadius, impactHits, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider hit = impactHits[i];
                impactHits[i] = null;
                if (hit == null || hit.attachedRigidbody == rb) continue;
                IDamageable damageable = hit.GetComponentInParent<IDamageable>();
                if (damageable is not Component owner || !owner.CompareTag("Player") || !damageable.IsAlive) continue;
                if (!alreadyHit.Add(damageable)) continue;

                damageable.ApplyDamage(attack.damage);
                hitCount++;
                Vector3 push = owner.transform.position - center;
                push.y = 0f;
                if (push.sqrMagnitude < 0.0001f) push = forward;
                owner.GetComponentInParent<IKnockbackable>()?.ApplyKnockback(push, attack.knockbackForce, attack.knockbackStun);
            }

            if (debugLogs) VelvetLog.Info(LogCategory, this, $"{attack.name}: impact at {center}, radius {attack.impactRadius}, hit {hitCount}");
            CameraShake.Impulse(center, attack.cameraShake);
            attack.onImpact?.Invoke(center);
            lastImpact = (center, attack.impactRadius, Time.time);
        }

        public void ApplyKnockback(Vector3 direction, float force, float stunSeconds)
        {
            if (health != null && !health.IsAlive) return;
            if (isAttacking && superArmorWhileAttacking)
            {
                if (debugLogs) VelvetLog.Info(LogCategory, this, "Knockback ignored (super armor)");
                return;
            }
            direction.y = 0f;
            float scaled = force * (1f - knockbackResistance);
            if (scaled > 0f) mover.Push(direction.normalized * scaled, stunSeconds);
            stunnedUntil = Mathf.Max(stunnedUntil, Time.time + Mathf.Max(0f, stunSeconds * (1f - knockbackResistance)));
        }

        private bool IsStunned => Time.time < stunnedUntil;

        private async Awaitable WaitWhileStunnedAsync(CancellationToken token)
        {
            while (IsStunned) await Awaitable.NextFrameAsync(token);
        }

        private bool TryAcquireTarget()
        {
            if (Time.time < nextSearchTime) return false;
            nextSearchTime = Time.time + retargetInterval;

            if (!targetFinder.TryFindNearest(transform.position, detectionRange, out IDamageable found, out Transform foundTransform, out Collider foundCollider))
                return false;
            float distance = EnemyAggroRules.PlanarDistance(transform.position, foundTransform.position);
            if (!EnemyAggroRules.ShouldStartChase(found.IsAlive, distance, detectionRange)) return false;

            if (debugLogs && !ReferenceEquals(found, target))
                VelvetLog.Info(LogCategory, this, $"Detects {foundTransform.name} at {distance:F1}u");
            target = found;
            targetTransform = foundTransform;
            targetCollider = foundCollider;
            return true;
        }

        private void ClearTarget()
        {
            target = null;
            targetTransform = null;
            targetCollider = null;
        }

        private bool GetZone(out Vector3 center, out Vector2 halfExtents)
        {
            if (wanderArea != null)
            {
                Bounds b = wanderArea.bounds;
                center = new Vector3(b.center.x, transform.position.y, b.center.z);
                halfExtents = new Vector2(b.extents.x, b.extents.z);
                return true;
            }
            center = spawnPosition;
            halfExtents = Vector2.zero;
            return false;
        }

        private Vector3 PickWanderPoint()
        {
            bool hasArea = GetZone(out Vector3 center, out Vector2 half);
            Vector3 candidate = transform.position;
            for (int i = 0; i < maxPointAttempts; i++)
            {
                // A short step from where it stands (inside the zone) instead of crossing the whole zone at once
                candidate = hasArea
                    ? WanderPointPicker.ClampToArea(WanderPointPicker.PickPoint(transform.position, maxWanderStep, random), center, half, areaMargin)
                    : WanderPointPicker.PickPoint(spawnPosition, fallbackWanderRadius, random);
                if (EnemyAggroRules.PlanarDistance(transform.position, candidate) < maxWanderStep * 0.25f) continue; // too close to be worth a walk
                if (HasGround(candidate)) return candidate;
            }
            if (debugLogs) VelvetLog.Warn(LogCategory, this, "No point with ground found; staying put");
            return transform.position;
        }

        private Vector3 ClampToZone(Vector3 point)
        {
            if (GetZone(out Vector3 center, out Vector2 half))
                return WanderPointPicker.ClampToArea(point, center, half, areaMargin);
            float d = EnemyAggroRules.PlanarDistance(spawnPosition, point);
            float max = fallbackWanderRadius + loseInterestRange;
            if (d <= max) return point;
            Vector3 dir = point - spawnPosition; dir.y = 0f;
            return spawnPosition + dir.normalized * max;
        }

        private bool HasGround(Vector3 point)
        {
            Vector3 from = point + Vector3.up * 5f;
            var hits = Physics.RaycastAll(from, Vector3.down, 15f, groundMask, QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
                if (h.rigidbody != rb && h.collider != body) return true;
            return false;
        }

        private void Face(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;
            facing = direction.normalized;
            if (spriteRoot == null || Mathf.Abs(direction.x) < 0.01f) return;
            Vector3 s = spriteRoot.localScale;
            bool right = direction.x > 0f;
            s.x = Mathf.Abs(s.x) * (right == artFacesRight ? 1f : -1f);
            spriteRoot.localScale = s;
        }

        private void SetBool(int h, bool v) { if (animatorParams.Contains(h)) animator.SetBool(h, v); }
        private void SetFloat(int h, float v) { if (animatorParams.Contains(h)) animator.SetFloat(h, v); }
        private void SetInt(int h, int v) { if (animator != null && animatorParams.Contains(h)) animator.SetInteger(h, v); }
        private void SetTrigger(int h)
        {
            if (animator != null && animatorParams.Contains(h)) animator.SetTrigger(h);
            else if (debugLogs) VelvetLog.Warn(LogCategory, this, "The Animator has no trigger for this attack");
        }

        private static List<CyclopsAttack> DefaultAttacks() => new List<CyclopsAttack>
        {
            new CyclopsAttack
            {
                name = "Ground Punch", animatorTrigger = "AttackPunch", weight = 1f,
                minGap = 1f, maxGap = 7f, windup = 1.3f, recovery = 3.2f, cooldown = 6f,
                lungeDistance = 6f, lungeStart = 0.6f,
                damage = 2, impactRadius = 2.8f, impactForwardOffset = 2.5f, knockbackForce = 10f, knockbackStun = 0.6f, cameraShake = 0.7f,
            },
            new CyclopsAttack
            {
                name = "Stomp", animatorTrigger = "AttackStomp", weight = 1.5f,
                minGap = 0f, maxGap = 1.5f, windup = 1.1f, recovery = 1.2f, cooldown = 3f,
                damage = 1, impactRadius = 3.5f, impactForwardOffset = 0.5f, knockbackForce = 11f, knockbackStun = 0.4f, cameraShake = 0.6f,
            },
        };

        private (Vector3 c, float r, float t) lastImpact;

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos) return;
            Vector3 p = transform.position;
            if (wanderArea != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireCube(wanderArea.bounds.center, wanderArea.bounds.size);
            }
            else
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(Application.isPlaying ? spawnPosition : p, fallbackWanderRadius);
            }
            Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(p, detectionRange);
            Gizmos.color = new Color(1f, 0.5f, 0f); Gizmos.DrawWireSphere(p, loseInterestRange);
            if (attacks != null)
                foreach (var a in attacks)
                {
                    if (a == null) continue;
                    Gizmos.color = new Color(1f, 0f, 0f, 0.6f);
                    Gizmos.DrawWireSphere(p + facing * a.impactForwardOffset, a.impactRadius);
                }
            if (Application.isPlaying && Time.time - lastImpact.t < 0.5f)
            {
                Gizmos.color = new Color(1f, 0f, 0f, 0.35f);
                Gizmos.DrawSphere(lastImpact.c, lastImpact.r);
            }
        }
    }
}
