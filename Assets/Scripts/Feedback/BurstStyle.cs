using UnityEngine;

namespace CodenameVelvet.Feedback
{
    /// <summary>
    /// One kind of particle burst (wood chips, stone shards, sparks, dust, a poof). Emitted through
    /// <see cref="Bursts"/>, which keeps one shared particle system per style: no objects are created per burst.
    /// </summary>
    [CreateAssetMenu(menuName = "Velvet/Feedback/Burst Style", fileName = "Burst_")]
    public class BurstStyle : ScriptableObject
    {
        [Tooltip("\"Velvet/Particle\" material: round (soft dot) or square (chips).")]
        public Material material;
        [Tooltip("Each particle takes a random color between these two.")]
        public Color colorA = Color.white;
        public Color colorB = Color.white;
        [Tooltip("Particles per burst (min, max).")]
        public Vector2Int count = new Vector2Int(6, 10);
        [Tooltip("Initial speed (min, max), units/second.")]
        public Vector2 speed = new Vector2(2f, 5f);
        [Tooltip("0 = flat ring on the ground, 1 = straight up.")]
        [Range(0f, 1f)] public float upward = 0.6f;
        [Tooltip("World size (min, max).")]
        public Vector2 size = new Vector2(0.1f, 0.2f);
        [Tooltip("Seconds (min, max).")]
        public Vector2 lifetime = new Vector2(0.35f, 0.6f);
        [Tooltip("1 = normal gravity, 0 = floats.")]
        public float gravity = 1f;
        [Tooltip("Speed lost over its life (0 = none, 1 = a lot).")]
        [Range(0f, 1f)] public float drag = 0.2f;
        [Tooltip("Particles start this far from the center.")]
        [Min(0f)] public float spawnRadius = 0.15f;
        [Tooltip("Shrinks to nothing over its life (otherwise only fades).")]
        public bool shrink = true;
        [Tooltip("Spins while flying (chips).")]
        public bool spin;
        [Tooltip("Draws over sprites with a lower order.")]
        public int sortingOrder = 50;
    }
}
