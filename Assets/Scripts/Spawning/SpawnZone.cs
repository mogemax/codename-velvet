using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CodenameVelvet.Core.Debugging;
using CodenameVelvet.Core.Events;
using CodenameVelvet.DayNight;
using CodenameVelvet.Enemies;
using CodenameVelvet.Gathering;
using CodenameVelvet.Map;
using UnityEngine;

namespace CodenameVelvet.Spawning
{
    /// <summary>
    /// Map area that keeps its own population of enemies and resource nodes at random points.
    /// The area is this object's trigger BoxCollider: XZ says where, Y says which floor heights are valid
    /// (so nothing spawns on top of a pillar sticking out of the box).
    /// <list type="bullet">
    /// <item>On start it fills enemies and nodes (in view is allowed).</item>
    /// <item>A dead enemy is replaced after <see cref="enemyRespawnDelay"/> seconds.</item>
    /// <item>A depleted node comes back after <see cref="nodeRespawnSeconds"/> somewhere else in the zone.</item>
    /// <item>Respawns never happen in view of any camera or near a player.</item>
    /// <item>With <see cref="dayNightEvent"/>: keeps <see cref="dayEnemies"/> by day and <see cref="maxEnemies"/>
    /// scaled by the night threat at night. At dawn the extra enemies are removed while nobody sees them.</item>
    /// </list>
    /// Every timer uses scaled time, so nothing respawns while the game is frozen (game over).
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class SpawnZone : MonoBehaviour
    {
        private const string LogCategory = "SpawnZone";
        private const float SkyHeight = 500f; // placement rays start this far above the box

        [Serializable]
        public class EnemyEntry
        {
            public EnemyDefinition definition;
            [Min(0f)] public float weight = 1f;
        }

        [Serializable]
        public class NodeEntry
        {
            public ResourceNode prefab;
            [Min(0f)] public float weight = 1f;
        }

        [Header("Enemies")]
        [SerializeField] private EnemyBrain enemyPrefab;
        [SerializeField] private EnemyEntry[] enemies = new EnemyEntry[0];
        [Tooltip("Enemies kept alive (at night: night 1 with 1 player; grows with the threat).")]
        [SerializeField, Min(0)] private int maxEnemies = 6;
        [Tooltip("Enemies kept alive by day. Only used with a Day Night Event.")]
        [SerializeField, Min(0)] private int dayEnemies = 2;
        [Tooltip("Empty = always maxEnemies (no day/night cycle).")]
        [SerializeField] private DayNightEvent dayNightEvent;
        [Tooltip("Seconds (min, max) before a dead enemy is replaced.")]
        [SerializeField] private Vector2 enemyRespawnDelay = new Vector2(20f, 40f);

        [Header("Resources")]
        [SerializeField] private NodeEntry[] nodes = new NodeEntry[0];
        [SerializeField, Min(0)] private int maxNodes = 4;
        [SerializeField, Min(0f)] private float nodeRespawnSeconds = 30f;

        [Header("Placement")]
        [Tooltip("Empty = 'Ground' layer.")]
        [SerializeField] private LayerMask groundMask;
        [Tooltip("Empty = 'Player' layer.")]
        [SerializeField] private LayerMask playerMask;
        [SerializeField, Range(0f, 89f)] private float maxSlope = 30f;
        [Tooltip("Minimum XZ distance between everything this zone spawns.")]
        [SerializeField, Min(0f)] private float minSpacing = 4f;
        [Tooltip("Nothing spawns closer than this to a player.")]
        [SerializeField, Min(0f)] private float playerClearance = 12f;
        [Tooltip("Free space a body needs, so nothing spawns inside rocks or walls.")]
        [SerializeField, Min(0.1f)] private float bodyRadius = 1f;
        [Tooltip("Extra margin around each view when checking a respawn is unseen (0.1 = 10%).")]
        [SerializeField, Min(0f)] private float viewportMargin = 0.1f;
        [SerializeField, Min(1)] private int maxAttempts = 25;
        [Tooltip("When no point is valid (e.g. all in view), retry after these seconds.")]
        [SerializeField, Min(0.1f)] private float retrySeconds = 2f;

        [Header("Debug")]
        [SerializeField] private bool debugLogs = true;

        private BoxCollider area;
        private System.Random random;
        private float[] enemyWeights;
        private float[] nodeWeights;
        private readonly List<EnemyBrain> liveEnemies = new List<EnemyBrain>();
        private readonly List<ResourceNode> spawnedNodes = new List<ResourceNode>();
        private readonly List<Vector3> occupied = new List<Vector3>();
        private int enemyTarget;
        private int pendingRespawns;
        private bool started;
        private Coroutine trimRoutine;

        private void Awake()
        {
            area = GetComponent<BoxCollider>();
            area.isTrigger = true;
            random = new System.Random(UnityEngine.Random.Range(int.MinValue, int.MaxValue));
            if (groundMask.value == 0) groundMask = LayerMask.GetMask("Ground");
            if (playerMask.value == 0) playerMask = LayerMask.GetMask("Player");
            enemyWeights = enemies.Select(e => e.definition != null ? e.weight : 0f).ToArray();
            nodeWeights = nodes.Select(n => n.prefab != null ? n.weight : 0f).ToArray();
            enemyTarget = dayNightEvent != null ? dayEnemies : maxEnemies;
        }

        private void OnEnable()
        {
            if (dayNightEvent != null) dayNightEvent.OnRaised += HandleDayNight;
        }

        private void OnDisable()
        {
            if (dayNightEvent != null) dayNightEvent.OnRaised -= HandleDayNight;
        }

        // Waits one frame: PlayerSpawner creates the players in its Start.
        private IEnumerator Start()
        {
            yield return null;

            for (int i = 0; i < maxNodes; i++)
                if (!TrySpawnNode()) StartCoroutine(RetryUntil(TrySpawnNode));
            for (int i = 0; i < enemyTarget; i++)
                if (!TrySpawnEnemy(allowVisible: true)) StartCoroutine(RetryUntil(SpawnIfBelowTarget));
            started = true;

            if (debugLogs)
                VelvetLog.Info(LogCategory, this, $"{liveEnemies.Count} enemies and {spawnedNodes.Count} resource nodes");
        }

        private bool TrySpawnEnemy(bool allowVisible)
        {
            int index = WeightedRandom.Pick(enemyWeights, random);
            if (enemyPrefab == null || index < 0 || !TryFindPoint(allowVisible, out Vector3 point))
                return false;

            EnemyBrain enemy = Instantiate(enemyPrefab, point, enemyPrefab.transform.rotation);
            enemy.name = $"{enemies[index].definition.DisplayName} ({name})";
            enemy.Initialize(enemies[index].definition);
            if (enemy.TryGetComponent(out EnemyHealth health))
                health.Died += _ => HandleEnemyDied(enemy);

            liveEnemies.Add(enemy);
            return true;
        }

        private void HandleEnemyDied(EnemyBrain enemy)
        {
            liveEnemies.Remove(enemy);
            pendingRespawns++;
            float delay = Mathf.Lerp(enemyRespawnDelay.x, enemyRespawnDelay.y, (float)random.NextDouble());
            StartCoroutine(AfterDelay(delay, () =>
            {
                pendingRespawns--;
                StartCoroutine(RetryUntil(SpawnIfBelowTarget));
            }));
        }

        /// <summary>Spawns one enemy out of sight, unless the target is already met (e.g. dawn came).</summary>
        private bool SpawnIfBelowTarget()
        {
            liveEnemies.RemoveAll(enemy => enemy == null);
            return liveEnemies.Count >= enemyTarget || TrySpawnEnemy(allowVisible: false);
        }

        private void HandleDayNight(DayNightState state)
        {
            enemyTarget = state.IsNight ? NightThreat.EnemyCount(maxEnemies, state.ThreatMultiplier) : dayEnemies;
            if (!started) return; // the initial fill already uses the new target

            liveEnemies.RemoveAll(enemy => enemy == null);
            int missing = enemyTarget - liveEnemies.Count - pendingRespawns;
            for (int i = 0; i < missing; i++)
                StartCoroutine(RetryUntil(SpawnIfBelowTarget));

            if (liveEnemies.Count > enemyTarget && trimRoutine == null)
                trimRoutine = StartCoroutine(TrimExcess());
        }

        /// <summary>Removes surplus enemies one at a time, only while no camera sees them.</summary>
        private IEnumerator TrimExcess()
        {
            var wait = new WaitForSeconds(retrySeconds);
            while (true)
            {
                liveEnemies.RemoveAll(enemy => enemy == null);
                if (liveEnemies.Count <= enemyTarget) break;

                EnemyBrain hidden = liveEnemies.FirstOrDefault(enemy => !IsVisibleToAnyCamera(enemy.transform.position));
                if (hidden != null)
                {
                    liveEnemies.Remove(hidden);
                    Destroy(hidden.gameObject);
                }
                yield return wait;
            }
            trimRoutine = null;
        }

        private bool TrySpawnNode()
        {
            int index = WeightedRandom.Pick(nodeWeights, random);
            if (index < 0 || !TryFindPoint(allowVisible: true, out Vector3 point))
                return false;

            ResourceNode node = Instantiate(nodes[index].prefab, point, nodes[index].prefab.transform.rotation);
            node.ExternalRespawn = true;
            node.Depleted += HandleNodeDepleted;
            spawnedNodes.Add(node);
            return true;
        }

        private void HandleNodeDepleted(ResourceNode node)
        {
            // The old position still counts as occupied, so the node comes back somewhere else.
            StartCoroutine(AfterDelay(nodeRespawnSeconds, () => StartCoroutine(RetryUntil(() =>
            {
                if (node == null) return true; // destroyed by something else: nothing to restore
                if (!TryFindPoint(allowVisible: false, out Vector3 point)) return false;
                node.RespawnAt(point);
                return true;
            }))));
        }

        private bool TryFindPoint(bool allowVisible, out Vector3 point)
        {
            RefreshOccupied();
            Bounds bounds = area.bounds;
            var halfExtents = new Vector2(bounds.extents.x, bounds.extents.z);
            var top = new Vector3(bounds.center.x, bounds.max.y + SkyHeight, bounds.center.z);

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                Vector3 origin = WanderPointPicker.PickPointInArea(top, halfExtents, random);
                if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, SkyHeight + bounds.size.y,
                        groundMask, QueryTriggerInteraction.Ignore))
                    continue;

                // The first surface hit must be inside the box height, not the top of a pillar.
                if (hit.point.y < bounds.min.y || hit.point.y > bounds.max.y) continue;
                if (WaterEdge.IsOverWater(hit.point)) continue;
                if (!SpawnRules.IsFlatEnough(hit.normal, maxSlope)) continue;
                if (!SpawnRules.IsClear(hit.point, occupied, minSpacing)) continue;

                Vector3 bodyCenter = hit.point + hit.normal * (bodyRadius + 0.05f);
                if (Physics.CheckSphere(bodyCenter, bodyRadius, groundMask, QueryTriggerInteraction.Ignore)) continue;
                if (Physics.CheckSphere(hit.point, playerClearance, playerMask, QueryTriggerInteraction.Ignore)) continue;
                if (!allowVisible && IsVisibleToAnyCamera(hit.point)) continue;

                point = hit.point;
                return true;
            }

            point = default;
            return false;
        }

        private void RefreshOccupied()
        {
            occupied.Clear();
            liveEnemies.RemoveAll(enemy => enemy == null);
            foreach (EnemyBrain enemy in liveEnemies)
                occupied.Add(enemy.transform.position);
            foreach (ResourceNode node in spawnedNodes)
                if (node != null) occupied.Add(node.transform.position);
        }

        private bool IsVisibleToAnyCamera(Vector3 point)
        {
            foreach (Camera cam in Camera.allCameras)
                if (SpawnRules.IsInsideViewport(cam.WorldToViewportPoint(point), viewportMargin))
                    return true;
            return false;
        }

        private static IEnumerator AfterDelay(float seconds, Action action)
        {
            yield return new WaitForSeconds(seconds);
            action();
        }

        private IEnumerator RetryUntil(Func<bool> attempt)
        {
            while (!attempt())
                yield return new WaitForSeconds(retrySeconds);
        }

        private void OnDrawGizmos()
        {
            var box = GetComponent<BoxCollider>();
            if (box == null) return;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.15f);
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.9f);
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}
