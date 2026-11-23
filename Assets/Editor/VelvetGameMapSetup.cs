#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Puts every object in Mapa.prefab on the Ground layer and marks it static. Ground raycasts
/// (player movement, cyclops, spawn zones) only hit that layer, so re-run it whenever map pieces
/// are added.
/// </summary>
public static class VelvetGameMapSetup
{
    private const string MapPrefabPath = "Assets/Prefabs/Map/Mapa.prefab";
    private const string GroundLayerName = "Ground";

    // No ContributeGI: it would require lightmap UVs on every mesh.
    private const StaticEditorFlags MapStaticFlags =
        StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic;

    [MenuItem("Tools/Velvet/Map/Apply Ground Layer")]
    public static void ApplyGroundLayer()
    {
        int ground = LayerMask.NameToLayer(GroundLayerName);
        if (ground < 0)
        {
            Debug.LogError($"[VelvetGameMapSetup] Layer '{GroundLayerName}' is missing from Tags and Layers.");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(MapPrefabPath);
        try
        {
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in all)
            {
                t.gameObject.layer = ground;
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, MapStaticFlags);
            }

            int withoutCollider = root.GetComponentsInChildren<MeshRenderer>(true)
                .Count(r => r.GetComponent<Collider>() == null);

            PrefabUtility.SaveAsPrefabAsset(root, MapPrefabPath);
            Debug.Log($"[VelvetGameMapSetup] Map: {all.Length} objects on layer '{GroundLayerName}' and static.");
            if (withoutCollider > 0)
                Debug.LogWarning($"[VelvetGameMapSetup] {withoutCollider} map MeshRenderer(s) have no collider and can be walked through.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
#endif
