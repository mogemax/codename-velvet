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
    /// Map area that keeps its own population of enemies and resource nodes with an organic spread.
    /// The area is this object's trigger BoxCollider, rotated and scaled as drawn: XZ says where, Y says which
    /// floor heights are valid (so nothing spawns on top of a pillar sticking out of the box).
    /// <list type="bullet">
    /// <item>How many: a density per 1000 m² of usable ground (measured on start: flat, dry and inside the box),
    /// capped.</item>
    /// <item>Where: a smooth noise shared by every zone makes dense patches and empty clearings. Resources come in
    /// clusters (size per entry) around a center point; the center piece is bigger, the rest smaller, with random
    /// scale and left/right flip.</item>
    /// <item>Every zone sees what the others placed, so overlapping zones never stack things.</item>
    /// <item>On start it fills enemies and nodes (in view is allowed).</item>
    /// <item>A dead enemy is replaced after <see cref="enemyRespawnDelay"/> seconds, somewhere else.</item>
    /// <item>A depleted node comes back alone after <see cref="nodeRespawnSeconds"/>, at a new random spot.</item>
    /// <item>Respawns never happen near a player, nor in view of any camera (unless <see cref="respawnInView"/>).</item>
    /// <item>With <see cref="dayNightEvent"/>: keeps <see cref="dayEnemies"/> by day and, at night, the
    /// area's count scaled by the night threat (never fewer than by day, never more than <see cref="maxEnemies"/>).
    /// At dawn the extra enemies are removed while nobody sees them.</item>
    /// </list>
    /// Every timer uses scaled time, so nothing respawns while the game is frozen (game over).
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class SpawnZone : MonoBehaviour
    {
        private const string LogCategory = "SpawnZone";
        private const float SkyHeight = 500f; // placement rays start this far above the box
        private const int AreaSamples = 16;    // usable ground is measured on a 16 × 16 grid
        private const float BodyCheckScale = 0.75f; // a little give, so uneven ground does not reject big bodies
        private const float DefaultFootprint = 1f;
        private const float ClusterGap = 0.2f;  // members of a cluster almost touch, but never overlap
        private const int MemberAttempts = 8;

        // Every active zone, so each one sees what the others placed.
        private static readonly List<SpawnZone> ActiveZones = new List<SpawnZone>();
        private static Vector2 noiseOffset;
        private static bool noiseReady;

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
            [Tooltip("Pieces per cluster (min, max). 1-1 = always alone.")]
            public Vector2Int clusterSize = new Vector2Int(1, 1);
            [Tooltip("Extra room (m) a cluster member may stand from the center piece, beyond touching it.")]
            [Min(0f)] public float clusterSpread = 2f;
            [Tooltip("Random scale (min, max). The center of a cluster takes the upper half, the rest the lower half.")]
            public Vector2 scaleRange = new Vector2(1f, 1f);
        }

        [Header("Enemies")]
        [SerializeField] private EnemyBrain enemyPrefab;
        [SerializeField] private EnemyEntry[] enemies = new EnemyEntry[0];
        [Tooltip("Enemies per 1000 m² of usable ground at night 1 with 1 player; grows with the threat. " +
                 "Without a Day Night Event, the count kept all the time.")]
        [SerializeField, Min(0f)] private float nightEnemyDensity = 0.6f;
        [Tooltip("Upper limit of enemies kept alive, whatever the area and the threat.")]
        [SerializeField, Min(0)] private int maxEnemies = 20;
        [Tooltip("Enemies kept alive by day. Only used with a Day Night Event.")]
        [SerializeField, Min(0)] private int dayEnemies = 2;
        [Tooltip("Empty = always the night count (no day/night cycle).")]
        [SerializeField] private DayNightEvent dayNightEvent;
        [Tooltip("Seconds (min, max) before a dead enemy is replaced.")]
        [SerializeField] private Vector2 enemyRespawnDelay = new Vector2(20f, 40f);

        [Header("Resources")]
        [SerializeField] private NodeEntry[] nodes = new NodeEntry[0];
        [Tooltip("Resource pieces per 1000 m² of usable ground, counting every piece of a cluster.")]
        [SerializeField, Min(0f)] private float nodeDensity = 2.5f;
        [Tooltip("Upper limit of resource pieces, whatever the area.")]
        [SerializeField, Min(0)] private int maxNodes = 40;
        [SerializeField, Min(0f)] private float nodeRespawnSeconds = 30f;
        [Tooltip("Respawns may happen in view, for places the cameras never leave (e.g. a boss arena).")]
        [SerializeField] private bool respawnInView;
        [Tooltip("Layers that walk through the nodes, e.g. a boss without pathfinding. Empty = solid for everyone.")]
        [SerializeField] private LayerMask nodePassThrough;

        [Header("Organic spread")]
        [Tooltip("Size (m) of the dense patches and empty clearings. Shared by every zone.")]
        [SerializeField, Min(1f)] private float patchSize = 60f;
        [Tooltip("Noise below this = clearing (nothing); above Full Patch = always allowed. Between, a ramp.")]
        [SerializeField, Range(0f, 1f)] private float emptyBelow = 0.3f;
        [SerializeField, Range(0f, 1f)] private float fullPatch = 0.65f;

        [Header("Placement")]
        [Tooltip("Empty = 'Ground' layer.")]
        [SerializeField] private LayerMask groundMask;
        [Tooltip("Empty = 'Player' layer.")]
        [SerializeField] private LayerMask playerMask;
        [SerializeField, Range(0f, 89f)] private float maxSlope = 30f;
        [Tooltip("Free space (m, edge to edge) between clusters and enemies.")]
        [SerializeField, Min(0f)] private float minSpacing = 4f;
        [Tooltip("Nothing spawns closer than this to a player.")]
        [SerializeField, Min(0f)] private float playerClearance = 12f;
        [Tooltip("Extra margin around each view when checking a respawn is unseen (0.1 = 10%).")]
        [SerializeField, Min(0f)] private float viewportMargin = 0.1f;
        [Tooltip("Random points tried for each spawn (including the ones that turn out invalid).")]
        [SerializeField, Min(1)] private int maxAttempts = 40;
        [Tooltip("When no point is valid (e.g. all in view), retry after these seconds.")]
        [SerializeField, Min(0.1f)] private float retrySeconds = 2f;

        [Header("Debug")]
        [SerializeField] private bool debugLogs = true;

        private BoxCollider area;
        private System.Random random;
        private float[] enemyWeights;
        private float[] nodeWeights;
        private float[] nodeFootprints;
        private float enemyFootprint;
        private float usableArea;
        private int nightEnemies;
        private int nodeCount;
        private readonly List<EnemyBrain> liveEnemies = new List<EnemyBrain>();
        private readonly Dictionary<ResourceNode, float> spawnedNodes = new Dictionary<ResourceNode, float>();
        private readonly List<SpawnBody> bodies = new List<SpawnBody>();
        private DayNightState lastState;
        private bool phaseKnown;
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
            nodeFootprints = nodes.Select(n => Footprint(n.prefab)).ToArray();
            enemyFootprint = Footprint(enemyPrefab);
            if (!noiseReady)
            {
                noiseOffset = new Vector2(UnityEngine.Random.Range(0f, 1000f), UnityEngine.Random.Range(0f, 1000f));
                noiseReady = true;
            }
        }

        private void OnEnable()
        {
            ActiveZones.Add(this);
            if (dayNightEvent != null) dayNightEvent.OnRaised += HandleDayNight;
        }

        private void OnDisable()
        {
            ActiveZones.Remove(this);
            if (dayNightEvent != null) dayNightEvent.OnRaised -= HandleDayNight;
        }

        // The Editor may skip the domain reload between Play sessions: new noise every session.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ActiveZones.Clear();
            noiseReady = false;
        }

        // Waits one frame: PlayerSpawner creates the players in its Start.
        private IEnumerator Start()
        {
            yield return null;

            usableArea = MeasureUsableArea();
            nodeCount = SpawnRules.CountForArea(usableArea, nodeDensity, maxNodes);
            nightEnemies = Mathf.Max(dayEnemies, SpawnRules.CountForArea(usableArea, nightEnemyDensity, maxEnemies));
            // Without a cycle the night count is kept all the time; with one, the phase announced so far decides.
            enemyTarget = dayNightEvent == null ? nightEnemies
                : phaseKnown && lastState.IsNight ? NightTarget(lastState.ThreatMultiplier)
                : dayEnemies;

            // Every cluster is one try; a try that finds no spot is retried later, out of view.
            int placed = 0;
            for (int attempt = 0; placed < nodeCount && attempt < nodeCount * 2; attempt++)
                placed += TrySpawnCluster(nodeCount - placed, allowVisible: true);
            for (int i = 0; i < enemyTarget; i++)
                if (!TrySpawnEnemy(allowVisible: true)) StartCoroutine(RetryUntil(SpawnIfBelowTarget));
            started = true;

            if (debugLogs)
                VelvetLog.Info(LogCategory, this, $"{usableArea:0} m² usable: {liveEnemies.Count} enemies (night base " +
                    $"{nightEnemies}) and {spawnedNodes.Count}/{nodeCount} resource pieces");
        }

        // ---- Enemies -------------------------------------------------------------------------------------

        private bool TrySpawnEnemy(bool allowVisible)
        {
            int index = WeightedRandom.Pick(enemyWeights, random);
            if (enemyPrefab == null || index < 0 || !TryFindSeed(allowVisible, enemyFootprint, useNoise: false, out Vector3 point))
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

        /// <summary>Spawns one enemy, unless the target is already met (e.g. dawn came).</summary>
        private bool SpawnIfBelowTarget()
        {
            liveEnemies.RemoveAll(enemy => enemy == null);
            return liveEnemies.Count >= enemyTarget || TrySpawnEnemy(respawnInView);
        }

        private void HandleDayNight(DayNightState state)
        {
            lastState = state;
            phaseKnown = true;
            if (!started) return; // Start sets the target once the area is measured

            enemyTarget = state.IsNight ? NightTarget(state.ThreatMultiplier) : dayEnemies;

            liveEnemies.RemoveAll(enemy => enemy == null);
            int missing = enemyTarget - liveEnemies.Count - pendingRespawns;
            for (int i = 0; i < missing; i++)
                StartCoroutine(RetryUntil(SpawnIfBelowTarget));

            if (liveEnemies.Count > enemyTarget && trimRoutine == null)
                trimRoutine = StartCoroutine(TrimExcess());
        }

        private int NightTarget(float threat) =>
            Mathf.Clamp(NightThreat.EnemyCount(nightEnemies, threat), dayEnemies, Mathf.Max(dayEnemies, maxEnemies));

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

        // ---- Resource clusters ---------------------------------------------------------------------------

        /// <summary>
        /// Places one cluster of a weighted-random resource: a bigger center piece on a free spot inside a dense
        /// patch, then smaller pieces around it. Returns how many pieces were placed (0 = no spot found).
        /// </summary>
        private int TrySpawnCluster(int remaining, bool allowVisible)
        {
            int index = WeightedRandom.Pick(nodeWeights, random);
            if (index < 0) return 0;
            NodeEntry entry = nodes[index];

            int size = SpawnRules.ClusterSize(entry.clusterSize, (float)random.NextDouble(), remaining);
            float centerScale = SpawnRules.ClusterMemberScale(true, entry.scaleRange, (float)random.NextDouble());
            float centerRadius = nodeFootprints[index] * centerScale;
            if (!TryFindSeed(allowVisible, centerRadius, useNoise: true, out Vector3 center))
                return 0;

            SpawnNode(entry, center, centerScale);
            int placed = 1;
            for (int i = 1; i < size; i++)
            {
                float scale = SpawnRules.ClusterMemberScale(false, entry.scaleRange, (float)random.NextDouble());
                float radius = nodeFootprints[index] * scale;
                if (TryFindMember(center, centerRadius, radius, entry.clusterSpread, out Vector3 point))
                {
                    SpawnNode(entry, point, scale);
                    placed++;
                }
            }
            return placed;
        }

        private void SpawnNode(NodeEntry entry, Vector3 point, float scale)
        {
            ResourceNode node = Instantiate(entry.prefab, point, entry.prefab.transform.rotation);
            node.transform.localScale = entry.prefab.transform.localScale * scale;
            node.SizeScale = scale;
            bool flip = random.NextDouble() < 0.5;
            foreach (SpriteRenderer sprite in node.GetComponentsInChildren<SpriteRenderer>(true))
                sprite.flipX = flip;
            foreach (Collider body in node.GetComponentsInChildren<Collider>(true))
                body.excludeLayers |= nodePassThrough;

            node.ExternalRespawn = true;
            node.Depleted += HandleNodeDepleted;
            spawnedNodes.Add(node, Footprint(node));
        }

        /// <summary>
        /// A depleted node comes back alone at a new random spot of the zone (patches respected), out of view
        /// unless <see cref="respawnInView"/>.
        /// Its old spot still counts as occupied until then.
        /// </summary>
        private void HandleNodeDepleted(ResourceNode node)
        {
            StartCoroutine(AfterDelay(nodeRespawnSeconds, () => StartCoroutine(RetryUntil(() =>
            {
                if (node == null) return true; // destroyed by something else: nothing to restore
                if (!TryFindSeed(respawnInView, spawnedNodes[node], useNoise: true, out Vector3 point)) return false;
                node.RespawnAt(point);
                return true;
            }))));
        }

        // ---- Placement -----------------------------------------------------------------------------------

        /// <summary>
        /// A free spot for a body of <paramref name="footprint"/> radius, at least <see cref="minSpacing"/> away
        /// from everything any zone placed. With <paramref name="useNoise"/>, clearings reject it and patches welcome it.
        /// </summary>
        private bool TryFindSeed(bool allowVisible, float footprint, bool useNoise, out Vector3 point)
        {
            RefreshBodies();
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                if (!TryGround(RandomTop(), out RaycastHit hit)) continue;
                if (useNoise && random.NextDouble() > PatchWeightAt(hit.point)) continue;
                if (!SpawnRules.IsClearOfBodies(hit.point, footprint, bodies, minSpacing)) continue;
                if (!HasRoom(hit, footprint) || IsNearPlayer(hit.point)) continue;
                if (!allowVisible && IsVisibleToAnyCamera(hit.point)) continue;

                point = hit.point;
                return true;
            }
            point = default;
            return false;
        }

        /// <summary>A spot next to a cluster's center piece: touching distance plus up to <paramref name="spread"/>.</summary>
        private bool TryFindMember(Vector3 center, float centerRadius, float radius, float spread, out Vector3 point)
        {
            RefreshBodies();
            for (int attempt = 0; attempt < MemberAttempts; attempt++)
            {
                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                float distance = centerRadius + radius + ClusterGap + (float)random.NextDouble() * spread;
                Vector3 xz = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
                Vector3 origin = new Vector3(xz.x, area.bounds.max.y + SkyHeight, xz.z);

                if (!TryGround(origin, out RaycastHit hit)) continue;
                if (!SpawnRules.IsClearOfBodies(hit.point, radius, bodies, ClusterGap)) continue;
                if (!HasRoom(hit, radius)) continue;

                point = hit.point;
                return true;
            }
            point = default;
            return false;
        }

        /// <summary>Ground the zone can use under <paramref name="origin"/>: inside the box, dry and flat enough.</summary>
        private bool TryGround(Vector3 origin, out RaycastHit hit)
        {
            if (!Physics.Raycast(origin, Vector3.down, out hit, origin.y - area.bounds.min.y, groundMask, QueryTriggerInteraction.Ignore))
                return false;
            // The first surface hit must be inside the box, not the top of a pillar.
            return Contains(hit.point)
                && !WaterEdge.IsOverWater(hit.point)
                && SpawnRules.IsFlatEnough(hit.normal, maxSlope);
        }

        /// <summary>No wall or slope cuts into the body (checked a little smaller, so uneven ground still passes).</summary>
        private bool HasRoom(RaycastHit hit, float footprint)
        {
            float checkRadius = footprint * BodyCheckScale;
            Vector3 bodyCenter = hit.point + hit.normal * (checkRadius + 0.05f);
            return !Physics.CheckSphere(bodyCenter, checkRadius, groundMask, QueryTriggerInteraction.Ignore);
        }

        private bool IsNearPlayer(Vector3 point) =>
            Physics.CheckSphere(point, playerClearance, playerMask, QueryTriggerInteraction.Ignore);

        private float PatchWeightAt(Vector3 point)
        {
            float noise = Mathf.PerlinNoise(point.x / patchSize + noiseOffset.x, point.z / patchSize + noiseOffset.y);
            return SpawnRules.PatchWeight(noise, emptyBelow, fullPatch);
        }

        /// <summary>Everything every active zone placed (enemies and nodes, depleted ones included).</summary>
        private void RefreshBodies()
        {
            bodies.Clear();
            foreach (SpawnZone zone in ActiveZones)
            {
                zone.liveEnemies.RemoveAll(enemy => enemy == null);
                foreach (EnemyBrain enemy in zone.liveEnemies)
                    bodies.Add(new SpawnBody(enemy.transform.position, zone.enemyFootprint));
                foreach (KeyValuePair<ResourceNode, float> node in zone.spawnedNodes)
                    if (node.Key != null) bodies.Add(new SpawnBody(node.Key.transform.position, node.Value));
            }
        }

        // ---- The box, as drawn (rotation and scale included) ---------------------------------------------

        /// <summary>A random point of the box's footprint, high above it, ready to cast down.</summary>
        private Vector3 RandomTop()
        {
            var local = new Vector3((float)random.NextDouble() - 0.5f, 0f, (float)random.NextDouble() - 0.5f);
            return TopAt(local);
        }

        /// <summary>The box's footprint at (-0.5..0.5, -0.5..0.5), high above the box.</summary>
        private Vector3 TopAt(Vector3 normalized)
        {
            Vector3 world = transform.TransformPoint(area.center + Vector3.Scale(normalized, area.size));
            return new Vector3(world.x, area.bounds.max.y + SkyHeight, world.z);
        }

        private bool Contains(Vector3 point)
        {
            Vector3 local = transform.InverseTransformPoint(point) - area.center;
            Vector3 half = area.size * 0.5f;
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
        }

        /// <summary>Square meters of the box that are usable ground, sampled on a regular grid.</summary>
        private float MeasureUsableArea()
        {
            int usable = 0;
            for (int x = 0; x < AreaSamples; x++)
            for (int z = 0; z < AreaSamples; z++)
            {
                var normalized = new Vector3((x + 0.5f) / AreaSamples - 0.5f, 0f, (z + 0.5f) / AreaSamples - 0.5f);
                if (TryGround(TopAt(normalized), out _)) usable++;
            }
            Vector3 scale = transform.lossyScale;
            float footprintArea = Mathf.Abs(area.size.x * scale.x * area.size.z * scale.z);
            return footprintArea * usable / (AreaSamples * AreaSamples);
        }

        /// <summary>Radius on the ground plane of a prefab's or instance's collider (its body), or 1 when it has none.</summary>
        private static float Footprint(Component body)
        {
            if (body == null || !body.TryGetComponent(out Collider collider)) return DefaultFootprint;
            Vector3 scale = body.transform.lossyScale;
            float planarScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            return collider switch
            {
                SphereCollider sphere => sphere.radius * planarScale,
                CapsuleCollider capsule => capsule.radius * planarScale,
                BoxCollider box => Mathf.Max(box.size.x, box.size.z) * 0.5f * planarScale,
                _ => DefaultFootprint,
            };
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
