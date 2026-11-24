#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using CodenameVelvet.DayNight;
using CodenameVelvet.Enemies;
using CodenameVelvet.Gathering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Sets up the world look in two steps, both safe to run again (they only fill what is missing):
/// <list type="number">
/// <item><b>Sprite Shadows</b> (project): characters, enemies, resource nodes and pickups get a
/// <see cref="ContactShadow"/> (ground blob, and the ground height for the shadow "Velvet/Sprite" casts by itself);
/// trees use the wind material. SpriteRenderer "Cast Shadows" stays off: URP leaves sprites out of the shadow map.</item>
/// <item><b>Atmosphere</b> (open scene): a "World Look" object with three global volumes (base look, dusk, night),
/// wired to <see cref="DayNightLighting"/>, which blends dusk and night by weight.</item>
/// </list>
/// Created assets live in <see cref="Folder"/>; their values can be tuned there.
/// </summary>
public static class VelvetWorldLook
{
    private const string Tag = "[VelvetWorldLook]";
    private const string Folder = "Assets/Settings/WorldLook";
    private const string SpriteMaterialPath = "Assets/Materials/Velvet_Sprite.mat";
    private const string WindMaterialPath = "Assets/Materials/Velvet_Sprite_Wind.mat";
    private const string ContactShadowMaterialPath = "Assets/Materials/Velvet_ContactShadow.mat";
    private const string TreePrefabPath = "Assets/Prefabs/Gathering/Arbol.prefab";
    private const string LookObjectName = "World Look";

    // ---- 1. Prefabs ----------------------------------------------------------------------------------------

    [MenuItem("Tools/Velvet/Visuals/Setup Sprite Shadows")]
    public static void SetupSpriteShadows()
    {
        Material sprite = AssetDatabase.LoadAssetAtPath<Material>(SpriteMaterialPath);
        if (sprite == null)
        {
            Debug.LogError($"{Tag} {SpriteMaterialPath} not found; nothing was changed.");
            return;
        }
        Material wind = WindMaterial(sprite);
        Material contact = ContactShadowMaterial();

        // Base prefabs first, so variants inherit instead of getting their own copy.
        string[] paths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(path => PrefabUtility.GetPrefabAssetType(AssetDatabase.LoadAssetAtPath<GameObject>(path)) == PrefabAssetType.Variant)
            .ToArray();

        int changed = 0;
        foreach (string path in paths)
            if (SetupPrefab(path, sprite, wind, contact)) changed++;

        AssetDatabase.SaveAssets();
        Debug.Log($"{Tag} Sprite shadows set on {changed} prefab(s).");
    }

    private static bool SetupPrefab(string path, Material sprite, Material wind, Material contact)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            bool changed = false;
            bool isTree = path == TreePrefabPath;
            foreach (SpriteRenderer renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                Material current = renderer.sharedMaterial;
                if (current != sprite && current != wind) continue;

                if (isTree && current != wind) { renderer.sharedMaterial = wind; changed = true; }
                if (renderer.shadowCastingMode != ShadowCastingMode.Off) { renderer.shadowCastingMode = ShadowCastingMode.Off; changed = true; }
            }

            if (NeedsContactShadow(root) && root.GetComponent<ContactShadow>() == null)
            {
                var shadow = new SerializedObject(root.AddComponent<ContactShadow>());
                shadow.FindProperty("material").objectReferenceValue = contact;
                shadow.ApplyModifiedPropertiesWithoutUndo();
                changed = true;
            }

            if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
            return changed;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>Things that stand on the ground: players, enemies (the boss too), resource nodes and pickups.</summary>
    private static bool NeedsContactShadow(GameObject root) =>
        root.GetComponent<PlayerMovement>() != null
        || root.GetComponent<EnemyBrain>() != null
        || root.GetComponent<CyclopsBrain>() != null
        || root.GetComponent<ResourceNode>() != null
        || root.GetComponent<ResourcePickup>() != null;

    private static Material WindMaterial(Material sprite)
    {
        var wind = AssetDatabase.LoadAssetAtPath<Material>(WindMaterialPath);
        if (wind != null) return wind;
        wind = new Material(sprite) { name = "Velvet_Sprite_Wind" };
        wind.SetFloat("_WindStrength", 0.06f);
        AssetDatabase.CreateAsset(wind, WindMaterialPath);
        return wind;
    }

    private static Material ContactShadowMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(ContactShadowMaterialPath);
        if (material != null) return material;
        material = new Material(Shader.Find("Velvet/Contact Shadow")) { name = "Velvet_ContactShadow", enableInstancing = true };
        AssetDatabase.CreateAsset(material, ContactShadowMaterialPath);
        return material;
    }

    // ---- 2. Scene ------------------------------------------------------------------------------------------

    [MenuItem("Tools/Velvet/Visuals/Setup Atmosphere (Open Scene)")]
    public static void SetupAtmosphere()
    {
        VolumeProfile look = Profile("Look_Base", BaseLook);
        VolumeProfile dusk = Profile("Look_Dusk", DuskLook);
        VolumeProfile night = Profile("Look_Night", NightLook);

        var scene = EditorSceneManager.GetActiveScene();
        GameObject holder = scene.GetRootGameObjects().FirstOrDefault(go => go.name == LookObjectName);
        if (holder == null)
        {
            holder = new GameObject(LookObjectName);
            Undo.RegisterCreatedObjectUndo(holder, "Create World Look");
        }

        Volume baseVolume = VolumeChild(holder, "Base", look, priority: 0, weight: 1f);
        Volume duskVolume = VolumeChild(holder, "Dusk", dusk, priority: 1, weight: 0f);
        Volume nightVolume = VolumeChild(holder, "Night", night, priority: 2, weight: 0f);

        DayNightLighting lighting = Object.FindAnyObjectByType<DayNightLighting>();
        if (lighting != null)
        {
            var so = new SerializedObject(lighting);
            so.FindProperty("duskVolume").objectReferenceValue = duskVolume;
            so.FindProperty("nightVolume").objectReferenceValue = nightVolume;
            so.ApplyModifiedProperties();
        }
        else
        {
            Debug.LogWarning($"{Tag} No DayNightLighting in the scene: the dusk and night volumes stay at weight 0.");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"{Tag} Atmosphere ready in {scene.name} (base look on '{baseVolume.name}'). Save the scene.");
    }

    private static Volume VolumeChild(GameObject holder, string name, VolumeProfile profile, int priority, float weight)
    {
        Transform child = holder.transform.Find(name);
        if (child == null)
        {
            child = new GameObject(name).transform;
            Undo.RegisterCreatedObjectUndo(child.gameObject, "Create volume");
            child.SetParent(holder.transform, false);
        }
        if (!child.TryGetComponent(out Volume volume)) volume = child.gameObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = profile;
        volume.priority = priority;
        volume.weight = weight;
        return volume;
    }

    /// <summary>Loads the profile, or creates it with <paramref name="fill"/> when missing (later edits are kept).</summary>
    private static VolumeProfile Profile(string name, System.Action<VolumeProfile> fill)
    {
        string path = $"{Folder}/{name}.asset";
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (profile != null) return profile;

        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Settings", "WorldLook");
        profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, path);
        fill(profile);
        foreach (VolumeComponent component in profile.components)
        {
            component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(component, profile);
        }
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        return profile;
    }

    private static void BaseLook(VolumeProfile profile)
    {
        profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.Neutral);
        Bloom bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(0.95f);
        bloom.intensity.Override(0.25f);
        bloom.scatter.Override(0.6f);
        ColorAdjustments color = profile.Add<ColorAdjustments>(true);
        color.saturation.Override(8f);
        color.contrast.Override(8f);
        Vignette vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(0.18f);
        vignette.smoothness.Override(0.45f);
    }

    private static void DuskLook(VolumeProfile profile)
    {
        ColorAdjustments color = profile.Add<ColorAdjustments>(true);
        color.colorFilter.Override(new Color(1f, 0.9f, 0.82f));
        color.saturation.Override(15f);
        profile.Add<Vignette>(true).intensity.Override(0.26f);
    }

    private static void NightLook(VolumeProfile profile)
    {
        ColorAdjustments color = profile.Add<ColorAdjustments>(true);
        color.saturation.Override(-20f);
        color.contrast.Override(12f);
        Vignette vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(0.32f);
        vignette.color.Override(new Color(0.02f, 0.03f, 0.1f));
    }
}
#endif
