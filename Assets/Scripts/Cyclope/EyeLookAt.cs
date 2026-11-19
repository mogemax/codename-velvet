using CodenameVelvet.Enemies;
using UnityEngine;

/// <summary>
/// Hace que el hueso del ojo se desplace (como la pupila deslizándose dentro
/// de la cuenca) hacia la dirección del jugador. Colócalo en el GameObject
/// del hueso del ojo (ej. "EYE"). Se ejecuta en LateUpdate para sobreescribir
/// cualquier posición que el Animator le haya aplicado ese mismo frame.
/// </summary>
public class EyeLookAt : MonoBehaviour
{
    [Header("Referencia")]
    [SerializeField] private Transform target; // Arrastra al Player, o se busca por tag

    [Header("Rango de movimiento del ojo")]
    [Tooltip("Qué tanto se desplaza el hueso desde su posición de reposo, en unidades locales. Ajusta según el tamaño de tu sprite/cuenca del ojo.")]
    [SerializeField] private float maxOffsetX = 0.05f;
    [SerializeField] private float maxOffsetY = 0.05f;

    [Header("Suavizado")]
    [SerializeField] private float moveSpeed = 10f;

    [Header("Rango de detección (opcional)")]
    [Tooltip("Si está activo, el ojo solo sigue al jugador dentro de este rango. Fuera de rango, vuelve a su posición de reposo.")]
    [SerializeField] private bool useDetectionRange = false;
    [SerializeField] private float detectionRange = 8f;

    private const float RetargetInterval = 0.25f;
    private const float UnlimitedSearchRange = 100f;

    private Vector3 restLocalPosition; // Posición original del hueso (pose de reposo del rig/animación base)
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

        Vector3 desiredLocalPosition;

        if (withinRange)
        {
            // Dirección hacia el jugador, convertida a espacio LOCAL del padre del hueso
            // (para que el offset sea relativo a la orientación del sprite, no del mundo)
            Vector3 worldDir = target.position - transform.position;
            Vector3 localDir = transform.parent != null
                ? transform.parent.InverseTransformDirection(worldDir)
                : worldDir;
            localDir.Normalize();

            Vector3 offset = new Vector3(
                localDir.x * maxOffsetX,
                localDir.y * maxOffsetY,
                0f
            );

            desiredLocalPosition = restLocalPosition + offset;
        }
        else
        {
            // Fuera de rango: regresa a la posición de reposo
            desiredLocalPosition = restLocalPosition;
        }

        transform.localPosition = Vector3.Lerp(
            transform.localPosition,
            desiredLocalPosition,
            moveSpeed * Time.deltaTime
        );
    }

    // Con varios jugadores mira al más cercano; sin EnemyTargetFinder en el cíclope usa el target asignado.
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
