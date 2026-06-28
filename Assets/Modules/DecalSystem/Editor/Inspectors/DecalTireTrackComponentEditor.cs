using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DecalMini
{
    [CustomEditor(typeof(DecalTireTrackComponent))]
    public class DecalTireTrackComponentEditor : DecalEditorBaseMini
    {
        private VisualElement _texturePreview;

        public override VisualElement CreateInspectorGUI()
        {
            _root = new VisualElement();
            _root.AddToClassList("decal-inspector-root");
            _root.style.paddingTop = 10;

            StyleSheet style = DecalMini.Editor.DecalSystemPathUtility.LoadUSS("DecalSystemStyle");
            if (style != null)
                _root.styleSheets.Add(style);

            StyleSheet editorStyle = DecalMini.Editor.DecalSystemPathUtility.LoadUSS("DecalSystemEditor");
            if (editorStyle != null)
                _root.styleSheets.Add(editorStyle);

            CreateHeader(_root);
            CreateEmitterSection(_root);
            CreateVisualSection(_root);
            CreateSamplingSection(_root);
            CreateLifetimeSection(_root);
            CreateGallerySection(_root);

            UpdateTexturePreview();
            return _root;
        }

        protected override void OnGridItemClick(Texture2D tex)
        {
            serializedObject.Update();
            SerializedProperty textureProperty = serializedObject.FindProperty("tireTrackModule.texture");
            textureProperty.objectReferenceValue = tex;
            serializedObject.ApplyModifiedProperties();
            UpdateTexturePreview();
        }

        protected override bool IsItemSelected(Texture2D tex)
        {
            var component = target as DecalTireTrackComponent;
            return component != null
                && component.tireTrackModule != null
                && component.tireTrackModule.texture == tex;
        }

        private void CreateHeader(VisualElement root)
        {
            var header = new VisualElement();
            header.AddToClassList("inspector-section");
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.paddingBottom = 8;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);

            var title = new Label("Tire Track");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 14;
            header.Add(title);

            var emitField = new PropertyField(serializedObject.FindProperty("emitOnPlay"), "Emit");
            emitField.style.width = 120;
            header.Add(emitField);
            root.Add(header);
        }

        private void CreateEmitterSection(VisualElement root)
        {
            var section = CreateSection("EMITTER");
            section.Add(new PropertyField(serializedObject.FindProperty("wheelAnchors")));
            root.Add(section);
        }

        private void CreateVisualSection(VisualElement root)
        {
            var section = CreateSection("VISUAL");

            _texturePreview = new VisualElement();
            _texturePreview.AddToClassList("decal-slot-preview");
            _texturePreview.style.height = 92;
            _texturePreview.style.marginBottom = 8;
            section.Add(_texturePreview);

            section.Add(new PropertyField(serializedObject.FindProperty("tireTrackModule.material")));
            section.Add(new PropertyField(serializedObject.FindProperty("tireTrackModule.color")));
            section.Add(new PropertyField(serializedObject.FindProperty("tireTrackModule.trackWidth")));
            section.Add(new PropertyField(serializedObject.FindProperty("tireTrackModule.textureRepeatMeters")));
            section.Add(new PropertyField(serializedObject.FindProperty("tireTrackModule.sortingOrder")));
            root.Add(section);
        }

        private void CreateSamplingSection(VisualElement root)
        {
            var section = CreateSection("SAMPLING");
            section.Add(new PropertyField(serializedObject.FindProperty("tireTrackModule.wheelCount")));
            section.Add(new PropertyField(serializedObject.FindProperty("tireTrackModule.wheelSpacing")));
            section.Add(new PropertyField(serializedObject.FindProperty("tireTrackModule.sampleDistance")));
            section.Add(new PropertyField(serializedObject.FindProperty("tireTrackModule.minSpeed")));
            section.Add(new PropertyField(serializedObject.FindProperty("tireTrackModule.groundLayer")));
            section.Add(new PropertyField(serializedObject.FindProperty("tireTrackModule.raycastDistance")));
            section.Add(new PropertyField(serializedObject.FindProperty("tireTrackModule.normalOffset")));
            root.Add(section);
        }

        private void CreateLifetimeSection(VisualElement root)
        {
            var section = CreateSection("LIFETIME");
            section.Add(new PropertyField(serializedObject.FindProperty("tireTrackModule.lifetime")));
            section.Add(new PropertyField(serializedObject.FindProperty("tireTrackModule.fadeDuration")));
            section.Add(new PropertyField(serializedObject.FindProperty("tireTrackModule.maxPointsPerWheel")));
            root.Add(section);
        }

        private void CreateGallerySection(VisualElement root)
        {
            var gridContainer = new VisualElement();
            gridContainer.style.marginTop = 15;
            CreateBaseGUI(gridContainer);
            root.Add(gridContainer);
        }

        private VisualElement CreateSection(string title)
        {
            var section = new VisualElement();
            section.AddToClassList("inspector-section");
            section.style.marginTop = 10;

            var label = new Label(title);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginBottom = 5;
            label.style.color = new Color(0.7f, 0.7f, 0.7f);
            section.Add(label);

            return section;
        }

        private void UpdateTexturePreview()
        {
            if (_texturePreview == null)
                return;

            var component = target as DecalTireTrackComponent;
            Texture2D texture = component != null && component.tireTrackModule != null
                ? component.tireTrackModule.texture
                : null;

            _texturePreview.style.backgroundImage = texture;
        }
    }
}
