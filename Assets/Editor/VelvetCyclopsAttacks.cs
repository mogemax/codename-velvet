#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using CodenameVelvet.Enemies;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Generates the cyclops attack animations (front view) synced with the scene CyclopsBrain's timings
/// (windup = hit frame, windup + recovery = end of the clip):
///  - Ground Punch: raises the fist, throws itself down onto it and takes a while to get up.
///  - Stomp: lifts a leg, slams it into the ground and the body bounces.
/// Each clip gets an "AnimationEvent_Impact" Animation Event on the hit frame and goes through the
/// ground solver so nothing sinks into the floor.
/// </summary>
public static class VelvetCyclopsAttacks
{
    const string PrefabPath = "Assets/Assets/BOSS/CYCLOPS 2.prefab";
    const string ControllerPath = "Assets/Assets/BOSS/CYCLOPS_Final.controller";
    const string PunchPath = "Assets/Assets/BOSS/CYCLOPS_Attack_Punch.anim";
    const string StompPath = "Assets/Assets/BOSS/CYCLOPS_Attack_Stomp.anim";

    [MenuItem("Tools/Velvet/Cyclops/Generate Attack Animations")]
    public static void Generate()
    {
        var report = new StringBuilder();
        var (pw, pr) = Timing("AttackPunch", 1.3f, 3.2f);
        var (sw, sr) = Timing("AttackStomp", 1.1f, 1.2f);
        report.AppendLine($"Punch: hit {pw}s, recovery {pr}s · Stomp: hit {sw}s, recovery {sr}s");

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        AnimationClip punch, stomp;
        try
        {
            var rig = new Rig(root);
            var brain = Object.FindFirstObjectByType<CyclopsBrain>();
            if (brain != null) rig.Forward = new SerializedObject(brain).FindProperty("artFacesRight").boolValue ? 1f : -1f;
            report.AppendLine($"Leg {rig.Leg:F2}, L side {rig.SideL}");
            punch = Save(BuildPunch(rig, pw, pr), PunchPath, pw);
            stomp = Save(BuildStomp(rig, sw, sr), StompPath, sw);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }

        AssignToController(punch, stomp, report);
        VelvetGroundSolver.Solve(PrefabPath, PunchPath, "Logs/velvet_suelo_ciclope_punch.txt");
        VelvetGroundSolver.Solve(PrefabPath, StompPath, "Logs/velvet_suelo_ciclope_stomp.txt");
        System.IO.File.WriteAllText("Logs/velvet_cyclops_attacks.txt", report.ToString());
        Debug.Log("[Cyclops] Attack animations done.\n" + report);
    }

    // Reads windup/recovery from the scene's CyclopsBrain (when there is one) so the hit lines up.
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

    // PUNCH: throws itself forward
    // "Forward" is where the cyclops faces in local space: the brain flips the root (Scale.x) toward
    // the attack, so animating one side is enough.
    static AnimationClip BuildPunch(Rig r, float W, float R)
    {
        // 0 neutral · 1 anticipation (crouch, fist back) · 2 push off · 3 airborne, stretched
        // 4 HIT: lands on its belly, fist first · 5 lying still · 6 pushes up with the arms
        // 7 kneeling · 8 standing
        float[] t = { 0f, 0.45f * W, 0.65f * W, 0.85f * W, W, W + 0.32f * R, W + 0.55f * R, W + 0.80f * R, W + R };
        var c = new Clip(r, t);
        float L = r.Leg, sR = r.SideR, sL = r.SideL, f = r.Forward;

        // The whole body pivots at the hip: leans back, launches and ends lying down (~88°).
        // + rotation = the top goes to -X, so "forward" is -f.
        float[] lean = { 0f, -12f, 25f, 62f, 88f, 88f, 55f, 18f, 0f };
        c.Rot("HIP", M(lean, -f));
        // Height: crouch, jump, then ask for very low; the ground solver rests it exactly on the floor
        float[] hipY = M(new[] { 0f, -0.12f, 0.12f, 0.25f, -0.75f, -0.75f, -0.40f, -0.22f, 0f }, L);
        float[] hipX = M(new[] { 0f, -0.04f, 0.04f, 0.10f, 0.12f, 0.12f, 0.08f, 0.03f, 0f }, L * f);
        c.PosRoot("HIP", hipX, hipY);

        c.Rot("BODY_1", M(new[] { 0f, -6f, 8f, 10f, 4f, 4f, 10f, 8f, 0f }, -f));
        c.Rot("BODY_2", M(new[] { 0f, -4f, 5f, 6f, 2f, 2f, 6f, 4f, 0f }, -f));
        // Head: looks where it goes (lifts a little while airborne)
        c.Rot("HEAD", M(new[] { 0f, 6f, -6f, -14f, -8f, -6f, -10f, -4f, 0f }, -f));

        // Legs: bend to push off, stay stretched back in the air and on the floor,
        // and tuck under the body (kneeling) when getting up
        foreach (var (pfx, k) in new[] { ("L", 1f), ("R", 0.85f) })
        {
            c.Rot(pfx + "_LEG_1", M(new[] { 0f, 28f, -12f, -18f, -6f, -6f, 42f, 62f, 0f }, f * k));
            c.Rot(pfx + "_LEG_2", M(new[] { 0f, -38f, 4f, 10f, 2f, 2f, -55f, -78f, 0f }, f * k));
            c.Rot(pfx + "_FOOT", M(new[] { 0f, 8f, -10f, -15f, -8f, -8f, 10f, 15f, 0f }, f * k));
        }

        // Punching arm: back on the anticipation, over the head on the launch (pointing forward once the
        // body lies down), then pushes the floor to get up
        c.Rot("R_ARM_1", M(new[] { 0f, -25f, 120f, 170f, 172f, 168f, 50f, 15f, 0f }, sR)); // shoulder: the whole arm
        c.Rot("R_ARM_2", M(new[] { 0f, -20f, 20f, 5f, 0f, 0f, 20f, 8f, 0f }, sR));
        c.Rot("R_ARM_3", M(new[] { 0f, 30f, 10f, 0f, 0f, 2f, -30f, -10f, 0f }, -sR));
        c.Rot("R_HAND_1", M(new[] { 0f, 15f, -5f, -10f, -10f, -8f, 15f, 5f, 0f }, sR));
        // The other arm follows (superman pose) and also helps getting up
        c.Rot("L_ARM_1", M(new[] { 0f, -20f, 110f, 165f, 170f, 165f, 300f, 345f, 360f }, sL)); // keeps rotating through the front (not over the top) until it rests on the floor
        c.Rot("L_ARM_2", M(new[] { 0f, -10f, 20f, 5f, 0f, 0f, 15f, 5f, 0f }, sL));
        c.Rot("L_ARM_3", M(new[] { 0f, 20f, 15f, 5f, 5f, 8f, -25f, -8f, 0f }, -sL));

        // Ears: swept back by the launch, bounce on landing
        foreach (var (pfx, side) in new[] { ("L", sL), ("R", sR) })
        {
            c.Rot(pfx + "_EAR_1", M(new[] { 0f, -4f, 8f, 14f, -6f, 4f, 6f, 2f, 0f }, side));
            c.Rot(pfx + "_EAR_2", M(new[] { 0f, -2f, 4f, 18f, 10f, 6f, 8f, 3f, 0f }, side));
        }
        return c.Build("CYCLOPS_Attack_Punch");
    }

    // STOMP (longer anticipation)
    static AnimationClip BuildStomp(Rig r, float W, float R)
    {
        // 0 neutral · 1 crouch and load · 2 starts lifting · 3 leg up · 4 holds up
        // 5 HIT · 6 bounce · 7 settles · 8 almost neutral · 9 neutral
        float[] t = { 0f, 0.25f * W, 0.50f * W, 0.75f * W, 0.90f * W, W, W + 0.10f * R, W + 0.30f * R, W + 0.55f * R, W + R };
        var c = new Clip(r, t);
        float L = r.Leg, sR = r.SideR, sL = r.SideL;

        float[] footL = M(new[] { 0f, 0f, 0.30f, 0.60f, 0.63f, 0f, 0f, 0f, 0f, 0f }, L); // L leg stomps
        float[] hipY = M(new[] { 0f, -0.10f, 0.04f, 0.10f, 0.12f, -0.18f, -0.07f, -0.11f, -0.03f, 0f }, L);
        float[] hipX = M(new[] { 0f, -0.02f, -0.07f, -0.10f, -0.10f, 0f, 0.02f, 0f, 0f, 0f }, L * sL); // weight on R

        c.PosRoot("HIP", hipX, hipY);
        int n = t.Length;
        var legL = new float[n]; var legR = new float[n]; var bend = new float[n]; var crouch = new float[n];
        for (int i = 0; i < n; i++)
        {
            legL[i] = footL[i] - hipY[i]; legR[i] = -hipY[i];
            bend[i] = footL[i] / (0.60f * L);
            crouch[i] = Mathf.Max(0f, -hipY[i] / (0.18f * L));
        }
        c.PosRoot("L_LEG_1", null, legL);
        c.PosRoot("R_LEG_1", null, legR);
        c.Rot("L_LEG_1", Map(Add(bend, crouch, 0.6f), b => sL * 16f * b));
        c.Rot("L_LEG_2", Map(Add(bend, crouch, 0.5f), b => -sL * 28f * b));
        c.Rot("L_FOOT", Map(bend, b => sL * 8f * b));
        c.Rot("R_LEG_1", Map(crouch, k => sR * 12f * k));
        c.Rot("R_LEG_2", Map(crouch, k => -sR * 8f * k));

        // Torso: crunches while loading, leans over the supporting leg and slams with the hit
        c.Rot("BODY_1", M(new[] { 0f, -2f, 4f, 7f, 8f, -4f, -1f, -2f, 0f, 0f }, sL));
        c.Rot("BODY_2", M(new[] { 0f, -1f, 2f, 5f, 6f, -3f, -3f, -1f, 0f, 0f }, sL));
        c.PosRoot("BODY_1", null, M(new[] { 0f, -0.03f, 0f, 0.01f, 0.02f, -0.06f, -0.02f, -0.03f, 0f, 0f }, L));
        c.Rot("HEAD", M(new[] { 0f, -3f, 3f, 5f, 6f, -7f, -2f, -3f, 0f, 0f }, -sL));

        // Arms: close while loading, open up for balance and drop with the impact
        foreach (var (pfx, side) in new[] { ("L", sL), ("R", sR) })
        {
            c.Rot(pfx + "_ARM_2", M(new[] { 0f, -10f, 30f, 60f, 65f, -10f, 6f, 0f, 0f, 0f }, side));
            c.Rot(pfx + "_ARM_3", M(new[] { 0f, 10f, 15f, 25f, 25f, 5f, 10f, 4f, 0f, 0f }, -side));
            c.Rot(pfx + "_EAR_1", M(new[] { 0f, 2f, -3f, -6f, -7f, 12f, 4f, 7f, 1f, 0f }, side));
            c.Rot(pfx + "_EAR_2", M(new[] { 0f, 1f, -2f, -7f, -8f, 9f, 13f, 5f, 2f, 0f }, side));
        }
        return c.Build("CYCLOPS_Attack_Stomp");
    }

    static float[] Add(float[] a, float[] b, float kb) { var r = new float[a.Length]; for (int i = 0; i < a.Length; i++) r[i] = a[i] + b[i] * kb; return r; }

    // Helpers
    static float[] M(float[] a, float k) { var r = new float[a.Length]; for (int i = 0; i < a.Length; i++) r[i] = a[i] * k; return r; }
    static float[] Neg(float[] a) => M(a, -1f);
    static float[] Map(float[] a, System.Func<float, float> f) { var r = new float[a.Length]; for (int i = 0; i < a.Length; i++) r[i] = f(a[i]); return r; }

    class Rig
    {
        public readonly GameObject Root; public readonly float Leg, SideL, SideR;
        /// <summary>+1 when "forward" is local +X (art facing right), -1 when it is -X.</summary>
        public float Forward = -1f;
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
            var t = rig.Find(bone); if (t == null) { Debug.LogWarning($"[Cyclops] {bone} not found"); return; }
            string path = AnimationUtility.CalculateTransformPath(t, rig.Root.transform);
            Vector3 e = t.localEulerAngles;
            Set(path, "localEulerAnglesRaw.x", Const(e.x));
            Set(path, "localEulerAnglesRaw.y", Const(e.y));
            Set(path, "localEulerAnglesRaw.z", Keys(e.z, delta));
        }

        // Offset in root space (x = side, y = up), accumulated per bone
        public void PosRoot(string bone, float[] dx, float[] dy)
        {
            var t = rig.Find(bone); if (t == null) { Debug.LogWarning($"[Cyclops] {bone} not found"); return; }
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
        report.AppendLine("Clips assigned to Attack_Punch and Attack_Stomp");
    }
}
#endif
