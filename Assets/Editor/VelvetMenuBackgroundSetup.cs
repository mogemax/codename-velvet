#if UNITY_EDITOR
using System;
using System.Collections.Generic;
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
/// back and front waves (the front ones hide its hull), and the hidden parts of its <see cref="Shipwreck"/> (giant
/// wave behind the boat, broken boat, splash, and the boat's pieces in front of everything; Shipwreck/Pieces.json
/// says where each piece was in the boat).</item>
/// <item>Puts it in the menu scenes that show it.</item>
/// </list>
/// Layout in the units of a 1920x1080 canvas. Opens and saves the scenes it changes; save your own changes first.
/// </summary>
public static class VelvetMenuBackgroundSetup
{
    private const string Tag = "[VelvetMenuBackgroundSetup]";
    private const string ArtFolder = "Assets/Art/UI/MenuBackground";
    private const string WreckFolder = ArtFolder + "/Shipwreck";
    private const string PrefabPath = "Assets/Prefabs/UI/MenuBackground.prefab";
    private const float WaterLine = 0.32f;  // of the screen's height
    private const float LayerHeight = 760f; // the sea textures' height
    private const float BoatWidth = 520f;
    private const float BoatWaterline = 0.26f; // of the boat's height, from its bottom
    private const float BrokenWaterline = 0.32f; // sits lower in the water than the sound boat
    private const float RestX = 0.6f;
    private const float PlaneDistance = 900f;  // menu canvases sit at 100, cameras see to 1000
    private const int SortingOrder = -100;

    private static readonly string[] Scenes =
    {
        "Assets/Scenes/MainMenu.unity",
        "Assets/Scenes/SettingsMenu.unity",
        "Assets/Scenes/CharacterMenu.unity",
    };

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
        Sprite boat = ImportSprite($"{ArtFolder}/MenuBoat.png");
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

    private static Sprite ImportSprite(string path)
    {
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

            var layers = new List<ScrollingWaveLayer>();
            foreach (var layer in BackLayers)
                layers.Add(WaveLayer(sea, layer));
            RectTransform giantWave = WreckPicture("GiantWave", sea, size: new Vector2(1800f, 1300f), pivot: new Vector2(330f / 1800f, 1f - 820f / 1300f)); // the lip's tip / the water
            RectTransform boatRect = Boat(sea, boat);
            RectTransform broken = WreckPicture("BrokenBoat", sea, width: BoatWidth, pivot: new Vector2(0.5f, BrokenWaterline));
            RectTransform splash = WreckPicture("Splash", sea, size: new Vector2(900f, 700f), pivot: new Vector2(0.5f, 0.12f));
            layers.Add(WaveLayer(sea, FrontLayer));
            Shipwreck.Piece[] pieces = Pieces(sea);
            if (layers.Contains(null) || giantWave == null || broken == null || splash == null || pieces == null)
                return null;

            var wreck = new SerializedObject(root.AddComponent<Shipwreck>());
            wreck.FindProperty("sea").objectReferenceValue = sea;
            wreck.FindProperty("boat").objectReferenceValue = boatRect;
            wreck.FindProperty("sailing").objectReferenceValue = boatRect.GetComponent<SailingBoat>();
            wreck.FindProperty("brokenBoat").objectReferenceValue = broken;
            wreck.FindProperty("giantWave").objectReferenceValue = giantWave;
            wreck.FindProperty("splash").objectReferenceValue = splash;
            SerializedProperty layerArray = wreck.FindProperty("layers");
            layerArray.arraySize = layers.Count;
            for (int i = 0; i < layers.Count; i++) layerArray.GetArrayElementAtIndex(i).objectReferenceValue = layers[i];
            SerializedProperty pieceArray = wreck.FindProperty("pieces");
            pieceArray.arraySize = pieces.Length;
            for (int i = 0; i < pieces.Length; i++)
            {
                SerializedProperty item = pieceArray.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("rect").objectReferenceValue = pieces[i].rect;
                item.FindPropertyRelative("offset").vector2Value = pieces[i].offset;
                item.FindPropertyRelative("size").vector2Value = pieces[i].size;
                item.FindPropertyRelative("floats").boolValue = pieces[i].floats;
            }
            wreck.ApplyModifiedPropertiesWithoutUndo();

            return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>A hidden picture of the shipwreck, its size given or <paramref name="width"/> wide.</summary>
    private static RectTransform WreckPicture(string name, Transform parent, Vector2 pivot, Vector2 size = default, float width = 0f)
    {
        Sprite sprite = ImportSprite($"{WreckFolder}/{name}.png");
        if (sprite == null)
        {
            Debug.LogError($"{Tag} Missing {WreckFolder}/{name}.png");
            return null;
        }

        if (width > 0f) size = new Vector2(width, width * sprite.rect.height / sprite.rect.width);
        RectTransform rect = Sprite(name, parent, sprite, size);
        rect.pivot = pivot;
        rect.gameObject.SetActive(false);
        return rect;
    }

    [Serializable] private class PieceData { public string name; public float cx, cy, w, h; public bool floats; }
    [Serializable] private class PieceList { public PieceData[] pieces; }

    private static Shipwreck.Piece[] Pieces(Transform sea)
    {
        var json = AssetDatabase.LoadAssetAtPath<TextAsset>($"{WreckFolder}/Pieces.json");
        if (json == null)
        {
            Debug.LogError($"{Tag} Missing {WreckFolder}/Pieces.json");
            return null;
        }

        var holder = new GameObject("Pieces", typeof(RectTransform)).GetComponent<RectTransform>();
        holder.SetParent(sea, false);
        holder.sizeDelta = Vector2.zero;
        var pieces = new List<Shipwreck.Piece>();
        foreach (PieceData data in JsonUtility.FromJson<PieceList>(json.text).pieces)
        {
            Sprite sprite = ImportSprite($"{WreckFolder}/{data.name}.png");
            if (sprite == null)
            {
                Debug.LogError($"{Tag} Missing {WreckFolder}/{data.name}.png");
                return null;
            }

            var size = new Vector2(data.w, data.h);
            RectTransform rect = Sprite(data.name, holder, sprite, size * BoatWidth);
            rect.gameObject.SetActive(false);
            pieces.Add(new Shipwreck.Piece { rect = rect, offset = new Vector2(data.cx, data.cy), size = size, floats = data.floats });
        }
        return pieces.ToArray();
    }

    private static RectTransform Sprite(string name, Transform parent, Sprite sprite, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        return rect;
    }

    private static ScrollingWaveLayer WaveLayer(RectTransform sea, (string texture, float surface, float y, float speed, float bob, float phase) layer)
    {
        Texture2D texture = ImportTexture(layer.texture, wrap: true);
        if (texture == null)
        {
            Debug.LogError($"{Tag} Missing {ArtFolder}/{layer.texture}.png");
            return null;
        }

        RawImage image = Picture(layer.texture.Replace("Menu", ""), sea, texture);
        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(0.5f, 1f - layer.surface / LayerHeight); // the pivot is on the water surface
        rect.sizeDelta = new Vector2(0f, LayerHeight);
        rect.anchoredPosition = new Vector2(0f, layer.y);

        var component = image.gameObject.AddComponent<ScrollingWaveLayer>();
        var scroll = new SerializedObject(component);
        scroll.FindProperty("speed").floatValue = layer.speed;
        scroll.FindProperty("bobHeight").floatValue = layer.bob;
        scroll.FindProperty("phase").floatValue = layer.phase;
        scroll.ApplyModifiedPropertiesWithoutUndo();
        return component;
    }

    private static RectTransform Boat(RectTransform sea, Sprite sprite)
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
        return rect;
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
        MenuBackground existing = UnityEngine.Object.FindFirstObjectByType<MenuBackground>(FindObjectsInactive.Include);
        if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.transform.SetAsFirstSibling();
        EditorSceneManager.MarkSceneDirty(instance.scene);
    }
}
#endif
