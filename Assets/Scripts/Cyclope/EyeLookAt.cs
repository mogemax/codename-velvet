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

    private Vector3 restLocalPosition; // Posición original del hueso (pose de reposo del rig/animación base)

    private void Awake()
    {
        restLocalPosition = transform.localPosition;

        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) target = player.transform;
        }
    }

    private void LateUpdate()
    {
        if (target == null) return;

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
}
