using System;
using System.Collections.Generic;
using System.Threading;
using CodenameVelvet.Combat;
using CodenameVelvet.Core.Debugging;
using UnityEngine;

namespace CodenameVelvet.Enemies
{
    /// <summary>
    /// Cerebro del cíclope (jefe). Estados:
    /// <list type="bullet">
    /// <item>Deambular: camina a puntos aleatorios dentro de su zona (un BoxCollider trigger, o un radio
    /// alrededor del spawn) y solo elige puntos con suelo debajo, para no caerse del mundo.</item>
    /// <item>Descansar: de vez en cuando se detiene y reproduce uno de sus idles al azar (parámetro IdleVariant).</item>
    /// <item>Perseguir: al detectar a un jugador camina hacia él, sin salirse de la zona.</item>
    /// <item>Atacar: con el jugador cerca elige uno de los ataques disponibles (por rango, cooldown y peso),
    /// se compromete con él (no se puede interrumpir) y espera a recuperarse.</item>
    /// </list>
    /// Igual que <see cref="EnemyBrain"/>: un solo bucle Awaitable por activación, cancelado al desactivarse,
    /// y todos los tiempos usan Time.time (se congela con la pausa).
    /// </summary>
    [RequireComponent(typeof(EnemyTargetFinder))]
    public class CyclopsBrain : MonoBehaviour, IKnockbackable
    {
        private const string LogCategory = "Cyclops";

        [Header("Vida")]
        [SerializeField, Min(1)] private int maxHp = 20;

        [Header("Zona")]
        [Tooltip("BoxCollider (trigger) que delimita por dónde puede andar. Vacío = radio alrededor del spawn.")]
        [SerializeField] private BoxCollider wanderArea;
        [SerializeField, Min(0f)] private float fallbackWanderRadius = 8f;
        [Tooltip("Distancia que deja al borde de la zona (normalmente ~ el radio de su cuerpo).")]
        [SerializeField, Min(0f)] private float areaMargin = 1.5f;
        [Tooltip("Capas que cuentan como suelo al revisar que un punto no esté en el vacío.")]
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField, Min(1)] private int maxPointAttempts = 12;

        [Header("Deambular")]
        [SerializeField, Min(0f)] private float wanderSpeed = 1.8f;
        [Tooltip("Probabilidad (0-1) de detenerse a hacer un idle al llegar a cada punto.")]
        [SerializeField, Range(0f, 1f)] private float restChance = 0.6f;
        [SerializeField] private Vector2 restTimeRange = new Vector2(2.5f, 6f);
        [Tooltip("Peso de cada idle: índice 0 = estado Idle, 1 = Idle_V1.")]
        [SerializeField] private float[] idleWeights = { 1f, 1f };

        [Header("Persecución")]
        [SerializeField, Min(0f)] private float chaseSpeed = 3.5f;
        [SerializeField, Min(0f)] private float detectionRange = 9f;
        [Tooltip("Deja de perseguir más allá de esta distancia. Siempre >= detección.")]
        [SerializeField, Min(0f)] private float loseInterestRange = 14f;
        [SerializeField, Min(0f)] private float retargetInterval = 0.25f;

        [Header("Ataques")]
        [SerializeField] private List<CyclopsAttack> attacks = new List<CyclopsAttack>();
        [Tooltip("Pausa mínima entre el final de un ataque y el inicio del siguiente.")]
        [SerializeField, Min(0f)] private float timeBetweenAttacks = 0.6f;
        [Tooltip("Si está activo, el golpe ocurre cuando la animación llama al Animation Event 'AnimationEvent_Impact' (con el windup como límite de seguridad).")]
        [SerializeField] private bool useAnimationEvents = false;

        [Header("Golpes recibidos")]
        [Tooltip("0 = el knockback lo empuja completo, 1 = no lo mueve.")]
        [SerializeField, Range(0f, 1f)] private float knockbackResistance = 0.8f;
        [Tooltip("Mientras ataca no se le puede empujar ni aturdir (super armor).")]
        [SerializeField] private bool superArmorWhileAttacking = true;

        [Header("Animator y visual")]
        [SerializeField] private Animator animator;
        [Tooltip("Hijo que se voltea con Scale.x según hacia dónde va.")]
        [SerializeField] private Transform spriteRoot;
        [Tooltip("Actívalo si el dibujo original mira a la derecha.")]
        [SerializeField] private bool artFacesRight = false;

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
        private bool impactSignaled;
        private Vector3 facing = Vector3.back; // hacia la cámara

        public bool IsAttacking => isAttacking;

        // ─────────────────────── LIFETIME ───────────────────────

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
            else Debug.LogWarning($"{name}: CyclopsBrain sin EnemyHealth; no podrá recibir daño.", this);
        }

        private void Start()
        {
            spawnPosition = transform.position;
            hasStarted = true;
            if (debugLogs)
                VelvetLog.Info(LogCategory, this, $"Listo en {spawnPosition}, HP {maxHp}, zona {(wanderArea != null ? wanderArea.name : $"radio {fallbackWanderRadius}")}, {attacks.Count} ataques");
            StartLoop();
        }

        private void OnEnable() { if (hasStarted) StartLoop(); }
        private void OnDisable() => StopLoop();

        private void Update()
        {
            if (animator == null) return;
            Vector3 v = rb != null ? rb.linearVelocity : Vector3.zero;
            v.y = 0f;
            bool moving = !isAttacking && v.sqrMagnitude > 0.01f;
            SetBool(IsMovingHash, moving);
            SetBool(IsAttackingHash, isAttacking);
            SetFloat(SpeedHash, v.magnitude);
            if (moving)
            {
                SetFloat(MoveXHash, v.normalized.x);
                SetFloat(MoveYHash, v.normalized.z);
                Face(v);
            }
        }

        // ─────────────────────── LOOP ───────────────────────

        private void StartLoop()
        {
            StopLoop();
            if (mover == null)
            {
                Debug.LogError($"{name}: CyclopsBrain necesita un IEnemyMover (p. ej. RigidbodyEnemyMover).", this);
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

        // ─────────────────────── DEAMBULAR / DESCANSAR ───────────────────────

        private async Awaitable WanderAsync(CancellationToken token)
        {
            while (true)
            {
                if (await WalkToWanderPointAsync(token)) return;

                if (random.NextDouble() < restChance && await RestAsync(token))
                    return;
            }
        }

        /// <returns>True si encontró un objetivo en el camino.</returns>
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

        /// <returns>True si encontró un objetivo mientras descansaba.</returns>
        private async Awaitable<bool> RestAsync(CancellationToken token)
        {
            mover.Stop();
            int variant = Mathf.Max(0, WeightedRandom.Pick(idleWeights, random));
            SetInt(IdleVariantHash, variant);
            float end = Time.time + Mathf.Lerp(restTimeRange.x, restTimeRange.y, (float)random.NextDouble());
            if (debugLogs) VelvetLog.Info(LogCategory, this, $"Descansa con idle {variant} por {end - Time.time:F1}s");

            while (Time.time < end)
            {
                await WaitWhileStunnedAsync(token);
                if (TryAcquireTarget()) return true;
                await Awaitable.NextFrameAsync(token);
            }
            return false;
        }

        // ─────────────────────── PERSEGUIR / ATACAR ───────────────────────

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
                    if (debugLogs) VelvetLog.Info(LogCategory, this, alive ? $"Pierde interés ({distance:F1}u)" : "Objetivo muerto o desaparecido");
                    ClearTarget();
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
                    // Ya está encima pero todo está en cooldown: se queda plantado mirando al jugador.
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

        /// <summary>El alcance del ataque más corto: se acerca hasta ahí aunque los de mayor alcance estén en cooldown.</summary>
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
            Vector3 lockedFacing = facing; // la dirección no cambia a mitad del ataque

            isAttacking = true;
            impactSignaled = false;
            SetBool(IsAttackingHash, true);
            SetTrigger(Animator.StringToHash(attack.animatorTrigger));
            if (debugLogs) VelvetLog.Info(LogCategory, this, $"Ataca: {attack.name}");

            try
            {
                // Preparación: hasta el Animation Event (si se usa) o hasta que pase el windup
                float impactTime = Time.time + attack.windup;
                float safety = impactTime + 1.5f;
                while (useAnimationEvents ? (!impactSignaled && Time.time < safety) : Time.time < impactTime)
                    await Awaitable.NextFrameAsync(token);

                Impact(attack, lockedFacing);

                float recoverEnd = Time.time + attack.recovery;
                while (Time.time < recoverEnd)
                    await Awaitable.NextFrameAsync(token);
            }
            finally
            {
                isAttacking = false;
                SetBool(IsAttackingHash, false);
                attack.readyTime = Time.time + attack.cooldown;
                nextAttackTime = Time.time + timeBetweenAttacks;
            }
        }

        /// <summary>Llamar desde un Animation Event del clip de ataque en el cuadro del golpe.</summary>
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

            if (debugLogs) VelvetLog.Info(LogCategory, this, $"{attack.name}: impacto en {center}, radio {attack.impactRadius}, golpeó a {hitCount}");
            attack.onImpact?.Invoke(center);
            lastImpact = (center, attack.impactRadius, Time.time);
        }

        // ─────────────────────── KNOCKBACK / STUN ───────────────────────

        public void ApplyKnockback(Vector3 direction, float force, float stunSeconds)
        {
            if (health != null && !health.IsAlive) return;
            if (isAttacking && superArmorWhileAttacking)
            {
                if (debugLogs) VelvetLog.Info(LogCategory, this, "Knockback ignorado (super armor)");
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

        // ─────────────────────── OBJETIVO ───────────────────────

        private bool TryAcquireTarget()
        {
            if (Time.time < nextSearchTime) return false;
            nextSearchTime = Time.time + retargetInterval;

            if (!targetFinder.TryFindNearest(transform.position, detectionRange, out IDamageable found, out Transform foundTransform, out Collider foundCollider))
                return false;
            float distance = EnemyAggroRules.PlanarDistance(transform.position, foundTransform.position);
            if (!EnemyAggroRules.ShouldStartChase(found.IsAlive, distance, detectionRange)) return false;

            if (debugLogs && !ReferenceEquals(found, target))
                VelvetLog.Info(LogCategory, this, $"Detecta a {foundTransform.name} a {distance:F1}u");
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

        // ─────────────────────── ZONA ───────────────────────

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
                candidate = hasArea
                    ? WanderPointPicker.PickPointInArea(center, new Vector2(Mathf.Max(0f, half.x - areaMargin), Mathf.Max(0f, half.y - areaMargin)), random)
                    : WanderPointPicker.PickPoint(spawnPosition, fallbackWanderRadius, random);
                if (HasGround(candidate)) return candidate;
            }
            if (debugLogs) VelvetLog.Warn(LogCategory, this, "No encontré un punto con suelo; se queda donde está");
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

        // ─────────────────────── VISUAL / ANIMATOR ───────────────────────

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
            else if (debugLogs) VelvetLog.Warn(LogCategory, this, "El Animator no tiene el trigger del ataque");
        }

        private static List<CyclopsAttack> DefaultAttacks() => new List<CyclopsAttack>
        {
            new CyclopsAttack
            {
                name = "Puñetazo al suelo", animatorTrigger = "AttackPunch", weight = 1f,
                minGap = 0f, maxGap = 2.5f, windup = 0.9f, recovery = 2.2f, cooldown = 5f,
                damage = 2, impactRadius = 2.5f, impactForwardOffset = 2.5f, knockbackForce = 9f, knockbackStun = 0.6f,
            },
            new CyclopsAttack
            {
                name = "Pisotón", animatorTrigger = "AttackStomp", weight = 1.5f,
                minGap = 0f, maxGap = 1.5f, windup = 0.5f, recovery = 0.8f, cooldown = 2.5f,
                damage = 1, impactRadius = 3.5f, impactForwardOffset = 0.5f, knockbackForce = 11f, knockbackStun = 0.4f,
            },
        };

        // ─────────────────────── GIZMOS ───────────────────────

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
