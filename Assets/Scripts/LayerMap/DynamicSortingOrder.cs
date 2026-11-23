using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Sorts 2D characters in the 3D world like 3D objects: whoever is closer to the camera (more negative Z,
/// the camera looks toward +Z) draws on top. Uses this object's position (the character's feet) and drives
/// the Sorting Group on the same object.
/// Every character shares <see cref="UnitsPerOrder"/> so their values are comparable, and the result stays
/// inside Unity's sorting order range (±32767).
/// </summary>
[RequireComponent(typeof(SortingGroup))]
public class DynamicSortingOrder : MonoBehaviour
{
    /// <summary>Sorting order steps per Z unit. 100 = 1 cm precision on a ±320 unit map.</summary>
    public const float UnitsPerOrder = 100f;
    private const int MaxOrder = 32000; // headroom below Unity's limit for the manual offsets

    [Tooltip("Added to the result to force something in front of or behind everything else.")]
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
