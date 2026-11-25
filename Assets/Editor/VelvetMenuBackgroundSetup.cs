#if UNITY_EDITOR
using CodenameVelvet.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The animated sea behind the menus (<see cref="MenuBackground"/>). Safe to run again.
/// <list type="number">
/// <item>Imports the art in Assets/Art/UI/MenuBackground (the sea layers wrap sideways; the boat is a sprite).</item>
/// <item>Builds Assets/Prefabs/UI/MenuBackground.prefab: sky, then the sea from far to near, the boat between the
/// back and front waves (the front ones hide its hull).</item>
/// <item>Puts it in the menu scenes that show it.</item>
/// </list>
/// Layout in the units of a 1920x1080 canvas. Opens and saves the scenes it changes; save your own changes first.
/// </summary>
public static class VelvetMenuBackgroundSetup
{
    private const string Tag = "[VelvetMenuBackgroundSetup]";
    private const string ArtFolder = "Assets/Art/UI/MenuBackground";
    private const string PrefabPath = "Assets/Prefabs/UI/MenuBackground.prefab";
    private const float WaterLine = 0.32f;  // of the screen's height
    private const float LayerHeight = 760f; // the sea textures' height
    private const float BoatWidth = 520f;
    private const float BoatWaterline = 0.26f; // of the boat's height, from its bottom
    private const float RestX = 0.6f;
    private const float PlaneDistance = 900f;  // menu canvases sit at 100, cameras see to 1000
    private const int SortingOrder = -100;

    private static readonly string[] Scenes = { "Assets/Scenes/MainMenu.unity", "Assets/Scenes/CharacterMenu.unity" };

    // Texture, its water surface (pixels from its top), height over the water line, speed, bob height, phase.
    private static readonly (string texture, float surface, float y, float speed, float bob, float phase)[] BackLayers =
    {
        ("MenuSeaFar", 60f, 140f, 25f, 2f, 0.15f),
        ("MenuWavesBack", 110f, 20f, 80f, 7f, 0f),
    };
    private static readonly (string texture, float surface, float y, float speed, float bob, float phase) FrontLayer =
        ("MenuWavesFront", 130f, -20f, 140f, 8f, 0.4f);

    [MenuItem("Tools/Velvet/Menus/Setup Menu Background")]
    public static void Setup()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        Texture2D sky = ImportTexture("MenuSky", wrap: false);
        Sprite boat = ImportBoat();
        if (sky == null || boat == null)
        {
            Debug.LogError($"{Tag} Missing art in {ArtFolder}");
            return;
        }

        GameObject prefab = BuildPrefab(sky, boat);
        if (prefab == null) return;

        string current = EditorSceneManager.GetActiveScene().path;
        foreach (string path in Scenes)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            Place(prefab);
            EditorSceneManager.SaveScene(scene);
        }

        if (!string.IsNullOrEmpty(current)) EditorSceneManager.OpenScene(current, OpenSceneMode.Single);
        Debug.Log($"{Tag} Done");
    }

    private static Texture2D ImportTexture(string name, bool wrap)
    {
        string path = $"{ArtFolder}/{name}.png";
        if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            return null;

        importer.textureType = TextureImporterType.Default;
        importer.wrapModeU = wrap ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        importer.wrapModeV = TextureWrapMode.Clamp;
        ApplyCommon(importer);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static Sprite ImportBoat()
    {
        string path = $"{ArtFolder}/MenuBoat.png";
        if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            return null;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.wrapMode = TextureWrapMode.Clamp;
        ApplyCommon(importer);
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static void ApplyCommon(TextureImporter importer)
    {
        importer.mipmapEnabled = false; // always drawn at screen size
        importer.alphaIsTransparency = true;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
    }

    private static GameObject BuildPrefab(Texture2D sky, Sprite boat)
    {
        var root = new GameObject("MenuBackground", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(MenuBackground));
        try
        {
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; // its camera is the scene's, set when it runs
            canvas.planeDistance = PlaneDistance;
            canvas.sortingOrder = SortingOrder;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            // No GraphicRaycaster: never takes the menu's clicks.

            RawImage skyImage = Picture("Sky", root.transform, sky);
            skyImage.rectTransform.anchorMin = Vector2.zero;
            skyImage.rectTransform.anchorMax = Vector2.one;
            skyImage.rectTransform.offsetMin = skyImage.rectTransform.offsetMax = Vector2.zero;

            var sea = new GameObject("Sea", typeof(RectTransform)).GetComponent<RectTransform>();
            sea.SetParent(root.transform, false);
            sea.anchorMin = new Vector2(0f, WaterLine);
            sea.anchorMax = new Vector2(1f, WaterLine);
            sea.sizeDelta = Vector2.zero;

            foreach (var layer in BackLayers)
                if (!WaveLayer(sea, layer)) return null;
            Boat(sea, boat);
            if (!WaveLayer(sea, FrontLayer)) return null;

            return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static bool WaveLayer(RectTransform sea, (string texture, float surface, float y, float speed, float bob, float phase) layer)
    {
        Texture2D texture = ImportTexture(layer.texture, wrap: true);
        if (texture == null)
        {
            Debug.LogError($"{Tag} Missing {ArtFolder}/{layer.texture}.png");
            return false;
        }

        RawImage image = Picture(layer.texture.Replace("Menu", ""), sea, texture);
        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(0.5f, 1f - layer.surface / LayerHeight); // the pivot is on the water surface
        rect.sizeDelta = new Vector2(0f, LayerHeight);
        rect.anchoredPosition = new Vector2(0f, layer.y);

        var scroll = new SerializedObject(image.gameObject.AddComponent<ScrollingWaveLayer>());
        scroll.FindProperty("speed").floatValue = layer.speed;
        scroll.FindProperty("bobHeight").floatValue = layer.bob;
        scroll.FindProperty("phase").floatValue = layer.phase;
        scroll.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }

    private static void Boat(RectTransform sea, Sprite sprite)
    {
        var boat = new GameObject("Boat", typeof(RectTransform), typeof(Image), typeof(SailingBoat));
        var rect = (RectTransform)boat.transform;
        rect.SetParent(sea, false);
        rect.anchorMin = rect.anchorMax = new Vector2(RestX, 0.5f);
        rect.pivot = new Vector2(0.5f, BoatWaterline);
        rect.sizeDelta = new Vector2(BoatWidth, BoatWidth * sprite.rect.height / sprite.rect.width);
        var image = boat.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
    }

    private static RawImage Picture(string name, Transform parent, Texture texture)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<RawImage>();
        image.texture = texture;
        image.raycastTarget = false;
        return image;
    }

    private static void Place(GameObject prefab)
    {
        MenuBackground existing = Object.FindFirstObjectByType<MenuBackground>(FindObjectsInactive.Include);
        if (existing != null) Object.DestroyImmediate(existing.gameObject);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.transform.SetAsFirstSibling();
        EditorSceneManager.MarkSceneDirty(instance.scene);
    }
}
#endif
