using System;
using System.Threading;
using CodenameVelvet.Combat;
using CodenameVelvet.Core.Debugging;
using UnityEngine;

namespace CodenameVelvet.Gathering
{
    /// <summary>
    /// A harvestable node (tree, rock, mineral vein) driven by a <see cref="ResourceNodeDefinition"/>.
    /// Every sword hit drops pickups on the ground around it; after enough hits it is depleted:
    /// renderers and colliders turn off while the GameObject stays active, so the respawn
    /// wait can keep running. It comes back after the definition's respawn time, or stays
    /// depleted when that is 0. Takes damage but never knockback.
    /// </summary>
    public class ResourceNode : MonoBehaviour, IDamageable
    {
        private const string LogCategory = "Gather";

        [SerializeField] private ResourceNodeDefinition definition;
        [Tooltip("Pickup spawned for every dropped piece.")]
        [SerializeField] private ResourcePickup pickupPrefab;

        [Header("Drops")]
        [Tooltip("Pieces land on a ring around the node, on the ground plane (x = min radius, y = max radius).")]
        [SerializeField] private Vector2 dropRadius = new Vector2(0.6f, 1.2f);

        [Header("Debug")]
        [Tooltip("Logs every hit, the pieces dropped, depletion and respawn.")]
        [SerializeField] private bool debugLogs = true;

        private Renderer[] renderers;
        private Collider[] colliders;
        private int hitsRemaining;
        private bool isDepleted;
        private float respawnTime;
        private CancellationTokenSource respawnCancellation;

        public bool IsAlive => definition != null && !isDepleted;

        /// <summary>Raised on depletion, after the node hides.</summary>
        public event Action<ResourceNode> Depleted;

        /// <summary>
        /// True = never respawns by itself; its owner (e.g. a SpawnZone) calls <see cref="RespawnAt"/>.
        /// False (default) = respawns in place according to the definition.
        /// </summary>
        public bool ExternalRespawn { get; set; }

        /// <summary>Size multiplier given by the spawner (1 = as the prefab): the drop ring grows with it.</summary>
        public float SizeScale { get; set; } = 1f;

        /// <summary>Moves the node to <paramref name="position"/> and restores it fully.</summary>
        public void RespawnAt(Vector3 position)
        {
            StopRespawn();
            transform.position = position;
            Restore();
        }

        private void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            colliders = GetComponentsInChildren<Collider>(true);

            if (definition == null)
            {
                Debug.LogError($"{name}: ResourceNode needs a ResourceNodeDefinition.", this);
                return;
            }

            hitsRemaining = definition.HitsToDeplete;
            ApplyColor();
        }

        private void OnEnable()
        {
            // Resumes a respawn that was interrupted by disabling the node.
            if (isDepleted && !ExternalRespawn && definition != null && definition.RespawnSeconds > 0f)
                StartRespawn();
        }

        private void OnDisable()
        {
            StopRespawn();
        }

        /// <summary>
        /// One sword hit: drops pieces and counts towards depletion. <paramref name="amount"/>
        /// is ignored on purpose, so harvesting speed depends on the node, not on weapon damage.
        /// Hits on a depleted node are ignored.
        /// </summary>
        public void ApplyDamage(int amount)
        {
            if (!IsAlive)
                return;

            hitsRemaining--;
            int hitNumber = definition.HitsToDeplete - hitsRemaining;
            if (debugLogs)
                VelvetLog.Info(LogCategory, this, $"Hit {hitNumber}/{definition.HitsToDeplete}");

            DropPieces();

            if (hitsRemaining <= 0)
                Deplete();
        }

        private void DropPieces()
        {
            if (pickupPrefab == null)
            {
                if (debugLogs)
                    VelvetLog.Warn(LogCategory, this, "No pickup prefab assigned, nothing dropped");
                return;
            }

            for (int i = 0; i < definition.PiecesPerHit; i++)
            {
                ResourcePickup pickup = Instantiate(pickupPrefab, RandomDropPosition(), Quaternion.identity);
                pickup.Initialize(definition.Resource, 1);
            }

            if (debugLogs)
                VelvetLog.Info(LogCategory, this, $"Dropped {definition.PiecesPerHit} {definition.Resource}");
        }

        /// <summary>A random point on the drop ring around the node, at the node's height.</summary>
        private Vector3 RandomDropPosition()
        {
            float minRadius = Mathf.Max(0f, dropRadius.x);
            float maxRadius = Mathf.Max(minRadius, dropRadius.y);
            float radius = UnityEngine.Random.Range(minRadius, maxRadius) * SizeScale;
            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            return transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
        }

        private void Deplete()
        {
            isDepleted = true;
            SetPresent(false);
            Depleted?.Invoke(this);

            if (ExternalRespawn)
            {
                if (debugLogs)
                    VelvetLog.Info(LogCategory, this, "Depleted, waiting for its spawner");
                return;
            }

            if (definition.RespawnSeconds <= 0f)
            {
                if (debugLogs)
                    VelvetLog.Info(LogCategory, this, "Depleted for good");
                return;
            }

            respawnTime = Time.time + definition.RespawnSeconds;
            if (debugLogs)
                VelvetLog.Info(LogCategory, this, $"Depleted, respawns in {definition.RespawnSeconds:F0}s");

            StartRespawn();
        }

        private void Restore()
        {
            isDepleted = false;
            hitsRemaining = definition.HitsToDeplete;
            SetPresent(true);

            if (debugLogs)
                VelvetLog.Info(LogCategory, this, "Respawned");
        }

        /// <summary>
        /// Shows or hides the node without deactivating it. Every child renderer and collider
        /// is toggled, so ones disabled on purpose in the prefab come back enabled.
        /// </summary>
        private void SetPresent(bool present)
        {
            foreach (Renderer nodeRenderer in renderers)
                nodeRenderer.enabled = present;
            foreach (Collider nodeCollider in colliders)
                nodeCollider.enabled = present;
        }

        private void StartRespawn()
        {
            StopRespawn();
            respawnCancellation = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            RunRespawn(respawnCancellation.Token);
        }

        private void StopRespawn()
        {
            if (respawnCancellation == null)
                return;

            respawnCancellation.Cancel();
            respawnCancellation.Dispose();
            respawnCancellation = null;
        }

        /// <summary>Only async void in the node: the fire-and-forget entry point.</summary>
        private async void RunRespawn(CancellationToken token)
        {
            try
            {
                // Scaled time, so pausing the game (timeScale 0) freezes the countdown.
                while (Time.time < respawnTime)
                    await Awaitable.NextFrameAsync(token);

                Restore();
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

        private void ApplyColor()
        {
            foreach (SpriteRenderer spriteRenderer in GetComponentsInChildren<SpriteRenderer>(true))
                spriteRenderer.color = definition.Color;
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 origin = transform.position;
            Gizmos.color = Color.yellow;
            DrawPlanarCircle(origin, dropRadius.x);
            DrawPlanarCircle(origin, dropRadius.y);
        }

        private static void DrawPlanarCircle(Vector3 centre, float radius)
        {
            const int segments = 24;
            Vector3 previous = centre + new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                Vector3 point = centre + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                Gizmos.DrawLine(previous, point);
                previous = point;
            }
        }
    }
}
