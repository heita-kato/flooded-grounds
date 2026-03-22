using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ThirdPersonJuggernautSetupTool
{
    private const string PlayerObjectName = "FpsController";
    private const string ScenePath = "Assets/Flooded_Grounds/Scenes/Scene_A.unity";
    private const string JuggernautPrefabPath = "Assets/IronSpear Content/Iron_Juggernaut/Prefabs/MidPoly.prefab";

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

        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);
        AssetDatabase.SaveAssets();
        Debug.Log("Iron Juggernaut third-person setup completed and Scene_A saved.");
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
