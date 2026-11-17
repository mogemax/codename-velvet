using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Ordena personajes 2D en un mundo 3D como si fueran objetos 3D: el que está más cerca
/// de la cámara (Z más negativa, la cámara mira hacia +Z) se dibuja encima, y el que está
/// más lejos queda detrás. Va en el MISMO objeto que el Sorting Group (raíz del Player o del Enemy)
/// y usa la posición de ese objeto (los pies del personaje) como referencia.
///
/// Todos los personajes usan la misma escala (<see cref="UnitsPerOrder"/>) para que sus valores
/// sean comparables, y el resultado se mantiene dentro del rango que acepta Unity (±32767):
/// antes se multiplicaba por 1000 y a partir de Z = ±33 todos se topaban en el mismo valor,
/// así que Unity los dibujaba en cualquier orden.
/// </summary>
[RequireComponent(typeof(SortingGroup))]
public class DynamicSortingOrder : MonoBehaviour
{
    /// <summary>Pasos de Sorting Order por unidad de Z. 100 = precisión de 1 cm en un mapa de ±320 unidades.</summary>
    public const float UnitsPerOrder = 100f;
    private const int MaxOrder = 32000; // margen bajo el límite de Unity para los offsets manuales

    [Header("Offset manual (opcional)")]
    [Tooltip("Suma un valor fijo al resultado, útil si necesitas forzar que algo específico siempre esté delante o detrás de todo lo demás.")]
    [SerializeField] private int manualOffset = 0;

    private SortingGroup sortingGroup;

    public static int OrderForZ(float z, int offset = 0) =>
        Mathf.Clamp(Mathf.RoundToInt(-z * UnitsPerOrder), -MaxOrder, MaxOrder) + offset;

    private void Awake()
    {
        sortingGroup = GetComponent<SortingGroup>();
    }

    private void LateUpdate()
    {
        sortingGroup.sortingOrder = OrderForZ(transform.position.z, manualOffset);
    }
}
