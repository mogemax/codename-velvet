#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D;

/// <summary>
/// Revisa el Walk cuadro por cuadro y, si la punta de un pie bajaría del suelo,
/// dobla el pie hacia arriba (rotando desde el tobillo) justo lo necesario para que
/// la punta se quede apoyada. El "suelo" de cada pie es la altura de su punta en la pose de reposo.
/// Solo modifica las curvas de L FOOT y R FOOT; el resto del Walk queda igual.
/// </summary>
public static class VelvetWalkFootGround
{
    const string PrefabPath = "Assets/Assets/CHARACTERS/PLAYER.prefab";
    const string WalkPath = "Assets/Animators/Animations/PLAYER_Walk.anim";
    const int Samples = 48;
    static readonly string[] Feet = { "L FOOT", "R FOOT" };

    [MenuItem("Tools/Velvet/Walk: que las puntas no atraviesen el suelo")]
    public static void Fix()
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(WalkPath);
        if (clip == null) { Debug.LogError("[Suelo] No encontré PLAYER_Walk."); return; }

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            GameObject root = VelvetGroundSolver.AnimatedRoot(prefabRoot);
            var report = new System.Text.StringBuilder();
            var feet = new List<(Transform t, float len, float floor, string path)>();
            foreach (var name in Feet)
            {
                Transform foot = FindBone(root.transform, name);
                if (foot == null) { Debug.LogWarning($"[Suelo] No encontré {name}."); continue; }
                float len = BoneLength(root, foot, name);
                if (len <= 0f) { Debug.LogWarning($"[Suelo] No pude leer el largo de {name}."); continue; }
                // Suelo = altura de la punta en reposo (antes de muestrear el clip)
                float floor = Tip(root.transform, foot, len).y;
                report.AppendLine($"{name} reposo: tobillo {((Vector2)root.transform.InverseTransformPoint(foot.position)).y:F3}");
                feet.Add((foot, len, floor, AnimationUtility.CalculateTransformPath(foot, root.transform)));
                Debug.Log($"[Suelo] {name}: largo {len:F2}, suelo {floor:F2}"); report.AppendLine($"{name}: largo {len:F3}, suelo {floor:F3}, ruta {AnimationUtility.CalculateTransformPath(foot, root.transform)}");
            }

            float duration = clip.length;
            var newValues = new Dictionary<string, float[]>();
            var bindings = new Dictionary<string, EditorCurveBinding>();
            foreach (var f in feet)
            {
                var b = EditorCurveBinding.FloatCurve(f.path, typeof(Transform), "localEulerAnglesRaw.z");
                if (AnimationUtility.GetEditorCurve(clip, b) == null) { Debug.LogWarning($"[Suelo] El Walk no anima {f.path}."); continue; }
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
                    report.AppendLine($"  t={time:F3} {f.path.Substring(f.path.LastIndexOf('/') + 1)} tobillo {ankle.y:F3} punta {tip.y:F3}");
                    if (tip.y < f.floor)
                    {
                        Vector2 d = tip - ankle;
                        float r = d.magnitude;
                        float phi = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                        float s = Mathf.Clamp((f.floor - ankle.y) / r, -1f, 1f);
                        float target = Mathf.Asin(s) * Mathf.Rad2Deg;
                        if (d.x < 0f) target = 180f - target; // pie apuntando hacia atrás
                        z += Mathf.DeltaAngle(phi, target);
                        fixedCount++;
                        report.AppendLine($"t={time:F3} {f.path}: punta {tip.y:F3} < suelo {f.floor:F3} -> +{Mathf.DeltaAngle(phi, target):F1}°");
                    }
                    newValues[f.path][i] = z;
                }
            }

            foreach (var kv in newValues)
            {
                var v = kv.Value;
                v[Samples] = v[0]; // loop exacto
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
            Debug.Log($"[Suelo] Listo: {fixedCount} muestras corregidas en el Walk.");
            report.AppendLine($"Total corregidas: {fixedCount}");
            System.IO.File.WriteAllText("Logs/velvet_suelo.txt", report.ToString());
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    // Punta del hueso (eje X local del hueso * largo) en el espacio del PLAYER
    static Vector2 Tip(Transform root, Transform bone, float len) =>
        root.InverseTransformPoint(bone.TransformPoint(Vector3.right * len));

    // Largo del hueso tomado del Sprite (SpriteBone), convertido a unidades del Transform
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
