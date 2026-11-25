#if UNITY_EDITOR
using System.Linq;
using CodenameVelvet.Feedback;
using CodenameVelvet.Gathering;
using CodenameVelvet.Inventory;
using CodenameVelvet.Map;
using CodenameVelvet.Player;
using CodenameVelvet.Spawning;
using CodenameVelvet.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Puts the hand-drawn art in the game (art in Assets/Art, prepared by Tools/prepare_art.py). Safe to run again:
/// it only fills what is missing.
/// <list type="number">
/// <item><b>Items and HUD</b> (project): the resource visuals (wood, stone, mineral = tinted stone, rope), the apple
/// and rope pickups, trees dropping them when felled (apple 35%, rope 50%), and the player HUD (hearts + slots).</item>
/// <item><b>Grass</b> (open scene): a <see cref="GrassField"/> covering the scene's ground.</item>
/// <item><b>Trees</b> (project + open scene): oak and pine variants of the tree with their art and a stump shown
/// once felled; the scene's spawn zones grow them instead of the placeholder tree, half and half, one by one.</item>
/// </list>
/// </summary>
public static class VelvetArtSetup
{
    private const string Tag = "[VelvetArtSetup]";
    private const string Art = "Assets/Art";
    private const string Folder = "Assets/Settings/Art";
    private const string PickupPath = "Assets/Prefabs/Gathering/ResourcePickup.prefab";
    private const string RopePath = "Assets/Prefabs/Gathering/Pickup_Rope.prefab";
    private const string ApplePath = "Assets/Prefabs/Gathering/Pickup_Apple.prefab";
    private const string WindMaterialPath = "Assets/Materials/Velvet_Sprite_Wind.mat";
    private const string GrassMaterialPath = "Assets/Materials/Velvet_Sprite_Grass.mat";
    private const string FontPath = "Assets/UI/Fonts/OPTITimes-Roman SDF.asset";
    private const string CountMaterialPath = "Assets/Settings/Art/HudCount.mat";
    private const string HealBurstPath = "Assets/Settings/GameFeel/Burst_Heal.asset";
    private const string SparkMaterialPath = "Assets/Settings/GameFeel/Velvet_Particle_Spark.mat";
    private const string TreePath = "Assets/Prefabs/Gathering/Arbol.prefab";
    private const string SpriteMaterialPath = "Assets/Materials/Velvet_Sprite.mat";
    private const string TreeDefinitionPath = "Assets/ScriptableObjects/Gathering/Arbol.asset";
    private const float AppleChance = 0.35f;

    /// <summary>The tree species: prefab, art and height in world units (the player is about 5).</summary>
    private static readonly (string prefab, string tree, string stump, float height)[] Species =
    {
        ("Assets/Prefabs/Gathering/Arbol_Roble.prefab", "Environment/Trees/Oak", "Environment/Trees/Oak_Stump", 13f),
        ("Assets/Prefabs/Gathering/Arbol_Pino.prefab", "Environment/Trees/Pine", "Environment/Trees/Pine_Stump", 15f),
    };
    private const float RopeChance = 0.5f;

    // ---- 1. Items and HUD ----------------------------------------------------------------------------------

    [MenuItem("Tools/Velvet/Art/Setup Items and HUD")]
    public static void SetupItemsAndHud()
    {
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Settings", "Art");

        ResourceVisuals visuals = Visuals();
        SetOnPrefab<ResourcePickup>(PickupPath, ("visuals", visuals));
        GameObject rope = RopePickup();
        GameObject apple = ApplePickup();
        int trees = SetTreeDrops(apple, rope);
        int players = SetupPlayers(visuals);

        AssetDatabase.SaveAssets();
        Debug.Log($"{Tag} Items ready: visuals, apple and rope pickups, drops on {trees} tree definition(s), HUD on {players} player prefab(s).");
    }

    private static ResourceVisuals Visuals()
    {
        string path = $"{Folder}/ResourceVisuals.asset";
        var visuals = AssetDatabase.LoadAssetAtPath<ResourceVisuals>(path);
        if (visuals != null) return visuals;

        Sprite stone = Sprite("Items/Stone");
        visuals = ScriptableObject.CreateInstance<ResourceVisuals>();
        visuals.SetEntries(new[]
        {
            Entry(ResourceType.Wood, Sprite("Items/Wood"), Color.white, 1.6f),
            Entry(ResourceType.Stone, stone, Color.white, 1.4f),
            Entry(ResourceType.Mineral, stone, new Color(0.55f, 0.85f, 1f), 1.4f), // crystal blue: same drawing, told apart by color
            Entry(ResourceType.Rope, Sprite("Items/Rope"), Color.white, 1.5f),
        });
        AssetDatabase.CreateAsset(visuals, path);
        return visuals;
    }

    private static ResourceVisuals.Entry Entry(ResourceType type, Sprite sprite, Color tint, float height) =>
        new ResourceVisuals.Entry { type = type, sprite = sprite, tint = tint, worldHeight = height };

    /// <summary>A variant of the resource pickup holding one rope.</summary>
    private static GameObject RopePickup()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(RopePath);
        if (existing != null) return existing;

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PickupPath));
        try
        {
            var pickup = new SerializedObject(instance.GetComponent<ResourcePickup>());
            pickup.FindProperty("type").enumValueIndex = (int)ResourceType.Rope;
            pickup.FindProperty("amount").intValue = 1;
            pickup.ApplyModifiedPropertiesWithoutUndo();
            return PrefabUtility.SaveAsPrefabAsset(instance, RopePath);
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    /// <summary>A copy of the resource pickup (same body, shadow and motion) that heals instead of filling the inventory.</summary>
    private static GameObject ApplePickup()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(ApplePath);
        if (existing != null) return existing;

        AssetDatabase.CopyAsset(PickupPath, ApplePath);
        GameObject root = PrefabUtility.LoadPrefabContents(ApplePath);
        try
        {
            Object.DestroyImmediate(root.GetComponent<ResourcePickup>());
            var heal = new SerializedObject(root.AddComponent<HealPickup>());
            heal.FindProperty("sprite").objectReferenceValue = Sprite("Items/Apple");
            heal.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, ApplePath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        return AssetDatabase.LoadAssetAtPath<GameObject>(ApplePath);
    }

    private static int SetTreeDrops(GameObject apple, GameObject rope)
    {
        int count = 0;
        foreach (ResourceNodeDefinition definition in AssetDatabase.FindAssets("t:ResourceNodeDefinition")
                     .Select(guid => AssetDatabase.LoadAssetAtPath<ResourceNodeDefinition>(AssetDatabase.GUIDToAssetPath(guid))))
        {
            if (definition.Resource != ResourceType.Wood || definition.BonusDrops.Count > 0) continue;
            definition.SetBonusDrops(new[]
            {
                new BonusDrop { prefab = apple, chance = AppleChance },
                new BonusDrop { prefab = rope, chance = RopeChance },
            });
            EditorUtility.SetDirty(definition);
            count++;
        }
        return count;
    }

    private static int SetupPlayers(ResourceVisuals visuals)
    {
        BurstStyle healBurst = HealBurst();
        int count = 0;
        foreach (string path in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath))
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset.GetComponent<PlayerMovement>() == null || asset.GetComponent<PlayerHealth>() == null) continue;
            if (PrefabUtility.GetPrefabAssetType(asset) == PrefabAssetType.Variant) continue;

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                bool changed = false;
                if (root.GetComponent<PlayerHud>() == null)
                {
                    var hud = new SerializedObject(root.AddComponent<PlayerHud>());
                    hud.FindProperty("heartFull").objectReferenceValue = Sprite("UI/Heart_Full");
                    hud.FindProperty("heartLost").objectReferenceValue = Sprite("UI/Heart_Lost");
                    hud.FindProperty("slotFrame").objectReferenceValue = Sprite("UI/Slot_Frame");
                    hud.FindProperty("visuals").objectReferenceValue = visuals;
                    hud.FindProperty("font").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
                    hud.ApplyModifiedPropertiesWithoutUndo();
                    changed = true;
                }
                changed |= SetCountLook(root.GetComponent<PlayerHud>());
                if (root.TryGetComponent(out PlayerFeedback feedback))
                {
                    var serialized = new SerializedObject(feedback);
                    SerializedProperty burst = serialized.FindProperty("healBurst");
                    if (burst.objectReferenceValue == null)
                    {
                        burst.objectReferenceValue = healBurst;
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                        changed = true;
                    }
                }
                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    count++;
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
        return count;
    }

    /// <summary>
    /// Light count with a dark outline over the see-through slot (set once: later Inspector tweaks are kept).
    /// </summary>
    private static bool SetCountLook(PlayerHud hud)
    {
        var serialized = new SerializedObject(hud);
        SerializedProperty material = serialized.FindProperty("countMaterial");
        if (material.objectReferenceValue != null) return false;

        material.objectReferenceValue = CountMaterial();
        serialized.FindProperty("countSize").floatValue = 40f;
        serialized.FindProperty("countColor").colorValue = new Color(1f, 0.95f, 0.84f);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }

    /// <summary>A preset of the HUD font's material with an outline (set on the asset, so it renders reliably).</summary>
    private static Material CountMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(CountMaterialPath);
        if (material != null) return material;

        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        material = new Material(font.material) { name = "HudCount" };
        material.EnableKeyword("OUTLINE_ON");
        material.SetFloat("_OutlineWidth", 0.28f);
        material.SetColor("_OutlineColor", new Color(0.12f, 0.07f, 0.03f));
        material.SetFloat("_FaceDilate", 0.15f);
        AssetDatabase.CreateAsset(material, CountMaterialPath);
        return material;
    }

    private static BurstStyle HealBurst()
    {
        var burst = AssetDatabase.LoadAssetAtPath<BurstStyle>(HealBurstPath);
        if (burst != null) return burst;
        burst = ScriptableObject.CreateInstance<BurstStyle>();
        burst.material = AssetDatabase.LoadAssetAtPath<Material>(SparkMaterialPath);
        burst.colorA = new Color(0.55f, 1f, 0.45f);
        burst.colorB = new Color(0.9f, 1f, 0.7f);
        burst.count = new Vector2Int(8, 12);
        burst.speed = new Vector2(2f, 4f);
        burst.upward = 0.9f;
        burst.size = new Vector2(0.3f, 0.55f);
        burst.lifetime = new Vector2(0.4f, 0.7f);
        burst.gravity = -0.3f;
        burst.drag = 0.4f;
        burst.spawnRadius = 0.8f;
        AssetDatabase.CreateAsset(burst, HealBurstPath);
        return burst;
    }

    // ---- 2. Grass ------------------------------------------------------------------------------------------

    [MenuItem("Tools/Velvet/Art/Add Grass (Open Scene)")]
    public static void AddGrass()
    {
        var scene = EditorSceneManager.GetActiveScene();
        Bounds? ground = GroundBounds();
        if (ground == null)
        {
            Debug.LogError($"{Tag} No colliders on the Ground layer in {scene.name}: nothing to cover.");
            return;
        }

        GameObject holder = scene.GetRootGameObjects().FirstOrDefault(go => go.GetComponent<GrassField>() != null);
        if (holder == null)
        {
            holder = new GameObject("Grass");
            Undo.RegisterCreatedObjectUndo(holder, "Add Grass");
            holder.AddComponent<GrassField>();
        }

        Bounds bounds = ground.Value;
        holder.transform.position = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
        var field = new SerializedObject(holder.GetComponent<GrassField>());
        SerializedProperty sprites = field.FindProperty("sprites");
        sprites.arraySize = 3;
        for (int i = 0; i < 3; i++)
            sprites.GetArrayElementAtIndex(i).objectReferenceValue = Sprite($"Environment/Grass_{i + 1}");
        field.FindProperty("material").objectReferenceValue = GrassMaterial();
        field.FindProperty("area").vector2Value = new Vector2(bounds.size.x, bounds.size.z);
        field.FindProperty("seed").intValue = scene.name.GetHashCode();
        field.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"{Tag} Grass covers {bounds.size.x:F0} × {bounds.size.z:F0} in {scene.name}. Save the scene.");
    }

    private static Bounds? GroundBounds()
    {
        int ground = LayerMask.NameToLayer("Ground");
        Bounds? bounds = null;
        foreach (Collider collider in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
        {
            if (collider.gameObject.layer != ground || collider.isTrigger) continue;
            if (bounds == null) bounds = collider.bounds;
            else { Bounds b = bounds.Value; b.Encapsulate(collider.bounds); bounds = b; }
        }
        return bounds;
    }

    /// <summary>The wind sprite material, facing the camera by itself, casting its shadow on the ground like the rest.</summary>
    private static Material GrassMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(GrassMaterialPath);
        if (material == null)
        {
            material = new Material(AssetDatabase.LoadAssetAtPath<Material>(WindMaterialPath)) { name = "Velvet_Sprite_Grass" };
            material.SetFloat("_Billboard", 1f);
            material.SetFloat("_WindStrength", 0.1f);
            AssetDatabase.CreateAsset(material, GrassMaterialPath);
        }
        // Earlier versions turned the cast-shadow pass off.
        if (!material.GetShaderPassEnabled("SRPDefaultUnlit"))
        {
            material.SetShaderPassEnabled("SRPDefaultUnlit", true);
            EditorUtility.SetDirty(material);
        }
        return material;
    }

    // ---- 3. Trees ------------------------------------------------------------------------------------------

    [MenuItem("Tools/Velvet/Art/Use Tree Art (Open Scene)")]
    public static void UseTreeArt()
    {
        ResourceNode[] variants = Species.Select(TreeVariant).ToArray();
        WhiteTreeTint();

        var placeholder = AssetDatabase.LoadAssetAtPath<GameObject>(TreePath).GetComponent<ResourceNode>();
        int zones = 0;
        foreach (SpawnZone zone in Object.FindObjectsByType<SpawnZone>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (ReplaceTreeEntries(zone, placeholder, variants)) zones++;

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log($"{Tag} Tree art ready: {variants.Length} species, placeholder replaced in {zones} spawn zone(s). Save the scene.");
    }

    /// <summary>A variant of the tree prefab with this species' art, standing on its base, and its stump.</summary>
    private static ResourceNode TreeVariant((string prefab, string tree, string stump, float height) species)
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(species.prefab);
        if (existing != null) return existing.GetComponent<ResourceNode>();

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(TreePath));
        try
        {
            Sprite tree = Sprite(species.tree);
            Transform visual = instance.transform.Find("Visual");
            visual.GetComponent<SpriteRenderer>().sprite = tree;
            visual.localPosition = Vector3.zero; // the sprite's pivot is the trunk's base
            visual.localScale = Vector3.one * (species.height / tree.bounds.size.y);

            // Same scale as the tree: the artist drew both at the same size.
            var stump = new GameObject("Stump");
            stump.transform.SetParent(visual, false);
            var stumpRenderer = stump.AddComponent<SpriteRenderer>();
            stumpRenderer.sprite = Sprite(species.stump);
            stumpRenderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(SpriteMaterialPath); // no wind on a stump
            stumpRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            stump.SetActive(false);

            // Hit around the trunk, not the crown.
            var body = instance.GetComponent<SphereCollider>();
            body.radius = 1.3f;
            body.center = new Vector3(0f, 1.3f, 0f);

            var node = new SerializedObject(instance.GetComponent<ResourceNode>());
            node.FindProperty("depletedVisual").objectReferenceValue = stump;
            node.FindProperty("dropRadius").vector2Value = new Vector2(2.2f, 3.4f);
            node.ApplyModifiedPropertiesWithoutUndo();

            return PrefabUtility.SaveAsPrefabAsset(instance, species.prefab).GetComponent<ResourceNode>();
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    /// <summary>The placeholder tinted the circle green; the art brings its own colors.</summary>
    private static void WhiteTreeTint()
    {
        var definition = new SerializedObject(AssetDatabase.LoadAssetAtPath<ResourceNodeDefinition>(TreeDefinitionPath));
        definition.FindProperty("color").colorValue = Color.white;
        definition.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>Each placeholder entry becomes one entry per species, splitting its weight (same sizes); trees always spawn alone.</summary>
    private static bool ReplaceTreeEntries(SpawnZone zone, ResourceNode placeholder, ResourceNode[] variants)
    {
        var serialized = new SerializedObject(zone);
        SerializedProperty nodes = serialized.FindProperty("nodes");
        bool changed = false;
        for (int i = nodes.arraySize - 1; i >= 0; i--)
        {
            SerializedProperty entry = nodes.GetArrayElementAtIndex(i);
            if (entry.FindPropertyRelative("prefab").objectReferenceValue != placeholder) continue;

            float weight = entry.FindPropertyRelative("weight").floatValue / variants.Length;
            entry.FindPropertyRelative("weight").floatValue = weight;
            entry.FindPropertyRelative("prefab").objectReferenceValue = variants[0];
            for (int v = 1; v < variants.Length; v++)
            {
                nodes.InsertArrayElementAtIndex(i); // copies the entry
                nodes.GetArrayElementAtIndex(i + 1).FindPropertyRelative("prefab").objectReferenceValue = variants[v];
            }
            changed = true;
        }

        // Trees stand alone, never in groves (rocks keep their clusters).
        for (int i = 0; i < nodes.arraySize; i++)
        {
            SerializedProperty entry = nodes.GetArrayElementAtIndex(i);
            if (!variants.Contains(entry.FindPropertyRelative("prefab").objectReferenceValue as ResourceNode)) continue;
            SerializedProperty cluster = entry.FindPropertyRelative("clusterSize");
            if (cluster.vector2IntValue == Vector2Int.one) continue;
            cluster.vector2IntValue = Vector2Int.one;
            changed = true;
        }
        if (changed) serialized.ApplyModifiedProperties();
        return changed;
    }

    // ---- helpers -------------------------------------------------------------------------------------------

    private static Sprite Sprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{Art}/{name}.png");

    private static void SetOnPrefab<T>(string path, params (string field, Object value)[] references) where T : Component
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var serialized = new SerializedObject(root.GetComponent<T>());
            foreach ((string field, Object value) in references)
                serialized.FindProperty(field).objectReferenceValue = value;
            if (serialized.ApplyModifiedPropertiesWithoutUndo())
                PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
#endif
