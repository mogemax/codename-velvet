using System.Collections;
using CodenameVelvet.Core;
using CodenameVelvet.Map;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CodenameVelvet.Enemies
{
    /// <summary>
    /// When the boat is built, freezes the match and moves every player's camera to the boss and back, to show where
    /// it is. Runs on real time, since the match is frozen meanwhile (<see cref="MatchFlow.SetCutscene"/>); the boss
    /// keeps animating.
    /// </summary>
    public class BossReveal : MonoBehaviour
    {
        [SerializeField] private Boat boat;
        [SerializeField] private Transform boss;
        [SerializeField] private MatchFlow matchFlow;

        [Header("Framing")]
        [Tooltip("Height above the boss's origin (its feet) the cameras center on.")]
        [SerializeField] private float focusHeight = 8.5f;
        [Tooltip("Extra height halfway, so the cameras fly over the terrain in between.")]
        [SerializeField, Min(0f)] private float arcHeight = 15f;

        [Header("Seconds")]
        [Tooltip("Before it starts, so the last part of the boat is seen appearing.")]
        [SerializeField, Min(0f)] private float startDelay = 0.8f;
        [SerializeField, Min(0.1f)] private float goSeconds = 2f;
        [SerializeField, Min(0f)] private float holdSeconds = 2f;
        [SerializeField, Min(0.1f)] private float returnSeconds = 1.5f;

        private bool isPlaying;
        private Animator bossAnimator;
        private AnimatorUpdateMode bossUpdateMode;

        private void OnEnable()
        {
            if (boat != null) boat.Built += Play;
        }

        private void OnDisable()
        {
            if (boat != null) boat.Built -= Play;
            StopAllCoroutines();
            End();
        }

        private void Play() => StartCoroutine(Reveal());

        private IEnumerator Reveal()
        {
            yield return new WaitForSeconds(startDelay); // scaled: a pause, game over or level change holds it
            if (matchFlow == null || boss == null) yield break;
            if (boss.TryGetComponent(out EnemyHealth health) && !health.IsAlive) yield break;

            Begin();
            Vector3 point = boss.position + Vector3.up * focusHeight;
            float total = goSeconds + holdSeconds + returnSeconds;
            for (float time = 0f; time < total; time += Time.unscaledDeltaTime)
            {
                float blend = Mathf.Min(time / goSeconds, (total - time) / returnSeconds, 1f); // there, hold, back
                LookAt(point + Vector3.up * (4f * arcHeight * (1f - blend)), blend);
                yield return null;
            }
            End();
        }

        private void Begin()
        {
            isPlaying = true;
            matchFlow.SetCutscene(true);
            bossAnimator = boss.GetComponentInChildren<Animator>();
            if (bossAnimator == null) return;
            bossUpdateMode = bossAnimator.updateMode;
            bossAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
        }

        private void End()
        {
            if (!isPlaying) return;
            isPlaying = false;
            LookAt(Vector3.zero, 0f);
            if (bossAnimator != null) bossAnimator.updateMode = bossUpdateMode;
            if (matchFlow != null) matchFlow.SetCutscene(false);
        }

        private static void LookAt(Vector3 point, float blend)
        {
            foreach (PlayerInput player in PlayerInput.all)
                if (player.camera != null && player.camera.TryGetComponent(out CameraFollow follow))
                    follow.SetCinematic(point, blend);
        }
    }
}
