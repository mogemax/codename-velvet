using UnityEngine;

/// <summary>
/// Máquina de estados simple para un enemigo: deambula al azar por el mapa,
/// se detiene a descansar de vez en cuando, y si el jugador entra en su
/// rango de detección, lo persigue hasta que se aleja lo suficiente.
/// Usa el mismo sistema de Rigidbody + Animator que PlayerMovement.cs,
/// así que reutiliza el mismo Animator Controller sin cambios.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class EnemyChase : MonoBehaviour
{
    private enum State { Wandering, Pausing, Chasing }

    [Header("Objetivo")]
    [Tooltip("Arrastra al Player, o se busca automáticamente por tag 'Player' si se deja vacío.")]
    [SerializeField] private Transform target;

    [Header("Rangos de detección")]
    [Tooltip("Distancia a la que el enemigo empieza a perseguir al jugador.")]
    [SerializeField] private float detectionRange = 5f;

    [Tooltip("Distancia a la que el enemigo DEJA de perseguir (debe ser mayor a Detection Range, para evitar que entre/salga del rango de golpe y tiemble entre estados).")]
    [SerializeField] private float loseInterestRange = 7f;

    [Tooltip("Distancia mínima a la que se detiene al perseguir (para no encimarse con el jugador). Déjalo en 0 si quieres que lo alcance por completo.")]
    [SerializeField] private float stopDistance = 0.8f;

    [Header("Deambular (Wander)")]
    [Tooltip("Radio máximo, desde el punto de inicio del enemigo, en el que elige puntos aleatorios para caminar.")]
    [SerializeField] private float wanderRadius = 4f;

    [Tooltip("Qué tan cerca debe estar del punto elegido para considerar que 'llegó' y pasar a pausa.")]
    [SerializeField] private float wanderArriveThreshold = 0.3f;

    [Tooltip("Rango de tiempo (mín-máx, segundos) que se queda parado entre un recorrido y otro.")]
    [SerializeField] private Vector2 pauseDurationRange = new Vector2(1.5f, 4f);

    [Tooltip("Velocidad al deambular (normalmente más lenta que al perseguir).")]
    [SerializeField] private float wanderSpeed = 1.8f;

    [Header("Movimiento")]
    [SerializeField] private float moveSpeed = 3.5f;
    [SerializeField] private float acceleration = 15f;
    [SerializeField] private float deceleration = 20f;

    [Header("Referencias")]
    [SerializeField] private Animator animator;
    [SerializeField] private Transform spriteRoot;

    [Header("Animator - nombres de parámetros")]
    [SerializeField] private string paramMoveX = "MoveX";
    [SerializeField] private string paramMoveY = "MoveY";
    [SerializeField] private string paramSpeed = "Speed";
    [SerializeField] private string paramIsMoving = "IsMoving";

    [Header("Debug")]
    [Tooltip("Dibuja los círculos de detección en la vista Scene (no se ve en Game).")]
    [SerializeField] private bool showGizmos = true;

    private Rigidbody rb;
    private Vector2 currentVelocity;
    private Vector2 lastMoveDir = Vector2.down;

    private State currentState = State.Wandering;
    private Vector3 spawnPosition;       // Centro del área de deambular
    private Vector3 wanderDestination;   // Punto actual al que camina mientras deambula
    private float pauseTimer = 0f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;

        if (animator == null) animator = GetComponentInChildren<Animator>();

        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) target = player.transform;
        }
    }

    private void Start()
    {
        spawnPosition = transform.position;
        PickNewWanderDestination();
    }

    private void Update()
    {
        UpdateStateMachine();
        UpdateAnimator();
        FlipSprite();
    }

    private void FixedUpdate()
    {
        MoveCharacter();
    }

    private void UpdateStateMachine()
    {
        bool playerInRange = target != null &&
            Vector3.Distance(transform.position, target.position) <= detectionRange;
        bool playerOutOfRange = target == null ||
            Vector3.Distance(transform.position, target.position) > loseInterestRange;

        switch (currentState)
        {
            case State.Chasing:
                // Histéresis: solo deja de perseguir si se aleja más allá
                // del rango MAYOR, no del mismo rango de detección.
                if (playerOutOfRange)
                {
                    currentState = State.Pausing;
                    pauseTimer = Random.Range(pauseDurationRange.x, pauseDurationRange.y);
                }
                break;

            case State.Wandering:
                if (playerInRange)
                {
                    currentState = State.Chasing;
                    break;
                }

                float distToDestination = Vector3.Distance(transform.position, wanderDestination);
                if (distToDestination <= wanderArriveThreshold)
                {
                    currentState = State.Pausing;
                    pauseTimer = Random.Range(pauseDurationRange.x, pauseDurationRange.y);
                }
                break;

            case State.Pausing:
                if (playerInRange)
                {
                    currentState = State.Chasing;
                    break;
                }

                pauseTimer -= Time.deltaTime;
                if (pauseTimer <= 0f)
                {
                    PickNewWanderDestination();
                    currentState = State.Wandering;
                }
                break;
        }
    }

    private void PickNewWanderDestination()
    {
        // Punto aleatorio dentro de un círculo alrededor del punto de spawn
        // (no alrededor de la posición actual, para que no se vaya alejando
        // sin control con cada ciclo de deambular).
        Vector2 randomCircle = Random.insideUnitCircle * wanderRadius;
        wanderDestination = spawnPosition + new Vector3(randomCircle.x, 0f, randomCircle.y);
    }

    private void MoveCharacter()
    {
        Vector2 inputDir = Vector2.zero;
        float targetSpeed = moveSpeed;

        if (currentState == State.Chasing && target != null)
        {
            float distance = Vector3.Distance(transform.position, target.position);

            if (distance > stopDistance)
            {
                Vector3 toTarget = target.position - transform.position;
                // Mismo mapeo que PlayerMovement: X del mundo -> X, Z del mundo -> Y del input 2D
                inputDir = new Vector2(toTarget.x, toTarget.z).normalized;
            }

            targetSpeed = moveSpeed;
        }
        else if (currentState == State.Wandering)
        {
            Vector3 toDestination = wanderDestination - transform.position;
            inputDir = new Vector2(toDestination.x, toDestination.z).normalized;
            targetSpeed = wanderSpeed;
        }
        // En State.Pausing, inputDir se queda en (0,0) → el enemigo frena y se detiene

        if (inputDir.sqrMagnitude > 0.01f)
            lastMoveDir = inputDir;

        Vector2 targetVelocity = inputDir * targetSpeed;
        float accel = inputDir.sqrMagnitude > 0.01f ? acceleration : deceleration;
        currentVelocity = Vector2.MoveTowards(currentVelocity, targetVelocity, accel * Time.fixedDeltaTime);

        Vector3 worldVelocity = new Vector3(currentVelocity.x, 0f, currentVelocity.y);
        rb.linearVelocity = new Vector3(worldVelocity.x, rb.linearVelocity.y, worldVelocity.z);
    }

    private void UpdateAnimator()
    {
        if (animator == null) return;

        animator.SetFloat(paramMoveX, lastMoveDir.x);
        animator.SetFloat(paramMoveY, lastMoveDir.y);
        animator.SetFloat(paramSpeed, currentVelocity.magnitude);

        bool isMoving = currentVelocity.sqrMagnitude > 0.01f;
        animator.SetBool(paramIsMoving, isMoving);
    }

    private void FlipSprite()
    {
        if (spriteRoot == null) return;

        float dirX = currentVelocity.sqrMagnitude > 0.01f ? currentVelocity.x : 0f;

        if (Mathf.Abs(dirX) > 0.01f)
        {
            Vector3 scale = spriteRoot.localScale;
            scale.x = Mathf.Abs(scale.x) * (dirX < 0 ? 1f : -1f);
            spriteRoot.localScale = scale;
        }
    }

    // Dibuja los rangos de detección y el área de deambular en la vista Scene
    private void OnDrawGizmosSelected()
    {
        if (!showGizmos) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, loseInterestRange);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, stopDistance);

        // Área de deambular: en Play Mode se centra en el punto de spawn real;
        // en modo edición (sin Play) se aproxima con la posición actual.
        Gizmos.color = Color.green;
        Vector3 center = Application.isPlaying ? spawnPosition : transform.position;
        Gizmos.DrawWireSphere(center, wanderRadius);
    }
}
