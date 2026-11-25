using System;
using System.Threading;
using CodenameVelvet.Player;
using UnityEngine;

namespace CodenameVelvet.Enemies
{
    /// <summary>
    /// An archer's bow (<see cref="EnemyWeapon.Bow"/>): <see cref="ShootAsync"/> shows it in hand, pulls the string
    /// back while aiming at the target (the players' warning), then lets an <see cref="Arrow"/> go that hurts players
    /// only. Hidden the rest of the time. Scaled time, like the rest of the enemy.
    /// </summary>
    public class EnemyBow : MonoBehaviour
    {
        [SerializeField] private Arrow arrowPrefab;
        [Tooltip("The bow in pieces (grip, limbs, string), the same the players use.")]
        [SerializeField] private HeldBowView heldBowPrefab;
        [Tooltip("The character's drawing (it faces the camera): the bow goes under it.")]
        [SerializeField] private Transform drawing;
        [Tooltip("Height of the bow in world units (the enemy is about 5).")]
        [SerializeField, Min(0.1f)] private float heldSize = 4.5f;
        [Tooltip("Where the bow is held, in world units: sideways towards the shot, and up from the feet.")]
        [SerializeField] private Vector2 heldOffset = new Vector2(1.2f, 2.2f);
        [Tooltip("Height above the feet the arrow flies at.")]
        [SerializeField, Min(0f)] private float shotHeight = 2f;
        [Tooltip("Seconds the bow stays in hand after the release (the string snaps back meanwhile).")]
        [SerializeField, Min(0.01f)] private float showAfterRelease = 0.3f;

        private const float SnapSeconds = 0.08f; // the string springs back after the release

        private HeldBowView bow;

        private void Awake()
        {
            if (heldBowPrefab != null && drawing != null)
                bow = HeldBowView.CreateIn(heldBowPrefab, drawing, heldSize);
        }

        /// <summary>
        /// Draws for <paramref name="drawSeconds"/> and shoots at <paramref name="target"/>. False if
        /// <paramref name="canKeepDrawing"/> turns false (stunned, dead) or the target is gone first: nothing is shot.
        /// </summary>
        public async Awaitable<bool> ShootAsync(Transform target, int damage, float drawSeconds, Func<bool> canKeepDrawing, CancellationToken token)
        {
            if (bow == null || arrowPrefab == null) return false;

            bow.gameObject.SetActive(true);
            bow.ShowArrow(true);
            try
            {
                Vector2 aim = Vector2.right;
                for (float start = Time.time; Time.time - start < drawSeconds; )
                {
                    if (target == null || !canKeepDrawing()) return false;
                    aim = AimAt(target);
                    Hold(aim, (Time.time - start) / drawSeconds);
                    await Awaitable.NextFrameAsync(token);
                }

                if (target == null || !canKeepDrawing()) return false;
                aim = AimAt(target);
                Arrow arrow = Instantiate(arrowPrefab, transform.position + Vector3.up * shotHeight, Quaternion.identity);
                arrow.Launch(aim, damage, shotByEnemy: true);

                bow.ShowArrow(false);
                for (float start = Time.time; Time.time - start < showAfterRelease; )
                {
                    Hold(aim, 1f - (Time.time - start) / SnapSeconds);
                    await Awaitable.NextFrameAsync(token);
                }
                return true;
            }
            finally
            {
                if (bow != null) bow.gameObject.SetActive(false);
            }
        }

        private Vector2 AimAt(Transform target)
        {
            Vector3 toTarget = target.position - transform.position;
            var aim = new Vector2(toTarget.x, toTarget.z);
            return aim.sqrMagnitude > 0.0001f ? aim.normalized : Vector2.right;
        }

        private void Hold(Vector2 aim, float pull)
        {
            bow.SetPull(pull);
            Vector3 screenRight = drawing.right; // the drawing turns to face the camera
            float side = Vector3.Dot(new Vector3(aim.x, 0f, aim.y), screenRight) >= 0f ? 1f : -1f;
            bow.HoldAt(transform.position, new Vector3(screenRight.x, 0f, screenRight.z).normalized, side, heldOffset);
        }
    }
}
