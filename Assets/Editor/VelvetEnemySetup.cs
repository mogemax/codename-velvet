#if UNITY_EDITOR
using CodenameVelvet.Cameras;
using CodenameVelvet.Enemies;
using CodenameVelvet.Gathering;
using CodenameVelvet.Player;
using CodenameVelvet.Spawning;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// The enemies: one boar drawing with the perrito's rig and animations, in three types. Safe to run again.
/// <list type="number">
/// <item>Rigs ENEMIGO.psd from the perrito (<see cref="VelvetEnemyRig"/>).</item>
/// <item>The enemy prefab (every level's spawns use it) gets the boar as its drawing, the perrito's Animator
/// controller, a sword in its hand (shown for swordsmen), a bow (for archers) and its loot (rope, apple).</item>
/// <item>An archer's arrow: the players' arrow, slower and shorter, so it can be dodged.</item>
/// <item>The three types reuse the old placeholder types' assets (so the levels keep pointing at them): Unarmed
/// (was the witch), Swordsman (was the minotaur, 2 damage), Archer (was the siren).</item>
/// <item>Every spawn zone in the levels picks them 50 / 30 / 20.</item>
/// <item>Trees stop dropping rope (enemies drop it now); they keep the apple.</item>
/// </list>
/// Opens and saves the levels; save your own changes first.
/// </summary>
public static class VelvetEnemySetup
{
    private const string Tag = "[VelvetEnemySetup]";
    private const string OldPrefabPath = "Assets/Prefabs/Enemies/EnemyPlaceholder.prefab";
    private const string PrefabPath = "Assets/Prefabs/Enemies/Enemy.prefab";
    private const string PerritoPath = "Assets/Assets/CHARACTERS/PLAYER(S)/PERRITO.prefab";
    private const string ControllerPath = "Assets/Animators/PERRITO.controller";
    private const string SpriteMaterialPath = "Assets/Materials/Velvet_Sprite.mat";
    private const string ArrowPath = "Assets/Prefabs/Gathering/Arrow.prefab";
    private const string EnemyArrowPath = "Assets/Prefabs/Gathering/Arrow_Enemy.prefab";
    private const string HeldBowPath = "Assets/Prefabs/Gathering/Bow_Held.prefab";
    private const string RopePath = "Assets/Prefabs/Gathering/Pickup_Rope.prefab";
    private const string ApplePath = "Assets/Prefabs/Gathering/Pickup_Apple.prefab";
    private const string TreePath = "Assets/ScriptableObjects/Gathering/Arbol.asset";
    private const string EnemiesFolder = "Assets/ScriptableObjects/Enemies";
    private const string SwordHand = "HIP_1/BODY/L ARM_1/L HAND_1";
    private const float DrawingScale = 0.3f; // the players' drawing scale: the same size as them
    private const float EnemyArrowSpeed = 14f;   // the players' arrows fly at 30
    private const float EnemyArrowRange = 16f;

    private static readonly string[] Levels = { "Assets/Scenes/Level_01.unity", "Assets/Scenes/Level_02.unity" };

    [MenuItem("Tools/Velvet/Enemies/Build Enemies")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        VelvetEnemyRig.Rig();
        Arrow enemyArrow = EnemyArrow();
        if (enemyArrow == null || !BuildPrefab(enemyArrow))
            return;

        EnemyDefinition unarmed = Type("Bruja", "Enemy_Unarmed", "Unarmed", EnemyWeapon.None, damage: 1, chaseSpeed: 3f);
        EnemyDefinition swordsman = Type("Minotauro", "Enemy_Swordsman", "Swordsman", EnemyWeapon.Sword, damage: 2, chaseSpeed: 3.5f);
        EnemyDefinition archer = Type("Sirena", "Enemy_Archer", "Archer", EnemyWeapon.Bow, damage: 1, chaseSpeed: 3f);
        if (unarmed == null || swordsman == null || archer == null)
            return;
        Archer(archer);
        AssetDatabase.SaveAssets();

        string current = EditorSceneManager.GetActiveScene().path;
        foreach (string level in Levels)
        {
            var scene = EditorSceneManager.OpenScene(level, OpenSceneMode.Single);
            foreach (SpawnZone zone in Object.FindObjectsByType<SpawnZone>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Mix(zone, (unarmed, 50f), (swordsman, 30f), (archer, 20f));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        if (!string.IsNullOrEmpty(current)) EditorSceneManager.OpenScene(current, OpenSceneMode.Single);

        TreeWithoutRope();
        Debug.Log($"{Tag} Done");
    }

    private static Arrow EnemyArrow()
    {
        var arrow = AssetDatabase.LoadAssetAtPath<GameObject>(ArrowPath);
        if (arrow == null)
        {
            Debug.LogError($"{Tag} Missing {ArrowPath} (run Tools/Velvet/Weapons/Setup Bow first)");
            return null;
        }

        var variant = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyArrowPath);
        if (variant == null)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(arrow);
            variant = PrefabUtility.SaveAsPrefabAsset(instance, EnemyArrowPath);
            Object.DestroyImmediate(instance);
        }

        var serialized = new SerializedObject(variant.GetComponent<Arrow>());
        serialized.FindProperty("speed").floatValue = EnemyArrowSpeed;
        serialized.FindProperty("maxDistance").floatValue = EnemyArrowRange;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return variant.GetComponent<Arrow>();
    }

    private static bool BuildPrefab(Arrow enemyArrow)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
        {
            string error = AssetDatabase.MoveAsset(OldPrefabPath, PrefabPath); // keeps it: the levels point at it
            if (!string.IsNullOrEmpty(error))
            {
                Debug.LogError($"{Tag} {error}");
                return false;
            }
        }

        var drawingAsset = AssetDatabase.LoadAssetAtPath<GameObject>(VelvetEnemyRig.EnemyPath);
        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
        var material = AssetDatabase.LoadAssetAtPath<Material>(SpriteMaterialPath);
        if (drawingAsset == null || controller == null || material == null)
        {
            Debug.LogError($"{Tag} Missing the boar drawing, {ControllerPath} or {SpriteMaterialPath}");
            return false;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform old = root.transform.Find("Visual");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var drawing = (GameObject)PrefabUtility.InstantiatePrefab(drawingAsset, root.transform);
            drawing.name = "Visual";
            drawing.transform.localPosition = Vector3.zero;
            drawing.transform.localScale = Vector3.one * DrawingScale;
            var animator = drawing.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            drawing.AddComponent<ViewTilt>();
            foreach (SpriteRenderer part in drawing.GetComponentsInChildren<SpriteRenderer>(true))
                part.sharedMaterial = material;

            GameObject sword = Sword(drawing.transform);
            if (sword == null) return false;

            var view = Component<EnemyView>(root);
            view.FindProperty("animator").objectReferenceValue = animator;
            view.FindProperty("drawing").objectReferenceValue = drawing.transform;
            view.FindProperty("artFacesRight").boolValue = true;
            view.FindProperty("sword").objectReferenceValue = sword;
            view.ApplyModifiedPropertiesWithoutUndo();

            var bow = Component<EnemyBow>(root);
            bow.FindProperty("arrowPrefab").objectReferenceValue = enemyArrow;
            bow.FindProperty("heldBowPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<HeldBowView>(HeldBowPath);
            bow.FindProperty("drawing").objectReferenceValue = drawing.transform;
            bow.ApplyModifiedPropertiesWithoutUndo();

            var loot = Component<EnemyLoot>(root);
            SerializedProperty drops = loot.FindProperty("drops");
            drops.arraySize = 2;
            SetDrop(drops.GetArrayElementAtIndex(0), RopePath, 0.5f);
            SetDrop(drops.GetArrayElementAtIndex(1), ApplePath, 0.35f);
            loot.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static SerializedObject Component<T>(GameObject root) where T : Component =>
        new SerializedObject(root.TryGetComponent(out T existing) ? existing : root.AddComponent<T>());

    /// <summary>The perrito's held sword, copied into the same hand bone of the boar (the bones match).</summary>
    private static GameObject Sword(Transform drawing)
    {
        var perrito = AssetDatabase.LoadAssetAtPath<GameObject>(PerritoPath);
        Transform model = null;
        foreach (Transform t in perrito.GetComponentsInChildren<Transform>(true))
            if (t.name == "Sword") model = t;
        Transform hand = drawing.Find(SwordHand);
        if (model == null || hand == null)
        {
            Debug.LogError($"{Tag} No sword in {PerritoPath} or no {SwordHand} in the boar");
            return null;
        }

        var sword = new GameObject("Sword");
        sword.transform.SetParent(hand, false);
        sword.transform.localPosition = model.localPosition;
        sword.transform.localRotation = model.localRotation;
        sword.transform.localScale = model.localScale;
        var from = model.GetComponent<SpriteRenderer>();
        var renderer = sword.AddComponent<SpriteRenderer>();
        renderer.sprite = from.sprite;
        renderer.sharedMaterial = from.sharedMaterial;
        renderer.sortingOrder = from.sortingOrder;
        sword.SetActive(false);
        return sword;
    }

    private static void SetDrop(SerializedProperty drop, string prefabPath, float chance)
    {
        drop.FindPropertyRelative("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        drop.FindPropertyRelative("chance").floatValue = chance;
    }

    /// <summary>An old placeholder type's asset, renamed and turned into one of the boar's types.</summary>
    private static EnemyDefinition Type(string oldName, string newName, string displayName, EnemyWeapon weapon, int damage, float chaseSpeed)
    {
        string path = $"{EnemiesFolder}/{newName}.asset";
        if (AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path) == null)
        {
            string error = AssetDatabase.RenameAsset($"{EnemiesFolder}/{oldName}.asset", newName);
            if (!string.IsNullOrEmpty(error))
            {
                Debug.LogError($"{Tag} {error}");
                return null;
            }
        }

        var definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
        var serialized = new SerializedObject(definition);
        serialized.FindProperty("displayName").stringValue = displayName;
        serialized.FindProperty("color").colorValue = Color.white; // the drawing, untinted
        serialized.FindProperty("weapon").enumValueIndex = (int)weapon;
        serialized.FindProperty("attackDamage").intValue = damage;
        serialized.FindProperty("chaseSpeed").floatValue = chaseSpeed;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(definition);
        return definition;
    }

    /// <summary>Archers see further and shoot from afar, every 2.5 s, after a 0.6 s draw.</summary>
    private static void Archer(EnemyDefinition archer)
    {
        var serialized = new SerializedObject(archer);
        serialized.FindProperty("detectionRange").floatValue = 9f;
        serialized.FindProperty("loseInterestRange").floatValue = 12f;
        serialized.FindProperty("shootRange").floatValue = 8f;
        serialized.FindProperty("drawSeconds").floatValue = 0.6f;
        serialized.FindProperty("attackCooldown").floatValue = 2.5f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(archer);
    }

    private static void Mix(SpawnZone zone, params (EnemyDefinition definition, float weight)[] types)
    {
        var serialized = new SerializedObject(zone);
        SerializedProperty entries = serialized.FindProperty("enemies");
        entries.arraySize = types.Length;
        for (int i = 0; i < types.Length; i++)
        {
            SerializedProperty entry = entries.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("definition").objectReferenceValue = types[i].definition;
            entry.FindPropertyRelative("weight").floatValue = types[i].weight;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void TreeWithoutRope()
    {
        var tree = AssetDatabase.LoadAssetAtPath<ResourceNodeDefinition>(TreePath);
        var rope = AssetDatabase.LoadAssetAtPath<GameObject>(RopePath);
        if (tree == null) return;

        var serialized = new SerializedObject(tree);
        SerializedProperty drops = serialized.FindProperty("bonusDrops");
        for (int i = drops.arraySize - 1; i >= 0; i--)
            if (drops.GetArrayElementAtIndex(i).FindPropertyRelative("prefab").objectReferenceValue == rope)
                drops.DeleteArrayElementAtIndex(i);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(tree);
        AssetDatabase.SaveAssets();
    }
}
#endif
