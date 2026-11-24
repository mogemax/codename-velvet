using System.Collections;
using CodenameVelvet.Map;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CodenameVelvet.Player
{
    /// <summary>
    /// Climbs the <see cref="Ladder"/> in reach when Interact is pressed and does not stop until the ledge;
    /// only dying interrupts it.
    /// </summary>
    [RequireComponent(typeof(PlayerMovement))]
    public class PlayerClimb : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float climbSpeed = 5f;

        private PlayerMovement movement;
        private PlayerHealth health;
        private Rigidbody body;
        private Ladder ladder;
        private Ladder climbed; // the one taken by this climb: the trigger may be left before it ends

        /// <summary>True when pressing Interact now would start a climb.</summary>
        public bool CanClimb => TryGetPath(out _);

        private void Awake()
        {
            movement = GetComponent<PlayerMovement>();
            health = GetComponent<PlayerHealth>();
            body = GetComponent<Rigidbody>();
        }

        private void OnEnable()
        {
            if (health != null) health.Died += StopClimb;
        }

        private void OnDisable()
        {
            if (health != null) health.Died -= StopClimb;
            StopClimb();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent(out Ladder entered)) ladder = entered;
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.TryGetComponent(out Ladder exited) && exited == ladder) ladder = null;
        }

        private void OnInteract(InputValue value)
        {
            if (value.isPressed && TryGetPath(out Vector3[] path)) StartCoroutine(Climb(path));
        }

        private bool TryGetPath(out Vector3[] path)
        {
            path = null;
            if (ladder == null || movement.IsClimbing || movement.IsFlying) return false;
            if (health != null && !health.IsAlive) return false;
            return ladder.TryGetPath(body.position, out path);
        }

        private IEnumerator Climb(Vector3[] path)
        {
            Vector3 toLedge = path[2] - path[1];
            climbed = ladder;
            climbed.IsOccupied = true;
            movement.BeginClimb(new Vector2(toLedge.x, toLedge.z).normalized);

            // shortcut: a dev-mode teleport mid-climb is dragged back to the path, cancel there if it ever matters.
            foreach (Vector3 point in path)
            {
                // The last stretch is walked on the ledge: the ladder is already behind.
                if (point == path[2]) movement.StepOffLadder();
                while ((body.position - point).sqrMagnitude > 1e-4f)
                {
                    body.MovePosition(Vector3.MoveTowards(body.position, point, climbSpeed * Time.fixedDeltaTime));
                    yield return new WaitForFixedUpdate();
                }
            }

            EndClimb();
        }

        private void EndClimb()
        {
            if (climbed != null) climbed.IsOccupied = false;
            movement.EndClimb();
        }

        private void StopClimb()
        {
            if (!movement.IsClimbing) return;
            StopAllCoroutines();
            EndClimb();
        }
    }
}
