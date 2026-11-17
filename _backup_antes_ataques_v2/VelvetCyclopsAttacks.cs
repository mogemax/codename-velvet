#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using CodenameVelvet.Enemies;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Genera las animaciones de ataque del cíclope (vista de frente) sincronizadas con los tiempos del
/// CyclopsBrain de la escena (windup = momento del golpe, windup + recovery = fin del clip):
///  - Puñetazo al suelo: alza el puño, se deja caer casi por completo sobre él y tarda en levantarse.
///  - Pisotón: levanta una pierna, la azota contra el suelo y el cuerpo rebota.
/// Cada clip lleva un Animation Event "AnimationEvent_Impact" en el cuadro del golpe y pasa por el
/// solver de suelo para que nada atraviese el piso.
/// </summary>
public static class VelvetCyclopsAttacks
{
    const string PrefabPath = "Assets/Assets/BOSS/CYCLOPS 2.prefab";
    const string ControllerPath = "Assets/Assets/BOSS/CYCLOPS_Final.controller";
    const string PunchPath = "Assets/Assets/BOSS/CYCLOPS_Attack_Punch.anim";
    const string StompPath = "Assets/Assets/BOSS/CYCLOPS_Attack_Stomp.anim";

    [MenuItem("Tools/Velvet/Cíclope: generar animaciones de ataque")]
    public static void Generate()
    {
        var report = new StringBuilder();
        var (pw, pr) = Timing("AttackPunch", 0.9f, 2.2f);
        var (sw, sr) = Timing("AttackStomp", 0.5f, 0.8f);
        report.AppendLine($"Puñetazo: golpe {pw}s, recuperación {pr}s · Pisotón: golpe {sw}s, recuperación {sr}s");

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        AnimationClip punch, stomp;
        try
        {
            var rig = new Rig(root);
            report.AppendLine($"Pierna {rig.Leg:F2}, lado L {rig.SideL}");
            punch = Save(BuildPunch(rig, pw, pr), PunchPath, pw);
            stomp = Save(BuildStomp(rig, sw, sr), StompPath, sw);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }

        AssignToController(punch, stomp, report);
        VelvetGroundSolver.Solve(PrefabPath, PunchPath, "Logs/velvet_suelo_ciclope_punch.txt");
        VelvetGroundSolver.Solve(PrefabPath, StompPath, "Logs/velvet_suelo_ciclope_stomp.txt");
        System.IO.File.WriteAllText("Logs/velvet_ciclope_ataques.txt", report.ToString());
        Debug.Log("[Cíclope] Animaciones de ataque listas.\n" + report);
    }

    // Lee windup/recovery del CyclopsBrain de la escena (si existe) para que el golpe cuadre.
    static (float windup, float recovery) Timing(string trigger, float defW, float defR)
    {
        var brain = Object.FindFirstObjectByType<CyclopsBrain>();
        if (brain == null) return (defW, defR);
        var arr = new SerializedObject(brain).FindProperty("attacks");
        for (int i = 0; i < arr.arraySize; i++)
        {
            var a = arr.GetArrayElementAtIndex(i);
            if (a.FindPropertyRelative("animatorTrigger").stringValue == trigger)
                return (a.FindPropertyRelative("windup").floatValue, a.FindPropertyRelative("recovery").floatValue);
        }
        return (defW, defR);
    }

    // ───────────────────────── PUÑETAZO AL SUELO ─────────────────────────
    static AnimationClip BuildPunch(Rig r, float W, float R)
    {
        // 0 neutral · 1 anticipación · 2 puño arriba · 3 baja con fuerza · 4 GOLPE · 5 se desploma
        // 6 empuja con los brazos · 7 medio levantado · 8 casi de pie · 9 neutral
        float[] t = { 0f, 0.39f * W, 0.78f * W, 0.89f * W, W, W + 0.11f * R, W + 0.32f * R, W + 0.64f * R, W + 0.84f * R, W + R };
        var c = new Clip(r, t);
        float L = r.Leg;
        float sR = r.SideR, sL = r.SideL;

        // Cadera: sube al cargar el golpe, se desploma casi hasta el suelo y se levanta despacio
        float[] hipY = M(new[] { 0f, 0.04f, 0.08f, 0f, -0.48f, -0.52f, -0.42f, -0.18f, -0.03f, 0f }, L);
        c.PosRoot("HIP", null, hipY);
        // Piernas: se doblan para absorber (los pies se quedan en el piso) con las rodillas abiertas
        foreach (var (pfx, side) in new[] { ("L", sL), ("R", sR) })
        {
            c.PosRoot(pfx + "_LEG_1", null, Neg(hipY));
            c.Rot(pfx + "_LEG_1", Map(hipY, h => side * 16f * Mathf.Max(0f, -h / L) / 0.52f));
            c.Rot(pfx + "_LEG_2", Map(hipY, h => -side * 12f * Mathf.Max(0f, -h / L) / 0.52f));
        }

        // Torso: se dobla hacia el frente (se acorta) y se inclina hacia el brazo que golpea
        c.PosRoot("BODY_1", null, M(new[] { 0f, 0.01f, 0.03f, -0.06f, -0.18f, -0.20f, -0.14f, -0.05f, 0f, 0f }, L));
        c.Rot("BODY_1", M(new[] { 0f, -4f, -8f, 0f, 8f, 10f, 7f, 3f, 0f, 0f }, -sR));
        c.Rot("BODY_2", M(new[] { 0f, -2f, -5f, -2f, 4f, 6f, 5f, 2f, 0f, 0f }, -sR));
        c.Rot("HEAD", M(new[] { 0f, 4f, 7f, 2f, -6f, -9f, -6f, -2f, 1f, 0f }, sR));

        // Brazo que golpea (R): sube por encima de la cabeza y baja recto al suelo frente a él
        c.Rot("R_ARM_1", M(new[] { 0f, 15f, 35f, 20f, -5f, -6f, -3f, 0f, 0f, 0f }, sR));
        c.Rot("R_ARM_2", M(new[] { 0f, 70f, 165f, 110f, -20f, -22f, -12f, -4f, 2f, 0f }, sR));
        c.Rot("R_ARM_3", M(new[] { 0f, 25f, 40f, 15f, 0f, 0f, 12f, 15f, 5f, 0f }, -sR));
        c.Rot("R_HAND_1", M(new[] { 0f, 10f, 20f, -10f, -5f, -5f, 0f, 5f, 0f, 0f }, sR));
        // El otro brazo se abre para equilibrar y luego se apoya para levantarse
        c.Rot("L_ARM_2", M(new[] { 0f, 15f, 30f, 20f, -14f, -16f, -10f, 0f, 0f, 0f }, sL));
        c.Rot("L_ARM_3", M(new[] { 0f, 10f, 15f, 5f, 15f, 20f, 25f, 10f, 3f, 0f }, -sL));

        // Orejas: se van hacia arriba al cargar y caen pesadas con el golpe
        foreach (var (pfx, side) in new[] { ("L", sL), ("R", sR) })
        {
            c.Rot(pfx + "_EAR_1", M(new[] { 0f, -3f, -6f, 0f, 10f, 14f, 6f, 2f, -2f, 0f }, side));
            c.Rot(pfx + "_EAR_2", M(new[] { 0f, -2f, -8f, -4f, 12f, 18f, 10f, 3f, -3f, 0f }, side));
        }
        return c.Build("CYCLOPS_Attack_Punch");
    }

    // ───────────────────────── PISOTÓN ─────────────────────────
    static AnimationClip BuildStomp(Rig r, float W, float R)
    {
        // 0 neutral · 1 empieza a levantar · 2 pierna arriba · 3 baja con fuerza · 4 GOLPE
        // 5 rebote · 6 segundo asentamiento · 7 casi neutral · 8 neutral
        float[] t = { 0f, 0.30f * W, 0.64f * W, 0.86f * W, W, W + 0.12f * R, W + 0.31f * R, W + 0.56f * R, W + R };
        var c = new Clip(r, t);
        float L = r.Leg;
        float sR = r.SideR, sL = r.SideL;

        float[] footL = M(new[] { 0f, 0.22f, 0.55f, 0.40f, 0f, 0f, 0f, 0f, 0f }, L); // pierna L = la que pisa
        float[] hipY = M(new[] { 0f, 0.03f, 0.08f, 0.04f, -0.16f, -0.07f, -0.11f, -0.03f, 0f }, L);
        float[] hipX = M(new[] { 0f, -0.05f, -0.08f, -0.05f, 0f, 0.02f, 0f, 0f, 0f }, L * sL); // carga el peso en la R

        c.PosRoot("HIP", hipX, hipY);
        var legL = new float[t.Length]; var legR = new float[t.Length];
        var bend = new float[t.Length];
        for (int i = 0; i < t.Length; i++) { legL[i] = footL[i] - hipY[i]; legR[i] = -hipY[i]; bend[i] = footL[i] / (0.55f * L); }
        c.PosRoot("L_LEG_1", null, legL);
        c.PosRoot("R_LEG_1", null, legR);
        c.Rot("L_LEG_1", Map(bend, b => sL * 16f * b));
        c.Rot("L_LEG_2", Map(bend, b => -sL * 28f * b));
        c.Rot("L_FOOT", Map(bend, b => sL * 8f * b));
        // Al aterrizar las dos rodillas se abren un poco por el peso
        c.Rot("R_LEG_1", Map(hipY, h => h < 0 ? sR * 10f * (-h / (0.16f * L)) : 0f));

        // Torso: se inclina sobre la pierna de apoyo y se azota hacia abajo con el golpe
        c.Rot("BODY_1", M(new[] { 0f, 3f, 6f, 4f, -3f, -1f, -2f, 0f, 0f }, sL));
        c.Rot("BODY_2", M(new[] { 0f, 1f, 4f, 4f, -2f, -3f, -1f, 0f, 0f }, sL));
        c.PosRoot("BODY_1", null, M(new[] { 0f, 0f, 0.01f, 0f, -0.05f, -0.02f, -0.03f, 0f, 0f }, L));
        c.Rot("HEAD", M(new[] { 0f, 2f, 4f, 2f, -6f, -2f, -3f, 0f, 0f }, -sL));

        // Brazos: se abren para equilibrarse y caen con el impacto
        foreach (var (pfx, side) in new[] { ("L", sL), ("R", sR) })
        {
            c.Rot(pfx + "_ARM_2", M(new[] { 0f, 25f, 55f, 40f, -8f, 6f, 0f, 0f, 0f }, side));
            c.Rot(pfx + "_ARM_3", M(new[] { 0f, 8f, 20f, 25f, 5f, 10f, 4f, 0f, 0f }, -side));
            c.Rot(pfx + "_EAR_1", M(new[] { 0f, -2f, -5f, -3f, 10f, 4f, 7f, 1f, 0f }, side));
            c.Rot(pfx + "_EAR_2", M(new[] { 0f, -1f, -6f, -6f, 8f, 12f, 5f, 2f, 0f }, side));
        }
        return c.Build("CYCLOPS_Attack_Stomp");
    }

    // ───────────────────────── helpers ─────────────────────────
    static float[] M(float[] a, float k) { var r = new float[a.Length]; for (int i = 0; i < a.Length; i++) r[i] = a[i] * k; return r; }
    static float[] Neg(float[] a) => M(a, -1f);
    static float[] Map(float[] a, System.Func<float, float> f) { var r = new float[a.Length]; for (int i = 0; i < a.Length; i++) r[i] = f(a[i]); return r; }

    class Rig
    {
        public readonly GameObject Root; public readonly float Leg, SideL, SideR;
        public Rig(GameObject root)
        {
            Root = root;
            Transform hip = Find("HIP"), leg = Find("L_LEG_1"), foot = Find("L_FOOT");
            Leg = Vector3.Distance(root.transform.InverseTransformPoint(leg.position), root.transform.InverseTransformPoint(foot.position));
            SideL = Mathf.Sign(root.transform.InverseTransformPoint(leg.position).x - root.transform.InverseTransformPoint(hip.position).x);
            SideR = -SideL;
        }
        public Transform Find(string n)
        {
            foreach (var t in Root.GetComponentsInChildren<Transform>(true))
                if (t.name == n && t.GetComponent<SpriteRenderer>() == null) return t;
            return null;
        }
    }

    class Clip
    {
        readonly Rig rig; readonly float[] times;
        readonly AnimationClip clip = new AnimationClip { frameRate = 30f };
        readonly Dictionary<Transform, (float[] x, float[] y)> pos = new Dictionary<Transform, (float[] x, float[] y)>();
        public Clip(Rig r, float[] t) { rig = r; times = t; }

        public void Rot(string bone, float[] delta)
        {
            var t = rig.Find(bone); if (t == null) { Debug.LogWarning($"[Cíclope] No encontré {bone}"); return; }
            string path = AnimationUtility.CalculateTransformPath(t, rig.Root.transform);
            Vector3 e = t.localEulerAngles;
            Set(path, "localEulerAnglesRaw.x", Const(e.x));
            Set(path, "localEulerAnglesRaw.y", Const(e.y));
            Set(path, "localEulerAnglesRaw.z", Keys(e.z, delta));
        }

        // Desplazamiento en el espacio del root (x = lado, y = arriba), acumulable por hueso
        public void PosRoot(string bone, float[] dx, float[] dy)
        {
            var t = rig.Find(bone); if (t == null) { Debug.LogWarning($"[Cíclope] No encontré {bone}"); return; }
            int n = times.Length;
            var cur = pos.TryGetValue(t, out var v) ? v : (new float[n], new float[n]);
            for (int i = 0; i < n; i++) { if (dx != null) cur.Item1[i] += dx[i]; if (dy != null) cur.Item2[i] += dy[i]; }
            pos[t] = cur;
            Vector3 ax = t.parent.InverseTransformVector(rig.Root.transform.TransformVector(Vector3.right));
            Vector3 ay = t.parent.InverseTransformVector(rig.Root.transform.TransformVector(Vector3.up));
            var lx = new float[n]; var ly = new float[n];
            for (int i = 0; i < n; i++) { Vector3 d = ax * cur.Item1[i] + ay * cur.Item2[i]; lx[i] = d.x; ly[i] = d.y; }
            string path = AnimationUtility.CalculateTransformPath(t, rig.Root.transform);
            Vector3 p = t.localPosition;
            Set(path, "m_LocalPosition.x", Keys(p.x, lx));
            Set(path, "m_LocalPosition.y", Keys(p.y, ly));
            Set(path, "m_LocalPosition.z", Const(p.z));
        }

        public AnimationClip Build(string name) { clip.name = name; return clip; }

        void Set(string path, string prop, AnimationCurve c) =>
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), prop), c);
        AnimationCurve Const(float v) => new AnimationCurve(new Keyframe(0f, v), new Keyframe(times[times.Length - 1], v));
        AnimationCurve Keys(float b, float[] d)
        {
            var c = new AnimationCurve();
            for (int i = 0; i < times.Length; i++) c.AddKey(new Keyframe(times[i], b + d[i]));
            for (int i = 0; i < c.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(c, i, AnimationUtility.TangentMode.ClampedAuto);
                AnimationUtility.SetKeyRightTangentMode(c, i, AnimationUtility.TangentMode.ClampedAuto);
            }
            return c;
        }
    }

    static AnimationClip Save(AnimationClip clip, string path, float impactTime)
    {
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        AnimationUtility.SetAnimationEvents(clip, new[] { new AnimationEvent { time = impactTime, functionName = "AnimationEvent_Impact" } });
        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (existing != null) { EditorUtility.CopySerialized(clip, existing); EditorUtility.SetDirty(existing); return existing; }
        AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    static void AssignToController(AnimationClip punch, AnimationClip stomp, StringBuilder report)
    {
        var c = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        foreach (var s in c.layers[0].stateMachine.states)
        {
            if (s.state.name == "Attack_Punch") s.state.motion = punch;
            if (s.state.name == "Attack_Stomp") s.state.motion = stomp;
        }
        EditorUtility.SetDirty(c);
        AssetDatabase.SaveAssets();
        report.AppendLine("Clips asignados a Attack_Punch y Attack_Stomp");
    }
}
#endif
