using UnityEngine;

/// <summary>
/// Follows one player at a fixed angle and distance, with smoothing. Split screen uses one per player,
/// assigned through <see cref="SetTarget"/>.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;

    [Header("Camera offset")]
    [Tooltip("Position relative to the player: Y up, negative Z to sit behind and above.")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 10f, -8f);

    [Header("Smoothing")]
    [Tooltip("How fast it catches up with the player. Higher = tighter. Frame-rate independent.")]
    [SerializeField] private float smoothSpeed = 8f;

    [Header("Focus")]
    [Tooltip("Keeps the camera rotated to look exactly at the target.")]
    [SerializeField] private bool alwaysLookAtTarget = true;

    [Tooltip("Shifts the point the camera looks at, without moving the camera. Positive Y shows the character lower on screen.")]
    [SerializeField] private Vector3 lookAtOffset = new Vector3(0f, 2f, 0f);

    [Header("Optional bounds (keeps the camera over the map)")]
    [SerializeField] private bool useBounds = false;
    [SerializeField] private Vector2 minBounds;
    [SerializeField] private Vector2 maxBounds;

    /// <summary>Sets who to follow. Call before Start.</summary>
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    private void Start()
    {
        if (target == null)
        {
            Debug.LogWarning($"{name}: CameraFollow has no target; call SetTarget before Start.", this);
            return;
        }

        // Start in place instead of flying in from wherever the camera was.
        transform.position = target.position + offset;
        transform.LookAt(target.position + lookAtOffset);
    }

    // LateUpdate: the player's Rigidbody interpolates every frame, so its position is already current here.
    // Following in FixedUpdate (50 Hz) stutters on 60+ Hz screens.
    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 desiredPosition = target.position + offset;

        if (useBounds)
        {
            desiredPosition.x = Mathf.Clamp(desiredPosition.x, minBounds.x, maxBounds.x);
            desiredPosition.z = Mathf.Clamp(desiredPosition.z, minBounds.y, maxBounds.y);
        }

        // Exponential smoothing: same catch-up per second at any frame rate.
        float t = 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, desiredPosition, t);

        if (alwaysLookAtTarget)
            transform.LookAt(target.position + lookAtOffset);
    }
}
