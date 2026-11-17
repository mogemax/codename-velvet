#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Copia al prefab PLAYER (desempaquetado) los huesos nuevos de la capa que se crearon en el
/// Skinning Editor del PSB, y reconecta los Sprite Skin de los sprites para que usen exactamente
/// los mismos huesos (en el mismo orden) que el prefab que genera el importador del PSB.
/// Después regenera Idle/Walk/Dash con la capa animada y vuelve a aplicar las correcciones.
/// </summary>
public static class VelvetCapeSync
{
    const string PsdPath = "Assets/Assets/CHARACTERS/Ilustración_sin_título.psd";
    const string PrefabPath = "Assets/Assets/CHARACTERS/PLAYER.prefab";

    [MenuItem("Tools/Velvet/Capa: sincronizar huesos y animar")]
    public static void SyncAndAnimate()
    {
        if (!Sync()) return;
        VelvetPlayerIdleGenerator.Generate();                                // Idle, Walk, Dash (+ capa)
        VelvetFixRightFoot.IdleOriginalFoot();                               // pies opuestos en el Idle
        VelvetWalkFootGround.Fix();                                          // puntas sobre el suelo en el Walk
        VelvetGroundSolver.Solve("Assets/Animators/Animations/PLAYER_Dash.anim"); // nada bajo el suelo en el Dash
        Debug.Log("[Capa] Todo listo.");
    }

    public static bool Sync()
    {
        var report = new StringBuilder();
        var psb = AssetDatabase.LoadAssetAtPath<GameObject>(PsdPath);
        if (psb == null) { Debug.LogError("[Capa] No pude cargar el prefab del PSB."); return false; }

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            // 1) Huesos de la capa: crear los que falten con la pose de reposo del PSB
            var capeBones = new List<Transform>();
            foreach (var t in psb.GetComponentsInChildren<Transform>(true))
                if (t.name.ToUpper().Contains("CAPE") && t.GetComponent<SpriteRenderer>() == null) capeBones.Add(t);
            capeBones.Sort((a, b) => Depth(a).CompareTo(Depth(b)));
            report.AppendLine($"Huesos CAPE en el PSB: {capeBones.Count}");

            foreach (var src in capeBones)
            {
                string path = AnimationUtility.CalculateTransformPath(src, psb.transform);
                string parentPath = AnimationUtility.CalculateTransformPath(src.parent, psb.transform);
                Transform parent = string.IsNullOrEmpty(parentPath) ? root.transform : root.transform.Find(parentPath);
                if (parent == null) { report.AppendLine($"  ERROR: no encontré el padre '{parentPath}' para {path}"); continue; }
                Transform dst = parent.Find(src.name);
                if (dst == null)
                {
                    dst = new GameObject(src.name).transform;
                    dst.SetParent(parent, false);
                    report.AppendLine($"  creado {path}");
                }
                else report.AppendLine($"  ya existía {path} (se actualiza su reposo)");
                dst.localPosition = src.localPosition;
                dst.localRotation = src.localRotation;
                dst.localScale = src.localScale;
            }

            // 2) Reconectar los Sprite Skin con los mismos huesos que usa el PSB
            Transform spritesRoot = root.transform.Find("SPRITES");
            int changed = 0;
            foreach (var srcComp in psb.GetComponentsInChildren<Component>(true))
            {
                if (srcComp == null || srcComp.GetType().Name != "SpriteSkin") continue;
                string spriteName = srcComp.gameObject.name;
                Transform dstSprite = spritesRoot != null ? spritesRoot.Find(spriteName) : null;
                if (dstSprite == null) { report.AppendLine($"  aviso: no encontré el sprite {spriteName} en SPRITES"); continue; }
                Component dstComp = null;
                foreach (var c in dstSprite.GetComponents<Component>()) if (c != null && c.GetType().Name == "SpriteSkin") dstComp = c;
                if (dstComp == null) { report.AppendLine($"  aviso: {spriteName} no tiene Sprite Skin"); continue; }

                var srcSo = new SerializedObject(srcComp);
                var dstSo = new SerializedObject(dstComp);
                var srcArr = srcSo.FindProperty("m_BoneTransforms");
                var dstArr = dstSo.FindProperty("m_BoneTransforms");

                var newBones = new List<Transform>();
                bool ok = true;
                for (int i = 0; i < srcArr.arraySize; i++)
                {
                    var b = srcArr.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
                    string p = b != null ? AnimationUtility.CalculateTransformPath(b, psb.transform) : null;
                    Transform mapped = p != null ? root.transform.Find(p) : null;
                    if (mapped == null) { ok = false; report.AppendLine($"  ERROR {spriteName}: no encontré el hueso '{p}'"); }
                    newBones.Add(mapped);
                }
                if (!ok) continue;

                bool same = dstArr.arraySize == newBones.Count;
                for (int i = 0; same && i < newBones.Count; i++)
                    same = dstArr.GetArrayElementAtIndex(i).objectReferenceValue == newBones[i];
                if (same) continue;

                dstArr.arraySize = newBones.Count;
                for (int i = 0; i < newBones.Count; i++) dstArr.GetArrayElementAtIndex(i).objectReferenceValue = newBones[i];
                var srcRoot = srcSo.FindProperty("m_RootBone").objectReferenceValue as Transform;
                if (srcRoot != null)
                {
                    var r = root.transform.Find(AnimationUtility.CalculateTransformPath(srcRoot, psb.transform));
                    if (r != null) dstSo.FindProperty("m_RootBone").objectReferenceValue = r;
                }
                dstSo.ApplyModifiedPropertiesWithoutUndo();
                changed++;
                var names = new List<string>();
                foreach (var nb in newBones) names.Add(nb.name);
                report.AppendLine($"  Sprite Skin actualizado: {spriteName} -> [{string.Join(", ", names)}]");
            }
            report.AppendLine($"Sprite Skins actualizados: {changed}");
            ApplyCapeProxies(root, report);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("[Capa] Huesos sincronizados.\n" + report);
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
            System.IO.File.WriteAllText("Logs/velvet_capa.txt", report.ToString());
        }
    }

    // Huesos de los que la capa SÍ puede depender directamente
    static readonly string[] CapeAllowed = { "HIP", "BODY", "HEAD" };

    /// <summary>
    /// La capa (Capa 35) tiene pesos heredados de brazos y botas, lo que la estira al caminar/rodar.
    /// Sin tocar los pesos: cada uno de esos huesos se sustituye, solo para la capa, por un "PROXY"
    /// colocado exactamente en su pose de reposo pero colgado del hueso de capa más cercano.
    /// En reposo la capa se ve idéntica, y al animar sigue a la capa en vez de a las extremidades.
    /// </summary>
    static void ApplyCapeProxies(GameObject root, StringBuilder report)
    {
        Transform cape = root.transform.Find("SPRITES/Capa 35");
        if (cape == null) { report.AppendLine("Proxies: no encontré SPRITES/Capa 35"); return; }
        Component skin = null;
        foreach (var c in cape.GetComponents<Component>()) if (c != null && c.GetType().Name == "SpriteSkin") skin = c;
        if (skin == null) return;

        var capeBones = new List<Transform>();
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name.ToUpper().Contains("CAPE") && !t.name.StartsWith("PROXY") && t.GetComponent<SpriteRenderer>() == null) capeBones.Add(t);
        if (capeBones.Count == 0) { report.AppendLine("Proxies: no hay huesos CAPE"); return; }

        var so = new SerializedObject(skin);
        var arr = so.FindProperty("m_BoneTransforms");
        for (int i = 0; i < arr.arraySize; i++)
        {
            var b = arr.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
            if (b == null || b.name.StartsWith("PROXY") || b.name.ToUpper().Contains("CAPE")) continue;
            string baseName = System.Text.RegularExpressions.Regex.Replace(b.name.Trim(), @"_\d+$", "");
            if (System.Array.IndexOf(CapeAllowed, baseName) >= 0) continue;

            // Hueso de capa más cercano a este hueso (en reposo)
            Transform best = null; float bestD = float.MaxValue;
            foreach (var cb in capeBones)
            {
                float d = Vector3.Distance(cb.position, b.position);
                if (d < bestD) { bestD = d; best = cb; }
            }
            string proxyName = "PROXY " + b.name;
            Transform proxy = best.Find(proxyName);
            if (proxy == null) { proxy = new GameObject(proxyName).transform; }
            proxy.SetParent(best, false);
            proxy.position = b.position; proxy.rotation = b.rotation;
            proxy.localScale = Vector3.one;
            arr.GetArrayElementAtIndex(i).objectReferenceValue = proxy;
            report.AppendLine($"  Proxy: {b.name} -> {proxyName} (bajo {best.name})");
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static int Depth(Transform t) { int d = 0; while (t.parent != null) { d++; t = t.parent; } return d; }
}
#endif
