#if UNITY_EDITOR
using CodenameVelvet.Cameras;
using CodenameVelvet.Gathering;
using CodenameVelvet.Player;
using CodenameVelvet.Spawning;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Sets up the bow. Safe to run again.
/// <list type="number">
/// <item>Art: ARCO.psd and FLECHA.psd imported as centred sprites, like the sword.</item>
/// <item>Prefabs: "Pickup_Bow" (a copy of the sword pickup with the bow drawing and <see cref="BowPickup"/>) and
/// "Arrow" (<see cref="Arrow"/>, depth-sorted, its drawing facing the camera).</item>
/// <item>Players: the base player prefab gets a <see cref="PlayerBow"/> (every character inherits it).</item>
/// <item>Open scene: its <see cref="WeaponScatter"/>, if any, also leaves the bows.</item>
/// </list>
/// </summary>
public static class VelvetBowSetup
{
    private const string Tag = "[VelvetBowSetup]";
    private const string BowArtPath = "Assets/Art/Items/ARCO.psd";
    private const string ArrowArtPath = "Assets/Art/Items/FLECHA.psd";
    private const string SwordPickupPath = "Assets/Prefabs/Gathering/Pickup_Sword.prefab";
    private const string BowPickupPath = "Assets/Prefabs/Gathering/Pickup_Bow.prefab";
    private const string ArrowPath = "Assets/Prefabs/Gathering/Arrow.prefab";
    private const string PlayerPath = "Assets/Assets/CHARACTERS/PLAYER M/PLAYER.prefab";
    private const string SpriteMaterialPath = "Assets/Materials/Velvet_Sprite.mat";
    private const float ArrowLength = 2.6f; // world units; the player is about 5 tall

    [MenuItem("Tools/Velvet/Weapons/Setup Bow (Open Scene)")]
    public static void Setup()
    {
        Sprite bow = ImportSprite(BowArtPath);
        Sprite arrow = ImportSprite(ArrowArtPath);
        if (bow == null || arrow == null)
        {
            Debug.LogError($"{Tag} Bow art missing ({BowArtPath}, {ArrowArtPath})");
            return;
        }

        BowPickup pickup = CreatePickup(bow);
        Arrow arrowPrefab = CreateArrow(arrow);
        SetupPlayer(bow, arrowPrefab);
        SetupScene(pickup);
        AssetDatabase.SaveAssets();
        Debug.Log($"{Tag} Done");
    }

    private static Sprite ImportSprite(string path)
    {
        if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            return null;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        bool ready = importer.textureType == TextureImporterType.Sprite
                     && importer.spriteImportMode == SpriteImportMode.Single
                     && settings.spriteAlignment == (int)SpriteAlignment.Center;
        if (!ready)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            importer.SetTextureSettings(settings);
            importer.spritePixelsPerUnit = 100f;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    /// <summary>The sword pickup with the bow drawing (upright art, laid diagonally like the sword).</summary>
    private static BowPickup CreatePickup(Sprite bow)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(BowPickupPath) == null)
            AssetDatabase.CopyAsset(SwordPickupPath, BowPickupPath);

        GameObject root = PrefabUtility.LoadPrefabContents(BowPickupPath);
        try
        {
            root.name = "Pickup_Bow";
            if (root.TryGetComponent(out SwordPickup sword))
                Object.DestroyImmediate(sword);
            if (!root.TryGetComponent(out BowPickup _))
                root.AddComponent<BowPickup>();

            SpriteRenderer drawing = root.GetComponentInChildren<SpriteRenderer>(true);
            drawing.sprite = bow;
            drawing.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            drawing.transform.localRotation = Quaternion.Euler(0f, 0f, -35f);
            drawing.transform.localScale = new Vector3(0.18f, 0.18f, 1f);

            PrefabUtility.SaveAsPrefabAsset(root, BowPickupPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        return AssetDatabase.LoadAssetAtPath<BowPickup>(BowPickupPath);
    }

    /// <summary>Root (Arrow, depth sorting) → Visual (faces the camera) → Sprite (turned by the arrow when shot).</summary>
    private static Arrow CreateArrow(Sprite art)
    {
        var root = new GameObject("Arrow");
        try
        {
            root.AddComponent<SortingGroup>();
            root.AddComponent<DynamicSortingOrder>();
            Arrow arrow = root.AddComponent<Arrow>();

            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            visual.AddComponent<ViewTilt>();

            var sprite = new GameObject("Sprite");
            sprite.transform.SetParent(visual.transform, false);
            sprite.transform.localScale = Vector3.one * (ArrowLength / art.bounds.size.x);
            SpriteRenderer renderer = sprite.AddComponent<SpriteRenderer>();
            renderer.sprite = art;
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(SpriteMaterialPath);
            renderer.shadowCastingMode = ShadowCastingMode.Off;

            var serialized = new SerializedObject(arrow);
            serialized.FindProperty("drawing").objectReferenceValue = sprite.transform;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, ArrowPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }

        return AssetDatabase.LoadAssetAtPath<Arrow>(ArrowPath);
    }

    private static void SetupPlayer(Sprite bow, Arrow arrow)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPath);
        try
        {
            if (!root.TryGetComponent(out PlayerBow playerBow))
                playerBow = root.AddComponent<PlayerBow>();

            var serialized = new SerializedObject(playerBow);
            serialized.FindProperty("arrowPrefab").objectReferenceValue = arrow;
            serialized.FindProperty("bowSprite").objectReferenceValue = bow;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void SetupScene(BowPickup pickup)
    {
        WeaponScatter scatter = Object.FindFirstObjectByType<WeaponScatter>();
        if (scatter == null)
        {
            Debug.Log($"{Tag} No WeaponScatter in the open scene: bows not placed here");
            return;
        }

        var serialized = new SerializedObject(scatter);
        serialized.FindProperty("bowPrefab").objectReferenceValue = pickup;
        serialized.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(scatter.gameObject.scene);
        EditorSceneManager.SaveScene(scatter.gameObject.scene);
    }
}
#endif
