#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// El hueso R FOOT quedó apuntando hacia el talón en el rig original, así que el Walk
/// lo animaba volteado. Esto toma la rotación corregida del R FOOT_1 en la escena,
/// la pasa al prefab PLAYER y gira 180° la curva de R FOOT en PLAYER_Walk.
/// </summary>
public static class VelvetFixRightFoot
{
    const string PrefabPath = "Assets/Assets/CHARACTERS/PLAYER.prefab";
    const string WalkPath = "Assets/Animators/Animations/PLAYER_Walk.anim";
    const string BoneName = "R FOOT_1";

    [MenuItem("Tools/Velvet/Corregir pie derecho en Walk")]
    public static void Fix()
    {
        // 1) Rotación corregida desde la escena
        Transform sceneBone = null;
        foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
            if (go.name == "PLAYER") sceneBone = FindBone(go.transform);
        if (sceneBone == null) { Debug.LogError("[Pie] No encontré PLAYER/R FOOT_1 en la escena."); return; }
        Quaternion fixedRot = sceneBone.localRotation;
        float fixedZ = sceneBone.localEulerAngles.z;

        // 2) Pasarla al prefab
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform prefabBone = FindBone(root.transform);
            if (prefabBone == null) { Debug.LogError("[Pie] No encontré R FOOT_1 en el prefab."); return; }
            prefabBone.localRotation = fixedRot;
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }

        // 3) Girar la curva del Walk para que parta de la nueva rotación
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(WalkPath);
        if (clip == null) { Debug.LogError("[Pie] No encontré PLAYER_Walk."); return; }
        bool done = false;
        foreach (var b in AnimationUtility.GetCurveBindings(clip))
        {
            if (!b.path.EndsWith("/" + BoneName) || b.propertyName != "localEulerAnglesRaw.z") continue;
            var curve = AnimationUtility.GetEditorCurve(clip, b);
            float offset = Mathf.Round(Mathf.DeltaAngle(curve.keys[0].value, fixedZ) / 180f) * 180f;
            var keys = curve.keys;
            for (int i = 0; i < keys.Length; i++) keys[i].value += offset;
            curve.keys = keys;
            AnimationUtility.SetEditorCurve(clip, b, curve);
            Debug.Log($"[Pie] Curva de {BoneName} en Walk girada {offset}°.");
            done = true;
        }
        if (!done) Debug.LogWarning("[Pie] El Walk no tenía curva de R FOOT_1.");
        EditorUtility.SetDirty(clip);
        AssetDatabase.SaveAssets();
    }

    // Rotación original del R FOOT en el rig (apuntando hacia el talón).
    const float OriginalRightFootZ = -88.5991f;
    const string IdlePath = "Assets/Animators/Animations/PLAYER_Idle.anim";

    [MenuItem("Tools/Velvet/Idle con pie derecho original")]
    public static void IdleOriginalFoot()
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(IdlePath);
        if (clip == null) { Debug.LogError("[Pie] No encontré PLAYER_Idle."); return; }

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform bone = FindBone(root.transform);
            if (bone == null) { Debug.LogError("[Pie] No encontré R FOOT_1 en el prefab."); return; }
            string path = AnimationUtility.CalculateTransformPath(bone, root.transform);
            float len = clip.length > 0f ? clip.length : 2.4f;

            void Set(string prop, float v) => AnimationUtility.SetEditorCurve(clip,
                EditorCurveBinding.FloatCurve(path, typeof(Transform), prop),
                new AnimationCurve(new Keyframe(0f, v), new Keyframe(len, v)));

            Set("localEulerAnglesRaw.x", 0f);
            Set("localEulerAnglesRaw.y", 0f);
            Set("localEulerAnglesRaw.z", OriginalRightFootZ);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }

        EditorUtility.SetDirty(clip);
        AssetDatabase.SaveAssets();
        Debug.Log("[Pie] Idle: R FOOT_1 fijo en su rotación original.");
    }

    static Transform FindBone(Transform root)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name.Trim() == BoneName && t.GetComponent<SpriteRenderer>() == null) return t;
        return null;
    }
}
#endif
