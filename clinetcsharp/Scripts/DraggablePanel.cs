using Godot;
using System.Threading;

namespace ClinetCSharp
{
    /// <summary>
    /// 可拖拽面板基类 — 封装标题栏拖拽、8方向resize、最小化、关闭、位置保存防抖。
    /// 输入优先级：_Input + SetInputAsHandled() → 相机 _UnhandledInput 永远收不到面板事件。
    ///
    /// 约定场景结构：
    ///   PanelName (PanelContainer, script=子类)
    ///     VBoxContainer
    ///       TitleBar (PanelContainer, custom_minimum_size.y = 32)
    ///         HBoxContainer
    ///           MinimizeButton (Button, 可选)
    ///           [Label/TabLabel ...]
    ///           Spacer (Control, size_flags_horizontal = EXPAND_FILL)
    ///           CloseButton (Button, 可选)
    ///       Content (Control, 子类自定义)
    /// </summary>
    [GlobalClass]
    public partial class DraggablePanel : PanelContainer, IPanel
    {
        /// <summary>全局标志：是否有面板正在拖拽。CameraController 检查此标志避免冲突。</summary>
        public static bool IsAnyDragging { get; set; }

        #region Export Properties
        [Export] public float MinWidth { get; set; } = 300;
        [Export] public float MinHeight { get; set; } = 150;
        [Export] public float TitleBarHeight { get; set; } = 32;
        [Export] public float ResizeEdgeZone { get; set; } = 8.0f;
        [Export] public bool EnableDrag { get; set; } = true;
        [Export] public bool EnableResize { get; set; } = true;
        [Export] public bool EnableMinimize { get; set; } = true;
        [Export] public bool EnableClose { get; set; } = true;
        [Export] public bool ClampToScreen { get; set; } = true;
        #endregion

        #region Discovered Nodes
        private VBoxContainer _vbox;
        private PanelContainer _titleBar;
        private HBoxContainer _titleBarHBox;
        private Button _minimizeBtn;
        private Button _closeBtn;
        private Control _contentArea;
        #endregion

        #region Drag State
        protected bool _dragging;
        private Vector2 _dragOffset;
        #endregion

        #region Resize State
        private enum ResizeEdge { None, Left, Right, Top, Bottom, TopLeft, TopRight, BottomLeft, BottomRight }
        private ResizeEdge _detectedEdge = ResizeEdge.None;
        protected bool _resizing;
        private Vector2 _resizeStartMouse;
        private Vector2 _resizeStartPos;
        private Vector2 _resizeStartSize;
        #endregion

        #region Minimize State
        private bool _minimized;
        private float _normalHeight;
        #endregion

        #region Debounce
        private CancellationTokenSource _saveDebounceCts;
        #endregion

        #region Public API
        public bool IsMinimized => _minimized;
        public float NormalHeight => _normalHeight;
        /// <summary>是否正在交互（拖拽或调整大小）</summary>
        public bool IsInteracting => _dragging || _resizing;
        #endregion

        #region Toggle Key
        private Key _toggleKey;
        #endregion

        #region Virtual Methods (subclass overrides)
        /// <summary>节点发现完成后调用。基类自动应用主题、发现内容节点，然后调用 OnPanelInitialized</summary>
        protected virtual void OnPanelReady()
        {
            UiStyles.ApplyDarkTheme(this);
            OnPanelInitialized();
        }

        /// <summary>面板初始化完成（主题已应用），子类在此发现内容节点、订阅事件</summary>
        protected virtual void OnPanelInitialized() { }

        /// <summary>位置保存（防抖 1 秒后触发），子类覆写以持久化位置</summary>
        protected virtual void SavePosition() { }

        /// <summary>关闭按钮按下，默认隐藏面板</summary>
        protected virtual void OnClosed() { Visible = false; }

        /// <summary>最小化状态变化通知</summary>
        protected virtual void OnMinimizeChanged(bool minimized) { }

        /// <summary>获得焦点通知（面板被点击置顶时触发）</summary>
        internal protected virtual void NotifyFocusGained() { }

        /// <summary>失去焦点通知（其他面板获得焦点或点击面板外区域时触发）</summary>
        internal protected virtual void NotifyFocusLost() { }
        #endregion

        #region Lifecycle
        public override void _Ready()
        {
            DiscoverNodes();
            SetupTitleBar();
            ConnectButtons();
            _normalHeight = Size.Y;
            OnPanelReady();
            CallDeferred(MethodName.RegisterWithManager);
        }

        private void RegisterWithManager()
        {
            var pm = PanelManager.Instance;
            if (pm == null) return;
            pm.Register(this);
            if (_toggleKey != Key.Unknown)
                pm.RegisterToggleKey(_toggleKey, this);
        }

        public override void _ExitTree()
        {
            PanelManager.Instance?.Unregister(this);
            _saveDebounceCts?.Cancel();
            _saveDebounceCts?.Dispose();
        }

        private void DiscoverNodes()
        {
            _vbox = GetNodeOrNull<VBoxContainer>("VBoxContainer");
            _titleBar = GetNodeOrNull<PanelContainer>("VBoxContainer/TitleBar");
            _titleBarHBox = GetNodeOrNull<HBoxContainer>("VBoxContainer/TitleBar/HBoxContainer");
            _minimizeBtn = GetNodeOrNull<Button>("VBoxContainer/TitleBar/HBoxContainer/MinimizeButton");
            _closeBtn = GetNodeOrNull<Button>("VBoxContainer/TitleBar/HBoxContainer/CloseButton");

            // 发现内容区域（VBoxContainer 中第一个非 TitleBar 的 Control）
            if (_vbox != null)
            {
                foreach (var child in _vbox.GetChildren())
                {
                    if (child is Control c && c != _titleBar)
                    {
                        _contentArea = c;
                        break;
                    }
                }
            }

            // 按实际节点情况调整功能开关
            if (_titleBar == null) EnableDrag = false;
            if (_minimizeBtn == null) EnableMinimize = false;
            if (_closeBtn == null) EnableClose = false;
            if (_contentArea == null) EnableMinimize = false;
        }

        private void SetupTitleBar()
        {
            if (_titleBar == null) return;

            _titleBar.MouseFilter = MouseFilterEnum.Stop;

            if (_titleBarHBox != null)
            {
                _titleBarHBox.MouseFilter = MouseFilterEnum.Pass;
                foreach (var child in _titleBarHBox.GetChildren())
                {
                    if (child is not Button && child is Control c)
                        c.MouseFilter = MouseFilterEnum.Pass;
                }
            }
        }

        private void ConnectButtons()
        {
            if (EnableMinimize && _minimizeBtn != null)
                _minimizeBtn.Pressed += HandleMinimizePressed;
            if (EnableClose && _closeBtn != null)
                _closeBtn.Pressed += HandleClosePressed;
        }
        #endregion

        #region _Input - input isolation + drag/resize start + release
        public override void _Input(InputEvent @event)
        {
            if (@event is not InputEventMouseButton mb) return;
            if (mb.ButtonIndex != MouseButton.Left) return;

            var viewport = GetViewport();

            if (mb.Pressed)
            {
                // 用 Godot hover 系统检测鼠标位置（天然考虑 z-order 遮挡，只返回最顶层控件）
                var hovered = viewport?.GuiGetHoveredControl();
                ReleaseFocusedTransientDragControlOnMousePress(viewport, hovered);

                // 输入隔离：鼠标在本面板上时，消费事件防止穿透到游戏世界，并请求焦点
                // 但交互控件（Button/SpinBox 等）需要接收事件才能工作，不消费
                bool mouseOverPanel = hovered != null && (hovered == this || IsAncestorOf(hovered));
                if (mouseOverPanel)
                {
                    bool isInteractive = UiUtils.IsInteractiveControl(hovered);
                    if (!isInteractive)
                        GetViewport().SetInputAsHandled();
                    PanelManager.Instance?.RequestFocus(this);
                }

                // 标题栏拖拽开始
                bool onTitleBar = EnableDrag && _titleBar != null && hovered != null &&
                    (hovered == _titleBar || _titleBar.IsAncestorOf(hovered));
                bool onButton = hovered is Button && _titleBarHBox != null && _titleBarHBox.IsAncestorOf(hovered);

                if (onTitleBar && !onButton && !IsAnyDragging)
                {
                    _dragging = true;
                    IsAnyDragging = true;
                    _dragOffset = mb.GlobalPosition - GlobalPosition;
                    return;
                }

                // Resize 开始 — 实时验证鼠标确实在边缘区域
                if (EnableResize && mouseOverPanel && !IsAnyDragging && DebugPanelTransientFocusPolicy.ShouldStartPanelResize(hovered.GetClass()))
                {
                    var edge = DetectEdgeAtMouse();
                    if (edge != ResizeEdge.None)
                    {
                        _detectedEdge = edge;
                        StartResize();
                    }
                }
            }
            else // 释放
            {
                ReleaseFocusedTransientDragControlOnMouseRelease(viewport);

                if (_dragging)
                {
                    _dragging = false;
                    IsAnyDragging = false;
                    SavePositionDebounced();
                }
                if (_resizing)
                {
                    EndResize();
                }
            }
        }

        private static void ReleaseFocusedTransientDragControlOnMousePress(Viewport viewport, Control hovered)
        {
            var focusOwner = viewport?.GuiGetFocusOwner();
            if (focusOwner == null) return;

            bool pointerStillOnFocusOwner = hovered != null && (hovered == focusOwner || focusOwner.IsAncestorOf(hovered));
            if (DebugPanelTransientFocusPolicy.ShouldReleaseOnLeftMousePress(focusOwner.GetClass(), pointerStillOnFocusOwner))
                focusOwner.ReleaseFocus();
        }

        private static void ReleaseFocusedTransientDragControlOnMouseRelease(Viewport viewport)
        {
            var focusOwner = viewport?.GuiGetFocusOwner();
            if (focusOwner == null) return;

            if (DebugPanelTransientFocusPolicy.ShouldReleaseOnLeftMouseRelease(focusOwner.GetClass()))
                focusOwner.ReleaseFocus();
        }
        #endregion

        #region _Process - drag/resize move (polled, + safety release check)
        public override void _Process(double delta)
        {
            if (!Input.IsMouseButtonPressed(MouseButton.Left))
                ReleaseFocusedTransientDragControlOnMouseRelease(GetViewport());

            // 1. 拖拽中：更新位置
            if (_dragging)
            {
                if (Input.IsMouseButtonPressed(MouseButton.Left))
                {
                    var mousePos = GetGlobalMousePosition();
                    var newPos = mousePos - _dragOffset;
                    if (ClampToScreen)
                    {
                        var vp = GetViewport();
                        if (vp != null)
                        {
                            var ss = vp.GetVisibleRect().Size;
                            float minX = Mathf.Min(-Size.X + 100, ss.X - 20);
                            float minY = Mathf.Min(-Size.Y + 100, ss.Y - 20);
                            newPos.X = Mathf.Clamp(newPos.X, minX, ss.X - 20);
                            newPos.Y = Mathf.Clamp(newPos.Y, minY, ss.Y - 20);
                        }
                    }
                    GlobalPosition = newPos;
                }
                else
                {
                    // 安全：释放事件没到达 _Input 时自动结束
                    _dragging = false;
                    IsAnyDragging = false;
                    SavePositionDebounced();
                }
                return;
            }

            // 2. resize 中：更新大小
            if (_resizing)
            {
                if (Input.IsMouseButtonPressed(MouseButton.Left))
                    ApplyResize();
                else
                    EndResize();
                return;
            }

            // 3. 光标检测（只在未按下时）
            if (Visible && EnableResize && !Input.IsMouseButtonPressed(MouseButton.Left))
                DetectResizeEdge();
        }
        #endregion

        #region Resize
        private ResizeEdge DetectEdgeAtMouse()
        {
            var mouse = GetGlobalMousePosition();
            var rect = GetGlobalRect();

            var outer = rect.Grow(ResizeEdgeZone);
            if (!outer.HasPoint(mouse))
                return ResizeEdge.None;

            var inner = rect.Grow(-ResizeEdgeZone);
            if (inner.HasPoint(mouse))
                return ResizeEdge.None;

            bool onLeft = mouse.X < rect.Position.X + ResizeEdgeZone;
            bool onRight = mouse.X > rect.End.X - ResizeEdgeZone;
            bool onTop = mouse.Y < rect.Position.Y + ResizeEdgeZone;
            bool onBottom = mouse.Y > rect.End.Y - ResizeEdgeZone;

            if (onTop && onLeft) return ResizeEdge.TopLeft;
            if (onTop && onRight) return ResizeEdge.TopRight;
            if (onBottom && onLeft) return ResizeEdge.BottomLeft;
            if (onBottom && onRight) return ResizeEdge.BottomRight;
            if (onLeft) return ResizeEdge.Left;
            if (onRight) return ResizeEdge.Right;
            if (onTop) return ResizeEdge.Top;
            if (onBottom) return ResizeEdge.Bottom;
            return ResizeEdge.None;
        }

        private void DetectResizeEdge()
        {
            _detectedEdge = DetectEdgeAtMouse();
            SetResizeCursor(_detectedEdge);
        }

        private void SetResizeCursor(ResizeEdge edge)
        {
            var shape = edge switch
            {
                ResizeEdge.Left or ResizeEdge.Right => CursorShape.Hsize,
                ResizeEdge.Top or ResizeEdge.Bottom => CursorShape.Vsize,
                ResizeEdge.TopLeft or ResizeEdge.BottomRight => CursorShape.Fdiagsize,
                ResizeEdge.TopRight or ResizeEdge.BottomLeft => CursorShape.Bdiagsize,
                _ => CursorShape.Arrow,
            };
            MouseDefaultCursorShape = shape;
        }

        private void StartResize()
        {
            _resizing = true;
            _resizeStartMouse = GetGlobalMousePosition();
            _resizeStartPos = GlobalPosition;
            _resizeStartSize = Size;
        }

        private void ApplyResize()
        {
            var mouse = GetGlobalMousePosition();
            var delta = mouse - _resizeStartMouse;

            float x = _resizeStartPos.X;
            float y = _resizeStartPos.Y;
            float w = _resizeStartSize.X;
            float h = _resizeStartSize.Y;

            var e = _detectedEdge;
            if (e == ResizeEdge.Left || e == ResizeEdge.TopLeft || e == ResizeEdge.BottomLeft)
            {
                x = _resizeStartPos.X + delta.X;
                w = _resizeStartSize.X - delta.X;
            }
            if (e == ResizeEdge.Right || e == ResizeEdge.TopRight || e == ResizeEdge.BottomRight)
            {
                w = _resizeStartSize.X + delta.X;
            }
            if (e == ResizeEdge.Top || e == ResizeEdge.TopLeft || e == ResizeEdge.TopRight)
            {
                y = _resizeStartPos.Y + delta.Y;
                h = _resizeStartSize.Y - delta.Y;
            }
            if (e == ResizeEdge.Bottom || e == ResizeEdge.BottomLeft || e == ResizeEdge.BottomRight)
            {
                h = _resizeStartSize.Y + delta.Y;
            }

            // 最小尺寸限制
            if (w < MinWidth)
            {
                if (e == ResizeEdge.Left || e == ResizeEdge.TopLeft || e == ResizeEdge.BottomLeft)
                    x = _resizeStartPos.X + _resizeStartSize.X - MinWidth;
                w = MinWidth;
            }
            if (h < MinHeight)
            {
                if (e == ResizeEdge.Top || e == ResizeEdge.TopLeft || e == ResizeEdge.TopRight)
                    y = _resizeStartPos.Y + _resizeStartSize.Y - MinHeight;
                h = MinHeight;
            }

            GlobalPosition = new Vector2(x, y);
            Size = new Vector2(w, h);
            if (!_minimized)
                _normalHeight = h;
        }

        private void EndResize()
        {
            _resizing = false;
            SetResizeCursor(ResizeEdge.None);
            SavePositionDebounced();
        }
        #endregion

        #region Minimize / Close
        private void HandleMinimizePressed()
        {
            _minimized = !_minimized;
            if (_minimized)
            {
                _normalHeight = Size.Y;
                if (_contentArea != null && _contentArea.GetParent() == _vbox)
                {
                    // 从 VBoxContainer 移除 Content，PanelContainer 必然收缩到只剩 TitleBar
                    _vbox.RemoveChild(_contentArea);
                }
                CallDeferred(MethodName.ForceMinimizeSize, TitleBarHeight);
            }
            else
            {
                if (_contentArea != null && _contentArea.GetParent() == null)
                {
                    _vbox.AddChild(_contentArea);
                    _vbox.MoveChild(_contentArea, 1); // 放在 TitleBar 之后
                }
                CallDeferred(MethodName.ForceMinimizeSize, _normalHeight);
            }
            OnMinimizeChanged(_minimized);
        }

        /// <summary>延迟强制设置面板高度（绕过 Container 自动布局）</summary>
        private void ForceMinimizeSize(float height)
        {
            CustomMinimumSize = new Vector2(CustomMinimumSize.X, height);
            Size = new Vector2(Size.X, height);
            // 最小化时面板自身透传鼠标（只留标题栏可交互），防止遮挡其他面板
            MouseFilter = _minimized ? MouseFilterEnum.Pass : MouseFilterEnum.Stop;
        }

        private void HandleClosePressed()
        {
            OnClosed();
        }
        #endregion

        #region Position Save / Restore
        private void SavePositionDebounced()
        {
            _saveDebounceCts?.Cancel();
            _saveDebounceCts?.Dispose();
            _saveDebounceCts = new CancellationTokenSource();
            var token = _saveDebounceCts.Token;
            RunDebouncedSave(token);
        }

        private async void RunDebouncedSave(CancellationToken token)
        {
            await ToSignal(GetTree().CreateTimer(1.0), "timeout");
            if (token.IsCancellationRequested) return;
            SavePosition();
        }

        /// <summary>恢复面板位置和尺寸（供外部调用，如从服务器数据恢复）</summary>
        public void RestorePosition(float x, float y, float w, float h)
        {
            var viewport = GetViewport();
            if (viewport == null) return;
            var screenSize = viewport.GetVisibleRect().Size;

            // 视口尺寸无效时（如 headless / 启动极早阶段）跳过恢复，避免 Clamp 抛异常
            if (screenSize.X < MinWidth + 100 || screenSize.Y < MinHeight + 100)
                return;

            float px = Mathf.Clamp(x, 0, screenSize.X - 100);
            float py = Mathf.Clamp(y < 0 ? screenSize.Y - h - 20 : y, 0, screenSize.Y - 100);
            float pw = Mathf.Max(MinWidth, w);
            float ph = Mathf.Max(MinHeight, h);

            Position = new Vector2(px, py);
            Size = new Vector2(pw, ph);
            _normalHeight = ph;
        }

        /// <summary>鼠标是否在面板区域上</summary>
        public bool IsMouseOver()
        {
            return Visible && GetGlobalRect().HasPoint(GetGlobalMousePosition());
        }

        /// <summary>显示/隐藏切换</summary>
        public void Toggle()
        {
            Visible = !Visible;
            if (Visible)
                PanelManager.Instance?.RequestFocus(this);
        }

        /// <summary>注册快捷键（在 OnPanelReady 中调用）</summary>
        protected void SetToggleKey(Key key)
        {
            _toggleKey = key;
        }
        #endregion

        #region IPanel Implementation
        bool IPanel.IsVisible() => Visible;
        void IPanel.ShowPanel() => Visible = true;
        void IPanel.HidePanel() => Visible = false;
        string IPanel.PanelName => Name;
        #endregion
    }
}
