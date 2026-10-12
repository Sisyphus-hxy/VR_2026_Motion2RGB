using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Motion2RGB.Editor
{
    public static class RobotSceneUpgrade
    {
        private const string Root = "Assets/Motion2RGB";
        private const string ModelPath = Root + "/ThirdParty/RobotKyle/Model/Robot Kyle.fbx";

        [MenuItem("Tools/Motion2RGB/Create Robot Scene From Current Scene")]
        public static void CreateRobotScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before creating the robot scene.");
            Scene source = SceneManager.GetActiveScene();
            FirstPersonController controller = UnityEngine.Object.FindObjectOfType<FirstPersonController>();
            if (controller == null || controller.cleanCamera == null || controller.degradedCamera == null)
                throw new InvalidOperationException("Open the existing Motion2RGB demo scene first.");
            if (controller.GetComponent<RobotGaitAnimator>() != null)
                throw new InvalidOperationException("This scene already contains the robot upgrade.");
            string path = AssetDatabase.GenerateUniqueAssetPath(Root + "/Scenes/Motion2RGB_Robot.unity");
            if (!EditorSceneManager.SaveScene(source, path, true)) return;
            EditorSceneManager.OpenScene(path);
            controller = UnityEngine.Object.FindObjectOfType<FirstPersonController>();
            AttachRobot(controller);
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log("Robot scene created: " + path);
        }

        public static void AttachRobot(FirstPersonController controller)
        {
            ModelImporter importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Robot Kyle FBX is not imported yet.");
            importer.isReadable = true;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = false;
            importer.SaveAndReimport();
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            GameObject robot = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            robot.name = "Robot Kyle";
            Transform rig = controller.transform;
            robot.transform.SetParent(rig, false);
            Animator importedAnimator = robot.GetComponent<Animator>();
            if (importedAnimator != null) importedAnimator.enabled = false;
            SkinnedMeshRenderer skin = robot.GetComponentInChildren<SkinnedMeshRenderer>();
            if (skin == null) throw new InvalidOperationException("Robot model has no skinned mesh.");
            Bounds bounds = skin.bounds;
            float scale = 1.8f / Mathf.Max(0.01f, bounds.size.y);
            robot.transform.localScale *= scale;
            bounds = skin.bounds;
            robot.transform.position += Vector3.up * (rig.position.y - 1.05f - bounds.min.y);
            Material robotMaterial = CreateRobotMaterial();
            Material[] materials = new Material[Mathf.Max(1, skin.sharedMesh.subMeshCount)];
            for (int i = 0; i < materials.Length; i++) materials[i] = robotMaterial;
            skin.sharedMaterials = materials;
            skin.updateWhenOffscreen = true;
            Transform head = Find(robot, "Head");
            Transform neck = Find(robot, "Neck");
            if (head == null) throw new InvalidOperationException("Robot head bone not found.");

            // Original whole robot is visible only in the observer camera.
            foreach (Renderer renderer in robot.GetComponentsInChildren<Renderer>()) renderer.gameObject.layer = 2;
            GameObject bodyObject = new GameObject("First Person Body", typeof(SkinnedMeshRenderer));
            bodyObject.transform.SetParent(skin.transform, false);
            SkinnedMeshRenderer body = bodyObject.GetComponent<SkinnedMeshRenderer>();
            body.sharedMesh = MakeBodyMesh(skin, head, neck);
            body.sharedMaterials = skin.sharedMaterials;
            body.bones = skin.bones;
            body.rootBone = skin.rootBone;
            body.localBounds = skin.localBounds;
            body.updateWhenOffscreen = true;

            Camera clean = controller.cleanCamera;
            Camera degraded = controller.degradedCamera;
            GaitMotionDriver oldGait = degraded.GetComponent<GaitMotionDriver>();
            if (oldGait != null) UnityEngine.Object.DestroyImmediate(oldGait);
            Transform reference = new GameObject("Stabilized Head Reference").transform;
            reference.SetParent(rig, false);
            reference.position = head.position + rig.forward * 0.10f;
            reference.rotation = rig.rotation;
            Transform headMount = new GameObject("Robot Head Camera Mount").transform;
            headMount.SetParent(head, false);
            headMount.SetPositionAndRotation(reference.position, rig.rotation);
            Transform look = new GameObject("Head Look").transform;
            look.SetParent(headMount, false);
            clean.transform.SetParent(look, false);
            degraded.transform.SetParent(look, false);
            clean.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            degraded.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            clean.cullingMask &= ~(1 << 2);
            degraded.cullingMask &= ~(1 << 2);
            GaitMotionDriver gait = headMount.gameObject.AddComponent<GaitMotionDriver>();
            gait.tracker = controller.GetComponent<MotionStateTracker>();
            CameraDegradationController degradation = degraded.GetComponent<CameraDegradationController>();
            degradation.gaitDriver = gait;
            degradation.blurIntensity = 0.9f;
            degradation.exposureProxy = 0.8f;
            controller.headLookPivot = reference;
            RobotGaitAnimator animation = rig.gameObject.AddComponent<RobotGaitAnimator>();
            animation.gait = gait;
            animation.cleanCamera = clean;
            animation.headReference = reference;
            animation.cameraLook = look;
            animation.leftHip = Find(robot, "Left_Thigh_Joint_01");
            animation.rightHip = Find(robot, "Right_Thigh_Joint_01");
            animation.leftKnee = Find(robot, "Left_Knee_Joint_01");
            animation.rightKnee = Find(robot, "Right_Knee_Joint_01");
            animation.leftFoot = Find(robot, "Left_Ankle_Joint_01");
            animation.rightFoot = Find(robot, "Right_Ankle_Joint_01");
            animation.leftShoulder = Find(robot, "Left_Upper_Arm_Joint_01");
            animation.rightShoulder = Find(robot, "Right_Upper_Arm_Joint_01");
            LowerArm(animation.leftShoulder, robot.transform);
            LowerArm(animation.rightShoulder, robot.transform);
            ConfigureUI(gait, animation, bodyObject);
        }

        private static void LowerArm(Transform bone, Transform model)
        {
            if (bone == null || bone.childCount == 0) return;
            Vector3 direction = bone.GetChild(0).position - bone.position;
            bone.rotation = Quaternion.FromToRotation(direction, -model.up) * bone.rotation;
        }

        private static Transform Find(GameObject root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>())
                if (child.name == name) return child;
            return null;
        }

        private static Material CreateRobotMaterial()
        {
            string path = Root + "/ThirdParty/RobotKyle/RobotKyle_Builtin.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/ThirdParty/RobotKyle/Textures/Robot_Color.tga");
            material.color = Color.white;
            string normalPath = Root + "/ThirdParty/RobotKyle/Textures/Robot_Normal.tga";
            TextureImporter normalImporter = AssetImporter.GetAtPath(normalPath) as TextureImporter;
            if (normalImporter != null && normalImporter.textureType != TextureImporterType.NormalMap)
            {
                normalImporter.textureType = TextureImporterType.NormalMap;
                normalImporter.SaveAndReimport();
            }
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
            material.EnableKeyword("_NORMALMAP");
            material.SetFloat("_Metallic", 0.3f);
            material.SetFloat("_Glossiness", 0.35f);
            return material;
        }

        private static Mesh MakeBodyMesh(SkinnedMeshRenderer skin, Transform head, Transform neck)
        {
            Mesh mesh = UnityEngine.Object.Instantiate(skin.sharedMesh);
            mesh.name = "Robot Kyle First Person Body";
            BoneWeight[] weights = mesh.boneWeights;
            bool[] hiddenBones = new bool[skin.bones.Length];
            for (int i = 0; i < hiddenBones.Length; i++)
                hiddenBones[i] = skin.bones[i] == head || skin.bones[i] == neck || skin.bones[i].IsChildOf(head);
            bool[] hiddenVertices = new bool[weights.Length];
            for (int i = 0; i < weights.Length; i++)
            {
                BoneWeight w = weights[i];
                float headWeight = (hiddenBones[w.boneIndex0] ? w.weight0 : 0f)
                    + (hiddenBones[w.boneIndex1] ? w.weight1 : 0f)
                    + (hiddenBones[w.boneIndex2] ? w.weight2 : 0f)
                    + (hiddenBones[w.boneIndex3] ? w.weight3 : 0f);
                hiddenVertices[i] = headWeight > 0.35f;
            }
            for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                int[] triangles = mesh.GetTriangles(submesh);
                List<int> bodyTriangles = new List<int>();
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    if (hiddenVertices[triangles[i]] || hiddenVertices[triangles[i + 1]] || hiddenVertices[triangles[i + 2]]) continue;
                    bodyTriangles.Add(triangles[i]);
                    bodyTriangles.Add(triangles[i + 1]);
                    bodyTriangles.Add(triangles[i + 2]);
                }
                mesh.SetTriangles(bodyTriangles, submesh);
            }
            string path = AssetDatabase.GenerateUniqueAssetPath(Root + "/ThirdParty/RobotKyle/FirstPersonBody.asset");
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static void ConfigureUI(GaitMotionDriver gait, RobotGaitAnimator animation, GameObject body)
        {
            DemoUIController ui = UnityEngine.Object.FindObjectOfType<DemoUIController>();
            if (ui == null) return;
            ui.gait = gait;
            if (ui.gaitAmplitudeSlider != null)
            {
                ui.gaitAmplitudeSlider.maxValue = 0.2f;
                ui.gaitAmplitudeSlider.value = gait.verticalAmplitude;
            }
            RenderTexture texture = new RenderTexture(400, 300, 24) { name = "Robot Observer", antiAliasing = 1 };
            AssetDatabase.CreateAsset(texture, AssetDatabase.GenerateUniqueAssetPath(Root + "/RenderTextures/RobotObserver.renderTexture"));
            GameObject cameraObject = new GameObject("Robot Observer Camera", typeof(Camera), typeof(RobotObserverCamera));
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.targetTexture = texture;
            camera.fieldOfView = 48f;
            // Include the environment and complete robot, exclude only duplicate body via layer 3.
            body.layer = 3;
            camera.cullingMask = ~(1 << 3);
            cameraObject.GetComponent<RobotObserverCamera>().target = animation.transform;
            GameObject display = new GameObject("Robot Observer View", typeof(RectTransform), typeof(RawImage));
            display.transform.SetParent(ui.transform, false);
            RectTransform rect = display.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.78f, 0.69f);
            rect.anchorMax = new Vector2(0.99f, 0.96f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            RawImage image = display.GetComponent<RawImage>();
            image.texture = texture;
            image.raycastTarget = false;
        }
    }
}
