using Godot;

namespace ClinetCSharp
{
    /// <summary>
    /// 地图编辑器视口容器（SubViewportContainer 子类）。直接在编辑器 GUI 树里接收鼠标输入，
    /// 不依赖 SubViewport 内部节点转发——SubViewport 内的 Node2D/Control 的 _UnhandledInput / _GuiInput
    /// 在编辑器插件下经常收不到被转发进来的事件，导致无法平移/缩放。
    ///
    /// 这里用 _GuiInput 统一处理导航与工具：
    ///   左键拖拽 = 平移；滚轮 = 以鼠标为锚点缩放；Ctrl+左键拖拽 = 框选；右键 = 取消框选/搬运。
    ///   PlaceDecoration 工具下：左键单击放置新装饰 / 单击已有装饰拾起（再单击落下，移动时跟随预览）。
    /// 坐标换算：开启 Stretch 后 SubViewport 拉伸填满容器，鼠标需经 SubMouse() 按比例换算到内部坐标，
    /// 再经 Camera2D 变换反推世界坐标。
    /// </summary>
    [Tool]
    public partial class MapEditViewport : SubViewportContainer
    {
        public MapEditController Controller;
        public GridManager Grid;
        public Camera2D Camera;
        public MapEditDecorationOverlay Overlay;
        public MapEditorBottomPanel Panel;
        public SubViewport Sub;

        public MapEditController.MapEditTool CurrentTool = MapEditController.MapEditTool.PaintTerrain;
        public int CurrentDecorationId;

        /// <summary>游戏视口宽高比（由 MapEditorBottomPanel 从 project.godot 注入），用于布局下限。</summary>
        public float GameAspect = 16f / 9f;

        // 布局最小尺寸与 SubViewport 渲染尺寸解耦：SubViewportContainer 在 Stretch=false 时
        // 会把 SubViewport.Size 当作自身最小尺寸，不拦截会把整个 Dock 顶到屏幕外，
        // 导致 Godot 编辑器底部的“输出/调试器/动画/音频”标签栏被挤出可视区、无法点击。
        // 这里按游戏宽高比给出一个固定下限（16:9 时 320x180），真实渲染尺寸由 Resized 回调同步。
        public override Vector2 _GetMinimumSize() => new(320, 320f / GameAspect);

        private bool _isPanning;
        private Vector2 _panStartMouse;
        private Vector2 _panStartCam;
        private bool _leftDown;
        private bool _ctrlHeld;
        private bool _leftMoved;
        private int _dragType;
        private Vector2I? _carrying;   // 搬运中的装饰锚点（PlaceDecoration 模式单击拾起，再次单击落下）

        public override void _Ready()
        {
            MouseFilter = MouseFilterEnum.Stop;
        }

        public override void _GuiInput(InputEvent @event)
        {
            if (Grid == null || Controller == null) return;

            // ---- 右键：取消框选 / 取消搬运 ----
            if (@event is InputEventMouseButton rmb && rmb.ButtonIndex == MouseButton.Right)
            {
                if (rmb.Pressed) CancelAll();
                GetViewport()?.SetInputAsHandled();
                return;
            }

            // ---- 缩放（滚轮，以鼠标位置为锚点）----
            if (@event is InputEventMouseButton wheel)
            {
                if (wheel.ButtonIndex == MouseButton.WheelUp || wheel.ButtonIndex == MouseButton.WheelDown)
                {
                    if (Camera != null && Sub != null)
                    {
                        float factor = wheel.ButtonIndex == MouseButton.WheelUp ? 1.1f : 1f / 1.1f;
                        // 强制等比：始终以 X 分量为基准重设 (z, z)。一旦 Zoom 因任何原因变成
                        // 非等比（如 (0.2, 4)），画面会被拉成竖/横条纹，这里一次性纠正回来。
                        float z = Mathf.Clamp(Camera.Zoom.X * factor, 0.2f, 4f);
                        var newZoom = new Vector2(z, z);
                        var mouse = SubMouse();
                        var viewCenter = (Vector2)Sub.Size / 2f;
                        var worldBefore = (mouse - viewCenter) / Camera.Zoom + Camera.GlobalPosition;
                        Camera.Zoom = newZoom;
                        Camera.GlobalPosition = worldBefore - (mouse - viewCenter) / newZoom;
                        Panel?.SyncZoomSlider(z);
#if DEBUG
                        GD.Print($"[MapEditViewport] 滚轮缩放: zoom={Camera.Zoom}, camPos={Camera.GlobalPosition}, subSize={Sub.Size}");
#endif
                    }
                    GetViewport()?.SetInputAsHandled();
                    return;
                }
            }

            // ---- 平移（中键，或 左键无 Ctrl 拖拽）----
            if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Middle)
            {
                if (mb.Pressed)
                {
                    _isPanning = true;
                    _panStartMouse = SubMouse();
                    _panStartCam = Camera != null ? Camera.GlobalPosition : Vector2.Zero;
                }
                else
                {
                    _isPanning = false;
                }
                GetViewport()?.SetInputAsHandled();
                return;
            }

            // ---- 左键 ----
            if (@event is InputEventMouseButton lmb && lmb.ButtonIndex == MouseButton.Left)
            {
                var gp = GetGridPos();
                if (lmb.Pressed)
                {
                    _leftDown = true;
                    _ctrlHeld = lmb.CtrlPressed;
                    _leftMoved = false;
                    _panStartMouse = SubMouse();
                    _panStartCam = Camera != null ? Camera.GlobalPosition : Vector2.Zero;

                    if (_ctrlHeld)
                    {
                        // Ctrl+左键：框选
                        Controller.BeginSelection(gp ?? SelectionStartSafe(), true);
                    }
                    else if (_carrying.HasValue)
                    {
                        // 搬运中：在点击处落下装饰
                        if (gp != null)
                            Controller.TryPlaceDecoration(gp.Value, _dragType, _carrying.Value);
                        _carrying = null;
                        if (Overlay != null) { Overlay.ShowDropPreview = false; Overlay.QueueRedraw(); }
                    }
                    // 否则：按下时不动作，等移动时平移 / 松开时判定单击
                    GetViewport()?.SetInputAsHandled();
                }
                else
                {
                    if (_ctrlHeld)
                    {
                        if (Controller.IsSelecting) Controller.EndSelection();
                    }
                    else if (!_leftMoved)
                    {
                        // 视为单击（非拖拽）
                        if (CurrentTool == MapEditController.MapEditTool.PlaceDecoration && gp != null)
                        {
                            var anchor = Controller.FindAnchorAt(gp.Value);
                            if (anchor != null)
                            {
                                // 拾起已有装饰准备搬运
                                _carrying = anchor.Value;
                                _dragType = Grid.GetCell(anchor.Value)?.DecorationType ?? 0;
                                UpdateDropPreview(gp.Value);
                            }
                            else
                            {
                                // 放置新装饰
                                Controller.TryPlaceDecoration(gp.Value, CurrentDecorationId);
                            }
                        }
                        // PaintTerrain 模式下纯左键单击无动作（框选需 Ctrl+左键）
                    }
                    _leftDown = false;
                    _ctrlHeld = false;
                    GetViewport()?.SetInputAsHandled();
                }
                return;
            }

            // ---- 移动（平移 / 框选更新 / 搬运预览）----
            if (@event is InputEventMouseMotion)
            {
                var gp = GetGridPos();
                if (_ctrlHeld && Controller.IsSelecting)
                {
                    if (gp != null) Controller.UpdateSelection(gp.Value);
                }
                else if (_carrying.HasValue)
                {
                    if (gp != null) UpdateDropPreview(gp.Value);
                }
                else if ((_leftDown && !_ctrlHeld) || _isPanning)
                {
                    if (Camera != null)
                    {
                        var delta = SubMouse() - _panStartMouse;
                        if (delta.Length() > 3f) _leftMoved = true;
                        Camera.GlobalPosition = _panStartCam - delta / Camera.Zoom;
                    }
                }
                GetViewport()?.SetInputAsHandled();
                return;
            }
        }

        private void CancelAll()
        {
            if (Controller.IsSelecting) Controller.EndSelection();
            Controller.ClearSelection();
            _carrying = null;
            if (Overlay != null) { Overlay.ShowDropPreview = false; Overlay.QueueRedraw(); }
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            // 键盘快捷键（Ctrl+Z/Y、Delete、Ctrl+A）走未处理输入通道，与鼠标分离
            if (@event is InputEventKey key && key.Pressed && !key.Echo)
            {
                if (key.CtrlPressed && key.Keycode == Key.Z) { Controller.Undo(); GetViewport()?.SetInputAsHandled(); }
                else if (key.CtrlPressed && (key.Keycode == Key.Y || (key.CtrlPressed && key.ShiftPressed && key.Keycode == Key.Z))) { Controller.Redo(); GetViewport()?.SetInputAsHandled(); }
                else if (key.Keycode == Key.Delete && CurrentTool == MapEditController.MapEditTool.PlaceDecoration)
                {
                    var gp = GetGridPos();
                    if (gp != null) Controller.DeleteDecorationAt(gp.Value);
                    GetViewport()?.SetInputAsHandled();
                }
                else if (key.CtrlPressed && key.Keycode == Key.A && CurrentTool == MapEditController.MapEditTool.PaintTerrain)
                {
                    Controller.SelectAll();
                    GetViewport()?.SetInputAsHandled();
                }
            }
        }

        private Vector2I? GetGridPos()
        {
            // 容器鼠标位置经 SubMouse() 换算到 SubViewport 内部坐标，再经 Camera2D 变换反推世界坐标
            var mouse = SubMouse();
            Vector2 world;
            if (Camera != null && Sub != null)
                world = (mouse - (Vector2)Sub.Size / 2f) / Camera.Zoom + Camera.GlobalPosition;
            else
                world = Grid.ToLocal(mouse);
            return Grid.WorldToGrid(world);
        }

        private Vector2 MouseLocal()
        {
            // 鼠标在容器（SubViewportContainer）内的本地坐标：全局鼠标位置 - 容器全局原点
            return GetGlobalMousePosition() - GlobalPosition;
        }

        private Vector2 SubMouse()
        {
            // 容器（拉伸显示）坐标 -> SubViewport 内部坐标：按比例换算，使鼠标映射与渲染一致。
            var mouse = MouseLocal();
            var c = (Vector2)Size;
            if (Sub == null || c.X <= 0.0001f || c.Y <= 0.0001f) return mouse;
            return mouse * (Vector2)Sub.Size / c;
        }

        private Vector2I SelectionStartSafe()
        {
            return Controller.SelectionStart;
        }

        private void UpdateDropPreview(Vector2I anchor)
        {
            if (Overlay == null) return;
            var (sx, sy) = Controller.GetDecorationSize(_dragType);
            Overlay.ShowDropPreview = true;
            Overlay.DropAnchor = anchor;
            Overlay.DropSizeX = sx;
            Overlay.DropSizeY = sy;

            bool valid = true;
            var fp = Controller.GetFootprintCells(anchor, sx, sy);
            foreach (var p in fp) if (!Grid.IsInBounds(p)) { valid = false; break; }
            if (valid)
            {
                // 搬运已有装饰时，排除其原始锚点避免自重叠
                var exclude = _carrying.HasValue ? (Vector2I?)_carrying.Value : null;
                if (Controller.IsFootprintOverlapping(anchor, sx, sy, exclude)) valid = false;
            }
            Overlay.DropValid = valid;
            Overlay.QueueRedraw();
        }
    }
}
