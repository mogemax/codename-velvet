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
/// <item>Art: ARCO.psd imported by layers (grip "AGARRE", limbs "TOP"/"BOT", string "Capa 5"); FLECHA.psd as a
/// centred sprite.</item>
/// <item>"Bow_Held": the bow rebuilt from its pieces as a <see cref="HeldBowView"/> (limbs on hinges at the grip's
/// ends, the string as a line, an arrow resting on it), its origin on the grip.</item>
/// <item>"Pickup_Bow": a copy of the sword pickup holding a Bow_Held, with <see cref="BowPickup"/>.
/// "Arrow": <see cref="Arrow"/>, depth-sorted, its drawing facing the camera.</item>
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
    private const string HeldBowPath = "Assets/Prefabs/Gathering/Bow_Held.prefab";
    private const string ArrowPath = "Assets/Prefabs/Gathering/Arrow.prefab";
    private const string StringMaterialPath = "Assets/Materials/BowString.mat";
    private const string PlayerPath = "Assets/Assets/CHARACTERS/PLAYER M/PLAYER.prefab";
    private const string SpriteMaterialPath = "Assets/Materials/Velvet_Sprite.mat";
    private const float ArrowLength = 2.6f;    // flying arrow, world units; the player is about 5 tall
    private const float PickupScale = 0.27f;   // the bow on the ground (1.5 × the first version)
    private const float StringWidth = 0.12f;   // bow units (the bow is about 15 tall)
    private static readonly Color StringColor = new Color(0.16f, 0.1f, 0.07f);

    [MenuItem("Tools/Velvet/Weapons/Setup Bow (Open Scene)")]
    public static void Setup()
    {
        GameObject layers = ImportLayers(BowArtPath);
        Sprite arrow = ImportSprite(ArrowArtPath);
        if (layers == null || arrow == null)
        {
            Debug.LogError($"{Tag} Bow art missing ({BowArtPath}, {ArrowArtPath})");
            return;
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(SpriteMaterialPath);
        HeldBowView held = CreateHeldBow(layers, arrow, material);
        if (held == null) return;
        BowPickup pickup = CreatePickup(held);
        Arrow arrowPrefab = CreateArrow(arrow, material);
        SetupPlayer(held, arrowPrefab);
        SetupScene(pickup);
        AssetDatabase.SaveAssets();
        Debug.Log($"{Tag} Done");
    }

    // ---- Art -----------------------------------------------------------------------------------------------------

    /// <summary>The PSD by layers: one sprite per visible layer and a GameObject per layer, in place.</summary>
    private static GameObject ImportLayers(string path)
    {
        if (AssetDatabase.GetImporterOverride(path) != typeof(UnityEditor.U2D.PSD.PSDImporter))
            AssetDatabase.SetImporterOverride<UnityEditor.U2D.PSD.PSDImporter>(path);

        if (AssetImporter.GetAtPath(path) is not UnityEditor.U2D.PSD.PSDImporter importer)
            return null;

        var serialized = new SerializedObject(importer);
        SerializedProperty hierarchy = serialized.FindProperty("m_GenerateGOHierarchy");
        if (!importer.useMosaicMode || !importer.useCharacterMode || !hierarchy.boolValue)
        {
            hierarchy.boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            importer.useMosaicMode = true;
            importer.useCharacterMode = true; // the mode that builds the GameObjects (no bones needed)
            importer.spritePixelsPerUnit = 100f;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
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

    // ---- Bow in pieces -------------------------------------------------------------------------------------------

    private static HeldBowView CreateHeldBow(GameObject layers, Sprite arrowSprite, Material material)
    {
        GameObject source = Object.Instantiate(layers);
        var root = new GameObject("Bow_Held");
        try
        {
            SpriteRenderer grip = Part(source, "AGARRE"), top = Part(source, "TOP"), bottom = Part(source, "BOT"), line = Part(source, "Capa 5");
            if (grip == null || top == null || bottom == null || line == null)
            {
                Debug.LogError($"{Tag} ARCO.psd needs the layers AGARRE, TOP, BOT and Capa 5");
                return null;
            }

            // Everything relative to the grip's center: that is the point held in the hand.
            Vector3 origin = grip.bounds.center;
            Bounds gripBounds = grip.bounds, stringBounds = line.bounds;
            float inset = gripBounds.extents.y * 0.15f; // hinges slightly inside the grip: no gap when bent
            Vector3 topJoint = new Vector3(gripBounds.center.x, gripBounds.max.y - inset) - origin;
            Vector3 bottomJoint = new Vector3(gripBounds.center.x, gripBounds.min.y + inset) - origin;
            Vector3 topTip = new Vector3(stringBounds.center.x, stringBounds.max.y) - origin;
            Vector3 bottomTip = new Vector3(stringBounds.center.x, stringBounds.min.y) - origin;
            int stringOrder = line.sortingOrder;

            Adopt(grip, root.transform, origin, material);
            Transform topHinge = Hinge("Top Hinge", root.transform, topJoint);
            Transform bottomHinge = Hinge("Bottom Hinge", root.transform, bottomJoint);
            Adopt(top, topHinge, origin, material);
            Adopt(bottom, bottomHinge, origin, material);

            LineRenderer bowString = new GameObject("String").AddComponent<LineRenderer>();
            bowString.transform.SetParent(root.transform, false);
            bowString.useWorldSpace = false;
            bowString.positionCount = 3;
            bowString.widthMultiplier = StringWidth;
            bowString.numCapVertices = 0;
            bowString.alignment = LineAlignment.TransformZ;
            bowString.sharedMaterial = StringMaterial();
            bowString.startColor = bowString.endColor = StringColor;
            bowString.shadowCastingMode = ShadowCastingMode.Off;
            bowString.sortingOrder = stringOrder;

            var arrow = new GameObject("Arrow");
            arrow.transform.SetParent(root.transform, false);
            SpriteRenderer arrowRenderer = arrow.AddComponent<SpriteRenderer>();
            arrowRenderer.sprite = arrowSprite;
            arrowRenderer.sharedMaterial = material;
            arrowRenderer.sortingOrder = grip.sortingOrder + 1;

            HeldBowView view = root.AddComponent<HeldBowView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("topHinge").objectReferenceValue = topHinge;
            serialized.FindProperty("bottomHinge").objectReferenceValue = bottomHinge;
            serialized.FindProperty("topTip").vector3Value = topTip - topJoint;
            serialized.FindProperty("bottomTip").vector3Value = bottomTip - bottomJoint;
            serialized.FindProperty("bowString").objectReferenceValue = bowString;
            serialized.FindProperty("nockedArrow").objectReferenceValue = arrow.transform;
            serialized.FindProperty("arrowTail").vector3Value = new Vector3(arrowSprite.bounds.max.x * 0.97f, 0f, 0f);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // At rest, as the prefab is seen in the editor: straight string, no arrow.
            bowString.SetPositions(new[] { topTip, (topTip + bottomTip) * 0.5f, bottomTip });
            arrow.SetActive(false);

            PrefabUtility.SaveAsPrefabAsset(root, HeldBowPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(source);
        }

        return AssetDatabase.LoadAssetAtPath<HeldBowView>(HeldBowPath);
    }

    private static SpriteRenderer Part(GameObject source, string layer)
    {
        foreach (SpriteRenderer part in source.GetComponentsInChildren<SpriteRenderer>(true))
            if (part.name.Trim() == layer) return part;
        return null;
    }

    /// <summary>Moves a piece under <paramref name="parent"/> keeping where it was relative to <paramref name="origin"/>.</summary>
    private static void Adopt(SpriteRenderer part, Transform parent, Vector3 origin, Material material)
    {
        Vector3 position = part.transform.position - origin;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position - parent.localPosition;
        part.transform.localRotation = Quaternion.identity;
        part.transform.localScale = Vector3.one;
        part.name = part.name.Trim();
        part.sharedMaterial = material;
        part.shadowCastingMode = ShadowCastingMode.Off;
    }

    private static Transform Hinge(string name, Transform parent, Vector3 position)
    {
        var hinge = new GameObject(name).transform;
        hinge.SetParent(parent, false);
        hinge.localPosition = position;
        return hinge;
    }

    private static Material StringMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(StringMaterialPath);
        if (material != null) return material;

        material = new Material(Shader.Find("Sprites/Default")) { name = "BowString" };
        AssetDatabase.CreateAsset(material, StringMaterialPath);
        return material;
    }

    // ---- Prefabs -------------------------------------------------------------------------------------------------

    /// <summary>The sword pickup holding the bow in pieces, laid diagonally like the sword.</summary>
    private static BowPickup CreatePickup(HeldBowView held)
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

            Transform visual = root.GetComponentInChildren<ViewTilt>(true).transform;
            for (int i = visual.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(visual.GetChild(i).gameObject);

            var bow = (GameObject)PrefabUtility.InstantiatePrefab(held.gameObject, visual);
            bow.name = "Bow";
            bow.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            bow.transform.localRotation = Quaternion.Euler(0f, 0f, -35f);
            bow.transform.localScale = new Vector3(PickupScale, PickupScale, 1f);

            PrefabUtility.SaveAsPrefabAsset(root, BowPickupPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        return AssetDatabase.LoadAssetAtPath<BowPickup>(BowPickupPath);
    }

    /// <summary>Root (Arrow, depth sorting) → Visual (faces the camera) → Sprite (turned by the arrow when shot).</summary>
    private static Arrow CreateArrow(Sprite art, Material material)
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
            renderer.sharedMaterial = material;
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

    private static void SetupPlayer(HeldBowView held, Arrow arrow)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPath);
        try
        {
            if (!root.TryGetComponent(out PlayerBow playerBow))
                playerBow = root.AddComponent<PlayerBow>();

            var serialized = new SerializedObject(playerBow);
            serialized.FindProperty("arrowPrefab").objectReferenceValue = arrow;
            serialized.FindProperty("heldBowPrefab").objectReferenceValue = held;
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
