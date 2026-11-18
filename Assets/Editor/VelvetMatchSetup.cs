#if UNITY_EDITOR
using System.Linq;
using CodenameVelvet.Characters;
using CodenameVelvet.Core;
using CodenameVelvet.Core.Events;
using CodenameVelvet.Gathering;
using CodenameVelvet.Multiplayer;
using CodenameVelvet.Player;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Herramienta de UN SOLO USO (fase B2 + E). Bórrala después de correrla.
/// <list type="bullet">
/// <item>PLAYER: agrega PlayerIdentity y PlayerInventory (con su drop al morir) y conecta PlayerDiedEvent.</item>
/// <item>Game_Map: quita el player y la cámara de prueba; agrega PlayerSpawner (lobby → jugadores con
/// pantalla dividida) y MatchFlow con sus paneles de Game Over y Victoria.</item>
/// <item>CharacterMenu: el botón de empezar carga Game_Map en vez de Dev_Enemies.</item>
/// </list>
/// </summary>
public static class VelvetMatchSetup
{
    private const string PlayerPrefabPath = "Assets/Assets/CHARACTERS/PLAYER.prefab";
    private const string CameraPrefabPath = "Assets/Prefabs/Player/PlayerCamera.prefab";
    private const string PickupPrefabPath = "Assets/Prefabs/Gathering/ResourcePickup.prefab";
    private const string DiedEventPath = "Assets/ScriptableObjects/Events/PlayerDiedEvent.asset";
    private const string RosterPath = "Assets/ScriptableObjects/Multiplayer/SessionRoster.asset";
    private const string CatalogPath = "Assets/ScriptableObjects/Characters/CharacterCatalog.asset";
    private const string GameScenePath = "Assets/Scenes/Game_Map.unity";
    private const string LobbyScenePath = "Assets/Scenes/CharacterMenu.unity";
    private const string GameSceneName = "Game_Map";
    private const string OldLevelSceneName = "Dev_Enemies";
    private const string SpawnRootName = "PlayerSpawnPoints";
    private const float DropScatterRadius = 2f; // los pickups se escalaron al tamaño del player

    [MenuItem("Tools/Velvet/Match/Setup Players + Split Screen (B2+E)")]
    public static void Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        string previousScene = SceneManager.GetActiveScene().path;

        SetupPlayerPrefab();
        SetupGameScene();
        PointLobbyToGameScene();

        if (!string.IsNullOrEmpty(previousScene))
            EditorSceneManager.OpenScene(previousScene);
        Debug.Log("[VelvetMatchSetup] Listo. Prueba desde MainMenu → lobby → Game_Map y después borra este script.");
    }

    // ─────────────────────── PLAYER ───────────────────────

    private static void SetupPlayerPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            if (!root.TryGetComponent(out PlayerIdentity _))
                root.AddComponent<PlayerIdentity>();

            if (!root.TryGetComponent(out PlayerInventory inventory))
                inventory = root.AddComponent<PlayerInventory>();
            SetFields(inventory,
                ("pickupPrefab", AssetDatabase.LoadAssetAtPath<ResourcePickup>(PickupPrefabPath)));
            var so = new SerializedObject(inventory);
            so.FindProperty("dropScatterRadius").floatValue = DropScatterRadius;
            so.ApplyModifiedPropertiesWithoutUndo();

            SetFields(root.GetComponent<PlayerHealth>(),
                ("onPlayerDied", AssetDatabase.LoadAssetAtPath<PlayerDiedEvent>(DiedEventPath)));

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Debug.Log("[VelvetMatchSetup] PLAYER: PlayerIdentity + PlayerInventory + PlayerDiedEvent.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ─────────────────────── Game_Map ───────────────────────

    private static void SetupGameScene()
    {
        Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
        GameObject[] roots = scene.GetRootGameObjects();
        if (roots.Any(go => go.GetComponent<PlayerSpawner>() != null))
        {
            Debug.Log("[VelvetMatchSetup] Game_Map ya tiene PlayerSpawner; escena sin cambios.");
            return;
        }

        // El spawner crea players y cámaras en runtime: fuera los de prueba.
        var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        var cameraPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CameraPrefabPath);
        foreach (GameObject go in roots)
        {
            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(go);
            if (source == playerPrefab || source == cameraPrefab || go.GetComponent<Camera>() != null)
                Object.DestroyImmediate(go);
        }

        MatchFlow matchFlow = CreateMatchFlow();

        Transform spawnRoot = scene.GetRootGameObjects().FirstOrDefault(go => go.name == SpawnRootName)?.transform;
        Transform[] spawnPoints = spawnRoot != null
            ? spawnRoot.Cast<Transform>().OrderBy(t => t.name).ToArray()
            : new Transform[0];
        if (spawnPoints.Length == 0)
            Debug.LogWarning($"[VelvetMatchSetup] No encontré '{SpawnRootName}' con hijos; asigna los spawn points a mano.");

        var spawner = new GameObject("PlayerSpawner").AddComponent<PlayerSpawner>();
        SetFields(spawner,
            ("rosterAsset", AssetDatabase.LoadAssetAtPath<SessionRosterAsset>(RosterPath)),
            ("catalog", AssetDatabase.LoadAssetAtPath<CharacterCatalog>(CatalogPath)),
            ("playerPrefab", playerPrefab.GetComponent<PlayerInput>()),
            ("cameraPrefab", cameraPrefab.GetComponent<CameraFollow>()),
            ("matchFlow", matchFlow));
        var so = new SerializedObject(spawner);
        SerializedProperty points = so.FindProperty("spawnPoints");
        points.arraySize = spawnPoints.Length;
        for (int i = 0; i < spawnPoints.Length; i++)
            points.GetArrayElementAtIndex(i).objectReferenceValue = spawnPoints[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[VelvetMatchSetup] Game_Map: PlayerSpawner ({spawnPoints.Length} spawn points) + MatchFlow.");
    }

    private static MatchFlow CreateMatchFlow()
    {
        var canvasGo = new GameObject("MatchCanvas", typeof(Canvas), typeof(CanvasScaler));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; // encima de las 4 vistas
        canvas.sortingOrder = 100;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject gameOver = CreatePanel(canvasGo.transform, "GameOverPanel", "GAME OVER", new Color(0.85f, 0.2f, 0.2f));
        GameObject victory = CreatePanel(canvasGo.transform, "VictoryPanel", "¡VICTORIA!", new Color(1f, 0.85f, 0.3f));

        var matchFlow = new GameObject("MatchFlow").AddComponent<MatchFlow>();
        SetFields(matchFlow, ("gameOverPanel", gameOver), ("victoryPanel", victory));
        return matchFlow;
    }

    private static GameObject CreatePanel(Transform parent, string name, string message, Color textColor)
    {
        var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        Stretch(panel.GetComponent<RectTransform>());
        panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f);

        var text = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        text.transform.SetParent(panel.transform, false);
        Stretch(text.GetComponent<RectTransform>());
        var tmp = text.GetComponent<TextMeshProUGUI>();
        tmp.text = message;
        tmp.fontSize = 140f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = textColor;

        panel.SetActive(false);
        return panel;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    // ─────────────────────── Lobby ───────────────────────

    private static void PointLobbyToGameScene()
    {
        Scene scene = EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Single);
        int changed = 0;
        foreach (CambioEscenaAnimado transition in scene.GetRootGameObjects()
                     .SelectMany(go => go.GetComponentsInChildren<CambioEscenaAnimado>(true)))
        {
            var so = new SerializedObject(transition);
            SerializedProperty target = so.FindProperty("nombreSiguienteEscena");
            if (target.stringValue != OldLevelSceneName) continue;
            target.stringValue = GameSceneName;
            so.ApplyModifiedPropertiesWithoutUndo();
            changed++;
        }

        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[VelvetMatchSetup] CharacterMenu: {changed} transición(es) ahora cargan {GameSceneName}.");
    }

    // ─────────────────────── Jefe (fase C) ───────────────────────

    private const string BossSourceScenePath = "Assets/Scenes/CD_Velvet.unity";
    private const string BossPrefabPath = "Assets/Prefabs/Enemies/Boss_Cyclops.prefab";
    private const string CaveName = "Cueva";
    private const float BossRoamMargin = 25f; // cuánto puede alejarse de la cueva (XZ, unidades)

    /// <summary>
    /// Saca el ciclope configurado en CD_Velvet a un prefab y lo pone en la cueva de Game_Map con una
    /// zona de deambular/persecución alrededor de la cueva y BossVictory conectado a MatchFlow.
    /// </summary>
    [MenuItem("Tools/Velvet/Match/Setup Boss in Game_Map (C)")]
    public static void SetupBoss()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        string previousScene = SceneManager.GetActiveScene().path;

        GameObject bossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath) ?? CreateBossPrefab();
        if (bossPrefab != null)
            PlaceBoss(bossPrefab);

        if (!string.IsNullOrEmpty(previousScene))
            EditorSceneManager.OpenScene(previousScene);
    }

    private static GameObject CreateBossPrefab()
    {
        Scene scene = EditorSceneManager.OpenScene(BossSourceScenePath, OpenSceneMode.Single);
        CyclopsBrainLookup(scene, out GameObject source);
        if (source == null)
        {
            Debug.LogError("[VelvetMatchSetup] No encontré el ciclope (CyclopsBrain) en CD_Velvet.");
            return null;
        }

        // Se edita el objeto de la escena solo en memoria (conserva el arte como prefab anidado de
        // CYCLOPS 2) y CD_Velvet NO se guarda: la escena de pruebas de EVP029 queda igual.
        source.name = "Boss_Cyclops";
        source.layer = LayerMask.NameToLayer("ENEMY");
        source.GetComponent<Rigidbody>().interpolation = RigidbodyInterpolation.Interpolate;

        var brain = new SerializedObject(source.GetComponent<CodenameVelvet.Enemies.CyclopsBrain>());
        brain.FindProperty("groundMask").intValue = LayerMask.GetMask("Ground");
        brain.FindProperty("wanderArea").objectReferenceValue = null; // la zona es de cada escena
        brain.ApplyModifiedPropertiesWithoutUndo();

        if (source.GetComponent<CodenameVelvet.Enemies.BossVictory>() == null)
            source.AddComponent<CodenameVelvet.Enemies.BossVictory>();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(source, BossPrefabPath);
        EditorSceneManager.OpenScene(BossSourceScenePath, OpenSceneMode.Single); // descarta los cambios en memoria
        Debug.Log($"[VelvetMatchSetup] Prefab creado: {BossPrefabPath}");
        return prefab;
    }

    private static void PlaceBoss(GameObject bossPrefab)
    {
        Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
        GameObject[] roots = scene.GetRootGameObjects();
        if (roots.Any(go => PrefabUtility.GetCorrespondingObjectFromSource(go) == bossPrefab))
        {
            Debug.Log("[VelvetMatchSetup] Game_Map ya tiene al jefe; sin cambios.");
            return;
        }

        Renderer cave = roots.SelectMany(go => go.GetComponentsInChildren<Renderer>(true))
            .FirstOrDefault(r => r.name.Contains(CaveName));
        if (cave == null)
        {
            Debug.LogError($"[VelvetMatchSetup] No encontré una pieza '{CaveName}' en Game_Map; coloca al jefe a mano.");
            return;
        }
        Bounds caveBounds = cave.bounds;

        // Desde el centro de la cueva (debajo del techo) hacia abajo, al piso.
        Physics.SyncTransforms();
        Vector3 position = caveBounds.center;
        if (Physics.Raycast(caveBounds.center, Vector3.down, out RaycastHit hit, caveBounds.size.y,
                LayerMask.GetMask("Ground"), QueryTriggerInteraction.Ignore))
            position = hit.point;
        else
            Debug.LogWarning("[VelvetMatchSetup] No encontré piso dentro de la cueva; revisa la altura del jefe.");

        var area = new GameObject("Boss Roam Area") { layer = LayerMask.NameToLayer("Ignore Raycast") };
        area.transform.position = caveBounds.center;
        var box = area.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(caveBounds.size.x + BossRoamMargin * 2f, caveBounds.size.y + 20f,
            caveBounds.size.z + BossRoamMargin * 2f);

        var boss = (GameObject)PrefabUtility.InstantiatePrefab(bossPrefab, scene);
        boss.transform.position = position;

        var brain = new SerializedObject(boss.GetComponent<CodenameVelvet.Enemies.CyclopsBrain>());
        brain.FindProperty("wanderArea").objectReferenceValue = box;
        brain.ApplyModifiedPropertiesWithoutUndo();

        MatchFlow matchFlow = roots.Select(go => go.GetComponent<MatchFlow>()).FirstOrDefault(m => m != null);
        if (matchFlow == null)
            Debug.LogWarning("[VelvetMatchSetup] Game_Map no tiene MatchFlow: corre primero 'Setup Players + Split Screen'.");
        SetFields(boss.GetComponent<CodenameVelvet.Enemies.BossVictory>(), ("matchFlow", matchFlow));

        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[VelvetMatchSetup] Jefe colocado en la cueva ({position}). Ajusta 'Boss Roam Area' si quieres que salga más o menos.");
    }

    private static void CyclopsBrainLookup(Scene scene, out GameObject boss)
    {
        boss = scene.GetRootGameObjects()
            .SelectMany(go => go.GetComponentsInChildren<CodenameVelvet.Enemies.CyclopsBrain>(true))
            .Select(brain => brain.gameObject)
            .FirstOrDefault();
    }

    // ─────────────────────── Util ───────────────────────

    private static void SetFields(Object target, params (string name, Object value)[] fields)
    {
        var so = new SerializedObject(target);
        foreach ((string name, Object value) in fields)
        {
            SerializedProperty property = so.FindProperty(name);
            if (property == null)
            {
                Debug.LogError($"[VelvetMatchSetup] {target.GetType().Name} no tiene el campo '{name}'.");
                continue;
            }
            property.objectReferenceValue = value;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
