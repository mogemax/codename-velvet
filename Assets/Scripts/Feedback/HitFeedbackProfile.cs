using UnityEngine;

namespace CodenameVelvet.Feedback
{
    /// <summary>
    /// How one kind of thing reacts when it is hit (an enemy, the boss, a tree, a rock, a player): read by
    /// <see cref="HitReaction"/>. One asset per kind, tuned in the Inspector.
    /// </summary>
    [CreateAssetMenu(menuName = "Velvet/Feedback/Hit Feedback Profile", fileName = "Feedback_")]
    public class HitFeedbackProfile : ScriptableObject
    {
        [Header("Flash")]
        [Tooltip("Color the sprites flash to; alpha = how much.")]
        public Color flashColor = new Color(1f, 1f, 1f, 0.85f);
        [Min(0f)] public float flashSeconds = 0.1f;

        [Header("Body")]
        [Tooltip("x = extra width, y = extra height on a hit (squash = +x, -y).")]
        public Vector2 squash = new Vector2(0.22f, -0.18f);
        [Tooltip("Same, on the last hit (death, depletion): usually flatter.")]
        public Vector2 fatalSquash = new Vector2(0.35f, -0.45f);
        [Tooltip("Side-to-side rattle of the sprite, in world units (0 = none).")]
        [Min(0f)] public float joltAmplitude = 0.12f;
        [Min(0f)] public float joltSeconds = 0.18f;

        [Header("Hit stop (this object only)")]
        [Min(0f)] public float hitStopSeconds = 0.06f;
        [Min(0f)] public float fatalHitStopSeconds = 0.12f;

        [Header("Particles")]
        public BurstStyle hitBurst;
        [Tooltip("Extra burst on the last hit.")]
        public BurstStyle fatalBurst;
        [Tooltip("Height above the feet where bursts start.")]
        public float burstHeight = 1.2f;

        [Header("Camera")]
        [Tooltip("Shake of the cameras near it on the last hit (0-1).")]
        [Range(0f, 1f)] public float fatalCameraShake = 0.2f;

        [Header("Damage numbers")]
        [Tooltip("Empty = no numbers.")]
        public DamageNumberStyle damageNumbers;
        public float numberHeight = 3f;
    }
}
