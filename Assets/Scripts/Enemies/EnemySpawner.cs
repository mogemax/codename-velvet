using System;
using CodenameVelvet.Core.Debugging;
using UnityEngine;

namespace CodenameVelvet.Enemies
{
    /// <summary>
    /// Spawns a fixed list of enemies at Start. Every enemy comes from the same
    /// prefab and gets its type through <see cref="EnemyBrain.Initialize"/>.
    /// Spawn points are used round-robin with a small random XZ offset so
    /// enemies sharing a point don't overlap.
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        private const string LogCategory = "Spawner";

        /// <summary>How many enemies of one type to spawn.</summary>
        [Serializable]
        public class SpawnEntry
        {
            [SerializeField] private EnemyDefinition definition;
            [SerializeField] private int count = 1;

            public EnemyDefinition Definition => definition;
            public int Count => count;
        }

        [SerializeField] private EnemyBrain enemyPrefab;
        [Tooltip("Used round-robin. Empty = spawn at this spawner's position.")]
        [SerializeField] private Transform[] spawnPoints;
        [SerializeField] private SpawnEntry[] entries;
        [Tooltip("Max random XZ offset from the spawn point, in units.")]
        [SerializeField] private float spawnJitter = 1f;

        [Header("Debug")]
        [Tooltip("Logs how many enemies of each type were spawned and skipped entries.")]
        [SerializeField] private bool debugLogs = true;

        private int nextSpawnPointIndex;
        private int spawnedCount; // Running index for unique instance names.

        private void Start()
        {
            if (enemyPrefab == null)
            {
                Debug.LogError($"{name}: EnemySpawner has no enemy prefab.", this);
                return;
            }
            if (entries == null)
                return;

            for (int entryIndex = 0; entryIndex < entries.Length; entryIndex++)
            {
                SpawnEntry entry = entries[entryIndex];
                if (entry.Definition == null)
                {
                    if (debugLogs)
                        VelvetLog.Warn(LogCategory, this, $"Entry {entryIndex} has no EnemyDefinition, skipped");
                    continue;
                }

                for (int i = 0; i < entry.Count; i++)
                    Spawn(entry.Definition);

                if (debugLogs)
                    VelvetLog.Info(LogCategory, this, $"Spawned {entry.Count} × {entry.Definition.DisplayName}");
            }
        }

        private void Spawn(EnemyDefinition definition)
        {
            Vector2 jitter = UnityEngine.Random.insideUnitCircle * spawnJitter;
            Vector3 position = NextSpawnPosition() + new Vector3(jitter.x, 0f, jitter.y);

            EnemyBrain enemy = Instantiate(enemyPrefab, position, enemyPrefab.transform.rotation);
            spawnedCount++;
            enemy.name = $"{definition.DisplayName}#{spawnedCount}";
            enemy.Initialize(definition);
        }

        private Vector3 NextSpawnPosition()
        {
            if (spawnPoints == null || spawnPoints.Length == 0)
                return transform.position;

            Transform point = spawnPoints[nextSpawnPointIndex % spawnPoints.Length];
            nextSpawnPointIndex++;
            return point != null ? point.position : transform.position;
        }
    }
}
