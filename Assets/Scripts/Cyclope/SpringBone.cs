using UnityEngine;

/// <summary>
/// Simula física simple de péndulo/resorte para un hueso secundario
/// (capa, tela, cabello, etc.). Colócalo directamente en el hueso que
/// quieres que se balancee (ej. "CAPE_1"). Se ejecuta en LateUpdate
/// para aplicarse DESPUÉS de que el Animator ponga su pose base.
///
/// Cómo funciona: el hueso intenta "alcanzar" su rotación de reposo
/// (la que le da el Animator/rig), pero con inercia y resorte, así que
/// se queda atrás y oscila cuando el personaje se mueve o rota rápido.
/// </summary>
public class SpringBone : MonoBehaviour
{
    [Header("Física del resorte")]
    [Tooltip("Qué tan rígido es el resorte. Más alto = vuelve más rápido a su posición de reposo (menos bamboleo).")]
    [SerializeField] private float stiffness = 8f;

    [Tooltip("Amortiguación del movimiento. Más alto = se detiene más rápido (menos oscilación libre).")]
    [SerializeField] private float damping = 4f;

    [Header("Sensibilidad al movimiento")]
    [Tooltip("Qué tanto afecta la velocidad del personaje al balanceo. Súbelo si quieres más exageración al moverse.")]
    [SerializeField] private float movementInfluence = 15f;

    [Header("Límites")]
    [Tooltip("Ángulo máximo (grados) que se puede desviar de la pose de reposo. Evita que gire de forma exagerada o rota.")]
    [SerializeField] private float maxAngle = 25f;

    private Transform rootParent;       // El objeto raíz del personaje (para medir su velocidad)
    private Vector3 lastRootPosition;
    private Vector3 rootVelocity;

    private float currentAngleOffset = 0f; // Desviación actual respecto a la pose de reposo (grados, eje Z)
    private float angularVelocity = 0f;     // "Velocidad" de esa desviación (para el resorte)
    private Quaternion lastAppliedOffset = Quaternion.identity; // Para poder "restarlo" antes de aplicar el nuevo

    private void Start()
    {
        // Busca el objeto raíz del personaje subiendo por los padres
        // (asume que el root tiene el Rigidbody/PlayerMovement o similar).
        rootParent = transform.root;
        lastRootPosition = rootParent.position;
    }

    private void LateUpdate()
    {
        if (Time.deltaTime <= 0f) return; // en pausa (timeScale 0) la velocidad sería 0/0 = NaN
        // 1. Calcula qué tan rápido se está moviendo el personaje completo
        rootVelocity = (rootParent.position - lastRootPosition) / Time.deltaTime;
        lastRootPosition = rootParent.position;

        // 2. Convierte esa velocidad en una "fuerza" que empuja al hueso
        //    (usamos solo el eje X del mundo, proyectado al espacio local, ya que
        //    es un rig 2D y nos interesa el balanceo lateral principalmente)
        Vector3 localVelocity = transform.parent != null
            ? transform.parent.InverseTransformDirection(rootVelocity)
            : rootVelocity;

        float targetForce = -localVelocity.x * movementInfluence;

        // 3. Simulación tipo resorte: el ángulo "quiere" volver a 0 (reposo),
        //    pero la fuerza del movimiento lo empuja, y hay amortiguación.
        float springForce = (targetForce - currentAngleOffset) * stiffness;
        angularVelocity += springForce * Time.deltaTime;
        angularVelocity *= Mathf.Clamp01(1f - damping * Time.deltaTime);

        currentAngleOffset += angularVelocity * Time.deltaTime;
        currentAngleOffset = Mathf.Clamp(currentAngleOffset, -maxAngle, maxAngle);

        // 4. Aplica la rotación ENCIMA de lo que el Animator ya haya puesto,
        //    quitando primero el offset que nosotros mismos sumamos el frame
        //    anterior (si no, se acumularía sin control frame tras frame).
        Quaternion animatedBaseRotation = transform.localRotation * Quaternion.Inverse(lastAppliedOffset);

        Quaternion newOffset = Quaternion.Euler(0f, 0f, currentAngleOffset);
        transform.localRotation = animatedBaseRotation * newOffset;
        lastAppliedOffset = newOffset;
    }
}
