#if UNITY_EDITOR
using System.Collections.Generic;
using CodenameVelvet.Cameras;
using CodenameVelvet.Map;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Puts the drawn boat on the boat prefab (every level's boat). Safe to run again.
/// The art is two drawings of the same boat, broken and repaired, cut in horizontal bands from the bottom up
/// (Assets/Art/Map/Boat/Boat_{Repaired|Broken}_N.png; every band anchored at the boat's bottom center, so they line
/// up). Each repaired band is one of the <see cref="Boat"/>'s parts and replaces the broken band at its height: the
/// boat repairs itself from the hull up to the sail as materials arrive. The old placeholder pieces are removed;
/// the solid body and the use area stay.
/// </summary>
public static class VelvetBoatArt
{
    private const string Tag = "[VelvetBoatArt]";
    private const string BoatPath = "Assets/Prefabs/Map/Boat.prefab";
    private const string ArtFolder = "Assets/Art/Map/Boat";
    private const string SpriteMaterialPath = "Assets/Materials/Velvet_Sprite.mat";
    private const int Bands = 5;
    private const float BoatLength = 15f; // world units, the placeholder's length (the player is about 5 tall)

    [MenuItem("Tools/Velvet/Art/Use Boat Art")]
    public static void Setup()
    {
        var repaired = new Sprite[Bands];
        var broken = new Sprite[Bands];
        float offset = 0f; // pixels from the boat's bottom to this band's bottom
        for (int i = 0; i < Bands; i++)
        {
            Vector2Int size = TextureSize($"{ArtFolder}/Boat_Repaired_{i + 1}.png");
            if (size == Vector2Int.zero)
            {
                Debug.LogError($"{Tag} Missing {ArtFolder}/Boat_Repaired_{i + 1}.png");
                return;
            }

            repaired[i] = ImportBand($"{ArtFolder}/Boat_Repaired_{i + 1}.png", offset);
            broken[i] = ImportBand($"{ArtFolder}/Boat_Broken_{i + 1}.png", offset); // the top ones are empty: none
            offset += size.y;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(BoatPath);
        try
        {
            Build(root, repaired, broken);
            PrefabUtility.SaveAsPrefabAsset(root, BoatPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        Debug.Log($"{Tag} Done");
    }

    private static Vector2Int TextureSize(string path)
    {
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture == null) return Vector2Int.zero;
        // Before the first import as a sprite the texture may be scaled to a power of two: read the source size.
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.GetSourceTextureWidthAndHeight(out int width, out int height);
        return new Vector2Int(width, height);
    }

    /// <summary>A band as a sprite whose pivot is the boat's bottom center, <paramref name="offset"/> pixels below it.</summary>
    private static Sprite ImportBand(string path, float offset)
    {
        if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            return null;

        importer.GetSourceTextureWidthAndHeight(out int _, out int height);
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.textureType = TextureImporterType.Sprite;
        settings.spriteMode = (int)SpriteImportMode.Single;
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = new Vector2(0.5f, -offset / height);
        settings.spritePixelsPerUnit = 100f;
        settings.spriteMeshType = SpriteMeshType.FullRect; // bands meet edge to edge
        settings.alphaIsTransparency = true;
        settings.wrapMode = TextureWrapMode.Clamp;
        settings.npotScale = TextureImporterNPOTScale.None;
        importer.SetTextureSettings(settings);
        importer.maxTextureSize = 2048;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static void Build(GameObject root, Sprite[] repaired, Sprite[] broken)
    {
        Transform visual = root.transform.Find("Visual");
        for (int i = visual.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(visual.GetChild(i).gameObject);

        // Visual is what the delivery squash scales; the drawing under it faces the camera and sorts by depth.
        var drawing = new GameObject("Drawing");
        drawing.transform.SetParent(visual, false);
        drawing.AddComponent<SortingGroup>();
        drawing.AddComponent<DynamicSortingOrder>();
        drawing.AddComponent<ViewTilt>();
        float pixels = repaired[0].rect.width;
        drawing.transform.localScale = Vector3.one * (BoatLength / (pixels / repaired[0].pixelsPerUnit));

        Material material = AssetDatabase.LoadAssetAtPath<Material>(SpriteMaterialPath);
        var parts = new List<GameObject>();
        var replaced = new List<GameObject>();
        for (int i = 0; i < repaired.Length; i++)
        {
            parts.Add(Band($"Repaired {i + 1}", repaired[i], drawing.transform, material, order: 1));
            replaced.Add(broken[i] != null ? Band($"Broken {i + 1}", broken[i], drawing.transform, material, order: 0) : null);
        }

        var serialized = new SerializedObject(root.GetComponent<Boat>());
        SetArray(serialized.FindProperty("parts"), parts);
        SetArray(serialized.FindProperty("replacedParts"), replaced);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject Band(string name, Sprite sprite, Transform parent, Material material, int order)
    {
        var band = new GameObject(name);
        band.transform.SetParent(parent, false);
        SpriteRenderer renderer = band.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sharedMaterial = material;
        renderer.sortingOrder = order;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = true;
        return band;
    }

    private static void SetArray(SerializedProperty array, List<GameObject> items)
    {
        array.arraySize = items.Count;
        for (int i = 0; i < items.Count; i++)
            array.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
    }
}
#endif
