using CodenameVelvet.Audio;
using CodenameVelvet.Map;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Top-down 8-direction movement plus Dash on a non-kinematic Rigidbody, so collisions with the map work.
/// Input comes from this object's PlayerInput (Send Messages), so each player reads only their own device.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PlayerInput))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float acceleration = 20f;
    [SerializeField] private float deceleration = 25f;
    [Tooltip("Acceleration when changing direction. Higher = sharper turns, no drifting.")]
    [SerializeField] private float turnAcceleration = 90f;
    [Tooltip("How much the direction must change to use Turn Acceleration (1 = any change, 0 = only turns over 90°).")]
    [Range(-1f, 1f)]
    [SerializeField] private float turnThreshold = 0.7f;
    [Tooltip("Enable when up/down look inverted relative to the camera.")]
    [SerializeField] private bool invertVertical = false;
    [Tooltip("Enable when left/right look inverted relative to the camera.")]
    [SerializeField] private bool invertHorizontal = false;

    [Header("Dash")]
    [Tooltip("Speed during the dash.")]
    [SerializeField] private float dashSpeed = 16f;
    [Tooltip("Dash duration in seconds.")]
    [SerializeField] private float dashDuration = 0.2f;
    [Tooltip("Wait between dashes, counted from the end of the dash.")]
    [SerializeField] private float dashCooldown = 0.5f;
    [Tooltip("Without input, dash toward the last facing direction.")]
    [SerializeField] private bool dashWithoutInput = true;

    [Header("Ground")]
    [Tooltip("Layers that count as ground. Empty = 'Ground' layer.")]
    [SerializeField] private LayerMask groundMask;
    [Tooltip("Max walkable slope in degrees. Steeper counts as a wall.")]
    [SerializeField, Range(0f, 89f)] private float maxSlopeAngle = 50f;
    [Tooltip("How far below the feet it stays snapped to the ground (walks down steps without floating).")]
    [SerializeField, Min(0f)] private float groundSnapDistance = 0.4f;
    [Tooltip("Gravity multiplier while falling, so falls do not feel floaty.")]
    [SerializeField, Min(1f)] private float fallGravityMultiplier = 2.5f;

    [Header("References")]
    [SerializeField] private Animator animator;
    [Tooltip("Child flipped with Scale.x to face left or right.")]
    [SerializeField] private Transform spriteRoot;
    [Tooltip("Enable when the original art faces right; disable when it faces left.")]
    [SerializeField] private bool artFacesRight = true;
    [Tooltip("Looping footsteps, played while walking.")]
    [SerializeField] private AudioSource footsteps;

    [Header("Animator parameter names")]
    [SerializeField] private string paramIsMoving = "IsMoving";
    [SerializeField] private string paramDashTrigger = "Dash";     // trigger: starts the animation
    [SerializeField] private string paramIsDashing = "IsDashing";  // bool: true for the whole dash
    [SerializeField] private string paramIsClimbing = "IsClimbing";

    public bool IsDashing { get; private set; }

    /// <summary>While true something else (PlayerClimb) moves the kinematic body.</summary>
    public bool IsClimbing { get; private set; }

    /// <summary>Yaw of this player's camera, written by CameraFollow: controls and sprite flip are relative to it.</summary>
    public float ViewYaw { get; set; }

    /// <summary>The part of a climb before reaching the top; false while stepping onto the ledge.</summary>
    public bool IsOnLadder { get; private set; }

    /// <summary>Multiplies walking and dash speed (1 = normal).</summary>
    public float SpeedMultiplier { get; set; } = 1f;

    /// <summary>Last movement direction, i.e. where the player faces.</summary>
    public Vector2 FacingDirection => lastMoveDir;

    /// <summary>
    /// Free flight (dev mode): no gravity, ground snapping, dash or water edge; the vertical speed comes from
    /// <see cref="FlightVerticalSpeed"/>.
    /// </summary>
    public bool IsFlying
    {
        get => isFlying;
        set
        {
            isFlying = value;
            rb.useGravity = !value && usesGravity;
            if (value) CancelDash();
        }
    }

    /// <summary>Vertical speed while flying (positive = up).</summary>
    public float FlightVerticalSpeed { get; set; }

    private const string MoveActionName = "Move";
    private const float GroundProbeSkin = 0.05f; // the probe sphere starts slightly above the feet

    private Rigidbody rb;
    private Collider body;
    private bool isGrounded;
    private Vector3 groundNormal = Vector3.up;
    private float groundGap; // distance between the feet and the detected ground
    private PlayerInput playerInput;
    private Vector2 rawMoveInput;
    private Vector2 inputDir;
    private float controlYaw;
    private Vector2 currentVelocity;
    private Vector2 lastMoveDir = Vector2.down;
    private bool controlEnabled = true;
    private bool isFlying;
    private bool usesGravity;

    private Vector2 dashDir;
    private float dashTimeLeft;
    private float dashCooldownLeft;
    private bool dashRequested;     // consumed by the next Update, like GetKeyDown

    private int isMovingHash;
    private int dashTriggerHash;
    private int isDashingHash;
    private int isClimbingHash;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        usesGravity = rb.useGravity;
        // The dash is fast enough to tunnel through walls with discrete detection.
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        // Without interpolation the body moves at 50 Hz and stutters on 60+ Hz screens.
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        body = GetComponent<Collider>();
        if (groundMask.value == 0) groundMask = LayerMask.GetMask("Ground");

        playerInput = GetComponent<PlayerInput>();

        if (animator == null) animator = GetComponentInChildren<Animator>();

        isMovingHash = Animator.StringToHash(paramIsMoving);
        dashTriggerHash = Animator.StringToHash(paramDashTrigger);
        isDashingHash = Animator.StringToHash(paramIsDashing);
        isClimbingHash = Animator.StringToHash(paramIsClimbing);
    }

    private void Update()
    {
        ReadInput();
        HandleDash();
        UpdateAnimator();
        footsteps.SetLoopPlaying(isGrounded && !isFlying && !IsDashing && !IsClimbing && inputDir.sqrMagnitude > 0.01f);
        FlipSprite();
    }

    private void FixedUpdate()
    {
        if (IsClimbing) return;

        ProbeGround();

        if (IsDashing)
        {
            DashMove();
            // Dash time runs on the physics step, like its velocity, so the distance does not depend on FPS.
            dashTimeLeft -= Time.fixedDeltaTime;
            if (dashTimeLeft <= 0f) EndDash();
        }
        else
        {
            MoveCharacter();
        }
    }

    private void OnMove(InputValue value)
    {
        if (!controlEnabled) return;
        rawMoveInput = value.Get<Vector2>();
    }

    private void OnDash(InputValue value)
    {
        if (!controlEnabled || !value.isPressed) return;
        dashRequested = true;
    }

    /// <summary>
    /// Enables/disables player control (e.g. while dead). Disabling drops pending input; enabling reads
    /// the current Move value so a direction already held keeps working.
    /// </summary>
    public void SetControlEnabled(bool isEnabled)
    {
        controlEnabled = isEnabled;
        dashRequested = false;

        rawMoveInput = isEnabled ? ReadCurrentMoveValue() : Vector2.zero;
        if (!isEnabled) inputDir = Vector2.zero;
    }

    private Vector2 ReadCurrentMoveValue()
    {
        InputAction move = playerInput != null && playerInput.actions != null
            ? playerInput.actions.FindAction(MoveActionName)
            : null;
        return move != null ? move.ReadValue<Vector2>() : Vector2.zero;
    }

    private void ReadInput()
    {
        float x = rawMoveInput.x;
        float y = rawMoveInput.y;

        if (invertHorizontal) x *= -1f;
        if (invertVertical) y *= -1f;

        // Follows a camera turn only once the stick is released, so a held direction never swerves mid-walk.
        if (rawMoveInput.sqrMagnitude < 0.01f) controlYaw = ViewYaw;
        inputDir = Turn(new Vector2(x, y).normalized, controlYaw);

        // The direction is locked during a dash.
        if (!IsDashing && inputDir.sqrMagnitude > 0.01f)
            lastMoveDir = inputDir;
    }

    /// <summary>Turns a planar direction (world X/Z) by a yaw.</summary>
    private static Vector2 Turn(Vector2 direction, float yaw)
    {
        Vector3 turned = Quaternion.Euler(0f, yaw, 0f) * new Vector3(direction.x, 0f, direction.y);
        return new Vector2(turned.x, turned.z);
    }

    private void HandleDash()
    {
        // Like GetKeyDown: a press only counts on its frame; during a dash or cooldown it is dropped, not queued.
        bool requested = dashRequested;
        dashRequested = false;

        if (IsDashing) return; // the dash ends in FixedUpdate

        if (dashCooldownLeft > 0f)
            dashCooldownLeft -= Time.deltaTime;

        if (requested && !isFlying && dashCooldownLeft <= 0f)
            TryStartDash();
    }

    private void TryStartDash()
    {
        bool hasInput = inputDir.sqrMagnitude > 0.01f;
        if (!hasInput && !dashWithoutInput) return;

        dashDir = hasInput ? inputDir : lastMoveDir;
        lastMoveDir = dashDir;

        IsDashing = true;
        dashTimeLeft = dashDuration;

        if (animator != null)
        {
            animator.SetBool(isDashingHash, true);
            animator.SetTrigger(dashTriggerHash);
        }
    }

    /// <summary>
    /// Cuts a dash short without exit velocity or cooldown (e.g. the player dies mid-dash).
    /// </summary>
    public void CancelDash()
    {
        if (!IsDashing) return;

        IsDashing = false;
        dashTimeLeft = 0f;
        currentVelocity = Vector2.zero;

        if (animator != null)
            animator.SetBool(isDashingHash, false);
    }

    /// <summary>Takes control away and frees the body from physics until <see cref="EndClimb"/>.</summary>
    public void BeginClimb(Vector2 direction)
    {
        CancelDash();
        SetControlEnabled(false);
        currentVelocity = Vector2.zero;
        lastMoveDir = direction;
        SetClimbing(true);
    }

    /// <summary>The top was reached: what is left of the climb is walking onto the ledge.</summary>
    public void StepOffLadder() => SetOnLadder(false);

    public void EndClimb()
    {
        SetClimbing(false);
        SetControlEnabled(true);
    }

    private void SetClimbing(bool climbing)
    {
        IsClimbing = climbing;
        rb.isKinematic = climbing;
        SetOnLadder(climbing);
    }

    private void SetOnLadder(bool onLadder)
    {
        IsOnLadder = onLadder;
        if (animator != null)
            animator.SetBool(isClimbingHash, onLadder);
    }

    private void EndDash()
    {
        IsDashing = false;
        dashCooldownLeft = dashCooldown;

        // Leave the dash at walking speed in the same direction instead of stopping dead.
        currentVelocity = dashDir * (moveSpeed * SpeedMultiplier);

        if (animator != null)
            animator.SetBool(isDashingHash, false);
    }

    private void DashMove()
    {
        ApplyPlanarVelocity(dashDir * (dashSpeed * SpeedMultiplier));
    }

    private void MoveCharacter()
    {
        Vector2 targetVelocity = inputDir * (moveSpeed * SpeedMultiplier);

        bool hasInput = inputDir.sqrMagnitude > 0.01f;
        float accel = deceleration;

        if (hasInput)
        {
            accel = acceleration;

            // Turning away from the current velocity uses the turn acceleration, so it does not drift.
            if (currentVelocity.sqrMagnitude > 0.01f)
            {
                float alignment = Vector2.Dot(currentVelocity.normalized, inputDir);
                if (alignment < turnThreshold)
                    accel = turnAcceleration;
            }
        }

        currentVelocity = Vector2.MoveTowards(currentVelocity, targetVelocity, accel * Time.fixedDeltaTime);

        ApplyPlanarVelocity(currentVelocity);
    }

    /// <summary>
    /// Sphere-casts down from the feet. Only a walkable surface (slope ≤ maxSlopeAngle) within
    /// groundSnapDistance counts as ground.
    /// </summary>
    private void ProbeGround()
    {
        isGrounded = false;
        groundNormal = Vector3.up;
        if (body == null) return;

        Bounds bounds = body.bounds;
        float radius = Mathf.Min(bounds.extents.x, bounds.extents.z) * 0.9f;
        Vector3 origin = new Vector3(bounds.center.x, bounds.min.y + radius + GroundProbeSkin, bounds.center.z);

        if (!Physics.SphereCast(origin, radius, Vector3.down, out RaycastHit hit,
                GroundProbeSkin + groundSnapDistance, groundMask, QueryTriggerInteraction.Ignore))
            return;

        if (Vector3.Angle(hit.normal, Vector3.up) > maxSlopeAngle) return;

        isGrounded = true;
        groundNormal = hit.normal;
        groundGap = Mathf.Max(0f, hit.distance - GroundProbeSkin);
    }

    /// <summary>
    /// Applies the XZ velocity (2D input → world X/Z) and decides Y here instead of leaving it to physics:
    /// <list type="bullet">
    /// <item>Grounded: follows the slope and stays snapped to the ground (no floating down steps).</item>
    /// <item>Airborne: can only fall. There is no jump, so any upward velocity comes from pushing against
    /// an edge and is discarded.</item>
    /// </list>
    /// </summary>
    private void ApplyPlanarVelocity(Vector2 planar)
    {
        Vector3 velocity = new Vector3(planar.x, 0f, planar.y);

        if (isFlying)
        {
            velocity.y = FlightVerticalSpeed;
            rb.linearVelocity = velocity;
            return;
        }

        velocity = WaterEdge.Clamp(rb.position, velocity, EdgeRadius(), Time.fixedDeltaTime);

        if (isGrounded)
        {
            velocity = Vector3.ProjectOnPlane(velocity, groundNormal);
            velocity.y -= groundGap / Time.fixedDeltaTime;
        }
        else
        {
            float fallSpeed = Mathf.Min(rb.linearVelocity.y, 0f);
            fallSpeed += Physics.gravity.y * (fallGravityMultiplier - 1f) * Time.fixedDeltaTime;
            velocity.y = fallSpeed;
        }

        rb.linearVelocity = velocity;
    }

    private float EdgeRadius() => body != null ? Mathf.Min(body.bounds.extents.x, body.bounds.extents.z) : 0.5f;

    private void UpdateAnimator()
    {
        if (animator == null) return;

        // Raw input, not velocity: Walk/Idle switch the moment the stick moves or is released.
        // False during a dash so it does not compete with the Dash animation.
        bool isMoving = !IsDashing && (IsClimbing ? !IsOnLadder : inputDir.sqrMagnitude > 0.01f);
        animator.SetBool(isMovingHash, isMoving);
    }

    private void FlipSprite()
    {
        if (spriteRoot == null) return;

        // During a dash or climb the flip follows that direction, not the current input.
        Vector2 direction = IsDashing ? dashDir : IsClimbing ? lastMoveDir : inputDir;
        float xDir = Turn(direction, -ViewYaw).x; // left or right as seen on screen

        if (Mathf.Abs(xDir) > 0.01f)
        {
            Vector3 scale = spriteRoot.localScale;
            bool movingRight = xDir > 0f;
            bool keepOriginal = movingRight == artFacesRight;
            scale.x = Mathf.Abs(scale.x) * (keepOriginal ? 1f : -1f);
            spriteRoot.localScale = scale;
        }
    }
}