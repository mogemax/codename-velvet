using System.Collections.Generic;
using UnityEngine;

namespace CodenameVelvet.Cameras
{
    /// <summary>
    /// Trauma-based shake: impacts add trauma that decays over time, and the shake grows with trauma² so small
    /// hits stay subtle. <see cref="CameraFollow"/> adds <see cref="Offset"/> and <see cref="Roll"/> on top of its
    /// framing. Uses scaled time, so a paused game freezes the shake.
    /// </summary>
    [RequireComponent(typeof(CameraFollow))]
    public class CameraShake : MonoBehaviour
    {
        private static readonly List<CameraShake> Active = new List<CameraShake>();

        [Tooltip("Max offset at full trauma, in world units.")]
        [SerializeField, Min(0f)] private float maxOffset = 1f;
        [Tooltip("Max roll at full trauma, in degrees.")]
        [SerializeField, Min(0f)] private float maxRoll = 3f;
        [SerializeField, Min(0.1f)] private float frequency = 50f;
        [Tooltip("Trauma lost per second (1 = a full-strength shake lasts one second).")]
        [SerializeField, Min(0.1f)] private float recovery = 2f;
        [Tooltip("Impacts this far from the camera's focus (or farther) do not shake it.")]
        [SerializeField, Min(0.1f)] private float falloffDistance = 30f;

        private CameraFollow follow;
        private float trauma;
        private float seed;

        public Vector3 Offset { get; private set; }
        public float Roll { get; private set; }

        /// <summary>Shakes every camera near <paramref name="position"/>: full strength at the impact, none at the falloff distance.</summary>
        public static void Impulse(Vector3 position, float strength)
        {
            foreach (CameraShake shake in Active)
                shake.AddTrauma(strength * shake.Falloff(position));
        }

        public void AddTrauma(float amount) => trauma = Mathf.Clamp01(trauma + Mathf.Max(0f, amount));

        private void Awake()
        {
            follow = GetComponent<CameraFollow>();
            seed = Random.value * 100f;
        }

        private void OnEnable() => Active.Add(this);
        private void OnDisable() => Active.Remove(this);

        // Update, so the offset is ready before CameraFollow applies it in LateUpdate.
        private void Update()
        {
            trauma = Mathf.Max(0f, trauma - recovery * Time.deltaTime);
            float amount = trauma * trauma;
            float t = Time.time * frequency;
            Offset = new Vector3(Noise(t, 0), Noise(t, 1), 0f) * (maxOffset * amount);
            Roll = Noise(t, 2) * maxRoll * amount;
        }

        private float Falloff(Vector3 position)
        {
            Vector3 delta = position - follow.FocusPoint;
            delta.y = 0f;
            return 1f - Mathf.Clamp01(delta.magnitude / falloffDistance);
        }

        private float Noise(float t, int channel) => Mathf.PerlinNoise(seed + channel * 31.7f, t) * 2f - 1f;
    }
}
