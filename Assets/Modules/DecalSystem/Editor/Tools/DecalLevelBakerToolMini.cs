using System.Collections.Generic;
using System.IO;
using DecalMini;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace DecalMini.Editor
{
    public class DecalLevelBakerToolMini : EditorWindow
    {
        private enum BakeAction
        {
            KeepObjects,
            DisableComponents,
            DisableGameObjects,
            DestroyObjects,
        }

        private VisualElement _root;
        private Label _uve_TotalLabel;
        private Label _uve_ActiveLabel;
        private Label _uve_InvalidLabel;
        private TextField _uve_PathField;
        private EnumField _uve_ActionField;
        private Toggle _uve_SilentToggle;
        private Button _uve_SelectInvalidBtn;

        private List<DecalProjectorMini> _foundProjectors = new();
        private const string PREFS_SAVE_PATH = "DecalMini_Baker_SavePath";

        [MenuItem("Tools/Decal System/Professional Baker")]
        public static void ShowWindow()
        {
            var window = GetWindow<DecalLevelBakerToolMini>("Static Baker Pro");
            window.minSize = new Vector2(350, 500);
        }

        public void CreateGUI()
        {
            _root = rootVisualElement;
            _root.style.flexDirection = FlexDirection.Column;

            var visualTree = DecalSystemPathUtility.LoadUXML("DecalBakerProfessional");
            if (visualTree == null) return;
            _root.Add(visualTree.Instantiate());

            var styleSheet = DecalSystemPathUtility.LoadUSS("DecalSystemStyle");
            if (styleSheet != null) _root.styleSheets.Add(styleSheet);

            BindElements();
            ScanScene();
        }

        private void BindElements()
        {
            _uve_TotalLabel = _root.Q<Label>("TotalCountLabel");
            _uve_ActiveLabel = _root.Q<Label>("ActiveCountLabel");
            _uve_InvalidLabel = _root.Q<Label>("IssueCountLabel");
            _uve_PathField = _root.Q<TextField>("PathField");
            _uve_ActionField = _root.Q<EnumField>("ActionField");
            if (_uve_ActionField != null) _uve_ActionField.Init(BakeAction.DisableGameObjects);

            if (_uve_PathField != null)
            {
                _uve_PathField.SetEnabled(false);
            }

            _uve_SilentToggle = _root.Q<Toggle>("SilentToggle");
            _root.Q<Button>("ScanBtn").clicked += ScanScene;
            _root.Q<Button>("BakeBtn").clicked += OnBakeClicked;
            _uve_SelectInvalidBtn = _root.Q<Button>("SelectInvalidBtn");
            if (_uve_SelectInvalidBtn != null) _uve_SelectInvalidBtn.clicked += SelectInvalidDecals;
        }

        private void UpdatePathField()
        {
            if (_uve_PathField != null)
            {
                var scene = SceneManager.GetActiveScene();
                if (scene.IsValid() && !string.IsNullOrEmpty(scene.path))
                {
                    string actualPath = Path.GetDirectoryName(scene.path).Replace("\\", "/");
                    _uve_PathField.value = actualPath;
                    _uve_PathField.label = "Export Folder (Auto)";
                }
                else
                {
                    _uve_PathField.value = "Unsaved Scene (Please Save First)";
                    _uve_PathField.label = "Export Folder (Auto)";
                }
            }
        }

        private void ScanScene()
        {
            UpdatePathField();
            _foundProjectors.Clear();
            var all = FindObjectsByType<DecalProjectorMini>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int active = 0, invalid = 0;

            foreach (var p in all)
            {
                _foundProjectors.Add(p);
                if (p.gameObject.activeInHierarchy && p.enabled) active++;
                if (p.decalTexture == null) invalid++;
            }

            if (_uve_TotalLabel != null) _uve_TotalLabel.text = all.Length.ToString();
            if (_uve_ActiveLabel != null) _uve_ActiveLabel.text = active.ToString();
            if (_uve_InvalidLabel != null) _uve_InvalidLabel.text = invalid.ToString();
            if (_uve_SelectInvalidBtn != null) _uve_SelectInvalidBtn.style.display = invalid > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void SelectInvalidDecals()
        {
            var invalid = _foundProjectors.FindAll(p => p.decalTexture == null).ConvertAll(p => p.gameObject);
            if (invalid.Count > 0) Selection.objects = invalid.ToArray();
        }

        private void OnBakeClicked()
        {
            EditorUtility.DisplayProgressBar("Decal Baker", "Baking...", 0.5f);
            try
            {
                // 调用核心引擎执行烘焙
                DecalBakeEngine.Bake(SceneManager.GetActiveScene(), _uve_PathField.value, _uve_SilentToggle.value);
                
                // 执行清理策略 (UI 层逻辑)
                ExecuteCleanup();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                ScanScene();
            }
        }

        private void ExecuteCleanup()
        {
            BakeAction strategy = (BakeAction)_uve_ActionField.value;
            if (strategy == BakeAction.KeepObjects) return;

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            foreach (var proj in _foundProjectors)
            {
                if (proj == null) continue;
                switch (strategy)
                {
                    case BakeAction.DisableComponents: Undo.RecordObject(proj, "Disable"); proj.enabled = false; break;
                    case BakeAction.DisableGameObjects: Undo.RecordObject(proj.gameObject, "Disable"); proj.gameObject.SetActive(false); break;
                    case BakeAction.DestroyObjects: Undo.DestroyObjectImmediate(proj.gameObject); break;
                }
            }
            Undo.CollapseUndoOperations(group);
        }
    }
}
