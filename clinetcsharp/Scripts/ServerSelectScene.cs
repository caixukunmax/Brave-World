using Godot;
using Godot.Collections;
using Protocol;

namespace ClinetCSharp
{
    /// <summary>
    /// 服务器选择场景
    /// </summary>
    public partial class ServerSelectScene : Control
    {
        private Label _accountLabel;
        private VBoxContainer _serverList;
        private Button _refreshButton;
        private Label _selectedLabel;
        private Button _confirmButton;

        private int _selectedServerId = 0;
        private Godot.Collections.Dictionary<int, Button> _serverButtons = new();
        private string _selectedServerName = "";

        public override void _Ready()
        {
            _accountLabel = GetNode<Label>("CenterContainer/Panel/VBoxContainer/AccountLabel");
            _serverList = GetNode<VBoxContainer>("CenterContainer/Panel/VBoxContainer/ServerList");
            _refreshButton = GetNode<Button>("CenterContainer/Panel/VBoxContainer/HBoxContainer/RefreshButton");
            _selectedLabel = GetNode<Label>("CenterContainer/Panel/VBoxContainer/SelectedLabel");
            _confirmButton = GetNode<Button>("CenterContainer/Panel/VBoxContainer/HBoxContainer/ConfirmButton");

            if (_accountLabel == null || _serverList == null || _refreshButton == null || _selectedLabel == null || _confirmButton == null)
            {
                GD.PushError("[ServerSelectScene] Failed to get UI nodes");
                return;
            }

            _refreshButton.Pressed += LoadServerList;
            _confirmButton.Pressed += OnConfirmPressed;

            var nm = GetNode<NetworkManager>("/root/NetworkManager");
            nm.PacketReceived += OnPacketReceived;

            _accountLabel.Text = $"账号ID: {nm.AccountId}";
            LoadServerList();
        }

        private void LoadServerList()
        {
            _selectedLabel.Text = "请选择区服";
            _confirmButton.Disabled = true;
            _selectedServerId = 0;
            _selectedServerName = "";

            foreach (Node child in _serverList.GetChildren())
                child.QueueFree();
            _serverButtons.Clear();

            var nm = GetNode<NetworkManager>("/root/NetworkManager");

            if (nm.Servers.Count == 0)
            {
                var label = new Label();
                label.Text = "暂无可用区服";
                label.HorizontalAlignment = HorizontalAlignment.Center;
                _serverList.AddChild(label);
                return;
            }

            foreach (var server in nm.Servers)
            {
                var serverDict = server.AsGodotDictionary();
                if (serverDict == null) continue;

                var btn = new Button();
                int serverId = serverDict.GetValueOrDefault("serverId", Variant.From(0)).AsInt32();
                string serverName = serverDict.GetValueOrDefault("serverName", Variant.From("未知区服")).AsString();
                int status = serverDict.GetValueOrDefault("status", Variant.From(0)).AsInt32();
                bool isRecommend = serverDict.GetValueOrDefault("isRecommend", Variant.From(false)).AsBool();
                bool isNew = serverDict.GetValueOrDefault("isNew", Variant.From(false)).AsBool();

                var tags = new System.Collections.Generic.List<string>();
                if (isNew) tags.Add("新服");
                if (isRecommend) tags.Add("推荐");
                if (status == 0) tags.Add("维护中");

                var tagStr = tags.Count > 0 ? $" [{string.Join(",", tags)}]" : "";
                btn.Text = $"{serverId}. {serverName}{tagStr}";
                btn.Alignment = HorizontalAlignment.Left;
                btn.Disabled = status == 0;

                int capturedId = serverId;
                string capturedName = serverName;
                btn.Pressed += () => OnServerSelected(capturedId, capturedName);

                _serverButtons[serverId] = btn;
                _serverList.AddChild(btn);
            }

            if (nm.LastServerId > 0)
            {
                foreach (var server in nm.Servers)
                {
                    var serverDict = server.AsGodotDictionary();
                    if (serverDict == null) continue;
                    if (serverDict.GetValueOrDefault("serverId", Variant.From(0)).AsInt32() == (int)nm.LastServerId)
                    {
                        OnServerSelected((int)nm.LastServerId, serverDict.GetValueOrDefault("serverName", Variant.From("")).AsString());
                        break;
                    }
                }
            }
        }

        private void OnServerSelected(int serverId, string serverName)
        {
            _selectedServerId = serverId;
            _selectedServerName = serverName;

            foreach (var kvp in _serverButtons)
            {
                var btn = kvp.Value;
                btn.Modulate = kvp.Key == serverId
                    ? new Color(0.7f, 1.0f, 0.7f)
                    : new Color(1.0f, 1.0f, 1.0f);
            }

            _selectedLabel.Text = $"已选择: {serverId}服 {serverName}";
            _confirmButton.Disabled = false;
        }

        private void OnConfirmPressed()
        {
            if (_selectedServerId == 0)
            {
                _selectedLabel.Text = "请先选择区服";
                return;
            }

            _selectedLabel.Text = "正在连接区服...";
            _confirmButton.Disabled = true;
            _refreshButton.Disabled = true;

            var nm = GetNode<NetworkManager>("/root/NetworkManager");
            var req = new Login.SelectServerRequest
            {
                AccountToken = nm.AccountToken,
                ServerId = (uint)_selectedServerId
            };
            nm.SendPacket(MessageId.LoginSelectServerReq, req);
        }

        private void OnPacketReceived(int msgId)
        {
            if ((MessageId)msgId != MessageId.LoginSelectServerRsp)
                return;

            _confirmButton.Disabled = false;
            _refreshButton.Disabled = false;

            var nm = GetNode<NetworkManager>("/root/NetworkManager");
            var payload = nm.GetLastPayload() ?? new byte[0];
            try
            {
                var rsp = Login.SelectServerResponse.Parser.ParseFrom(payload);

                if (rsp.Code == Common.ErrorCode.Success)
                {
                    _selectedLabel.Text = "选服成功，正在进入游戏...";
                    GetTree().ChangeSceneToFile("res://scenes/role_select_scene.tscn");
                }
                else
                {
                    _selectedLabel.Text = "选服失败: " + rsp.Message;
                }
            }
            catch (System.Exception e)
            {
                GD.PushError($"[ServerSelectScene] Parse response failed: {e.Message}");
                _selectedLabel.Text = "解析服务器响应失败";
            }
        }
    }
}
