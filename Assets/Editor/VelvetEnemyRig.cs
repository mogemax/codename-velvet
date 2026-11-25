#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Animation;
using UnityEditor.U2D.PSD;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.U2D;

/// <summary>
/// Gives the enemy drawing (ENEMIGO.psd) the perrito's rig, so the perrito's animations play on it unchanged.
/// Safe to run again.
/// The enemy was drawn on the perrito's template: its layers have the same names, sizes and places, shifted a little.
/// <list type="number">
/// <item>Skeleton: the perrito's bones, moved by that shift (measured from the layers both have). Same names, same
/// lengths and angles, so the generated bones match the animation paths.</item>
/// <item>Meshes: each layer gets a grid over its whole sprite (so no drawn pixel is cut off), every vertex weighted
/// like the nearest vertices of the matching perrito layer (layers it does not have borrow from <see cref="Borrow"/>).</item>
/// </list>
/// </summary>
public static class VelvetEnemyRig
{
    private const string Tag = "[VelvetEnemyRig]";
    private const string SourcePath = "Assets/Assets/CHARACTERS/PLAYER(S)/PERRITO.psd";
    public const string EnemyPath = "Assets/Assets/CHARACTERS/ENEMIES/ENEMIGO.psd";
    private const float CellPixels = 40f;
    private const int Nearest = 4;

    // Enemy layers the perrito has no layer for, and the perrito layer they move like.
    private static readonly Dictionary<string, string> Borrow = new()
    {
        { "Capa 14", "MOUTH" }, // the head
        { "Capa 17", "MOUTH" }, // ears and hair
        { "Capa 20", "MOUTH" },
        { "Capa 22", "MOUTH" },
        { "Capa 24", "MOUTH" },
        { "R PUPIL", "PUPILS" },
        { "L PUPIL", "PUPILS" },
    };

    [MenuItem("Tools/Velvet/Enemies/Rig Enemy From Perrito")]
    public static void Rig()
    {
        AssetDatabase.SetImporterOverride<PSDImporter>(EnemyPath);
        if (AssetImporter.GetAtPath(EnemyPath) is not PSDImporter enemyImporter)
        {
            Debug.LogError($"{Tag} Missing {EnemyPath}");
            return;
        }

        if (!enemyImporter.useCharacterMode || !enemyImporter.useMosaicMode)
        {
            enemyImporter.useMosaicMode = true;
            enemyImporter.useCharacterMode = true;
            enemyImporter.SaveAndReimport();
        }

        ISpriteEditorDataProvider source = Provider(AssetImporter.GetAtPath(SourcePath));
        ISpriteEditorDataProvider enemy = Provider(enemyImporter);
        CharacterData from = source.GetDataProvider<ICharacterDataProvider>().GetCharacterData();
        CharacterData to = enemy.GetDataProvider<ICharacterDataProvider>().GetCharacterData();
        Dictionary<string, CharacterPart> fromParts = PartsByName(source, from);
        Dictionary<string, CharacterPart> toParts = PartsByName(enemy, to);

        Vector2 shift = Shift(fromParts, toParts);
        to.bones = from.bones.Select(bone =>
        {
            if (bone.parentId < 0) bone.position += (Vector3)shift;
            return bone;
        }).ToArray();

        var fromMesh = source.GetDataProvider<ISpriteMeshDataProvider>();
        var toMesh = enemy.GetDataProvider<ISpriteMeshDataProvider>();
        var toBones = enemy.GetDataProvider<ISpriteBoneDataProvider>();
        var rects = enemy.GetSpriteRects().ToDictionary(r => r.spriteID.ToString());
        (Vector2 position, Quaternion rotation)[] world = WorldBones(to.bones);

        for (int i = 0; i < to.parts.Length; i++)
        {
            CharacterPart part = to.parts[i];
            string name = rects[part.spriteId].name;
            string sourceName = fromParts.ContainsKey(name) ? name : Borrow.GetValueOrDefault(name);
            if (sourceName == null || !fromParts.TryGetValue(sourceName, out CharacterPart model))
            {
                Debug.LogWarning($"{Tag} No perrito layer for \"{name}\"; left unrigged");
                continue;
            }

            var id = new GUID(part.spriteId);
            part.bones = model.bones.ToArray();
            to.parts[i] = part;

            Vector2 size = part.spritePosition.size;
            Vector2 toSource = part.spritePosition.position - shift; // this sprite's space → the perrito character's
            Vertex2DMetaData[] modelVertices = fromMesh.GetVertices(new GUID(model.spriteId));
            Vector2 modelOrigin = model.spritePosition.position;
            Grid(size, out Vector2[] points, out int[] indices, out Vector2Int[] edges);
            toMesh.SetVertices(id, points.Select(p => new Vertex2DMetaData
            {
                position = p,
                boneWeight = Weigh(p + toSource, modelVertices, modelOrigin),
            }).ToArray());
            toMesh.SetIndices(id, indices);
            toMesh.SetEdges(id, edges);
            toBones.SetBones(id, SpriteBones(to.bones, world, part));
        }

        enemy.GetDataProvider<ICharacterDataProvider>().SetCharacterData(to);
        enemy.Apply();
        SetPivot(enemyImporter, AssetImporter.GetAtPath(SourcePath), shift, from.dimension, to.dimension);
        enemyImporter.SaveAndReimport();
        Debug.Log($"{Tag} Done: {to.bones.Length} bones, shift {shift}");
    }

    private static ISpriteEditorDataProvider Provider(AssetImporter importer)
    {
        var factories = new SpriteDataProviderFactories();
        factories.Init();
        ISpriteEditorDataProvider provider = factories.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();
        return provider;
    }

    private static Dictionary<string, CharacterPart> PartsByName(ISpriteEditorDataProvider provider, CharacterData data)
    {
        var names = provider.GetSpriteRects().ToDictionary(r => r.spriteID.ToString(), r => r.name);
        return data.parts.ToDictionary(p => names[p.spriteId]);
    }

    /// <summary>How far the enemy's layers sit from the perrito's: the average over the layers both have.</summary>
    private static Vector2 Shift(Dictionary<string, CharacterPart> from, Dictionary<string, CharacterPart> to)
    {
        Vector2[] shifts = to.Keys.Where(from.ContainsKey)
            .Select(name => to[name].spritePosition.center - from[name].spritePosition.center).ToArray();
        Vector2 sum = Vector2.zero;
        foreach (Vector2 shift in shifts) sum += shift;
        return new Vector2(Mathf.Round(sum.x / shifts.Length), Mathf.Round(sum.y / shifts.Length));
    }

    private static (Vector2, Quaternion)[] WorldBones(SpriteBone[] bones)
    {
        var world = new (Vector2 position, Quaternion rotation)[bones.Length];
        for (int i = 0; i < bones.Length; i++) // parents come before their children
        {
            SpriteBone bone = bones[i];
            if (bone.parentId < 0) world[i] = (bone.position, bone.rotation);
            else
            {
                var parent = world[bone.parentId];
                world[i] = (parent.position + (Vector2)(parent.rotation * bone.position), parent.rotation * bone.rotation);
            }
        }
        return world;
    }

    /// <summary>The part's bones in its sprite's space: chains stay local, the others are placed from the sprite's corner.</summary>
    private static List<SpriteBone> SpriteBones(SpriteBone[] bones, (Vector2 position, Quaternion rotation)[] world, CharacterPart part)
    {
        var list = new List<SpriteBone>();
        foreach (int index in part.bones)
        {
            SpriteBone bone = bones[index];
            int parent = System.Array.IndexOf(part.bones, bone.parentId);
            if (parent < 0)
            {
                bone.position = world[index].position - part.spritePosition.position;
                bone.rotation = world[index].rotation;
            }
            bone.parentId = parent;
            list.Add(bone);
        }
        return list;
    }

    /// <summary>A grid over the whole sprite: two triangles per cell, the outline as its edges.</summary>
    private static void Grid(Vector2 size, out Vector2[] points, out int[] indices, out Vector2Int[] edges)
    {
        int columns = Mathf.Max(1, Mathf.CeilToInt(size.x / CellPixels));
        int rows = Mathf.Max(1, Mathf.CeilToInt(size.y / CellPixels));
        points = new Vector2[(columns + 1) * (rows + 1)];
        for (int y = 0; y <= rows; y++)
            for (int x = 0; x <= columns; x++)
                points[y * (columns + 1) + x] = new Vector2(size.x * x / columns, size.y * y / rows);

        int At(int x, int y) => y * (columns + 1) + x;
        var triangles = new List<int>();
        for (int y = 0; y < rows; y++)
            for (int x = 0; x < columns; x++)
                triangles.AddRange(new[] { At(x, y), At(x, y + 1), At(x + 1, y + 1), At(x, y), At(x + 1, y + 1), At(x + 1, y) });
        indices = triangles.ToArray();

        var outline = new List<int>();
        for (int x = 0; x < columns; x++) outline.Add(At(x, 0));
        for (int y = 0; y < rows; y++) outline.Add(At(columns, y));
        for (int x = columns; x > 0; x--) outline.Add(At(x, rows));
        for (int y = rows; y > 0; y--) outline.Add(At(0, y));
        edges = outline.Select((v, i) => new Vector2Int(v, outline[(i + 1) % outline.Count])).ToArray();
    }

    /// <summary>The weights of the nearest model vertices, blended by closeness (bone indices are the part's own).</summary>
    private static BoneWeight Weigh(Vector2 point, Vertex2DMetaData[] model, Vector2 modelOrigin)
    {
        var total = new Dictionary<int, float>();
        foreach (Vertex2DMetaData vertex in model
                     .OrderBy(v => (v.position + modelOrigin - point).sqrMagnitude).Take(Nearest))
        {
            float closeness = 1f / (0.001f + Vector2.Distance(vertex.position + modelOrigin, point));
            BoneWeight w = vertex.boneWeight;
            Add(total, w.boneIndex0, w.weight0 * closeness);
            Add(total, w.boneIndex1, w.weight1 * closeness);
            Add(total, w.boneIndex2, w.weight2 * closeness);
            Add(total, w.boneIndex3, w.weight3 * closeness);
        }

        var top = total.OrderByDescending(p => p.Value).Take(4).ToArray();
        float sum = top.Sum(p => p.Value);
        float Weight(int i) => i < top.Length ? top[i].Value / sum : 0f;
        int Bone(int i) => i < top.Length ? top[i].Key : 0;
        return new BoneWeight
        {
            boneIndex0 = Bone(0), weight0 = Weight(0),
            boneIndex1 = Bone(1), weight1 = Weight(1),
            boneIndex2 = Bone(2), weight2 = Weight(2),
            boneIndex3 = Bone(3), weight3 = Weight(3),
        };
    }

    private static void Add(Dictionary<int, float> total, int bone, float weight)
    {
        if (weight <= 0f) return;
        total[bone] = total.GetValueOrDefault(bone) + weight;
    }

    /// <summary>The enemy's root pivot at the same point of the body as the perrito's (between the feet).</summary>
    private static void SetPivot(AssetImporter enemy, AssetImporter source, Vector2 shift, Vector2Int fromSize, Vector2Int toSize)
    {
        var from = new SerializedObject(source);
        var to = new SerializedObject(enemy);
        Vector2 pivot = from.FindProperty("m_DocumentPivot").vector2Value * (Vector2)fromSize + shift;
        to.FindProperty("m_DocumentAlignment").intValue = from.FindProperty("m_DocumentAlignment").intValue;
        to.FindProperty("m_DocumentPivot").vector2Value = pivot / toSize;
        to.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
