#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D;

/// <summary>
/// Checks the Walk clip frame by frame and, when a toe would go below the ground, bends that foot up
/// (rotating at the ankle) just enough to keep the toe on the ground. Each foot's ground is its toe
/// height at rest. Only the L FOOT and R FOOT curves change.
/// </summary>
public static class VelvetWalkFootGround
{
    const string PrefabPath = "Assets/Assets/CHARACTERS/PLAYER.prefab";
    const string WalkPath = "Assets/Animators/Animations/PLAYER_Walk.anim";
    const int Samples = 48;
    static readonly string[] Feet = { "L FOOT", "R FOOT" };

    [MenuItem("Tools/Velvet/Walk: Keep Toes Above Ground")]
    public static void Fix()
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(WalkPath);
        if (clip == null) { Debug.LogError("[Ground] PLAYER_Walk not found."); return; }

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            GameObject root = VelvetGroundSolver.AnimatedRoot(prefabRoot);
            var report = new System.Text.StringBuilder();
            var feet = new List<(Transform t, float len, float floor, string path)>();
            foreach (var name in Feet)
            {
                Transform foot = FindBone(root.transform, name);
                if (foot == null) { Debug.LogWarning($"[Ground] {name} not found."); continue; }
                float len = BoneLength(root, foot, name);
                if (len <= 0f) { Debug.LogWarning($"[Ground] Could not read the length of {name}."); continue; }
                // Ground = toe height at rest, read before sampling the clip.
                float floor = Tip(root.transform, foot, len).y;
                report.AppendLine($"{name} rest: ankle {((Vector2)root.transform.InverseTransformPoint(foot.position)).y:F3}");
                feet.Add((foot, len, floor, AnimationUtility.CalculateTransformPath(foot, root.transform)));
                Debug.Log($"[Ground] {name}: length {len:F2}, ground {floor:F2}"); report.AppendLine($"{name}: length {len:F3}, ground {floor:F3}, path {AnimationUtility.CalculateTransformPath(foot, root.transform)}");
            }

            float duration = clip.length;
            var newValues = new Dictionary<string, float[]>();
            var bindings = new Dictionary<string, EditorCurveBinding>();
            foreach (var f in feet)
            {
                var b = EditorCurveBinding.FloatCurve(f.path, typeof(Transform), "localEulerAnglesRaw.z");
                if (AnimationUtility.GetEditorCurve(clip, b) == null) { Debug.LogWarning($"[Ground] Walk does not animate {f.path}."); continue; }
                bindings[f.path] = b;
                newValues[f.path] = new float[Samples + 1];
            }

            int fixedCount = 0;
            for (int i = 0; i <= Samples; i++)
            {
                float time = duration * i / Samples;
                clip.SampleAnimation(root, time);
                foreach (var f in feet)
                {
                    if (!bindings.ContainsKey(f.path)) continue;
                    float z = AnimationUtility.GetEditorCurve(clip, bindings[f.path]).Evaluate(time);
                    Vector2 ankle = root.transform.InverseTransformPoint(f.t.position);
                    Vector2 tip = Tip(root.transform, f.t, f.len);
                    report.AppendLine($"  t={time:F3} {f.path.Substring(f.path.LastIndexOf('/') + 1)} ankle {ankle.y:F3} toe {tip.y:F3}");
                    if (tip.y < f.floor)
                    {
                        Vector2 d = tip - ankle;
                        float r = d.magnitude;
                        float phi = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                        float s = Mathf.Clamp((f.floor - ankle.y) / r, -1f, 1f);
                        float target = Mathf.Asin(s) * Mathf.Rad2Deg;
                        if (d.x < 0f) target = 180f - target; // foot pointing backwards
                        z += Mathf.DeltaAngle(phi, target);
                        fixedCount++;
                        report.AppendLine($"t={time:F3} {f.path}: toe {tip.y:F3} < ground {f.floor:F3} -> +{Mathf.DeltaAngle(phi, target):F1}°");
                    }
                    newValues[f.path][i] = z;
                }
            }

            foreach (var kv in newValues)
            {
                var v = kv.Value;
                v[Samples] = v[0]; // seamless loop
                float dt = duration / Samples;
                var keys = new Keyframe[Samples + 1];
                for (int i = 0; i <= Samples; i++)
                {
                    float prev = v[(i - 1 + Samples) % Samples], next = v[(i + 1) % Samples];
                    float slope = (next - prev) / (2f * dt);
                    keys[i] = new Keyframe(i * dt, v[i % Samples], slope, slope);
                }
                AnimationUtility.SetEditorCurve(clip, bindings[kv.Key], new AnimationCurve(keys));
            }

            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Ground] Done: {fixedCount} Walk samples fixed.");
            report.AppendLine($"Fixed samples: {fixedCount}");
            System.IO.File.WriteAllText("Logs/velvet_ground_walk.txt", report.ToString());
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    // Bone tip (local X axis * length) in the animated root's space
    static Vector2 Tip(Transform root, Transform bone, float len) =>
        root.InverseTransformPoint(bone.TransformPoint(Vector3.right * len));

    // Bone length from the Sprite's SpriteBone, converted to Transform units
    static float BoneLength(GameObject root, Transform bone, string baseName)
    {
        foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr.sprite == null) continue;
            var bones = sr.sprite.GetBones();
            if (bones == null) continue;
            foreach (var sb in bones)
            {
                if (sb.name.Trim() != baseName) continue;
                float ratio = sb.position.magnitude > 0.0001f
                    ? bone.localPosition.magnitude / ((Vector2)sb.position).magnitude
                    : 1f;
                return sb.length * ratio;
            }
        }
        return -1f;
    }

    static Transform FindBone(Transform root, string baseName)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.GetComponent<SpriteRenderer>() != null) continue;
            string n = System.Text.RegularExpressions.Regex.Replace(t.name.Trim(), @"_\d+$", "").Trim();
            if (t.name.Trim() == baseName || n == baseName) return t;
        }
        return null;
    }
}
#endif
