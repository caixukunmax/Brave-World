using Godot;
using System.Collections.Generic;

namespace ClinetCSharp
{
    /// <summary>
    /// UI 输入策略单例 — 统一管理游戏暂停时的 UI 输入生命周期与输入门控。
    ///
    /// 职责：
    ///   1. 暂停/恢复游戏时，保证已注册的 UI 节点仍可处理输入（ProcessMode=Always）。
    ///   2. 提供统一的鼠标是否位于交互式 UI 上的判断。
    ///   3. 为 DraggablePanel 等 UI 面板提供注册/注销入口。
    ///
    /// 设计原则：
    ///   - 非 UI 输入（相机拖拽/缩放、地图编辑）应下沉到 _UnhandledInput，
    ///     让 GUI 控件先在 _GuiInput 层消费事件。
    ///   - UI 面板在 _Ready 时自动注册，退出场景时自动注销。
    /// </summary>
    [GlobalClass]
    public partial class UIInputPolicy : Node
    {
        public static UIInputPolicy Instance { get; private set; }

        private bool _isPaused;
        private bool _wasTreePaused;
        private readonly HashSet<Node> _registeredNodes = new();
        private readonly Dictionary<Node, ProcessModeEnum> _savedProcessModes = new();

        public override void _Ready()
        {
            Instance = this;
            // 自身必须始终保持处理，否则暂停期间无法恢复其他节点
            ProcessMode = ProcessModeEnum.Always;
        }

        public override void _ExitTree()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>注册需要在游戏暂停时保持输入的 UI 节点。</summary>
        public void RegisterUiNode(Node node)
        {
            if (node == null || !IsInstanceValid(node)) return;
            if (!_registeredNodes.Add(node)) return;

            // 如果当前已暂停，立即让新注册节点也保持输入
            if (_isPaused)
                SaveAndSetAlways(node);
        }

        /// <summary>注销 UI 节点，恢复其原本的 ProcessMode。</summary>
        public void UnregisterUiNode(Node node)
        {
            if (node == null) return;

            if (_isPaused && _savedProcessModes.TryGetValue(node, out var savedMode) && IsInstanceValid(node))
            {
                node.ProcessMode = savedMode;
                _savedProcessModes.Remove(node);
            }

            _registeredNodes.Remove(node);
        }

        /// <summary>
        /// 暂停游戏，同时保证已注册 UI 节点继续处理输入。
        /// 可嵌套调用：内部只保存第一次调用前的暂停状态。
        /// </summary>
        public void PauseGame()
        {
            if (_isPaused) return;

            var tree = GetTree();
            if (tree == null) return;

            _wasTreePaused = tree.Paused;
            tree.Paused = true;
            _isPaused = true;

            foreach (var node in _registeredNodes)
            {
                if (IsInstanceValid(node))
                    SaveAndSetAlways(node);
            }
        }

        /// <summary>恢复游戏到 PauseGame 之前的暂停状态。</summary>
        public void ResumeGame()
        {
            if (!_isPaused) return;

            foreach (var node in _registeredNodes)
            {
                if (!IsInstanceValid(node)) continue;
                if (_savedProcessModes.TryGetValue(node, out var savedMode))
                    node.ProcessMode = savedMode;
            }
            _savedProcessModes.Clear();

            _isPaused = false;

            var tree = GetTree();
            if (tree != null)
                tree.Paused = _wasTreePaused;
        }

        /// <summary>判断鼠标当前是否位于交互式 UI 控件上（会阻断世界输入）。</summary>
        public bool IsMouseOverInteractiveUi()
        {
            var viewport = GetViewport();
            if (viewport == null) return false;

            var hovered = viewport.GuiGetHoveredControl();
            if (hovered == null) return false;
            // MouseFilter=Ignore 的控件不接收也不阻挡输入
            if (hovered.MouseFilter == Control.MouseFilterEnum.Ignore) return false;

            return true;
        }

        /// <summary>判断世界层（相机/地图）是否应该接收该输入事件。</summary>
        public bool ShouldWorldReceiveInput(InputEvent @event)
        {
            // 非鼠标事件（如纯键盘）默认交给世界层自己的 _Input/_UnhandledInput 判断
            if (@event is not InputEventMouse) return true;
            return !IsMouseOverInteractiveUi();
        }

        private void SaveAndSetAlways(Node node)
        {
            if (_savedProcessModes.ContainsKey(node)) return;

            var currentMode = node.ProcessMode;
            if (currentMode == ProcessModeEnum.Always) return;

            _savedProcessModes[node] = currentMode;
            node.ProcessMode = ProcessModeEnum.Always;
        }
    }
}
