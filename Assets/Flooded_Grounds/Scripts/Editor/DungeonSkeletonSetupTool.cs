using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class DungeonSkeletonSetupTool
{
    private const string ScenePath = "Assets/Flooded_Grounds/Scenes/Scene_A.unity";
    private const string SkeletonModelPath = "Assets/DungeonCharacters/Skeletons_demo/models/DungeonSkeleton_demo.FBX";
    private const string IdleAnimPath = "Assets/DungeonCharacters/Skeletons_demo/animation/DS_onehand_idle_A.FBX";
    private const string WalkAnimPath = "Assets/DungeonCharacters/Skeletons_demo/animation/DS_onehand_walk.FBX";
    private const string AttackAnimPath = "Assets/DungeonCharacters/Skeletons_demo/animation/DS_onehand_attack_A.FBX";
    private const string ControllerPath = "Assets/Flooded_Grounds/Content/Enemies/DungeonSkeleton.controller";

    private static readonly Vector3 SpawnCenter = new Vector3(556.1f, 17.35f, 218.17f);
    private static readonly Vector3[] SpawnOffsets =
    {
        new Vector3(0f, 0f, 0f),
        new Vector3(2.2f, 0f, 1.4f),
        new Vector3(-2.1f, 0f, -1.6f)
    };

    [MenuItem("Tools/Flooded Grounds/Setup Dungeon Skeleton Enemies")]
    public static void SetupDungeonSkeletonEnemies()
    {
        Scene scene = EnsureTargetSceneOpen();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError("Could not open target scene: " + ScenePath);
            return;
        }

        GameObject skeletonModel = AssetDatabase.LoadAssetAtPath<GameObject>(SkeletonModelPath);
        if (skeletonModel == null)
        {
            Debug.LogError("Could not load skeleton model: " + SkeletonModelPath);
            return;
        }

        AnimationClip idleClip = LoadClipByName(IdleAnimPath, "DS_onehand_idle_A");
        AnimationClip walkClip = LoadClipByName(WalkAnimPath, "DS_onehand_walk");
        AnimationClip attackClip = LoadClipByName(AttackAnimPath, "DS_onehand_attack_A");

        if (idleClip == null || walkClip == null || attackClip == null)
        {
            Debug.LogError("Could not load one or more skeleton animation clips.");
            return;
        }

        EnsureFolder("Assets/Flooded_Grounds/Content");
        EnsureFolder("Assets/Flooded_Grounds/Content/Enemies");

        AnimatorController controller = EnsureAnimatorController(idleClip, walkClip, attackClip);
        if (controller == null)
        {
            Debug.LogError("Failed to create or load skeleton animator controller.");
            return;
        }

        RemoveExistingSkeletons();

        Transform player = null;
        GameObject playerObj = GameObject.Find("FpsController");
        if (playerObj != null)
        {
            player = playerObj.transform;
        }

        for (int i = 0; i < SpawnOffsets.Length; i++)
        {
            GameObject enemy = (GameObject)PrefabUtility.InstantiatePrefab(skeletonModel, scene);
            if (enemy == null)
            {
                continue;
            }

            enemy.name = "DungeonSkeleton_demo_" + (i + 1);
            Undo.RegisterCreatedObjectUndo(enemy, "Create dungeon skeleton enemy");

            enemy.transform.position = SpawnCenter + SpawnOffsets[i];
            enemy.transform.rotation = Quaternion.identity;

            Animator animator = enemy.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                animator = enemy.GetComponent<Animator>();
            }

            if (animator != null)
            {
                Undo.RecordObject(animator, "Assign skeleton animator controller");
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                EditorUtility.SetDirty(animator);
            }

            Collider col = enemy.GetComponent<Collider>();
            if (col == null)
            {
                col = enemy.GetComponentInChildren<Collider>();
            }

            if (col == null)
            {
                CapsuleCollider capsule = Undo.AddComponent<CapsuleCollider>(enemy);
                capsule.center = new Vector3(0f, 1.0f, 0f);
                capsule.height = 2.0f;
                capsule.radius = 0.35f;
            }

            DungeonSkeletonEnemyAI ai = enemy.GetComponent<DungeonSkeletonEnemyAI>();
            if (ai == null)
            {
                ai = Undo.AddComponent<DungeonSkeletonEnemyAI>(enemy);
            }

            Undo.RecordObject(ai, "Configure skeleton AI");
            ai.animator = animator;
            ai.player = player;
            ai.idleStateName = "DS_onehand_idle_A";
            ai.walkStateName = "DS_onehand_walk";
            ai.attackStateName = "DS_onehand_attack_A";
            ai.detectRange = 12f;
            ai.chaseStopRange = 1.6f;
            ai.wanderMoveSpeed = 1.2f;
            ai.chaseMoveSpeed = 2.2f;
            ai.attackCooldown = 1.25f;
            ai.attackHitDelay = 0.35f;
            ai.attackAnimationDuration = 0.8f;
            ai.playerForcedIdleSeconds = 0.2f;
            EditorUtility.SetDirty(ai);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log("Dungeon skeleton enemies setup completed and Scene_A saved.");
    }

    private static void RemoveExistingSkeletons()
    {
        GameObject[] existing = Object.FindObjectsOfType<GameObject>();
        foreach (GameObject go in existing)
        {
            if (go.name.StartsWith("DungeonSkeleton_demo_"))
            {
                Undo.DestroyObjectImmediate(go);
            }
        }
    }

    private static Scene EnsureTargetSceneOpen()
    {
        Scene currentScene = SceneManager.GetActiveScene();
        if (currentScene.IsValid() && currentScene.path == ScenePath)
        {
            return currentScene;
        }

        return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    private static AnimationClip LoadClipByName(string assetPath, string clipName)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        return assets.OfType<AnimationClip>().FirstOrDefault(c => c.name == clipName);
    }

    private static AnimatorController EnsureAnimatorController(AnimationClip idleClip, AnimationClip walkClip, AnimationClip attackClip)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        }

        if (controller == null)
        {
            return null;
        }

        AnimatorControllerLayer layer = controller.layers[0];
        AnimatorStateMachine sm = layer.stateMachine;

        sm.states = new ChildAnimatorState[0];

        AnimatorState idleState = sm.AddState("DS_onehand_idle_A");
        AnimatorState walkState = sm.AddState("DS_onehand_walk");
        AnimatorState attackState = sm.AddState("DS_onehand_attack_A");

        idleState.motion = idleClip;
        walkState.motion = walkClip;
        attackState.motion = attackClip;

        sm.defaultState = idleState;

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        int slash = path.LastIndexOf('/');
        if (slash <= 0)
        {
            return;
        }

        string parent = path.Substring(0, slash);
        string folderName = path.Substring(slash + 1);
        if (!AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        AssetDatabase.CreateFolder(parent, folderName);
    }
}
