using CodenameVelvet.Cameras;
using CodenameVelvet.Combat;
using UnityEngine;

namespace CodenameVelvet.Feedback
{
    /// <summary>
    /// Plays the hit feedback of its <see cref="HitFeedbackProfile"/> every time this object takes a hit (any
    /// <see cref="IDamageEvents"/> on it: players, enemies, the boss, resource nodes): flash, squash, rattle, local hit
    /// stop, particles, damage numbers and, on the last hit, a bigger burst and camera shake. Adds the small
    /// components it drives by itself.
    /// </summary>
    [DisallowMultipleComponent]
    public class HitReaction : MonoBehaviour
    {
        [SerializeField] private HitFeedbackProfile profile;

        private IDamageEvents source;
        private HitFlash flash;
        private SquashStretch squash;
        private Jolt jolt;
        private HitStop hitStop;

        private void Awake()
        {
            source = GetComponent<IDamageEvents>();
            if (source == null)
            {
                Debug.LogWarning($"{name}: HitReaction needs a component that announces hits (IDamageEvents).", this);
                enabled = false;
                return;
            }
            flash = FeedbackRig.GetOrAdd<HitFlash>(gameObject);
            squash = FeedbackRig.GetOrAdd<SquashStretch>(gameObject);
            jolt = FeedbackRig.GetOrAdd<Jolt>(gameObject);
            hitStop = FeedbackRig.GetOrAdd<HitStop>(gameObject);
        }

        private void OnEnable()
        {
            if (source != null) source.Damaged += Play;
        }

        private void OnDisable()
        {
            if (source != null) source.Damaged -= Play;
        }

        private void Play(DamageEvent hit)
        {
            if (profile == null)
                return;

            flash.Flash(profile.flashColor, profile.flashSeconds);
            squash.Kick(hit.IsFatal ? profile.fatalSquash : profile.squash);
            jolt.Shake(profile.joltAmplitude, profile.joltSeconds);
            hitStop.Freeze(hit.IsFatal ? profile.fatalHitStopSeconds : profile.hitStopSeconds);

            Vector3 feet = transform.position;
            Vector3 burstPoint = feet + Vector3.up * profile.burstHeight;
            float size = Mathf.Max(0.5f, transform.lossyScale.y); // bigger trees throw more chips
            Bursts.Emit(profile.hitBurst, burstPoint, size);

            if (hit.IsFatal)
            {
                Bursts.Emit(profile.fatalBurst, burstPoint, size);
                CameraShake.Impulse(feet, profile.fatalCameraShake);
            }

            DamageNumbers.Show(profile.damageNumbers, hit.Amount, feet + Vector3.up * profile.numberHeight);
        }
    }
}
