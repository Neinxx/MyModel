using UnityEngine;
using UnityEditor;
using Cinemachine;
using CameraSystem.Runtime;
using CharacterController.Runtime;
using System.IO;

namespace CameraSystem.Editor
{
    /// <summary>
    /// 一键角色相机自适应适配与完美相机预制体生成工具 (Character Camera Adaptor)
    /// 遵循 style.md 的高对比度工业风编辑器设计，自动生成完美相机预制体、注入核心组件、绑定目标与配置消解冲突。
    /// 极度智能：不再需要现有的相机预制体，一键生成全新完美的 CameraView 预制体并自动覆盖配置。
    /// </summary>
    public class CameraSetupTool : EditorWindow
    {
        [MenuItem("Tools/Camera System/Character Camera Adaptor")]
        public static void ShowWindow()
        {
            CameraSetupTool window = GetWindow<CameraSetupTool>("Camera Adaptor");
            window.minSize = new Vector2(500, 680);
            window.maxSize = new Vector2(800, 980);
            window.Show();
        }

        private GameObject _characterTarget;
        private string _prefabSavePath = "Assets/Demo/Art/Prefabs/CameraView.prefab";
        private string _characterLogicPrefabSavePath =
            "Assets/Demo/Art/Prefabs/Character_logic.prefab";
        private string _uiCameraPrefabSavePath = "Assets/Demo/Art/Prefabs/UICamera.prefab";
        private string _uiRootPrefabSavePath = "Assets/Demo/Art/Prefabs/UIRoot.prefab";
        private Vector3 _pivotOffset = new Vector3(0f, 1.5f, 0f);
        private float _cameraDistance = 6.0f;
        private Vector3 _shoulderOffset = new Vector3(0.5f, -0.4f, 0f);

        private enum CameraComponentType
        {
            ThirdPersonFollow,
            Transposer,
            FramingTransposer
        }

        private CameraComponentType _cameraComponentType = CameraComponentType.ThirdPersonFollow;
        private Vector2 _scrollPosition;

        private void OnEnable()
        {
            // 智能感知：如果是场景内激活的 GameObject，则作为默认适配目标
            if (_characterTarget == null && Selection.activeGameObject != null)
            {
                if (Selection.activeGameObject.scene.IsValid())
                {
                    _characterTarget = Selection.activeGameObject;
                }
            }
        }

        private void OnGUI()
        {
            // 🌟 严格遵守 style.md 设计系统
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 20,
                alignment = TextAnchor.MiddleCenter
            };
            titleStyle.normal.textColor = new Color(0.24f, 0.72f, 1.00f); // Primary Azure Accent (#3DB8FF)

            GUIStyle subtitleStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleCenter
            };
            subtitleStyle.normal.textColor = Color.gray;

            GUIStyle sectionHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                margin = new RectOffset(0, 0, 8, 4)
            };

            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                fixedHeight = 40
            };

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            // 1. Header Banner 标题与副标题
            GUILayout.Space(15);
            GUILayout.Label("CHARACTER CAMERA ADAPTOR", titleStyle);
            GUILayout.Label("ONE-CLICK ELDEN RING CAMERA PREFAB GENERATOR", subtitleStyle);
            GUILayout.Space(10);
            DrawLine();

            // 2. Audit Status Cards (状态诊断卡片，纯色色块)
            GUILayout.Label(" SETUP AUDIT STATUS", sectionHeaderStyle);
            GUILayout.Space(5);

            bool hasCharacter = _characterTarget != null;
            bool hasPivot = hasCharacter && HasCameraTargetSocket(_characterTarget.transform);
            
            CameraManager sceneCamManager = Object.FindAnyObjectByType<CameraManager>();
            bool hasCameraManager = sceneCamManager != null;
            
            CinemachineVirtualCamera activeVirtualCam = Object.FindAnyObjectByType<CinemachineVirtualCamera>();
            bool hasVirtualCamera = activeVirtualCam != null;

            DrawAuditBox(hasCharacter, "Target Character Selection", 
                hasCharacter ? $"✔ Target character object identified: '{_characterTarget.name}'" : "✘ Please assign a Target Character GameObject (Scene Instance or Prefab).");

            DrawAuditBox(true, "Camera Prefab Output", 
                $"✔ Will automatically generate a perfect new prefab at:\n    '{_prefabSavePath}'");

            DrawAuditBox(hasPivot, "Camera Target Pivot Status", 
                hasPivot ? "✔ CharacterSocket(CameraTarget) exists inside character transform hierarchy." : "⚠ No CameraTarget socket found. It will be generated at local offset (0, 1.5, 0).");

            DrawAuditBox(hasCameraManager, "Global Camera Manager", 
                hasCameraManager ? $"✔ Active CameraManager found in current scene on '{sceneCamManager.gameObject.name}'." : "⚠ No CameraManager in scene. A perfect new one will be spawned from the generated prefab.");

            DrawAuditBox(hasVirtualCamera, "Cinemachine Virtual Camera", 
                hasVirtualCamera ? $"✔ Virtual camera '{activeVirtualCam.name}' detected in current scene." : "⚠ No Cinemachine Virtual Camera in scene. A new instance will be spawned from the generated prefab.");

            GUILayout.Space(10);
            DrawLine();

            // 3. Configuration Parameters (参数配置)
            GUILayout.Label(" CONFIGURATION PARAMETERS", sectionHeaderStyle);
            GUILayout.Space(5);

            _characterTarget = (GameObject)EditorGUILayout.ObjectField(new GUIContent("Target Character", "The Character GameObject (Scene Instance or Project Prefab) to attach camera pivot."), _characterTarget, typeof(GameObject), true);
            _prefabSavePath = EditorGUILayout.TextField(new GUIContent("Prefab Save Path", "The path where the generated perfect camera prefab will be saved."), _prefabSavePath);
            _characterLogicPrefabSavePath = EditorGUILayout.TextField(new GUIContent("Character Logic Prefab", "The path where the generated character logic prefab will be saved."), _characterLogicPrefabSavePath);
            _uiCameraPrefabSavePath = EditorGUILayout.TextField(new GUIContent("UI Camera Prefab", "The path where the generated UI camera prefab will be saved."), _uiCameraPrefabSavePath);
            _uiRootPrefabSavePath = EditorGUILayout.TextField(new GUIContent("UIRoot Prefab", "The path where the generated UIRoot prefab will be saved."), _uiRootPrefabSavePath);

            GUILayout.Space(5);
            _cameraComponentType = (CameraComponentType)EditorGUILayout.EnumPopup(new GUIContent("Camera Body Type", "The Cinemachine body component type to configure inside the virtual camera."), _cameraComponentType);

            GUILayout.Space(5);
            _pivotOffset = EditorGUILayout.Vector3Field("Pivot Local Offset", _pivotOffset);
            _cameraDistance = EditorGUILayout.Slider("Camera Zoom Distance", _cameraDistance, 2.0f, 20.0f);

            if (_cameraComponentType == CameraComponentType.ThirdPersonFollow)
            {
                _shoulderOffset = EditorGUILayout.Vector3Field("3rd Person Shoulder Offset", _shoulderOffset);
            }

            GUILayout.Space(15);
            DrawLine();

            // 4. One-Click Execution (大号触感按钮)
            bool ready = hasCharacter && !string.IsNullOrEmpty(_prefabSavePath);
            GUI.enabled = ready;

            if (!ready)
            {
                var warningBoxStyle = new GUIStyle(GUI.skin.box)
                {
                    margin = new RectOffset(5, 5, 5, 5),
                    padding = new RectOffset(10, 10, 8, 8)
                };
                GUI.backgroundColor = new Color(0.53f, 0.40f, 0.10f); // Amber Warning Color
                GUILayout.BeginVertical(warningBoxStyle);
                GUI.backgroundColor = Color.white;
                var warnStyle = new GUIStyle(EditorStyles.boldLabel);
                warnStyle.normal.textColor = new Color(1f, 0.9f, 0.6f);
                GUILayout.Label("⚠ REQUIREMENT MISSING", warnStyle);
                GUILayout.Label("Ensure a Target Character is assigned and Prefab Save Path is specified above.", EditorStyles.wordWrappedMiniLabel);
                GUILayout.EndVertical();
                GUILayout.Space(10);
            }

            // 使用 Success Emerald Green (#40B84F / new Color(0.25f, 0.72f, 0.31f)) 作为按钮颜色
            GUI.backgroundColor = ready ? new Color(0.25f, 0.72f, 0.31f) : Color.gray;
            if (GUILayout.Button(" GENERATE PERFECT PREFAB & SETUP SCENE ", buttonStyle))
            {
                ExecuteGenerationAndSetup();
            }
            GUI.backgroundColor = Color.white; // 重置
            GUI.enabled = true;

            GUILayout.Space(10);
            DrawLine();
            GUILayout.Label(" SUPPORT PREFAB GENERATORS", sectionHeaderStyle);
            GUILayout.Space(5);

            GUI.backgroundColor = new Color(0.24f, 0.72f, 1.00f);
            if (GUILayout.Button(" GENERATE CHARACTER LOGIC PREFAB ", buttonStyle))
            {
                GenerateCharacterLogicPrefab(_characterLogicPrefabSavePath);
            }

            if (GUILayout.Button(" GENERATE UI CAMERA PREFAB ", buttonStyle))
            {
                GenerateUICameraPrefab(_uiCameraPrefabSavePath, true);
            }

            if (GUILayout.Button(" GENERATE UI ROOT PREFAB ", buttonStyle))
            {
                GenerateUIRootPrefabViaUISystem(_uiRootPrefabSavePath);
            }

            GUI.backgroundColor = Color.white;
            GUILayout.Space(20);
            EditorGUILayout.EndScrollView();
        }

        private void ExecuteGenerationAndSetup()
        {
            if (_characterTarget == null || string.IsNullOrEmpty(_prefabSavePath)) return;

            // Step 1: 资产数据库路径目录处理
            string directory = Path.GetDirectoryName(_prefabSavePath);
            if (!Directory.Exists(directory) && !string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Step 2: 开启 Undo 组
            Undo.SetCurrentGroupName("Generate Perfect Camera & Adapt Scene");
            int undoGroup = Undo.GetCurrentGroup();

            // 2.1 针对场景中的角色实例创建/更新 Pivot（如果角色是场景节点）
            string charPrefabPath = AssetDatabase.GetAssetPath(_characterTarget);
            bool isCharPrefab = !string.IsNullOrEmpty(charPrefabPath);

            Transform pivotInScene = null;
            if (!isCharPrefab)
            {
                pivotInScene = EnsureCameraTargetSocket(_characterTarget.transform, _pivotOffset, true);
            }
            else
            {
                // 如果是角色预制体资产，则直接注入 Pivot 到角色预制体并保存
                GameObject charPrefabRoot = PrefabUtility.LoadPrefabContents(charPrefabPath);
                try
                {
                    EnsureCameraTargetSocket(charPrefabRoot.transform, _pivotOffset, false);
                    PrefabUtility.SaveAsPrefabAsset(charPrefabRoot, charPrefabPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(charPrefabRoot);
                }
            }

            // Step 3: 创建并生成全新的完美的 CameraView 预制体！
            // 3.1 创建一个临时 GameObject 层次结构
            GameObject tempRoot = new GameObject("CameraView");
            
            // 3.2 创建 Main Camera 子节点
            GameObject tempCamGo = new GameObject("Main Camera");
            tempCamGo.transform.SetParent(tempRoot.transform);
            tempCamGo.transform.localPosition = Vector3.zero;
            tempCamGo.transform.localRotation = Quaternion.identity;

            Camera camComponent = tempCamGo.AddComponent<Camera>();
            camComponent.tag = "MainCamera";
            camComponent.clearFlags = CameraClearFlags.Skybox;
            
            tempCamGo.AddComponent<AudioListener>();
            tempCamGo.AddComponent<CinemachineBrain>();
            tempCamGo.AddComponent<CameraManager>();
            tempCamGo.AddComponent<URPCameraStackLinker>();

            // 3.3 创建 cm (Virtual Camera) 子节点
            GameObject tempVCamGo = new GameObject("cm");
            tempVCamGo.transform.SetParent(tempCamGo.transform);
            tempVCamGo.transform.localPosition = Vector3.zero;
            tempVCamGo.transform.localRotation = Quaternion.identity;

            CinemachineVirtualCamera vCam = tempVCamGo.AddComponent<CinemachineVirtualCamera>();

            // 根据用户选项，配置完美的 Cinemachine 身体组件参数
            if (_cameraComponentType == CameraComponentType.ThirdPersonFollow)
            {
                var follow = vCam.AddCinemachineComponent<Cinemachine3rdPersonFollow>();
                follow.CameraDistance = _cameraDistance;
                follow.ShoulderOffset = _shoulderOffset;
                follow.Damping = new Vector3(0.1f, 0.1f, 0.1f);
            }
            else if (_cameraComponentType == CameraComponentType.Transposer)
            {
                var transposer = vCam.AddCinemachineComponent<CinemachineTransposer>();
                transposer.m_BindingMode = CinemachineTransposer.BindingMode.WorldSpace;
                transposer.m_FollowOffset = new Vector3(0f, 2f, -_cameraDistance);
                transposer.m_XDamping = 0.5f;
                transposer.m_YDamping = 0.5f;
                transposer.m_ZDamping = 0.5f;
            }
            else if (_cameraComponentType == CameraComponentType.FramingTransposer)
            {
                var framing = vCam.AddCinemachineComponent<CinemachineFramingTransposer>();
                framing.m_CameraDistance = _cameraDistance;
                framing.m_XDamping = 0.5f;
                framing.m_YDamping = 0.5f;
                framing.m_ZDamping = 0.5f;
            }

            // 添加 CinemachineCollider 确保完美防穿地/墙体碰撞（艾尔登法环灵魂级体验）
            tempVCamGo.AddComponent<CinemachineCollider>();

            // 添加 CinemachineTargetLinker (采用事件自动绑定注册中心的解耦机制)
            tempVCamGo.AddComponent<CinemachineTargetLinker>();

            // 3.4 将临时节点结构保存为完美的预制体资产，然后销毁临时结构
            GameObject perfectPrefab = PrefabUtility.SaveAsPrefabAssetAndConnect(tempRoot, _prefabSavePath, InteractionMode.AutomatedAction);
            DestroyImmediate(tempRoot);
            Debug.Log($"<color=#40B84F><b>[Adaptor]</b></color> Generated brand new perfect Camera Prefab asset at: '{_prefabSavePath}'.");

            // Step 4: 将这个全新完美的预制体例化注入到当前场景中 (如果有旧的实例则删除/替换)
            // 4.1 寻找场景中现有的 CameraView (或挂有 CameraManager 且来自旧预制体的实例)
            CameraManager existingManager = Object.FindAnyObjectByType<CameraManager>();
            if (existingManager != null)
            {
                // 如果是场景实例，则安全将其标记并销毁
                GameObject parentGo = existingManager.gameObject;
                while (parentGo.transform.parent != null)
                {
                    parentGo = parentGo.transform.parent.gameObject;
                }
                Undo.DestroyObjectImmediate(parentGo);
                Debug.Log($"<color=#FF5252><b>[Adaptor]</b></color> Cleaned up existing obsolete CameraView instance from scene.");
            }

            // 4.2 将完美的新预制体例化到场景中并注册 Undo
            GameObject sceneCamInstance = (GameObject)PrefabUtility.InstantiatePrefab(perfectPrefab);
            Undo.RegisterCreatedObjectUndo(sceneCamInstance, "Instantiate Perfect CameraView");

            // 4.3 连接场景实例引用
            CinemachineVirtualCamera sceneVCam = sceneCamInstance.GetComponentInChildren<CinemachineVirtualCamera>(true);
            CameraManager sceneCamManager = sceneCamInstance.GetComponentInChildren<CameraManager>(true);

            if (sceneVCam != null && pivotInScene != null)
            {
                Undo.RecordObject(sceneVCam, "Link Virtual Camera Targets");
                sceneVCam.Follow = pivotInScene;
                sceneVCam.LookAt = pivotInScene;
            }

            if (sceneCamManager != null && !isCharPrefab)
            {
                Undo.RecordObject(sceneCamManager, "Link Target to CameraManager");
                sceneCamManager.SetAllTargets(_characterTarget.transform);
            }

            // Step 5: 冲突消解：查找并屏蔽场景内原有的多余 “Main Camera” 渲染实体以防双重渲染冲突
            Camera[] allSceneCameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            Camera prefabInstantiatedCamera = sceneCamInstance.GetComponentInChildren<Camera>(true);

            int disabledCount = 0;
            foreach (Camera cam in allSceneCameras)
            {
                if (cam != null && cam.gameObject.CompareTag("MainCamera") && cam != prefabInstantiatedCamera)
                {
                    Undo.RecordObject(cam, "Disable Duplicate Main Camera component");
                    cam.enabled = false;

                    AudioListener audioListener = cam.GetComponent<AudioListener>();
                    if (audioListener != null)
                    {
                        Undo.RecordObject(audioListener, "Disable Duplicate Audio Listener component");
                        audioListener.enabled = false;
                    }

                    Undo.RecordObject(cam.gameObject, "Rename Duplicate Camera");
                    cam.gameObject.name = cam.gameObject.name + " (Disabled Backup)";
                    
                    disabledCount++;
                    Debug.Log($"<color=#FFCC00><b>[Adaptor]</b></color> Conflict resolved: Disabled redundant Main Camera component on '{cam.gameObject.name}'.");
                }
            }

            // 合并所有场景操作进单次 Undo 栈
            Undo.CollapseUndoOperations(undoGroup);

            // 6. 弹窗提示
            string summaryMsg = $"Successfully generated and adapted perfect camera to '{_characterTarget.name}'!\n\n" +
                $"1. Prefab Generated: Saved a clean, complete, and optimized CameraView Prefab asset to '{_prefabSavePath}'.\n" +
                $"2. Custom Setup: Pre-configured '{_cameraComponentType}' with distance = {_cameraDistance} and added a wall-collider script!\n" +
                $"3. Scene Synced: Instantiated the brand new perfect camera prefab and linked target pivot.\n";

            if (disabledCount > 0)
            {
                summaryMsg += $"4. Conflicts Resolved: Found and disabled {disabledCount} duplicate Main Camera(s) in the scene to avoid dual-rendering errors.\n";
            }

            summaryMsg += "\nYou can undo all scene modifications with standard Ctrl+Z.";

            EditorUtility.DisplayDialog("Camera Generation Successful", summaryMsg, "Perfect");
        }

        [MenuItem("Tools/Camera System/Generate Character Logic Prefab", false, 21)]
        public static void GenerateDefaultCharacterLogicPrefab()
        {
            GenerateCharacterLogicPrefab("Assets/Demo/Art/Prefabs/Character_logic.prefab");
        }

        [MenuItem("Tools/Camera System/Generate UI Camera Prefab", false, 22)]
        public static void GenerateDefaultUICameraPrefab()
        {
            GenerateUICameraPrefab("Assets/Demo/Art/Prefabs/UICamera.prefab", true);
        }

        private static GameObject GenerateCharacterLogicPrefab(string prefabPath)
        {
            if (string.IsNullOrWhiteSpace(prefabPath))
            {
                EditorUtility.DisplayDialog("Character Logic Generator", "Prefab path is empty.", "OK");
                return null;
            }

            EnsureAssetFolder(prefabPath);

            GameObject root = new GameObject(Path.GetFileNameWithoutExtension(prefabPath));
            try
            {
                root.AddComponent<UnityEngine.CharacterController>();
                root.AddComponent<CharacterMotor>();
                var registry = root.AddComponent<CharacterSocketRegistry>();
                TryAddComponentByTypeName(root, "ModularDemo.Runtime.PlayerCharacterBrain, ModularDemo.Runtime");
                TryAddComponentByTypeName(root, "ModularDemo.Runtime.PlayerStateBridge, ModularDemo.Runtime");
                Component interactor = TryAddComponentByTypeName(
                    root,
                    "InteractionSystem.Runtime.ProximityInteractor, InteractionSystem.Runtime"
                );
                ConfigureGeneratedInteractor(interactor);

                CreateSocket(root.transform, CharacterSocketId.VisualRoot, "VisualRoot", new Vector3(0f, 0f, 0f));
                CreateSocket(root.transform, CharacterSocketId.CameraTarget, "CameraTarget", new Vector3(0f, 1.5f, 0f));
                CreateSocket(root.transform, CharacterSocketId.Body, "Body", new Vector3(0f, 1.0f, 0f));
                CreateSocket(root.transform, CharacterSocketId.Head, "Head", new Vector3(0f, 1.65f, 0f));
                CreateSocket(root.transform, CharacterSocketId.Aura, "Aura", new Vector3(0f, 0.05f, 0f));
                CreateSocket(root.transform, CharacterSocketId.Footprint, "Footprint", new Vector3(0f, 0.05f, 0f));
                CreateSocket(root.transform, CharacterSocketId.LeftFoot, "LeftFoot", new Vector3(-0.18f, 0.05f, 0f));
                CreateSocket(root.transform, CharacterSocketId.RightFoot, "RightFoot", new Vector3(0.18f, 0.05f, 0f));
                CreateSocket(root.transform, CharacterSocketId.HitPoint, "HitPoint", new Vector3(0f, 1.0f, 0f));

                registry.Refresh();

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                SetSerializedStringIfAssetExists(
                    "Assets/Modules/Mainboard/Data/PlayerFeature.asset",
                    "playerPrefabKey",
                    prefabPath
                );
                AssetDatabase.SaveAssets();
                Selection.activeObject = prefab;
                Debug.Log($"<color=#40B84F><b>[Character Logic Generator]</b></color> Generated character logic prefab: {prefabPath}");
                return prefab;
            }
            finally
            {
                DestroyImmediate(root);
            }
        }

        private static void ConfigureGeneratedInteractor(Component interactor)
        {
            if (interactor == null)
                return;

            SerializedObject serializedObject = new SerializedObject(interactor);
            SetSerializedFloat(serializedObject, "radius", 1.5f);
            SetSerializedVector3(serializedObject, "detectionOffset", Vector3.zero);
            SetSerializedLayerMask(serializedObject, "targetLayers", ~0);
            SetSerializedFloat(serializedObject, "scanFrequency", 0.2f);
            SetSerializedBool(serializedObject, "_autoTrigger", true);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetSerializedFloat(SerializedObject serializedObject, string propertyName, float value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null && property.propertyType == SerializedPropertyType.Float)
                property.floatValue = value;
        }

        private static void SetSerializedBool(SerializedObject serializedObject, string propertyName, bool value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null && property.propertyType == SerializedPropertyType.Boolean)
                property.boolValue = value;
        }

        private static void SetSerializedVector3(SerializedObject serializedObject, string propertyName, Vector3 value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null && property.propertyType == SerializedPropertyType.Vector3)
                property.vector3Value = value;
        }

        private static void SetSerializedLayerMask(SerializedObject serializedObject, string propertyName, int value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null && property.propertyType == SerializedPropertyType.LayerMask)
                property.intValue = value;
        }

        private static GameObject GenerateUICameraPrefab(string prefabPath, bool showDialog)
        {
            if (string.IsNullOrWhiteSpace(prefabPath))
            {
                if (showDialog)
                    EditorUtility.DisplayDialog("UI Camera Generator", "Prefab path is empty.", "OK");
                return null;
            }

            EnsureAssetFolder(prefabPath);

            GameObject root = new GameObject(Path.GetFileNameWithoutExtension(prefabPath));
            try
            {
                Camera uiCamera = root.AddComponent<Camera>();
                uiCamera.clearFlags = CameraClearFlags.Depth;
                uiCamera.cullingMask = GetUILayerMask();
                uiCamera.orthographic = true;
                uiCamera.orthographicSize = 5f;
                uiCamera.nearClipPlane = -100f;
                uiCamera.farClipPlane = 100f;
                uiCamera.depth = 10f;
                uiCamera.allowHDR = false;
                uiCamera.allowMSAA = false;

                root.AddComponent<UICameraRegisterHook>();

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                SetSerializedStringIfAssetExists(
                    "Assets/Modules/Mainboard/Data/CameraRigFeature.asset",
                    "uiCameraKey",
                    prefabPath
                );
                AssetDatabase.SaveAssets();
                Selection.activeObject = prefab;
                Debug.Log($"<color=#40B84F><b>[UI Camera Generator]</b></color> Generated UI camera prefab: {prefabPath}");

                if (showDialog)
                    EditorUtility.DisplayDialog("UI Camera Generated", $"Generated UI camera prefab:\n{prefabPath}", "OK");

                return prefab;
            }
            finally
            {
                DestroyImmediate(root);
            }
        }

        private static Object GenerateUIRootPrefabViaUISystem(string prefabPath)
        {
            System.Type generatorType = System.Type.GetType(
                "UISystem.Editor.UIRootGenerator, Assembly-CSharp-Editor"
            );

            if (generatorType == null)
            {
                EditorUtility.DisplayDialog(
                    "UIRoot Generator",
                    "Could not find UISystem.Editor.UIRootGenerator.",
                    "OK"
                );
                return null;
            }

            var method = generatorType.GetMethod(
                "GenerateUIRootPrefab",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static
            );

            if (method == null)
            {
                EditorUtility.DisplayDialog(
                    "UIRoot Generator",
                    "UIRootGenerator.GenerateUIRootPrefab(string) was not found.",
                    "OK"
                );
                return null;
            }

            return method.Invoke(null, new object[] { prefabPath }) as Object;
        }

        private void DrawLine()
        {
            Rect rect = EditorGUILayout.GetControlRect(false, 1);
            rect.height = 1;
            EditorGUI.DrawRect(rect, new Color(0.3f, 0.3f, 0.3f, 0.5f));
            GUILayout.Space(5);
        }

        private void DrawAuditBox(bool passed, string title, string message)
        {
            // Pass: Deep Forest Green (#2E6338) | Light Mint (#99FFAA)
            // Fail: Wine Red (#873535) | Pastel Red (#FFAAAA)
            Color bgColor = passed ? new Color(0.18f, 0.39f, 0.22f) : new Color(0.53f, 0.21f, 0.21f);
            Color textColor = passed ? new Color(0.60f, 1.00f, 0.67f) : new Color(1.00f, 0.67f, 0.67f);

            var boxStyle = new GUIStyle(GUI.skin.box)
            {
                margin = new RectOffset(5, 5, 5, 5),
                padding = new RectOffset(10, 10, 8, 8)
            };

            GUI.backgroundColor = bgColor;
            GUILayout.BeginVertical(boxStyle);
            GUI.backgroundColor = Color.white; // 必须重置

            var headerStyle = new GUIStyle(EditorStyles.boldLabel);
            headerStyle.normal.textColor = textColor;
            GUILayout.Label($"{(passed ? "✔" : "✘")}  {title}", headerStyle);

            var descStyle = new GUIStyle(EditorStyles.wordWrappedLabel) { fontSize = 11 };
            descStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f);
            GUILayout.Label(message, descStyle);

            GUILayout.EndVertical();
        }

        private static bool HasCameraTargetSocket(Transform characterRoot)
        {
            if (characterRoot == null)
                return false;

            var registry = characterRoot.GetComponentInChildren<CharacterSocketRegistry>(true);
            return registry != null && registry.TryGet(CharacterSocketId.CameraTarget, out var socket) && socket != null;
        }

        private static Transform EnsureCameraTargetSocket(Transform characterRoot, Vector3 localOffset, bool useUndo)
        {
            var registry = characterRoot.GetComponentInChildren<CharacterSocketRegistry>(true);
            if (registry == null)
            {
                registry = useUndo
                    ? Undo.AddComponent<CharacterSocketRegistry>(characterRoot.gameObject)
                    : characterRoot.gameObject.AddComponent<CharacterSocketRegistry>();
            }

            registry.Refresh();
            if (registry.TryGet(CharacterSocketId.CameraTarget, out var socket) && socket != null)
            {
                if (useUndo)
                    Undo.RecordObject(socket, "Update CameraTarget Socket Position");

                socket.localPosition = localOffset;
                return socket;
            }

            var pivotGo = new GameObject("CameraTarget");
            if (useUndo)
            {
                Undo.RegisterCreatedObjectUndo(pivotGo, "Create CameraTarget Socket");
                Undo.SetTransformParent(pivotGo.transform, characterRoot, "Parent CameraTarget Socket to Character");
            }
            else
            {
                pivotGo.transform.SetParent(characterRoot, false);
            }

            var pivot = pivotGo.transform;

            if (useUndo)
                Undo.RecordObject(pivot, "Configure CameraTarget Socket");

            pivot.localPosition = localOffset;
            pivot.localRotation = Quaternion.identity;
            pivot.localScale = Vector3.one;

            var socketComponent = pivot.GetComponent<CharacterSocket>();
            if (socketComponent == null)
            {
                socketComponent = useUndo
                    ? Undo.AddComponent<CharacterSocket>(pivot.gameObject)
                    : pivot.gameObject.AddComponent<CharacterSocket>();
            }

            socketComponent.Configure(CharacterSocketId.CameraTarget);
            registry.Refresh();
            return pivot;
        }

        private static Transform CreateSocket(
            Transform parent,
            CharacterSocketId id,
            string name,
            Vector3 localPosition
        )
        {
            GameObject socketGo = new GameObject(name);
            socketGo.transform.SetParent(parent, false);
            socketGo.transform.localPosition = localPosition;
            socketGo.transform.localRotation = Quaternion.identity;
            socketGo.transform.localScale = Vector3.one;

            CharacterSocket socket = socketGo.AddComponent<CharacterSocket>();
            socket.Configure(id);
            return socketGo.transform;
        }

        private static Component TryAddComponentByTypeName(GameObject target, string qualifiedTypeName)
        {
            if (target == null || string.IsNullOrWhiteSpace(qualifiedTypeName))
                return null;

            System.Type type = System.Type.GetType(qualifiedTypeName);
            if (type == null || !typeof(Component).IsAssignableFrom(type))
                return null;

            return target.GetComponent(type) ?? target.AddComponent(type);
        }

        private static void EnsureAssetFolder(string assetPath)
        {
            string directory = Path.GetDirectoryName(assetPath)?.Replace("\\", "/");
            if (string.IsNullOrWhiteSpace(directory) || AssetDatabase.IsValidFolder(directory))
                return;

            string[] parts = directory.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);

                current = next;
            }
        }

        private static int GetUILayerMask()
        {
            int uiLayer = LayerMask.NameToLayer("UI");
            return uiLayer >= 0 ? 1 << uiLayer : 0;
        }

        private static void SetSerializedStringIfAssetExists(
            string assetPath,
            string propertyName,
            string value
        )
        {
            Object asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
            if (asset == null)
                return;

            SerializedObject serializedObject = new SerializedObject(asset);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null || property.propertyType != SerializedPropertyType.String)
                return;

            property.stringValue = value;
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);
        }
    }
}
