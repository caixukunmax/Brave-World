using Godot;
using System;

namespace ClinetCSharp
{
    /// <summary>
    /// 演出编辑器地图视口容器（SubViewportContainer 子类）。输入处理放在容器上，
    /// 不依赖 SubViewport 内部节点转发——与 MapEditViewport 相同的理由：
    /// 编辑器插件下 SubViewport 内节点的输入事件经常收不到。
    ///
    /// 交互：
    ///   滚轮 = 以鼠标为锚点缩放；中键或左键拖拽 = 平移；
    ///   拾取模式（Cell）：左键单击 = 把格子坐标回传给表单；
    ///   框选模式（Rect）：左键拖拽 = 画矩形，松开回传 trigger.rect；
    ///   右键 = 取消当前拾取/框选。
    /// </summary>
    [Tool]
    public partial class CutsceneMapView : SubViewportContainer
    {
        public enum PickMode
        {
            None,   // 普通浏览（拖拽平移）
            Cell,   // 单击拾取格子坐标
            Rect,   // 拖拽框选触发区
        }

        public SubViewport Sub;
        public Camera2D Camera;
        public CutsceneMapRenderer Renderer;
        public PickMode Mode = PickMode.None;
        /// <summary>游戏视口宽高比（由 CutsceneDock 按 project.godot display/window/size 注入），用于布局下限</summary>
        public float GameAspect = 16f / 9f;
        /// <summary>是否处于预览模式。预览时锁定所有手动相机操作（滚轮缩放/平移/拾取），
        /// 让输入交给预览播放器（仅对白"点击任意处继续"生效），避免预览中手动改机位。</summary>
        public bool IsPreviewing { get; set; }

        /// <summary>拾取模式下左键单击命中格子</summary>
        public event Action<Vector2I> CellPicked;
        /// <summary>框选模式下松开左键得到矩形（格子坐标，已归一化）</summary>
        public event Action<Rect2I> RectPicked;
        /// <summary>右键取消拾取/框选</summary>
        public event Action PickCancelled;

        private bool _leftDown;
        private bool _leftMoved;
        private bool _isPanning;
        private Vector2 _panStartMouse;
        private Vector2 _panStartCam;
        private Vector2I _rectStart;

        public override void _Ready()
        {
            MouseFilter = MouseFilterEnum.Stop;
        }

        // 布局最小尺寸与 SubViewport 渲染尺寸解耦：SubViewportContainer 在 Stretch=false 时
        // 会把 SubViewport.Size 当作自身最小尺寸（本插件会同步成容器实际大小），不拦截会把
        // 整个 Dock 顶到屏幕外（预览按钮被挤出可视区的根因）。
        // 下限同样按游戏宽高比给（16:9 时 320x180），保证任何尺寸下视口画面都不失真
        public override Vector2 _GetMinimumSize() => new(320, 320f / GameAspect);

        public override void _GuiInput(InputEvent @event)
        {
            // 预览模式下锁定所有手动相机操作：滚轮缩放/平移/拾取一律不处理，
            // 仅让对白点击继续（由上层预览播放器的 dialogueClickCatcher 接管）。
            if (IsPreviewing) return;
            if (Renderer == null || Camera == null || Sub == null) return;

            // ---- 右键：取消拾取/框选 ----
            if (@event is InputEventMouseButton rmb && rmb.ButtonIndex == MouseButton.Right)
            {
                if (rmb.Pressed && Mode != PickMode.None)
                {
                    Mode = PickMode.None;
                    Renderer.RectDragPreview = null;
                    Renderer.QueueRedraw();
                    PickCancelled?.Invoke();
                }
                GetViewport()?.SetInputAsHandled();
                return;
            }

            // ---- 滚轮缩放（以鼠标为锚点，强制等比，同 MapEditViewport） ----
            if (@event is InputEventMouseButton wheel &&
                (wheel.ButtonIndex == MouseButton.WheelUp || wheel.ButtonIndex == MouseButton.WheelDown))
            {
                float factor = wheel.ButtonIndex == MouseButton.WheelUp ? 1.1f : 1f / 1.1f;
                float z = Mathf.Clamp(Camera.Zoom.X * factor, 0.2f, 4f);
                var newZoom = new Vector2(z, z);
                var mouse = SubMouse();
                var viewCenter = (Vector2)Sub.Size / 2f;
                var worldBefore = (mouse - viewCenter) / Camera.Zoom + Camera.GlobalPosition;
                Camera.Zoom = newZoom;
                Camera.GlobalPosition = worldBefore - (mouse - viewCenter) / newZoom;
                GetViewport()?.SetInputAsHandled();
                return;
            }

            // ---- 中键平移 ----
            if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Middle)
            {
                if (mb.Pressed)
                {
                    _isPanning = true;
                    _panStartMouse = SubMouse();
                    _panStartCam = Camera.GlobalPosition;
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
                if (lmb.Pressed)
                {
                    _leftDown = true;
                    _leftMoved = false;
                    _panStartMouse = SubMouse();
                    _panStartCam = Camera.GlobalPosition;
                    if (Mode == PickMode.Rect)
                    {
                        _rectStart = GetGridPos();
                        Renderer.RectDragPreview = new Rect2I(_rectStart, new Vector2I(1, 1));
                        Renderer.QueueRedraw();
                    }
                }
                else
                {
                    if (Mode == PickMode.Rect)
                    {
                        // 框选完成：归一化矩形（最小 1x1）后回传
                        var end = GetGridPos();
                        var rect = NormalizeRect(_rectStart, end);
                        Mode = PickMode.None;
                        Renderer.RectDragPreview = null;
                        Renderer.QueueRedraw();
                        RectPicked?.Invoke(rect);
                    }
                    else if (Mode == PickMode.Cell && !_leftMoved)
                    {
                        Mode = PickMode.None;
                        CellPicked?.Invoke(GetGridPos());
                    }
                    _leftDown = false;
                }
                GetViewport()?.SetInputAsHandled();
                return;
            }

            // ---- 移动：框选更新 / 平移 ----
            if (@event is InputEventMouseMotion)
            {
                if (_leftDown && Mode == PickMode.Rect)
                {
                    Renderer.RectDragPreview = NormalizeRect(_rectStart, GetGridPos());
                    Renderer.QueueRedraw();
                }
                else if ((_leftDown && Mode != PickMode.Rect) || _isPanning)
                {
                    var delta = SubMouse() - _panStartMouse;
                    if (delta.Length() > 3f) _leftMoved = true;
                    Camera.GlobalPosition = _panStartCam - delta / Camera.Zoom;
                }
                GetViewport()?.SetInputAsHandled();
                return;
            }
        }

        /// <summary>由拖拽起止格归一化出矩形（含起止格，最小 1x1）</summary>
        private static Rect2I NormalizeRect(Vector2I a, Vector2I b)
        {
            int x = Mathf.Min(a.X, b.X);
            int y = Mathf.Min(a.Y, b.Y);
            return new Rect2I(x, y, Mathf.Abs(a.X - b.X) + 1, Mathf.Abs(a.Y - b.Y) + 1);
        }

        private Vector2I GetGridPos()
        {
            // 容器鼠标位置经 SubMouse() 换算到 SubViewport 内部坐标，再经 Camera2D 变换反推世界坐标
            var mouse = SubMouse();
            var world = (mouse - (Vector2)Sub.Size / 2f) / Camera.Zoom + Camera.GlobalPosition;
            return new Vector2I(
                Mathf.FloorToInt(world.X / CutsceneMapRenderer.GridSize),
                Mathf.FloorToInt(world.Y / CutsceneMapRenderer.GridSize));
        }

        private Vector2 SubMouse()
        {
            // 容器（拉伸显示）坐标 -> SubViewport 内部坐标：按比例换算，使鼠标映射与渲染一致
            var mouse = GetGlobalMousePosition() - GlobalPosition;
            var c = (Vector2)Size;
            if (Sub == null || c.X <= 0.0001f || c.Y <= 0.0001f) return mouse;
            return mouse * (Vector2)Sub.Size / c;
        }
    }
}
