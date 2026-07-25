using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UnityClientSharp.UI
{
    /// <summary>
    /// GM 面板（F2）— 移植自 Godot GMPanel.cs（壳与生命周期）。
    /// 构建细节在 GMPanelHud.Build.cs，分组数据/持久化在 GMPanelHud.Data.cs，
    /// 执行/响应/自动补全/历史在 GMPanelHud.Actions.cs。
    /// 热键 F2 注册进 GamePanelManager（对齐 Godot SetToggleKey(Key.F2)）；
    /// ESC 优先关闭补全下拉（实现 <see cref="IGamePanelEscapeHandler"/>，
    /// 对齐 Godot OnCmdEditGuiInput 的 Escape 处理），未开下拉时由管理器关闭面板。
    /// </summary>
    public partial class GMPanelHud : MonoBehaviour, IGamePanel, IGamePanelEscapeHandler
    {
        private GameObject _panelRoot;

        // 命令栏
        private TMP_InputField _cmdEdit;
        private TextMeshProUGUI _cmdHint;      // 参数暗字提示（Godot 幽灵文本的 uGUI 适配，见 Build.cs）
        private RectTransform _suggestPanel;
        private readonly List<GmCommandSuggester.Suggestion> _suggestItems = new();
        private bool _suppressSuggestionUpdate;

        // 响应日志区（Unity 增强，Godot 仅输出到编辑器控制台）
        private ScrollRect _logScroll;
        private RectTransform _logContent;
        private const int MaxLogLines = 50;

        // 分组区
        private RectTransform _groupContainer;

        // 内联编辑器
        private GameObject _addGroupRow;
        private TMP_InputField _addGroupEdit;
        private GameObject _addCmdRow;
        private TMP_InputField _addCmdLabelEdit;
        private TMP_InputField _addCmdEdit;
        private GmGroup _addCmdTarget;
        private GmCommand _editCmdTarget;

        // 右键菜单
        private GameObject _contextMenu;
        private GmCommand _menuCmd;
        private GmGroup _menuGroup;

        // 命令历史（Unity 增强：上下键翻历史，Godot 端无；实现便宜且 GM 面板高频重复执行）
        private readonly List<string> _history = new();
        private int _historyIndex = -1;
        private const int MaxHistory = 50;

        public bool PanelVisible => _panelRoot != null && _panelRoot.activeSelf;
        public string PanelName => "GM面板";

        public static GMPanelHud Create()
        {
            var go = new GameObject("GMPanelHud");
            var hud = go.AddComponent<GMPanelHud>();
            hud.Build();
            return hud;
        }

        /// <summary>ESC 优先消费：补全下拉可见时只关下拉（对齐 Godot），否则交还管理器关面板。</summary>
        public bool OnEscape()
        {
            if (_suggestPanel != null && _suggestPanel.gameObject.activeSelf)
            {
                HideSuggestions();
                return true;
            }
            return false;
        }

        public void SetVisible(bool visible)
        {
            if (_panelRoot != null) _panelRoot.SetActive(visible);
            if (visible)
            {
                // 对齐 Godot NotifyFocusGained → GrabFocus
                _cmdEdit?.ActivateInputField();
            }
            else
            {
                // 对齐 Godot OnClosed：收起补全与提示（整个 canvas  deactivate 已覆盖，这里复位状态）
                HideSuggestions();
                if (_cmdHint != null) _cmdHint.gameObject.SetActive(false);
                CloseContextMenu();
            }
        }

        public void Toggle() => SetVisible(!PanelVisible);

        private void Update()
        {
            // 命令历史：输入框聚焦时上下键翻历史（输入框导航已设 None，不会与 UI 导航冲突）
            if (_cmdEdit == null || !_cmdEdit.isFocused || _history.Count == 0)
                return;

            if (Input.GetKeyDown(KeyCode.UpArrow))
                NavigateHistory(-1);
            else if (Input.GetKeyDown(KeyCode.DownArrow))
                NavigateHistory(1);
        }

        private void OnDestroy()
        {
            GamePanelManager.Instance?.UnregisterPanel(this);

            var nm = Net.NetworkManager.Instance;
            if (nm != null)
                nm.GmResponse -= OnGmResponse;
        }

        // ============ UI 工具 ============

        private static GameObject CreateUI(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            // 本引擎新建 RectTransform 的 sizeDelta 默认 (100,100) 而非 (0,0)——
            // 不 layout 驱动也不显式设尺寸的节点（如 ScrollRect 的锚点拉伸 Content）
            // 会带着 +100 的默认尺寸溢出（第二轮事故"内容宽 504>视口 404 左移 50px"根因）
            ((RectTransform)go.transform).sizeDelta = Vector2.zero;
            return go;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, string text, int fontSize, Color color)
        {
            var go = CreateUI(parent, name);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            Map.Rendering.FontUtil.ApplyCjkFont(tmp);
            return tmp;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
