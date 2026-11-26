#if UNITY_EDITOR
using CodenameVelvet.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// The credits scene: the loading screen's art without the fox (a still), "CREDITS" and the team in alphabetical
/// order, in the game's font and the borders' blue. B / Esc (or A on the selected back button) returns to the main
/// menu. Safe to run again. Opens and saves the Credits scene; save your own changes first.
/// </summary>
public static class VelvetCreditsSetup
{
    private const string Tag = "[VelvetCreditsSetup]";
    private const string ScenePath = "Assets/Scenes/Credits.unity";
    private const string BackgroundPath = "Assets/Art/UI/Credits/CreditsBackground.png";
    private const string FontPath = "Assets/TextMesh Pro/Fonts/gelio.retsina SDF.asset";
    private const string ActionsPath = "Assets/InputSystem_Actions.inputactions";
    private const string CanvasName = "CreditsMenu";
    private const string BackButtonName = "BackButton";

    private static readonly Color Ink = new Color32(35, 48, 85, 255); // the borders' blue

    private static readonly string[] Team =
    {
        "Emilio Velazquez Puentes",
        "Esteban Montes Gomez",
        "Jesus Roberto Rivera Cuellar",
        "Mauricio Garza Melo",
        "Maximo Samar Sanchez Antonio",
    };

    [MenuItem("Tools/Velvet/Menus/Setup Credits")]
    public static void Setup()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        Sprite background = ImportBackground();
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (background == null || font == null)
        {
            Debug.LogError($"{Tag} Missing {BackgroundPath} or {FontPath}");
            return;
        }

        string current = EditorSceneManager.GetActiveScene().path;
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject canvas = GameObject.Find(CanvasName);
        GameObject back = GameObject.Find(BackButtonName);
        if (canvas == null || back == null)
        {
            Debug.LogError($"{Tag} No {CanvasName} or {BackButtonName} in {ScenePath}");
            return;
        }

        Background(canvas.transform, background);
        string names = string.Join("\n", Team);
        Text(canvas.transform, "Title", "CREDITS", font, new Vector2(0.15f, 0.68f), new Vector2(0.85f, 0.82f), 96f);
        Text(canvas.transform, "Names", names, font, new Vector2(0.15f, 0.2f), new Vector2(0.85f, 0.64f), 56f);
        back.transform.SetAsLastSibling(); // over the background and the text

        BackWithGamepad(back);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (!string.IsNullOrEmpty(current) && current != ScenePath) EditorSceneManager.OpenScene(current, OpenSceneMode.Single);
        Debug.Log($"{Tag} Done");
    }

    private static Sprite ImportBackground()
    {
        if (AssetImporter.GetAtPath(BackgroundPath) is not TextureImporter importer)
            return null;
        if (importer.textureType != TextureImporterType.Sprite || importer.mipmapEnabled)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundPath);
    }

    private static void Background(Transform canvas, Sprite sprite)
    {
        var image = Get<Image>(Child(canvas, "Background"));
        image.sprite = sprite;
        image.color = Color.white;
        image.raycastTarget = false;
        Stretch(image.rectTransform, Vector2.zero, Vector2.one);
        image.transform.SetAsFirstSibling();
    }

    private static void Text(Transform canvas, string name, string content, TMP_FontAsset font, Vector2 min, Vector2 max, float maxSize)
    {
        GameObject go = Child(canvas, name);
        var text = Get<TextMeshProUGUI>(go);
        text.text = content;
        text.font = font;
        text.color = Ink;
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true; // the canvas keeps pixel sizes: fit the box at any resolution
        text.fontSizeMin = 12f;
        text.fontSizeMax = maxSize;
        text.lineSpacing = 20f;
        text.raycastTarget = false;
        Stretch(text.rectTransform, min, max);
    }

    // Not "GetComponent ?? AddComponent": in the Editor a missing component comes back as a fake null object.
    private static T Get<T>(GameObject go) where T : Component =>
        go.TryGetComponent(out T existing) ? existing : go.AddComponent<T>();

    private static GameObject Child(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null) return existing.gameObject;
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    /// <summary>The back button starts selected (A presses it) and the UI's Cancel (B / Esc) presses it too.</summary>
    private static void BackWithGamepad(GameObject back)
    {
        InputActionReference reference = null;
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(ActionsPath))
            if (asset is InputActionReference r && r.action != null && r.action.actionMap.name == "UI" && r.action.name == "Cancel")
                reference = r;
        if (reference == null)
            Debug.LogError($"{Tag} No UI/Cancel action in {ActionsPath}");

        var onCancel = Get<BackOnCancel>(back);
        var serialized = new SerializedObject(onCancel);
        serialized.FindProperty("cancelAction").objectReferenceValue = reference;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EventSystem events = Object.FindFirstObjectByType<EventSystem>();
        if (events != null)
        {
            var system = new SerializedObject(events);
            system.FindProperty("m_FirstSelected").objectReferenceValue = back;
            system.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
