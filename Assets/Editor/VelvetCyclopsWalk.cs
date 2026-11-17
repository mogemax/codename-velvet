#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Walk cycle del cíclope (vista de frente, gigante pesado), pose to pose:
/// por cada paso: 0 Contact · 1 Recoil · 2 Passing (pierna libre arriba) · 3 High-Point.
/// - La pierna libre se levanta subiendo el hueso del muslo (la malla se acorta = la rodilla viene hacia cámara).
/// - La cadera se desplaza sobre la pierna de apoyo, el torso se inclina hacia ella y la cabeza compensa.
/// - Los brazos se balancean opuestos a las piernas. Orejas con follow-through.
/// Las amplitudes se escalan con el largo de la pierna, así que funcionan sin importar las unidades del rig.
/// </summary>
public static class VelvetCyclopsWalk
{
    const string PrefabPath = "Assets/Assets/BOSS/CYCLOPS 2.prefab";
    const string ClipPath = "Assets/Assets/BOSS/CYCLOPS_Walk.anim";
    const string ControllerPath = "Assets/Assets/BOSS/CYCLOPS_Final.controller";
    static float Duration = 1.4f; // 2 pasos (gigante: lento). La versión pose to pose usa 1.0 s (24 cuadros)
    static bool linearSpacing = false; // "inbetweens on thirds": espaciado parejo entre keys

    [MenuItem("Tools/Velvet/Cíclope: generar Walk")]
    public static void Generate()
    {
        var report = new StringBuilder();
        pending.Clear();
        Duration = 1.4f; linearSpacing = false;
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform R(string n) => Find(root.transform, n);
            Transform hip = R("HIP");
            Transform lLeg = R("L_LEG_1"), rLeg = R("R_LEG_1");
            if (hip == null || lLeg == null || rLeg == null) { Debug.LogError("[Cíclope] No encontré HIP/L_LEG_1/R_LEG_1."); return; }

            // Escala: largo de la pierna (cadera → pie) en espacio local del HIP
            Transform lFoot = R("L_FOOT");
            float legLen = Vector3.Distance(root.transform.InverseTransformPoint(lLeg.position), root.transform.InverseTransformPoint(lFoot.position));
            // Lado de cada pierna en X (respecto a la cadera, en espacio del root)
            float sideL = Mathf.Sign(root.transform.InverseTransformPoint(lLeg.position).x - root.transform.InverseTransformPoint(hip.position).x);
            float sideR = -sideL;
            report.AppendLine($"Largo pierna {legLen:F3}, lado L {sideL}, lado R {sideR}");

            var clip = new AnimationClip { name = "CYCLOPS_Walk", frameRate = 30f };

            // Paso 1 (poses 0-3): se levanta la R, apoya la L.  Paso 2 (poses 4-7): se levanta la L, apoya la R.
            float[] liftR = { 0f, 0.35f, 1f, 0.45f, 0f, 0f, 0f, 0f };
            float[] liftL = { 0f, 0f, 0f, 0f, 0f, 0.35f, 1f, 0.45f };
            // Peso hacia la pierna de apoyo: +1 = sobre la L
            float[] weight = { 0f, 0.5f, 1f, 0.6f, 0f, -0.5f, -1f, -0.6f };

            float lift = legLen * 0.16f;   // cuánto sube el muslo
            float shift = legLen * 0.06f;  // desplazamiento lateral de cadera
            float bob = legLen * 0.035f;   // rebote vertical

            // ── Piernas ──
            foreach (var (leg, side, lf) in new[] { (lLeg, sideL, liftL), (rLeg, sideR, liftR) })
            {
                string pfx = leg.name.StartsWith("L") ? "L" : "R";
                PosY(clip, root, leg, Scale(lf, lift));
                // Al levantarse, el muslo abre un poco hacia afuera y la espinilla regresa (rodilla doblada)
                Rot(clip, root, leg, Scale(lf, 6f * side));
                Rot(clip, root, R(pfx + "_LEG_2"), Scale(lf, -10f * side));
                Rot(clip, root, R(pfx + "_FOOT"), Scale(lf, 4f * side));
            }

            // ── Cadera: se va sobre la pierna de apoyo y sube un poco al pasar la pierna ──
            PosX(clip, root, hip, Scale(weight, shift * sideL));
            PosYAdd(clip, root, hip, new[] { 0f, -0.3f, 0.6f, 0.4f, 0f, -0.3f, 0.6f, 0.4f }, bob);

            // ── Torso: se inclina hacia el apoyo (+rot = la punta va a -X) y la cabeza compensa ──
            float[] lean = Scale(weight, -2.5f * sideL);
            Rot(clip, root, R("BODY_1"), lean);
            Rot(clip, root, R("BODY_2"), Scale(Shift(weight, 7), -1.5f * sideL)); // con un poco de retraso
            Rot(clip, root, R("HEAD"), Scale(Shift(weight, 7), 3f * sideL));      // compensa para quedar nivelada

            // ── Brazos: se balancean opuestos a la pierna que avanza; el codo se dobla al ir al frente ──
            float[] swingL = { 0f, 0.5f, 1f, 0.5f, 0f, -0.5f, -1f, -0.5f };   // brazo L adelante cuando avanza la R
            foreach (var (pfx, side, sw) in new[] { ("L", sideL, swingL), ("R", sideR, Scale(swingL, -1f)) })
            {
                Rot(clip, root, R(pfx + "_ARM_1"), Scale(sw, 4f * side));
                Rot(clip, root, R(pfx + "_ARM_3"), Scale(Shift(sw, 7), -6f * side));
                Rot(clip, root, R(pfx + "_HAND_1"), Scale(Shift(sw, 6), -4f * side));
            }

            // ── Orejas: rebotan con cada paso (2 veces por ciclo), con retraso ──
            foreach (var (pfx, side) in new[] { ("L", sideL), ("R", sideR) })
            {
                Rot(clip, root, R(pfx + "_EAR_1"), new[] { 0f, -2f * side, -3f * side, -1f * side, 0f, -2f * side, -3f * side, -1f * side });
                Rot(clip, root, R(pfx + "_EAR_2"), new[] { 1f * side, 0f, -3f * side, -4f * side, 1f * side, 0f, -3f * side, -4f * side });
            }

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
            if (existing != null) { EditorUtility.CopySerialized(clip, existing); EditorUtility.SetDirty(existing); clip = existing; }
            else AssetDatabase.CreateAsset(clip, ClipPath);

            SetupController(clip, report);
            AssetDatabase.SaveAssets();
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
            System.IO.File.WriteAllText("Logs/velvet_ciclope.txt", report.ToString());
        }

        // Que ninguna parte atraviese el suelo
        VelvetGroundSolver.Solve(PrefabPath, ClipPath, "Logs/velvet_suelo_ciclope.txt");
        Debug.Log("[Cíclope] Walk listo.");
    }

    const string PosesClipPath = "Assets/Assets/BOSS/CYCLOPS_Walk_Poses.anim";

    /// <summary>
    /// Versión de prueba basada en el pose to pose del PLAYER (Contact → Recoil → Passing → High-Point),
    /// traducido a vista de frente: rebote vertical marcado (más abajo en el Recoil, arriba en el High-Point),
    /// rodilla de la pierna libre muy arriba en el Passing, la pierna de apoyo se flexiona para absorber
    /// el peso y los brazos llegan a su máximo en el Recoil.
    /// </summary>
    [MenuItem("Tools/Velvet/Cíclope: generar Walk pose to pose (prueba)")]
    public static void GeneratePoses()
    {
        var report = new StringBuilder();
        pending.Clear();
        building = null;
        // Poses de Richard Williams (Contact, Down, Passing, Up), pero a ritmo de gigante pesado:
        // 1.6 s por ciclo y con ease en cada pose (se "asienta" en el Down en lugar de pasar de largo).
        Duration = 1.6f; linearSpacing = false;
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform R(string n) => Find(root.transform, n);
            Transform hip = R("HIP"), lLeg = R("L_LEG_1"), rLeg = R("R_LEG_1"), lFoot = R("L_FOOT");
            if (hip == null || lLeg == null || rLeg == null) { Debug.LogError("[Cíclope] Faltan huesos."); return; }
            float legLen = Vector3.Distance(root.transform.InverseTransformPoint(lLeg.position), root.transform.InverseTransformPoint(lFoot.position));
            float sideL = Mathf.Sign(root.transform.InverseTransformPoint(lLeg.position).x - root.transform.InverseTransformPoint(hip.position).x);
            float sideR = -sideL;

            float bob = legLen * 0.10f, liftAmt = legLen * 0.24f, shift = legLen * 0.09f;
            //                         Contact Down  Passing  Up  | Contact Down  Passing  Up
            float[] hipY   = Scale(new[] { -0.6f, -1f, -0.3f, 0f,   -0.6f, -1f, -0.3f, 0f }, bob);
            // En el Up la pierna de apoyo empuja "de puntas": se estira un poco (el solver de suelo sube el cuerpo)
            float[] pushL  = Scale(new[] { 0f, 0f, 0f, 0.25f, 0f, 0f, 0f, 0f }, bob);
            float[] pushR  = Scale(new[] { 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0.25f }, bob);
            float[] footR  = Scale(new[] {  0f,  0.3f,  1f,  0.5f,   0f,   0f,   0f,   0f }, liftAmt); // paso 1: la R es la libre
            float[] footL  = Scale(new[] {  0f,   0f,   0f,   0f,   0f,  0.3f,  1f,  0.5f }, liftAmt); // paso 2: la L es la libre
            float[] weight = { 0f, 0.6f, 1f, 0.6f, 0f, -0.6f, -1f, -0.6f }; // +1 = peso sobre la L

            // Cadera: rebote fuerte + desplazamiento sobre la pierna de apoyo
            PosX(clip0(), root, hip, Scale(weight, shift * sideL));
            PosY(clip0(), root, hip, hipY);

            foreach (var (leg, side, foot, push) in new[] { (lLeg, sideL, footL, pushL), (rLeg, sideR, footR, pushR) })
            {
                string pfx = leg.name.StartsWith("L") ? "L" : "R";
                // Altura del pie = rebote de la cadera + muslo: el muslo sube lo que baje la cadera (la pierna se flexiona)
                var legY = new float[8];
                var bend = new float[8];
                for (int i = 0; i < 8; i++) { legY[i] = foot[i] - hipY[i] - push[i]; bend[i] = foot[i] / liftAmt; }
                PosY(clip0(), root, leg, legY);
                // Rodillas: abiertas al cargar peso (gigante) y muy dobladas al levantar
                var thigh = new float[8]; var shin = new float[8]; var ft = new float[8];
                for (int i = 0; i < 8; i++)
                {
                    float load = -hipY[i] / bob * (1f - bend[i]); // flexión de la pierna de apoyo
                    thigh[i] = side * (3f * load + 10f * bend[i]);
                    shin[i] = side * (-2f * load - 18f * bend[i]);
                    ft[i] = side * (6f * bend[i]);
                }
                Rot(clip0(), root, leg, thigh);
                Rot(clip0(), root, R(pfx + "_LEG_2"), shin);
                Rot(clip0(), root, R(pfx + "_FOOT"), ft);
            }

            // Torso y cabeza (cabeza compensa con retraso)
            Rot(clip0(), root, R("BODY_1"), Scale(weight, -5.5f * sideL));
            Rot(clip0(), root, R("BODY_2"), Scale(Shift(weight, 7), -2f * sideL));
            Rot(clip0(), root, R("HEAD"), Scale(Shift(weight, 7), 4f * sideL));

            // Brazos: máximo en el Recoil, opuestos a la pierna de adelante
            float[] swingR = { 0.8f, 1f, 0.4f, -0.2f, -0.8f, -1f, -0.4f, 0.2f };
            foreach (var (pfx, side, sw) in new[] { ("R", sideR, swingR), ("L", sideL, Scale(swingR, -1f)) })
            {
                var fwd = new float[8]; for (int i = 0; i < 8; i++) fwd[i] = Mathf.Max(0f, sw[i]);
                Rot(clip0(), root, R(pfx + "_ARM_1"), Scale(sw, 6f * side));
                Rot(clip0(), root, R(pfx + "_ARM_3"), Scale(Shift(fwd, 7), -16f * side)); // el codo se dobla al ir al frente
                Rot(clip0(), root, R(pfx + "_HAND_1"), Scale(Shift(sw, 6), -6f * side));
            }

            // Orejas: rebote fuerte con el Recoil
            foreach (var (pfx, side) in new[] { ("L", sideL), ("R", sideR) })
            {
                Rot(clip0(), root, R(pfx + "_EAR_1"), Scale(new[] { -1f, -4f, -2f, 1f, -1f, -4f, -2f, 1f }, side));
                Rot(clip0(), root, R(pfx + "_EAR_2"), Scale(new[] { 1f, -2f, -6f, -2f, 1f, -2f, -6f, -2f }, side));
            }

            var clip = clip0();
            clip.frameRate = 24f;
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(PosesClipPath);
            if (existing != null) { EditorUtility.CopySerialized(clip, existing); EditorUtility.SetDirty(existing); clip = existing; }
            else AssetDatabase.CreateAsset(clip, PosesClipPath);
            SetupController(clip, report);
            AssetDatabase.SaveAssets();
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
            building = null;
            System.IO.File.WriteAllText("Logs/velvet_ciclope_poses.txt", report.ToString());
        }
        VelvetGroundSolver.Solve(PrefabPath, PosesClipPath, "Logs/velvet_suelo_ciclope_poses.txt");
        Debug.Log("[Cíclope] Walk pose to pose listo (asignado al estado Walk).");
    }

    static AnimationClip building;
    static AnimationClip clip0() => building ?? (building = new AnimationClip { name = "CYCLOPS_Walk_Poses", frameRate = 30f });

    [MenuItem("Tools/Velvet/Cíclope: usar Walk de frente (v1)")]
    public static void UseV1() => AssignWalk(ClipPath);
    [MenuItem("Tools/Velvet/Cíclope: usar Walk pose to pose (prueba)")]
    public static void UsePoses() => AssignWalk(PosesClipPath);

    static void AssignWalk(string path)
    {
        var c = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        foreach (var s in c.layers[0].stateMachine.states) if (s.state.name == "Walk") s.state.motion = clip;
        EditorUtility.SetDirty(c); AssetDatabase.SaveAssets();
        Debug.Log($"[Cíclope] Walk usa ahora {clip.name}");
    }

    static void SetupController(AnimationClip walk, StringBuilder report)
    {
        var c = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (c == null) { report.AppendLine("No encontré CYCLOPS_Final.controller"); return; }
        // Parámetros que manda EnemyChase (si faltan, Unity avisa "Parameter does not exist")
        Ensure(c, "IsMoving", AnimatorControllerParameterType.Bool);
        Ensure(c, "Speed", AnimatorControllerParameterType.Float);
        Ensure(c, "MoveX", AnimatorControllerParameterType.Float);
        Ensure(c, "MoveY", AnimatorControllerParameterType.Float);

        var sm = c.layers[0].stateMachine;
        AnimatorState walkState = null;
        foreach (var s in sm.states) if (s.state.name == "Walk") walkState = s.state;
        if (walkState != null && HasAttackStates(sm))
        {
            // Las transiciones ya las administra "Cíclope: instalar cerebro"; aquí solo se cambia el clip.
            walkState.motion = walk;
            EditorUtility.SetDirty(c);
            report.AppendLine("Walk ya existía: solo se asignó el clip");
            return;
        }
        if (walkState == null) walkState = sm.AddState("Walk", new Vector3(500, 120, 0));
        walkState.motion = walk;
        var idle = sm.defaultState;
        report.AppendLine($"Estado por defecto: {(idle != null ? idle.name : "ninguno")}");

        // Limpiar transiciones previas hacia/desde Walk y volver a crearlas
        foreach (var s in sm.states)
            foreach (var t in s.state.transitions)
                if (t.destinationState == walkState || s.state == walkState) s.state.RemoveTransition(t);

        foreach (var s in sm.states)
        {
            if (s.state == walkState) continue;
            var t = s.state.AddTransition(walkState);
            t.hasExitTime = false; t.hasFixedDuration = true; t.duration = 0.2f;
            t.AddCondition(AnimatorConditionMode.If, 0, "IsMoving");
            report.AppendLine($"Transición {s.state.name} -> Walk (IsMoving)");
        }
        if (idle != null && idle != walkState)
        {
            var back = walkState.AddTransition(idle);
            back.hasExitTime = false; back.hasFixedDuration = true; back.duration = 0.25f;
            back.AddCondition(AnimatorConditionMode.IfNot, 0, "IsMoving");
            report.AppendLine($"Transición Walk -> {idle.name} (!IsMoving)");
        }
        EditorUtility.SetDirty(c);
    }

    static bool HasAttackStates(AnimatorStateMachine sm)
    {
        foreach (var s in sm.states) if (s.state.name.StartsWith("Attack_")) return true;
        return false;
    }

    static void Ensure(AnimatorController c, string n, AnimatorControllerParameterType t)
    {
        foreach (var p in c.parameters) if (p.name == n) return;
        c.AddParameter(n, t);
    }

    // ───── helpers de curvas (8 poses, loop) ─────
    static float[] Scale(float[] a, float k) { var r = new float[a.Length]; for (int i = 0; i < a.Length; i++) r[i] = a[i] * k; return r; }
    static float[] Shift(float[] a, int n) { var r = new float[a.Length]; for (int i = 0; i < a.Length; i++) r[i] = a[(i + n) % a.Length]; return r; }

    static void Rot(AnimationClip clip, GameObject root, Transform t, float[] delta)
    {
        if (t == null) return;
        string path = AnimationUtility.CalculateTransformPath(t, root.transform);
        Vector3 e = t.localEulerAngles;
        Set(clip, path, "localEulerAnglesRaw.x", Const(e.x));
        Set(clip, path, "localEulerAnglesRaw.y", Const(e.y));
        Set(clip, path, "localEulerAnglesRaw.z", Loop(e.z, delta));
    }

    // Desplazamientos dados en el espacio del ROOT (x = lado, y = arriba); se convierten al espacio
    // del padre del hueso, porque los huesos de 2D Animation suelen estar rotados (su +X apunta a lo largo del hueso).
    static readonly Dictionary<Transform, (float[] x, float[] y)> pending = new Dictionary<Transform, (float[] x, float[] y)>();
    static void PosX(AnimationClip clip, GameObject root, Transform t, float[] dx) => AddPos(clip, root, t, dx, null);
    static void PosY(AnimationClip clip, GameObject root, Transform t, float[] dy) => AddPos(clip, root, t, null, dy);
    static void PosYAdd(AnimationClip clip, GameObject root, Transform t, float[] unit, float k) => AddPos(clip, root, t, null, Scale(unit, k));

    static void AddPos(AnimationClip clip, GameObject root, Transform t, float[] dx, float[] dy)
    {
        if (t == null) return;
        var cur = pending.TryGetValue(t, out var v) ? v : (new float[8], new float[8]);
        if (dx != null) for (int i = 0; i < 8; i++) cur.Item1[i] += dx[i];
        if (dy != null) for (int i = 0; i < 8; i++) cur.Item2[i] += dy[i];
        pending[t] = cur;

        Vector3 ax = t.parent.InverseTransformVector(root.transform.TransformVector(Vector3.right));
        Vector3 ay = t.parent.InverseTransformVector(root.transform.TransformVector(Vector3.up));
        Vector3 p = t.localPosition;
        var lx = new float[8]; var ly = new float[8];
        for (int i = 0; i < 8; i++)
        {
            Vector3 d = ax * cur.Item1[i] + ay * cur.Item2[i];
            lx[i] = d.x; ly[i] = d.y;
        }
        string path = AnimationUtility.CalculateTransformPath(t, root.transform);
        Set(clip, path, "m_LocalPosition.x", Loop(p.x, lx));
        Set(clip, path, "m_LocalPosition.y", Loop(p.y, ly));
        Set(clip, path, "m_LocalPosition.z", Const(p.z));
    }

    static void Set(AnimationClip clip, string path, string prop, AnimationCurve c) =>
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), prop), c);

    static AnimationCurve Const(float v) => new AnimationCurve(new Keyframe(0f, v), new Keyframe(Duration, v));

    static AnimationCurve Loop(float baseValue, float[] v)
    {
        int n = v.Length; float dt = Duration / n;
        var keys = new Keyframe[n + 1];
        for (int i = 0; i <= n; i++)
        {
            float inS = (v[i % n] - v[(i - 1 + n) % n]) / dt, outS = (v[(i + 1) % n] - v[i % n]) / dt;
            float smooth = (inS + outS) * 0.5f;
            keys[i] = linearSpacing
                ? new Keyframe(i * dt, baseValue + v[i % n], inS, outS)      // intermedios en tercios (espaciado parejo)
                : new Keyframe(i * dt, baseValue + v[i % n], smooth, smooth);
        }
        return new AnimationCurve(keys);
    }

    static Transform Find(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name && t.GetComponent<SpriteRenderer>() == null) return t;
        return null;
    }
}
#endif
