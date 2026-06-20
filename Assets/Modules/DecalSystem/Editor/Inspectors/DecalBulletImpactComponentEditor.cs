using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DecalMini
{
    [CustomEditor(typeof(DecalBulletImpactComponent))]
    public sealed class DecalBulletImpactComponentEditor : DecalEditorBaseMini
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

            CreateModeHeader();
            CreateCollisionSection();
            CreateVisualSection();
            CreateParticleSection();

            var gridContainer = new VisualElement();
            gridContainer.style.marginTop = 15;
            CreateBaseGUI(gridContainer);
            _root.Add(gridContainer);

            UpdateTexturePreview();
            return _root;
        }

        private void CreateModeHeader()
        {
            var header = new VisualElement();
            header.AddToClassList("inspector-section");
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.alignItems = Align.Center;
            header.style.paddingBottom = 8;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);

            var label = new Label("Bullet Impact");
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.fontSize = 14;
            header.Add(label);

            var modeField = new PropertyField(serializedObject.FindProperty("triggerMode"), string.Empty);
            modeField.style.width = 170;
            header.Add(modeField);
            _root.Add(header);
        }

        private void CreateCollisionSection()
        {
            var box = CreateSection("Impact Behaviour");
            box.Add(new PropertyField(serializedObject.FindProperty("hitLayers")));
            box.Add(new PropertyField(serializedObject.FindProperty("emitOnce")));
            box.Add(new PropertyField(serializedObject.FindProperty("destroyAfterImpact")));
            box.Add(new PropertyField(serializedObject.FindProperty("raycastPadding")));
            _root.Add(box);
        }

        private void CreateVisualSection()
        {
            var box = CreateSection("Bullet Mark");

            var slot = new VisualElement();
            slot.AddToClassList("decal-slot-item");
            slot.style.marginBottom = 8;

            var slotLabel = new Label("Impact Texture");
            slotLabel.AddToClassList("decal-slot-label");
            slot.Add(slotLabel);

            _texturePreview = new VisualElement();
            _texturePreview.AddToClassList("decal-slot-preview");
            slot.Add(_texturePreview);
            box.Add(slot);

            box.Add(new PropertyField(serializedObject.FindProperty("impactModule.color")));
            box.Add(CreateSizeRangeField());
            box.Add(new PropertyField(serializedObject.FindProperty("impactModule.projectionDepth")));
            box.Add(new PropertyField(serializedObject.FindProperty("impactModule.softFade")));
            box.Add(new PropertyField(serializedObject.FindProperty("impactModule.sortingOrder")));
            box.Add(new PropertyField(serializedObject.FindProperty("impactModule.lifetime")));
            box.Add(new PropertyField(serializedObject.FindProperty("impactModule.normalOffset")));
            box.Add(new PropertyField(serializedObject.FindProperty("impactModule.allowedLayers")));
            box.Add(new PropertyField(serializedObject.FindProperty("impactModule.alignToIncomingDirection")));
            box.Add(new PropertyField(serializedObject.FindProperty("impactModule.randomRoll")));

            _root.Add(box);
        }

        private VisualElement CreateSizeRangeField()
        {
            var sizeRange = serializedObject.FindProperty("impactModule.sizeRange");

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 3;
            row.style.marginBottom = 3;
            row.style.minHeight = 22;

            var label = new Label("Size Range");
            label.style.minWidth = 120;
            label.style.maxWidth = 120;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            row.Add(label);

            var fields = new VisualElement();
            fields.style.flexDirection = FlexDirection.Row;
            fields.style.flexGrow = 1;
            fields.style.flexShrink = 1;

            fields.Add(CreateCompactFloatField("Min", sizeRange.FindPropertyRelative("x")));
            fields.Add(CreateCompactFloatField("Max", sizeRange.FindPropertyRelative("y")));
            row.Add(fields);
            return row;
        }

        private static VisualElement CreateCompactFloatField(string labelText, SerializedProperty property)
        {
            var group = new VisualElement();
            group.style.flexDirection = FlexDirection.Row;
            group.style.alignItems = Align.Center;
            group.style.flexGrow = 1;
            group.style.flexShrink = 1;
            group.style.flexBasis = 0;
            group.style.marginRight = 6;

            var label = new Label(labelText);
            label.style.minWidth = 28;
            label.style.maxWidth = 28;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            label.style.color = new Color(0.72f, 0.72f, 0.72f, 1f);
            group.Add(label);

            var field = new FloatField();
            field.BindProperty(property);
            field.style.flexGrow = 1;
            field.style.flexShrink = 1;
            field.style.minWidth = 44;
            group.Add(field);
            return group;
        }

        private void CreateParticleSection()
        {
            var box = CreateSection("Particle");
            box.Add(new PropertyField(serializedObject.FindProperty("impactModule.impactParticlePrefab")));
            box.Add(new PropertyField(serializedObject.FindProperty("impactModule.particleLifetime")));
            _root.Add(box);
        }

        private VisualElement CreateSection(string title)
        {
            var box = new VisualElement();
            box.AddToClassList("inspector-section");
            box.style.marginTop = 10;

            var label = new Label(title.ToUpperInvariant());
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginBottom = 5;
            label.style.color = new Color(0.7f, 0.7f, 0.7f);
            box.Add(label);
            return box;
        }

        protected override void OnGridItemClick(Texture2D tex)
        {
            serializedObject.Update();
            serializedObject.FindProperty("impactModule.texture").objectReferenceValue = tex;
            serializedObject.ApplyModifiedProperties();
            UpdateTexturePreview();
        }

        protected override bool IsItemSelected(Texture2D tex)
        {
            var texture = serializedObject.FindProperty("impactModule.texture").objectReferenceValue;
            return texture == tex;
        }

        private void UpdateTexturePreview()
        {
            if (_texturePreview == null)
                return;

            var texture = serializedObject.FindProperty("impactModule.texture").objectReferenceValue as Texture2D;
            _texturePreview.style.backgroundImage = texture;
        }
    }
}
