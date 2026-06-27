using Godot;

namespace ClinetCSharp
{
    public partial class GMPanel
    {
        private void BuildContent()
        {
            _content.AddThemeConstantOverride("separation", 4);

            BuildCommandBar();
            BuildGroupScroll();
            BuildInlineCommandEditor();
            BuildInlineGroupEditor();
            BuildToolbar();
            BuildContextMenu();

            UiUtils.ConfigureTransientDragControlFocus(this);
        }

        private void BuildCommandBar()
        {
            var commandBox = new HBoxContainer();

            _cmdEdit = new LineEdit
            {
                PlaceholderText = "例如 additem,1001,20",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 32),
            };
            _cmdEdit.TextSubmitted += (_) => OnExecPressed();
            commandBox.AddChild(_cmdEdit);

            var executeButton = new Button
            {
                Text = "执行",
                CustomMinimumSize = new Vector2(60, 32),
            };
            executeButton.Pressed += OnExecPressed;
            commandBox.AddChild(executeButton);

            _content.AddChild(commandBox);
        }

        private void BuildGroupScroll()
        {
            _groupScroll = new ScrollContainer
            {
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            };

            _groupContainer = new VBoxContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };

            _groupScroll.AddChild(_groupContainer);
            _content.AddChild(_groupScroll);
        }

        private void BuildInlineCommandEditor()
        {
            _addCmdRow = new HBoxContainer { Visible = false };
            _addCmdRow.AddThemeConstantOverride("separation", 4);

            _addCmdLabelEdit = new LineEdit
            {
                PlaceholderText = "标签",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 28),
            };
            _addCmdRow.AddChild(_addCmdLabelEdit);

            _addCmdEdit = new LineEdit
            {
                PlaceholderText = "命令",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 28),
            };
            _addCmdEdit.TextSubmitted += (_) => ConfirmAddCommand();
            _addCmdRow.AddChild(_addCmdEdit);

            var confirmButton = new Button { Text = "确定", CustomMinimumSize = new Vector2(44, 28) };
            confirmButton.Pressed += ConfirmAddCommand;
            _addCmdRow.AddChild(confirmButton);

            var cancelButton = new Button { Text = "取消", CustomMinimumSize = new Vector2(44, 28) };
            cancelButton.Pressed += CancelAddCommand;
            _addCmdRow.AddChild(cancelButton);

            _content.AddChild(_addCmdRow);
        }

        private void BuildInlineGroupEditor()
        {
            _addGroupRow = new HBoxContainer { Visible = false };
            _addGroupRow.AddThemeConstantOverride("separation", 4);

            _addGroupEdit = new LineEdit
            {
                PlaceholderText = "分组名称",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 28),
            };
            _addGroupEdit.TextSubmitted += (_) => ConfirmAddGroup();
            _addGroupRow.AddChild(_addGroupEdit);

            var confirmButton = new Button { Text = "确定", CustomMinimumSize = new Vector2(44, 28) };
            confirmButton.Pressed += ConfirmAddGroup;
            _addGroupRow.AddChild(confirmButton);

            var cancelButton = new Button { Text = "取消", CustomMinimumSize = new Vector2(44, 28) };
            cancelButton.Pressed += CancelAddGroup;
            _addGroupRow.AddChild(cancelButton);

            _content.AddChild(_addGroupRow);
        }

        private void BuildToolbar()
        {
            var toolbar = new HBoxContainer();
            toolbar.AddThemeConstantOverride("separation", 4);

            var addGroupButton = new Button
            {
                Text = "+ 添加分组",
                CustomMinimumSize = new Vector2(0, 28),
            };
            addGroupButton.Pressed += OnAddGroupPressed;
            toolbar.AddChild(addGroupButton);

            var giveTestItemsButton = new Button
            {
                Text = "一键获取测试道具",
                CustomMinimumSize = new Vector2(0, 28),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                TooltipText = "向背包添加全部测试道具（默认每种10个）",
            };
            giveTestItemsButton.Pressed += () => ExecuteGmCommand("addtestitems,10");
            toolbar.AddChild(giveTestItemsButton);

            _content.AddChild(toolbar);
        }

        private void BuildContextMenu()
        {
            _cmdMenu = new PopupMenu();
            _cmdMenu.AddItem("编辑", 0);
            _cmdMenu.AddItem("删除", 1);
            _cmdMenu.IdPressed += OnCmdMenuIdPressed;
            AddChild(_cmdMenu);
        }

        private void OnAddGroupPressed()
        {
            CancelAddCommand();
            _addGroupRow.Visible = true;
            _addGroupEdit.Text = "";
            _addGroupEdit.GrabFocus();
        }

        private void ConfirmAddGroup()
        {
            string name = _addGroupEdit.Text.StripEdges();
            if (name != "")
            {
                AddGroupData(name);
                RebuildGroupUI();
                SaveGmConfig();
            }

            _addGroupRow.Visible = false;
        }

        private void CancelAddGroup()
        {
            _addGroupRow.Visible = false;
        }

        private void ShowAddCommandRow(GmGroup group)
        {
            CancelAddGroup();
            _addCmdTarget = group;
            _editCmdTarget = null;
            _addCmdRow.Visible = true;
            _addCmdLabelEdit.Text = "";
            _addCmdEdit.Text = "";
            _addCmdLabelEdit.PlaceholderText = $"标签 ({group.Name})";
            _addCmdLabelEdit.GrabFocus();
        }

        private void ShowEditCommandRow(GmGroup group, GmCommand command)
        {
            CancelAddGroup();
            _addCmdTarget = group;
            _editCmdTarget = command;
            _addCmdRow.Visible = true;
            _addCmdLabelEdit.Text = command.Label;
            _addCmdEdit.Text = command.Cmd;
            _addCmdLabelEdit.GrabFocus();
        }

        private void ConfirmAddCommand()
        {
            if (_addCmdTarget == null)
                return;

            string label = _addCmdLabelEdit.Text.StripEdges();
            string cmd = _addCmdEdit.Text.StripEdges();
            if (label == "" || cmd == "")
                return;

            if (_editCmdTarget != null)
            {
                _editCmdTarget.Label = label;
                _editCmdTarget.Cmd = cmd;
            }
            else
            {
                AddCommandData(_addCmdTarget, label, cmd);
            }

            RebuildGroupUI();
            SaveGmConfig();
            CancelAddCommand();
        }

        private void CancelAddCommand()
        {
            _addCmdRow.Visible = false;
            _addCmdTarget = null;
            _editCmdTarget = null;
        }

        private void OnCmdMenuIdPressed(long id)
        {
            if (_menuCmd == null || _menuGroup == null)
                return;

            if (id == 0)
            {
                ShowEditCommandRow(_menuGroup, _menuCmd);
            }
            else if (id == 1)
            {
                _menuGroup.Commands.Remove(_menuCmd);
                RebuildGroupUI();
                SaveGmConfig();
            }

            _menuCmd = null;
            _menuGroup = null;
        }

        private void RebuildGroupUI()
        {
            foreach (var child in _groupContainer.GetChildren())
                child.QueueFree();

            foreach (var group in _groups)
            {
                var capturedGroup = group;
                _groupContainer.AddChild(BuildGroupHeader(capturedGroup));
                _groupContainer.AddChild(BuildGroupFlow(capturedGroup));
            }
        }

        private HBoxContainer BuildGroupHeader(GmGroup group)
        {
            var header = new HBoxContainer();
            header.AddThemeConstantOverride("separation", 4);

            var collapseButton = new Button
            {
                Text = group.Collapsed ? ">" : "v",
                CustomMinimumSize = new Vector2(24, 24),
                TooltipText = group.Collapsed ? "展开" : "折叠",
            };
            collapseButton.Pressed += () =>
            {
                group.Collapsed = !group.Collapsed;
                RebuildGroupUI();
                SaveGmConfig();
            };
            header.AddChild(collapseButton);

            var nameLabel = new Label
            {
                Text = group.Name,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            nameLabel.AddThemeFontSizeOverride("font_size", 13);
            header.AddChild(nameLabel);

            var addCommandButton = new Button
            {
                Text = "+",
                CustomMinimumSize = new Vector2(28, 24),
                TooltipText = "添加命令",
            };
            addCommandButton.Pressed += () => ShowAddCommandRow(group);
            header.AddChild(addCommandButton);

            var deleteGroupButton = new Button
            {
                Text = "x",
                CustomMinimumSize = new Vector2(28, 24),
                TooltipText = "删除分组",
            };
            deleteGroupButton.Pressed += () =>
            {
                _groups.Remove(group);
                CancelAddCommand();
                RebuildGroupUI();
                SaveGmConfig();
            };
            header.AddChild(deleteGroupButton);

            return header;
        }

        private HFlowContainer BuildGroupFlow(GmGroup group)
        {
            var flow = new HFlowContainer
            {
                Visible = !group.Collapsed,
            };
            flow.AddThemeConstantOverride("h_separation", 4);
            flow.AddThemeConstantOverride("v_separation", 4);

            foreach (var command in group.Commands)
                flow.AddChild(BuildCommandButton(group, command));

            return flow;
        }

        private Button BuildCommandButton(GmGroup group, GmCommand command)
        {
            var button = new Button
            {
                Text = command.Label,
                CustomMinimumSize = new Vector2(0, 26),
            };

            button.Pressed += () =>
            {
                _cmdEdit.Text = command.Cmd;
                _cmdEdit.GrabFocus();
            };

            button.GuiInput += (@event) =>
            {
                if (@event is InputEventMouseButton mouseButton &&
                    mouseButton.Pressed &&
                    mouseButton.ButtonIndex == MouseButton.Right)
                {
                    _menuCmd = command;
                    _menuGroup = group;
                    _cmdMenu.Position = (Vector2I)button.GlobalPosition + new Vector2I(0, (int)button.Size.Y);
                    _cmdMenu.ResetSize();
                    _cmdMenu.Popup();
                }
            };

            return button;
        }
    }
}
