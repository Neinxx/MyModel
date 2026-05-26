using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DecalMini
{
    /// <summary>
    /// 贴花系统编辑器基类：提供标准图库选择器、序列帧标记以及资产状态管理
    /// </summary>
    public abstract class DecalEditorBaseMini : UnityEditor.Editor
    {
        // ========================================================================
        // 1. 核心字段
        // ========================================================================

        protected VisualElement _root;
        protected VisualElement _gridContainer;

        // ========================================================================
        // 2. 编辑器生命周期 (Unity Editor)
        // ========================================================================

        /// <summary>
        /// 加载并应用全局贴花系统样式 (USS)
        /// </summary>
        protected void ApplyGlobalStyle(VisualElement root)
        {
            var uss = DecalMini.Editor.DecalSystemPathUtility.LoadUSS("DecalSystemStyle");
            if (uss != null)
            {
                root.styleSheets.Add(uss);
                root.AddToClassList("decal-root");
            }
        }

        public override VisualElement CreateInspectorGUI() => null;

        // ========================================================================
        // 3. 基础 GUI 构建
        // ========================================================================

        /// <summary>
        /// 创建标准图集选择网格
        /// </summary>
        protected void CreateBaseGUI(VisualElement container)
        {
            if (container == null)
                return;

            var title = new Label("Decal Gallery (Click to apply)");
            title.AddToClassList("decal-card-title"); // 使用统一卡片标题样式
            container.Add(title);

            _gridContainer = new VisualElement();
            _gridContainer.AddToClassList("decal-gallery-grid");
            container.Add(_gridContainer);

            RefreshGrid();
        }

        /// <summary>
        /// 刷新图集网格内容与状态
        /// </summary>
        public void RefreshGrid()
        {
            if (_gridContainer == null)
                return;

            _gridContainer.Clear();
            var config = DecalSystemMini.CurrentConfig;
            if (config == null)
                return;

            foreach (var slice in config.slices)
            {
                var tex = slice.albedoMap;
                if (tex == null)
                    continue;

                var btn = CreateGridItem(tex);
                _gridContainer.Add(btn);
            }
        }

        // ========================================================================
        // 4. 子类扩展接口
        // ========================================================================

        protected abstract void OnGridItemClick(Texture2D tex);

        protected abstract bool IsItemSelected(Texture2D tex);

        // ========================================================================
        // 5. 内部构建逻辑
        // ========================================================================

        private Button CreateGridItem(Texture2D tex)
        {
            var btn = new Button(() =>
            {
                OnGridItemClick(tex);
                RefreshGrid();
            });

            btn.AddToClassList("decal-item-btn");
            btn.style.backgroundImage = tex;

            // 序列帧自动标记
            int flipCount = DecalSystemMini.GetFlipbookCount(tex);
            if (flipCount > 1)
            {
                var badge = new Label($"▶ {flipCount}f");
                badge.AddToClassList("decal-anim-badge");
                btn.Add(badge);
            }

            if (IsItemSelected(tex))
            {
                btn.AddToClassList("decal-item-btn--selected");
            }

            return btn;
        }
    }
}
