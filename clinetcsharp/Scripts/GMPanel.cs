using Godot;
using Protocol;
using System.Collections.Generic;

namespace ClinetCSharp
{
    /// <summary>
    /// GM 调试面板 — 继承 DraggablePanel，F2 切换。
    /// 支持动态增删改分组和命令（内联编辑 + 右键菜单）。
    /// </summary>
    public partial class GMPanel : DraggablePanel
    {
        private NetworkManager _network;

        // UI refs
        private VBoxContainer _content;
        private LineEdit _cmdEdit;
        private ScrollContainer _groupScroll;
        private VBoxContainer _groupContainer;

        // 内联编辑：添加分组
        private HBoxContainer _addGroupRow;
        private LineEdit _addGroupEdit;

        // 内联编辑：添加/编辑命令
        private HBoxContainer _addCmdRow;
        private LineEdit _addCmdLabelEdit;
        private LineEdit _addCmdEdit;
        private GmGroup _addCmdTarget;
        private GmCommand _editCmdTarget;

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
            public bool Collapsed; // 折叠状态
        }
        private readonly List<GmGroup> _groups = new();

        protected override void OnPanelInitialized()
        {
            SetToggleKey(Key.F2);
            MinHeight = 300;

            _content = GetNodeOrNull<VBoxContainer>("VBoxContainer/Content");
            if (_content == null) return;

            BuildContent();
            AddDefaultGroups();

            _network = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
            if (_network != null)
                _network.GmResponse += OnGmResponse;
        }

        public override void _ExitTree()
        {
            if (_network != null)
                _network.GmResponse -= OnGmResponse;
            base._ExitTree();
        }

        protected override void OnClosed() => Visible = false;

        protected internal override void NotifyFocusGained()
        {
            if (_cmdEdit != null)
                _cmdEdit.GrabFocus();
        }

        private bool _debugLogged;

        public override void _Process(double delta)
        {
            base._Process(delta);
        }

        private void BuildContent()
        {
            _content.AddThemeConstantOverride("separation", 4);

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
            _content.AddChild(cmdBox);

            // ── 分组滚动区 ──
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

            // ── 添加/编辑命令（内联，默认隐藏）──
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

            var addCmdOk = new Button { Text = "确定", CustomMinimumSize = new Vector2(44, 28) };
            addCmdOk.Pressed += ConfirmAddCommand;
            _addCmdRow.AddChild(addCmdOk);

            var addCmdCancel = new Button { Text = "取消", CustomMinimumSize = new Vector2(44, 28) };
            addCmdCancel.Pressed += CancelAddCommand;
            _addCmdRow.AddChild(addCmdCancel);
            _content.AddChild(_addCmdRow);

            // ── 添加分组（内联，默认隐藏）──
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

            var addGroupOk = new Button { Text = "确定", CustomMinimumSize = new Vector2(44, 28) };
            addGroupOk.Pressed += ConfirmAddGroup;
            _addGroupRow.AddChild(addGroupOk);

            var addGroupCancel = new Button { Text = "取消", CustomMinimumSize = new Vector2(44, 28) };
            addGroupCancel.Pressed += CancelAddGroup;
            _addGroupRow.AddChild(addGroupCancel);
            _content.AddChild(_addGroupRow);

            // ── 底部工具栏 ──
            var toolbar = new HBoxContainer();
            toolbar.AddThemeConstantOverride("separation", 4);
            var addGroupBtn = new Button
            {
                Text = "+ 添加分组",
                CustomMinimumSize = new Vector2(0, 28),
            };
            addGroupBtn.Pressed += OnAddGroupPressed;
            toolbar.AddChild(addGroupBtn);
            _content.AddChild(toolbar);

            // ── 右键菜单 ──
            _cmdMenu = new PopupMenu();
            _cmdMenu.AddItem("编辑", 0);
            _cmdMenu.AddItem("删除", 1);
            _cmdMenu.IdPressed += OnCmdMenuIdPressed;
            AddChild(_cmdMenu);

            UiUtils.ConfigureTransientDragControlFocus(this);
        }

        // ============ Default Groups ============

        private void AddDefaultGroups()
        {
            var grpItem = AddGroupData("道具");
            // 第一个分组默认展开，其余默认折叠
            grpItem.Collapsed = false;
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

            var grpSkill = AddGroupData("技能");
            AddCommandData(grpSkill, "学习烈斩(2)", "learnskill,2");
            AddCommandData(grpSkill, "学习盾击(3)", "learnskill,3");
            AddCommandData(grpSkill, "学习旋风斩(4)", "learnskill,4");

            var grpBuff = AddGroupData("Buff");
            AddCommandData(grpBuff, "中毒(1)", "addbuff,1");
            AddCommandData(grpBuff, "冰冻(2)", "addbuff,2");
            AddCommandData(grpBuff, "战吼(3)", "addbuff,3");
            AddCommandData(grpBuff, "护盾(4)", "addbuff,4");
            AddCommandData(grpBuff, "石肤(5)", "addbuff,5");
            AddCommandData(grpBuff, "减速(6)", "addbuff,6");
            AddCommandData(grpBuff, "灼烧(7)", "addbuff,7");
            AddCommandData(grpBuff, "祝福(8)", "addbuff,8");
            AddCommandData(grpBuff, "破甲(9)", "addbuff,9");
            AddCommandData(grpBuff, "眩晕(10)", "addbuff,10");
            AddCommandData(grpBuff, "狂暴(11)", "addbuff,11");
            AddCommandData(grpBuff, "移除中毒", "removebuff,1");
            AddCommandData(grpBuff, "移除冰冻", "removebuff,2");
            AddCommandData(grpBuff, "移除战吼", "removebuff,3");
            AddCommandData(grpBuff, "移除护盾", "removebuff,4");

            RebuildGroupUI();
        }

        // ============ Data Operations ============

        private GmGroup AddGroupData(string name)
        {
            var g = new GmGroup { Name = name, Collapsed = true };
            _groups.Add(g);
            return g;
        }

        private void AddCommandData(GmGroup group, string label, string cmd)
        {
            group.Commands.Add(new GmCommand { Label = label, Cmd = cmd });
        }

        // ============ Inline Edit: Add Group ============

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

        // ============ Inline Edit: Add/Edit Command ============

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
                _editCmdTarget.Label = label;
                _editCmdTarget.Cmd = cmd;
            }
            else
            {
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

        // ============ Context Menu ============

        private void OnCmdMenuIdPressed(long id)
        {
            if (_menuCmd == null || _menuGroup == null) return;

            if (id == 0)
                ShowEditCommandRow(_menuGroup, _menuCmd);
            else if (id == 1)
            {
                _menuGroup.Commands.Remove(_menuCmd);
                RebuildGroupUI();
            }
            _menuCmd = null;
            _menuGroup = null;
        }

        // ============ UI Rebuild ============

        private void RebuildGroupUI()
        {
            foreach (var child in _groupContainer.GetChildren())
                child.QueueFree();

            foreach (var grp in _groups)
            {
                GmGroup capturedGrp = grp;

                // 分组标题行
                var header = new HBoxContainer();
                header.AddThemeConstantOverride("separation", 4);

                // 折叠/展开按钮
                var collapseBtn = new Button
                {
                    Text = grp.Collapsed ? "▶" : "▼",
                    CustomMinimumSize = new Vector2(24, 24),
                    TooltipText = grp.Collapsed ? "展开" : "折叠",
                };
                collapseBtn.Pressed += () =>
                {
                    capturedGrp.Collapsed = !capturedGrp.Collapsed;
                    RebuildGroupUI();
                };
                header.AddChild(collapseBtn);

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

                // 命令按钮流（折叠时隐藏）
                var flow = new HFlowContainer
                {
                    Visible = !grp.Collapsed,
                };
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
                    btn.Pressed += () =>
                    {
                        _cmdEdit.Text = capturedCmd.Cmd;
                        _cmdEdit.GrabFocus();
                    };
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

        // ============ Execute / Callbacks ============

        private void OnExecPressed()
        {
            string cmdLine = _cmdEdit.Text.StripEdges();
            if (cmdLine == "") return;

            if (_network == null || !_network.IsServerConnected())
            {
                AppendLog("[color=red]未连接服务器[/color]");
                return;
            }

            var req = new Game.GmCommandRequest { Command = cmdLine, Args = "" };
            _network.SendPacket(MessageId.GameGmReq, req);
            AppendLog($"[color=cyan]> {cmdLine}[/color]");
            _cmdEdit.Text = "";
        }

        private void OnGmResponse(Game.GmCommandResponse rsp)
        {
            string color = rsp.Code == Common.ErrorCode.Success ? "green" : "red";
            AppendLog($"[color={color}]{rsp.Message}[/color]");

            if (rsp.Code != Common.ErrorCode.Success) return;

            // 处理瞬移响应
            if (rsp.Message.StartsWith("TELEPORT:"))
            {
                var parts = rsp.Message.Split(':');
                if (parts.Length == 3 && int.TryParse(parts[1], out int tx) && int.TryParse(parts[2], out int ty))
                {
                    var player = GetTree()?.GetFirstNodeInGroup("player") as Player;
                    if (player != null)
                    {
                        player.TeleportToGrid(tx, ty);
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

        private void AppendLog(string bbcode)
        {
            // 日志区已移除，输出到控制台
            GD.Print($"[GM] {bbcode}");
        }
    }
}
