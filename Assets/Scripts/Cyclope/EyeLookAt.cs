using CodenameVelvet.Enemies;
using UnityEngine;

/// <summary>
/// Slides the eye bone inside its socket toward the nearest player, like a pupil. Runs in LateUpdate
/// to override whatever position the Animator set this frame.
/// </summary>
public class EyeLookAt : MonoBehaviour
{
    private const float RetargetInterval = 0.25f;
    private const float UnlimitedSearchRange = 100f;

    [Header("Target")]
    [Tooltip("Only used when the cyclops has no EnemyTargetFinder; otherwise the nearest living player is followed.")]
    [SerializeField] private Transform target;

    [Header("Eye movement")]
    [Tooltip("Max offset from the rest position, in local units of the bone's parent.")]
    [SerializeField] private float maxOffsetX = 0.05f;
    [SerializeField] private float maxOffsetY = 0.05f;

    [Header("Smoothing")]
    [SerializeField] private float moveSpeed = 10f;

    [Header("Detection range (optional)")]
    [Tooltip("When enabled, the eye only follows players inside this range and returns to rest otherwise.")]
    [SerializeField] private bool useDetectionRange = false;
    [SerializeField] private float detectionRange = 8f;

    private Vector3 restLocalPosition;
    private EnemyTargetFinder targetFinder;
    private float nextRetargetTime;

    private void Awake()
    {
        restLocalPosition = transform.localPosition;
        targetFinder = GetComponentInParent<EnemyTargetFinder>();
    }

    private void LateUpdate()
    {
        RetargetNearestPlayer();
        if (target == null)
        {
            transform.localPosition = Vector3.Lerp(transform.localPosition, restLocalPosition, moveSpeed * Time.deltaTime);
            return;
        }

        bool withinRange = !useDetectionRange ||
            Vector3.Distance(transform.position, target.position) <= detectionRange;

        Vector3 desiredLocalPosition = restLocalPosition;
        if (withinRange)
        {
            // In the parent's local space, so the offset follows the sprite's orientation, not the world's.
            Vector3 worldDir = target.position - transform.position;
            Vector3 localDir = transform.parent != null
                ? transform.parent.InverseTransformDirection(worldDir)
                : worldDir;
            localDir.Normalize();

            desiredLocalPosition += new Vector3(localDir.x * maxOffsetX, localDir.y * maxOffsetY, 0f);
        }

        transform.localPosition = Vector3.Lerp(transform.localPosition, desiredLocalPosition, moveSpeed * Time.deltaTime);
    }

    private void RetargetNearestPlayer()
    {
        if (targetFinder == null || Time.time < nextRetargetTime) return;
        nextRetargetTime = Time.time + RetargetInterval;

        float range = useDetectionRange ? detectionRange : UnlimitedSearchRange;
        target = targetFinder.TryFindNearest(transform.position, range, out _, out Transform nearest, out _)
            ? nearest
            : null;
    }
}
