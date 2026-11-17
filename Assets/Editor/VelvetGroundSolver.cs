#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.U2D;

/// <summary>
/// Evita que CUALQUIER parte del personaje atraviese el suelo en un clip.
/// Calcula la malla deformada real de todos los sprites (igual que el Sprite Skin),
/// busca el punto más bajo en cada cuadro y, si queda debajo del suelo, sube el hueso HIP
/// lo justo para que quede apoyado. El suelo es el punto más bajo del personaje en reposo.
/// </summary>
public static class VelvetGroundSolver
{
    const string PrefabPath = "Assets/Assets/CHARACTERS/PLAYER.prefab";
    const string DashPath = "Assets/Animators/Animations/PLAYER_Dash.anim";
    const int Samples = 60;

    class SkinData { public string name; public Transform[] bones; public Vector3[] verts; public BoneWeight[] w; public Matrix4x4[] bind; }

    [MenuItem("Tools/Velvet/Dash: que nada atraviese el suelo")]
    public static void SolveDash() => Solve(DashPath);

    public static void Solve(string clipPath) => Solve(PrefabPath, clipPath, "Logs/velvet_suelo_dash.txt");

    public static void Solve(string prefabPath, string clipPath, string logPath)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (clip == null) { Debug.LogError($"[Suelo] No encontré {clipPath}."); return; }
        var report = new StringBuilder();

        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            var skins = Collect(root);
            Transform hip = null;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.GetComponent<SpriteRenderer>() == null &&
                    System.Text.RegularExpressions.Regex.Replace(t.name.Trim(), @"_\d+$", "") == "HIP") { hip = t; break; }
            if (hip == null) { Debug.LogError("[Suelo] No encontré HIP."); return; }
            string hipPath = AnimationUtility.CalculateTransformPath(hip, root.transform);

            float ground = MinY(root.transform, skins);
            report.AppendLine($"{clip.name}: sprites {skins.Count}, suelo (reposo) {ground:F3}");

            var yBinding = EditorCurveBinding.FloatCurve(hipPath, typeof(Transform), "m_LocalPosition.y");
            var yCurve = AnimationUtility.GetEditorCurve(clip, yBinding);
            if (yCurve == null)
            {
                float y0 = hip.localPosition.y;
                yCurve = new AnimationCurve(new Keyframe(0f, y0), new Keyframe(clip.length, y0));
                foreach (var axis in new[] { "x", "z" })
                {
                    float v = axis == "x" ? hip.localPosition.x : hip.localPosition.z;
                    AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(hipPath, typeof(Transform), "m_LocalPosition." + axis),
                        new AnimationCurve(new Keyframe(0f, v), new Keyframe(clip.length, v)));
                }
            }

            // Cuánto hay que subir (en unidades del PLAYER) en cada muestra
            var lift = new float[Samples + 1];
            var names = new string[Samples + 1];
            for (int i = 0; i <= Samples; i++)
            {
                float time = clip.length * i / Samples;
                clip.SampleAnimation(root, time);
                float m = MinY(root.transform, skins);
                lift[i] = Mathf.Max(0f, ground - m);
                names[i] = lowestName;
            }
            // Margen entre muestras: toma el máximo con las vecinas para no dejar huecos
            var safe = new float[Samples + 1];
            for (int i = 0; i <= Samples; i++)
                safe[i] = Mathf.Max(lift[i], lift[Mathf.Max(0, i - 1)], lift[Mathf.Min(Samples, i + 1)]);

            // Convertir la subida (espacio del PLAYER) a espacio local del padre del HIP
            Vector3 upLocal = hip.parent.InverseTransformVector(root.transform.TransformVector(Vector3.up));
            float scaleY = upLocal.y;

            var curve = new AnimationCurve();
            float maxLift = 0f;
            for (int i = 0; i <= Samples; i++)
            {
                float time = clip.length * i / Samples;
                curve.AddKey(new Keyframe(time, yCurve.Evaluate(time) + safe[i] * scaleY));
                maxLift = Mathf.Max(maxLift, safe[i]);
                if (lift[i] > 0f) report.AppendLine($"t={time:F3} punto más bajo {ground - lift[i]:F3} ({names[i]}) -> sube {lift[i]:F3}");
            }
            for (int i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
            }
            AnimationUtility.SetEditorCurve(clip, yBinding, curve);
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssets();

            // Verificación con el clip ya corregido
            float worst = float.MaxValue;
            for (int i = 0; i <= Samples * 2; i++)
            {
                clip.SampleAnimation(root, clip.length * i / (Samples * 2));
                worst = Mathf.Min(worst, MinY(root.transform, skins) - ground);
            }
            report.AppendLine($"Subida máxima {maxLift:F3}. Verificación: lo más que algo queda bajo el suelo = {Mathf.Max(0f, -worst):F4}");
            Debug.Log($"[Suelo] {clip.name} corregido. Subida máx {maxLift:F2}. Peor penetración tras corregir: {Mathf.Max(0f, -worst):F4}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
            System.IO.File.WriteAllText(logPath, report.ToString());
        }
    }

    static List<SkinData> Collect(GameObject root)
    {
        var list = new List<SkinData>();
        foreach (var comp in root.GetComponentsInChildren<Component>(true))
        {
            if (comp == null || comp.GetType().Name != "SpriteSkin") continue;
            var sr = comp.GetComponent<SpriteRenderer>();
            if (sr == null || sr.sprite == null) continue;
            var so = new SerializedObject(comp);
            var arr = so.FindProperty("m_BoneTransforms");
            var bones = new Transform[arr.arraySize];
            for (int i = 0; i < bones.Length; i++) bones[i] = arr.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
            var s = sr.sprite;
            if (!s.HasVertexAttribute(VertexAttribute.BlendWeight)) continue;
            list.Add(new SkinData
            {
                name = sr.gameObject.name,
                bones = bones,
                verts = s.GetVertexAttribute<Vector3>(VertexAttribute.Position).ToArray(),
                w = s.GetVertexAttribute<BoneWeight>(VertexAttribute.BlendWeight).ToArray(),
                bind = s.GetBindPoses().ToArray(),
            });
        }
        return list;
    }

    static string lowestName = "";
    static float MinY(Transform root, List<SkinData> skins)
    {
        float min = float.MaxValue;
        Matrix4x4 toRoot = root.worldToLocalMatrix;
        foreach (var sk in skins)
        {
            var mats = new Matrix4x4[sk.bones.Length];
            for (int b = 0; b < mats.Length; b++)
                mats[b] = sk.bones[b] != null && b < sk.bind.Length ? toRoot * sk.bones[b].localToWorldMatrix * sk.bind[b] : Matrix4x4.identity;
            for (int v = 0; v < sk.verts.Length; v++)
            {
                var bw = sk.w[v]; var p = sk.verts[v];
                Vector3 q = bw.weight0 * mats[bw.boneIndex0].MultiplyPoint3x4(p)
                          + bw.weight1 * mats[bw.boneIndex1].MultiplyPoint3x4(p)
                          + bw.weight2 * mats[bw.boneIndex2].MultiplyPoint3x4(p)
                          + bw.weight3 * mats[bw.boneIndex3].MultiplyPoint3x4(p);
                if (q.y < min) { min = q.y; lowestName = sk.name; }
            }
        }
        return min;
    }
}
#endif
