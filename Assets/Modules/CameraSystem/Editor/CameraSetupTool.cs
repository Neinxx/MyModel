using UnityEngine;
using UnityEditor;
using Cinemachine;
using CameraSystem.Runtime;

namespace CameraSystem.Editor
{
    /// <summary>
    /// Generates a reusable CameraView prefab and wires it to a character target.
    /// </summary>
    public class CameraSetupTool : EditorWindow
    {
        [MenuItem("Tools/Camera System/Character Camera Adapter")]
        public static void ShowWindow()
        {
            CameraSetupTool window = GetWindow<CameraSetupTool>("Camera Adapter");
            window.minSize = new Vector2(500, 680);
            window.maxSize = new Vector2(800, 980);
            window.Show();
        }

        private GameObject _characterTarget;
        private string _prefabSavePath = "Assets/Modules/CameraSystem/Prefabs/CameraView.prefab";
        private string _uiCameraPrefabSavePath = "Assets/Modules/CameraSystem/Prefabs/UICamera.prefab";
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
            // Prefer the selected scene object as the default target.
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
            GUILayout.Label("CHARACTER CAMERA ADAPTER", titleStyle);
            GUILayout.Label("CAMERA PREFAB GENERATOR", subtitleStyle);
            GUILayout.Space(10);
            DrawLine();

            // 2. Audit Status Cards (状态诊断卡片，纯色色块)
            GUILayout.Label(" SETUP AUDIT STATUS", sectionHeaderStyle);
            GUILayout.Space(5);

            bool hasCharacter = _characterTarget != null;
            bool hasPivot = hasCharacter && CameraRigPrefabGenerator.HasCameraTargetSocket(_characterTarget.transform);
            
            CameraManager sceneCamManager = Object.FindAnyObjectByType<CameraManager>();
            bool hasCameraManager = sceneCamManager != null;
            
            CinemachineVirtualCamera activeVirtualCam = Object.FindAnyObjectByType<CinemachineVirtualCamera>();
            bool hasVirtualCamera = activeVirtualCam != null;

            DrawAuditBox(hasCharacter, "Target Character Selection", 
                hasCharacter ? $"✔ Target character object identified: '{_characterTarget.name}'" : "✘ Please assign a Target Character GameObject (Scene Instance or Prefab).");

            DrawAuditBox(true, "Camera Prefab Output", 
                $"✔ Will generate a CameraView prefab at:\n    '{_prefabSavePath}'");

            DrawAuditBox(hasPivot, "Camera Target Pivot Status", 
                hasPivot ? "✔ CharacterSocket(CameraTarget) exists inside character transform hierarchy." : "⚠ No CameraTarget socket found. It will be generated at local offset (0, 1.5, 0).");

            DrawAuditBox(hasCameraManager, "Global Camera Manager", 
                hasCameraManager ? $"✔ Active CameraManager found in current scene on '{sceneCamManager.gameObject.name}'." : "⚠ No CameraManager in scene. One will be spawned from the generated prefab.");

            DrawAuditBox(hasVirtualCamera, "Cinemachine Virtual Camera", 
                hasVirtualCamera ? $"✔ Virtual camera '{activeVirtualCam.name}' detected in current scene." : "⚠ No Cinemachine Virtual Camera in scene. A new instance will be spawned from the generated prefab.");

            GUILayout.Space(10);
            DrawLine();

            // 3. Configuration Parameters (参数配置)
            GUILayout.Label(" CONFIGURATION PARAMETERS", sectionHeaderStyle);
            GUILayout.Space(5);

            _characterTarget = (GameObject)EditorGUILayout.ObjectField(new GUIContent("Target Character", "The Character GameObject (Scene Instance or Project Prefab) to attach camera pivot."), _characterTarget, typeof(GameObject), true);
            _prefabSavePath = EditorGUILayout.TextField(new GUIContent("Prefab Save Path", "The path where the generated CameraView prefab will be saved."), _prefabSavePath);
            _uiCameraPrefabSavePath = EditorGUILayout.TextField(new GUIContent("UI Camera Prefab", "The path where the generated UI camera prefab will be saved."), _uiCameraPrefabSavePath);

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

            GUI.backgroundColor = ready ? new Color(0.25f, 0.72f, 0.31f) : Color.gray;
            if (GUILayout.Button(" GENERATE PREFAB & SETUP SCENE ", buttonStyle))
            {
                ExecuteGenerationAndSetup();
            }
            GUI.backgroundColor = Color.white;
            GUI.enabled = true;

            GUILayout.Space(10);
            DrawLine();
            GUILayout.Label(" SUPPORT PREFAB GENERATORS", sectionHeaderStyle);
            GUILayout.Space(5);

            GUI.backgroundColor = new Color(0.24f, 0.72f, 1.00f);
            if (GUILayout.Button(" GENERATE UI CAMERA PREFAB ", buttonStyle))
            {
                GenerateUICameraPrefab(_uiCameraPrefabSavePath, true);
            }

            GUI.backgroundColor = Color.white;
            GUILayout.Space(20);
            EditorGUILayout.EndScrollView();
        }

        private void ExecuteGenerationAndSetup()
        {
            if (_characterTarget == null || string.IsNullOrEmpty(_prefabSavePath)) return;

            Undo.SetCurrentGroupName("Generate Camera Rig And Adapt Scene");
            int undoGroup = Undo.GetCurrentGroup();

            // Create or update the camera target socket on scene objects or prefabs.
            string charPrefabPath = AssetDatabase.GetAssetPath(_characterTarget);
            bool isCharPrefab = !string.IsNullOrEmpty(charPrefabPath);

            Transform pivotInScene = null;
            if (!isCharPrefab)
            {
                pivotInScene = CameraRigPrefabGenerator.EnsureCameraTargetSocket(_characterTarget.transform, _pivotOffset, true);
            }
            else
            {
                GameObject charPrefabRoot = PrefabUtility.LoadPrefabContents(charPrefabPath);
                try
                {
                    CameraRigPrefabGenerator.EnsureCameraTargetSocket(charPrefabRoot.transform, _pivotOffset, false);
                    PrefabUtility.SaveAsPrefabAsset(charPrefabRoot, charPrefabPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(charPrefabRoot);
                }
            }

            GameObject cameraPrefab = CameraRigPrefabGenerator.GenerateCameraRigPrefab(
                _prefabSavePath,
                ToRigBodyType(_cameraComponentType),
                _cameraDistance,
                _shoulderOffset);

            if (cameraPrefab == null)
            {
                EditorUtility.DisplayDialog("Camera Generation Failed", "Camera prefab path is empty or invalid.", "OK");
                return;
            }

            Debug.Log($"<color=#40B84F><b>[CameraSetup]</b></color> Generated CameraView prefab at: '{_prefabSavePath}'.");

            // Replace the existing camera rig instance in the current scene.
            CameraManager existingManager = Object.FindAnyObjectByType<CameraManager>();
            if (existingManager != null)
            {
                GameObject parentGo = existingManager.gameObject;
                while (parentGo.transform.parent != null)
                {
                    parentGo = parentGo.transform.parent.gameObject;
                }
                Undo.DestroyObjectImmediate(parentGo);
                Debug.Log($"<color=#FF5252><b>[CameraSetup]</b></color> Removed existing CameraView instance from scene.");
            }

            GameObject sceneCamInstance = (GameObject)PrefabUtility.InstantiatePrefab(cameraPrefab);
            Undo.RegisterCreatedObjectUndo(sceneCamInstance, "Instantiate CameraView");

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

            // Disable duplicate Main Camera renderers to avoid double rendering and audio listener conflicts.
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
                    Debug.Log($"<color=#FFCC00><b>[CameraSetup]</b></color> Disabled redundant Main Camera component on '{cam.gameObject.name}'.");
                }
            }

            Undo.CollapseUndoOperations(undoGroup);

            string summaryMsg = $"Generated and linked camera rig for '{_characterTarget.name}'.\n\n" +
                $"1. Prefab: Saved CameraView prefab to '{_prefabSavePath}'.\n" +
                $"2. Camera Body: Configured '{_cameraComponentType}' with distance = {_cameraDistance}.\n" +
                $"3. Scene: Instantiated the prefab and linked the target pivot.\n";

            if (disabledCount > 0)
            {
                summaryMsg += $"4. Conflicts: Disabled {disabledCount} duplicate Main Camera(s).\n";
            }

            summaryMsg += "\nYou can undo all scene modifications with standard Ctrl+Z.";

            EditorUtility.DisplayDialog("Camera Generation Successful", summaryMsg, "OK");
        }

        [MenuItem("Tools/Camera System/Generate UI Camera Prefab", false, 22)]
        public static void GenerateDefaultUICameraPrefab()
        {
            GenerateUICameraPrefab("Assets/Modules/CameraSystem/Prefabs/UICamera.prefab", true);
        }

        private static GameObject GenerateUICameraPrefab(string prefabPath, bool showDialog)
        {
            if (string.IsNullOrWhiteSpace(prefabPath))
            {
                if (showDialog)
                    EditorUtility.DisplayDialog("UI Camera Generator", "Prefab path is empty.", "OK");
                return null;
            }

            GameObject prefab = CameraRigPrefabGenerator.GenerateUICameraPrefab(prefabPath);
            AssetDatabase.SaveAssets();
            Selection.activeObject = prefab;
            Debug.Log($"<color=#40B84F><b>[UI Camera Generator]</b></color> Generated UI camera prefab: {prefabPath}");

            if (showDialog)
                EditorUtility.DisplayDialog("UI Camera Generated", $"Generated UI camera prefab:\n{prefabPath}", "OK");

            return prefab;
        }

        private static CameraRigBodyType ToRigBodyType(CameraComponentType componentType)
        {
            return componentType switch
            {
                CameraComponentType.Transposer => CameraRigBodyType.Transposer,
                CameraComponentType.FramingTransposer => CameraRigBodyType.FramingTransposer,
                _ => CameraRigBodyType.ThirdPersonFollow
            };
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
            Color bgColor = passed ? new Color(0.18f, 0.39f, 0.22f) : new Color(0.53f, 0.21f, 0.21f);
            Color textColor = passed ? new Color(0.60f, 1.00f, 0.67f) : new Color(1.00f, 0.67f, 0.67f);

            var boxStyle = new GUIStyle(GUI.skin.box)
            {
                margin = new RectOffset(5, 5, 5, 5),
                padding = new RectOffset(10, 10, 8, 8)
            };

            GUI.backgroundColor = bgColor;
            GUILayout.BeginVertical(boxStyle);
            GUI.backgroundColor = Color.white;

            var headerStyle = new GUIStyle(EditorStyles.boldLabel);
            headerStyle.normal.textColor = textColor;
            GUILayout.Label($"{(passed ? "✔" : "✘")}  {title}", headerStyle);

            var descStyle = new GUIStyle(EditorStyles.wordWrappedLabel) { fontSize = 11 };
            descStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f);
            GUILayout.Label(message, descStyle);

            GUILayout.EndVertical();
        }

    }
}
