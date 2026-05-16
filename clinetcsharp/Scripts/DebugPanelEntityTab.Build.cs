using Godot;

namespace ClinetCSharp
{
    public partial class DebugPanelEntityTab
    {
        public override void BuildUI(VBoxContainer tabContainer)
        {
            _tabContainer = tabContainer;
            AddTabTitle(tabContainer, "玩家/实体配置", 13);
            AddSectionSeparator(tabContainer);

            BuildCurrentProfileSection(tabContainer);
            AddSectionSeparator(tabContainer);
            BuildProfileActionsSection(tabContainer);
            AddSectionSeparator(tabContainer);
            BuildComponentManagementSection(tabContainer);
            AddSectionSeparator(tabContainer);
            BuildCurrentComponentsSection(tabContainer);

            BuildDialogs();
        }

        private void BuildCurrentProfileSection(Container parent)
        {
            var section = CreateSectionCard(parent, "当前配置");

            var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            row.AddChild(new Label { Name = "_lbl", Text = "当前配置", CustomMinimumSize = new Vector2(72, 0) });
            _profileOption = new OptionButton
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(120, 0)
            };
            row.AddChild(_profileOption);
            section.AddChild(row);

            var nameRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            nameRow.AddChild(new Label { Name = "_lbl", Text = "配置名称", CustomMinimumSize = new Vector2(72, 0) });
            _profileNameEdit = new LineEdit
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                PlaceholderText = "输入当前配置名称"
            };
            nameRow.AddChild(_profileNameEdit);
            section.AddChild(nameRow);

            _profileOption.ItemSelected += OnProfileOptionSelected;
            _profileNameEdit.TextChanged += OnProfileNameChanged;
        }

        private void BuildProfileActionsSection(Container parent)
        {
            var section = CreateSectionCard(parent, "配置操作");

            var hint = new Label
            {
                Text = "这里用于新建或删除玩家、怪物、NPC 共用的配置档。",
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                Modulate = new Color(0.82f, 0.82f, 0.82f),
            };
            section.AddChild(hint);

            var actionRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            actionRow.AddThemeConstantOverride("separation", 8);

            _addProfileBtn = new Button
            {
                Text = "新建配置",
                CustomMinimumSize = new Vector2(96, 28),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                TooltipText = "创建一个新的实体配置档",
            };
            actionRow.AddChild(_addProfileBtn);

            _deleteProfileBtn = new Button
            {
                Text = "删除当前配置",
                CustomMinimumSize = new Vector2(120, 28),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                TooltipText = "删除当前选中的实体配置档",
            };
            actionRow.AddChild(_deleteProfileBtn);

            section.AddChild(actionRow);

            _addProfileBtn.Pressed += OnAddProfilePressed;
            _deleteProfileBtn.Pressed += OnDeleteProfilePressed;
        }

        private void BuildComponentManagementSection(Container parent)
        {
            var section = CreateSectionCard(parent, "组件管理");

            var hint = new Label
            {
                Text = "给当前配置增删组件、启用或停用组件时，从这里进入。",
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                Modulate = new Color(0.82f, 0.82f, 0.82f),
            };
            section.AddChild(hint);

            _manageComponentsBtn = new Button
            {
                Text = "管理组件",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 28),
            };
            section.AddChild(_manageComponentsBtn);
            _manageComponentsBtn.Pressed += OnManageComponentsPressed;
        }

        private void BuildCurrentComponentsSection(Container parent)
        {
            var section = CreateSectionCard(parent, "当前组件");

            _componentContainer = new VBoxContainer
            {
                Name = "ComponentContainer",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            _componentContainer.AddThemeConstantOverride("separation", 8);
            section.AddChild(_componentContainer);
        }

        private void BuildDialogs()
        {
            _newProfileDialog = new AcceptDialog { Title = "新建配置", DialogText = "输入配置名称和类型：" };
            var dialogVBox = new VBoxContainer();
            dialogVBox.AddThemeConstantOverride("separation", 8);
            _newProfileDialog.AddChild(dialogVBox);

            var nameRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            nameRow.AddChild(new Label { Name = "_lbl", Text = "名称", CustomMinimumSize = new Vector2(48, 0) });
            _newProfileNameEdit = new LineEdit
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                PlaceholderText = "输入新配置名称",
                Text = "新配置"
            };
            nameRow.AddChild(_newProfileNameEdit);
            dialogVBox.AddChild(nameRow);

            var typeRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            typeRow.AddChild(new Label { Name = "_lbl", Text = "类型", CustomMinimumSize = new Vector2(48, 0) });
            _newProfileTypeOption = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            _newProfileTypeOption.AddItem("玩家");
            _newProfileTypeOption.AddItem("怪物");
            _newProfileTypeOption.AddItem("NPC");
            typeRow.AddChild(_newProfileTypeOption);
            dialogVBox.AddChild(typeRow);

            _newProfileDialog.Confirmed += OnNewProfileConfirmed;
            Owner.AddChild(_newProfileDialog);

            _deleteProfileDialog = new ConfirmationDialog { Title = "确认删除配置" };
            _deleteProfileDialog.Confirmed += OnDeleteProfileConfirmed;
            Owner.AddChild(_deleteProfileDialog);

            _deleteComponentDialog = new ConfirmationDialog { Title = "确认删除组件" };
            _deleteComponentDialog.Confirmed += OnDeleteComponentConfirmed;
            _deleteComponentDialog.Canceled += () => _pendingDeleteComponentName = null;
            Owner.AddChild(_deleteComponentDialog);

            _manageComponentsDialog = new AcceptDialog { Title = "管理组件" };
            Owner.AddChild(_manageComponentsDialog);
        }

        private static VBoxContainer CreateSectionCard(Container parent, string title)
        {
            var section = new VBoxContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                Name = $"{title}Section",
            };
            section.AddThemeConstantOverride("separation", 6);

            var frame = new PanelContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            frame.AddThemeStyleboxOverride("panel", new StyleBoxFlat
            {
                BgColor = new Color(0.12f, 0.12f, 0.12f, 0.55f),
                BorderColor = new Color(0.34f, 0.34f, 0.34f, 0.95f),
                BorderWidthLeft = 1,
                BorderWidthTop = 1,
                BorderWidthRight = 1,
                BorderWidthBottom = 1,
                CornerRadiusTopLeft = 8,
                CornerRadiusTopRight = 8,
                CornerRadiusBottomRight = 8,
                CornerRadiusBottomLeft = 8,
                ContentMarginLeft = 10,
                ContentMarginTop = 8,
                ContentMarginRight = 10,
                ContentMarginBottom = 10,
            });

            var body = new VBoxContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                Name = "Body",
            };
            body.AddThemeConstantOverride("separation", 8);

            var header = new Label
            {
                Name = "_lbl",
                Text = title,
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            header.AddThemeFontSizeOverride("font_size", 12);
            body.AddChild(header);

            frame.AddChild(body);
            section.AddChild(frame);
            parent.AddChild(section);
            return body;
        }
    }
}
