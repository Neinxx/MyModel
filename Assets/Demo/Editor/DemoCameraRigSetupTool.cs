using CameraSystem.Editor;
using CharacterController.Runtime;
using InteractionSystem.Runtime;
using ModularDemo.Runtime;
using UISystem.Editor;
using UnityEditor;
using UnityEngine;

namespace ModularDemo.Editor
{
    public static class DemoCameraRigSetupTool
    {
        private const string CharacterLogicPrefabPath = "Assets/Demo/Art/Prefabs/Character_logic.prefab";
        private const string UICameraPrefabPath = "Assets/Demo/Art/Prefabs/UICamera.prefab";
        private const string UIRootPrefabPath = "Assets/Demo/Art/Prefabs/UIRoot.prefab";

        [MenuItem("Tools/Demo/Camera Rig/Generate Character Logic Prefab", false, 20)]
        public static void GenerateCharacterLogicPrefab()
        {
            var prefab = GenerateCharacterLogicPrefab(CharacterLogicPrefabPath);
            if (prefab == null)
                return;

            Selection.activeObject = prefab;
            Debug.Log($"[DemoCameraRigSetup] Generated character logic prefab: {CharacterLogicPrefabPath}");
        }

        [MenuItem("Tools/Demo/Camera Rig/Generate UI Camera Prefab", false, 21)]
        public static void GenerateUICameraPrefab()
        {
            var prefab = CameraRigPrefabGenerator.GenerateUICameraPrefab(UICameraPrefabPath);
            AssetDatabase.SaveAssets();
            Selection.activeObject = prefab;
            Debug.Log($"[DemoCameraRigSetup] Generated UI camera prefab: {UICameraPrefabPath}");
        }

        [MenuItem("Tools/Demo/Camera Rig/Generate UI Root Prefab", false, 22)]
        public static void GenerateUIRootPrefab()
        {
            var prefab = UIRootGenerator.GenerateUIRootPrefab(UIRootPrefabPath);
            AssetDatabase.SaveAssets();
            Selection.activeObject = prefab;
            Debug.Log($"[DemoCameraRigSetup] Generated UI root prefab: {UIRootPrefabPath}");
        }

        private static GameObject GenerateCharacterLogicPrefab(string prefabPath)
        {
            if (string.IsNullOrWhiteSpace(prefabPath))
            {
                EditorUtility.DisplayDialog("Demo Camera Rig", "Prefab path is empty.", "OK");
                return null;
            }

            CameraRigPrefabGenerator.EnsureAssetFolder(prefabPath);

            var root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(prefabPath));
            try
            {
                root.AddComponent<UnityEngine.CharacterController>();
                root.AddComponent<CharacterMotor>();
                var registry = root.AddComponent<CharacterSocketRegistry>();
                root.AddComponent<PlayerCharacterBrain>();
                root.AddComponent<PlayerStateBridge>();
                ConfigureInteractor(root.AddComponent<ProximityInteractor>());

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

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                AssetDatabase.SaveAssets();
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void ConfigureInteractor(ProximityInteractor interactor)
        {
            if (interactor == null)
                return;

            var serializedObject = new SerializedObject(interactor);
            SetSerializedFloat(serializedObject, "_radius", 1.5f);
            SetSerializedVector3(serializedObject, "_detectionOffset", Vector3.zero);
            SetSerializedLayerMask(serializedObject, "_targetLayers", ~0);
            SetSerializedFloat(serializedObject, "_scanFrequency", 0.2f);
            SetSerializedBool(serializedObject, "_autoTrigger", true);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Transform CreateSocket(
            Transform parent,
            CharacterSocketId id,
            string name,
            Vector3 localPosition)
        {
            var socketGo = new GameObject(name);
            socketGo.transform.SetParent(parent, false);
            socketGo.transform.localPosition = localPosition;
            socketGo.transform.localRotation = Quaternion.identity;
            socketGo.transform.localScale = Vector3.one;

            var socket = socketGo.AddComponent<CharacterSocket>();
            socket.Configure(id);
            return socketGo.transform;
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
    }
}
