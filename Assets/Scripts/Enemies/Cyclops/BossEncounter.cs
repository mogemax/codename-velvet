using UnityEngine;

namespace CodenameVelvet.Enemies
{
    /// <summary>
    /// Boss fight lock-in, God of War style: the first time the boss sees a player, invisible
    /// walls rise around the arena box so nobody can leave, and HUD that doesn't belong in the
    /// fight (the day/night clock) is hidden. Everything is undone when the boss dies.
    /// shortcut: a co-op player who dies respawns at their spawn point, outside the walls,
    /// and can't get back in; move their respawn into the arena if co-op boss fights matter.
    /// </summary>
    public class BossEncounter : MonoBehaviour
    {
        [Header("Boss")]
        [SerializeField] private CyclopsBrain brain;
        [SerializeField] private EnemyHealth health;

        [Header("Arena")]
        [Tooltip("Box that the walls are raised around (usually the boss's wander area).")]
        [SerializeField] private BoxCollider arenaArea;
        [SerializeField, Min(0.1f)] private float wallThickness = 1f;
        [Tooltip("Optional visible walls, fog, etc. Turned on only during the fight.")]
        [SerializeField] private GameObject[] showDuringFight;

        [Header("HUD")]
        [Tooltip("Turned off during the fight (e.g. the day/night label).")]
        [SerializeField] private GameObject[] hideDuringFight;

        [Header("Music")]
        [Tooltip("Level music source; switches to Fight Music during the fight and back afterwards.")]
        [SerializeField] private AudioSource music;
        [SerializeField] private AudioClip fightMusic;

        private GameObject walls;
        private AudioClip levelMusic;

        /// <summary>True from the moment the boss first sees a player until it dies.</summary>
        public bool InFight { get; private set; }

        private void Awake()
        {
            if (brain == null || health == null || arenaArea == null)
                Debug.LogWarning($"{name}: BossEncounter needs Brain, Health and Arena Area assigned.", this);
            else
                walls = BuildWalls();

            SetActive(showDuringFight, false);
        }

        private void Update()
        {
            // EnemyHealth destroys the boss on death, which nulls the references.
            if (health == null || !health.IsAlive)
            {
                if (InFight) SetFight(false);
                enabled = false;
                return;
            }

            if (!InFight && brain != null && brain.HasTarget)
                SetFight(true);
        }

        private void SetFight(bool inFight)
        {
            InFight = inFight;
            if (walls != null) walls.SetActive(inFight);
            SetActive(showDuringFight, inFight);
            SetActive(hideDuringFight, !inFight);
            SwitchMusic(inFight);
        }

        private void SwitchMusic(bool inFight)
        {
            if (music == null || fightMusic == null) return;
            if (inFight) levelMusic = music.clip;
            music.clip = inFight ? fightMusic : levelMusic;
            music.Play();
        }

        /// <summary>Four solid walls hugging the outside of the arena box, in its local space so rotation and scale carry over.</summary>
        private GameObject BuildWalls()
        {
            Transform area = arenaArea.transform;
            var root = new GameObject("BossArenaWalls");
            root.transform.SetParent(area, false);
            root.SetActive(false);

            Vector3 scale = area.lossyScale;
            float tx = wallThickness / Mathf.Max(Mathf.Abs(scale.x), 0.0001f);
            float tz = wallThickness / Mathf.Max(Mathf.Abs(scale.z), 0.0001f);
            Vector3 c = arenaArea.center;
            Vector3 s = arenaArea.size;

            AddWall(root.transform, "West", c + new Vector3(-(s.x + tx) * 0.5f, 0f, 0f), new Vector3(tx, s.y, s.z + 2f * tz));
            AddWall(root.transform, "East", c + new Vector3((s.x + tx) * 0.5f, 0f, 0f), new Vector3(tx, s.y, s.z + 2f * tz));
            AddWall(root.transform, "South", c + new Vector3(0f, 0f, -(s.z + tz) * 0.5f), new Vector3(s.x + 2f * tx, s.y, tz));
            AddWall(root.transform, "North", c + new Vector3(0f, 0f, (s.z + tz) * 0.5f), new Vector3(s.x + 2f * tx, s.y, tz));
            return root;
        }

        private static void AddWall(Transform parent, string wallName, Vector3 localCenter, Vector3 localSize)
        {
            var wall = new GameObject(wallName);
            wall.transform.SetParent(parent, false);
            BoxCollider box = wall.AddComponent<BoxCollider>();
            box.center = localCenter;
            box.size = localSize;
        }

        private static void SetActive(GameObject[] objects, bool active)
        {
            if (objects == null) return;
            foreach (GameObject go in objects)
                if (go != null) go.SetActive(active);
        }

        private void OnDrawGizmosSelected()
        {
            if (arenaArea == null) return;
            Gizmos.color = Color.red;
            Gizmos.matrix = arenaArea.transform.localToWorldMatrix;
            Gizmos.DrawWireCube(arenaArea.center, arenaArea.size);
        }
    }
}
