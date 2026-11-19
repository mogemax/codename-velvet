using System.Linq;
using CodenameVelvet.Core.Events;
using CodenameVelvet.DayNight;
using CodenameVelvet.Multiplayer;
using CodenameVelvet.Spawning;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// UN SOLO USO: arma el ciclo día/noche en Game_Map (evento, ciclo, luz, indicador y SpawnZones).
/// Se puede correr de nuevo sin duplicar nada. Borrar este archivo cuando el ciclo esté probado.
/// </summary>
public static class VelvetDayNightSetup
{
    private const string GameScenePath = "Assets/Scenes/Game_Map.unity";
    private const string EventPath = "Assets/ScriptableObjects/Events/DayNightEvent.asset";
    private const string CycleName = "DayNightCycle";
    private const string CanvasName = "MatchCanvas";
    private const string LabelName = "DayNightLabel";

    [MenuItem("Tools/Velvet/Day-Night/Setup in Game_Map")]
    private static void Setup()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        DayNightEvent channel = LoadOrCreateEvent();
        Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
        GameObject[] roots = scene.GetRootGameObjects();

        DayNightCycle cycle = SetupCycle(roots, channel);
        SetupLighting(cycle);
        SetupIndicator(roots, cycle);
        int zones = WireSpawnZones(channel);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[DayNightSetup] Listo: ciclo, luz, indicador y {zones} SpawnZones conectadas en {GameScenePath}.");
    }

    private static DayNightEvent LoadOrCreateEvent()
    {
        var channel = AssetDatabase.LoadAssetAtPath<DayNightEvent>(EventPath);
        if (channel != null) return channel;

        channel = ScriptableObject.CreateInstance<DayNightEvent>();
        AssetDatabase.CreateAsset(channel, EventPath);
        AssetDatabase.SaveAssets();
        return channel;
    }

    private static DayNightCycle SetupCycle(GameObject[] roots, DayNightEvent channel)
    {
        GameObject go = roots.FirstOrDefault(r => r.name == CycleName) ?? new GameObject(CycleName);
        DayNightCycle cycle = GetOrAdd<DayNightCycle>(go);

        var so = new SerializedObject(cycle);
        so.FindProperty("onPhaseChanged").objectReferenceValue = channel;
        so.FindProperty("rosterAsset").objectReferenceValue = FindRosterAsset();
        so.ApplyModifiedPropertiesWithoutUndo();
        return cycle;
    }

    /// <summary>El mismo roster que ya usa el PlayerSpawner de la escena.</summary>
    private static SessionRosterAsset FindRosterAsset()
    {
        PlayerSpawner spawner = Object.FindAnyObjectByType<PlayerSpawner>();
        if (spawner == null)
        {
            Debug.LogWarning("[DayNightSetup] No hay PlayerSpawner: la amenaza contará siempre 1 jugador.");
            return null;
        }
        return new SerializedObject(spawner).FindProperty("rosterAsset").objectReferenceValue as SessionRosterAsset;
    }

    private static void SetupLighting(DayNightCycle cycle)
    {
        Light sun = Object.FindObjectsByType<Light>(FindObjectsSortMode.None)
            .FirstOrDefault(l => l.type == LightType.Directional);
        if (sun == null)
            Debug.LogWarning("[DayNightSetup] No hay luz direccional: de noche solo bajará la luz ambiental.");

        DayNightLighting lighting = GetOrAdd<DayNightLighting>(cycle.gameObject);
        var so = new SerializedObject(lighting);
        so.FindProperty("cycle").objectReferenceValue = cycle;
        so.FindProperty("sun").objectReferenceValue = sun;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetupIndicator(GameObject[] roots, DayNightCycle cycle)
    {
        GameObject canvas = roots.FirstOrDefault(r => r.name == CanvasName);
        if (canvas == null)
        {
            Debug.LogError($"[DayNightSetup] No encontré '{CanvasName}' en la escena: el indicador no se creó.");
            return;
        }

        Transform existing = canvas.transform.Find(LabelName);
        GameObject labelGo = existing != null
            ? existing.gameObject
            : new GameObject(LabelName, typeof(RectTransform), typeof(TextMeshProUGUI));
        labelGo.transform.SetParent(canvas.transform, false);
        labelGo.transform.SetAsFirstSibling(); // los paneles de game over / victoria lo tapan

        var rect = labelGo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -12f);
        rect.sizeDelta = new Vector2(900f, 60f);

        var text = labelGo.GetComponent<TextMeshProUGUI>();
        text.fontSize = 36f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Top;
        text.raycastTarget = false;
        text.outlineWidth = 0.2f;
        text.text = "DÍA 1";

        DayNightIndicator indicator = GetOrAdd<DayNightIndicator>(labelGo);
        var so = new SerializedObject(indicator);
        so.FindProperty("cycle").objectReferenceValue = cycle;
        so.FindProperty("label").objectReferenceValue = text;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // TryGetComponent: en el Editor GetComponent devuelve un "null falso" que ?? no detecta.
    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        return go.TryGetComponent(out T existing) ? existing : go.AddComponent<T>();
    }

    private static int WireSpawnZones(DayNightEvent channel)
    {
        SpawnZone[] zones = Object.FindObjectsByType<SpawnZone>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (SpawnZone zone in zones)
        {
            var so = new SerializedObject(zone);
            so.FindProperty("dayNightEvent").objectReferenceValue = channel;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        return zones.Length;
    }
}
