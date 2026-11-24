using UnityEngine;

namespace CodenameVelvet.Feedback
{
    /// <summary>
    /// Local hit stop: freezes this object's animation for a few hundredths of a second when a hit lands, so the
    /// impact reads. Only the attacker and the target freeze, never the game (with four players a global freeze would
    /// stutter all the time). Gameplay keeps running: knockback and timers are untouched. Real time, so it lasts the
    /// same whatever Time.timeScale is.
    /// </summary>
    public class HitStop : MonoBehaviour
    {
        private Animator[] animators;
        private float[] speeds;
        private float frozenUntil;
        private bool isFrozen;

        private void Awake() => enabled = false;

        public void Freeze(float seconds)
        {
            if (seconds <= 0f)
                return;

            if (!isFrozen)
            {
                animators = GetComponentsInChildren<Animator>();
                speeds = new float[animators.Length];
                for (int i = 0; i < animators.Length; i++)
                {
                    speeds[i] = animators[i].speed;
                    animators[i].speed = 0f;
                }
                isFrozen = true;
            }
            frozenUntil = Mathf.Max(frozenUntil, Time.unscaledTime + seconds);
            enabled = true;
        }

        private void Update()
        {
            if (Time.unscaledTime >= frozenUntil)
                enabled = false;
        }

        private void OnDisable()
        {
            if (!isFrozen)
                return;
            for (int i = 0; i < animators.Length; i++)
                if (animators[i] != null)
                    animators[i].speed = speeds[i];
            isFrozen = false;
        }
    }
}
