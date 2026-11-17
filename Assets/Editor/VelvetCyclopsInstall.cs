#if UNITY_EDITOR
using System.Text;
using CodenameVelvet.Enemies;
using CodenameVelvet.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Instala el nuevo cerebro del cíclope:
/// 1) Animator: parámetros (IdleVariant, IsAttacking, AttackPunch, AttackStomp), estados de ataque y
///    transiciones (Walk → Idle / Idle_V1 según IdleVariant; Any State → ataques; ataques → Idle/Walk).
/// 2) Escena: desactiva EnemyChase (no se borra) y agrega RigidbodyEnemyMover, EnemyTargetFinder,
///    EnemyHealth y CyclopsBrain al CYCLOPS_Final, más una zona de deambular (BoxCollider trigger).
/// 3) PLAYER: tag "Player" y PlayerHealth, para que el cíclope lo pueda detectar y dañar.
/// Todo lo de la escena queda con Undo (Ctrl/Cmd+Z) y la escena marcada sin guardar.
/// </summary>
public static class VelvetCyclopsInstall
{
    const string ControllerPath = "Assets/Assets/BOSS/CYCLOPS_Final.controller";
    const string PlayerPrefabPath = "Assets/Assets/CHARACTERS/PLAYER.prefab";
    const string CyclopsName = "CYCLOPS_Final";

    [MenuItem("Tools/Velvet/Cíclope: instalar cerebro (estados y ataques)")]
    public static void Install()
    {
        var report = new StringBuilder();
        SetupAnimator(report);
        SetupScene(report);
        SetupPlayer(report);
        System.IO.File.WriteAllText("Logs/velvet_ciclope_cerebro.txt", report.ToString());
        Debug.Log("[Cíclope] Cerebro instalado.\n" + report);
    }

    // ─────────────── Animator ───────────────
    static void SetupAnimator(StringBuilder report)
    {
        var c = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (c == null) { report.AppendLine("ERROR: no encontré el controller"); return; }
        Ensure(c, "IsMoving", AnimatorControllerParameterType.Bool);
        Ensure(c, "IsAttacking", AnimatorControllerParameterType.Bool);
        Ensure(c, "IdleVariant", AnimatorControllerParameterType.Int);
        Ensure(c, "AttackPunch", AnimatorControllerParameterType.Trigger);
        Ensure(c, "AttackStomp", AnimatorControllerParameterType.Trigger);
        Ensure(c, "Speed", AnimatorControllerParameterType.Float);
        Ensure(c, "MoveX", AnimatorControllerParameterType.Float);
        Ensure(c, "MoveY", AnimatorControllerParameterType.Float);

        var sm = c.layers[0].stateMachine;
        AnimatorState idle = Find(sm, "Idle"), idleV1 = Find(sm, "Idle_V1"), walk = Find(sm, "Walk");
        if (idle == null || walk == null) { report.AppendLine("ERROR: faltan los estados Idle o Walk"); return; }

        AnimatorState punch = Find(sm, "Attack_Punch") ?? sm.AddState("Attack_Punch", new Vector3(750, 0, 0));
        AnimatorState stomp = Find(sm, "Attack_Stomp") ?? sm.AddState("Attack_Stomp", new Vector3(750, 120, 0));
        // Mientras no existan las animaciones, usan el Idle para no quedarse congelados en una pose rara.
        if (punch.motion == null) punch.motion = idle.motion;
        if (stomp.motion == null) stomp.motion = idle.motion;

        // Rehacer transiciones de forma limpia
        foreach (var s in sm.states) foreach (var t in s.state.transitions) s.state.RemoveTransition(t);
        foreach (var t in sm.anyStateTransitions) sm.RemoveAnyStateTransition(t);

        foreach (var from in new[] { idle, idleV1 })
        {
            if (from == null) continue;
            var t = Quick(from.AddTransition(walk), 0.2f);
            t.AddCondition(AnimatorConditionMode.If, 0, "IsMoving");
        }
        var toIdle = Quick(walk.AddTransition(idle), 0.25f);
        toIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsMoving");
        toIdle.AddCondition(AnimatorConditionMode.Equals, 0, "IdleVariant");
        if (idleV1 != null)
        {
            var toV1 = Quick(walk.AddTransition(idleV1), 0.25f);
            toV1.AddCondition(AnimatorConditionMode.IfNot, 0, "IsMoving");
            toV1.AddCondition(AnimatorConditionMode.Equals, 1, "IdleVariant");
        }

        foreach (var (state, trigger) in new[] { (punch, "AttackPunch"), (stomp, "AttackStomp") })
        {
            var any = Quick(sm.AddAnyStateTransition(state), 0.1f);
            any.canTransitionToSelf = false;
            any.AddCondition(AnimatorConditionMode.If, 0, trigger);

            var back = Quick(state.AddTransition(idle), 0.3f);
            back.AddCondition(AnimatorConditionMode.IfNot, 0, "IsAttacking");
            back.AddCondition(AnimatorConditionMode.IfNot, 0, "IsMoving");
            var backWalk = Quick(state.AddTransition(walk), 0.3f);
            backWalk.AddCondition(AnimatorConditionMode.IfNot, 0, "IsAttacking");
            backWalk.AddCondition(AnimatorConditionMode.If, 0, "IsMoving");
        }
        EditorUtility.SetDirty(c);
        AssetDatabase.SaveAssets();
        report.AppendLine("Animator: parámetros, Attack_Punch / Attack_Stomp y transiciones listos");
    }

    static AnimatorState Find(AnimatorStateMachine sm, string n)
    {
        foreach (var s in sm.states) if (s.state.name == n) return s.state;
        return null;
    }

    static AnimatorStateTransition Quick(AnimatorStateTransition t, float d)
    {
        t.hasExitTime = false; t.hasFixedDuration = true; t.duration = d;
        return t;
    }

    static void Ensure(AnimatorController c, string n, AnimatorControllerParameterType type)
    {
        foreach (var p in c.parameters) if (p.name == n) return;
        c.AddParameter(n, type);
    }

    // ─────────────── Escena ───────────────
    static void SetupScene(StringBuilder report)
    {
        Scene scene = SceneManager.GetActiveScene();
        GameObject cyclops = null, plane = null;
        foreach (var go in scene.GetRootGameObjects())
        {
            if (go.name == CyclopsName) cyclops = go;
            if (go.name == "Plane") plane = go;
        }
        if (cyclops == null) { report.AppendLine($"ERROR: no encontré {CyclopsName} en la escena abierta"); return; }

        Transform spriteRoot = null;
        foreach (var mb in cyclops.GetComponents<MonoBehaviour>())
        {
            if (mb == null || mb.GetType().Name != "EnemyChase") continue;
            var so = new SerializedObject(mb);
            spriteRoot = so.FindProperty("spriteRoot")?.objectReferenceValue as Transform;
            Undo.RecordObject(mb, "Desactivar EnemyChase");
            mb.enabled = false;
            report.AppendLine("EnemyChase desactivado (no se borró)");
        }

        if (cyclops.GetComponent<RigidbodyEnemyMover>() == null) Undo.AddComponent<RigidbodyEnemyMover>(cyclops);
        if (cyclops.GetComponent<EnemyTargetFinder>() == null) Undo.AddComponent<EnemyTargetFinder>(cyclops);
        if (cyclops.GetComponent<EnemyHealth>() == null) Undo.AddComponent<EnemyHealth>(cyclops);
        var brain = cyclops.GetComponent<CyclopsBrain>();
        if (brain == null) brain = Undo.AddComponent<CyclopsBrain>(cyclops);

        // Zona de deambular
        GameObject area = GameObject.Find("Cyclops Wander Area");
        if (area == null)
        {
            area = new GameObject("Cyclops Wander Area");
            Undo.RegisterCreatedObjectUndo(area, "Crear zona del cíclope");
            area.layer = 2; // Ignore Raycast
            var box = area.AddComponent<BoxCollider>();
            box.isTrigger = true;
            Bounds b = plane != null && plane.GetComponent<Renderer>() != null
                ? plane.GetComponent<Renderer>().bounds
                : new Bounds(cyclops.transform.position, new Vector3(30f, 1f, 30f));
            area.transform.position = new Vector3(b.center.x, b.center.y + 2f, b.center.z);
            box.size = new Vector3(Mathf.Max(2f, b.size.x - 4f), 4f, Mathf.Max(2f, b.size.z - 4f));
            report.AppendLine($"Zona creada: centro {area.transform.position}, tamaño {box.size} (a partir de {(plane != null ? "Plane" : "un área de 30x30")})");
        }

        var bso = new SerializedObject(brain);
        bso.FindProperty("wanderArea").objectReferenceValue = area.GetComponent<BoxCollider>();
        if (spriteRoot != null) bso.FindProperty("spriteRoot").objectReferenceValue = spriteRoot;
        var anim = cyclops.GetComponentInChildren<Animator>();
        if (anim != null) bso.FindProperty("animator").objectReferenceValue = anim;
        if (bso.FindProperty("attacks").arraySize == 0)
        {
            bso.ApplyModifiedProperties();
            var reset = typeof(CyclopsBrain).GetMethod("Reset", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Undo.RecordObject(brain, "Ataques por defecto");
            reset?.Invoke(brain, null);
            bso = new SerializedObject(brain);
            bso.FindProperty("wanderArea").objectReferenceValue = area.GetComponent<BoxCollider>();
            if (spriteRoot != null) bso.FindProperty("spriteRoot").objectReferenceValue = spriteRoot;
        }
        bso.ApplyModifiedProperties();
        report.AppendLine($"CyclopsBrain configurado (spriteRoot: {(spriteRoot != null ? spriteRoot.name : "ninguno")}, animator: {(anim != null ? anim.name : "ninguno")}, ataques: {new SerializedObject(brain).FindProperty("attacks").arraySize})");

        EditorSceneManager.MarkSceneDirty(scene);
    }

    // ─────────────── PLAYER ───────────────
    static void SetupPlayer(StringBuilder report)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            bool changed = false;
            if (root.tag != "Player") { root.tag = "Player"; changed = true; }
            if (root.GetComponent<PlayerHealth>() == null) { root.AddComponent<PlayerHealth>(); changed = true; }
            if (changed) PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            report.AppendLine(changed ? "PLAYER: tag Player + PlayerHealth agregados" : "PLAYER ya tenía tag y PlayerHealth");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
#endif
