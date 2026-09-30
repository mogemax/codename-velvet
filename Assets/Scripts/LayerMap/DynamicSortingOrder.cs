using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Actualiza el Sorting Order de un Sorting Group según la posición Z del objeto,
/// para que los personajes que estén más "adelante" (más cerca de la cámara)
/// se dibujen encima de los que estén más "atrás", como en Cult of the Lamb.
///
/// Colócalo en el MISMO objeto que tiene el componente Sorting Group
/// (el objeto raíz del Player o del Enemy).
/// </summary>
[RequireComponent(typeof(SortingGroup))]
public class DynamicSortingOrder : MonoBehaviour
{
    [Header("Ajuste de precisión")]
    [Tooltip("Multiplica la posición Z antes de redondear a entero. Súbelo si notas que dos objetos muy cerca en Z comparten el mismo Sorting Order.")]
    [SerializeField] private float precisionMultiplier = 100f;

    [Header("Offset manual (opcional)")]
    [Tooltip("Suma un valor fijo al resultado, útil si necesitas forzar que algo específico siempre esté delante o detrás de todo lo demás.")]
    [SerializeField] private int manualOffset = 0;

    private SortingGroup sortingGroup;

    private void Awake()
    {
        sortingGroup = GetComponent<SortingGroup>();
    }

    private void LateUpdate()
    {
        // Z más NEGATIVO (más cerca de la cámara, según el offset (0,10,-8) que usamos)
        // debe dibujarse ENCIMA → Sorting Order más alto.
        // Por eso invertimos el signo: a menor Z, mayor Sorting Order.
        int order = Mathf.RoundToInt(-transform.position.z * precisionMultiplier) + manualOffset;
        sortingGroup.sortingOrder = order;
    }
}
