using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ThirdPersonJuggernautSetupTool
{
    private const string PlayerObjectName = "FpsController";
    private const string ScenePath = "Assets/Flooded_Grounds/Scenes/Scene_A.unity";
    private const string JuggernautPrefabPath = "Assets/IronSpear Content/Iron_Juggernaut/Prefabs/MidPoly.prefab";
    private const string SkeletonModelPath = "Assets/DungeonCharacters/Skeletons_demo/models/DungeonSkeleton_demo.FBX";
    private const string SkeletonIdleAnimPath = "Assets/DungeonCharacters/Skeletons_demo/animation/DS_onehand_idle_A.FBX";
    private const string SkeletonWalkAnimPath = "Assets/DungeonCharacters/Skeletons_demo/animation/DS_onehand_walk.FBX";
    private const string SkeletonAttackAnimPath = "Assets/DungeonCharacters/Skeletons_demo/animation/DS_onehand_attack_A.FBX";
    private const string SkeletonControllerPath = "Assets/Flooded_Grounds/Content/Enemies/DungeonSkeleton.controller";

    private static readonly Vector3 SkeletonSpawnCenter = new Vector3(556.1f, 17.35f, 218.17f);
    private static readonly Vector3[] SkeletonOffsets =
    {
        new Vector3(0f, 0f, 0f),
        new Vector3(2.2f, 0f, 1.4f),
        new Vector3(-2.1f, 0f, -1.6f)
    };

    [MenuItem("Tools/Flooded Grounds/Setup Iron Juggernaut Third Person")]
    public static void Setup()
    {
        Scene activeScene = EnsureTargetSceneOpen();
        if (!activeScene.IsValid() || !activeScene.isLoaded)
        {
            Debug.LogError("Could not open target scene: " + ScenePath);
            return;
        }

        GameObject player = GameObject.Find(PlayerObjectName);
        if (player == null)
        {
            Debug.LogError("Could not find FpsController in the active scene.");
            return;
        }

        CharController_Motor motor = player.GetComponent<CharController_Motor>();
        if (motor == null)
        {
            Debug.LogError("FpsController does not have CharController_Motor.");
            return;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(JuggernautPrefabPath);
        if (prefab == null)
        {
            Debug.LogError("Could not load MidPoly prefab at expected path: " + JuggernautPrefabPath);
            return;
        }

        Transform modelRoot = player.transform.Find("JuggernautModel");
        if (modelRoot == null)
        {
            GameObject root = new GameObject("JuggernautModel");
            Undo.RegisterCreatedObjectUndo(root, "Create JuggernautModel root");
            modelRoot = root.transform;
            modelRoot.SetParent(player.transform, false);
        }

        Animator modelAnimator = modelRoot.GetComponentInChildren<Animator>(true);
        if (modelAnimator == null)
        {
            GameObject modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, activeScene);
            Undo.RegisterCreatedObjectUndo(modelInstance, "Instantiate MidPoly");
            modelInstance.name = "MidPoly";
            modelInstance.transform.SetParent(modelRoot, false);
            modelInstance.transform.localPosition = Vector3.zero;
            modelInstance.transform.localRotation = Quaternion.identity;
            modelInstance.transform.localScale = Vector3.one;
            modelAnimator = modelInstance.GetComponentInChildren<Animator>(true);
        }

        MeshRenderer meshRenderer = player.GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            Undo.RecordObject(meshRenderer, "Disable FpsController MeshRenderer");
            meshRenderer.enabled = false;
            EditorUtility.SetDirty(meshRenderer);
        }

        GameObject cameraObject = null;
        if (motor.cam != null)
        {
            cameraObject = motor.cam;
        }
        else
        {
            Camera childCamera = player.GetComponentInChildren<Camera>(true);
            if (childCamera != null)
            {
                cameraObject = childCamera.gameObject;
            }
            else if (Camera.main != null)
            {
                cameraObject = Camera.main.gameObject;
            }
        }

        if (cameraObject == null)
        {
            Debug.LogError("Could not find a camera to configure.");
            return;
        }

        if (cameraObject.transform.parent == player.transform)
        {
            Undo.SetTransformParent(cameraObject.transform, null, "Detach camera from player");
        }

        ThirdPersonOrbitCamera orbitCameraComponent = cameraObject.GetComponent<ThirdPersonOrbitCamera>();
        if (orbitCameraComponent == null)
        {
            orbitCameraComponent = Undo.AddComponent<ThirdPersonOrbitCamera>(cameraObject);
        }

        if (orbitCameraComponent == null)
        {
            Debug.LogError("Could not add ThirdPersonOrbitCamera. Ensure scripts are compiled.");
            return;
        }

        Undo.RecordObject(orbitCameraComponent, "Configure ThirdPersonOrbitCamera");
        orbitCameraComponent.target = player.transform;
        orbitCameraComponent.distance = 4.5f;
        orbitCameraComponent.height = 1.7f;
        orbitCameraComponent.sensitivity = 120f;
        orbitCameraComponent.minPitch = -20f;
        orbitCameraComponent.maxPitch = 60f;
        EditorUtility.SetDirty(orbitCameraComponent);

        Undo.RecordObject(motor, "Assign camera/animator on motor");
        motor.cam = cameraObject;
        motor.idleStateName = "Unreal Take 0";
        motor.walkStateName = "Unreal Take 5";
        motor.runStateName = "Unreal Take 3";
        motor.jumpStateName = "Unreal Take 1";
        motor.fallStateName = "Unreal Take 6";
        if (modelAnimator != null)
        {
            Undo.RecordObject(modelAnimator, "Disable Animator Root Motion");
            modelAnimator.applyRootMotion = false;
            EditorUtility.SetDirty(modelAnimator);
            motor.animator = modelAnimator;
        }
        EditorUtility.SetDirty(motor);

        CharacterController controller = player.GetComponent<CharacterController>();
        if (controller != null)
        {
            Undo.RecordObject(controller, "Tune CharacterController");
            controller.height = Mathf.Max(controller.height, 2.2f);
            controller.radius = Mathf.Max(controller.radius, 0.5f);
            controller.center = new Vector3(0f, controller.height * 0.5f, 0f);
            EditorUtility.SetDirty(controller);
        }

        SetupDungeonSkeletonEnemies(activeScene, player.transform);

        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);
        AssetDatabase.SaveAssets();
        Debug.Log("Iron Juggernaut + Dungeon Skeleton setup completed and Scene_A saved.");
    }

    private static void SetupDungeonSkeletonEnemies(Scene scene, Transform player)
    {
        GameObject skeletonModel = AssetDatabase.LoadAssetAtPath<GameObject>(SkeletonModelPath);
        if (skeletonModel == null)
        {
            Debug.LogError("Could not load skeleton model: " + SkeletonModelPath);
            return;
        }

        AnimationClip idleClip = LoadClipByName(SkeletonIdleAnimPath, "DS_onehand_idle_A");
        AnimationClip walkClip = LoadClipByName(SkeletonWalkAnimPath, "DS_onehand_walk");
        AnimationClip attackClip = LoadClipByName(SkeletonAttackAnimPath, "DS_onehand_attack_A");
        if (idleClip == null || walkClip == null || attackClip == null)
        {
            Debug.LogError("Could not load one or more skeleton animation clips.");
            return;
        }

        EnsureFolder("Assets/Flooded_Grounds/Content");
        EnsureFolder("Assets/Flooded_Grounds/Content/Enemies");

        AnimatorController controller = EnsureSkeletonController(idleClip, walkClip, attackClip);
        if (controller == null)
        {
            Debug.LogError("Could not create/load skeleton animator controller.");
            return;
        }

        GameObject[] existing = Object.FindObjectsOfType<GameObject>();
        foreach (GameObject go in existing)
        {
            if (go.name.StartsWith("DungeonSkeleton_demo_"))
                Undo.DestroyObjectImmediate(go);
        }

        for (int i = 0; i < SkeletonOffsets.Length; i++)
        {
            GameObject enemy = (GameObject)PrefabUtility.InstantiatePrefab(skeletonModel, scene);
            if (enemy == null)
                continue;

            Undo.RegisterCreatedObjectUndo(enemy, "Create dungeon skeleton enemy");
            enemy.name = "DungeonSkeleton_demo_" + (i + 1);
            enemy.transform.position = SkeletonSpawnCenter + SkeletonOffsets[i];
            enemy.transform.rotation = Quaternion.identity;

            Animator enemyAnimator = enemy.GetComponentInChildren<Animator>(true);
            if (enemyAnimator == null)
                enemyAnimator = enemy.GetComponent<Animator>();

            if (enemyAnimator != null)
            {
                Undo.RecordObject(enemyAnimator, "Assign skeleton animator");
                enemyAnimator.runtimeAnimatorController = controller;
                enemyAnimator.applyRootMotion = false;
                EditorUtility.SetDirty(enemyAnimator);
            }

            Collider col = enemy.GetComponent<Collider>();
            if (col == null)
                col = enemy.GetComponentInChildren<Collider>();

            if (col == null)
            {
                CapsuleCollider capsule = Undo.AddComponent<CapsuleCollider>(enemy);
                capsule.center = new Vector3(0f, 1.0f, 0f);
                capsule.height = 2.0f;
                capsule.radius = 0.35f;
            }

            DungeonSkeletonEnemyAI ai = enemy.GetComponent<DungeonSkeletonEnemyAI>();
            if (ai == null)
                ai = Undo.AddComponent<DungeonSkeletonEnemyAI>(enemy);

            Undo.RecordObject(ai, "Configure skeleton enemy AI");
            ai.player = player;
            ai.animator = enemyAnimator;
            ai.idleStateName = "DS_onehand_idle_A";
            ai.walkStateName = "DS_onehand_walk";
            ai.attackStateName = "DS_onehand_attack_A";
            ai.playerForcedIdleSeconds = 0.2f;
            EditorUtility.SetDirty(ai);
        }
    }

    private static AnimationClip LoadClipByName(string assetPath, string clipName)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        return assets.OfType<AnimationClip>().FirstOrDefault(c => c.name == clipName);
    }

    private static AnimatorController EnsureSkeletonController(AnimationClip idleClip, AnimationClip walkClip, AnimationClip attackClip)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(SkeletonControllerPath);
        if (controller == null)
            controller = AnimatorController.CreateAnimatorControllerAtPath(SkeletonControllerPath);

        if (controller == null)
            return null;

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        stateMachine.states = new ChildAnimatorState[0];

        AnimatorState idleState = stateMachine.AddState("DS_onehand_idle_A");
        AnimatorState walkState = stateMachine.AddState("DS_onehand_walk");
        AnimatorState attackState = stateMachine.AddState("DS_onehand_attack_A");

        idleState.motion = idleClip;
        walkState.motion = walkClip;
        attackState.motion = attackClip;
        stateMachine.defaultState = idleState;

        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        int slash = path.LastIndexOf('/');
        if (slash <= 0)
            return;

        string parent = path.Substring(0, slash);
        string folderName = path.Substring(slash + 1);
        if (!AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);

        AssetDatabase.CreateFolder(parent, folderName);
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
}
