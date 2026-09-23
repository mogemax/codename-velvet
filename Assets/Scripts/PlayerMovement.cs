using UnityEngine;

/// <summary>
/// Movimiento top-down/isométrico en 8 direcciones, estilo Cult of the Lamb.
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

    [Header("Referencias")]
    [SerializeField] private Animator animator;      // Animator del rig/sprite
    [SerializeField] private Transform spriteRoot;    // Hijo que se voltea (Scale.x) o rota

    [Header("Animator - nombres de parámetros")]
    [SerializeField] private string paramMoveX = "MoveX";
    [SerializeField] private string paramMoveY = "MoveY";
    [SerializeField] private string paramSpeed = "Speed";
    [SerializeField] private string paramIsMoving = "IsMoving";

    private Rigidbody rb;
    private Vector2 inputDir;
    private Vector2 currentVelocity;
    private Vector2 lastMoveDir = Vector2.down; // Para mantener animación Idle mirando última dirección

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true; // Evita que el Rigidbody rote el personaje por física

        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        ReadInput();
        UpdateAnimator();
        FlipSprite();
    }

    private void FixedUpdate()
    {
        MoveCharacter();
    }

    private void ReadInput()
    {
        float x = Input.GetAxisRaw("Horizontal"); // A/D o flechas
        float y = Input.GetAxisRaw("Vertical");   // W/S o flechas

        if (invertHorizontal) x *= -1f;
        if (invertVertical) y *= -1f;

        inputDir = new Vector2(x, y).normalized;

        if (inputDir.sqrMagnitude > 0.01f)
            lastMoveDir = inputDir;
    }

    private void MoveCharacter()
    {
        Vector2 targetVelocity = inputDir * moveSpeed;

        float accel = inputDir.sqrMagnitude > 0.01f ? acceleration : deceleration;
        currentVelocity = Vector2.MoveTowards(currentVelocity, targetVelocity, accel * Time.fixedDeltaTime);

        // Mapeo 2D -> plano 3D (X, Z). Si tu mundo usa X/Y en vez de X/Z, ajusta aquí.
        Vector3 worldVelocity = new Vector3(currentVelocity.x, 0f, currentVelocity.y);
        rb.linearVelocity = new Vector3(worldVelocity.x, rb.linearVelocity.y, worldVelocity.z);
    }

    private void UpdateAnimator()
    {
        if (animator == null) return;

        animator.SetFloat(paramMoveX, lastMoveDir.x);
        animator.SetFloat(paramMoveY, lastMoveDir.y);
        animator.SetFloat(paramSpeed, currentVelocity.magnitude);

        // true mientras se presiona una tecla de movimiento (input crudo, no velocidad),
        // así el cambio a Walk es inmediato al presionar y a Idle inmediato al soltar.
        bool isMoving = inputDir.sqrMagnitude > 0.01f;
        animator.SetBool(paramIsMoving, isMoving);
    }

    private void FlipSprite()
    {
        if (spriteRoot == null) return;

        if (Mathf.Abs(inputDir.x) > 0.01f)
        {
            Vector3 scale = spriteRoot.localScale;
            scale.x = Mathf.Abs(scale.x) * (inputDir.x < 0 ? 1f : -1f);
            spriteRoot.localScale = scale;
        }
    }
}
