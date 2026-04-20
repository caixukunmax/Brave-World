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

        // 测试直通流程状态
        private enum TestFlowState { None, Login, SelectServer, EnterGame, CreateRole }
        private TestFlowState _testFlowState = TestFlowState.None;

        public override void _Ready()
        {
            GD.Print("[LoginScene] _ready() called");

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

            var nm = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
            if (nm != null)
            {
                nm.Connected += OnConnected;
                nm.ConnectionError += OnConnectionError;
                nm.Kicked += OnKicked;
                nm.LoginResponse += OnLoginResponse;
                nm.SelectServerResponse += OnSelectServerResponse;
                nm.EnterGameResponse += OnEnterGameResponse;
                nm.CreateRoleResponse += OnCreateRoleResponse;

                _statusLabel.Text = "正在连接服务器...";
                _loginButton.Disabled = true;
                nm.ConnectToServer();
            }
            else
            {
                _statusLabel.Text = "网络管理器未初始化";
                GD.PushError("[LoginScene] NetworkManager is null");
            }
        }

        private void OnConnected()
        {
            GD.Print("[LoginScene] Connected to server");
            _statusLabel.Text = "已连接到服务器";
            _loginButton.Disabled = false;
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
            GD.Print("[LoginScene] OnLoginPressed() called");

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

            GD.Print("[LoginScene] Sending login request: " + username);

            var nm = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
            if (nm == null)
            {
                GD.PushError("[LoginScene] NetworkManager is null!");
                _statusLabel.Text = "网络错误";
                _loginButton.Disabled = false;
                return;
            }

            nm.SendPacket(MessageId.LoginAccountLoginReq, req);
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
            GD.Print($"[LoginScene] Login response: code={rsp.Code}");

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
            GD.Print("[LoginScene] Test direct entry pressed");
            var nm = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
            if (nm == null || !nm.IsServerConnected())
            {
                _statusLabel.Text = "未连接服务器";
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
            nm.SendPacket(MessageId.LoginAccountLoginReq, req);
        }

        private void HandleTestFlowLogin(Login.AccountLoginResponse rsp)
        {
            if (rsp.Code != Common.ErrorCode.Success)
            {
                _statusLabel.Text = $"[测试] 登录失败: {rsp.Message}";
                ResetTestFlow();
                return;
            }

            var nm = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
            GD.Print($"[TestFlow] Login OK, accountId={nm.AccountId}");

            // 选服：取第一个服务器，或默认 serverId=1
            uint serverId = nm.LastServerId;
            if (serverId == 0 && nm.Servers.Count > 0)
                serverId = nm.Servers[0].ServerId;
            if (serverId == 0) serverId = 1;

            _testFlowState = TestFlowState.SelectServer;
            _statusLabel.Text = $"[测试] 选服中... (serverId={serverId})";

            var req = new Login.SelectServerRequest
            {
                AccountToken = nm.AccountToken,
                ServerId = serverId,
            };
            nm.SendPacket(MessageId.LoginSelectServerReq, req);
        }

        private void HandleTestFlowSelectServer(Login.SelectServerResponse rsp)
        {
            if (rsp.Code != Common.ErrorCode.Success)
            {
                _statusLabel.Text = $"[测试] 选服失败: {rsp.Message}";
                ResetTestFlow();
                return;
            }

            var nm = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
            GD.Print($"[TestFlow] SelectServer OK, gatewayToken set, roles={nm.Roles.Count}");

            if (nm.Roles.Count > 0)
            {
                long roleId = (long)nm.Roles[0].RoleId;
                _testFlowState = TestFlowState.EnterGame;
                _statusLabel.Text = $"[测试] 进入游戏... (roleId={roleId})";

                var req = new Game.EnterGameRequest { RoleId = (ulong)roleId };
                nm.SendPacket(MessageId.GameEnterGameReq, req);
            }
            else
            {
                _testFlowState = TestFlowState.CreateRole;
                _statusLabel.Text = "[测试] 创建角色...";

                var req = new Game.CreateRoleRequest { RoleName = "测试勇者" };
                nm.SendPacket(MessageId.GameCreateRoleReq, req);
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
            var deviceId = Time.GetUnixTimeFromSystem().ToString();
            deviceId += GD.Randi().ToString();
            return deviceId.Md5Text().Substring(0, 16);
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
            var nm = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
            if (nm != null)
            {
                nm.Connected -= OnConnected;
                nm.ConnectionError -= OnConnectionError;
                nm.Kicked -= OnKicked;
                nm.LoginResponse -= OnLoginResponse;
                nm.SelectServerResponse -= OnSelectServerResponse;
                nm.EnterGameResponse -= OnEnterGameResponse;
                nm.CreateRoleResponse -= OnCreateRoleResponse;
            }
        }
    }
}
