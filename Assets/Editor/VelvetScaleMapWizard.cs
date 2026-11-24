#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Groups the open scene's map pieces (root objects on the Ground or Water layer) under an "Environment" object
/// at the origin and sets its uniform scale. The scale is absolute: applying again with the same factor changes
/// nothing, and pieces added later keep the size they had when grouped. Gameplay objects (spawn points, SpawnZones) are left out on purpose: their sizes are character-sized,
/// so they are placed again after scaling.
/// </summary>
public class VelvetScaleMapWizard : ScriptableWizard
{
    private const string EnvironmentName = "Environment";

    [Tooltip("Uniform scale of the Environment object. Level 1 matches level 2 at about 22.")]
    [SerializeField, Min(0.01f)] private float factor = 22f;

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

        // The pivot stays at the origin so the sea level (y = 0) does not move.
        Undo.RecordObject(environment.transform, "Scale map");
        Vector3 currentScale = environment.transform.localScale;
        environment.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        GameObject[] pieces = scene.GetRootGameObjects()
            .Where(go => go != environment && (go.layer == ground || go.layer == water))
            .ToArray();
        foreach (GameObject piece in pieces)
            Undo.SetTransformParent(piece.transform, environment.transform, true, "Group map");

        // Pieces kept their world size when parented, so everything grows by factor / current scale.
        float relative = factor / Mathf.Max(0.0001f, currentScale.x);
        environment.transform.localScale = Vector3.one * factor;

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"[VelvetScaleMapWizard] {pieces.Length} map piece(s) grouped under '{EnvironmentName}', scale {factor} " +
                  $"(×{relative:0.##} from before). Place the spawn points and SpawnZones again, then save the scene.");
    }
}
#endif
