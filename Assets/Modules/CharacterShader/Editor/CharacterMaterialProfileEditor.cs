using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using CharacterShader.Runtime;

namespace CharacterShader.Editor
{
    /// <summary>
    /// Custom Inspector for CharacterMaterialProfile using UI Toolkit.
    /// Implements the Premium High-Contrast Industrial-Grade Editor UI Aesthetic.
    /// </summary>
    [CustomEditor(typeof(CharacterMaterialProfile))]
    public class CharacterMaterialProfileEditor : UnityEditor.Editor
    {
        private CharacterMaterialProfile.MaterialKind _activeTab = CharacterMaterialProfile.MaterialKind.Skin;
        private VisualElement _slotContainer;

        public override VisualElement CreateInspectorGUI()
        {
            var profile = (CharacterMaterialProfile)target;
            var root = new VisualElement();

            // Load USS
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Modules/CharacterShader/Editor/CharacterMaterialProfileEditor.uss");
            if (styleSheet != null)
            {
                root.styleSheets.Add(styleSheet);
            }
            root.AddToClassList("root");

            // Header
            var header = new VisualElement();
            header.AddToClassList("header");
            var title = new Label("CHARACTER MATERIAL PROFILE");
            title.AddToClassList("title");
            header.Add(title);
            var subtitle = new Label("NPR Shader Physical Material Definitions");
            subtitle.AddToClassList("subtitle");
            header.Add(subtitle);
            root.Add(header);

            // Ensure we have 8 slots
            var slotsProp = serializedObject.FindProperty("slots");
            if (slotsProp.arraySize != CharacterMaterialProfile.SlotCount)
            {
                profile.EnsureSlotCount();
                serializedObject.Update();
            }

            // Tab Selector
            var tabSelector = new EnumField("Active Material Slot", _activeTab);
            tabSelector.AddToClassList("config-field");
            tabSelector.RegisterValueChangedCallback(evt => {
                _activeTab = (CharacterMaterialProfile.MaterialKind)evt.newValue;
                RefreshSlotContainer();
            });
            root.Add(tabSelector);

            // Container for the active Slot
            _slotContainer = new VisualElement();
            root.Add(_slotContainer);

            RefreshSlotContainer();

            // Reset Button
            var resetBtn = new Button(() => {
                if (EditorUtility.DisplayDialog("Confirm Reset", "Reset all 8 slots to default presets? This will overwrite your current settings.", "Reset to Defaults", "Cancel"))
                {
                    Undo.RecordObject(profile, "Reset Profile Defaults");
                    profile.ResetToDefaults();
                    EditorUtility.SetDirty(profile);
                    serializedObject.Update();
                    RefreshSlotContainer();
                }
            }) { text = "RESET TO DEFAULTS" };
            resetBtn.AddToClassList("btn-reset");
            root.Add(resetBtn);

            return root;
        }

        private void RefreshSlotContainer()
        {
            if (_slotContainer == null) return;
            _slotContainer.Clear();

            var profile = (CharacterMaterialProfile)target;
            var slotsProp = serializedObject.FindProperty("slots");
            int index = (int)_activeTab;
            
            if (index >= 0 && index < slotsProp.arraySize)
            {
                var slotProp = slotsProp.GetArrayElementAtIndex(index);
                _slotContainer.Add(CreateSlotCard(profile, slotProp, index));
            }
        }

        private VisualElement CreateSlotCard(CharacterMaterialProfile profile, SerializedProperty slotProp, int index)
        {
            var card = new VisualElement();
            card.AddToClassList("card");

            var header = new VisualElement();
            header.AddToClassList("card-header");

            var title = new Label($"[Slot {index}] {slotProp.FindPropertyRelative("name").stringValue}");
            title.AddToClassList("card-title");
            
            // Re-bind title when name changes dynamically
            title.TrackPropertyValue(slotProp.FindPropertyRelative("name"), prop => {
                title.text = $"[Slot {index}] {prop.stringValue}";
            });

            header.Add(title);

            var presetBtn = new Button(() => ShowPresetMenu(profile, index)) { text = "Apply Preset ▼" };
            presetBtn.AddToClassList("btn-preset");
            header.Add(presetBtn);

            card.Add(header);

            // Add configuration fields
            card.Add(CreateField(slotProp.FindPropertyRelative("name")));
            card.Add(CreateField(slotProp.FindPropertyRelative("kind")));
            card.Add(CreateField(slotProp.FindPropertyRelative("rampSlice")));
            card.Add(CreateField(slotProp.FindPropertyRelative("matCapSlice")));
            card.Add(CreateField(slotProp.FindPropertyRelative("matCapStrength")));
            card.Add(CreateField(slotProp.FindPropertyRelative("rimStrength")));
            card.Add(CreateField(slotProp.FindPropertyRelative("shadowHardness")));
            card.Add(CreateField(slotProp.FindPropertyRelative("rampBiasScale")));
            card.Add(CreateField(slotProp.FindPropertyRelative("metalShadowLift")));
            card.Add(CreateField(slotProp.FindPropertyRelative("aoStrength")));

            return card;
        }

        private PropertyField CreateField(SerializedProperty prop)
        {
            var field = new PropertyField(prop);
            field.AddToClassList("config-field");
            field.BindProperty(prop);
            return field;
        }

        private void ShowPresetMenu(CharacterMaterialProfile profile, int slotIndex)
        {
            GenericMenu menu = new GenericMenu();
            foreach (CharacterMaterialProfile.MaterialKind kind in System.Enum.GetValues(typeof(CharacterMaterialProfile.MaterialKind)))
            {
                menu.AddItem(new GUIContent(kind.ToString()), false, () => {
                    Undo.RecordObject(profile, $"Apply {kind} Preset");
                    profile.ApplyPreset(slotIndex, kind);
                    EditorUtility.SetDirty(profile);
                    serializedObject.Update();
                    RefreshSlotContainer(); // Refresh to show new values immediately
                });
            }
            menu.ShowAsContext();
        }
    }
}
