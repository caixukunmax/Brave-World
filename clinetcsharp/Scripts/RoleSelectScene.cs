using Godot;
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
        private NetworkManager _network;

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

            _network = UiServices.GetNetworkManager(this);
            _network.EnterGameResponse += OnEnterGameResponse;
            _network.CreateRoleResponse += OnCreateRoleResponse;

            _serverLabel.Text = $"当前区服: {_network.LastServerId}服";
            LoadRoles();
        }

        private void LoadRoles()
        {
            _roleContainer.ClearChildren();
            _roleCards.Clear();
            _selectedRoleId = 0;
            _enterButton.Disabled = true;

            int roleCount = _network.Roles.Count;
            uint maxCount = _network.MaxRoleCount;
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

            foreach (var role in _network.Roles)
            {
                var card = CreateRoleCard(role);
                _roleContainer.AddChild(card);
            }

            _createButton.Disabled = roleCount >= maxCount;
        }

        private Button CreateRoleCard(Login.RoleBrief role)
        {
            long roleId = (long)role.RoleId;
            string roleName = role.RoleName;
            int level = (int)role.Level;
            long totalPower = (long)role.TotalPower;

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
            if (name.Length < 2)
                return;

            var req = new Game.CreateRoleRequest { RoleName = name };
            bool sent = _network.SendPacket(MessageId.GameCreateRoleReq, req);

            if (!sent)
            {
                _confirmCreateButton.Disabled = false;
                _nameEdit.PlaceholderText = "网络错误，请重试";
            }
            else
            {
                _confirmCreateButton.Disabled = true;
                var timer = GetTree().CreateTimer(3.0);
                timer.Timeout += RestoreButton;
            }
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

            var req = new Game.EnterGameRequest
            {
                RoleId = (ulong)_selectedRoleId,
            };
            _network.SendPacket(MessageId.GameEnterGameReq, req);
        }

        private void OnEnterGameResponse(Game.EnterGameResponse rsp)
        {
            _enterButton.Disabled = false;
            if (rsp.Code == Common.ErrorCode.Success)
            {
                GetTree().ChangeSceneToFile("res://scenes/main.tscn");
            }
            else
            {
                GD.PushError("进入游戏失败: " + rsp.Message);
            }
        }

        private void OnCreateRoleResponse(Game.CreateRoleResponse rsp)
        {
            _confirmCreateButton.Disabled = false;
            if (rsp.Code == Common.ErrorCode.Success)
            {
                _network.Roles.Add(new Login.RoleBrief
                {
                    RoleId = rsp.RoleInfo.RoleId,
                    RoleName = rsp.RoleInfo.RoleName,
                    Level = rsp.RoleInfo.Level,
                    AvatarId = rsp.RoleInfo.AvatarId,
                    TotalPower = rsp.RoleInfo.TotalPower,
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

        public override void _ExitTree()
        {
            if (_network != null)
            {
                _network.EnterGameResponse -= OnEnterGameResponse;
                _network.CreateRoleResponse -= OnCreateRoleResponse;
            }
        }
    }
}
