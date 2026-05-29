using Godot;

namespace ClinetCSharp
{
    /// <summary>
    /// 实体模板预览面板 — 可拖动，内置 SubViewport 直接显示预览实体
    /// </summary>
    public partial class EntityProfilePreviewPanel : DraggablePanel
    {
        private Label _infoLabel;
        private SubViewportContainer _subViewportContainer;
        private SubViewport _subViewport;

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

            _subViewportContainer = new SubViewportContainer
            {
                CustomMinimumSize = new Vector2(300, 200),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                Stretch = true,
            };
            _subViewport = new SubViewport
            {
                Size = new Vector2I(300, 200),
                RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
                TransparentBg = false,
            };
            var camera = new Camera2D
            {
                Position = Vector2.Zero,
                AnchorMode = Camera2D.AnchorModeEnum.DragCenter,
            };
            _subViewport.AddChild(camera);
            _subViewportContainer.AddChild(_subViewport);
            content.AddChild(_subViewportContainer);

            vbox.AddChild(content);
        }

        protected override void OnClosed()
        {
            OnClosePreview?.Invoke();
        }

        public void UpdateInfo(string profileName, string entityType, int profileId)
        {
            _infoLabel.Text = $"模板: {profileName}\n类型: {entityType}\nID: {profileId}";
        }

        /// <summary>将预览实体放入 SubViewport 中显示</summary>
        public void SetPreviewEntity(EntityBase entity)
        {
            if (_subViewport == null)
                return;

            // 移除旧的预览实体
            foreach (var child in _subViewport.GetChildren())
            {
                if (child is EntityBase)
                {
                    child.QueueFree();
                }
            }

            if (entity != null)
            {
                _subViewport.AddChild(entity);
                entity.Position = Vector2.Zero;
            }
        }
    }
}
