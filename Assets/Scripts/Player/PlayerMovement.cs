using UnityEngine;

/// <summary>
/// Movimiento top-down/isométrico en 8 direcciones + Dash, estilo Cult of the Lamb.
/// Usa Rigidbody (no Kinematic) para colisiones correctas contra el escenario.
/// El sprite/rig debe estar en un hijo del objeto con este script.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movimiento")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float acceleration = 20f;
    [SerializeField] private float deceleration = 25f;
    [Tooltip("Actívalo si arriba/abajo se ven invertidos respecto a la cámara.")]
    [SerializeField] private bool invertVertical = false;
    [Tooltip("Actívalo si izquierda/derecha se ven invertidos respecto a la cámara.")]
    [SerializeField] private bool invertHorizontal = false;

    [Header("Dash")]
    [SerializeField] private KeyCode dashKey = KeyCode.Space;
    [Tooltip("Velocidad durante el dash.")]
    [SerializeField] private float dashSpeed = 16f;
    [Tooltip("Duración del dash en segundos.")]
    [SerializeField] private float dashDuration = 0.2f;
    [Tooltip("Tiempo de espera entre dashes (se cuenta desde que termina el dash).")]
    [SerializeField] private float dashCooldown = 0.5f;
    [Tooltip("Si no hay input, hace dash hacia la última dirección en la que miraba.")]
    [SerializeField] private bool dashWithoutInput = true;

    [Header("Referencias")]
    [SerializeField] private Animator animator;      // Animator del rig/sprite
    [SerializeField] private Transform spriteRoot;    // Hijo que se voltea (Scale.x) o rota

    [Header("Animator - nombres de parámetros")]
    [SerializeField] private string paramMoveX = "MoveX";
    [SerializeField] private string paramMoveY = "MoveY";
    [SerializeField] private string paramSpeed = "Speed";
    [SerializeField] private string paramIsMoving = "IsMoving";
    [SerializeField] private string paramDashTrigger = "Dash";     // Trigger: dispara la animación
    [SerializeField] private string paramIsDashing = "IsDashing";  // Bool: true mientras dura el dash

    /// <summary>Útil para otros scripts (ej. invulnerabilidad durante el dash).</summary>
    public bool IsDashing { get; private set; }

    private Rigidbody rb;
    private Vector2 inputDir;
    private Vector2 currentVelocity;
    private Vector2 lastMoveDir = Vector2.down; // Para mantener animación Idle mirando última dirección

    // Estado del dash
    private Vector2 dashDir;
    private float dashTimeLeft;
    private float dashCooldownLeft;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true; // Evita que el Rigidbody rote el personaje por física
        // Evita atravesar paredes a alta velocidad durante el dash
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        ReadInput();
        HandleDash();
        UpdateAnimator();
        FlipSprite();
    }

    private void FixedUpdate()
    {
        if (IsDashing) DashMove();
        else MoveCharacter();
    }

    private void ReadInput()
    {
        float x = Input.GetAxisRaw("Horizontal"); // A/D o flechas
        float y = Input.GetAxisRaw("Vertical");   // W/S o flechas

        if (invertHorizontal) x *= -1f;
        if (invertVertical) y *= -1f;

        inputDir = new Vector2(x, y).normalized;

        // Durante el dash la dirección queda bloqueada
        if (!IsDashing && inputDir.sqrMagnitude > 0.01f)
            lastMoveDir = inputDir;
    }

    // ───────────────────────── DASH ─────────────────────────

    private void HandleDash()
    {
        if (IsDashing)
        {
            dashTimeLeft -= Time.deltaTime;
            if (dashTimeLeft <= 0f) EndDash();
            return;
        }

        if (dashCooldownLeft > 0f)
            dashCooldownLeft -= Time.deltaTime;

        if (Input.GetKeyDown(dashKey) && dashCooldownLeft <= 0f)
            TryStartDash();
    }

    private void TryStartDash()
    {
        bool hasInput = inputDir.sqrMagnitude > 0.01f;
        if (!hasInput && !dashWithoutInput) return;

        dashDir = hasInput ? inputDir : lastMoveDir;
        lastMoveDir = dashDir; // El animator usa esta dirección para el blend tree del dash

        IsDashing = true;
        dashTimeLeft = dashDuration;

        if (animator != null)
        {
            animator.SetBool(paramIsDashing, true);
            animator.SetTrigger(paramDashTrigger);
        }
    }

    private void EndDash()
    {
        IsDashing = false;
        dashCooldownLeft = dashCooldown;

        // Transición suave: al terminar conservamos velocidad normal en esa dirección
        // en lugar de frenar en seco.
        currentVelocity = dashDir * moveSpeed;

        if (animator != null)
            animator.SetBool(paramIsDashing, false);
    }

    private void DashMove()
    {
        // Mismo mapeo 2D -> 3D (X, Z) que el movimiento normal. Se conserva la Y (gravedad).
        Vector3 v = new Vector3(dashDir.x * dashSpeed, rb.linearVelocity.y, dashDir.y * dashSpeed);
        rb.linearVelocity = v;
    }

    // ─────────────────────── MOVIMIENTO ───────────────────────

    private void MoveCharacter()
    {
        Vector2 targetVelocity = inputDir * moveSpeed;

        float accel = inputDir.sqrMagnitude > 0.01f ? acceleration : deceleration;
        currentVelocity = Vector2.MoveTowards(currentVelocity, targetVelocity, accel * Time.fixedDeltaTime);

        // Mapeo 2D -> plano 3D (X, Z). Si tu mundo usa X/Y en vez de X/Z, ajusta aquí.
        Vector3 worldVelocity = new Vector3(currentVelocity.x, 0f, currentVelocity.y);
        rb.linearVelocity = new Vector3(worldVelocity.x, rb.linearVelocity.y, worldVelocity.z);
    }

    // ─────────────────────── ANIMACIÓN ───────────────────────

    private void UpdateAnimator()
    {
        if (animator == null) return;

        animator.SetFloat(paramMoveX, lastMoveDir.x);
        animator.SetFloat(paramMoveY, lastMoveDir.y);
        animator.SetFloat(paramSpeed, IsDashing ? dashSpeed : currentVelocity.magnitude);

        // true mientras se presiona una tecla de movimiento (input crudo, no velocidad),
        // así el cambio a Walk es inmediato al presionar y a Idle inmediato al soltar.
        // Durante el dash se pone en false para que no compita con la animación de Dash.
        bool isMoving = !IsDashing && inputDir.sqrMagnitude > 0.01f;
        animator.SetBool(paramIsMoving, isMoving);
    }

    private void FlipSprite()
    {
        if (spriteRoot == null) return;

        // Durante el dash el flip sigue la dirección del dash, no el input actual
        float xDir = IsDashing ? dashDir.x : inputDir.x;

        if (Mathf.Abs(xDir) > 0.01f)
        {
            Vector3 scale = spriteRoot.localScale;
            scale.x = Mathf.Abs(scale.x) * (xDir < 0 ? 1f : -1f);
            spriteRoot.localScale = scale;
        }
    }
}