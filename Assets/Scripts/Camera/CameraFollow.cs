using CodenameVelvet.Cameras;
using CodenameVelvet.Map;
using CodenameVelvet.Player;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Follows one player with a fixed rotation: the view is defined by pitch, distance and field of view, and the
/// camera only slides (it never re-aims while following, except to face a ladder being climbed or in a turned zone). Split screen uses one per player, assigned through
/// <see cref="SetTarget"/>. Inside a <see cref="CameraFramingZone"/> it pulls back and shifts its focus toward
/// the zone's focus, and <see cref="CameraShake"/> (when present) is added on top of the framing.
/// A cutscene can borrow the view with <see cref="SetCinematic"/>.
/// While its camera draws, it tells "Velvet/Lit" (the 3D map) where its player is and, when most of them is hidden, to
/// turn what hides them see-through.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    private const float ReferenceAspect = 16f / 9f;
    private const float SeeThroughSeconds = 0.2f;
    private const float SeeThroughMargin = 1.2f; // "Velvet/Lit" fully opens what is this far in front of the player
    private static readonly int SeeThroughId = Shader.PropertyToID("_VelvetSeeThrough");

    [SerializeField] private Transform target;

    [Header("Framing")]
    [Tooltip("Degrees the camera looks down. Sprites with ViewTilt lean back by the same angle to face it.")]
    [SerializeField, Range(10f, 89f)] private float pitch = ViewAngle.DefaultPitch;
    [Tooltip("Distance from the focus point to the camera.")]
    [SerializeField, Min(1f)] private float distance = 55f;
    [Tooltip("Vertical field of view on a 16:9 view. Other view shapes keep the same horizontal coverage, " +
             "so a half-height split-screen view shows the same width at the same scale.")]
    [SerializeField, Range(10f, 90f)] private float fieldOfView = 35f;
    [Tooltip("Height above the player's feet the camera centers on.")]
    [SerializeField] private float focusHeight = 1.5f;

    [Header("Smoothing")]
    [Tooltip("How fast it catches up with the player. Higher = tighter. Frame-rate independent.")]
    [SerializeField] private float smoothSpeed = 8f;
    [Tooltip("How fast framing zones pull the camera back and shift its focus. Lower = slower, more cinematic.")]
    [SerializeField, Min(0.1f)] private float zoneBlendSpeed = 2.5f;

    [Header("Climbing")]
    [Tooltip("Distance from the focus point while the player is on a ladder, seen from the front.")]
    [SerializeField, Min(1f)] private float climbDistance = 30f;
    [Tooltip("How fast it turns to face a ladder or a zone's yaw, and back.")]
    [SerializeField, Min(0.1f)] private float climbBlendSpeed = 4f;

    [Header("See-through map")]
    [Tooltip("Part of the player that must be hidden before what hides them turns see-through.")]
    [SerializeField, Range(0.3f, 1f)] private float hiddenPart = 0.7f;

    [Header("Optional bounds (keeps the focus over the map)")]
    [SerializeField] private bool useBounds = false;
    [SerializeField] private Vector2 minBounds;
    [SerializeField] private Vector2 maxBounds;

    private Camera cam;
    private CameraShake shake;
    private Vector3 focus;
    private float zoneDistance;
    private Vector3 zoneShift;
    private PlayerMovement movement;
    private float climbBlend;
    private float yaw;
    private PlayerHealth health;
    private int mapMask;
    private bool isHidden;
    private float seeThrough;
    private Vector3 cinematicPoint;
    private float cinematicBlend;

    /// <summary>Smoothed world point the camera is centered on.</summary>
    public Vector3 FocusPoint => focus;

    /// <summary>Sets who to follow. Call before Start.</summary>
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        movement = target.GetComponent<PlayerMovement>();
        health = target.GetComponent<PlayerHealth>();
    }

    /// <summary>Moves the view from the player (0) to a world point (1), eased at both ends and framed like the
    /// <see cref="CameraFramingZone"/> there. Stays until called again with 0; works while time is frozen.</summary>
    public void SetCinematic(Vector3 point, float blend)
    {
        cinematicPoint = point;
        cinematicBlend = Mathf.SmoothStep(0f, 1f, blend);
    }

    /// <summary>Jumps straight to the target instead of gliding there (e.g. after a teleport).</summary>
    public void SnapToTarget()
    {
        if (target == null) return;
        focus = DesiredFocus() + zoneShift;
        Apply();
    }

    private void Awake()
    {
        cam = GetComponent<Camera>();
        TryGetComponent(out shake);
        mapMask = LayerMask.GetMask("Ground");
    }

    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += OpenSeeThrough;
        RenderPipelineManager.endCameraRendering += CloseSeeThrough;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OpenSeeThrough;
        RenderPipelineManager.endCameraRendering -= CloseSeeThrough;
    }

    // Per camera: in split screen each view only opens the map for its own player.
    private void OpenSeeThrough(ScriptableRenderContext context, Camera rendering)
    {
        if (rendering != cam || target == null) return;
        Vector3 feet = target.position;
        Shader.SetGlobalVector(SeeThroughId, new Vector4(feet.x, feet.y, feet.z, seeThrough));
    }

    private void CloseSeeThrough(ScriptableRenderContext context, Camera rendering)
    {
        if (rendering == cam) Shader.SetGlobalVector(SeeThroughId, Vector4.zero);
    }

    private void Start()
    {
        if (target == null)
        {
            Debug.LogWarning($"{name}: CameraFollow has no target; call SetTarget before Start.", this);
            return;
        }

        // Start in place instead of flying in from wherever the camera was.
        focus = DesiredFocus();
        Apply();
    }

    // LateUpdate: the player's Rigidbody interpolates every frame, so its position is already current here.
    private void LateUpdate()
    {
        if (target == null) return;

        // Exponential smoothing: same catch-up per second at any frame rate (and frozen while paused).
        CameraFramingZone zone = CameraFramingZone.Find(target.position);
        float zoneT = 1f - Mathf.Exp(-zoneBlendSpeed * Time.deltaTime);
        zoneDistance = Mathf.Lerp(zoneDistance, zone != null ? zone.ExtraDistance : 0f, zoneT);
        zoneShift = Vector3.Lerp(zoneShift, zone != null ? zone.FocusShift(target.position) : Vector3.zero, zoneT);

        // On a ladder it looks along the climb direction, so the ladder is seen from the front and never hidden.
        bool onLadder = movement != null && movement.IsOnLadder;
        float climbT = 1f - Mathf.Exp(-climbBlendSpeed * Time.deltaTime);
        float targetYaw = onLadder ? Mathf.Atan2(movement.FacingDirection.x, movement.FacingDirection.y) * Mathf.Rad2Deg
            : zone != null ? zone.Yaw : 0f;
        climbBlend = Mathf.Lerp(climbBlend, onLadder ? 1f : 0f, climbT);
        yaw = Mathf.LerpAngle(yaw, targetYaw, climbT);
        if (movement != null) movement.ViewYaw = targetYaw;

        float followT = 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime);
        focus = Vector3.Lerp(focus, DesiredFocus() + zoneShift, followT);
        Apply();

        isHidden = IsTargetHidden();
        seeThrough = Mathf.MoveTowards(seeThrough, isHidden ? 1f : 0f, Time.unscaledDeltaTime / SeeThroughSeconds);
    }

    private bool IsTargetHidden()
    {
        // Nobody to reveal from a cutscene's view, or while they are down (not drawn).
        if (cinematicBlend > 0f || (health != null && !health.IsAlive)) return false;

        int hiddenPoints = 0;
        for (int i = 0; i < OcclusionRules.BodyPoints; i++)
            if (IsBlocked(OcclusionRules.BodyPoint(i, target.position, transform.right, transform.up))) hiddenPoints++;
        return OcclusionRules.IsCovered(hiddenPoints, isHidden, hiddenPart);
    }

    // Only the map (Ground) and boats: water and the boss arena's invisible walls hide nobody.
    private bool IsBlocked(Vector3 point)
    {
        Vector3 toPoint = point - transform.position;
        Ray ray = new Ray(transform.position, toPoint);
        float reach = toPoint.magnitude - SeeThroughMargin;
        return Physics.Raycast(ray, reach, mapMask, QueryTriggerInteraction.Ignore) || Boat.Blocks(ray, reach);
    }

    private Vector3 DesiredFocus()
    {
        Vector3 desired = target.position + Vector3.up * focusHeight;
        if (useBounds)
        {
            desired.x = Mathf.Clamp(desired.x, minBounds.x, maxBounds.x);
            desired.z = Mathf.Clamp(desired.z, minBounds.y, maxBounds.y);
        }
        return desired;
    }

    private void Apply()
    {
        ViewAngle.Pitch = pitch;

        Vector3 viewFocus = focus;
        float viewYaw = yaw;
        float viewDistance = Mathf.Lerp(distance + zoneDistance, climbDistance, climbBlend);
        if (cinematicBlend > 0f)
        {
            CameraFramingZone zone = CameraFramingZone.Find(cinematicPoint);
            viewFocus = Vector3.Lerp(focus, cinematicPoint, cinematicBlend);
            viewYaw = Mathf.LerpAngle(yaw, zone != null ? zone.Yaw : 0f, cinematicBlend);
            viewDistance = Mathf.Lerp(viewDistance, distance + (zone != null ? zone.ExtraDistance : 0f), cinematicBlend);
        }

        Quaternion rotation = Quaternion.Euler(pitch, viewYaw, 0f);
        Vector3 position = viewFocus - rotation * Vector3.forward * viewDistance;
        if (shake != null)
        {
            position += rotation * shake.Offset;
            rotation *= Quaternion.Euler(0f, 0f, shake.Roll);
        }

        transform.SetPositionAndRotation(position, rotation);
        cam.fieldOfView = Camera.HorizontalToVerticalFieldOfView(
            Camera.VerticalToHorizontalFieldOfView(fieldOfView, ReferenceAspect), cam.aspect);
    }
}
