#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.U2D;

/// <summary>
/// Renderiza cuadros de un clip del PLAYER a un PNG (tira de cuadros) para revisar la animación
/// sin entrar a Play. Deforma las mallas igual que el Sprite Skin.
/// </summary>
public static class VelvetPreviewRenderer
{
    const string PrefabPath = "Assets/Assets/CHARACTERS/PLAYER.prefab";

    [MenuItem("Tools/Velvet/Preview/Walk")] public static void Walk() => Render("Assets/Animators/Animations/PLAYER_Walk.anim", 8, "Logs/preview_walk.png");
    [MenuItem("Tools/Velvet/Preview/Idle")] public static void Idle() => Render("Assets/Animators/Animations/PLAYER_Idle.anim", 4, "Logs/preview_idle.png");
    [MenuItem("Tools/Velvet/Preview/Dash")] public static void Dash() => Render("Assets/Animators/Animations/PLAYER_Dash.anim", 10, "Logs/preview_dash.png");

    class Skin { public SpriteRenderer sr; public Transform[] bones; public Vector3[] verts; public BoneWeight[] w; public Matrix4x4[] bind; public Vector2[] uv; public int[] tris; public Material mat; }

    public static void Render(string clipPath, int frames, string outPath, bool includeRest = true) =>
        Render(PrefabPath, clipPath, frames, outPath, includeRest);

    [MenuItem("Tools/Velvet/Preview/Cíclope Idle")] public static void CyclopsIdle() => Render("Assets/Assets/BOSS/CYCLOPS 2.prefab", "Assets/Assets/BOSS/Idle_V1.anim", 4, "Logs/preview_cyclops_idle.png");
    [MenuItem("Tools/Velvet/Preview/Cíclope Walk")] public static void CyclopsWalk() => Render("Assets/Assets/BOSS/CYCLOPS 2.prefab", "Assets/Assets/BOSS/CYCLOPS_Walk.anim", 8, "Logs/preview_cyclops_walk.png");

    [MenuItem("Tools/Velvet/Preview/Cíclope Walk pose to pose")] public static void CyclopsWalkPoses() => Render("Assets/Assets/BOSS/CYCLOPS 2.prefab", "Assets/Assets/BOSS/CYCLOPS_Walk_Poses.anim", 8, "Logs/preview_cyclops_walk_poses.png");

    [MenuItem("Tools/Velvet/Preview/Cíclope Puñetazo")] public static void CyclopsPunch() => Render("Assets/Assets/BOSS/CYCLOPS 2.prefab", "Assets/Assets/BOSS/CYCLOPS_Attack_Punch.anim", 12, "Logs/preview_cyclops_punch.png");
    [MenuItem("Tools/Velvet/Preview/Cíclope Pisotón")] public static void CyclopsStomp() => Render("Assets/Assets/BOSS/CYCLOPS 2.prefab", "Assets/Assets/BOSS/CYCLOPS_Attack_Stomp.anim", 10, "Logs/preview_cyclops_stomp.png");

    public static void Render(string prefabPath, string clipPath, int frames, string outPath, bool includeRest = true)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
        var created = new List<Object>();
        try
        {
            GameObject root = VelvetGroundSolver.AnimatedRoot(prefabRoot);
            var skins = new List<Skin>();
            var shader = Shader.Find("Sprites/Default");
            foreach (var comp in root.GetComponentsInChildren<Component>(true))
            {
                if (comp == null || comp.GetType().Name != "SpriteSkin") continue;
                var sr = comp.GetComponent<SpriteRenderer>();
                if (sr == null || sr.sprite == null || !sr.enabled || !sr.gameObject.activeInHierarchy) continue;
                var so = new SerializedObject(comp);
                var arr = so.FindProperty("m_BoneTransforms");
                var bones = new Transform[arr.arraySize];
                for (int i = 0; i < bones.Length; i++) bones[i] = arr.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
                var s = sr.sprite;
                var mat = new Material(shader) { mainTexture = s.texture }; created.Add(mat);
                var tris = new List<int>(); foreach (var t in s.triangles) tris.Add(t);
                skins.Add(new Skin
                {
                    sr = sr, bones = bones, mat = mat, uv = s.uv, tris = tris.ToArray(),
                    verts = s.GetVertexAttribute<Vector3>(VertexAttribute.Position).ToArray(),
                    w = s.GetVertexAttribute<BoneWeight>(VertexAttribute.BlendWeight).ToArray(),
                    bind = s.GetBindPoses().ToArray(),
                });
            }
            skins.Sort((a, b) => a.sr.sortingOrder.CompareTo(b.sr.sortingOrder));

            // Poses: reposo + cuadros del clip
            var poses = new List<List<Vector3[]>>();
            var labels = new List<string>();
            if (includeRest) { poses.Add(Deform(root.transform, skins)); labels.Add("reposo"); }
            for (int f = 0; clip != null && f < frames; f++)
            {
                float t = clip.length * f / frames;
                clip.SampleAnimation(root, t);
                poses.Add(Deform(root.transform, skins));
                labels.Add(t.ToString("F2"));
            }

            // Encuadre común
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            float ground = float.MaxValue;
            foreach (var v in poses[0]) foreach (var p in v) ground = Mathf.Min(ground, p.y);
            foreach (var pose in poses) foreach (var v in pose) foreach (var p in v)
            { minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y); }
            float cellW = (maxX - minX) * 1.1f, cellH = (maxY - minY) * 1.1f;
            int n = poses.Count;
            int pxH = 420, pxW = Mathf.RoundToInt(pxH * cellW / cellH) * n;

            var rt = new RenderTexture(pxW, pxH, 24, RenderTextureFormat.ARGB32);
            var cmd = new CommandBuffer();
            cmd.SetRenderTarget(rt);
            cmd.ClearRenderTarget(true, true, Color.white);
            float left = minX - cellW * 0.05f, bottom = minY - cellH * 0.05f;
            var proj = Matrix4x4.Ortho(left, left + cellW * n, bottom, bottom + cellH, -10, 10);
            cmd.SetViewProjectionMatrices(Matrix4x4.identity, GL.GetGPUProjectionMatrix(proj, false));

            var lineMat = new Material(shader) { mainTexture = Texture2D.whiteTexture }; created.Add(lineMat);
            for (int i = 0; i < n; i++)
            {
                float ox = cellW * i;
                // línea de suelo y separador
                cmd.DrawMesh(Quad(left + ox, ground - cellH * 0.004f, cellW, cellH * 0.008f, new Color(0.85f, 0.2f, 0.2f), created), Matrix4x4.identity, lineMat);
                cmd.DrawMesh(Quad(left + ox, bottom, cellW * 0.003f, cellH, new Color(0.7f, 0.7f, 0.7f), created), Matrix4x4.identity, lineMat);
                for (int s = 0; s < skins.Count; s++)
                {
                    var mesh = new Mesh(); created.Add(mesh);
                    var vs = poses[i][s];
                    var shifted = new Vector3[vs.Length];
                    for (int k = 0; k < vs.Length; k++) shifted[k] = new Vector3(vs[k].x + ox, vs[k].y, 0f);
                    mesh.vertices = shifted; mesh.uv = skins[s].uv; mesh.triangles = skins[s].tris;
                    var cols = new Color[vs.Length]; for (int k = 0; k < cols.Length; k++) cols[k] = Color.white; mesh.colors = cols;
                    cmd.DrawMesh(mesh, Matrix4x4.identity, skins[s].mat);
                }
            }
            Graphics.ExecuteCommandBuffer(cmd);
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(pxW, pxH, TextureFormat.RGB24, false); created.Add(tex);
            tex.ReadPixels(new Rect(0, 0, pxW, pxH), 0, 0); tex.Apply();
            RenderTexture.active = prev; rt.Release(); created.Add(rt);
            System.IO.File.WriteAllBytes(outPath, tex.EncodeToPNG());
            System.IO.File.WriteAllText(outPath + ".txt", string.Join(" | ", labels));
            Debug.Log($"[Preview] {outPath} ({string.Join(", ", labels)})");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
            foreach (var o in created) if (o != null) Object.DestroyImmediate(o);
        }
    }

    static List<Vector3[]> Deform(Transform root, List<Skin> skins)
    {
        var res = new List<Vector3[]>();
        Matrix4x4 toRoot = root.worldToLocalMatrix;
        foreach (var sk in skins)
        {
            var mats = new Matrix4x4[sk.bones.Length];
            for (int b = 0; b < mats.Length; b++)
                mats[b] = sk.bones[b] != null && b < sk.bind.Length ? toRoot * sk.bones[b].localToWorldMatrix * sk.bind[b] : Matrix4x4.identity;
            var outV = new Vector3[sk.verts.Length];
            for (int v = 0; v < outV.Length; v++)
            {
                var bw = sk.w[v]; var p = sk.verts[v];
                outV[v] = bw.weight0 * mats[bw.boneIndex0].MultiplyPoint3x4(p) + bw.weight1 * mats[bw.boneIndex1].MultiplyPoint3x4(p)
                        + bw.weight2 * mats[bw.boneIndex2].MultiplyPoint3x4(p) + bw.weight3 * mats[bw.boneIndex3].MultiplyPoint3x4(p);
            }
            res.Add(outV);
        }
        return res;
    }

    static Mesh Quad(float x, float y, float w, float h, Color c, List<Object> created)
    {
        var m = new Mesh(); created.Add(m);
        m.vertices = new[] { new Vector3(x, y), new Vector3(x + w, y), new Vector3(x + w, y + h), new Vector3(x, y + h) };
        m.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
        m.colors = new[] { c, c, c, c };
        m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        return m;
    }
}
#endif
