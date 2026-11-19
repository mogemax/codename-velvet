using UnityEngine;

/// <summary>
/// Simple spring/pendulum for a secondary bone (cape, cloth, hair). Runs in LateUpdate, after the
/// Animator set the base pose: the bone chases its animated rotation with inertia, so it lags and
/// swings when the character moves.
/// </summary>
public class SpringBone : MonoBehaviour
{
    [Header("Spring")]
    [Tooltip("Higher = returns to the rest pose faster (less wobble).")]
    [SerializeField] private float stiffness = 8f;

    [Tooltip("Higher = oscillation dies out faster.")]
    [SerializeField] private float damping = 4f;

    [Header("Movement response")]
    [Tooltip("How much the character's speed pushes the bone.")]
    [SerializeField] private float movementInfluence = 15f;

    [Header("Limits")]
    [Tooltip("Max deviation from the rest pose, in degrees.")]
    [SerializeField] private float maxAngle = 25f;

    private Transform rootParent;
    private Vector3 lastRootPosition;
    private Vector3 rootVelocity;

    private float currentAngleOffset = 0f; // degrees around Z from the animated pose
    private float angularVelocity = 0f;
    private Quaternion lastAppliedOffset = Quaternion.identity;

    private void Start()
    {
        rootParent = transform.root;
        lastRootPosition = rootParent.position;
    }

    private void LateUpdate()
    {
        if (Time.deltaTime <= 0f) return; // paused (timeScale 0): the velocity would be 0/0 = NaN

        rootVelocity = (rootParent.position - lastRootPosition) / Time.deltaTime;
        lastRootPosition = rootParent.position;

        // 2D rig: only the sideways (local X) velocity drives the swing.
        Vector3 localVelocity = transform.parent != null
            ? transform.parent.InverseTransformDirection(rootVelocity)
            : rootVelocity;

        float targetForce = -localVelocity.x * movementInfluence;

        float springForce = (targetForce - currentAngleOffset) * stiffness;
        angularVelocity += springForce * Time.deltaTime;
        angularVelocity *= Mathf.Clamp01(1f - damping * Time.deltaTime);

        currentAngleOffset += angularVelocity * Time.deltaTime;
        currentAngleOffset = Mathf.Clamp(currentAngleOffset, -maxAngle, maxAngle);

        // Remove last frame's offset first; otherwise it would accumulate on top of the animated pose.
        Quaternion animatedBaseRotation = transform.localRotation * Quaternion.Inverse(lastAppliedOffset);

        Quaternion newOffset = Quaternion.Euler(0f, 0f, currentAngleOffset);
        transform.localRotation = animatedBaseRotation * newOffset;
        lastAppliedOffset = newOffset;
    }
}
