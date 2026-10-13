using System;
using System.Threading;
using CodenameVelvet.Combat;
using UnityEngine;

namespace CodenameVelvet.Enemies
{
    /// <summary>
    /// Placeholder enemy behaviour: wander around the spawn point, chase the
    /// nearest living player, and hit it on contact. Runs as one Awaitable loop
    /// per enable that is cancelled on disable/destroy; every wait uses scaled
    /// time, so pausing with Time.timeScale = 0 freezes the enemy.
    /// Wander → Chase (player within detection range) → attacks while in attack
    /// range → back to Wander when the target dies or escapes the lose-interest range.
    /// </summary>
    [RequireComponent(typeof(EnemyTargetFinder))]
    public class EnemyBrain : MonoBehaviour
    {
        [Tooltip("Default definition; a spawner may override it via Initialize.")]
        [SerializeField] private EnemyDefinition definition;

        private IEnemyMover mover;
        private EnemyTargetFinder targetFinder;
        private System.Random random;
        private CancellationTokenSource loopCancellation;

        private Vector3 spawnPosition;
        private bool hasStarted;

        private IDamageable target;
        private Transform targetTransform;
        private float nextSearchTime;
        private float nextAttackTime;

        /// <summary>Sets the enemy type. Call right after instantiating, before Start.</summary>
        public void Initialize(EnemyDefinition enemyDefinition)
        {
            definition = enemyDefinition;
            ApplyColor();
        }

        private void Awake()
        {
            mover = GetComponent<IEnemyMover>();
            targetFinder = GetComponent<EnemyTargetFinder>();
            // Seeded from Unity's RNG: enemies spawned on the same frame would
            // otherwise share a time-based seed and wander in lockstep.
            random = new System.Random(UnityEngine.Random.Range(int.MinValue, int.MaxValue));
        }

        private void Start()
        {
            spawnPosition = transform.position;
            hasStarted = true;
            ApplyColor();
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

        // ─────────────────────── LOOP LIFETIME ───────────────────────

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

        // ─────────────────────── WANDER ───────────────────────

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

            while (!mover.HasReachedDestination && Time.time < giveUpTime)
            {
                if (TryAcquireTarget())
                    return true;
                await Awaitable.NextFrameAsync(token);
            }

            return false;
        }

        // ─────────────────────── CHASE / ATTACK ───────────────────────

        /// <summary>Follows and hits the current target until the chase is dropped.</summary>
        private async Awaitable ChaseAsync(CancellationToken token)
        {
            while (true)
            {
                // Switch to a closer player if one entered detection range.
                TryAcquireTarget();

                bool targetAlive = targetTransform != null && target.IsAlive;
                float distance = targetTransform != null
                    ? EnemyAggroRules.PlanarDistance(transform.position, targetTransform.position)
                    : float.MaxValue;

                if (EnemyAggroRules.ShouldDropChase(targetAlive, distance, definition.LoseInterestRange))
                {
                    ClearTarget();
                    mover.Stop();
                    return;
                }

                if (EnemyAggroRules.IsInAttackRange(distance, definition.AttackRange))
                {
                    mover.Stop();
                    if (Time.time >= nextAttackTime)
                    {
                        target.ApplyDamage(definition.AttackDamage);
                        nextAttackTime = Time.time + definition.AttackCooldown;
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

        // ─────────────────────── TARGETING ───────────────────────

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
            if (!targetFinder.TryFindNearest(origin, definition.DetectionRange, out IDamageable found, out Transform foundTransform))
                return false;

            float distance = EnemyAggroRules.PlanarDistance(origin, foundTransform.position);
            if (!EnemyAggroRules.ShouldStartChase(found.IsAlive, distance, definition.DetectionRange))
                return false;

            target = found;
            targetTransform = foundTransform;
            return true;
        }

        private void ClearTarget()
        {
            target = null;
            targetTransform = null;
        }

        // ─────────────────────── VISUALS ───────────────────────

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

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, definition.AttackRange);
        }
    }
}
