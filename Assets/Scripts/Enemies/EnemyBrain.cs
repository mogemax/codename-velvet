using System;
using System.Threading;
using CodenameVelvet.Combat;
using CodenameVelvet.Core.Debugging;
using UnityEngine;

namespace CodenameVelvet.Enemies
{
    /// <summary>
    /// Placeholder enemy behaviour: wander around the spawn point, chase the
    /// nearest living player, and hit it on contact. Runs as one Awaitable loop
    /// per enable that is cancelled on disable/destroy; every wait uses scaled
    /// time, so pausing with Time.timeScale = 0 freezes the enemy.
    /// Wander → Chase (player within detection range) → attacks while the gap between
    /// both bodies is within attack range → back to Wander when the target dies or escapes the lose-interest range.
    /// A knockback pushes the enemy and stuns it: the loop pauses (no moving,
    /// retargeting or attacking) and resumes the current state when the stun ends.
    /// </summary>
    [RequireComponent(typeof(EnemyTargetFinder))]
    public class EnemyBrain : MonoBehaviour, IKnockbackable
    {
        private const string LogCategory = "Enemy";

        [Tooltip("Default definition; a spawner may override it via Initialize.")]
        [SerializeField] private EnemyDefinition definition;

        [Header("Debug")]
        [Tooltip("Logs spawn, state changes, attacks, knockback and stun. Never per frame.")]
        [SerializeField] private bool debugLogs = true;

        private IEnemyMover mover;
        private EnemyTargetFinder targetFinder;
        private EnemyHealth health;
        private Collider body;
        private System.Random random;
        private CancellationTokenSource loopCancellation;

        private Vector3 spawnPosition;
        private bool hasStarted;

        private IDamageable target;
        private Transform targetTransform;
        private Collider targetCollider;
        private float nextSearchTime;
        private float nextAttackTime;
        private float stunnedUntil;
        private bool wasInAttackRange; // Debug-only: logs attack-range entry once.

        /// <summary>Sets the enemy type. Call right after instantiating, before Start.</summary>
        public void Initialize(EnemyDefinition enemyDefinition)
        {
            definition = enemyDefinition;
            ApplyColor();
            InitializeHealth();
        }

        /// <summary>Pushes the enemy away on the XZ plane and stuns it for <paramref name="stunSeconds"/>.</summary>
        public void ApplyKnockback(Vector3 direction, float force, float stunSeconds)
        {
            if (health != null && !health.IsAlive)
            {
                if (debugLogs)
                    VelvetLog.Info(LogCategory, this, "Knockback ignored (dead)");
                return;
            }

            direction.y = 0f;
            Vector3 planarDirection = direction.normalized;

            if (debugLogs)
            {
                if (mover == null)
                    VelvetLog.Warn(LogCategory, this, "Knockback received but there is no IEnemyMover: no push");
                else
                    VelvetLog.Info(LogCategory, this, $"Knockback dir {planarDirection} force {force} stun {stunSeconds}s, expected push ≈ {force * stunSeconds * 0.5f:F2}u");
            }
            if (mover != null)
                mover.Push(planarDirection * force, stunSeconds);

            // Time.time based, so a pause (timeScale = 0) freezes the stun too.
            stunnedUntil = Mathf.Max(stunnedUntil, Time.time + Mathf.Max(0f, stunSeconds));
        }

        private void Awake()
        {
            mover = GetComponent<IEnemyMover>();
            targetFinder = GetComponent<EnemyTargetFinder>();
            health = GetComponent<EnemyHealth>();
            body = GetComponent<Collider>();
            if (health != null)
                health.Died += _ => enabled = false; // the corpse lingers a moment: no moving or attacking
            if (health == null)
                Debug.LogError($"{name}: EnemyBrain has no EnemyHealth component; this enemy can't take damage. Add one to the prefab.", this);
            // Seeded from Unity's RNG: enemies spawned on the same frame would
            // otherwise share a time-based seed and wander in lockstep.
            random = new System.Random(UnityEngine.Random.Range(int.MinValue, int.MaxValue));
        }

        private void Start()
        {
            spawnPosition = transform.position;
            hasStarted = true;
            ApplyColor();
            InitializeHealth();

            if (debugLogs && definition != null)
                VelvetLog.Info(LogCategory, this, $"Spawned as {definition.DisplayName} at {spawnPosition}, HP {(health != null ? health.CurrentHp : 0)}/{(health != null ? health.MaxHp : 0)}");

            StartLoop();
        }

        private void OnEnable()
        {
            // The first run starts in Start, after a spawner had the chance to Initialize.
            if (hasStarted)
                StartLoop();
        }

        private void OnDisable()
        {
            StopLoop();
        }

        private void StartLoop()
        {
            StopLoop();

            if (definition == null || mover == null)
            {
                Debug.LogError($"{name}: EnemyBrain needs an EnemyDefinition and an IEnemyMover component.", this);
                return;
            }

            loopCancellation = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            RunLoop(loopCancellation.Token);
        }

        private void StopLoop()
        {
            if (loopCancellation == null)
                return;

            loopCancellation.Cancel();
            loopCancellation.Dispose();
            loopCancellation = null;

            ClearTarget();
            if (mover is Component moverComponent && moverComponent != null)
                mover.Stop();
        }

        /// <summary>Only async void in the enemy: the fire-and-forget entry point.</summary>
        private async void RunLoop(CancellationToken token)
        {
            try
            {
                await BehaviourLoopAsync(token);
            }
            catch (OperationCanceledException)
            {
                // Expected on disable/destroy.
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        private async Awaitable BehaviourLoopAsync(CancellationToken token)
        {
            while (true)
            {
                await WanderAsync(token);
                await ChaseAsync(token);
            }
        }

        /// <summary>Walks between random points around the spawn until a target is acquired.</summary>
        private async Awaitable WanderAsync(CancellationToken token)
        {
            while (true)
            {
                if (await WalkToWanderPointAsync(token))
                    return;

                mover.Stop();
                Vector2 restRange = definition.RestTimeRange;
                float restEnd = Time.time + Mathf.Lerp(restRange.x, restRange.y, (float)random.NextDouble());

                while (Time.time < restEnd)
                {
                    await WaitWhileStunnedAsync(token);
                    if (TryAcquireTarget())
                        return;
                    await Awaitable.NextFrameAsync(token);
                }
            }
        }

        /// <returns>True if a target was acquired on the way.</returns>
        private async Awaitable<bool> WalkToWanderPointAsync(CancellationToken token)
        {
            Vector3 point = WanderPointPicker.PickPoint(spawnPosition, definition.WanderRadius, random);
            mover.MoveTo(point, definition.WanderSpeed);

            // Give up on points blocked by obstacles instead of pushing against them forever.
            float travelTime = EnemyAggroRules.PlanarDistance(transform.position, point) / Mathf.Max(0.01f, definition.WanderSpeed);
            float giveUpTime = Time.time + travelTime * 2f + 1f;

            while (true)
            {
                // Checked before arrival: a push clears the mover's destination.
                if (IsStunned)
                {
                    // Resume the trip once the stun ends, without counting the stun against the give-up time.
                    float stunStart = Time.time;
                    await WaitWhileStunnedAsync(token);
                    giveUpTime += Time.time - stunStart;
                    mover.MoveTo(point, definition.WanderSpeed);
                }

                if (mover.HasReachedDestination || Time.time >= giveUpTime)
                    return false;
                if (TryAcquireTarget())
                    return true;
                await Awaitable.NextFrameAsync(token);
            }
        }

        /// <summary>Follows and hits the current target until the chase is dropped.</summary>
        private async Awaitable ChaseAsync(CancellationToken token)
        {
            while (true)
            {
                await WaitWhileStunnedAsync(token);

                // Switch to a closer player if one entered detection range.
                TryAcquireTarget();

                bool targetAlive = targetTransform != null && target.IsAlive;
                float distance = targetTransform != null
                    ? EnemyAggroRules.PlanarDistance(transform.position, targetTransform.position)
                    : float.MaxValue;

                if (EnemyAggroRules.ShouldDropChase(targetAlive, distance, definition.LoseInterestRange))
                {
                    if (debugLogs)
                        VelvetLog.Info(LogCategory, this, targetAlive
                            ? $"Chase → Wander: target escaped ({distance:F2}u > {definition.LoseInterestRange}u)"
                            : "Chase → Wander: target dead or gone");
                    ClearTarget();
                    mover.Stop();
                    return;
                }

                float gap = BodyDistance.PlanarGap(transform.position, body, targetTransform.position, targetCollider);
                bool inAttackRange = EnemyAggroRules.IsInAttackRange(gap, definition.AttackRange);
                if (debugLogs && inAttackRange != wasInAttackRange)
                    VelvetLog.Info(LogCategory, this, inAttackRange
                        ? $"Chase → Attack: {targetTransform.name} in range (gap {gap:F2}u, centres {distance:F2}u)"
                        : $"Attack → Chase: {targetTransform.name} left range (gap {gap:F2}u, centres {distance:F2}u)");
                wasInAttackRange = inAttackRange;

                if (inAttackRange)
                {
                    mover.Stop();
                    if (Time.time >= nextAttackTime)
                    {
                        target.ApplyDamage(definition.AttackDamage);
                        targetTransform.GetComponentInParent<IKnockbackable>()?.ApplyKnockback(
                            targetTransform.position - transform.position, definition.AttackKnockback, definition.AttackKnockbackSeconds);
                        nextAttackTime = Time.time + definition.AttackCooldown;

                        if (debugLogs)
                            VelvetLog.Info(LogCategory, this, $"Hit {targetTransform.name} for {definition.AttackDamage} damage (gap {gap:F2}u)");
                    }
                }
                else
                {
                    // Keeps following during the cooldown if the target steps away.
                    mover.MoveTo(targetTransform.position, definition.ChaseSpeed);
                }

                await Awaitable.NextFrameAsync(token);
            }
        }

        private bool IsStunned => Time.time < stunnedUntil;

        private async Awaitable WaitWhileStunnedAsync(CancellationToken token)
        {
            bool wasStunned = IsStunned;
            while (IsStunned)
                await Awaitable.NextFrameAsync(token);

            if (debugLogs && wasStunned)
                VelvetLog.Info(LogCategory, this, $"Stun ended at {transform.position}");
        }

        /// <summary>
        /// Throttled to one search per retarget interval. Replaces the current target
        /// only when a living player is found within detection range.
        /// </summary>
        private bool TryAcquireTarget()
        {
            if (Time.time < nextSearchTime)
                return false;
            nextSearchTime = Time.time + definition.RetargetInterval;

            Vector3 origin = transform.position;
            if (!targetFinder.TryFindNearest(origin, definition.DetectionRange, out IDamageable found, out Transform foundTransform, out Collider foundCollider))
                return false;

            float distance = EnemyAggroRules.PlanarDistance(origin, foundTransform.position);
            if (!EnemyAggroRules.ShouldStartChase(found.IsAlive, distance, definition.DetectionRange))
                return false;

            if (debugLogs && !ReferenceEquals(found, target))
                VelvetLog.Info(LogCategory, this, targetTransform == null
                    ? $"Wander → Chase: {foundTransform.name} at {distance:F2}u"
                    : $"Retarget: {targetTransform.name} → {foundTransform.name} at {distance:F2}u");

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
            wasInAttackRange = false;
        }

        private void InitializeHealth()
        {
            if (definition != null && health != null)
                health.Initialize(definition.MaxHp);
        }

        private void ApplyColor()
        {
            if (definition == null)
                return;

            foreach (SpriteRenderer spriteRenderer in GetComponentsInChildren<SpriteRenderer>(true))
                spriteRenderer.color = definition.Color;
        }

        private void OnDrawGizmosSelected()
        {
            if (definition == null)
                return;

            Vector3 wanderCenter = Application.isPlaying ? spawnPosition : transform.position;
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(wanderCenter, definition.WanderRadius);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, definition.DetectionRange);

            Gizmos.color = new Color(1f, 0.5f, 0f);
            Gizmos.DrawWireSphere(transform.position, definition.LoseInterestRange);

            // Attack range is a gap from this body's surface, so the reach grows with its size.
            Collider gizmoBody = body != null ? body : GetComponent<Collider>();
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, definition.AttackRange + BodyDistance.PlanarRadius(gizmoBody));
        }
    }
}
