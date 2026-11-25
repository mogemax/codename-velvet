using System;
using UnityEngine;
using UnityEngine.Events;

namespace CodenameVelvet.Enemies
{
    /// <summary>
    /// Data for one cyclops attack. The hit happens <see cref="windup"/> seconds after the
    /// attack starts (or when the clip fires the Animation Event, if enabled on the brain),
    /// damages every player inside a sphere of <see cref="impactRadius"/> placed
    /// <see cref="impactForwardOffset"/> in front of the cyclops, and then the cyclops stays
    /// committed for <see cref="recovery"/> seconds before it can act again.
    /// </summary>
    [Serializable]
    public class CyclopsAttack
    {
        [Tooltip("Only identifies it in the Inspector and the logs.")]
        public string name = "Attack";
        [Tooltip("Animator trigger that starts this attack's animation.")]
        public string animatorTrigger = "Attack";
        [Tooltip("Relative chance against the other available attacks.")]
        [Min(0f)] public float weight = 1f;

        [Header("Range (gap between bodies, on the ground plane)")]
        [Min(0f)] public float minGap = 0f;
        [Min(0f)] public float maxGap = 2f;

        [Header("Timing (seconds)")]
        [Tooltip("From the start of the attack to the hit.")]
        [Min(0f)] public float windup = 0.6f;
        [Tooltip("From the hit until it can move again (e.g. getting up from the ground).")]
        [Min(0f)] public float recovery = 1f;
        [Tooltip("Time before THIS attack can be used again.")]
        [Min(0f)] public float cooldown = 3f;

        [Header("Lunge (throws itself forward)")]
        [Tooltip("Distance covered toward the player during the attack. 0 = stays in place.")]
        [Min(0f)] public float lungeDistance = 0f;
        [Tooltip("Point of the windup where the lunge starts (0 = start, 1 = at the hit).")]
        [Range(0f, 1f)] public float lungeStart = 0.6f;

        [Header("Hit")]
        [Min(0)] public int damage = 1;
        [Min(0f)] public float impactRadius = 2f;
        [Tooltip("How far in front of the cyclops the hit lands (0 = at its feet).")]
        public float impactForwardOffset = 1f;
        [Min(0f)] public float knockbackForce = 8f;
        [Min(0f)] public float knockbackStun = 0.5f;

        [Tooltip("Camera shake at the hit (0 = none, 1 = a normal full shake, 2 = huge), fading with distance from each player.")]
        [Range(0f, 2f)] public float cameraShake = 0.6f;

        [Tooltip("Called at the hit with the impact position (VFX, sound).")]
        public UnityEvent<Vector3> onImpact = new UnityEvent<Vector3>();

        [NonSerialized] public float readyTime;

        public bool IsReady(float now) => now >= readyTime;
        public bool CoversGap(float gap) => gap >= minGap && gap <= maxGap;
    }
}
