using Godot;
using Godot.Collections;
using Protocol;

namespace ClinetCSharp
{
    /// <summary>
    /// 角色选择场景
    /// </summary>
    public partial class RoleSelectScene : Control
    {
        private Label _serverLabel;
        private HBoxContainer _roleContainer;
        private Label _countLabel;
        private Button _createButton;
        private Button _enterButton;

        private Panel _createDialog;
        private LineEdit _nameEdit;
        private Button _confirmCreateButton;
        private Button _cancelButton;

        private long _selectedRoleId = 0;
        private Godot.Collections.Dictionary<long, Button> _roleCards = new();

        public override void _Ready()
        {
            _serverLabel = GetNode<Label>("CenterContainer/Panel/VBoxContainer/ServerLabel");
            _roleContainer = GetNode<HBoxContainer>("CenterContainer/Panel/VBoxContainer/RoleContainer");
            _countLabel = GetNode<Label>("CenterContainer/Panel/VBoxContainer/CountLabel");
            _createButton = GetNode<Button>("CenterContainer/Panel/VBoxContainer/HBoxContainer/CreateButton");
            _enterButton = GetNode<Button>("CenterContainer/Panel/VBoxContainer/HBoxContainer/EnterButton");
            _createDialog = GetNode<Panel>("CenterContainer/Panel/CreateRoleDialog");
            _nameEdit = GetNode<LineEdit>("CenterContainer/Panel/CreateRoleDialog/VBoxContainer/NameEdit");
            _confirmCreateButton = GetNode<Button>("CenterContainer/Panel/CreateRoleDialog/VBoxContainer/HBoxContainer/ConfirmCreateButton");
            _cancelButton = GetNode<Button>("CenterContainer/Panel/CreateRoleDialog/VBoxContainer/HBoxContainer/CancelButton");

            if (_serverLabel == null || _roleContainer == null || _countLabel == null || _createButton == null || _enterButton == null)
            {
                GD.PushError("[RoleSelectScene] Failed to get UI nodes");
                return;
            }

            _createButton.Pressed += ShowCreateDialog;
            _enterButton.Pressed += OnEnterGame;
            _confirmCreateButton.Pressed += OnCreateRole;
            _cancelButton.Pressed += HideCreateDialog;
            _nameEdit.TextSubmitted += _ => OnCreateRole();

            var nm = GetNode<NetworkManager>("/root/NetworkManager");
            nm.PacketReceived += OnPacketReceived;

            _serverLabel.Text = $"当前区服: {nm.LastServerId}服";
            LoadRoles();
        }

        private void LoadRoles()
        {
            foreach (Node child in _roleContainer.GetChildren())
                child.QueueFree();
            _roleCards.Clear();
            _selectedRoleId = 0;
            _enterButton.Disabled = true;

            var nm = GetNode<NetworkManager>("/root/NetworkManager");
            int roleCount = nm.Roles.Count;
            uint maxCount = nm.MaxRoleCount;
            _countLabel.Text = $"角色数量: {roleCount}/{maxCount}";

            if (roleCount == 0)
            {
                var label = new Label();
                label.Text = "暂无角色，请创建新角色";
                label.HorizontalAlignment = HorizontalAlignment.Center;
                label.VerticalAlignment = VerticalAlignment.Center;
                _roleContainer.AddChild(label);
                _createButton.Disabled = false;
                _enterButton.Disabled = true;
                return;
            }

            foreach (var role in nm.Roles)
            {
                var roleDict = role.AsGodotDictionary();
                if (roleDict != null)
                {
                    var card = CreateRoleCard(roleDict);
                    _roleContainer.AddChild(card);
                }
            }

            _createButton.Disabled = roleCount >= maxCount;
        }

        private Button CreateRoleCard(Dictionary role)
        {
            long roleId = role.GetValueOrDefault("roleId", Variant.From(0L)).AsInt64();
            string roleName = role.GetValueOrDefault("roleName", Variant.From("未知")).AsString();
            int level = role.GetValueOrDefault("level", Variant.From(1)).AsInt32();
            long totalPower = role.GetValueOrDefault("totalPower", Variant.From(0L)).AsInt64();

            var button = new Button();
            button.CustomMinimumSize = new Vector2(120, 160);
            button.MouseDefaultCursorShape = CursorShape.Arrow;
            button.FocusMode = FocusModeEnum.None;

            var vbox = new VBoxContainer();
            vbox.Alignment = BoxContainer.AlignmentMode.Center;
            vbox.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            vbox.OffsetLeft = 5;
            vbox.OffsetTop = 5;
            vbox.OffsetRight = -5;
            vbox.OffsetBottom = -5;
            button.AddChild(vbox);

            var avatar = new ColorRect();
            avatar.CustomMinimumSize = new Vector2(80, 80);
            avatar.Color = new Color(0.3f, 0.5f, 0.7f);
            avatar.MouseFilter = MouseFilterEnum.Ignore;
            vbox.AddChild(avatar);

            var nameLabel = new Label();
            nameLabel.Text = roleName;
            nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
            nameLabel.AddThemeFontSizeOverride("font_size", 14);
            nameLabel.MouseFilter = MouseFilterEnum.Ignore;
            vbox.AddChild(nameLabel);

            var levelLabel = new Label();
            levelLabel.Text = $"Lv.{level}";
            levelLabel.HorizontalAlignment = HorizontalAlignment.Center;
            levelLabel.AddThemeFontSizeOverride("font_size", 12);
            levelLabel.MouseFilter = MouseFilterEnum.Ignore;
            vbox.AddChild(levelLabel);

            var powerLabel = new Label();
            powerLabel.Text = $"战力:{FormatPower(totalPower)}";
            powerLabel.HorizontalAlignment = HorizontalAlignment.Center;
            powerLabel.AddThemeFontSizeOverride("font_size", 11);
            powerLabel.MouseFilter = MouseFilterEnum.Ignore;
            vbox.AddChild(powerLabel);

            long capturedId = roleId;
            Button capturedButton = button;
            button.Pressed += () => SelectRole(capturedId, capturedButton);

            _roleCards[roleId] = button;
            return button;
        }

        private string FormatPower(long power)
        {
            if (power >= 10000)
                return $"{power / 10000}万";
            return power.ToString();
        }

        private void SelectRole(long roleId, Button button)
        {
            _selectedRoleId = roleId;
            _enterButton.Disabled = false;

            foreach (var child in _roleContainer.GetChildren())
            {
                if (child is Button btn)
                    btn.Modulate = btn == button
                        ? new Color(1.4f, 1.4f, 1.4f)
                        : new Color(0.8f, 0.8f, 0.8f);
            }
        }

        private void ShowCreateDialog()
        {
            _createDialog.Visible = true;
            _nameEdit.Text = "";
            _nameEdit.GrabFocus();
        }

        private void HideCreateDialog()
        {
            _createDialog.Visible = false;
        }

        private void OnCreateRole()
        {
            var name = _nameEdit.Text.StripEdges();
            GD.Print($"[RoleSelectScene] OnCreateRole called, name='{name}' len={name.Length}");
            if (name.Length < 2)
            {
                GD.Print("[RoleSelectScene] Name too short, skipping send");
                return;
            }

            var nm = GetNode<NetworkManager>("/root/NetworkManager");
            var req = new Game.CreateRoleRequest
            {
                RoleName = name,
            };

            GD.Print($"[RoleSelectScene] Sending create role request: {name}");
            bool sent = nm.SendPacket(MessageId.GameCreateRoleReq, req);

            if (!sent)
            {
                _confirmCreateButton.Disabled = false;
                _nameEdit.PlaceholderText = "网络错误，请重试";
            }
            else
            {
                _confirmCreateButton.Disabled = true;
                _ = RestoreButtonAfterDelay();
            }
        }

        private async System.Threading.Tasks.Task RestoreButtonAfterDelay()
        {
            await System.Threading.Tasks.Task.Delay(3000);
            CallDeferred(nameof(RestoreButton));
        }

        private void RestoreButton()
        {
            if (IsInstanceValid(_confirmCreateButton) && _confirmCreateButton.Disabled)
                _confirmCreateButton.Disabled = false;
        }

        private void OnEnterGame()
        {
            if (_selectedRoleId == 0)
                return;

            _enterButton.Disabled = true;

            var nm = GetNode<NetworkManager>("/root/NetworkManager");
            var req = new Game.EnterGameRequest
            {
                RoleId = (ulong)_selectedRoleId,
            };
            nm.SendPacket(MessageId.GameEnterGameReq, req);
        }

        private void OnPacketReceived(int msgId)
        {
            var nm = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
            var payload = nm?.GetLastPayload() ?? new byte[0];
            switch ((MessageId)msgId)
            {
                case MessageId.GameEnterGameRsp:
                    _enterButton.Disabled = false;
                    try
                    {
                        var rsp = Game.EnterGameResponse.Parser.ParseFrom(payload);
                        if (rsp.Code == Common.ErrorCode.Success)
                        {
                            GetTree().ChangeSceneToFile("res://scenes/main.tscn");
                        }
                        else
                        {
                            GD.PushError("进入游戏失败: " + rsp.Message);
                        }
                    }
                    catch (System.Exception e)
                    {
                        GD.PushError($"[RoleSelectScene] Parse EnterGameRsp failed: {e.Message}");
                    }
                    break;

                case MessageId.GameCreateRoleRsp:
                    _confirmCreateButton.Disabled = false;
                    try
                    {
                        var rsp = Game.CreateRoleResponse.Parser.ParseFrom(payload);
                        if (rsp.Code == Common.ErrorCode.Success)
                        {
                            var netMgr = GetNode<NetworkManager>("/root/NetworkManager");
                            // 把新角色加到缓存
                            netMgr.Roles.Add(new Godot.Collections.Dictionary
                            {
                                ["roleId"] = (long)rsp.RoleInfo.RoleId,
                                ["roleName"] = rsp.RoleInfo.RoleName,
                                ["level"] = (int)rsp.RoleInfo.Level,
                                ["avatarId"] = (int)rsp.RoleInfo.AvatarId,
                                ["totalPower"] = (long)rsp.RoleInfo.TotalPower,
                            });
                            HideCreateDialog();
                            LoadRoles();

                            long newRoleId = (long)rsp.RoleInfo.RoleId;
                            if (newRoleId > 0 && _roleCards.ContainsKey(newRoleId))
                                SelectRole(newRoleId, _roleCards[newRoleId]);
                        }
                        else
                        {
                            _nameEdit.PlaceholderText = rsp.Message;
                            _nameEdit.Text = "";
                        }
                    }
                    catch (System.Exception e)
                    {
                        GD.PushError($"[RoleSelectScene] Parse CreateRoleRsp failed: {e.Message}");
                    }
                    break;
            }
        }

        public override void _ExitTree()
        {
            var nm = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
            if (nm != null)
                nm.PacketReceived -= OnPacketReceived;
        }
    }
}
