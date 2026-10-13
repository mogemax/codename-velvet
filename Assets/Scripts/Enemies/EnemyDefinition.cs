using UnityEngine;

namespace CodenameVelvet.Enemies
{
    /// <summary>
    /// Tuning data for one enemy type (minotaur, siren, witch...). Every type
    /// shares the same prefab and <see cref="EnemyBrain"/>; only this asset changes.
    /// </summary>
    [CreateAssetMenu(menuName = "Enemies/Enemy Definition", fileName = "New EnemyDefinition")]
    public class EnemyDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string displayName = "Enemy";
        [Tooltip("Placeholder tint applied to the enemy's child SpriteRenderers.")]
        [SerializeField] private Color color = Color.white;

        [Header("Health")]
        [Tooltip("Hit points of a freshly spawned enemy of this type. Minimum 1.")]
        [SerializeField] private int maxHp = 3;

        [Header("Movement")]
        [SerializeField] private float wanderSpeed = 1.5f;
        [SerializeField] private float chaseSpeed = 3.5f;
        [Tooltip("Wander points are picked within this radius of the spawn position.")]
        [SerializeField] private float wanderRadius = 5f;
        [Tooltip("Random rest time between wander trips, in seconds (x = min, y = max).")]
        [SerializeField] private Vector2 restTimeRange = new Vector2(1f, 3f);

        [Header("Aggro")]
        [Tooltip("A living player inside this range starts a chase.")]
        [SerializeField] private float detectionRange = 6f;
        [Tooltip("The chase is dropped beyond this range. Always >= detection range.")]
        [SerializeField] private float loseInterestRange = 9f;
        [Tooltip("Seconds between nearest-player searches.")]
        [SerializeField] private float retargetInterval = 0.25f;

        [Header("Attack")]
        [SerializeField] private float attackRange = 1.2f;
        [SerializeField] private int attackDamage = 1;
        [Tooltip("Seconds between hits.")]
        [SerializeField] private float attackCooldown = 1f;

        public string DisplayName => displayName;
        public Color Color => color;
        public int MaxHp => maxHp;
        public float WanderSpeed => wanderSpeed;
        public float ChaseSpeed => chaseSpeed;
        public float WanderRadius => wanderRadius;
        public Vector2 RestTimeRange => restTimeRange;
        public float DetectionRange => detectionRange;
        public float LoseInterestRange => loseInterestRange;
        public float RetargetInterval => retargetInterval;
        public float AttackRange => attackRange;
        public int AttackDamage => attackDamage;
        public float AttackCooldown => attackCooldown;

        private void OnValidate()
        {
            maxHp = Mathf.Max(1, maxHp);
            wanderSpeed = Mathf.Max(0f, wanderSpeed);
            chaseSpeed = Mathf.Max(0f, chaseSpeed);
            wanderRadius = Mathf.Max(0f, wanderRadius);
            restTimeRange.x = Mathf.Max(0f, restTimeRange.x);
            restTimeRange.y = Mathf.Max(restTimeRange.x, restTimeRange.y);
            detectionRange = Mathf.Max(0f, detectionRange);
            loseInterestRange = Mathf.Max(detectionRange, loseInterestRange);
            retargetInterval = Mathf.Max(0f, retargetInterval);
            attackRange = Mathf.Max(0f, attackRange);
            attackDamage = Mathf.Max(0, attackDamage);
            attackCooldown = Mathf.Max(0f, attackCooldown);
        }
    }
}
