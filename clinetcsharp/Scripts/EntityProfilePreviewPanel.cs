using Godot;

namespace ClinetCSharp
{
    /// <summary>
    /// 实体模板预览面板 — 可拖动，内置 SubViewport 直接显示预览地图。
    /// 预览地图完全等同于一张真实地图的迷你版本，确保渲染效果 1:1 一致。
    /// </summary>
    public partial class EntityProfilePreviewPanel : DraggablePanel
    {
        private Label _infoLabel;
        private SubViewportContainer _subViewportContainer;
        private SubViewport _subViewport;
        private PreviewMap _previewMap;

        public System.Action OnClosePreview;

        public override void _Ready()
        {
            BuildStructure();
            base._Ready();
        }

        private void BuildStructure()
        {
            var vbox = new VBoxContainer { Name = "VBoxContainer" };
            AddChild(vbox);

            var titleBar = new PanelContainer { Name = "TitleBar" };
            var titleHBox = new HBoxContainer { Name = "HBoxContainer" };
            titleHBox.AddChild(new Label { Text = "模板预览" });
            titleHBox.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
            var closeBtn = new Button { Name = "CloseButton", Text = "×" };
            titleHBox.AddChild(closeBtn);
            titleBar.AddChild(titleHBox);
            vbox.AddChild(titleBar);

            var content = new VBoxContainer { Name = "Content" };
            content.AddThemeConstantOverride("separation", 8);
            _infoLabel = new Label
            {
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                Modulate = new Color(0.82f, 0.82f, 0.82f),
            };
            content.AddChild(_infoLabel);

            // 用 CenterContainer 包裹 SubViewportContainer，确保预览内容居中且不会被拉伸
            var viewportWrapper = new CenterContainer { Name = "ViewportWrapper" };
            _subViewportContainer = new SubViewportContainer
            {
                CustomMinimumSize = new Vector2(300, 200),
                Stretch = false,
            };
            _subViewport = new SubViewport
            {
                Size = new Vector2I(300, 200),
                RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
                TransparentBg = false,
            };
            _subViewportContainer.AddChild(_subViewport);
            viewportWrapper.AddChild(_subViewportContainer);
            content.AddChild(viewportWrapper);

            vbox.AddChild(content);

            // 创建预览地图（完全等同于一张真实地图）
            _previewMap = new PreviewMap();
            _subViewport.AddChild(_previewMap);
        }

        protected override void OnClosed()
        {
            OnClosePreview?.Invoke();
        }

        public void UpdateInfo(string profileName, string entityType, int profileId)
        {
            _infoLabel.Text = $"模板: {profileName}\n类型: {entityType}\nID: {profileId}";
        }

        /// <summary>将预览实体放入预览地图中显示</summary>
        public void SetPreviewEntity(EntityBase entity)
        {
            _previewMap?.SetEntity(entity);
        }

        /// <summary>清除预览实体</summary>
        public void ClearPreviewEntity()
        {
            _previewMap?.ClearEntity();
        }
    }
}
