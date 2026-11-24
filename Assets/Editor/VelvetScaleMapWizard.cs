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
/// Groups the open scene's map pieces (root objects on the Ground or Water layer) under an "Environment" object
/// at the origin, then sets its uniform scale and its yaw. The camera always looks toward +Z, so the yaw is how a
/// level is turned to be seen from the side it was designed for.
/// <list type="bullet">
/// <item>Scale and yaw are absolute: applying again with the same values changes nothing, and pieces added later
/// keep the pose they had when grouped.</item>
/// <item>Scale only affects the map: gameplay objects are character-sized and are placed again after scaling.</item>
/// <item>Yaw turns the gameplay objects with the map (spawn points, SpawnZones, camera framing zones and the
/// directional light), so nothing has to be placed again. Zones stay axis-aligned: they move and, on a quarter
/// turn, swap their length and width.</item>
/// </list>
/// </summary>
public class VelvetScaleMapWizard : ScriptableWizard
{
    private const string EnvironmentName = "Environment";
    private const string Tag = "[VelvetScaleMapWizard]";

    [Tooltip("Uniform scale of the Environment object. Level 1 matches level 2 at about 22.")]
    [SerializeField, Min(0.01f)] private float factor = 22f;
    [Tooltip("Degrees the map turns around the origin. The camera looks toward +Z; level 1 is seen from its side at 90.")]
    [SerializeField] private float yaw = 90f;

    [MenuItem("Tools/Velvet/Map/Group And Scale Map...")]
    private static void Open() => DisplayWizard<VelvetScaleMapWizard>("Group And Scale Map", "Apply");

    private void OnWizardCreate()
    {
        Scene scene = SceneManager.GetActiveScene();
        int ground = LayerMask.NameToLayer("Ground");
        int water = LayerMask.NameToLayer("Water");

        GameObject environment = scene.GetRootGameObjects().FirstOrDefault(go => go.name == EnvironmentName);
        if (environment == null)
        {
            environment = new GameObject(EnvironmentName);
            Undo.RegisterCreatedObjectUndo(environment, "Create Environment");
            SceneManager.MoveGameObjectToScene(environment, scene);
        }

        Transform root = environment.transform;
        Undo.RecordObject(root, "Scale and turn map");
        float currentScale = root.localScale.x;
        float turn = Mathf.DeltaAngle(root.eulerAngles.y, yaw);

        GameObject[] pieces = scene.GetRootGameObjects()
            .Where(go => go != environment && (go.layer == ground || go.layer == water))
            .ToArray();
        foreach (GameObject piece in pieces)
            Undo.SetTransformParent(piece.transform, root, true, "Group map");

        // The pivot stays at the origin so the sea level (y = 0) does not move.
        root.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, yaw, 0f));
        root.localScale = Vector3.one * factor;

        int turned = Mathf.Abs(turn) > 0.01f ? TurnGameplayObjects(scene, turn) : 0;

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"{Tag} {pieces.Length} new piece(s) grouped under '{EnvironmentName}'; scale {factor} " +
                  $"(×{factor / Mathf.Max(0.0001f, currentScale):0.##}), yaw {yaw} ({turn:+0;-0}°, {turned} gameplay object(s) turned). " +
                  "Save the scene.");
    }

    private static int TurnGameplayObjects(Scene scene, float turn)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        T[] Find<T>() where T : Component => roots.SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();

        var moved = new List<Transform>();
        foreach (PlayerSpawner spawner in Find<PlayerSpawner>())
        {
            moved.Add(spawner.transform);
            SerializedProperty points = new SerializedObject(spawner).FindProperty("spawnPoints");
            for (int i = 0; i < points.arraySize; i++)
                if (points.GetArrayElementAtIndex(i).objectReferenceValue is Transform point)
                    moved.Add(point.parent != null && point.parent != spawner.transform ? point.parent : point);
        }

        BoxCollider[] zones = Find<SpawnZone>().Select(z => z.GetComponent<BoxCollider>())
            .Concat(Find<CameraFramingZone>().Select(z => z.GetComponent<BoxCollider>()))
            .Where(box => box != null)
            .ToArray();
        moved.AddRange(zones.Select(box => box.transform));

        // Only the outermost: a child already turns with its parent.
        Transform[] outermost = moved.Distinct()
            .Where(t => !moved.Any(other => other != t && t.IsChildOf(other)))
            .ToArray();

        Quaternion rotation = Quaternion.Euler(0f, turn, 0f);
        foreach (Transform t in outermost)
        {
            Undo.RecordObject(t, "Turn map");
            t.position = rotation * t.position; // stays axis-aligned: only its place turns
        }

        bool quarterTurn = Mathf.Abs(Mathf.Abs(Mathf.DeltaAngle(0f, turn)) - 90f) < 45f;
        if (quarterTurn)
            foreach (BoxCollider box in zones)
            {
                Undo.RecordObject(box, "Turn map");
                box.size = new Vector3(box.size.z, box.size.y, box.size.x);
                box.center = rotation * box.center;
            }

        Light[] suns = Find<Light>().Where(light => light.type == LightType.Directional).ToArray();
        foreach (Light sun in suns)
        {
            Undo.RecordObject(sun.transform, "Turn map");
            sun.transform.RotateAround(Vector3.zero, Vector3.up, turn); // same shadows relative to the island
        }

        return outermost.Length + suns.Length;
    }
}
#endif
