using Protocol;
using TMPro;
using UnityClientSharp.Map.Rendering;
using UnityClientSharp.Net;
using UnityEngine;
using UnityEngine.UI;

namespace UnityClientSharp.UI
{
    /// <summary>
    /// 登录面板 — 移植自 Godot LoginScene.cs：
    /// 用户名+密码（密文）+ 登录 + Test Direct（橙色字直通 testdev）+ 状态文本；启动即自动连接；
    /// 记住用户名（PlayerPrefs，不存密码）；回车=登录；未连接时登录钮禁用（Test Direct 自带连接步骤，常可用）。
    /// 裁剪（对齐计划）：登录成功自动选服（LastServerId 优先否则第一个服）、自动进第一个角色（无角色按用户名建角色），
    /// 不做选服/角色选择界面。
    /// </summary>
    public class LoginPanel : MonoBehaviour
    {
        private const string PrefKeyUsername = "bw_login_username";

        private TMP_InputField _userInput;
        private TMP_InputField _passInput;
        private Button _loginBtn;
        private Button _testBtn;
        private TextMeshProUGUI _status;
        private Net.NetworkSmokeTest _smoke;
        private bool _busy;
        private bool _testDirectMode; // Test Direct 流程中：自动选服/进角色让位给冒烟状态机

        // 测试访问
        public Button LoginButton => _loginBtn;
        public Button TestDirectButton => _testBtn;
        public TextMeshProUGUI StatusText => _status;
        public TMP_InputField UserInput => _userInput;
        public TMP_InputField PassInput => _passInput;

        public static LoginPanel Create(Net.NetworkSmokeTest smoke)
        {
            var go = new GameObject("LoginPanel");
            var panel = go.AddComponent<LoginPanel>();
            panel._smoke = smoke;
            panel.Build();
            return panel;
        }

        private void Build()
        {
            UiEventSystemUtil.EnsureExists();

            var canvasGo = new GameObject("LoginCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            canvasGo.AddComponent<GraphicRaycaster>();

            var panelGo = CreateUI(canvasGo.transform, "Panel");
            var panelRt = (RectTransform)panelGo.transform;
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.anchoredPosition = Vector2.zero;
            panelRt.sizeDelta = new Vector2(360, 320);
            var bg = panelGo.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.08f, 0.15f, 0.97f);
            var outline = panelGo.AddComponent<Outline>();
            outline.effectColor = new Color(0.3f, 0.5f, 0.9f);
            outline.effectDistance = new Vector2(2, -2);

            var title = CreateText(panelGo.transform, "Title", "登录", 24, new Color(1f, 0.9f, 0.6f));
            Place((RectTransform)title.transform, 0, 118, 200, 36);

            _userInput = CreateInput(panelGo.transform, "Username", "用户名", false);
            Place((RectTransform)_userInput.transform, 0, 55, 280, 34);
            _passInput = CreateInput(panelGo.transform, "Password", "密码", true);
            Place((RectTransform)_passInput.transform, 0, 10, 280, 34);
            _userInput.onSubmit.AddListener(_ => OnLoginClick());
            _passInput.onSubmit.AddListener(_ => OnLoginClick());

            // 记住用户名（对齐 Godot user://login_data.cfg，只记用户名）
            string saved = PlayerPrefs.GetString(PrefKeyUsername, "");
            _userInput.text = saved;

            _loginBtn = CreateButton(panelGo.transform, "LoginBtn", "登录", 15, Color.white,
                new Color(0.2f, 0.35f, 0.6f, 0.95f), OnLoginClick);
            Place((RectTransform)_loginBtn.transform, 0, -48, 140, 36);

            _testBtn = CreateButton(panelGo.transform, "TestBtn", "Test Direct", 13,
                new Color(1f, 0.6f, 0.2f), new Color(0.25f, 0.2f, 0.15f, 0.95f), OnTestDirectClick);
            Place((RectTransform)_testBtn.transform, 0, -94, 140, 30);

            _status = CreateText(panelGo.transform, "Status", "", 12, Color.white);
            _status.enableWordWrapping = true;
            Place((RectTransform)_status.transform, 0, -132, 320, 40);

            // 启动即连接（对齐 Godot LoginScene._Ready）
            SetStatus("正在连接服务器...");
            var nm = NetworkManager.Instance;
            if (nm != null && !nm.IsServerConnected())
                nm.ConnectToServer();
            RefreshButtons();
        }

        // ============ 输入与按钮 ============

        /// <summary>输入校验（对齐 Godot：空用户名/空密码拦截）。返回错误文案，通过返回 null。</summary>
        public static string ValidateInput(string username, string password)
        {
            if (string.IsNullOrEmpty(username)) return "请输入用户名";
            if (string.IsNullOrEmpty(password)) return "请输入密码";
            return null;
        }

        /// <summary>自动选服策略：LastServerId 在列表中优先，否则第一个服；无服返回 0。</summary>
        public static uint PickServerId(System.Collections.Generic.List<Server.ServerInfo> servers, uint lastServerId)
        {
            if (servers == null || servers.Count == 0) return 0;
            foreach (var s in servers)
                if (s.ServerId == lastServerId) return s.ServerId;
            return servers[0].ServerId;
        }

        private void OnLoginClick()
        {
            var err = ValidateInput(_userInput.text, _passInput.text);
            if (err != null) { SetStatus(err, true); return; }
            var nm = NetworkManager.Instance;
            if (nm == null || !nm.IsServerConnected()) { SetStatus("未连接到服务器", true); return; }

            _testDirectMode = false;
            SetBusy(true);
            SetStatus("登录中...");
            PlayerPrefs.SetString(PrefKeyUsername, _userInput.text);
            PlayerPrefs.Save();
            nm.SendPacket(MessageId.LoginAccountLoginReq, new Login.AccountLoginRequest
            {
                Username = _userInput.text,
                Password = _passInput.text,
                Platform = "pc",
                DeviceId = SystemInfo.deviceUniqueIdentifier,
                ClientVersion = "1.0.0",
            });
        }

        private void OnTestDirectClick()
        {
            if (_smoke == null) { SetStatus("直通组件缺失", true); return; }
            _testDirectMode = true;
            SetBusy(true);
            SetStatus("Test Direct 直通中...");
            _smoke.Run();
        }

        // ============ 网络事件 ============

        private void Start()
        {
            var nm = NetworkManager.Instance;
            if (nm == null) return;
            nm.Connected += OnConnected;
            nm.ConnectionError += OnConnectionError;
            nm.Disconnected += OnDisconnected;
            nm.Kicked += OnKicked;
            nm.LoginResponse += OnLoginResponse;
            nm.SelectServerResponse += OnSelectServerResponse;
            nm.EnterGameResponse += OnEnterGameResponse;
            nm.CreateRoleResponse += OnCreateRoleResponse;
        }

        private void OnDestroy()
        {
            var nm = NetworkManager.Instance;
            if (nm == null) return;
            nm.Connected -= OnConnected;
            nm.ConnectionError -= OnConnectionError;
            nm.Disconnected -= OnDisconnected;
            nm.Kicked -= OnKicked;
            nm.LoginResponse -= OnLoginResponse;
            nm.SelectServerResponse -= OnSelectServerResponse;
            nm.EnterGameResponse -= OnEnterGameResponse;
            nm.CreateRoleResponse -= OnCreateRoleResponse;
        }

        private void OnConnected()
        {
            SetStatus("已连接到服务器");
            RefreshButtons();
        }

        private void OnConnectionError(string err)
        {
            SetBusy(false);
            SetStatus("连接失败: " + err, true);
            RefreshButtons();
        }

        private void OnDisconnected()
        {
            SetBusy(false);
            SetStatus("与服务器断开连接");
            RefreshButtons();
        }

        private void OnKicked(string reason)
        {
            SetBusy(false);
            SetStatus("被服务器踢下线: " + reason, true);
            RefreshButtons();
        }

        private void OnLoginResponse(Login.AccountLoginResponse rsp)
        {
            if (_testDirectMode) return; // 直通流程由冒烟状态机驱动
            if (rsp.Code != Common.ErrorCode.Success)
            {
                SetBusy(false);
                SetStatus("登录失败: " + rsp.Message, true);
                return;
            }
            var nm = NetworkManager.Instance;
            uint serverId = PickServerId(nm.Servers, nm.LastServerId);
            if (serverId == 0)
            {
                SetBusy(false);
                SetStatus("无可用服务器", true);
                return;
            }
            SetStatus("选择服务器...");
            nm.SendPacket(MessageId.LoginSelectServerReq, new Login.SelectServerRequest
            {
                AccountToken = nm.AccountToken,
                ServerId = serverId,
            });
        }

        private void OnSelectServerResponse(Login.SelectServerResponse rsp)
        {
            if (_testDirectMode) return;
            if (rsp.Code != Common.ErrorCode.Success)
            {
                SetBusy(false);
                SetStatus("选服失败: " + rsp.Message, true);
                return;
            }
            var nm = NetworkManager.Instance;
            SetStatus("进入游戏...");
            if (nm.Roles.Count > 0)
            {
                nm.SendPacket(MessageId.GameEnterGameReq, new Game.EnterGameRequest { RoleId = nm.Roles[0].RoleId });
            }
            else
            {
                string roleName = string.IsNullOrEmpty(_userInput.text) ? "player" : _userInput.text;
                nm.SendPacket(MessageId.GameCreateRoleReq, new Game.CreateRoleRequest { RoleName = roleName });
            }
        }

        private void OnEnterGameResponse(Game.EnterGameResponse rsp) => OnEnterResult(rsp.Code, rsp.Message);
        private void OnCreateRoleResponse(Game.CreateRoleResponse rsp) => OnEnterResult(rsp.Code, rsp.Message);

        private void OnEnterResult(Common.ErrorCode code, string message)
        {
            if (_testDirectMode) return;
            if (code != Common.ErrorCode.Success)
            {
                SetBusy(false);
                SetStatus("进入游戏失败: " + message, true);
                return;
            }
            // 面板销毁由 MapBootstrap 在 MapInfoReceived 后执行（地图名跟服务器推送走）
            SetStatus("正在加载地图...");
        }

        // ============ 状态与按钮 ============

        /// <summary>供外部（MapBootstrap 回登录页时）预设状态文案。</summary>
        public void SetInitialStatus(string text) => SetStatus(text, true);

        private void SetStatus(string text, bool isError = false)
        {
            if (_status == null) return;
            _status.text = text ?? "";
            _status.color = isError ? new Color(1f, 0.55f, 0.55f) : Color.white;
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            RefreshButtons();
        }

        private void RefreshButtons()
        {
            var nm = NetworkManager.Instance;
            bool connected = nm != null && nm.IsServerConnected();
            if (_loginBtn != null) _loginBtn.interactable = connected && !_busy;
            // Test Direct 自带连接步骤（冒烟第一步），未连接也可用
            if (_testBtn != null) _testBtn.interactable = !_busy;
        }

        // ============ UI 构建 ============

        private static GameObject CreateUI(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, string text, int fontSize, Color color)
        {
            var go = CreateUI(parent, name);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            FontUtil.ApplyCjkFont(tmp);
            return tmp;
        }

        private static TMP_InputField CreateInput(Transform parent, string name, string placeholder, bool password)
        {
            var go = CreateUI(parent, name);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.15f, 0.15f, 0.22f, 1f);

            var area = CreateUI(go.transform, "TextArea");
            var areaRt = (RectTransform)area.transform;
            areaRt.anchorMin = Vector2.zero;
            areaRt.anchorMax = Vector2.one;
            areaRt.offsetMin = new Vector2(10, 4);
            areaRt.offsetMax = new Vector2(-10, -4);
            area.AddComponent<RectMask2D>();

            var textGo = CreateUI(area.transform, "Text");
            var textRt = (RectTransform)textGo.transform;
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = 14;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.raycastTarget = false;
            FontUtil.ApplyCjkFont(tmp);

            var phGo = CreateUI(area.transform, "Placeholder");
            var phRt = (RectTransform)phGo.transform;
            phRt.anchorMin = Vector2.zero;
            phRt.anchorMax = Vector2.one;
            phRt.offsetMin = Vector2.zero;
            phRt.offsetMax = Vector2.zero;
            var ph = phGo.AddComponent<TextMeshProUGUI>();
            ph.text = placeholder;
            ph.fontSize = 14;
            ph.color = new Color(1f, 1f, 1f, 0.35f);
            ph.alignment = TextAlignmentOptions.Left;
            ph.raycastTarget = false;
            FontUtil.ApplyCjkFont(ph);

            var input = go.AddComponent<TMP_InputField>();
            input.targetGraphic = img;
            input.textViewport = areaRt;
            input.textComponent = tmp;
            input.placeholder = ph;
            if (password) input.contentType = TMP_InputField.ContentType.Password;
            return input;
        }

        private static Button CreateButton(Transform parent, string name, string label, int fontSize,
            Color textColor, Color bgColor, UnityEngine.Events.UnityAction onClick)
        {
            var go = CreateUI(parent, name);
            var img = go.AddComponent<Image>();
            img.color = bgColor;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);
            var tmp = CreateText(go.transform, "Text", label, fontSize, textColor);
            var tmpRt = (RectTransform)tmp.transform;
            tmpRt.anchorMin = Vector2.zero;
            tmpRt.anchorMax = Vector2.one;
            tmpRt.offsetMin = Vector2.zero;
            tmpRt.offsetMax = Vector2.zero;
            tmp.raycastTarget = false;
            return btn;
        }
    }
}
