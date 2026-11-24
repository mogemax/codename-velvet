#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Map layer tools. Ground raycasts (player movement, cyclops, spawn zones) only hit the Ground layer and the
/// water edge only sees the Water layer, so re-run them whenever map pieces are added:
/// <list type="bullet">
/// <item>Apply Ground Layer: everything in Mapa.prefab (level 2).</item>
/// <item>Mark Selection As Ground / Water: the objects selected in the open scene, adding missing colliders.</item>
/// </list>
/// </summary>
public static class VelvetGameMapSetup
{
    private const string MapPrefabPath = "Assets/Prefabs/Map/Mapa.prefab";
    private const string GroundLayerName = "Ground";
    private const string WaterLayerName = "Water";

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

    /// <summary>Selected scene objects and their children: Ground layer, static, and a MeshCollider on every mesh without a collider.</summary>
    [MenuItem("Tools/Velvet/Map/Mark Selection As Ground")]
    public static void MarkSelectionAsGround() => MarkSelection(GroundLayerName);

    /// <summary>Same as Ground, on the Water layer: characters never walk onto it (see WaterEdge).</summary>
    [MenuItem("Tools/Velvet/Map/Mark Selection As Water")]
    public static void MarkSelectionAsWater() => MarkSelection(WaterLayerName);

    [MenuItem("Tools/Velvet/Map/Mark Selection As Ground", true)]
    [MenuItem("Tools/Velvet/Map/Mark Selection As Water", true)]
    private static bool HasSceneSelection() => Selection.gameObjects.Any(go => go.scene.IsValid());


    private static void MarkSelection(string layerName)
    {
        int layer = LayerMask.NameToLayer(layerName);
        if (layer < 0)
        {
            Debug.LogError($"[VelvetGameMapSetup] Layer '{layerName}' is missing from Tags and Layers.");
            return;
        }

        int objects = 0, colliders = 0;
        foreach (GameObject selected in Selection.gameObjects.Where(go => go.scene.IsValid()))
        {
            foreach (Transform t in selected.GetComponentsInChildren<Transform>(true))
            {
                Undo.RecordObject(t.gameObject, "Mark map layer");
                t.gameObject.layer = layer;
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, MapStaticFlags);
                objects++;

                if (t.TryGetComponent(out MeshFilter filter) && filter.sharedMesh != null && !t.TryGetComponent(out Collider _))
                {
                    Undo.AddComponent<MeshCollider>(t.gameObject).sharedMesh = filter.sharedMesh;
                    colliders++;
                }
            }
        }

        Debug.Log($"[VelvetGameMapSetup] {objects} objects on layer '{layerName}', {colliders} MeshCollider(s) added. Save the scene.");
    }
}
#endif
