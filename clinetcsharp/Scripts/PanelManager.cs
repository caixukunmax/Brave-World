using Godot;
using System.Collections.Generic;
using System.Linq;

namespace ClinetCSharp
{
    /// <summary>
    /// UI 面板管理器 — 集中管理面板焦点、层级、快捷键和生命周期。
    /// 作为 UICanvas (CanvasLayer) 的子节点添加到场景中。
    /// 所有 DraggablePanel 子类通过 Register/Unregister 注册。
    /// 所有 IPanel 实现类通过 RegisterPanel/UnregisterPanel 注册。
    ///
    /// 职责：
    ///   1. 焦点管理：同时只有一个面板获得焦点，点击面板请求焦点
    ///   2. 层级管理：获得焦点的面板提到最前（CanvasLayer 子节点末尾）
    ///   3. 快捷键：统一管理面板切换快捷键，避免冲突
    ///   4. 面板查询：按类型查找面板，支持面板间通信
    /// </summary>
    public partial class PanelManager : Node
    {
        public static PanelManager Instance { get; private set; }

        private CanvasLayer _uiCanvas;
        private readonly List<DraggablePanel> _panels = new();
        private DraggablePanel _focusedPanel;
        private readonly Dictionary<Key, DraggablePanel> _toggleKeys = new();
        private readonly List<IPanel> _allPanels = new();

        public override void _Ready()
        {
            Instance = this;
            _uiCanvas = GetParent() as CanvasLayer;
        }

        public override void _ExitTree()
        {
            if (Instance == this)
                Instance = null;
        }

        #region DraggablePanel Registry
        public void Register(DraggablePanel panel)
        {
            if (!_panels.Contains(panel))
                _panels.Add(panel);
        }

        public void Unregister(DraggablePanel panel)
        {
            _panels.Remove(panel);
            if (_focusedPanel == panel)
                _focusedPanel = null;
        }
        #endregion

        #region IPanel Registry
        public void RegisterPanel(IPanel panel)
        {
            if (!_allPanels.Contains(panel))
                _allPanels.Add(panel);
        }

        public void UnregisterPanel(IPanel panel)
        {
            _allPanels.Remove(panel);
        }

        public T GetPanel<T>() where T : class, IPanel
        {
            return _allPanels.OfType<T>().FirstOrDefault();
        }

        public T GetDraggablePanel<T>() where T : DraggablePanel
        {
            return _panels.OfType<T>().FirstOrDefault();
        }

        public void HideAll()
        {
            foreach (var p in _allPanels)
                p.HidePanel();
            foreach (var p in _panels)
                p.Visible = false;
        }
        #endregion

        #region Toggle Keys
        public void RegisterToggleKey(Key key, DraggablePanel panel)
        {
            _toggleKeys[key] = panel;
        }

        public void UnregisterToggleKey(Key key)
        {
            _toggleKeys.Remove(key);
        }
        #endregion

        #region Focus Management
        public void RequestFocus(DraggablePanel panel)
        {
            if (_focusedPanel == panel) return;
            _focusedPanel?.NotifyFocusLost();
            _focusedPanel = panel;
            _focusedPanel?.NotifyFocusGained();
            BringToFront(panel);
        }

        public void ClearFocus()
        {
            _focusedPanel?.NotifyFocusLost();
            _focusedPanel = null;
        }

        public DraggablePanel GetFocusedPanel() => _focusedPanel;
        #endregion

        #region Z-Order
        public void BringToFront(DraggablePanel panel)
        {
            if (_uiCanvas == null) return;
            int lastIdx = _uiCanvas.GetChildCount() - 1;
            if (panel.GetIndex() < lastIdx)
                _uiCanvas.MoveChild(panel, lastIdx);
        }
        #endregion

        #region Query
        public bool IsAnyPanelInteracting()
        {
            foreach (var p in _panels)
                if (p.IsInteracting) return true;
            return false;
        }

        public bool IsMouseOverAnyPanel()
        {
            foreach (var p in _panels)
                if (p.IsMouseOver()) return true;
            return false;
        }
        #endregion

        #region Input
        public override void _Input(InputEvent @event)
        {
            if (@event is InputEventKey key && key.Pressed && !key.Echo)
            {
                if (_toggleKeys.TryGetValue(key.Keycode, out var panel))
                {
                    panel.Toggle();
                    GetViewport().SetInputAsHandled();
                }
            }
        }
        #endregion
    }

    /// <summary>
    /// 面板接口 — 统一管理所有面板的生命周期和可见性。
    /// DraggablePanel 和 DebugPanel 都实现此接口。
    /// </summary>
    public interface IPanel
    {
        bool IsVisible();
        void ShowPanel();
        void HidePanel();
        string PanelName { get; }
    }
}