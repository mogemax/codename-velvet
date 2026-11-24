#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using CodenameVelvet.Cameras;
using CodenameVelvet.Multiplayer;
using CodenameVelvet.Spawning;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Repairs gameplay objects of the open scene that were rotated or scaled along with the map:
/// <list type="bullet">
/// <item>Player spawn points: the group goes to the average of the current points, 1 unit above the ground below
/// it, with the points in a 4 × 4 square and no rotation or scale.</item>
/// <item>SpawnZones and camera framing zones: rotation and Transform scale move into the BoxCollider size (zones
/// read an axis-aligned area, so a rotated box spawns outside what it draws). SpawnZones also get their height
/// fitted to the ground inside them, since they only accept ground within the box height.</item>
/// </list>
/// Anything over water or without ground is left as it is and reported. Undoable.
/// </summary>
public static class VelvetFixGameplayObjects
{
    private const string Tag = "[VelvetFixGameplayObjects]";
    private const float SpawnHeight = 1f;
    private const float SpawnSpacing = 2f;
    private const float SkyHeight = 500f;
    private const int HeightSamples = 12;
    private const float HeightMargin = 2f;
    private const float MinZoneHeight = 6f;

    private static readonly Vector3[] SpawnOffsets =
    {
        new Vector3(-1f, 0f, -1f), new Vector3(1f, 0f, -1f), new Vector3(-1f, 0f, 1f), new Vector3(1f, 0f, 1f),
    };

    [MenuItem("Tools/Velvet/Map/Fix Gameplay Objects")]
    public static void Fix()
    {
        Scene scene = SceneManager.GetActiveScene();
        List<GameObject> roots = scene.GetRootGameObjects().ToList();
        int groundMask = LayerMask.GetMask("Ground");
        int surfaceMask = LayerMask.GetMask("Ground", "Water");

        foreach (PlayerSpawner spawner in roots.SelectMany(r => r.GetComponentsInChildren<PlayerSpawner>(true)))
            FixSpawnPoints(spawner, groundMask);

        foreach (BoxCollider area in roots.SelectMany(r => r.GetComponentsInChildren<CameraFramingZone>(true))
                     .Select(zone => zone.GetComponent<BoxCollider>()))
            Flatten(area);

        Physics.SyncTransforms();
        foreach (SpawnZone zone in roots.SelectMany(r => r.GetComponentsInChildren<SpawnZone>(true)))
        {
            BoxCollider area = zone.GetComponent<BoxCollider>();
            Flatten(area);
            Physics.SyncTransforms();
            FitHeight(area, groundMask, surfaceMask);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"{Tag} Done. Check the result with dev mode (F1, then 1-9) and save the scene.");
    }

    private static void FixSpawnPoints(PlayerSpawner spawner, int groundMask)
    {
        var serialized = new SerializedObject(spawner);
        SerializedProperty list = serialized.FindProperty("spawnPoints");
        Transform[] points = Enumerable.Range(0, list.arraySize)
            .Select(i => list.GetArrayElementAtIndex(i).objectReferenceValue as Transform)
            .Where(point => point != null)
            .ToArray();

        // Measured before any reset: the points may be children of the spawner.
        Vector3 average = points.Length > 0
            ? points.Aggregate(Vector3.zero, (sum, p) => sum + p.position) / points.Length
            : Vector3.zero;

        Undo.RecordObject(spawner.transform, "Fix spawner");
        ResetRotationAndScale(spawner.transform, keepWorldPosition: true);
        if (points.Length == 0)
        {
            Debug.LogWarning($"{Tag} {spawner.name} has no spawn points.", spawner);
            return;
        }

        if (!Physics.Raycast(new Vector3(average.x, average.y + SkyHeight, average.z), Vector3.down, out RaycastHit hit,
                SkyHeight * 2f, groundMask, QueryTriggerInteraction.Ignore))
        {
            Debug.LogWarning($"{Tag} No ground below the spawn points ({average}); place them by hand.", spawner);
            return;
        }

        Vector3 center = hit.point + Vector3.up * SpawnHeight;
        Transform group = points[0].parent;
        bool shareGroup = group != null && group != spawner.transform && points.All(p => p.parent == group);
        if (shareGroup)
        {
            Undo.RecordObject(group, "Fix spawn points");
            ResetRotationAndScale(group, keepWorldPosition: false);
            group.position = center;
        }

        for (int i = 0; i < points.Length; i++)
        {
            Undo.RecordObject(points[i], "Fix spawn points");
            ResetRotationAndScale(points[i], keepWorldPosition: false);
            points[i].position = center + SpawnOffsets[i % SpawnOffsets.Length] * SpawnSpacing;
        }

        Debug.Log($"{Tag} {points.Length} spawn point(s) placed around {center}.", spawner);
    }

    /// <summary>Moves Transform rotation and scale into the BoxCollider, keeping the box's place and real size.</summary>
    private static void Flatten(BoxCollider area)
    {
        if (area == null) return;
        Transform t = area.transform;
        bool rotated = Quaternion.Angle(t.rotation, Quaternion.identity) > 0.01f;
        bool scaled = (t.lossyScale - Vector3.one).sqrMagnitude > 1e-6f;
        if (!rotated && !scaled && area.center == Vector3.zero) return;

        Vector3 worldCenter = t.TransformPoint(area.center);
        Vector3 scale = t.lossyScale;
        var size = new Vector3(Mathf.Abs(area.size.x * scale.x), Mathf.Abs(area.size.y * scale.y), Mathf.Abs(area.size.z * scale.z));

        // Closer to a quarter turn than to straight: the box's length and width swap on the world axes.
        float yaw = Mathf.Repeat(t.eulerAngles.y, 180f);
        if (yaw > 45f && yaw < 135f) size = new Vector3(size.z, size.y, size.x);

        Undo.RecordObject(t, "Fix zone");
        Undo.RecordObject(area, "Fix zone");
        ResetRotationAndScale(t, keepWorldPosition: false);
        t.position = worldCenter;
        area.center = Vector3.zero;
        area.size = size;
        Debug.Log($"{Tag} {area.name}: rotation/scale moved into the box, size {size}.", area);
    }

    /// <summary>Makes the box height cover the ground inside it (zones only accept ground within that height).</summary>
    private static void FitHeight(BoxCollider area, int groundMask, int surfaceMask)
    {
        if (area == null) return;
        Bounds bounds = area.bounds;
        int waterLayer = LayerMask.NameToLayer("Water");
        var heights = new List<float>();

        for (int x = 0; x < HeightSamples; x++)
        for (int z = 0; z < HeightSamples; z++)
        {
            var origin = new Vector3(
                Mathf.Lerp(bounds.min.x, bounds.max.x, (x + 0.5f) / HeightSamples),
                bounds.max.y + SkyHeight,
                Mathf.Lerp(bounds.min.z, bounds.max.z, (z + 0.5f) / HeightSamples));
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, SkyHeight * 2f + bounds.size.y, surfaceMask,
                    QueryTriggerInteraction.Ignore) && hit.collider.gameObject.layer != waterLayer)
                heights.Add(hit.point.y);
        }

        if (heights.Count == 0)
        {
            Debug.LogWarning($"{Tag} {area.name}: no ground inside the zone (over water?); left as it is.", area);
            return;
        }

        float min = heights.Min() - HeightMargin;
        float max = heights.Max() + HeightMargin;
        float height = Mathf.Max(MinZoneHeight, max - min);

        Undo.RecordObject(area.transform, "Fit zone height");
        Undo.RecordObject(area, "Fit zone height");
        Vector3 position = area.transform.position;
        area.transform.position = new Vector3(position.x, (min + max) * 0.5f, position.z);
        area.size = new Vector3(area.size.x, height, area.size.z);
        Debug.Log($"{Tag} {area.name}: height fitted to the ground ({heights.Count}/{HeightSamples * HeightSamples} samples, " +
                  $"y {min:0.#}..{max:0.#}).", area);
    }

    private static void ResetRotationAndScale(Transform t, bool keepWorldPosition)
    {
        Vector3 position = t.position;
        t.localRotation = Quaternion.identity;
        t.localScale = Vector3.one;
        if (keepWorldPosition) t.position = position;
    }
}
#endif
