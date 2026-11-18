#if UNITY_EDITOR
using CodenameVelvet.Enemies;
using CodenameVelvet.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Ajusta velocidad y rangos del cíclope de la escena abierta (con Undo).</summary>
public static class VelvetCyclopsTuning
{
    [MenuItem("Tools/Velvet/Cíclope: más rápido y más rango de detección")]
    public static void Apply()
    {
        var brain = Object.FindFirstObjectByType<CyclopsBrain>();
        if (brain == null) { Debug.LogError("[Cíclope] No hay CyclopsBrain en la escena abierta."); return; }
        var so = new SerializedObject(brain);
        Set(so, "wanderSpeed", 2.8f);
        Set(so, "chaseSpeed", 5.5f);
        Set(so, "detectionRange", 30f);
        Set(so, "loseInterestRange", 50f);
        so.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(brain.gameObject.scene);
        var log = "Deambular 2.8 · Persecución 5.5 · Detección 30 · Pierde interés 50";
        System.IO.File.WriteAllText("Logs/velvet_ciclope_tuning.txt", log);
        Debug.Log("[Cíclope] " + log);
    }

    [MenuItem("Tools/Velvet/Cíclope: ataques más lentos con ventana vulnerable")]
    public static void SlowerAttacks()
    {
        var brain = Object.FindFirstObjectByType<CyclopsBrain>();
        if (brain == null) { Debug.LogError("[Cíclope] No hay CyclopsBrain en la escena abierta."); return; }
        var so = new SerializedObject(brain);
        var arr = so.FindProperty("attacks");
        var log = new System.Text.StringBuilder();
        for (int i = 0; i < arr.arraySize; i++)
        {
            var a = arr.GetArrayElementAtIndex(i);
            string trigger = a.FindPropertyRelative("animatorTrigger").stringValue;
            if (trigger == "AttackPunch")
            {
                Rel(a, "windup", 1.3f); Rel(a, "recovery", 3.2f); Rel(a, "cooldown", 6f);
                Rel(a, "minGap", 1f); Rel(a, "maxGap", 7f);
                Rel(a, "lungeDistance", 6f); Rel(a, "lungeStart", 0.6f);
                Rel(a, "impactRadius", 2.8f); Rel(a, "impactForwardOffset", 2.5f);
                log.AppendLine("Puñetazo: golpe 1.3s, tirado/levantándose 3.2s, se avienta 6u (rango 1-7)");
            }
            else if (trigger == "AttackStomp")
            {
                Rel(a, "windup", 1.1f); Rel(a, "recovery", 1.2f); Rel(a, "cooldown", 3f);
                log.AppendLine("Pisotón: golpe 1.1s, recuperación 1.2s");
            }
        }
        so.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(brain.gameObject.scene);

        // El PLAYER necesita poder atacar para aprovechar la ventana
        const string playerPath = "Assets/Assets/CHARACTERS/PLAYER.prefab";
        var root = PrefabUtility.LoadPrefabContents(playerPath);
        try
        {
            if (root.GetComponent<PlayerMeleeAttack>() == null)
            {
                root.AddComponent<PlayerMeleeAttack>();
                PrefabUtility.SaveAsPrefabAsset(root, playerPath);
                log.AppendLine("PLAYER: se agregó PlayerMeleeAttack (botón Attack del Input System)");
            }
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }

        System.IO.File.WriteAllText("Logs/velvet_ciclope_tuning.txt", log.ToString());
        Debug.Log("[Cíclope] " + log);
    }

    [MenuItem("Tools/Velvet/Cíclope: dejar solo el Pisotón")]
    public static void OnlyStomp()
    {
        var brain = Object.FindFirstObjectByType<CyclopsBrain>();
        if (brain == null) { Debug.LogError("[Cíclope] No hay CyclopsBrain en la escena abierta."); return; }
        var so = new SerializedObject(brain);
        var arr = so.FindProperty("attacks");
        var log = new System.Text.StringBuilder();
        for (int i = arr.arraySize - 1; i >= 0; i--)
        {
            string trigger = arr.GetArrayElementAtIndex(i).FindPropertyRelative("animatorTrigger").stringValue;
            if (trigger != "AttackStomp") { arr.DeleteArrayElementAtIndex(i); log.AppendLine($"Quitado: {trigger}"); }
        }
        // Que descanse seguido para ver los dos idles
        var step = so.FindProperty("maxWanderStep"); if (step != null) step.floatValue = 12f;
        var rest = so.FindProperty("restChance"); if (rest != null) rest.floatValue = 0.6f;
        so.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(brain.gameObject.scene);
        log.AppendLine($"Ataques restantes: {new SerializedObject(brain).FindProperty("attacks").arraySize}");
        System.IO.File.WriteAllText("Logs/velvet_ciclope_tuning.txt", log.ToString());
        Debug.Log("[Cíclope] " + log);
    }

    static void Rel(SerializedProperty a, string field, float value)
    {
        var p = a.FindPropertyRelative(field);
        if (p != null) p.floatValue = value;
    }

    static void Set(SerializedObject so, string field, float value)
    {
        var p = so.FindProperty(field);
        if (p != null) p.floatValue = value;
    }
}
#endif
