#if UNITY_EDITOR
using System.Linq;
using CodenameVelvet.Core;
using CodenameVelvet.Player;
using TMPro;
using CodenameVelvet.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Puts the team's pause menu and "You died" screen (Assets/Scenes/ScenePrefabs) in the open level. Safe to run again.
/// <list type="number">
/// <item>Prefabs: a <see cref="MenuScreen"/> on the root wired to the buttons by name; the animator runs in unscaled
/// time (the game is frozen behind them) and the canvas draws above the HUD and the boss bar.</item>
/// <item>Scene: both screens placed and given to <see cref="MatchFlow"/>, the old built-in "GameOverPanel" removed,
/// and an EventSystem (Input System) so buttons work with mouse, keyboard and gamepads.</item>
/// <item>Players: each gets its own death screen (<see cref="PlayerDeathScreen"/>), a copy of the end screen.</item>
/// </list>
/// </summary>
public static class VelvetMenusSetup
{
    private const string Tag = "[VelvetMenusSetup]";
    private const string PausePath = "Assets/Scenes/ScenePrefabs/PauseMenu.prefab";
    private const string EndPath = "Assets/Scenes/ScenePrefabs/EndScreen.prefab";
    private const string OldGameOverName = "GameOverPanel";
    private const string DefaultUiActionsPath = "Packages/com.unity.inputsystem/InputSystem/Plugins/PlayerInput/DefaultInputActions.inputactions";
    private const string FontPath = "Assets/UI/Fonts/OPTITimes-Roman SDF.asset";
    private const string PromptMaterialPath = "Assets/Settings/Art/HudCount.mat";
    private const int MenuSortingOrder = 200; // above the boss bar (100) and the HUD

    [MenuItem("Tools/Velvet/Menus/Setup Pause and End Screen (Open Scene)")]
    public static void Setup()
    {
        SetupPrefab(PausePath, firstButton: "Resumebtn");
        SetupPrefab(EndPath, firstButton: "Rebornbtn");
        SetupPlayers();

        var scene = EditorSceneManager.GetActiveScene();
        MatchFlow flow = Object.FindAnyObjectByType<MatchFlow>();
        if (flow == null)
        {
            Debug.LogError($"{Tag} No MatchFlow in {scene.name}: nothing placed.");
            return;
        }

        var serialized = new SerializedObject(flow);
        serialized.FindProperty("pauseMenu").objectReferenceValue = PlaceOnce(PausePath, scene);
        serialized.FindProperty("endScreen").objectReferenceValue = PlaceOnce(EndPath, scene);
        serialized.ApplyModifiedProperties();

        RemoveOldGameOver(scene);
        EnsureEventSystem();
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"{Tag} Pause menu and end screen ready in {scene.name}. Save the scene.");
    }

    private static void SetupPrefab(string path, string firstButton)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Canvas canvas = root.GetComponentInChildren<Canvas>(true);
            Animator animator = canvas.GetComponent<Animator>();
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            canvas.sortingOrder = MenuSortingOrder;

            if (!root.TryGetComponent(out MenuScreen screen)) screen = root.AddComponent<MenuScreen>();
            Button first = ButtonNamed(root, firstButton);
            screen.Configure(canvas.gameObject, animator, first,
                resume: ButtonNamed(root, "Resumebtn"),
                restart: ButtonNamed(root, "Rebornbtn"),
                mainMenu: ButtonNamed(root, "MainMenubtn"),
                quit: ButtonNamed(root, "Extibtn"));
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void SetupPlayers()
    {
        var endScreen = AssetDatabase.LoadAssetAtPath<GameObject>(EndPath).GetComponent<MenuScreen>();
        foreach (string path in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath))
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset.GetComponent<PlayerMovement>() == null || asset.GetComponent<PlayerHealth>() == null) continue;
            if (PrefabUtility.GetPrefabAssetType(asset) == PrefabAssetType.Variant || asset.GetComponent<PlayerDeathScreen>() != null) continue;

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var screen = new SerializedObject(root.AddComponent<PlayerDeathScreen>());
                screen.FindProperty("screenPrefab").objectReferenceValue = endScreen;
                screen.FindProperty("font").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
                screen.FindProperty("promptMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(PromptMaterialPath);
                screen.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }

    private static Button ButtonNamed(GameObject root, string name) =>
        root.GetComponentsInChildren<Button>(true).FirstOrDefault(button => button.name == name);

    private static MenuScreen PlaceOnce(string path, UnityEngine.SceneManagement.Scene scene)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        foreach (MenuScreen existing in Object.FindObjectsByType<MenuScreen>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (PrefabUtility.GetCorrespondingObjectFromSource(existing.gameObject) == prefab)
                return existing;

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        Undo.RegisterCreatedObjectUndo(instance, "Place menu");
        return instance.GetComponent<MenuScreen>();
    }

    private static void RemoveOldGameOver(UnityEngine.SceneManagement.Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name != OldGameOverName) continue;
            Undo.DestroyObjectImmediate(child.gameObject);
            return;
        }
    }

    /// <summary>
    /// One EventSystem with the Input System UI module, using the package's default UI actions (as the main menu
    /// does): its action references are sub-assets, so they serialize in the scene.
    /// </summary>
    private static void EnsureEventSystem()
    {
        EventSystem system = Object.FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include);
        if (system == null)
        {
            system = new GameObject("EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();
            Undo.RegisterCreatedObjectUndo(system.gameObject, "Add EventSystem");
        }
        if (!system.TryGetComponent(out InputSystemUIInputModule module))
            module = system.gameObject.AddComponent<InputSystemUIInputModule>();

        var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(DefaultUiActionsPath);
        var references = AssetDatabase.LoadAllAssetsAtPath(DefaultUiActionsPath).OfType<InputActionReference>()
            .Where(reference => AssetDatabase.IsSubAsset(reference) && reference.action.actionMap.name == "UI")
            .ToDictionary(reference => reference.action.name);
        // Written as serialized fields: the module's property setters make scene-local copies of the references.
        var serialized = new SerializedObject(module);
        serialized.FindProperty("m_ActionsAsset").objectReferenceValue = asset;
        foreach ((string field, string action) in new[]
                 {
                     ("m_PointAction", "Point"), ("m_LeftClickAction", "Click"), ("m_MiddleClickAction", "MiddleClick"),
                     ("m_RightClickAction", "RightClick"), ("m_ScrollWheelAction", "ScrollWheel"), ("m_MoveAction", "Navigate"),
                     ("m_SubmitAction", "Submit"), ("m_CancelAction", "Cancel"),
                     ("m_TrackedDevicePositionAction", "TrackedDevicePosition"),
                     ("m_TrackedDeviceOrientationAction", "TrackedDeviceOrientation"),
                 })
            serialized.FindProperty(field).objectReferenceValue = references[action];
        serialized.ApplyModifiedProperties();
    }
}
#endif
