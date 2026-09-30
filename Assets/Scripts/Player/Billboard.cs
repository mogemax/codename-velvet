using UnityEngine;

/// <summary>
/// Hace que el objeto (el SpriteRoot del rig) siempre encare a la cámara.
/// Colócalo en el mismo objeto donde está el Sprite Skin / las piezas del sprite.
/// </summary>
public class Billboard : MonoBehaviour
{
    [Header("Referencia")]
    [SerializeField] private Camera targetCamera;

    public enum BillboardAxis
    {
        FullBillboard,  // Encara la cámara en todos los ejes
        YAxisOnly,      // Solo gira izquierda/derecha (rotación en Y), no se inclina
        XAxisOnly       // Solo se inclina arriba/abajo (rotación en X), no gira lateralmente
    }

    [Header("Modo")]
    [SerializeField] private BillboardAxis axisMode = BillboardAxis.YAxisOnly;

    private void Awake()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;
    }

    private void LateUpdate()
    {
        if (targetCamera == null) return;

        switch (axisMode)
        {
            case BillboardAxis.YAxisOnly:
                {
                    Vector3 direction = targetCamera.transform.position - transform.position;
                    direction.y = 0f;
                    if (direction.sqrMagnitude > 0.0001f)
                        transform.rotation = Quaternion.LookRotation(direction);
                    break;
                }

            case BillboardAxis.XAxisOnly:
                {
                    // Solo copia el ángulo de inclinación (pitch) de la cámara en X,
                    // mantiene la rotación Y/Z actuales del objeto sin tocarlas.
                    Vector3 currentEuler = transform.eulerAngles;
                    float camPitch = targetCamera.transform.eulerAngles.x;
                    transform.eulerAngles = new Vector3(camPitch, currentEuler.y, currentEuler.z);
                    break;
                }

            case BillboardAxis.FullBillboard:
            default:
                transform.rotation = Quaternion.LookRotation(transform.position - targetCamera.transform.position);
                break;
        }
    }
}
