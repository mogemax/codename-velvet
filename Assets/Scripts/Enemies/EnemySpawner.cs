using System;
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

        private int nextSpawnPointIndex;

        private void Start()
        {
            if (enemyPrefab == null)
            {
                Debug.LogError($"{name}: EnemySpawner has no enemy prefab.", this);
                return;
            }
            if (entries == null)
                return;

            foreach (SpawnEntry entry in entries)
            {
                if (entry.Definition == null)
                    continue;

                for (int i = 0; i < entry.Count; i++)
                    Spawn(entry.Definition);
            }
        }

        private void Spawn(EnemyDefinition definition)
        {
            Vector2 jitter = UnityEngine.Random.insideUnitCircle * spawnJitter;
            Vector3 position = NextSpawnPosition() + new Vector3(jitter.x, 0f, jitter.y);

            EnemyBrain enemy = Instantiate(enemyPrefab, position, enemyPrefab.transform.rotation);
            enemy.name = definition.DisplayName;
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
