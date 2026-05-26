using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using ModularDemo.Runtime;
using DecalMini;

namespace ModularDemo.Editor
{
    /// <summary>
    /// 加特林射击系统的专业版编辑器
    /// 提供可视化图库选择弹孔贴图
    /// </summary>
    [CustomEditor(typeof(DecalGatlingShooter))]
    public class DecalGatlingShooterEditor : DecalEditorBaseMini
    {
        public override VisualElement CreateInspectorGUI()
        {
            _root = new VisualElement();

            // 1. 加载并绑定核心样式表
            ApplyGlobalStyle(_root);

            // 2. 默认属性绘制
            InspectorElement.FillDefaultInspector(_root, serializedObject, this);

            // 3. 添加间距
            _root.Add(new VisualElement { style = { height = 15 } });

            // 4. 构建贴花选择图库
            CreateBaseGUI(_root);

            return _root;
        }

        protected override void OnGridItemClick(Texture2D tex)
        {
            var shooter = target as DecalGatlingShooter;
            if (shooter == null) return;

            Undo.RecordObject(shooter, "Change Bullet Decal");
            shooter.bulletDecal = tex;
            EditorUtility.SetDirty(shooter);
        }

        protected override bool IsItemSelected(Texture2D tex)
        {
            var shooter = target as DecalGatlingShooter;
            if (shooter == null) return false;

            return shooter.bulletDecal == tex;
        }
    }
}
