using System.Collections.Generic;
using CodenameVelvet.Combat;
using CodenameVelvet.Core.Debugging;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CodenameVelvet.Player
{
    /// <summary>
    /// Sword swing: hits every living non-player <see cref="IDamageable"/> inside a
    /// planar arc around the aim direction (the nearest valid target in range when aim
    /// assist is on, otherwise the last movement direction, same as the dash), then knocks back and stuns whatever also implements
    /// <see cref="IKnockbackable"/>. Input arrives from this object's PlayerInput
    /// (Send Messages) through the "Attack" action, so each player reads only their device.
    /// </summary>
    [RequireComponent(typeof(PlayerMovement))]
    public class PlayerMeleeAttack : MonoBehaviour
    {
        private const string PlayerTag = "Player";
        private const string LogCategory = "Player";

        [Header("Swing")]
        [SerializeField] private int damage = 1;
        [Tooltip("Reach as the gap between bodies (this player's collider surface to the target's), on the ground plane. 0 = touching.")]
        [SerializeField] private float range = 1f;
        [Tooltip("Total width of the swing in degrees, centred on the aim direction.")]
        [SerializeField] private float arcDegrees = 150f;
        [Tooltip("Seconds between swings.")]
        [SerializeField] private float cooldown = 0.4f;
        [Tooltip("Aim the swing at the nearest valid target in range; falls back to the facing direction when there is none.")]
        [SerializeField] private bool aimAssist = true;

        [Header("Hit reaction")]
        [Tooltip("Initial push speed (units/second) given to targets that support knockback.")]
        [SerializeField] private float knockbackForce = 22f;
        [SerializeField] private float stunSeconds = 0.35f;

        [Header("Query")]
        [Tooltip("Layers the swing can hit. Narrow it to the enemy layer once one exists.")]
        [SerializeField] private LayerMask hitMask = ~0;
        [Tooltip("Max colliders inspected per swing. Raise it if crowded swings miss targets.")]
        [SerializeField] private int maxHits = 32;

        [Header("Debug")]
        [Tooltip("Logs blocked attacks, every target struck and the knockback applied.")]
        [SerializeField] private bool debugLogs = true;

        private PlayerMovement movement;
        private PlayerHealth health;
        private Collider body;
        private Collider[] hits;
        private readonly HashSet<IDamageable> struckThisSwing = new HashSet<IDamageable>();
        // Valid unique targets of the current swing, index-aligned; reused across swings.
        private readonly List<IDamageable> candidateTargets = new List<IDamageable>();
        private readonly List<Component> candidateOwners = new List<Component>();
        private readonly List<Vector3> candidatePositions = new List<Vector3>();
        private readonly List<float> candidateGaps = new List<float>();
        private float nextAttackTime;

        private void Awake()
        {
            movement = GetComponent<PlayerMovement>();
            health = GetComponent<PlayerHealth>();
            body = GetComponent<Collider>();
            hits = new Collider[Mathf.Max(1, maxHits)];
        }

        // ─────────────────────── INPUT (PlayerInput · Send Messages) ───────────────────────

        private void OnAttack(InputValue value)
        {
            if (!value.isPressed)
                return;

            if (!CanAttack())
            {
                if (debugLogs)
                    VelvetLog.Info(LogCategory, this, $"Attack blocked ({BlockedReason()})");
                return;
            }

            nextAttackTime = Time.time + cooldown;
            Swing();
        }

        private bool CanAttack()
        {
            if (health != null && !health.IsAlive)
                return false;

            return !movement.IsDashing && Time.time >= nextAttackTime;
        }

        /// <summary>Debug-only: why <see cref="CanAttack"/> returned false, checked in the same order.</summary>
        private string BlockedReason()
        {
            if (health != null && !health.IsAlive)
                return "dead";
            if (movement.IsDashing)
                return "dashing";
            return $"cooldown, {nextAttackTime - Time.time:F2}s left";
        }

        private void Swing()
        {
            Vector3 origin = transform.position;
            Vector2 facing = movement.FacingDirection;

            int count = CollectCandidates(origin);
            Vector2 aim = aimAssist
                ? MeleeAim.ChooseAimDirection(origin, facing, candidatePositions, candidateGaps, range)
                : facing;

            if (debugLogs)
                VelvetLog.Info(LogCategory, this, $"Swing aim {DescribeAim(origin, aim)} {aim}");

            int struckCount = 0;
            for (int i = 0; i < candidateTargets.Count; i++)
            {
                Component owner = candidateOwners[i];
                Vector3 targetPosition = candidatePositions[i];
                float gap = candidateGaps[i];
                bool inArc = MeleeArc.IsInArc(origin, aim, targetPosition, gap, range, arcDegrees);

                if (debugLogs)
                    LogCandidate(owner, origin, aim, targetPosition, gap, inArc);

                if (!inArc)
                    continue;

                Strike(candidateTargets[i], owner, origin, targetPosition, aim);
                struckCount++;
            }

            candidateTargets.Clear();
            candidateOwners.Clear();
            candidatePositions.Clear();
            candidateGaps.Clear();

            if (debugLogs)
                VelvetLog.Info(LogCategory, this, $"Swing facing {facing}: {count} collider(s) in range, {struckCount} target(s) hit");
        }

        /// <summary>
        /// Fills the candidate buffers with every valid target whose collider may be within
        /// reach, once per target even when it owns several colliders (the first collider
        /// found measures the gap). The query grows by this body's radius because range is
        /// a gap from its surface. Returns the collider count.
        /// </summary>
        private int CollectCandidates(Vector3 origin)
        {
            float queryRadius = range + BodyDistance.PlanarRadius(body);
            int count = Physics.OverlapSphereNonAlloc(origin, queryRadius, hits, hitMask);
            for (int i = 0; i < count; i++)
            {
                Collider hit = hits[i];
                hits[i] = null; // Don't keep scene objects alive through the buffer.

                IDamageable target = hit.GetComponentInParent<IDamageable>();
                if (target is not Component owner || !IsValidTarget(target, owner))
                    continue;

                if (!struckThisSwing.Add(target))
                    continue;

                candidateTargets.Add(target);
                candidateOwners.Add(owner);
                candidatePositions.Add(owner.transform.position);
                candidateGaps.Add(BodyDistance.PlanarGap(origin, body, owner.transform.position, hit));
            }

            struckThisSwing.Clear();
            return count;
        }

        /// <summary>Debug-only: "assist → name" when the aim points at a candidate, otherwise "facing".</summary>
        private string DescribeAim(Vector3 origin, Vector2 aim)
        {
            if (!aimAssist)
                return "facing";

            for (int i = 0; i < candidatePositions.Count; i++)
            {
                Vector3 position = candidatePositions[i];
                Vector2 toTarget = new Vector2(position.x - origin.x, position.z - origin.z);
                if (candidateGaps[i] <= range && (toTarget.normalized - aim).sqrMagnitude < 1e-6f)
                    return $"assist → {candidateOwners[i].name}";
            }

            return "facing";
        }

        /// <summary>Debug-only: body gap, centre distance and angle from the aim, plus the swing outcome.</summary>
        private void LogCandidate(Component owner, Vector3 origin, Vector2 aim, Vector3 targetPosition, float gap, bool inArc)
        {
            Vector2 toTarget = new Vector2(targetPosition.x - origin.x, targetPosition.z - origin.z);
            float distance = toTarget.magnitude;
            float angle = Vector2.Angle(aim, toTarget);
            string outcome = inArc ? "hit" : gap > range ? "out of range" : "outside arc";

            VelvetLog.Info(LogCategory, this, $"  {owner.name}: gap {gap:F2}, centres {distance:F2}, angle {angle:F1}° from aim → {outcome}");
        }

        /// <summary>Living, not this player, and not any player (no friendly fire).</summary>
        private bool IsValidTarget(IDamageable target, Component owner)
        {
            if (!target.IsAlive || owner.CompareTag(PlayerTag))
                return false;

            Transform ownerTransform = owner.transform;
            return !ownerTransform.IsChildOf(transform) && !transform.IsChildOf(ownerTransform);
        }

        private void Strike(IDamageable target, Component owner, Vector3 origin, Vector3 targetPosition, Vector2 aim)
        {
            target.ApplyDamage(damage);

            IKnockbackable knockbackable = owner.GetComponentInParent<IKnockbackable>();
            if (knockbackable == null)
            {
                if (debugLogs)
                    VelvetLog.Info(LogCategory, this, $"Hit {owner.name} for {damage} damage (no IKnockbackable)");
                return;
            }

            Vector3 push = new Vector3(targetPosition.x - origin.x, 0f, targetPosition.z - origin.z);
            // A target right on top of the player is pushed along the swing instead.
            if (push.sqrMagnitude < 1e-6f)
                push = new Vector3(aim.x, 0f, aim.y);

            if (debugLogs)
                VelvetLog.Info(LogCategory, this, $"Hit {owner.name} for {damage} damage, knockback dir {push.normalized} force {knockbackForce} stun {stunSeconds}s");

            knockbackable.ApplyKnockback(push, knockbackForce, stunSeconds);
        }

        private void OnDrawGizmosSelected()
        {
            PlayerMovement facingSource = movement != null ? movement : GetComponent<PlayerMovement>();
            Vector2 facing = facingSource != null ? facingSource.FacingDirection : Vector2.down;
            if (facing.sqrMagnitude < Mathf.Epsilon)
                facing = Vector2.down;

            Vector3 origin = transform.position;
            Vector3 forward = new Vector3(facing.x, 0f, facing.y).normalized;
            float halfArc = arcDegrees * 0.5f;
            // Range is a gap from this body's surface, so the drawn reach adds its radius.
            float reach = range + BodyDistance.PlanarRadius(body != null ? body : GetComponent<Collider>());

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(origin, reach);

            const int segments = 24;
            Vector3 previous = origin + Quaternion.Euler(0f, -halfArc, 0f) * forward * reach;
            Gizmos.DrawLine(origin, previous);
            for (int i = 1; i <= segments; i++)
            {
                float angle = Mathf.Lerp(-halfArc, halfArc, i / (float)segments);
                Vector3 point = origin + Quaternion.Euler(0f, angle, 0f) * forward * reach;
                Gizmos.DrawLine(previous, point);
                previous = point;
            }
            Gizmos.DrawLine(origin, previous);
        }
    }
}
