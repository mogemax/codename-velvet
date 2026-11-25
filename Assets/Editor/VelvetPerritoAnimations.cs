#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// The PERRITO animations (Idle, Walk, Dash, Climb and Attack) and its Animator Controller: the player's one, with its
/// own clips, plus an upper-body "Attack" layer so it can swing while walking.
/// Same sign convention as the player: + = the tip of a hanging bone moves forward.
/// </summary>
public static partial class VelvetPlayerIdleGenerator
{
    const string PerritoRig = "Assets/Assets/CHARACTERS/PLAYER(S)/PERRITO.psd";
    const string PerritoPrefab = "Assets/Assets/CHARACTERS/PLAYER(S)/PERRITO.prefab";
    const string PerritoController = "Assets/Animators/PERRITO.controller";

    static ClipDef PerritoIdle()
    {
        var c = new ClipDef { name = "PERRITO_Idle", duration = 2.4f, loop = true, samples = 12 };
        (string b, float amp, float ph)[] rot =
        {
            ("BODY", 1f, 0f), ("HEAD", -1.5f, 0.1f), ("CREST", 1.5f, 0.25f),
            ("R ARM", -3f, 0.08f), ("R HAND", -4f, 0.2f), ("L ARM", 3f, 0.08f), ("L HAND", 4f, 0.2f),
            ("SKIRT R", -2f, 0.2f), ("SKIRT L", 2f, 0.2f),
        };
        foreach (var r in rot) { var rr = r; c.Rot(rr.b, u => rr.amp * S(u, 1, -TwoPi * rr.ph)); }
        return c;
    }

    // Poses as in the player's walk: 0 Contact, 1 Recoil, 2 Passing, 3 High-Point, then the other leg.
    static ClipDef PerritoWalk()
    {
        var c = new ClipDef { name = "PERRITO_Walk", duration = 0.6f, loop = true };
        float[] thigh = { 18f, 11f, 0f, -11f, -18f, -5f, 12f, 22f };
        float[] knee  = { 0f, -5f, -3f, 0f, -6f, -18f, -24f, -10f };
        float[] arm   = { 24f, 20f, 0f, -14f, -24f, -20f, 0f, 14f };
        float[] hand  = { 12f, 16f, 8f, 0f, -4f, 0f, 4f, 8f };

        c.Poses("L LEG", thigh);   c.Poses("R LEG", Shift(thigh, 4));
        c.Poses("L LEG 2", knee);  c.Poses("R LEG 2", Shift(knee, 4));
        c.Poses("L FOOT", Flat(thigh, knee)); c.Poses("R FOOT", Shift(Flat(thigh, knee), 4));
        c.Poses("R ARM", arm);     c.Poses("L ARM", Shift(arm, 4));
        c.Poses("R HAND", hand);   c.Poses("L HAND", Shift(hand, 4));
        c.PosesY("HIP", Twice(new[] { 0f, -0.25f, 0.1f, 0.3f }));
        // Seen from the front the knees cannot lift the feet: the waddle does (+ raises the L leg)
        c.Poses("HIP", new[] { 0f, -4f, -6f, -4f, 0f, 4f, 6f, 4f });
        c.Poses("BODY", new[] { -4f, 0f, 2f, 0f, -4f, -8f, -10f, -8f });
        c.Poses("HEAD", new[] { 1f, 0f, -1f, -1f, 1f, 2f, 3f, 2f });
        c.Poses("CREST", Twice(new[] { 2f, 4f, 1f, -1f }));
        c.Poses("SKIRT R", Twice(new[] { -4f, 2f, 0f, -6f }));
        c.Poses("SKIRT L", Twice(new[] { 4f, -2f, 0f, 6f }));
        return c;
    }

    // Foot rotation that undoes most of the leg's, so the soles stay near the ground
    static float[] Flat(float[] thigh, float[] knee) => thigh.Zip(knee, (t, k) => -0.8f * (t + k)).ToArray();

    // Head-down charge (the body is too round for the player's dive roll); loops while IsDashing
    static ClipDef PerritoDash()
    {
        var c = new ClipDef { name = "PERRITO_Dash", duration = 0.3f, loop = true };
        float[] thigh = { 24f, 0f, -22f, 10f };
        float[] knee  = { -5f, -5f, -15f, -35f };
        c.Poses("L LEG", thigh);  c.Poses("R LEG", Shift(thigh, 2));
        c.Poses("L LEG 2", knee); c.Poses("R LEG 2", Shift(knee, 2));
        c.Poses("L FOOT", Flat(thigh, knee)); c.Poses("R FOOT", Shift(Flat(thigh, knee), 2));
        c.PosesY("HIP", new[] { -0.1f, 0.35f, -0.1f, 0.35f });
        c.Poses("BODY", new[] { -22f, -25f, -22f, -25f });
        c.Poses("HEAD", new[] { -8f, -6f, -8f, -6f });
        c.Poses("CREST", new[] { 9f, 6f, 9f, 6f });
        c.Poses("R ARM", new[] { -50f, -56f, -50f, -56f });
        c.Poses("L ARM", new[] { -40f, -46f, -40f, -46f });
        c.Poses("R HAND", new[] { -10f, -16f, -10f, -16f });
        c.Poses("L HAND", new[] { -10f, -16f, -10f, -16f });
        c.Poses("SKIRT R", new[] { -10f, -6f, -10f, -6f });
        c.Poses("SKIRT L", new[] { -6f, -2f, -6f, -2f });
        return c;
    }

    static ClipDef PerritoClimb()
    {
        var c = new ClipDef { name = "PERRITO_Climb", duration = 0.7f, loop = true };
        // The near arm reaches up on its own side: forward it would hide behind the head
        float[] arm   = { 140f, 118f, 92f, 110f };
        float[] hand  = { 5f, 20f, 35f, 30f };
        float[] nearArm = arm.Select(a => -a).ToArray(), nearHand = hand.Select(a => -a).ToArray();
        float[] thigh = { 25f, 15f, 5f, 15f };
        float[] knee  = { -30f, -20f, -5f, -28f };
        c.Poses("R ARM", nearArm);   c.Poses("L ARM", Shift(arm, 2));
        c.Poses("R HAND", nearHand); c.Poses("L HAND", Shift(hand, 2));
        c.Poses("L LEG", thigh);  c.Poses("R LEG", Shift(thigh, 2));
        c.Poses("L LEG 2", knee); c.Poses("R LEG 2", Shift(knee, 2));
        c.PosesY("HIP", new[] { -0.2f, 0.2f, -0.2f, 0.2f });
        c.Poses("BODY", new[] { -5f, -2f, 1f, -2f });
        c.Poses("HEAD", new[] { 5f, 4f, 5f, 4f });
        c.Poses("CREST", new[] { 2f, 0f, 2f, 0f });
        return c;
    }

    // Downward claw swipe with the far arm (the near one would hide under the head), upper body only. Damage lands on the press, so the wind-up is very short.
    // 0 Neutral · 1 Wind-up · 2 Strike · 3 Follow-through · 4 Neutral
    static ClipDef PerritoAttack()
    {
        var c = new ClipDef { name = "PERRITO_Attack", duration = 0.38f, loop = false };
        float[] T = { 0f, 0.06f, 0.13f, 0.22f, 0.38f };
        c.Keys("BODY",   T, new[] { 0f,   7f, -15f, -12f, 0f });
        c.Keys("HEAD",   T, new[] { 0f,   3f,  -5f,  -2f, 0f });
        c.Keys("CREST",  T, new[] { 0f,  -4f,   8f,   4f, 0f });
        c.Keys("L ARM",  T, new[] { 0f, 150f,  65f,  45f, 0f });
        c.Keys("L HAND", T, new[] { 0f,  30f, -10f, -15f, 0f });
        c.Keys("R ARM",  T, new[] { 0f, -15f, -45f, -38f, 0f });
        c.Keys("R HAND", T, new[] { 0f, -10f, -20f, -15f, 0f });
        return c;
    }

    [MenuItem("Tools/Velvet/Perrito Animations/Generate All")]
    public static void GeneratePerrito()
    {
        Transform root = AssetDatabase.LoadAssetAtPath<GameObject>(PerritoRig).transform;
        Transform hip = FindBone(root, "HIP");
        if (hip == null) { Debug.LogError("[Anim] HIP bone not found inside PERRITO."); return; }

        // The player's controller gives the base layer: same parameters, states and transitions
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(PerritoController) == null)
            AssetDatabase.CopyAsset(ControllerPath, PerritoController);
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(PerritoController);
        foreach (ClipDef def in new[] { PerritoIdle(), PerritoWalk(), PerritoDash(), PerritoClimb() })
            controller.layers[0].stateMachine.states.First(s => "PERRITO_" + s.state.name == def.name).state.motion = BuildClip(def, root, hip);

        EnsureAttackLayer(controller, BuildClip(PerritoAttack(), root, hip));
        AssetDatabase.SaveAssets();
        foreach (string name in new[] { "Walk", "Dash" })
            VelvetGroundSolver.Solve(PerritoPrefab, $"{AnimFolder}/PERRITO_{name}.anim", $"Logs/velvet_ground_perrito_{name}.txt");
        Debug.Log("[Anim] PERRITO clips and controller ready.");
    }
}
#endif
