using System.IO;
using System.Linq;
using CodenameVelvet.Characters;
using CodenameVelvet.Multiplayer;
using CodenameVelvet.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// TEMPORARY one-shot setup for plan 02-04 (lobby + character select).
/// Tools → Velvet → Build Lobby Setup. Idempotent; delete this file after the play test.
/// </summary>
public static class DevLobbyBuilder
{
    private const string CharactersFolder = "Assets/ScriptableObjects/Characters";
    private const string MultiplayerFolder = "Assets/ScriptableObjects/Multiplayer";
    private const string CatalogPath = CharactersFolder + "/CharacterCatalog.asset";
    private const string RosterPath = MultiplayerFolder + "/SessionRoster.asset";
    private const string LobbyPlayerPath = "Assets/Prefabs/Player/LobbyPlayer.prefab";
    private const string PlayerActionsPath = "Assets/InputSystem_Actions.inputactions";
    private const string LobbyControlsPath = "Assets/LobbyControls.inputactions";
    private const string ScenePath = "Assets/Scenes/CharacterMenu.unity";
    private const string FrameSpritesFolder = "Assets/UI/CharacterMenu";
    private const string StartScene = "Dev_Enemies";

    private static readonly string[] AnimationPaths =
    {
        "Assets/UI/UIAnimations/CharacterMenuIntro.anim",
        "Assets/UI/UIAnimations/CharacterMenuOutro.anim",
    };

    private static readonly string[] RequiredBuildScenes =
    {
        "Assets/Scenes/MainMenu.unity",
        ScenePath,
        "Assets/Scenes/Dev/Dev_Enemies.unity",
    };

    private static readonly (string name, Color color)[] PlaceholderCharacters =
    {
        ("Personaje 1", new Color(0.86f, 0.30f, 0.30f)),
        ("Personaje 2", new Color(0.30f, 0.55f, 0.90f)),
        ("Personaje 3", new Color(0.35f, 0.78f, 0.40f)),
        ("Personaje 4", new Color(0.95f, 0.78f, 0.25f)),
    };

    private static readonly float[] FrameX = { -562.5f, -187.5f, 187.5f, 562.5f };

    [MenuItem("Tools/Velvet/Build Lobby Setup")]
    public static void Build()
    {
        CreateCharacters();
        CreateRosterAsset();
        CreateLobbyPlayerPrefab();
        EnsureBuildScenes();
        DropFifthFrameFromAnimations();

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            Debug.LogWarning("[DevLobbyBuilder] Scene step skipped (save cancelled). Assets were created; click again.");
            return;
        }

        BuildScene();
        Debug.Log("[DevLobbyBuilder] Done. Play CharacterMenu to test, then delete DevLobbyBuilder.cs.");
    }

    // ─────────────────────── Assets ───────────────────────

    private static void CreateCharacters()
    {
        EnsureFolder(CharactersFolder);
        if (AssetDatabase.LoadAssetAtPath<CharacterCatalog>(CatalogPath) != null)
            return;

        var definitions = new CharacterDefinition[PlaceholderCharacters.Length];
        for (int i = 0; i < PlaceholderCharacters.Length; i++)
        {
            var definition = ScriptableObject.CreateInstance<CharacterDefinition>();
            var so = new SerializedObject(definition);
            so.FindProperty("displayName").stringValue = PlaceholderCharacters[i].name;
            so.FindProperty("color").colorValue = PlaceholderCharacters[i].color;
            so.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.CreateAsset(definition, $"{CharactersFolder}/Personaje{i + 1}.asset");
            definitions[i] = definition;
        }

        var catalog = ScriptableObject.CreateInstance<CharacterCatalog>();
        var catalogSo = new SerializedObject(catalog);
        SetObjectArray(catalogSo.FindProperty("characters"), definitions);
        catalogSo.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.CreateAsset(catalog, CatalogPath);
        AssetDatabase.SaveAssets();
    }

    private static void CreateRosterAsset()
    {
        EnsureFolder(MultiplayerFolder);
        if (AssetDatabase.LoadAssetAtPath<SessionRosterAsset>(RosterPath) != null)
            return;

        AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<SessionRosterAsset>(), RosterPath);
        AssetDatabase.SaveAssets();
    }

    private static void CreateLobbyPlayerPrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(LobbyPlayerPath) != null)
            return;

        var go = new GameObject("LobbyPlayer");
        var input = go.AddComponent<PlayerInput>();
        input.actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(PlayerActionsPath);
        input.defaultActionMap = "Lobby";
        input.notificationBehavior = PlayerNotifications.SendMessages;
        go.AddComponent<LobbyPlayer>();

        PrefabUtility.SaveAsPrefabAsset(go, LobbyPlayerPath);
        Object.DestroyImmediate(go);
    }

    private static void EnsureBuildScenes()
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        foreach (string path in RequiredBuildScenes)
        {
            if (scenes.All(scene => scene.path != path))
                scenes.Add(new EditorBuildSettingsScene(path, true));
        }

        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static void DropFifthFrameFromAnimations()
    {
        foreach (string path in AnimationPaths)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
                continue;

            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip).Where(b => b.path == "ChFrame5"))
                AnimationUtility.SetEditorCurve(clip, binding, null);

            EditorUtility.SetDirty(clip);
        }

        AssetDatabase.SaveAssets();
    }

    // ─────────────────────── Scene ───────────────────────

    private static void BuildScene()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        // Load assets after OpenScene: Single mode unloads anything loaded before it.
        var catalog = AssetDatabase.LoadAssetAtPath<CharacterCatalog>(CatalogPath);
        var roster = AssetDatabase.LoadAssetAtPath<SessionRosterAsset>(RosterPath);
        var lobbyPlayer = AssetDatabase.LoadAssetAtPath<GameObject>(LobbyPlayerPath);
        var references = AssetDatabase.LoadAllAssetsAtPath(LobbyControlsPath).OfType<InputActionReference>().ToArray();
        InputActionReference join = references.First(r => r.action.name == "Join");
        InputActionReference back = references.First(r => r.action.name == "Back");

        var canvas = GameObject.Find("Canvas");
        var menuGestor = GameObject.Find("MenuGestor").GetComponent<CambioEscenaAnimado>();
        var backButton = GameObject.Find("BackButton").GetComponent<Button>();

        var fifth = GameObject.Find("ChFrame5");
        if (fifth != null)
            Object.DestroyImmediate(fifth);

        var views = new LobbySlotView[FrameX.Length];
        for (int i = 0; i < FrameX.Length; i++)
            views[i] = BuildFrame(GameObject.Find($"ChFrame{i + 1}"), FrameX[i]);

        var startTransition = GetOrAdd<CambioEscenaAnimado>(FindOrCreate("StartGestor"));
        var startSo = new SerializedObject(startTransition);
        startSo.FindProperty("AnimatorMenu").objectReferenceValue = canvas.GetComponent<Animator>();
        startSo.FindProperty("tiempoDeAnimacion").floatValue = 0.5f;
        startSo.FindProperty("nombreSiguienteEscena").stringValue = StartScene;
        startSo.ApplyModifiedPropertiesWithoutUndo();

        GameObject managerGo = FindOrCreate("LobbyManager");
        var inputManager = GetOrAdd<PlayerInputManager>(managerGo);
        var inputSo = new SerializedObject(inputManager);
        inputSo.FindProperty("m_NotificationBehavior").enumValueIndex = (int)PlayerNotifications.InvokeCSharpEvents;
        inputSo.FindProperty("m_JoinBehavior").enumValueIndex = (int)PlayerJoinBehavior.JoinPlayersWhenJoinActionIsTriggered;
        inputSo.FindProperty("m_MaxPlayerCount").intValue = SessionRoster.DefaultMaxPlayers;
        inputSo.FindProperty("m_PlayerPrefab").objectReferenceValue = lobbyPlayer;
        inputSo.FindProperty("m_JoinAction.m_UseReference").boolValue = true;
        inputSo.FindProperty("m_JoinAction.m_Reference").objectReferenceValue = join;
        inputSo.ApplyModifiedPropertiesWithoutUndo();

        var manager = GetOrAdd<LobbyManager>(managerGo);
        var managerSo = new SerializedObject(manager);
        managerSo.FindProperty("rosterAsset").objectReferenceValue = roster;
        managerSo.FindProperty("catalog").objectReferenceValue = catalog;
        managerSo.FindProperty("playerInputManager").objectReferenceValue = inputManager;
        managerSo.FindProperty("backAction").objectReferenceValue = back;
        SetObjectArray(managerSo.FindProperty("slotViews"), views);
        managerSo.FindProperty("startTransition").objectReferenceValue = startTransition;
        managerSo.FindProperty("backTransition").objectReferenceValue = menuGestor;
        managerSo.ApplyModifiedPropertiesWithoutUndo();

        // The mouse Back goes through the lobby so it clears the roster and blocks late joins.
        while (backButton.onClick.GetPersistentEventCount() > 0)
            UnityEventTools.RemovePersistentListener(backButton.onClick, 0);
        UnityEventTools.AddPersistentListener(backButton.onClick, manager.GoBack);
        // Gamepads must never reach the button through the shared EventSystem.
        backButton.navigation = new Navigation { mode = Navigation.Mode.None };
        EditorUtility.SetDirty(backButton);

        EditorSceneManager.MarkSceneDirty(canvas.scene);
        EditorSceneManager.SaveScene(canvas.scene);
    }

    private static LobbySlotView BuildFrame(GameObject frameGo, float x)
    {
        var rect = frameGo.GetComponent<RectTransform>();
        rect.anchoredPosition = new Vector2(x, rect.anchoredPosition.y);

        // The frame is drawn by the lobby, not by mouse hover/selection.
        var button = frameGo.GetComponent<Button>();
        if (button != null)
            Object.DestroyImmediate(button);

        var view = frameGo.GetComponent<LobbySlotView>();
        if (view != null)
            return view;

        view = frameGo.AddComponent<LobbySlotView>();

        var portrait = CreateChild<Image>(frameGo.transform, "Portrait", new Vector2(0f, 25f), new Vector2(150f, 150f));
        portrait.raycastTarget = false;
        TMP_Text label = CreateText(frameGo.transform, "PlayerLabel", new Vector2(0f, 135f), new Vector2(300f, 40f), 30f);
        TMP_Text characterName = CreateText(frameGo.transform, "CharacterName", new Vector2(0f, -70f), new Vector2(300f, 36f), 28f);
        TMP_Text status = CreateText(frameGo.transform, "Status", new Vector2(0f, -125f), new Vector2(300f, 60f), 20f);

        var arrows = new GameObject("Arrows", typeof(RectTransform));
        arrows.transform.SetParent(frameGo.transform, false);
        CreateText(arrows.transform, "Left", new Vector2(-115f, 25f), new Vector2(50f, 80f), 64f).text = "<";
        CreateText(arrows.transform, "Right", new Vector2(115f, 25f), new Vector2(50f, 80f), 64f).text = ">";

        var so = new SerializedObject(view);
        so.FindProperty("frame").objectReferenceValue = frameGo.GetComponent<Image>();
        so.FindProperty("freeSprite").objectReferenceValue = LoadSprite("FrameDefault");
        so.FindProperty("choosingSprite").objectReferenceValue = LoadSprite("FrameHover");
        so.FindProperty("lockedSprite").objectReferenceValue = LoadSprite("FrameSelected");
        so.FindProperty("portrait").objectReferenceValue = portrait;
        so.FindProperty("playerLabel").objectReferenceValue = label;
        so.FindProperty("characterName").objectReferenceValue = characterName;
        so.FindProperty("arrows").objectReferenceValue = arrows;
        so.FindProperty("status").objectReferenceValue = status;
        so.ApplyModifiedPropertiesWithoutUndo();

        return view;
    }

    // ─────────────────────── Helpers ───────────────────────

    private static T CreateChild<T>(Transform parent, string name, Vector2 position, Vector2 size) where T : Component
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return go.AddComponent<T>();
    }

    private static TMP_Text CreateText(Transform parent, string name, Vector2 position, Vector2 size, float fontSize)
    {
        var text = CreateChild<TextMeshProUGUI>(parent, name, position, size);
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    private static GameObject FindOrCreate(string name)
    {
        GameObject existing = GameObject.Find(name);
        return existing != null ? existing : new GameObject(name);
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component =>
        go.TryGetComponent(out T component) ? component : go.AddComponent<T>();

    private static Sprite LoadSprite(string fileName) =>
        AssetDatabase.LoadAssetAtPath<Sprite>($"{FrameSpritesFolder}/{fileName}.png");

    private static void SetObjectArray(SerializedProperty array, Object[] values)
    {
        array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        EnsureFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));
        AssetDatabase.CreateFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'), Path.GetFileName(path));
    }
}
