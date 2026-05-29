using Godot;
using Protocol;

namespace ClinetCSharp
{
    /// <summary>
    /// 登录场景
    /// </summary>
    public partial class LoginScene : Control
    {
        private LineEdit _usernameEdit;
        private LineEdit _passwordEdit;
        private Button _loginButton;
        private Button _testButton;
        private Label _statusLabel;

        private const string SAVE_FILE = "user://login_data.cfg";
        private string _savedUsername = "";
        private NetworkManager _network;

        // 测试直通流程状态
        private enum TestFlowState { None, Login, SelectServer, EnterGame, CreateRole }
        private TestFlowState _testFlowState = TestFlowState.None;
        private bool _pendingTestDirect; // 重连成功后自动触发 Test Direct
        private bool _autoTestMode;      // --test-grid-visibility 自动化测试模式

        public override void _Ready()
        {
            _usernameEdit = GetNode<LineEdit>("CenterContainer/Panel/VBoxContainer/UsernameEdit");
            _passwordEdit = GetNode<LineEdit>("CenterContainer/Panel/VBoxContainer/PasswordEdit");
            _loginButton = GetNode<Button>("CenterContainer/Panel/VBoxContainer/LoginButton");
            _testButton = GetNode<Button>("CenterContainer/Panel/VBoxContainer/TestButton");
            _statusLabel = GetNode<Label>("CenterContainer/Panel/VBoxContainer/StatusLabel");

            if (_loginButton == null || _usernameEdit == null || _passwordEdit == null || _statusLabel == null)
            {
                GD.PushError("[LoginScene] Failed to get UI nodes");
                return;
            }

            _loginButton.Pressed += OnLoginPressed;
            _usernameEdit.TextSubmitted += _ => OnLoginPressed();
            _passwordEdit.TextSubmitted += _ => OnLoginPressed();

            if (_testButton != null)
                _testButton.Pressed += OnTestDirectPressed;

            LoadSavedAccount();

            _network = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
            if (_network != null)
            {
                _network.Connected += OnConnected;
                _network.ConnectionError += OnConnectionError;
                _network.Kicked += OnKicked;
                _network.LoginResponse += OnLoginResponse;
                _network.SelectServerResponse += OnSelectServerResponse;
                _network.EnterGameResponse += OnEnterGameResponse;
                _network.CreateRoleResponse += OnCreateRoleResponse;

                _statusLabel.Text = "正在连接服务器...";
                _loginButton.Disabled = true;
                _network.ConnectToServer();
            }
            else
            {
                _statusLabel.Text = "网络管理器未初始化";
                GD.PushError("[LoginScene] NetworkManager is null");
            }

            // 自动化测试：检测到参数后标记，连接成功后自动触发 Test Direct
            foreach (var arg in OS.GetCmdlineArgs())
            {
                if (arg == "--test-grid-visibility")
                {
                    GD.Print("[LoginScene] Auto-test mode detected.");
                    _autoTestMode = true;
                    break;
                }
            }
        }

        private void OnConnected()
        {
            GD.Print("[LoginScene] Connected to server");
            _statusLabel.Text = "已连接到服务器";
            _loginButton.Disabled = false;

            // 自动化测试：连接成功后自动走 Test Direct
            if (_autoTestMode)
            {
                _autoTestMode = false;
                GD.Print("[LoginScene] Auto-test: triggering Test Direct...");
                OnTestDirectPressed();
                return;
            }

            // 如果重连前点了 Test Direct，连上后自动继续
            if (_pendingTestDirect)
            {
                _pendingTestDirect = false;
                OnTestDirectPressed();
            }
        }

        private void OnConnectionError(string error)
        {
            GD.Print("[LoginScene] Connection error: " + error);
            _statusLabel.Text = "连接失败: " + error;
            _loginButton.Disabled = false;
        }

        private void OnKicked(string reason)
        {
            GD.Print("[LoginScene] Kicked: " + reason);
            _statusLabel.Text = "被服务器踢下线: " + reason;
            _loginButton.Disabled = false;
        }

        private void OnLoginPressed()
        {
            var username = _usernameEdit.Text.StripEdges();
            var password = _passwordEdit.Text;

            if (string.IsNullOrEmpty(username))
            {
                _statusLabel.Text = "请输入用户名";
                return;
            }
            if (string.IsNullOrEmpty(password))
            {
                _statusLabel.Text = "请输入密码";
                return;
            }

            _statusLabel.Text = "登录中...";
            _loginButton.Disabled = true;

            var req = new Login.AccountLoginRequest
            {
                Username = username,
                Password = password,
                Platform = "pc",
                DeviceId = GetDeviceId(),
                ClientVersion = "1.0.0"
            };

            if (_network == null)
            {
                GD.PushError("[LoginScene] NetworkManager is null!");
                _statusLabel.Text = "网络错误";
                _loginButton.Disabled = false;
                return;
            }

            _network.SendPacket(MessageId.LoginAccountLoginReq, req);
        }

        private void OnLoginResponse(Login.AccountLoginResponse rsp)
        {
            // 测试直通流程
            if (_testFlowState != TestFlowState.None)
            {
                HandleTestFlowLogin(rsp);
                return;
            }

            // 正常登录流程
            _loginButton.Disabled = false;

            if (rsp.Code == Common.ErrorCode.Success)
            {
                _statusLabel.Text = "登录成功，正在跳转...";
                SaveAccount(_usernameEdit.Text.StripEdges());
                GetTree().ChangeSceneToFile("res://scenes/server_select_scene.tscn");
            }
            else
            {
                _statusLabel.Text = "登录失败: " + rsp.Message;
            }
        }

        private void OnSelectServerResponse(Login.SelectServerResponse rsp)
        {
            if (_testFlowState != TestFlowState.None)
            {
                HandleTestFlowSelectServer(rsp);
            }
        }

        private void OnEnterGameResponse(Game.EnterGameResponse rsp)
        {
            if (_testFlowState == TestFlowState.EnterGame)
            {
                if (rsp.Code != Common.ErrorCode.Success)
                {
                    _statusLabel.Text = $"[测试] 进入游戏失败: {rsp.Message}";
                    ResetTestFlow();
                    return;
                }
                GD.Print($"[TestFlow] EnterGame OK, name={rsp.RoleInfo.RoleName}");
                _statusLabel.Text = "[测试] 进入游戏成功!";
                GetTree().ChangeSceneToFile("res://scenes/main.tscn");
            }
        }

        private void OnCreateRoleResponse(Game.CreateRoleResponse rsp)
        {
            if (_testFlowState == TestFlowState.CreateRole)
            {
                if (rsp.Code != Common.ErrorCode.Success)
                {
                    _statusLabel.Text = $"[测试] 创建角色失败: {rsp.Message}";
                    ResetTestFlow();
                    return;
                }
                GD.Print($"[TestFlow] CreateRole OK, name={rsp.RoleInfo.RoleName}");
                _statusLabel.Text = "[测试] 创建角色成功!";
                GetTree().ChangeSceneToFile("res://scenes/main.tscn");
            }
        }

        // ── 测试直通：走真实登录→选服→进游戏流程 ──

        private void OnTestDirectPressed()
        {
            if (_network == null)
            {
                _statusLabel.Text = "网络管理器未初始化";
                return;
            }

            if (!_network.IsServerConnected())
            {
                _statusLabel.Text = "正在连接服务器...";
                _pendingTestDirect = true;
                _network.ConnectToServer();
                return;
            }

            _testFlowState = TestFlowState.Login;
            _loginButton.Disabled = true;
            if (_testButton != null) _testButton.Disabled = true;
            _statusLabel.Text = "[测试] 登录中...";

            // 发送登录请求（服务器会自动注册账号）
            var req = new Login.AccountLoginRequest
            {
                Username = "testdev",
                Password = "testdev",
                Platform = "pc",
                DeviceId = "test_device",
                ClientVersion = "1.0.0",
            };
            _network.SendPacket(MessageId.LoginAccountLoginReq, req);
        }

        private void HandleTestFlowLogin(Login.AccountLoginResponse rsp)
        {
            if (rsp.Code != Common.ErrorCode.Success)
            {
                _statusLabel.Text = $"[测试] 登录失败: {rsp.Message}";
                ResetTestFlow();
                return;
            }

            GD.Print($"[TestFlow] Login OK, accountId={_network.AccountId}");

            // 选服：取第一个服务器，或默认 serverId=1
            uint serverId = _network.LastServerId;
            if (serverId == 0 && _network.Servers.Count > 0)
                serverId = _network.Servers[0].ServerId;
            if (serverId == 0) serverId = 1;

            _testFlowState = TestFlowState.SelectServer;
            _statusLabel.Text = $"[测试] 选服中... (serverId={serverId})";

            var req = new Login.SelectServerRequest
            {
                AccountToken = _network.AccountToken,
                ServerId = serverId,
            };
            _network.SendPacket(MessageId.LoginSelectServerReq, req);
        }

        private void HandleTestFlowSelectServer(Login.SelectServerResponse rsp)
        {
            if (rsp.Code != Common.ErrorCode.Success)
            {
                _statusLabel.Text = $"[测试] 选服失败: {rsp.Message}";
                ResetTestFlow();
                return;
            }

            GD.Print($"[TestFlow] SelectServer OK, gatewayToken set, roles={_network.Roles.Count}");

            if (_network.Roles.Count > 0)
            {
                long roleId = (long)_network.Roles[0].RoleId;
                _testFlowState = TestFlowState.EnterGame;
                _statusLabel.Text = $"[测试] 进入游戏... (roleId={roleId})";

                var req = new Game.EnterGameRequest { RoleId = (ulong)roleId };
                _network.SendPacket(MessageId.GameEnterGameReq, req);
            }
            else
            {
                _testFlowState = TestFlowState.CreateRole;
                _statusLabel.Text = "[测试] 创建角色...";

                var req = new Game.CreateRoleRequest { RoleName = "测试勇者" };
                _network.SendPacket(MessageId.GameCreateRoleReq, req);
            }
        }

        private void ResetTestFlow()
        {
            _testFlowState = TestFlowState.None;
            _loginButton.Disabled = false;
            if (_testButton != null) _testButton.Disabled = false;
        }

        private string GetDeviceId()
        {
            // 稳定设备 ID：基于持久化存储，首次生成后复用
            const string DEVICE_FILE = "user://device_id.cfg";
            var config = new ConfigFile();
            Error err = config.Load(DEVICE_FILE);
            if (err == Error.Ok)
            {
                var existing = config.GetValue("device", "id", "").AsString();
                if (!string.IsNullOrEmpty(existing))
                    return existing;
            }

            string newId = (OS.GetUniqueId() + ":" + System.Environment.MachineName).Md5Text().Substring(0, 16);
            config.SetValue("device", "id", newId);
            config.Save(DEVICE_FILE);
            return newId;
        }

        private void LoadSavedAccount()
        {
            var config = new ConfigFile();
            Error err = config.Load(SAVE_FILE);

            if (err == Error.Ok)
            {
                _savedUsername = config.GetValue("login", "username", "").AsString();
                if (!string.IsNullOrEmpty(_savedUsername))
                {
                    _usernameEdit.Text = _savedUsername;
                    _passwordEdit.GrabFocus();
                }
            }
        }

        private void SaveAccount(string username)
        {
            var config = new ConfigFile();
            config.SetValue("login", "username", username);
            config.SetValue("login", "last_login_time", Time.GetUnixTimeFromSystem());
            config.Save(SAVE_FILE);
        }

        public override void _ExitTree()
        {
            if (_network != null)
            {
                _network.Connected -= OnConnected;
                _network.ConnectionError -= OnConnectionError;
                _network.Kicked -= OnKicked;
                _network.LoginResponse -= OnLoginResponse;
                _network.SelectServerResponse -= OnSelectServerResponse;
                _network.EnterGameResponse -= OnEnterGameResponse;
                _network.CreateRoleResponse -= OnCreateRoleResponse;
            }
        }
    }
}
