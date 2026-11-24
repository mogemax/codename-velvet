using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Grounds a sprite character or prop in the 3D world:
/// <list type="bullet">
/// <item>A soft dark ellipse under it, so it reads as standing on the ground even when the sun is high. Created on
/// Awake (before other components cache their renderers, so it hides with the rest when a node is depleted or a
/// player dies).</item>
/// <item>Tells every "Velvet/Sprite" renderer below where the ground is (this object's height: its feet), so their
/// cast shadow starts at the feet. Updated only when the height changes. It goes through a copy of the material
/// owned by this object (one per material it uses), not a MaterialPropertyBlock: a property block turns off the
/// GPU deformation of bone-animated sprites (they would stop animating).</item>
/// <item>Size: from this object's collider (its body), unless <see cref="width"/> is set.</item>
/// <item>Draws after the ground and the water but before every sprite (its own Sorting Group, sorted at the root),
/// so it never covers another character.</item>
/// <item>It is a child of this object: it follows it and scales with it.</item>
/// </list>
/// </summary>
[DefaultExecutionOrder(-100)]
public class ContactShadow : MonoBehaviour
{
    private const string ChildName = "ContactShadow";
    private const int SortingOrder = -32000; // above the water (-32768), below every character
    private const float Lift = 0.08f;         // above the ground, so bumpy terrain does not swallow it
    private static readonly int ShadowGroundId = Shader.PropertyToID("_ShadowGround");

    [SerializeField] private Material material;
    [Tooltip("World width of the ellipse. 0 = from the collider.")]
    [SerializeField, Min(0f)] private float width = 0f;
    [Tooltip("Ellipse width × this = how far it reaches toward / away from the camera.")]
    [SerializeField, Range(0.1f, 1f)] private float depthRatio = 0.5f;
    [Tooltip("Width relative to the collider's diameter, when Width is 0.")]
    [SerializeField, Min(0.1f)] private float colliderScale = 1.3f;
    [SerializeField] private Vector3 offset = Vector3.zero;

    private readonly List<Material> groundMaterials = new List<Material>();
    private float groundY = float.NaN;

    private void Awake()
    {
        UseOwnGroundMaterials();
        if (material == null || transform.Find(ChildName) != null) return;

        float worldWidth = width > 0f ? width : ColliderWidth();
        Vector3 scale = transform.lossyScale;
        float sx = Mathf.Max(Mathf.Abs(scale.x), 0.0001f), sz = Mathf.Max(Mathf.Abs(scale.z), 0.0001f);

        var shadow = new GameObject(ChildName);
        shadow.transform.SetParent(transform, false);
        shadow.transform.localPosition = offset + Vector3.up * (Lift / Mathf.Max(Mathf.Abs(scale.y), 0.0001f));
        shadow.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // the quad faces up
        shadow.transform.localScale = new Vector3(worldWidth / sx, worldWidth * depthRatio / sz, 1f);

        shadow.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        var meshRenderer = shadow.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;

        var group = shadow.AddComponent<SortingGroup>();
        group.sortAtRoot = true;
        group.sortingOrder = SortingOrder;
    }

    private void LateUpdate()
    {
        float y = transform.position.y;
        if (Mathf.Abs(y - groundY) < 0.001f) return;
        groundY = y;
        var ground = new Vector4(y, 1f, 0f, 0f);
        foreach (Material groundMaterial in groundMaterials)
            groundMaterial.SetVector(ShadowGroundId, ground);
    }

    private void OnDestroy()
    {
        foreach (Material groundMaterial in groundMaterials)
            Destroy(groundMaterial);
    }

    /// <summary>Every sprite below that reads a ground height gets this object's own copy of its material.</summary>
    private void UseOwnGroundMaterials()
    {
        var copies = new Dictionary<Material, Material>();
        foreach (SpriteRenderer sprite in GetComponentsInChildren<SpriteRenderer>(true))
        {
            Material shared = sprite.sharedMaterial;
            if (shared == null || !shared.HasProperty(ShadowGroundId)) continue;
            if (!copies.TryGetValue(shared, out Material copy))
            {
                copy = new Material(shared) { name = $"{shared.name} ({name})" };
                copies.Add(shared, copy);
                groundMaterials.Add(copy);
            }
            sprite.sharedMaterial = copy;
        }
    }

    private float ColliderWidth()
    {
        Collider body = GetComponent<Collider>();
        if (body == null) return 1f;
        Vector3 extents = body.bounds.extents;
        return Mathf.Max(extents.x, extents.z) * 2f * colliderScale;
    }
}
