#if UNITY_EDITOR
using CodenameVelvet.Cameras;
using CodenameVelvet.Enemies;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Gives the cyclops the same "Visual" child as every other sprite, so it leans back toward the camera:
/// <list type="number">
/// <item>CYCLOPS 2 (art prefab): a "Visual" child with ViewTilt now holds the skeleton (HIP) and the Sprites.</item>
/// <item>Boss_Cyclops: the Animator moves to Visual (clip paths such as "HIP/..." stay the same because both move
/// together), Animation Events are relayed back to the brain, and the left/right flip now happens on Visual.</item>
/// </list>
/// The root keeps the physics untilted. Safe to run again: it stops when Visual already exists.
/// </summary>
public static class VelvetTiltCyclops
{
    private const string ArtPrefabPath = "Assets/Assets/BOSS/CYCLOPS 2.prefab";
    private const string BossPrefabPath = "Assets/Prefabs/Enemies/Boss_Cyclops.prefab";
    private const string VisualName = "Visual";
    private static readonly string[] VisualChildren = { "HIP", "Sprites" };

    [MenuItem("Tools/Velvet/Enemies/Tilt Cyclops Visual")]
    public static void Run()
    {
        if (!AddVisualToArt()) return;
        MoveAnimatorToVisual();
        Debug.Log("[VelvetTiltCyclops] Done: the cyclops now leans toward the camera. Play the boss fight to check walk and stomp.");
    }

    private static bool AddVisualToArt()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(ArtPrefabPath);
        try
        {
            if (root.transform.Find(VisualName) != null)
            {
                Debug.Log("[VelvetTiltCyclops] CYCLOPS 2 already has a Visual; nothing to do.");
                return false;
            }

            var visual = new GameObject(VisualName);
            visual.transform.SetParent(root.transform, false);
            foreach (string childName in VisualChildren)
            {
                Transform child = root.transform.Find(childName);
                if (child == null)
                {
                    Debug.LogError($"[VelvetTiltCyclops] '{childName}' not found in CYCLOPS 2; nothing was changed.");
                    return false;
                }
                child.SetParent(visual.transform, false);
            }
            visual.AddComponent<ViewTilt>();

            PrefabUtility.SaveAsPrefabAsset(root, ArtPrefabPath);
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void MoveAnimatorToVisual()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(BossPrefabPath);
        try
        {
            Transform visual = root.transform.Find(VisualName);
            Animator oldAnimator = root.GetComponent<Animator>();
            Animator animator = visual.gameObject.AddComponent<Animator>();
            if (oldAnimator != null)
            {
                animator.runtimeAnimatorController = oldAnimator.runtimeAnimatorController;
                animator.avatar = oldAnimator.avatar;
                animator.applyRootMotion = oldAnimator.applyRootMotion;
                animator.updateMode = oldAnimator.updateMode;
                animator.cullingMode = oldAnimator.cullingMode;
                Object.DestroyImmediate(oldAnimator, true);
            }
            visual.gameObject.AddComponent<CyclopsAnimationEvents>();

            var brain = new SerializedObject(root.GetComponent<CyclopsBrain>());
            brain.FindProperty("animator").objectReferenceValue = animator;
            brain.FindProperty("spriteRoot").objectReferenceValue = visual;
            brain.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, BossPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
#endif
