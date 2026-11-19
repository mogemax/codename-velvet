#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Generates the procedural PLAYER animations (Idle, Walk, Dash), builds the Animator Controller with
/// PlayerMovement's parameters and assigns it to the prefab.
///
/// Sign convention (character facing right):
///  - Bones pointing DOWN (arms, legs): positive rotation = tip moves forward.
///  - Bones pointing UP (body, head, ears): positive rotation = tip moves backward.
/// </summary>
public static class VelvetPlayerIdleGenerator
{
    const string PrefabPath = "Assets/Assets/CHARACTERS/PLAYER.prefab";
    const string AnimFolder = "Assets/Animators/Animations";
    const string ControllerPath = "Assets/Animators/PLAYER.controller";

    const float TwoPi = Mathf.PI * 2f;

    enum Channel { Rotation, PositionY }

    class Track
    {
        public string bone; public Channel channel; public Func<float, float> f;
        public float[] poses; // one key per pose (looping cycle), alternative to f
        public float[] times; // when set: time of each key (non-looping clip)
        public bool autoSign;  // + = tip moves forward, whichever way the bone points
        public Track(string b, Channel c, Func<float, float> fn) { bone = b; channel = c; f = fn; }
    }

    class ClipDef
    {
        public string name; public float duration; public bool loop; public int samples;
        public List<Track> tracks = new List<Track>();
        public ClipDef Rot(string b, Func<float, float> f) { tracks.Add(new Track(b, Channel.Rotation, f)); return this; }
        public ClipDef PosY(string b, Func<float, float> f) { tracks.Add(new Track(b, Channel.PositionY, f)); return this; }
        public ClipDef Keys(string b, float[] t, float[] v) { tracks.Add(new Track(b, Channel.Rotation, null) { poses = v, times = t }); return this; }
        public ClipDef KeysY(string b, float[] t, float[] v) { tracks.Add(new Track(b, Channel.PositionY, null) { poses = v, times = t }); return this; }
        public ClipDef RotA(string b, Func<float, float> f) { tracks.Add(new Track(b, Channel.Rotation, f) { autoSign = true }); return this; }
        public ClipDef PosesA(string b, float[] v) { tracks.Add(new Track(b, Channel.Rotation, null) { poses = v, autoSign = true }); return this; }
        public ClipDef KeysA(string b, float[] t, float[] v) { tracks.Add(new Track(b, Channel.Rotation, null) { poses = v, times = t, autoSign = true }); return this; }
        public ClipDef Poses(string b, float[] v) { tracks.Add(new Track(b, Channel.Rotation, null) { poses = v }); return this; }
        public ClipDef PosesY(string b, float[] v) { tracks.Add(new Track(b, Channel.PositionY, null) { poses = v }); return this; }
    }

    static float S(float u, float cycles, float phase) => Mathf.Sin(TwoPi * (u * cycles) + phase);

    // IDLE: breathing
    static ClipDef Idle()
    {
        var c = new ClipDef { name = "PLAYER_Idle", duration = 2.4f, loop = true, samples = 12 };
        (string b, float amp, float ph)[] rot =
        {
            ("BODY", 1.2f, 0.00f), ("HEAD", -2.0f, 0.10f),
            ("L EAR", 4.0f, 0.18f), ("L EAR TIP", 6.0f, 0.28f),
            ("R EAR", -4.0f, 0.20f), ("R EAR TIP", -6.0f, 0.30f),
            ("R ARM 1", 2.5f, 0.08f), ("R ARM 2", 3.0f, 0.16f), ("R HAND", 3.0f, 0.24f),
            ("L ARM 1", -2.5f, 0.08f), ("L ARM 2", -3.0f, 0.16f), ("R HAND_1", -3.0f, 0.24f),
        };
        foreach (var r in rot) { var rr = r; c.Rot(rr.b, u => rr.amp * S(u, 1, -TwoPi * rr.ph)); }
        c.PosY("BODY", u => -0.12f * (1f - Mathf.Cos(TwoPi * u)) * 0.5f);

        // Cape: gentle sway, lagging behind the breathing
        (string b, float amp, float ph)[] cape =
        {
            ("CAPE_1", 1.5f, 0.12f), ("CAPE_2", 2.5f, 0.25f),
            ("L_CAPE_1", 1.5f, 0.16f), ("L_CAPE_2", 2.5f, 0.30f),
            ("R_CAPE_1", 1.5f, 0.14f), ("R_CAPE_2", 2.5f, 0.28f),
        };
        foreach (var k in cape) { var kk = k; c.RotA(kk.b, u => kk.amp * S(u, 1, -TwoPi * kk.ph)); }
        return c;
    }

    // WALK: pose to pose (Contact → Recoil → Passing → High-Point)
    // 8 poses per cycle: 0 Contact, 1 Recoil, 2 Passing, 3 High-Point (L leg steps forward)
    //                   4 Contact, 5 Recoil, 6 Passing, 7 High-Point (R leg steps forward)
    // Each array describes side "A"; the opposite side uses the same array shifted by 4 poses.
    static readonly float[] Thigh = { 25f, 15f,  0f, -18f, -25f, -12f,  12f,  30f };
    static readonly float[] Knee  = {  0f,-15f, -5f,   0f,  -5f, -35f, -60f, -20f };
    static readonly float[] Foot  = { 15f,  0f,  0f, -20f, -25f, -15f, -10f,  10f };
    static readonly float[] Arm1  = { 22f, 26f,  5f, -15f, -22f, -26f,  -5f,  15f };
    static readonly float[] Arm2  = { 25f, 30f, 15f,  10f,   8f,  10f,  15f,  20f };
    static readonly float[] Hand  = {  5f, 10f,  0f,  -5f,  -5f,  -8f,   0f,   5f };
    // Per step (repeats twice per cycle)
    static readonly float[] HipY    = { 0f, -0.45f, 0.05f, 0.45f };
    static readonly float[] BodyRot = { -5f, -6.5f, -5f, -4f };
    static readonly float[] HeadRot = { 0f, -2f, -1f, 1f };
    static readonly float[] EarRot  = { 2f, 6f, 8f, 3f };
    static readonly float[] EarTip  = { 3f, 5f, 10f, 6f };

    static float[] Shift(float[] a, int n)
    {
        var r = new float[a.Length];
        for (int i = 0; i < a.Length; i++) r[i] = a[(i + n) % a.Length];
        return r;
    }

    static float[] Twice(float[] step)
    {
        var r = new float[step.Length * 2];
        for (int i = 0; i < r.Length; i++) r[i] = step[i % step.Length];
        return r;
    }

    static ClipDef Walk()
    {
        var c = new ClipDef { name = "PLAYER_Walk", duration = 0.8f, loop = true, samples = 8 };

        // L leg = side A (forward at the frame-0 Contact); R leg is half a cycle behind
        c.Poses("L LEG 1", Thigh);            c.Poses("R LEG 1", Shift(Thigh, 4));
        c.Poses("L BOOT FRONT", Knee);        c.Poses("R BOOT FRONT", Shift(Knee, 4));
        c.Poses("L FOOT", Foot);              c.Poses("R FOOT", Shift(Foot, 4));

        // Arms oppose the leg on their side: the R arm moves with the L leg
        c.Poses("R ARM 1", Arm1);             c.Poses("L ARM 1", Shift(Arm1, 4));
        c.Poses("R ARM 2", Arm2);             c.Poses("L ARM 2", Shift(Arm2, 4));
        c.Poses("R HAND", Hand);              c.Poses("R HAND_1", Shift(Hand, 4));

        // Body: down on the Recoil, up on the High-Point
        c.PosesY("HIP", Twice(HipY));
        c.Poses("BODY", Twice(BodyRot));
        c.Poses("HEAD", Twice(HeadRot));

        // Ears lag behind (follow-through)
        c.Poses("L EAR", Twice(EarRot));      c.Poses("R EAR", Twice(EarRot));
        c.Poses("L EAR TIP", Twice(EarTip));  c.Poses("R EAR TIP", Twice(EarTip));

        // Cape: the 3 chains move the SAME (otherwise the mesh folds between them into a triangle).
        // Little bend between _1 and _2 so it does not fold in half.
        float[] cape1 = Twice(new[] { -3f, -2f, -3.5f, -4.5f });
        float[] cape2 = Twice(new[] { -1f, -2f, -0.5f, 0.5f });
        foreach (var side in new[] { "", "L_", "R_" })
        {
            c.PosesA(side + "CAPE_1", cape1);
            c.PosesA(side + "CAPE_2", cape2);
        }
        return c;
    }

    // DASH: dive roll
    // 0 Neutral · 1 Anticipation (crouch, arms back) · 2 Dive (body stretched, arms forward)
    // 3 Hands on the floor, starts tucking · 4 Rolls over the back (tucked) · 5 Comes out of the roll
    // 6 Lands crouched · 7 Pushes off to keep running (knee up)
    // The ground solver sets the final height: negative HIP Y values only "ask" to crouch.
    static readonly float[] DashTimes = { 0f, 0.09f, 0.26f, 0.39f, 0.51f, 0.64f, 0.76f, 0.90f };

    static ClipDef Dash()
    {
        var c = new ClipDef { name = "PLAYER_Dash", duration = 0.90f, loop = false };
        var T = DashTimes;

        //                         0     1      2      3      4      5      6      7
        // Flatter, longer dive (0.07 → 0.20); compact roll close to the floor
        c.Keys ("HIP",  T, new[] { 0f,   5f,  -80f, -150f, -240f, -320f, -360f, -360f });
        c.KeysY("HIP",  T, new[] { 0f, -1.5f,  2.0f,  0f,   -3f,   -3f,  -1.5f, -0.3f });

        c.Keys("BODY",  T, new[] { 0f, -30f,   -5f,  -40f,  -50f,  -45f,  -22f,  -12f });
        c.Keys("HEAD",  T, new[] { 0f, -10f,   12f,  -45f,  -60f,  -50f,    0f,    3f });

        // Legs: stretched back in the dive, tucked (knees to chest) in the roll
        c.Keys("R LEG 1",      T, new[] { 0f,  45f, -40f,  60f,  105f,  100f,   50f,   45f });
        c.Keys("L LEG 1",      T, new[] { 0f,  40f, -50f,  55f,  100f,   98f,   45f,  -15f });
        c.Keys("R BOOT FRONT", T, new[] { 0f, -75f,  -5f, -90f, -135f, -135f,  -85f,  -60f });
        c.Keys("L BOOT FRONT", T, new[] { 0f, -70f, -10f, -85f, -132f, -132f,  -80f,  -10f });
        c.Keys("R FOOT",       T, new[] { 0f,   5f, -25f,  10f,   20f,   20f,   10f,   10f });
        c.Keys("L FOOT",       T, new[] { 0f,   5f, -30f,  10f,   20f,   20f,   10f,   -5f });

        // Arms: stretched forward in the dive → hug the legs in the roll
        c.Keys("R ARM 1", T, new[] { 0f, -45f, 165f, 100f,  65f,  45f, -10f, -30f });
        c.Keys("L ARM 1", T, new[] { 0f, -50f, 158f,  95f,  60f,  40f,  10f,  35f });
        c.Keys("R ARM 2", T, new[] { 0f,  10f,   5f,  80f, 100f,  90f,  40f,  40f });
        c.Keys("L ARM 2", T, new[] { 0f,  10f,   5f,  75f, 100f,  90f,  35f,  45f });
        c.Keys("R HAND",   T, new[] { 0f, 0f, -10f, 30f, 30f, 20f, 5f, 5f });
        c.Keys("R HAND_1", T, new[] { 0f, 0f, -10f, 30f, 30f, 20f, 5f, 5f });

        // Ears: back in the dive and FLAT against the head in the roll
        c.Keys("L EAR",     T, new[] { 0f,  5f, 45f, 70f, 85f, 75f, -10f, 12f });
        c.Keys("R EAR",     T, new[] { 0f,  5f, 45f, 70f, 85f, 75f, -10f, 12f });
        c.Keys("L EAR TIP", T, new[] { 0f, -5f, 30f, 45f, 50f, 45f,   5f, -8f });
        c.Keys("R EAR TIP", T, new[] { 0f, -5f, 30f, 45f, 50f, 45f,   5f, -8f });

        // Cape: waves back in the dive, wraps the body in the roll, whips on landing
        //                            0    1     2     3     4     5     6     7
        float[] dCape1 = { 0f,  8f, -55f, -15f, -10f,  -8f, -20f,  5f };
        float[] dCape2 = { 0f,  5f, -30f, -20f, -15f, -10f, -30f,  8f };
        foreach (var side in new[] { "", "L_", "R_" })
        {
            c.KeysA(side + "CAPE_1", T, dCape1);
            c.KeysA(side + "CAPE_2", T, dCape2);
        }
        return c;
    }

    [MenuItem("Tools/Velvet/Player Animations/Regenerate Dash Only")]
    public static void GenerateDashOnly()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            GameObject root = VelvetGroundSolver.AnimatedRoot(prefabRoot);
            Transform hip = FindBone(root.transform, "HIP");
            if (hip == null) { Debug.LogError("[Anim] HIP bone not found."); return; }
            BuildClip(Dash(), root.transform, hip);
            AssetDatabase.SaveAssets();
            Debug.Log("[Anim] Dash regenerated (dive roll).");
        }
        finally { PrefabUtility.UnloadPrefabContents(prefabRoot); }
        VelvetGroundSolver.Solve("Assets/Animators/Animations/PLAYER_Dash.anim");
    }

    [MenuItem("Tools/Velvet/Player Animations/Generate All")]
    public static void Generate()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            GameObject root = VelvetGroundSolver.AnimatedRoot(prefabRoot);
            Transform hip = FindBone(root.transform, "HIP");
            if (hip == null) { Debug.LogError("[Anim] HIP bone not found inside PLAYER."); return; }

            AnimationClip idle = BuildClip(Idle(), root.transform, hip);
            AnimationClip walk = BuildClip(Walk(), root.transform, hip);
            AnimationClip dash = BuildClip(Dash(), root.transform, hip);

            AnimatorController controller = BuildController(idle, walk, dash);

            var animator = root.GetComponent<Animator>();
            if (animator == null) animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var movement = prefabRoot.GetComponent<PlayerMovement>();
            if (movement != null)
            {
                var so = new SerializedObject(movement);
                so.FindProperty("animator").objectReferenceValue = animator;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[Anim] Done: Idle, Walk and Dash assigned to the PLAYER prefab.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    static AnimationClip BuildClip(ClipDef def, Transform root, Transform hip)
    {
        var clip = new AnimationClip { name = def.name, frameRate = 30f };
        foreach (var trackDef in def.tracks)
        {
            var tr = trackDef;
            Transform t = FindBone(hip, tr.bone);
            if (t == null) { Debug.LogWarning($"[Anim] {def.name}: bone '{tr.bone}' not found, skipped."); continue; }
            string path = AnimationUtility.CalculateTransformPath(t, root);

            if (tr.channel == Channel.Rotation)
            {
                Vector3 e = t.localEulerAngles;
                if (tr.autoSign)
                {
                    // A hanging bone (pointing down) already moves forward with +; a bone pointing up is inverted.
                    float sign = root.InverseTransformDirection(t.right).y < 0f ? 1f : -1f;
                    if (tr.poses != null) { var pv = (float[])tr.poses.Clone(); for (int k = 0; k < pv.Length; k++) pv[k] *= sign; tr = new Track(tr.bone, tr.channel, null) { poses = pv, times = tr.times }; }
                    else { var f0 = tr.f; tr = new Track(tr.bone, tr.channel, u => sign * f0(u)); }
                }
                SetCurve(clip, path, "localEulerAnglesRaw.x", Constant(e.x, def.duration));
                SetCurve(clip, path, "localEulerAnglesRaw.y", Constant(e.y, def.duration));
                SetCurve(clip, path, "localEulerAnglesRaw.z", tr.times != null ? FromTimedKeys(e.z, tr.times, tr.poses) : tr.poses != null ? FromPoses(e.z, tr.poses, def) : FromFunc(e.z, tr.f, def));
            }
            else
            {
                Vector3 p = t.localPosition;
                SetCurve(clip, path, "m_LocalPosition.x", Constant(p.x, def.duration));
                SetCurve(clip, path, "m_LocalPosition.y", tr.times != null ? FromTimedKeys(p.y, tr.times, tr.poses) : tr.poses != null ? FromPoses(p.y, tr.poses, def) : FromFunc(p.y, tr.f, def));
                SetCurve(clip, path, "m_LocalPosition.z", Constant(p.z, def.duration));
            }
        }

        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = def.loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        string path2 = $"{AnimFolder}/{def.name}.anim";
        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path2);
        if (existing != null)
        {
            // Keep the file (and its GUID) and only replace the contents
            EditorUtility.CopySerialized(clip, existing);
            EditorUtility.SetDirty(existing);
            return existing;
        }
        AssetDatabase.CreateAsset(clip, path2);
        return clip;
    }

    static AnimatorController BuildController(AnimationClip idle, AnimationClip walk, AnimationClip dash)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

        EnsureParam(controller, "IsMoving", AnimatorControllerParameterType.Bool);
        EnsureParam(controller, "Dash", AnimatorControllerParameterType.Trigger);
        EnsureParam(controller, "IsDashing", AnimatorControllerParameterType.Bool);

        var sm = controller.layers[0].stateMachine;
        foreach (var t in sm.anyStateTransitions) sm.RemoveAnyStateTransition(t);
        foreach (var s in sm.states) sm.RemoveState(s.state);

        var sIdle = sm.AddState("Idle", new Vector3(300, 0, 0));
        var sWalk = sm.AddState("Walk", new Vector3(300, 120, 0));
        var sDash = sm.AddState("Dash", new Vector3(560, 60, 0));
        sIdle.motion = idle; sWalk.motion = walk; sDash.motion = dash;
        sm.defaultState = sIdle;

        var t1 = Quick(sIdle.AddTransition(sWalk), 0.08f);
        t1.AddCondition(AnimatorConditionMode.If, 0, "IsMoving");

        var t2 = Quick(sWalk.AddTransition(sIdle), 0.12f);
        t2.AddCondition(AnimatorConditionMode.IfNot, 0, "IsMoving");

        var t3 = Quick(sm.AddAnyStateTransition(sDash), 0.03f);
        t3.canTransitionToSelf = false;
        t3.AddCondition(AnimatorConditionMode.If, 0, "Dash");

        var t4 = Quick(sDash.AddTransition(sWalk), 0.1f);
        t4.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDashing");
        t4.AddCondition(AnimatorConditionMode.If, 0, "IsMoving");

        var t5 = Quick(sDash.AddTransition(sIdle), 0.15f);
        t5.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDashing");
        t5.AddCondition(AnimatorConditionMode.IfNot, 0, "IsMoving");

        EditorUtility.SetDirty(controller);
        return controller;
    }

    static AnimatorStateTransition Quick(AnimatorStateTransition t, float duration)
    {
        t.hasExitTime = false;
        t.hasFixedDuration = true;
        t.duration = duration;
        return t;
    }

    static void EnsureParam(AnimatorController c, string name, AnimatorControllerParameterType type)
    {
        foreach (var p in c.parameters) if (p.name == name) return;
        c.AddParameter(name, type);
    }

    // Finds a bone by name, skipping sprites (they share names)
    // and tolerating the "_1" suffix the PSD importer adds.
    static Transform FindBone(Transform parent, string baseName)
    {
        var all = new List<Transform>();
        foreach (var t in parent.GetComponentsInChildren<Transform>(true))
            if (t.GetComponent<SpriteRenderer>() == null) all.Add(t);
        foreach (var t in all) if (t.name.Trim() == baseName) return t;
        foreach (var t in all)
            if (Regex.Replace(t.name.Trim(), @"_\d+$", "").Trim() == baseName) return t;
        return null;
    }

    static void SetCurve(AnimationClip clip, string path, string prop, AnimationCurve curve)
    {
        var binding = EditorCurveBinding.FloatCurve(path, typeof(Transform), prop);
        AnimationUtility.SetEditorCurve(clip, binding, curve);
    }

    static AnimationCurve Constant(float v, float duration) =>
        new AnimationCurve(new Keyframe(0f, v), new Keyframe(duration, v));

    // Keys at arbitrary times with ClampedAuto tangents (smooth, no overshoot)
    static AnimationCurve FromTimedKeys(float baseValue, float[] t, float[] v)
    {
        var curve = new AnimationCurve();
        for (int i = 0; i < t.Length; i++) curve.AddKey(new Keyframe(t[i], baseValue + v[i]));
        for (int i = 0; i < curve.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
        }
        return curve;
    }

    // One key per pose, evenly spread over the duration, with Catmull-Rom style tangents
    // that wrap around so the loop has no seams.
    static AnimationCurve FromPoses(float baseValue, float[] v, ClipDef def)
    {
        int n = v.Length;
        float dt = def.duration / n;
        var keys = new Keyframe[n + 1];
        for (int i = 0; i <= n; i++)
        {
            float prev = v[(i - 1 + n) % n], next = v[(i + 1) % n];
            float slope = (next - prev) / (2f * dt);
            keys[i] = new Keyframe(i * dt, baseValue + v[i % n], slope, slope);
        }
        return new AnimationCurve(keys);
    }

    [MenuItem("Tools/Velvet/Player Animations/Regenerate Walk Only")]
    public static void GenerateWalkOnly()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            GameObject root = VelvetGroundSolver.AnimatedRoot(prefabRoot);
            Transform hip = FindBone(root.transform, "HIP");
            if (hip == null) { Debug.LogError("[Anim] HIP bone not found."); return; }
            BuildClip(Walk(), root.transform, hip);
            AssetDatabase.SaveAssets();
            Debug.Log("[Anim] Walk regenerated (pose to pose).");
        }
        finally { PrefabUtility.UnloadPrefabContents(prefabRoot); }
    }

    // Samples f(u) (u = 0..1) with tangents from the derivative: smooth curves
    // that also loop seamlessly.
    static AnimationCurve FromFunc(float baseValue, Func<float, float> f, ClipDef def)
    {
        var keys = new Keyframe[def.samples + 1];
        const float h = 0.001f;
        for (int i = 0; i <= def.samples; i++)
        {
            float u = (float)i / def.samples;
            float a = def.loop ? u - h : Mathf.Max(0f, u - h);
            float b = def.loop ? u + h : Mathf.Min(1f, u + h);
            float slope = (f(b) - f(a)) / ((b - a) * def.duration);
            keys[i] = new Keyframe(u * def.duration, baseValue + f(u), slope, slope);
        }
        return new AnimationCurve(keys);
    }
}
#endif
