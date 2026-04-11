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
                nm.PacketReceived += OnPacketReceived;

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

        private void OnPacketReceived(int msgId)
        {
            if ((MessageId)msgId != MessageId.LoginAccountLoginRsp)
                return;

            _loginButton.Disabled = false;

            try
            {
                var nm = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
                var payload = nm?.GetLastPayload() ?? new byte[0];
                var rsp = Login.AccountLoginResponse.Parser.ParseFrom(payload);
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
            catch (System.Exception e)
            {
                GD.PushError($"[LoginScene] Parse login response failed: {e.Message}");
                _statusLabel.Text = "解析服务器响应失败";
            }
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

        /// <summary>
        /// 测试直通：跳过登录流程，直接进入游戏
        /// </summary>
        private void OnTestDirectPressed()
        {
            GD.Print("[LoginScene] Test direct entry pressed");
            var nm = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
            if (nm != null)
            {
                nm.AccountToken = "test_bypass_" + Time.GetUnixTimeFromSystem();
                nm.AccountId = 1;
                nm.LastServerId = 1;
                nm.CachedRoleInfo = new Game.FullRoleInfo
                {
                    RoleName = "测试勇者",
                    Level = 1,
                    Job = "勇者",
                    Title = "冒险家",
                    Status = "探索中...",
                    Gold = 10000,
                    Diamond = 100,
                    TotalPower = 100,
                };
            }
            _statusLabel.Text = "测试直通，跳转游戏...";
            GetTree().ChangeSceneToFile("res://scenes/main.tscn");
        }
    }
}
