using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace UnityClientSharp.UI
{
    /// <summary>
    /// 游戏面板统一接口（对齐 Godot PanelManager.IPanel 的最小集）。
    /// </summary>
    public interface IGamePanel
    {
        string PanelName { get; }
        bool PanelVisible { get; }
        void SetVisible(bool visible);
    }

    /// <summary>
    /// 面板可选实现的 ESC 处理器：返回 true 表示 ESC 已被面板内部消费
    /// （如 GM 面板先关闭自动补全下拉，对齐 Godot OnCmdEditGuiInput 的 Escape 处理），
    /// 不再触发 GamePanelManager 的"关闭最上面板"。
    /// </summary>
    public interface IGamePanelEscapeHandler
    {
        bool OnEscape();
    }

    /// <summary>
    /// 游戏面板管理器 — 对齐 Godot PanelManager 的职责子集，并做两处合理增强：
    /// 1. 统一热键分发：各面板不再各自在 Update 里轮询 GetKeyDown，改为注册键位由这里集中分发；
    ///    输入框（TMP_InputField/InputField）聚焦时跳过热键，避免在角色面板输数字时误触开关
    ///    （Godot 端没有输入框面板，无此问题；Unity 端 CharacterPanelHud 引入输入框后必须处理）。
    /// 2. ESC 关闭最上面板：维护显示顺序栈，ESC 关掉最近打开且仍可见的面板（Godot 端无 ESC 处理）。
    /// 3. 层级：经管理器打开的面板提升其 Canvas sortingOrder，后开的在上（对齐 Godot RequestFocus→BringToFront）。
    /// 同类互斥不做（Godot 端也无，不发明需求）。
    /// 生命周期：由 MapBootstrap.StartGame 通过 <see cref="Ensure"/> 创建并随 gameRoots 销毁；
    /// 面板注册对 null 实例容错（测试环境无管理器时静默跳过）。
    /// </summary>
    public class GamePanelManager : MonoBehaviour
    {
        public static GamePanelManager Instance { get; private set; }

        /// <summary>面板 Canvas 的基础 sortingOrder（与既有 HUD 一致），每次置顶递增。</summary>
        public const int BaseSortingOrder = 80;

        private readonly List<IGamePanel> _panels = new();
        private readonly Dictionary<KeyCode, IGamePanel> _hotkeys = new();
        private readonly List<IGamePanel> _showStack = new(); // 显示顺序栈，栈顶 = 最近打开
        private int _zCounter;

        /// <summary>获取或创建管理器实例（幂等）。</summary>
        public static GamePanelManager Ensure()
        {
            if (Instance == null)
            {
                var go = new GameObject("GamePanelManager");
                Instance = go.AddComponent<GamePanelManager>();
            }
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ============ 注册表 ============

        public void RegisterPanel(IGamePanel panel)
        {
            if (panel != null && !_panels.Contains(panel))
                _panels.Add(panel);
        }

        public void UnregisterPanel(IGamePanel panel)
        {
            _panels.Remove(panel);
            _showStack.Remove(panel);
            // 清理指向该面板的热键
            var stale = new List<KeyCode>();
            foreach (var kv in _hotkeys)
                if (ReferenceEquals(kv.Value, panel)) stale.Add(kv.Key);
            foreach (var key in stale)
                _hotkeys.Remove(key);
        }

        public T GetPanel<T>() where T : class, IGamePanel
        {
            foreach (var p in _panels)
                if (p is T t) return t;
            return null;
        }

        public void RegisterHotkey(KeyCode key, IGamePanel panel)
        {
            if (panel != null)
                _hotkeys[key] = panel;
        }

        public void UnregisterHotkey(KeyCode key) => _hotkeys.Remove(key);

        // ============ 开关与层级 ============

        /// <summary>统一切换入口：热键与功能按钮栏都走这里，保证层级/ESC 栈一致。</summary>
        public void TogglePanel(IGamePanel panel)
        {
            if (panel == null) return;
            panel.SetVisible(!panel.PanelVisible);
            if (panel.PanelVisible)
                OnPanelShown(panel);
            else
                _showStack.Remove(panel);
        }

        private void OnPanelShown(IGamePanel panel)
        {
            // 层级：后开的在上（对齐 Godot RequestFocus → BringToFront）
            if (panel is Component c)
            {
                var canvas = c.GetComponentInChildren<Canvas>(true);
                if (canvas != null)
                    canvas.sortingOrder = BaseSortingOrder + ++_zCounter;
            }
            _showStack.Remove(panel);
            _showStack.Add(panel);
        }

        /// <summary>ESC 关闭最近打开且仍可见的面板；没有可见面板时返回 false。</summary>
        public bool CloseTopmost()
        {
            for (int i = _showStack.Count - 1; i >= 0; i--)
            {
                var panel = _showStack[i];
                if (panel == null || !panel.PanelVisible)
                {
                    _showStack.RemoveAt(i);
                    continue;
                }
                panel.SetVisible(false);
                _showStack.RemoveAt(i);
                return true;
            }
            return false;
        }

        /// <summary>
        /// ESC 分发：先让栈顶可见面板消费（实现 <see cref="IGamePanelEscapeHandler"/> 的面板，
        /// 如 GM 面板先关补全下拉），无人消费才关闭最上面板。
        /// </summary>
        public void HandleEscape()
        {
            for (int i = _showStack.Count - 1; i >= 0; i--)
            {
                var panel = _showStack[i];
                if (panel != null && panel.PanelVisible && panel is IGamePanelEscapeHandler handler && handler.OnEscape())
                    return;
            }
            CloseTopmost();
        }

        /// <summary>隐藏所有已注册面板（对齐 Godot PanelManager.HideAll）。</summary>
        public void HideAll()
        {
            foreach (var p in _panels)
                p?.SetVisible(false);
            _showStack.Clear();
        }

        // ============ 输入分发 ============

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                HandleEscape();
                return;
            }

            // 输入框聚焦时不分发热键（角色面板输属性值时不误触 I/F4）
            if (IsTextInputFocused())
                return;

            foreach (var kv in _hotkeys)
            {
                if (Input.GetKeyDown(kv.Key))
                {
                    TogglePanel(kv.Value);
                    break;
                }
            }
        }

        private static bool IsTextInputFocused()
        {
            var es = EventSystem.current;
            if (es == null || es.currentSelectedGameObject == null)
                return false;
            var go = es.currentSelectedGameObject;
            return go.GetComponent<TMP_InputField>() != null
                || go.GetComponent<UnityEngine.UI.InputField>() != null;
        }
    }
}
