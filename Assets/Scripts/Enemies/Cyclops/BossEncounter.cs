using CodenameVelvet.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CodenameVelvet.Enemies
{
    /// <summary>
    /// Boss fight lock-in, God of War style. Until every player is inside the arena the boss ignores them
    /// and takes no damage. Then, the first time the boss sees a player, invisible
    /// walls rise around the arena box so nobody can leave, and HUD that doesn't belong in the
    /// fight (the day/night clock) is hidden. Everything is undone when the boss dies.
    /// Players who die during the fight come back inside the arena, where they stood when it started.
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
        [Tooltip("How far past the edge a player must be to count as inside, so the walls never rise on top of them.")]
        [SerializeField, Min(0f)] private float entryMargin = 2f;
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

        /// <summary>Some players are in the arena and the fight is waiting for the rest.</summary>
        public bool WaitingForPlayers { get; private set; }

        private void Awake()
        {
            if (brain == null || health == null || arenaArea == null)
                Debug.LogWarning($"{name}: BossEncounter needs Brain, Health and Arena Area assigned.", this);
            else
            {
                walls = BuildWalls();
                SetCanEngage(false);
            }

            SetActive(showDuringFight, false);
        }

        private void Update()
        {
            // EnemyHealth destroys the boss on death, which nulls the references.
            if (health == null || !health.IsAlive)
            {
                if (InFight) SetFight(false);
                WaitingForPlayers = false;
                enabled = false;
                return;
            }

            if (InFight || brain == null) return;

            if (brain.HasTarget)
            {
                SetFight(true);
                KeepRespawnsInside();
            }
            else if (arenaArea != null) CheckPlayers();
        }

        // Everyone is inside when the fight starts, so where they stand is safe to come back to.
        private static void KeepRespawnsInside()
        {
            foreach (PlayerInput player in PlayerInput.all)
                if (player.TryGetComponent(out PlayerHealth playerHealth))
                    playerHealth.RespawnPoint = player.transform.position;
        }

        private void CheckPlayers()
        {
            int inside = 0;
            foreach (PlayerInput player in PlayerInput.all)
                if (IsInside(player)) inside++;

            bool everyone = inside == PlayerInput.all.Count;
            SetCanEngage(everyone);
            WaitingForPlayers = inside > 0 && !everyone;
        }

        private bool IsInside(PlayerInput player)
        {
            if (player.TryGetComponent(out PlayerHealth playerHealth) && !playerHealth.IsAlive) return false;

            Transform area = arenaArea.transform;
            Vector3 offset = area.InverseTransformPoint(player.transform.position) - arenaArea.center;
            return ArenaRules.Contains(Vector3.Scale(offset, area.lossyScale), Vector3.Scale(arenaArea.size, area.lossyScale), entryMargin);
        }

        private void SetCanEngage(bool canEngage)
        {
            brain.CanEngage = canEngage;
            health.Invulnerable = !canEngage;
        }

        private void SetFight(bool inFight)
        {
            InFight = inFight;
            WaitingForPlayers = false;
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
