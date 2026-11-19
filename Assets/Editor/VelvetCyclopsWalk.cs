#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Cyclops walk cycle (front view, heavy giant), pose to pose. Per step:
/// 0 Contact · 1 Recoil · 2 Passing (free leg up) · 3 High-Point.
/// - The free leg rises by raising the thigh bone (the mesh shortens, so the knee comes toward the camera).
/// - The hips shift over the supporting leg, the torso leans toward it and the head compensates.
/// - Arms swing opposite the legs; ears follow through.
/// Amplitudes scale with the leg length, so they work whatever the rig's units.
/// </summary>
public static class VelvetCyclopsWalk
{
    const string PrefabPath = "Assets/Assets/BOSS/CYCLOPS 2.prefab";
    const string ClipPath = "Assets/Assets/BOSS/CYCLOPS_Walk.anim";
    const string ControllerPath = "Assets/Assets/BOSS/CYCLOPS_Final.controller";
    static float Duration = 1.4f; // 2 steps (slow giant)
    static bool linearSpacing = false; // "inbetweens on thirds": even spacing between keys

    [MenuItem("Tools/Velvet/Cyclops/Generate Walk")]
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
            if (hip == null || lLeg == null || rLeg == null) { Debug.LogError("[Cyclops] HIP/L_LEG_1/R_LEG_1 not found."); return; }

            // Scale: leg length (hip → foot) in HIP local space
            Transform lFoot = R("L_FOOT");
            float legLen = Vector3.Distance(root.transform.InverseTransformPoint(lLeg.position), root.transform.InverseTransformPoint(lFoot.position));
            // Side of each leg on X (relative to the hip, in root space)
            float sideL = Mathf.Sign(root.transform.InverseTransformPoint(lLeg.position).x - root.transform.InverseTransformPoint(hip.position).x);
            float sideR = -sideL;
            report.AppendLine($"Leg length {legLen:F3}, L side {sideL}, R side {sideR}");

            var clip = new AnimationClip { name = "CYCLOPS_Walk", frameRate = 30f };

            // Step 1 (poses 0-3): R lifts, L supports.  Step 2 (poses 4-7): L lifts, R supports.
            float[] liftR = { 0f, 0.35f, 1f, 0.45f, 0f, 0f, 0f, 0f };
            float[] liftL = { 0f, 0f, 0f, 0f, 0f, 0.35f, 1f, 0.45f };
            // Weight toward the supporting leg: +1 = on L
            float[] weight = { 0f, 0.5f, 1f, 0.6f, 0f, -0.5f, -1f, -0.6f };

            float lift = legLen * 0.16f;   // thigh lift
            float shift = legLen * 0.06f;  // sideways hip shift
            float bob = legLen * 0.035f;   // vertical bounce

            // Legs
            foreach (var (leg, side, lf) in new[] { (lLeg, sideL, liftL), (rLeg, sideR, liftR) })
            {
                string pfx = leg.name.StartsWith("L") ? "L" : "R";
                PosY(clip, root, leg, Scale(lf, lift));
                // When lifting, the thigh opens slightly outward and the shin comes back (bent knee)
                Rot(clip, root, leg, Scale(lf, 6f * side));
                Rot(clip, root, R(pfx + "_LEG_2"), Scale(lf, -10f * side));
                Rot(clip, root, R(pfx + "_FOOT"), Scale(lf, 4f * side));
            }

            // Hips: move over the supporting leg and rise a little as the other leg passes
            PosX(clip, root, hip, Scale(weight, shift * sideL));
            PosYAdd(clip, root, hip, new[] { 0f, -0.3f, 0.6f, 0.4f, 0f, -0.3f, 0.6f, 0.4f }, bob);

            // Torso leans toward the support (+rot = tip goes to -X) and the head compensates
            float[] lean = Scale(weight, -2.5f * sideL);
            Rot(clip, root, R("BODY_1"), lean);
            Rot(clip, root, R("BODY_2"), Scale(Shift(weight, 7), -1.5f * sideL)); // slightly delayed
            Rot(clip, root, R("HEAD"), Scale(Shift(weight, 7), 3f * sideL));      // compensates to stay level

            // Arms swing opposite the advancing leg; the elbow bends when going forward
            float[] swingL = { 0f, 0.5f, 1f, 0.5f, 0f, -0.5f, -1f, -0.5f };   // L arm forward while R leg advances
            foreach (var (pfx, side, sw) in new[] { ("L", sideL, swingL), ("R", sideR, Scale(swingL, -1f)) })
            {
                Rot(clip, root, R(pfx + "_ARM_1"), Scale(sw, 4f * side));
                Rot(clip, root, R(pfx + "_ARM_3"), Scale(Shift(sw, 7), -6f * side));
                Rot(clip, root, R(pfx + "_HAND_1"), Scale(Shift(sw, 6), -4f * side));
            }

            // Ears bounce on every step (twice per cycle), delayed
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
            System.IO.File.WriteAllText("Logs/velvet_cyclops_walk.txt", report.ToString());
        }

        // Keep every part above the ground.
        VelvetGroundSolver.Solve(PrefabPath, ClipPath, "Logs/velvet_suelo_ciclope.txt");
        Debug.Log("[Cyclops] Walk done.");
    }

    const string PosesClipPath = "Assets/Assets/BOSS/CYCLOPS_Walk_Poses.anim";

    /// <summary>
    /// Alternative walk based on the PLAYER's pose to pose (Contact → Recoil → Passing → High-Point), in front
    /// view: strong vertical bounce (lowest on the Recoil, highest on the High-Point), free knee very high on
    /// the Passing, the supporting leg bends to absorb the weight and the arms peak on the Recoil.
    /// </summary>
    [MenuItem("Tools/Velvet/Cyclops/Generate Walk Pose to Pose")]
    public static void GeneratePoses()
    {
        var report = new StringBuilder();
        pending.Clear();
        building = null;
        // Richard Williams' poses (Contact, Down, Passing, Up) at a heavy giant's pace:
        // 1.6 s per cycle with easing on each pose (it settles on the Down instead of passing through).
        Duration = 1.6f; linearSpacing = false;
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform R(string n) => Find(root.transform, n);
            Transform hip = R("HIP"), lLeg = R("L_LEG_1"), rLeg = R("R_LEG_1"), lFoot = R("L_FOOT");
            if (hip == null || lLeg == null || rLeg == null) { Debug.LogError("[Cyclops] Missing bones."); return; }
            float legLen = Vector3.Distance(root.transform.InverseTransformPoint(lLeg.position), root.transform.InverseTransformPoint(lFoot.position));
            float sideL = Mathf.Sign(root.transform.InverseTransformPoint(lLeg.position).x - root.transform.InverseTransformPoint(hip.position).x);
            float sideR = -sideL;

            float bob = legLen * 0.10f, liftAmt = legLen * 0.24f, shift = legLen * 0.09f;
            //                         Contact Down  Passing  Up  | Contact Down  Passing  Up
            float[] hipY   = Scale(new[] { -0.6f, -1f, -0.3f, 0f,   -0.6f, -1f, -0.3f, 0f }, bob);
            // On the Up the supporting leg pushes off its toes: it stretches a little (the ground solver lifts the body)
            float[] pushL  = Scale(new[] { 0f, 0f, 0f, 0.25f, 0f, 0f, 0f, 0f }, bob);
            float[] pushR  = Scale(new[] { 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0.25f }, bob);
            float[] footR  = Scale(new[] {  0f,  0.3f,  1f,  0.5f,   0f,   0f,   0f,   0f }, liftAmt); // step 1: R is the free leg
            float[] footL  = Scale(new[] {  0f,   0f,   0f,   0f,   0f,  0.3f,  1f,  0.5f }, liftAmt); // step 2: L is the free leg
            float[] weight = { 0f, 0.6f, 1f, 0.6f, 0f, -0.6f, -1f, -0.6f }; // +1 = weight on L

            // Hips: strong bounce plus shift over the supporting leg
            PosX(clip0(), root, hip, Scale(weight, shift * sideL));
            PosY(clip0(), root, hip, hipY);

            foreach (var (leg, side, foot, push) in new[] { (lLeg, sideL, footL, pushL), (rLeg, sideR, footR, pushR) })
            {
                string pfx = leg.name.StartsWith("L") ? "L" : "R";
                // Foot height = hip bounce + thigh: the thigh rises as much as the hip drops (the leg bends)
                var legY = new float[8];
                var bend = new float[8];
                for (int i = 0; i < 8; i++) { legY[i] = foot[i] - hipY[i] - push[i]; bend[i] = foot[i] / liftAmt; }
                PosY(clip0(), root, leg, legY);
                // Knees: open when loaded (giant) and deeply bent when lifting
                var thigh = new float[8]; var shin = new float[8]; var ft = new float[8];
                for (int i = 0; i < 8; i++)
                {
                    float load = -hipY[i] / bob * (1f - bend[i]); // supporting leg bend
                    thigh[i] = side * (3f * load + 10f * bend[i]);
                    shin[i] = side * (-2f * load - 18f * bend[i]);
                    ft[i] = side * (6f * bend[i]);
                }
                Rot(clip0(), root, leg, thigh);
                Rot(clip0(), root, R(pfx + "_LEG_2"), shin);
                Rot(clip0(), root, R(pfx + "_FOOT"), ft);
            }

            // Torso and head (the head compensates, delayed)
            Rot(clip0(), root, R("BODY_1"), Scale(weight, -5.5f * sideL));
            Rot(clip0(), root, R("BODY_2"), Scale(Shift(weight, 7), -2f * sideL));
            Rot(clip0(), root, R("HEAD"), Scale(Shift(weight, 7), 4f * sideL));

            // Arms: peak on the Recoil, opposite the front leg
            float[] swingR = { 0.8f, 1f, 0.4f, -0.2f, -0.8f, -1f, -0.4f, 0.2f };
            foreach (var (pfx, side, sw) in new[] { ("R", sideR, swingR), ("L", sideL, Scale(swingR, -1f)) })
            {
                var fwd = new float[8]; for (int i = 0; i < 8; i++) fwd[i] = Mathf.Max(0f, sw[i]);
                Rot(clip0(), root, R(pfx + "_ARM_1"), Scale(sw, 6f * side));
                Rot(clip0(), root, R(pfx + "_ARM_3"), Scale(Shift(fwd, 7), -16f * side)); // the elbow bends when going forward
                Rot(clip0(), root, R(pfx + "_HAND_1"), Scale(Shift(sw, 6), -6f * side));
            }

            // Ears: strong bounce on the Recoil
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
            System.IO.File.WriteAllText("Logs/velvet_cyclops_walk_poses.txt", report.ToString());
        }
        VelvetGroundSolver.Solve(PrefabPath, PosesClipPath, "Logs/velvet_suelo_ciclope_poses.txt");
        Debug.Log("[Cyclops] Pose-to-pose walk done (assigned to the Walk state).");
    }

    static AnimationClip building;
    static AnimationClip clip0() => building ?? (building = new AnimationClip { name = "CYCLOPS_Walk_Poses", frameRate = 30f });

    [MenuItem("Tools/Velvet/Cyclops/Use Front Walk (v1)")]
    public static void UseV1() => AssignWalk(ClipPath);
    [MenuItem("Tools/Velvet/Cyclops/Use Pose-to-Pose Walk")]
    public static void UsePoses() => AssignWalk(PosesClipPath);

    static void AssignWalk(string path)
    {
        var c = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        foreach (var s in c.layers[0].stateMachine.states) if (s.state.name == "Walk") s.state.motion = clip;
        EditorUtility.SetDirty(c); AssetDatabase.SaveAssets();
        Debug.Log($"[Cyclops] Walk now uses {clip.name}");
    }

    static void SetupController(AnimationClip walk, StringBuilder report)
    {
        var c = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (c == null) { report.AppendLine("CYCLOPS_Final.controller not found"); return; }
        // Parameters CyclopsBrain sets (missing ones log "Parameter does not exist")
        Ensure(c, "IsMoving", AnimatorControllerParameterType.Bool);
        Ensure(c, "Speed", AnimatorControllerParameterType.Float);
        Ensure(c, "MoveX", AnimatorControllerParameterType.Float);
        Ensure(c, "MoveY", AnimatorControllerParameterType.Float);

        var sm = c.layers[0].stateMachine;
        AnimatorState walkState = null;
        foreach (var s in sm.states) if (s.state.name == "Walk") walkState = s.state;
        if (walkState != null && HasAttackStates(sm))
        {
            // A controller with attack states already owns its transitions; only swap the clip.
            walkState.motion = walk;
            EditorUtility.SetDirty(c);
            report.AppendLine("Walk already existed: only the clip was assigned");
            return;
        }
        if (walkState == null) walkState = sm.AddState("Walk", new Vector3(500, 120, 0));
        walkState.motion = walk;
        var idle = sm.defaultState;
        report.AppendLine($"Default state: {(idle != null ? idle.name : "none")}");

        // Remove the old transitions to/from Walk and rebuild them
        foreach (var s in sm.states)
            foreach (var t in s.state.transitions)
                if (t.destinationState == walkState || s.state == walkState) s.state.RemoveTransition(t);

        foreach (var s in sm.states)
        {
            if (s.state == walkState) continue;
            var t = s.state.AddTransition(walkState);
            t.hasExitTime = false; t.hasFixedDuration = true; t.duration = 0.2f;
            t.AddCondition(AnimatorConditionMode.If, 0, "IsMoving");
            report.AppendLine($"Transition {s.state.name} -> Walk (IsMoving)");
        }
        if (idle != null && idle != walkState)
        {
            var back = walkState.AddTransition(idle);
            back.hasExitTime = false; back.hasFixedDuration = true; back.duration = 0.25f;
            back.AddCondition(AnimatorConditionMode.IfNot, 0, "IsMoving");
            report.AppendLine($"Transition Walk -> {idle.name} (!IsMoving)");
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

    // Curve helpers (8 looping poses)
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

    // Offsets are given in ROOT space (x = side, y = up) and converted to the bone parent's space,
    // because 2D Animation bones are usually rotated (their +X points along the bone).
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
                ? new Keyframe(i * dt, baseValue + v[i % n], inS, outS)      // inbetweens on thirds (even spacing)
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
