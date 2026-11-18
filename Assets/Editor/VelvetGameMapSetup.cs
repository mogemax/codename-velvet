#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Herramientas del mapa jugable:
/// <list type="bullet">
/// <item><b>Apply Ground Layer</b>: pone la capa Ground y Static a todo Mapa.prefab. Reutilizable: córrelo
/// cada vez que se agreguen piezas al mapa. Los raycasts de suelo (ciclope, spawners) usan esa capa.</item>
/// <item><b>Create Game_Map Scene</b>: crea la escena de juego a partir de Dev Map (solo si no existe),
/// con 4 spawn points pegados al piso y un PLAYER de prueba. No sobrescribe una escena existente.</item>
/// </list>
/// </summary>
public static class VelvetGameMapSetup
{
    private const string MapPrefabPath = "Assets/Prefabs/Map/Mapa.prefab";
    private const string PlayerPrefabPath = "Assets/Assets/CHARACTERS/PLAYER.prefab";
    private const string SourceScenePath = "Assets/Scenes/Dev/Dev Map.unity";
    private const string GameScenePath = "Assets/Scenes/Game_Map.unity";
    private const string GroundLayerName = "Ground";

    private const string TestObjectName = "Prueba ";       // esfera de prueba de Dev Map
    private const string SpawnRootName = "PlayerSpawnPoints";
    private const int SpawnCount = 4;
    private const float SpawnSpacing = 4f;                 // separación entre spawn points (unidades)
    private const float RaycastHeight = 100f;              // desde qué altura se busca el piso
    private const float SpawnLift = 0.5f;                  // el player aparece un poco arriba y cae al piso

    // Batching + occlusion: ayuda al render con 4 cámaras. Sin ContributeGI (pediría UVs de lightmap).
    private const StaticEditorFlags MapStaticFlags =
        StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic;

    // ─────────────────────── Capa Ground ───────────────────────

    [MenuItem("Tools/Velvet/Map/Apply Ground Layer")]
    public static void ApplyGroundLayer()
    {
        int ground = LayerMask.NameToLayer(GroundLayerName);
        if (ground < 0)
        {
            Debug.LogError($"[VelvetGameMapSetup] No existe la capa '{GroundLayerName}' en Tags and Layers.");
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
            Debug.Log($"[VelvetGameMapSetup] Mapa: {all.Length} objetos en capa '{GroundLayerName}' y Static.");
            if (withoutCollider > 0)
                Debug.LogWarning($"[VelvetGameMapSetup] {withoutCollider} MeshRenderer(s) del mapa no tienen collider: se podrán atravesar.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ─────────────────────── Escena Game_Map ───────────────────────

    [MenuItem("Tools/Velvet/Map/Create Game_Map Scene")]
    public static void CreateGameMapScene()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GameScenePath) != null)
        {
            Debug.LogWarning($"[VelvetGameMapSetup] {GameScenePath} ya existe; no se sobrescribe.");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        if (!AssetDatabase.CopyAsset(SourceScenePath, GameScenePath))
        {
            Debug.LogError($"[VelvetGameMapSetup] No se pudo copiar {SourceScenePath}.");
            return;
        }

        ApplyGroundLayer(); // el raycast de los spawn points necesita la capa en el mapa
        var scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);

        // Ancla = la esfera de prueba que el equipo ya puso sobre el mapa; luego se borra.
        GameObject testObject = scene.GetRootGameObjects().FirstOrDefault(go => go.name == TestObjectName);
        Vector3 anchor = testObject != null ? testObject.transform.position : Vector3.zero;
        if (testObject != null) Object.DestroyImmediate(testObject);

        // El PLAYER trae su propia cámara (con AudioListener): la de la escena sobra por ahora.
        // En la Fase E (pantalla dividida) las cámaras dejan de vivir en el player.
        foreach (Camera cam in scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Camera>(true)))
            Object.DestroyImmediate(cam.gameObject);

        Transform[] spawns = CreateSpawnPoints(anchor);
        PlaceTestPlayer(spawns[0].position);

        EditorSceneManager.SaveScene(scene);
        AddToBuildSettings(GameScenePath);
        Debug.Log($"[VelvetGameMapSetup] {GameScenePath} creada. Mueve '{SpawnRootName}' a donde deban aparecer los jugadores.");
    }

    private static Transform[] CreateSpawnPoints(Vector3 anchor)
    {
        Physics.SyncTransforms();
        int groundMask = LayerMask.GetMask(GroundLayerName);
        var root = new GameObject(SpawnRootName).transform;
        root.position = anchor;

        var spawns = new Transform[SpawnCount];
        for (int i = 0; i < SpawnCount; i++)
        {
            // Cuadrícula 2x2 centrada en el ancla.
            var offset = new Vector3((i % 2 - 0.5f) * SpawnSpacing, 0f, (i / 2 - 0.5f) * SpawnSpacing);
            Vector3 position = anchor + offset;

            if (Physics.Raycast(position + Vector3.up * RaycastHeight, Vector3.down, out RaycastHit hit,
                    RaycastHeight * 2f, groundMask, QueryTriggerInteraction.Ignore))
                position = hit.point;
            else
                Debug.LogWarning($"[VelvetGameMapSetup] PlayerSpawn_{i + 1}: no hay piso debajo de {position}; colócalo a mano.");

            var spawn = new GameObject($"PlayerSpawn_{i + 1}").transform;
            spawn.SetParent(root, false);
            spawn.position = position;
            spawns[i] = spawn;
        }
        return spawns;
    }

    private static void PlaceTestPlayer(Vector3 groundPoint)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[VelvetGameMapSetup] No se encontró {PlayerPrefabPath}.");
            return;
        }
        var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        player.transform.position = groundPoint + Vector3.up * SpawnLift;
    }

    private static void AddToBuildSettings(string path)
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == path)) return;
        scenes.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
#endif
