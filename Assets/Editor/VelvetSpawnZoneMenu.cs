#if UNITY_EDITOR
using CodenameVelvet.Enemies;
using CodenameVelvet.Gathering;
using CodenameVelvet.Spawning;
using UnityEditor;
using UnityEngine;

/// <summary>
/// GameObject ▸ Velvet ▸ Spawn Zone: creates a ready-to-use <see cref="SpawnZone"/> (the 3 enemy types and the
/// 3 resource nodes, equal weights) on the ground under the Scene view pivot.
/// </summary>
public static class VelvetSpawnZoneMenu
{
    private const string EnemyPrefabPath = "Assets/Prefabs/Enemies/EnemyPlaceholder.prefab";
    private static readonly string[] EnemyDefinitionPaths =
    {
        "Assets/ScriptableObjects/Enemies/Bruja.asset",
        "Assets/ScriptableObjects/Enemies/Minotauro.asset",
        "Assets/ScriptableObjects/Enemies/Sirena.asset",
    };
    private static readonly string[] NodePrefabPaths =
    {
        "Assets/Prefabs/Gathering/Arbol.prefab",
        "Assets/Prefabs/Gathering/Roca.prefab",
        "Assets/Prefabs/Gathering/VetaMineral.prefab",
    };
    private static readonly Vector3 DefaultSize = new Vector3(40f, 10f, 40f); // height = range of valid floor heights

    [MenuItem("GameObject/Velvet/Spawn Zone", false, 10)]
    public static void Create(MenuCommand command)
    {
        var zone = new GameObject("SpawnZone") { layer = LayerMask.NameToLayer("Ignore Raycast") };
        GameObjectUtility.SetParentAndAlign(zone, command.context as GameObject);
        zone.transform.position = GroundUnderSceneView();

        var box = zone.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = DefaultSize;

        var so = new SerializedObject(zone.AddComponent<SpawnZone>());
        so.FindProperty("enemyPrefab").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<EnemyBrain>(EnemyPrefabPath);
        Fill(so.FindProperty("enemies"), "definition", EnemyDefinitionPaths, AssetDatabase.LoadAssetAtPath<EnemyDefinition>);
        Fill(so.FindProperty("nodes"), "prefab", NodePrefabPaths, AssetDatabase.LoadAssetAtPath<ResourceNode>);
        so.ApplyModifiedPropertiesWithoutUndo();

        Undo.RegisterCreatedObjectUndo(zone, "Create Spawn Zone");
        Selection.activeObject = zone;
    }

    private static void Fill(SerializedProperty array, string field, string[] paths, System.Func<string, Object> load)
    {
        array.arraySize = paths.Length;
        for (int i = 0; i < paths.Length; i++)
        {
            SerializedProperty entry = array.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative(field).objectReferenceValue = load(paths[i]);
            entry.FindPropertyRelative("weight").floatValue = 1f;
        }
    }

    private static Vector3 GroundUnderSceneView()
    {
        SceneView view = SceneView.lastActiveSceneView;
        Vector3 pivot = view != null ? view.pivot : Vector3.zero;
        Physics.SyncTransforms();
        return Physics.Raycast(pivot + Vector3.up * 500f, Vector3.down, out RaycastHit hit, 1000f,
            LayerMask.GetMask("Ground"), QueryTriggerInteraction.Ignore)
            ? hit.point
            : pivot;
    }
}
#endif
