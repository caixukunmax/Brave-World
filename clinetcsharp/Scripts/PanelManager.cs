using Godot;
using System;
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

        [Export] public PackedScene DebugPanelScene { get; set; }
        [Export] public PackedScene GMPanelScene { get; set; }
        [Export] public PackedScene IntegratedPanelScene { get; set; }
        [Export] public PackedScene CharacterPanelScene { get; set; }
        [Export] public PackedScene EntityListPanelScene { get; set; }
        [Export] public PackedScene InventoryUIScene { get; set; }
        [Export] public PackedScene SkillPanelScene { get; set; }

        private CanvasLayer _uiCanvas;
        private readonly List<DraggablePanel> _panels = new();
        private DraggablePanel _focusedPanel;
        private readonly Dictionary<Key, DraggablePanel> _toggleKeys = new();
        private readonly List<IPanel> _allPanels = new();
        private readonly Dictionary<Type, PackedScene> _panelScenes = new();

        public override void _Ready()
        {
            Instance = this;
            _uiCanvas = GetParent() as CanvasLayer;

            // 兜底：当 C# [Export] 属性因 Godot/C# 绑定问题未从 tscn 加载时，按路径手动加载
            DebugPanelScene ??= GD.Load<PackedScene>("res://scenes/debug_panel.tscn");
            GMPanelScene ??= GD.Load<PackedScene>("res://scenes/gm_panel.tscn");
            IntegratedPanelScene ??= GD.Load<PackedScene>("res://scenes/integrated_panel.tscn");
            CharacterPanelScene ??= GD.Load<PackedScene>("res://scenes/character_panel.tscn");
            EntityListPanelScene ??= GD.Load<PackedScene>("res://scenes/entity_list_panel.tscn");
            InventoryUIScene ??= GD.Load<PackedScene>("res://scenes/inventory_ui_panel.tscn");
            SkillPanelScene ??= GD.Load<PackedScene>("res://scenes/skill_panel.tscn");

            RegisterScene<DebugPanel>(DebugPanelScene);
            RegisterScene<GMPanel>(GMPanelScene);
            RegisterScene<IntegratedPanel>(IntegratedPanelScene);
            RegisterScene<CharacterPanel>(CharacterPanelScene);
            RegisterScene<EntityListPanel>(EntityListPanelScene);
            RegisterScene<InventoryUI>(InventoryUIScene);
            RegisterScene<SkillPanel>(SkillPanelScene);

            // 调试面板负责在初始化时应用 debug_panel_config.cfg 里的网格/相机/实体配置
            // 必须在启动时就实例化（保持隐藏），否则游戏一开始会缺少这些配置
            CallDeferred(MethodName.EnsureDebugPanelAtStartup);
        }

        private void EnsureDebugPanelAtStartup()
        {
            EnsurePanel<DebugPanel>();
        }

        private void RegisterScene<T>(PackedScene scene) where T : class, IPanel
        {
            if (scene != null)
                _panelScenes[typeof(T)] = scene;
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
            return EnsurePanel<T>();
        }

        public T GetDraggablePanel<T>() where T : DraggablePanel
        {
            return EnsurePanel<T>() as T;
        }

        private T EnsurePanel<T>() where T : class, IPanel
        {
            var existing = _allPanels.OfType<T>().FirstOrDefault();
            if (existing != null) return existing;
            if (!_panelScenes.TryGetValue(typeof(T), out var scene)) return null;
            if (_uiCanvas == null) return null;

            var instance = scene.Instantiate();
            if (instance is not Node node) return null;

            _uiCanvas.AddChild(node);

            // 立即注册，避免 CallDeferred(RegisterWithManager) 的延迟导致首次查询返回空
            if (instance is DraggablePanel dp)
                Register(dp);
            if (instance is IPanel panel)
                RegisterPanel(panel);

            return _allPanels.OfType<T>().FirstOrDefault();
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