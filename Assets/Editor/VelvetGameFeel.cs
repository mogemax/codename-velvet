#if UNITY_EDITOR
using System;
using System.Linq;
using CodenameVelvet.Enemies;
using CodenameVelvet.Feedback;
using CodenameVelvet.Gathering;
using CodenameVelvet.Inventory;
using CodenameVelvet.Player;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Sets up the game feel: creates its assets in <see cref="Folder"/> (particle materials, burst styles, the damage
/// number style and one hit profile per kind of thing) and adds the feedback components to the prefabs:
/// <list type="bullet">
/// <item>players: hit reaction, knockback, rumble, hurt feedback (own camera, blink, vignette) and action feedback;</item>
/// <item>enemies, the boss (plus its slam dust and rumble) and resource nodes (profile by resource): hit reaction;</item>
/// <item>pickups: arc and magnet motion.</item>
/// </list>
/// Safe to run again: existing assets keep their tuned values and existing components are left alone.
/// </summary>
public static class VelvetGameFeel
{
    private const string Tag = "[VelvetGameFeel]";
    private const string Folder = "Assets/Settings/GameFeel";
    private const string FontPath = "Assets/UI/Fonts/OPTITimes-Roman SDF.asset";

    [MenuItem("Tools/Velvet/Game Feel/Setup")]
    public static void Setup()
    {
        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets/Settings", "GameFeel");

        Material round = ParticleMaterial("Velvet_Particle_Round", isRound: true, glow: 0f);
        Material square = ParticleMaterial("Velvet_Particle_Square", isRound: false, glow: 0.2f);
        Material spark = ParticleMaterial("Velvet_Particle_Spark", isRound: true, glow: 0.85f);

        // Sizes in world units for this camera: the player is ~5 units tall and ~50 px on a 1080p split.
        BurstStyle wood = Burst("Burst_WoodChips", square, b => { Colors(b, 0.55f, 0.35f, 0.17f, 0.78f, 0.58f, 0.32f); b.count = new Vector2Int(6, 9); b.speed = new Vector2(6f, 12f); b.upward = 0.55f; b.size = new Vector2(0.45f, 0.8f); b.lifetime = new Vector2(0.45f, 0.75f); b.gravity = 4f; b.drag = 0.1f; b.spawnRadius = 0.4f; b.spin = true; });
        BurstStyle stone = Burst("Burst_StoneShards", square, b => { Colors(b, 0.42f, 0.42f, 0.45f, 0.72f, 0.72f, 0.74f); b.count = new Vector2Int(6, 9); b.speed = new Vector2(6f, 12f); b.upward = 0.5f; b.size = new Vector2(0.4f, 0.75f); b.lifetime = new Vector2(0.4f, 0.7f); b.gravity = 4.4f; b.drag = 0.1f; b.spawnRadius = 0.4f; b.spin = true; });
        BurstStyle mineral = Burst("Burst_MineralSparks", spark, b => { Colors(b, 0.35f, 0.95f, 1f, 0.85f, 1f, 1f); b.count = new Vector2Int(8, 12); b.speed = new Vector2(8f, 15f); b.upward = 0.55f; b.size = new Vector2(0.3f, 0.55f); b.lifetime = new Vector2(0.25f, 0.45f); b.gravity = 3f; b.drag = 0.3f; b.spawnRadius = 0.4f; });
        BurstStyle hitSpark = Burst("Burst_HitSpark", spark, b => { Colors(b, 1f, 1f, 0.85f, 1f, 0.72f, 0.35f); b.count = new Vector2Int(7, 10); b.speed = new Vector2(10f, 17f); b.upward = 0.35f; b.size = new Vector2(0.35f, 0.7f); b.lifetime = new Vector2(0.15f, 0.3f); b.gravity = 1f; b.drag = 0.6f; b.spawnRadius = 0.3f; });
        BurstStyle hurt = Burst("Burst_Hurt", spark, b => { Colors(b, 1f, 0.35f, 0.3f, 1f, 0.85f, 0.8f); b.count = new Vector2Int(6, 9); b.speed = new Vector2(8f, 13f); b.upward = 0.4f; b.size = new Vector2(0.35f, 0.6f); b.lifetime = new Vector2(0.2f, 0.35f); b.gravity = 1.6f; b.drag = 0.5f; b.spawnRadius = 0.3f; });
        BurstStyle poof = Burst("Burst_Poof", round, b => { b.colorA = new Color(0.96f, 0.94f, 0.9f, 0.9f); b.colorB = new Color(0.8f, 0.78f, 0.75f, 0.8f); b.count = new Vector2Int(10, 14); b.speed = new Vector2(3f, 6f); b.upward = 0.4f; b.size = new Vector2(2.2f, 3.6f); b.lifetime = new Vector2(0.4f, 0.7f); b.gravity = -0.2f; b.drag = 0.7f; b.spawnRadius = 1f; });
        BurstStyle dust = Burst("Burst_Dust", round, b => { b.colorA = new Color(0.8f, 0.72f, 0.6f, 0.55f); b.colorB = new Color(0.66f, 0.6f, 0.5f, 0.45f); b.count = new Vector2Int(2, 3); b.speed = new Vector2(1.2f, 2.5f); b.upward = 0.25f; b.size = new Vector2(1.2f, 2f); b.lifetime = new Vector2(0.35f, 0.55f); b.gravity = -0.1f; b.drag = 0.6f; b.spawnRadius = 0.5f; });
        BurstStyle pickup = Burst("Burst_Pickup", spark, b => { Colors(b, 1f, 0.95f, 0.6f, 1f, 1f, 1f); b.count = new Vector2Int(4, 6); b.speed = new Vector2(3f, 5f); b.upward = 0.8f; b.size = new Vector2(0.3f, 0.5f); b.lifetime = new Vector2(0.2f, 0.35f); b.gravity = 0f; b.drag = 0.4f; b.spawnRadius = 0.3f; });
        BurstStyle slam = Burst("Burst_GroundSlam", round, b => { b.colorA = new Color(0.8f, 0.72f, 0.6f, 0.7f); b.colorB = new Color(0.62f, 0.56f, 0.46f, 0.6f); b.count = new Vector2Int(10, 14); b.speed = new Vector2(10f, 17f); b.upward = 0.05f; b.size = new Vector2(2.2f, 3.8f); b.lifetime = new Vector2(0.4f, 0.7f); b.gravity = -0.1f; b.drag = 0.75f; b.spawnRadius = 1.2f; });

        DamageNumberStyle numbers = Asset<DamageNumberStyle>("DamageNumbers", style => { style.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath); style.fontSize = 30f; style.rise = 9f; style.sideways = 2f; style.gravity = 16f; });

        HitFeedbackProfile player = Profile("Feedback_Player", p => { p.flashColor = new Color(1f, 0.35f, 0.3f, 0.9f); p.flashSeconds = 0.14f; p.squash = new Vector2(0.2f, -0.2f); p.fatalSquash = new Vector2(0.3f, -0.5f); p.joltAmplitude = 0f; p.hitStopSeconds = 0.08f; p.fatalHitStopSeconds = 0.15f; p.hitBurst = hurt; p.fatalBurst = poof; p.burstHeight = 2.4f; p.fatalCameraShake = 0f; });
        HitFeedbackProfile enemy = Profile("Feedback_Enemy", p => { p.hitBurst = hitSpark; p.fatalBurst = poof; p.joltAmplitude = 0.3f; p.burstHeight = 2.2f; p.squash = new Vector2(0.25f, -0.2f); p.fatalSquash = new Vector2(0.4f, -0.6f); p.fatalHitStopSeconds = 0.14f; p.fatalCameraShake = 0.25f; p.damageNumbers = numbers; p.numberHeight = 5.5f; });
        HitFeedbackProfile boss = Profile("Feedback_Boss", p => { p.flashColor = new Color(1f, 1f, 1f, 0.7f); p.flashSeconds = 0.08f; p.squash = new Vector2(0.06f, -0.05f); p.fatalSquash = new Vector2(0.2f, -0.3f); p.joltAmplitude = 0.4f; p.hitStopSeconds = 0.05f; p.fatalHitStopSeconds = 0.4f; p.hitBurst = hitSpark; p.fatalBurst = poof; p.burstHeight = 5f; p.fatalCameraShake = 0.8f; p.damageNumbers = numbers; p.numberHeight = 11f; });
        HitFeedbackProfile tree = Profile("Feedback_Tree", p => NodeProfile(p, wood, poof, burstHeight: 2f));
        HitFeedbackProfile rock = Profile("Feedback_Rock", p => NodeProfile(p, stone, poof, burstHeight: 1f));
        HitFeedbackProfile vein = Profile("Feedback_Vein", p => NodeProfile(p, mineral, poof, burstHeight: 1f));

        AssetDatabase.SaveAssets();

        string[] paths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(path => PrefabUtility.GetPrefabAssetType(AssetDatabase.LoadAssetAtPath<GameObject>(path)) == PrefabAssetType.Variant)
            .ToArray();

        int changed = 0;
        foreach (string path in paths)
        {
            bool didChange = Edit(path, root =>
            {
                bool any = false;
                // Real players only: test rigs without health would get one through RequireComponent.
                if (root.GetComponent<PlayerMovement>() != null && root.GetComponent<PlayerHealth>() != null)
                {
                    any |= Add<HitReaction>(root, ("profile", player));
                    any |= Add<PlayerKnockback>(root);
                    any |= Add<PlayerRumble>(root);
                    any |= Add<PlayerHurtFeedback>(root);
                    any |= Add<PlayerFeedback>(root, ("pickupBurst", pickup), ("dust", dust));
                }
                if (root.GetComponent<EnemyBrain>() != null)
                    any |= Add<HitReaction>(root, ("profile", enemy));
                if (root.GetComponent<CyclopsBrain>() != null)
                {
                    any |= Add<HitReaction>(root, ("profile", boss));
                    any |= Add<ImpactFeedback>(root, ("dust", slam));
                }
                if (root.TryGetComponent(out ResourceNode node))
                    any |= Add<HitReaction>(root, ("profile", NodeProfileFor(node, tree, rock, vein)));
                if (root.GetComponent<ResourcePickup>() != null)
                    any |= Add<PickupMotion>(root);
                return any;
            });
            if (didChange) changed++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"{Tag} Game feel set on {changed} prefab(s); assets in {Folder}.");
    }

    private static void NodeProfile(HitFeedbackProfile p, BurstStyle hit, BurstStyle fatal, float burstHeight)
    {
        p.flashColor = new Color(1f, 1f, 0.9f, 0.6f);
        p.flashSeconds = 0.08f;
        p.squash = new Vector2(0.08f, -0.06f);
        p.fatalSquash = new Vector2(0.3f, -0.6f);
        p.joltAmplitude = 0.45f;
        p.joltSeconds = 0.2f;
        p.hitStopSeconds = 0f; // nodes have no animation to freeze
        p.fatalHitStopSeconds = 0f;
        p.hitBurst = hit;
        p.fatalBurst = fatal;
        p.burstHeight = burstHeight;
        p.fatalCameraShake = 0.15f;
    }

    private static HitFeedbackProfile NodeProfileFor(ResourceNode node, HitFeedbackProfile tree, HitFeedbackProfile rock, HitFeedbackProfile vein)
    {
        var definition = new SerializedObject(node).FindProperty("definition").objectReferenceValue as ResourceNodeDefinition;
        if (definition == null) return rock;
        switch (definition.Resource)
        {
            case ResourceType.Wood: return tree;
            case ResourceType.Stone: return rock;
            default: return vein;
        }
    }

    // ---- prefabs -----------------------------------------------------------------------------------------------

    private static bool Edit(string path, Func<GameObject, bool> change)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            bool changed = change(root);
            if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
            return changed;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>Adds <typeparamref name="T"/> with these serialized references unless it is already there.</summary>
    private static bool Add<T>(GameObject root, params (string field, UnityEngine.Object value)[] references) where T : Component
    {
        if (root.GetComponent<T>() != null) return false;
        var serialized = new SerializedObject(root.AddComponent<T>());
        foreach ((string field, UnityEngine.Object value) in references)
            serialized.FindProperty(field).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }

    // ---- assets ------------------------------------------------------------------------------------------------

    private static Material ParticleMaterial(string name, bool isRound, float glow)
    {
        string path = $"{Folder}/{name}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        material = new Material(Shader.Find("Velvet/Particle")) { name = name, enableInstancing = true };
        material.SetFloat("_Round", isRound ? 1f : 0f);
        material.SetFloat("_Glow", glow);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static BurstStyle Burst(string name, Material material, Action<BurstStyle> fill) =>
        Asset<BurstStyle>(name, style => { style.material = material; fill(style); });

    private static HitFeedbackProfile Profile(string name, Action<HitFeedbackProfile> fill) => Asset(name, fill);

    /// <summary>Loads the asset, or creates it with <paramref name="fill"/> when missing (later edits are kept).</summary>
    private static T Asset<T>(string name, Action<T> fill) where T : ScriptableObject
    {
        string path = $"{Folder}/{name}.asset";
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        asset = ScriptableObject.CreateInstance<T>();
        fill(asset);
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static void Colors(BurstStyle style, float r1, float g1, float b1, float r2, float g2, float b2)
    {
        style.colorA = new Color(r1, g1, b1);
        style.colorB = new Color(r2, g2, b2);
    }
}
#endif
