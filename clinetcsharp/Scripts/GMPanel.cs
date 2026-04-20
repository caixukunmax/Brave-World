using Godot;
using System.Collections.Generic;
using Protocol;

namespace ClinetCSharp
{
    /// <summary>
    /// GM 调试面板 - 发送 GM 命令到服务器
    /// 按 F2 开关
    /// 支持动态增删改分组和命令（内联编辑）
    /// </summary>
    public partial class GMPanel : CanvasLayer
    {
        private Panel _panel;
        private VBoxContainer _vbox;
        private LineEdit _cmdEdit;
        private RichTextLabel _logOutput;
        private ScrollContainer _groupScroll;
        private VBoxContainer _groupContainer;
        private bool _isVisible = false;

        // 内联编辑：添加分组
        private HBoxContainer _addGroupRow;
        private LineEdit _addGroupEdit;

        // 内联编辑：添加/编辑命令
        private HBoxContainer _addCmdRow;
        private LineEdit _addCmdLabelEdit;
        private LineEdit _addCmdEdit;
        private GmGroup _addCmdTarget;
        private GmCommand _editCmdTarget; // 非 null 表示编辑模式

        // 右键菜单
        private PopupMenu _cmdMenu;
        private GmCommand _menuCmd;
        private GmGroup _menuGroup;

        // 数据
        private class GmCommand
        {
            public string Label;
            public string Cmd;
        }
        private class GmGroup
        {
            public string Name;
            public List<GmCommand> Commands = new();
        }
        private List<GmGroup> _groups = new();

        public override void _Ready()
        {
            BuildUI();
            AddDefaultGroups();

            var nm = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
            if (nm != null)
                nm.GmResponse += OnGmResponse;
        }

        public override void _ExitTree()
        {
            var nm = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
            if (nm != null)
                nm.GmResponse -= OnGmResponse;
        }

        public override void _Input(InputEvent @event)
        {
            // 快捷键
            if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.F2)
            {
                Toggle();
                GetViewport().SetInputAsHandled();
                return;
            }

            // 输入隔离：鼠标在面板上时消费事件，防止穿透到游戏世界
            if (_isVisible && @event is InputEventMouseButton)
            {
                var hovered = GetViewport().GuiGetHoveredControl();
                if (hovered != null && _panel != null && (_panel == hovered || _panel.IsAncestorOf(hovered)))
                    GetViewport().SetInputAsHandled();
            }
        }

        private void BuildUI()
        {
            var vpSize = GetViewport().GetVisibleRect().Size;
            var pw = 420f;
            var ph = 480f;

            _panel = new Panel();
            _panel.Position = new Vector2(vpSize.X - pw - 10, 60);
            _panel.Size = new Vector2(pw, ph);

            _vbox = new VBoxContainer();
            _vbox.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _vbox.OffsetLeft = 8;
            _vbox.OffsetTop = 8;
            _vbox.OffsetRight = -8;
            _vbox.OffsetBottom = -8;

            // ── 标题 ──
            var title = new Label
            {
                Text = "GM 调试面板 (F2)",
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            title.AddThemeFontSizeOverride("font_size", 14);
            _vbox.AddChild(title);

            // ── 命令输入行 ──
            var cmdBox = new HBoxContainer();
            _cmdEdit = new LineEdit
            {
                PlaceholderText = "例: additem,1001,20",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 32),
            };
            _cmdEdit.TextSubmitted += (_) => OnExecPressed();
            cmdBox.AddChild(_cmdEdit);

            var execBtn = new Button
            {
                Text = "执行",
                CustomMinimumSize = new Vector2(60, 32),
            };
            execBtn.Pressed += OnExecPressed;
            cmdBox.AddChild(execBtn);
            _vbox.AddChild(cmdBox);

            // ── 分组滚动区 ──
            _groupScroll = new ScrollContainer
            {
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 80),
                HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            };
            _groupContainer = new VBoxContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            _groupScroll.AddChild(_groupContainer);
            _vbox.AddChild(_groupScroll);

            // ── 添加/编辑命令（内联，默认隐藏）──
            _addCmdRow = new HBoxContainer();
            _addCmdRow.Visible = false;
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

            var addCmdOk = new Button { Text = "确定", CustomMinimumSize = new Vector2(44, 28) };
            addCmdOk.Pressed += ConfirmAddCommand;
            _addCmdRow.AddChild(addCmdOk);

            var addCmdCancel = new Button { Text = "取消", CustomMinimumSize = new Vector2(44, 28) };
            addCmdCancel.Pressed += CancelAddCommand;
            _addCmdRow.AddChild(addCmdCancel);
            _vbox.AddChild(_addCmdRow);

            // ── 添加分组（内联，默认隐藏）──
            _addGroupRow = new HBoxContainer();
            _addGroupRow.Visible = false;
            _addGroupRow.AddThemeConstantOverride("separation", 4);

            _addGroupEdit = new LineEdit
            {
                PlaceholderText = "分组名称",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 28),
            };
            _addGroupEdit.TextSubmitted += (_) => ConfirmAddGroup();
            _addGroupRow.AddChild(_addGroupEdit);

            var addGroupOk = new Button { Text = "确定", CustomMinimumSize = new Vector2(44, 28) };
            addGroupOk.Pressed += ConfirmAddGroup;
            _addGroupRow.AddChild(addGroupOk);

            var addGroupCancel = new Button { Text = "取消", CustomMinimumSize = new Vector2(44, 28) };
            addGroupCancel.Pressed += CancelAddGroup;
            _addGroupRow.AddChild(addGroupCancel);
            _vbox.AddChild(_addGroupRow);

            // ── 底部工具栏：添加分组 ──
            var toolbar = new HBoxContainer();
            toolbar.AddThemeConstantOverride("separation", 4);

            var addGroupBtn = new Button
            {
                Text = "+ 添加分组",
                CustomMinimumSize = new Vector2(0, 28),
            };
            addGroupBtn.Pressed += OnAddGroupPressed;
            toolbar.AddChild(addGroupBtn);
            _vbox.AddChild(toolbar);

            // ── 日志输出 ──
            _logOutput = new RichTextLabel
            {
                CustomMinimumSize = new Vector2(0, 80),
                BbcodeEnabled = true,
                ScrollActive = true,
                ScrollFollowing = true,
            };
            _vbox.AddChild(_logOutput);

            _panel.AddChild(_vbox);
            AddChild(_panel);
            _panel.Visible = false;

            // ── 右键菜单（全局，按需弹出）──
            _cmdMenu = new PopupMenu();
            _cmdMenu.AddItem("编辑", 0);
            _cmdMenu.AddItem("删除", 1);
            _cmdMenu.IdPressed += OnCmdMenuIdPressed;
            AddChild(_cmdMenu);
        }

        private void AddDefaultGroups()
        {
            var grpItem = AddGroupData("道具");
            AddCommandData(grpItem, "药水x1", "additem,1001,1");
            AddCommandData(grpItem, "药水x10", "additem,1001,10");
            AddCommandData(grpItem, "药水x99", "additem,1001,99");
            AddCommandData(grpItem, "矿石x10", "additem,1002,10");
            AddCommandData(grpItem, "矿石x100", "additem,1002,100");
            AddCommandData(grpItem, "金币袋x1", "additem,2001,1");

            var grpMove = AddGroupData("移动");
            AddCommandData(grpMove, "回出生点", "teleport,25,25");
            AddCommandData(grpMove, "宝箱1", "teleport,20,20");
            AddCommandData(grpMove, "宝箱2", "teleport,30,15");
            AddCommandData(grpMove, "宝箱3", "teleport,40,30");

            var grpChest = AddGroupData("宝箱");
            AddCommandData(grpChest, "添加宝箱1类(20,20)", "addchest,1,20,20");
            AddCommandData(grpChest, "添加宝箱2类(25,30)", "addchest,2,25,30");
            AddCommandData(grpChest, "添加宝箱3类(35,25)", "addchest,3,35,25");

            RebuildGroupUI();
        }

        // ── 数据操作 ──

        private GmGroup AddGroupData(string name)
        {
            var g = new GmGroup { Name = name };
            _groups.Add(g);
            return g;
        }

        private void AddCommandData(GmGroup group, string label, string cmd)
        {
            group.Commands.Add(new GmCommand { Label = label, Cmd = cmd });
        }

        // ── 内联编辑：添加分组 ──

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
            }
            _addGroupRow.Visible = false;
        }

        private void CancelAddGroup()
        {
            _addGroupRow.Visible = false;
        }

        // ── 内联编辑：添加/编辑命令 ──

        private void ShowAddCommandRow(GmGroup grp)
        {
            CancelAddGroup();
            _addCmdTarget = grp;
            _editCmdTarget = null;
            _addCmdRow.Visible = true;
            _addCmdLabelEdit.Text = "";
            _addCmdEdit.Text = "";
            _addCmdLabelEdit.PlaceholderText = $"标签 ({grp.Name})";
            _addCmdLabelEdit.GrabFocus();
        }

        private void ShowEditCommandRow(GmGroup grp, GmCommand cmd)
        {
            CancelAddGroup();
            _addCmdTarget = grp;
            _editCmdTarget = cmd;
            _addCmdRow.Visible = true;
            _addCmdLabelEdit.Text = cmd.Label;
            _addCmdEdit.Text = cmd.Cmd;
            _addCmdLabelEdit.GrabFocus();
        }

        private void ConfirmAddCommand()
        {
            if (_addCmdTarget == null) return;

            string label = _addCmdLabelEdit.Text.StripEdges();
            string cmd = _addCmdEdit.Text.StripEdges();
            if (label == "" || cmd == "") return;

            if (_editCmdTarget != null)
            {
                // 编辑模式：更新已有
                _editCmdTarget.Label = label;
                _editCmdTarget.Cmd = cmd;
            }
            else
            {
                // 新增模式
                AddCommandData(_addCmdTarget, label, cmd);
            }
            RebuildGroupUI();
            _addCmdRow.Visible = false;
            _addCmdTarget = null;
            _editCmdTarget = null;
        }

        private void CancelAddCommand()
        {
            _addCmdRow.Visible = false;
            _addCmdTarget = null;
            _editCmdTarget = null;
        }

        // ── 右键菜单 ──

        private void OnCmdMenuIdPressed(long id)
        {
            if (_menuCmd == null || _menuGroup == null) return;

            if (id == 0) // 编辑
            {
                ShowEditCommandRow(_menuGroup, _menuCmd);
            }
            else if (id == 1) // 删除
            {
                _menuGroup.Commands.Remove(_menuCmd);
                RebuildGroupUI();
            }
            _menuCmd = null;
            _menuGroup = null;
        }

        // ── UI 重建 ──

        private void RebuildGroupUI()
        {
            foreach (var child in _groupContainer.GetChildren())
                child.QueueFree();

            foreach (var grp in _groups)
            {
                // 分组标题行: 组名 | +添加命令 | x删除分组
                var header = new HBoxContainer();
                header.AddThemeConstantOverride("separation", 4);

                var nameLbl = new Label
                {
                    Text = grp.Name,
                    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                };
                nameLbl.AddThemeFontSizeOverride("font_size", 13);
                header.AddChild(nameLbl);

                var addCmdBtn = new Button
                {
                    Text = "+",
                    CustomMinimumSize = new Vector2(28, 24),
                    TooltipText = "添加命令",
                };
                GmGroup capturedGrp = grp;
                addCmdBtn.Pressed += () => ShowAddCommandRow(capturedGrp);
                header.AddChild(addCmdBtn);

                var delGroupBtn = new Button
                {
                    Text = "x",
                    CustomMinimumSize = new Vector2(28, 24),
                    TooltipText = "删除分组",
                };
                delGroupBtn.Pressed += () =>
                {
                    _groups.Remove(capturedGrp);
                    CancelAddCommand();
                    RebuildGroupUI();
                };
                header.AddChild(delGroupBtn);

                _groupContainer.AddChild(header);

                // 命令按钮流
                var flow = new HFlowContainer();
                flow.AddThemeConstantOverride("h_separation", 4);
                flow.AddThemeConstantOverride("v_separation", 4);

                foreach (var cmd in grp.Commands)
                {
                    GmCommand capturedCmd = cmd;
                    var btn = new Button
                    {
                        Text = cmd.Label,
                        CustomMinimumSize = new Vector2(0, 26),
                    };
                    // 左键：填入命令框
                    btn.Pressed += () =>
                    {
                        _cmdEdit.Text = capturedCmd.Cmd;
                        _cmdEdit.GrabFocus();
                    };
                    // 右键：弹出编辑/删除菜单
                    btn.GuiInput += (@event) =>
                    {
                        if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Right)
                        {
                            _menuCmd = capturedCmd;
                            _menuGroup = capturedGrp;
                            _cmdMenu.Position = (Vector2I)btn.GlobalPosition + new Vector2I(0, (int)btn.Size.Y);
                            _cmdMenu.ResetSize();
                            _cmdMenu.Popup();
                        }
                    };
                    flow.AddChild(btn);
                }
                _groupContainer.AddChild(flow);
            }
        }

        // ── 开关 / 执行 / 回调 ──

        private void Toggle()
        {
            _isVisible = !_isVisible;
            _panel.Visible = _isVisible;
            if (_isVisible)
                _cmdEdit.GrabFocus();
        }

        private void OnExecPressed()
        {
            string cmdLine = _cmdEdit.Text.StripEdges();
            if (cmdLine == "") return;

            var nm = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
            if (nm == null || !nm.IsServerConnected())
            {
                AppendLog("[color=red]未连接服务器[/color]");
                return;
            }

            var req = new Game.GmCommandRequest { Command = cmdLine, Args = "" };
            nm.SendPacket(MessageId.GameGmReq, req);

            AppendLog($"[color=cyan]> {cmdLine}[/color]");
            _cmdEdit.Text = "";
        }

        private void OnGmResponse(Game.GmCommandResponse rsp)
        {
            string color = rsp.Code == Common.ErrorCode.Success ? "green" : "red";
            AppendLog($"[color={color}]{rsp.Message}[/color]");

            if (rsp.Code == Common.ErrorCode.Success)
            {
                // 处理瞬移响应
                if (rsp.Message.StartsWith("TELEPORT:"))
                {
                    var parts = rsp.Message.Split(':');
                    if (parts.Length == 3 && int.TryParse(parts[1], out int tx) && int.TryParse(parts[2], out int ty))
                    {
                        var player = GetTree()?.GetFirstNodeInGroup("player") as Player;
                        if (player != null)
                        {
                            player.GridPos = new Vector2I(tx, ty);
                            player.Position = new Vector2(tx * player.GridSize + player.GridSize / 2.0f,
                                                           ty * player.GridSize + player.GridSize / 2.0f);
                            AppendLog($"[color=cyan]已瞬移到 ({tx}, {ty})[/color]");
                        }
                    }
                }

                // 处理背包更新
                if (rsp.Items.Count > 0)
                {
                    var inv = GetTree()?.GetFirstNodeInGroup("inventory_manager") as InventoryManager;
                    if (inv != null)
                        inv.UpdateFromProto(rsp.Items);
                }
            }
        }

        private void AppendLog(string bbcode)
        {
            _logOutput.AppendText(bbcode + "\n");
        }
    }
}
