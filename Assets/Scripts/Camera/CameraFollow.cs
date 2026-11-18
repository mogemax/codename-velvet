using UnityEngine;

/// <summary>
/// Cámara estilo Cult of the Lamb: sigue al jugador manteniendo un ángulo
/// y una distancia fijos, con suavizado. Colócala en un objeto vacío
/// separado del Player (no como hijo directo) y asigna el Target.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [Header("Referencia")]
    [SerializeField] private Transform target; // Arrastra aquí el Player

    [Header("Offset de cámara")]
    [Tooltip("Posición relativa al jugador. Y alto, Z negativo para quedar detrás/arriba")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 10f, -8f);

    [Header("Suavizado")]
    [Tooltip("Qué tan rápido alcanza al jugador. Más alto = más pegada. Igual a cualquier FPS.")]
    [SerializeField] private float smoothSpeed = 8f;

    [Header("Enfoque")]
    [Tooltip("Si está activo, la cámara siempre rota para mirar exactamente al target, evitando desalineación entre Offset y Rotation. Recomendado: activado.")]
    [SerializeField] private bool alwaysLookAtTarget = true;

    [Tooltip("Desplaza el PUNTO al que la cámara mira, sin mover la posición real de la cámara. Y positivo = la cámara mira más arriba del target, lo que hace que el personaje aparezca más ABAJO en pantalla.")]
    [SerializeField] private Vector3 lookAtOffset = new Vector3(0f, 2f, 0f);

    [Header("Límites opcionales (para no salir del mapa)")]
    [SerializeField] private bool useBounds = false;
    [SerializeField] private Vector2 minBounds;
    [SerializeField] private Vector2 maxBounds;

    /// <summary>Asigna a quién sigue (pantalla dividida: una cámara por jugador). Llamar antes de Start.</summary>
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    private void Start()
    {
        if (target == null)
        {
            Debug.LogWarning("CameraFollow: no se asignó target, buscando por tag 'Player'");
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) target = player.transform;
        }

        // Empieza ya en su lugar (sin "volar" desde donde quedó en la escena).
        if (target != null) transform.position = target.position + offset;

        // Fuerza la rotación inicial mirando hacia el offset (ángulo isométrico)
        transform.LookAt(target != null ? target.position + lookAtOffset : Vector3.zero);
    }

    // LateUpdate: el Rigidbody del jugador interpola su posición cada frame, así que la cámara
    // la lee ya actualizada. Seguirlo en FixedUpdate (50 Hz) se ve a tirones en pantallas de 60+ Hz.
    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 desiredPosition = target.position + offset;

        if (useBounds)
        {
            desiredPosition.x = Mathf.Clamp(desiredPosition.x, minBounds.x, maxBounds.x);
            desiredPosition.z = Mathf.Clamp(desiredPosition.z, minBounds.y, maxBounds.y);
        }

        // Suavizado exponencial: el mismo recorrido por segundo sin importar los FPS.
        float t = 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, desiredPosition, t);

        if (alwaysLookAtTarget)
            transform.LookAt(target.position + lookAtOffset);
    }
}
