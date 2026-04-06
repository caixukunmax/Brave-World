using Godot;
using System.Collections.Generic;

namespace ClinetCSharp
{
    /// <summary>
    /// 相机控制器
    /// </summary>
    [GlobalClass]
    public partial class CameraController : Camera2D
    {
        [Export] public Node2D Target { get; set; }
        [Export] public float SmoothSpeed { get; set; } = 10.0f;
        [Export] public float ReturnDelay { get; set; } = 0.5f;  // 松手后恢复跟随的延迟（秒）
        [Export] public float ReturnSpeed { get; set; } = 5.0f;  // 镜头回到玩家身上的移动速度
        [Export] public MouseButton DragButton { get; set; } = MouseButton.Right;  // 拖拽按键

        // 缓动曲线类型
        public enum EaseType
        {
            Linear,      // 线性
            SmoothStep,  // 平滑
            EaseOut,     // 缓出（快→慢）
            EaseIn,      // 缓入（慢→快）
            EaseInOut    // 缓入缓出
        }

        [Export] public EaseType CurrentEaseType { get; set; } = EaseType.EaseOut;
        [Export] public float EasePower { get; set; } = 2.0f;  // 缓动强度（1.0-5.0）

        // 拖拽状态
        public bool IsDragging { get; private set; } = false;          // 是否正在拖拽
        public bool IsReturning { get; private set; } = false;         // 是否处于延迟恢复期
        private Vector2 _dragStartMousePos;      // 拖拽开始时的鼠标位置
        private Vector2 _dragStartCameraPos;     // 拖拽开始时的相机位置
        private float _dragTimer = 0.0f;            // 延迟恢复计时器

        // 支持的拖拽按键（默认左键拖动视野）
        public System.Collections.Generic.List<MouseButton> DragButtons { get; set; } = new() { MouseButton.Left };

        // 调试开关
        public bool DebugDrag { get; set; } = false;           // 打印拖拽调试信息（关闭以提高性能）

        // ============ 地图编辑模式 ============
        public bool IsEditorMode { get; set; } = false;       // 地图编辑模式
        public bool FreeLookMode { get; set; } = false;       // 自由视角模式（正常模式下允许滚轮缩放）
        public float MinZoom { get; set; } = 0.2f;              // 最小缩放（限制视野最远位置，防止网格线渲染异常）
        public float MaxZoom { get; set; } = 3.0f;              // 最大缩放
        public float ZoomSpeed { get; set; } = 0.1f;            // 缩放速度

        // 线宽校准限制的视野范围
        public bool CalibrationZoomLimitsEnabled { get; set; } = false;
        public float CalibrationMinZoom { get; set; } = 0.2f;
        public float CalibrationMaxZoom { get; set; } = 3.0f;

        // 保存编辑模式前的状态
        private Godot.Collections.Dictionary _savedState;

        public override async void _Ready()
        {
            Enabled = true;
            // 等待一帧确保玩家初始化完成
            await ToSignal(GetTree(), "process_frame");

            // 自动查找玩家作为目标
            if (Target == null)
            {
                Target = GetParent()?.GetNode<Node2D>("Player");
            }

            if (Target != null)
            {
                var player = Target as Player;
                var gridPosStr = player != null ? player.GridPos.ToString() : "N/A";
                GD.Print("[Camera] 初始化 - Player position: " + Target.Position + " grid_pos: " + gridPosStr);
                // 立即设置到目标位置（不使用lerp初始化）
                Position = Target.Position;
            }
            else
            {
                GD.Print("[Camera] 错误：未找到 Player 节点");
            }

            // 打印拖拽按键信息
            GD.Print("[Camera] 支持的拖拽按键: [" + string.Join(", ", DragButtons) + "] (1=左键, 2=右键, 3=中键)");
            GD.Print("[Camera] 当前是否为当前相机: " + IsCurrent());
        }

        private bool IsMouseOnDebugPanel()
        {
            // 检查鼠标是否在调试面板上
            var debugPanel = GetTree().GetFirstNodeInGroup("debug_panel");
            if (debugPanel == null)
                return false;

            // 检查 panel 节点是否存在且可见
            var panel = debugPanel.Get("panel").AsGodotObject() as Control;
            if (panel == null)
                return false;

            if (!panel.Visible)
                return false;

            var panelRect = panel.GetGlobalRect();
            var mousePos = GetViewport().GetMousePosition();
            return panelRect.HasPoint(mousePos);
        }

        public override void _Input(InputEvent @event)
        {
            // 处理拖拽结束（即使在调试面板上也要处理，防止拖拽状态卡住）
            if (@event is InputEventMouseButton mb && !mb.Pressed)
            {
                if (DragButtons.Contains(mb.ButtonIndex) && IsDragging)
                {
                    EndDrag();
                    return;
                }
            }

            // 检查鼠标是否在调试面板上 - 如果在面板上，不处理其他输入
            if (IsMouseOnDebugPanel())
                return;

            // 处理滚轮缩放（仅在自由视角模式下可用）
            if (FreeLookMode && @event is InputEventMouseButton mouseBtn)
            {
                if (mouseBtn.ButtonIndex == MouseButton.WheelUp && mouseBtn.Pressed)
                {
                    ZoomAtMouse(ZoomSpeed);
                    return;
                }
                if (mouseBtn.ButtonIndex == MouseButton.WheelDown && mouseBtn.Pressed)
                {
                    ZoomAtMouse(-ZoomSpeed);
                    return;
                }
            }

            // 调试：打印所有鼠标按键事件
            if (DebugDrag && @event is InputEventMouseButton debugMb)
            {
                if (debugMb.ButtonIndex >= MouseButton.Left && debugMb.ButtonIndex <= MouseButton.Middle)
                {
                    GD.Print("[Camera._input] 鼠标按键事件: button=" + debugMb.ButtonIndex + " pressed=" + debugMb.Pressed + " 支持的按键: [" + string.Join(", ", DragButtons) + "]");
                }
            }

            // 处理拖拽按键（支持右键、左键、中键任意一个）
            if (@event is InputEventMouseButton eventMb)
            {
                if (DragButtons.Contains(eventMb.ButtonIndex))
                {
                    if (eventMb.Pressed)
                    {
                        // 开始拖拽
                        StartDrag();
                    }
                }
            }
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            // 检查鼠标是否在调试面板上
            if (IsMouseOnDebugPanel())
                return;

            // 备选：如果 _input 被其他节点消耗，尝试在这里处理
            if (@event is InputEventMouseButton mb)
            {
                if (DragButtons.Contains(mb.ButtonIndex))
                {
                    if (DebugDrag)
                        GD.Print("[Camera._unhandled_input] 接收到未处理事件: " + @event);
                    if (mb.Pressed)
                        StartDrag();
                    else
                        EndDrag();
                }
            }
        }

        private void StartDrag()
        {
            // 如果已经在拖拽中，不要重复设置起始位置（避免抖动）
            if (IsDragging)
                return;

            // 如果正在回归倒计时中，再次拖拽会取消回归
            if (IsReturning)
            {
                IsReturning = false;
                _dragTimer = 0.0f;
                if (DebugDrag)
                    GD.Print("[Camera] 拖拽打断回归倒计时，重新开始");
            }

            // 检查鼠标是否在调试面板上（最严格的检查）
            if (IsMouseOnDebugPanel())
            {
                if (DebugDrag)
                    GD.Print("[Camera] 鼠标在调试面板上，忽略拖拽");
                return;
            }

            // 检查鼠标是否在交互式UI控件上（如Slider、Button等）
            var hovered = GetViewport().GuiGetHoveredControl();
            if (IsInteractiveUi(hovered))
            {
                if (DebugDrag)
                    GD.Print("[Camera] 鼠标在交互式UI上，忽略拖拽: " + hovered.Name);
                return;
            }

            // 编辑模式下，按住Ctrl时留给地图编辑器框选，不进行拖拽
            if (IsEditorMode && Input.IsKeyPressed(Key.Ctrl))
            {
                if (DebugDrag)
                    GD.Print("[Camera] 编辑模式下按住Ctrl，留给编辑器框选，不进行拖拽");
                return;
            }

            IsDragging = true;
            IsReturning = false;
            _dragStartMousePos = GetGlobalMousePosition();
            _dragStartCameraPos = Position;
            if (DebugDrag)
                GD.Print("[Camera] 开始拖拽 - 鼠标位置: " + _dragStartMousePos + " 相机位置: " + _dragStartCameraPos);
        }

        // 检查控件是否为交互式UI（应该阻止拖拽的）
        private bool IsInteractiveUi(Control control)
        {
            return UiUtils.IsInteractiveControl(control);
        }

        private void EndDrag()
        {
            // 如果不在拖拽中，不要重复处理（避免抖动）
            if (!IsDragging)
                return;

            IsDragging = false;
            IsReturning = true;
            _dragTimer = 0.0f;
            if (DebugDrag)
                GD.Print("[Camera] 结束拖拽 - 开始恢复计时, 延迟: " + ReturnDelay + "秒");
            else
                GD.Print("[Camera] 拖拽结束，" + ReturnDelay + "秒后开始回归...");
        }

        public void SetReturnDelay(float delay)
        {
            ReturnDelay = delay;
            if (DebugDrag)
                GD.Print("[Camera] 设置恢复延迟: " + delay + "秒");
        }

        public float GetReturnDelay() => ReturnDelay;

        public void SetReturnSpeed(float speed)
        {
            ReturnSpeed = speed;
            if (DebugDrag)
                GD.Print("[Camera] 设置回退速度: " + speed);
        }

        public float GetReturnSpeed() => ReturnSpeed;

        public void SetEaseType(EaseType type)
        {
            CurrentEaseType = type;
            if (DebugDrag)
                GD.Print("[Camera] 设置缓动曲线: " + type);
        }

        public EaseType GetEaseType() => CurrentEaseType;

        public void SetEasePower(float power)
        {
            EasePower = Mathf.Clamp(power, 1.0f, 5.0f);
            if (DebugDrag)
                GD.Print("[Camera] 设置缓动强度: " + EasePower);
        }

        public float GetEasePower() => EasePower;

        // ============ 地图编辑模式控制 ============

        public void SetEditorMode(bool enabled)
        {
            IsEditorMode = enabled;
            if (enabled)
            {
                // 进入编辑模式：保存当前状态
                _savedState = new Godot.Collections.Dictionary()
                {
                    ["zoom"] = Zoom,
                    ["position"] = Position,
                    ["is_returning"] = IsReturning
                };
                IsReturning = false;
                _dragTimer = 0.0f;
                GD.Print("[Camera] 进入编辑模式 - 自由视角，已保存状态");
            }
            else
            {
                // 退出编辑模式：恢复保存的状态
                if (_savedState != null)
                {
                    if (_savedState.ContainsKey("zoom"))
                        Zoom = _savedState["zoom"].AsVector2();
                    if (_savedState.ContainsKey("position"))
                        Position = _savedState["position"].AsVector2();
                }
                // 恢复跟随
                if (Target != null)
                {
                    IsReturning = true;
                    _dragTimer = 0.0f;
                }
                GD.Print("[Camera] 退出编辑模式 - 恢复状态和跟随");
            }
        }

        private void ZoomAtMouse(float deltaZoom)
        {
            // 在鼠标位置进行缩放
            var mousePosBefore = GetGlobalMousePosition();

            var newZoom = Zoom.X + deltaZoom;
            // 应用基础 zoom 限制
            newZoom = Mathf.Clamp(newZoom, MinZoom, MaxZoom);
            // 如果启用了校准限制，再应用校准的 zoom 范围
            if (CalibrationZoomLimitsEnabled)
                newZoom = Mathf.Clamp(newZoom, CalibrationMinZoom, CalibrationMaxZoom);
            Zoom = new Vector2(newZoom, newZoom);

            // 调整位置使鼠标指向的 world 点保持不变
            var mousePosAfter = GetGlobalMousePosition();
            Position += mousePosBefore - mousePosAfter;
        }

        public void SetEditorZoomLimits(float minZ, float maxZ)
        {
            MinZoom = minZ;
            MaxZoom = maxZ;
        }

        public void SetCalibrationZoomLimits(bool enabled, float minZ = 0.2f, float maxZ = 3.0f)
        {
            // 设置线宽校准的 zoom 限制范围
            CalibrationZoomLimitsEnabled = enabled;
            CalibrationMinZoom = minZ;
            CalibrationMaxZoom = maxZ;
            // 如果当前 zoom 超出新范围，立即调整
            if (enabled)
            {
                var newZoom = Mathf.Clamp(Zoom.X, CalibrationMinZoom, CalibrationMaxZoom);
                if (!Mathf.IsEqualApprox(newZoom, Zoom.X))
                    Zoom = new Vector2(newZoom, newZoom);
            }
        }

        public void SetEditorZoomSpeed(float speed)
        {
            ZoomSpeed = speed;
        }

        // ============ 处理循环 ============

        public override void _Process(double delta)
        {
            // 检查鼠标是否在调试面板上
            var onDebugPanel = IsMouseOnDebugPanel();

            if (IsEditorMode)
            {
                // 编辑模式：不自动跟随，只处理拖拽
                // 如果在调试面板上，停止拖拽更新（防止抖动）
                if (IsDragging && !onDebugPanel)
                {
                    var currentMouse = GetGlobalMousePosition();
                    var offset = currentMouse - _dragStartMousePos;
                    Position = _dragStartCameraPos - offset;
                }
                // 编辑模式下也对齐像素
                if (!IsDragging)
                    Position = Position.Round();
                return;
            }

            if (IsDragging)
            {
                // 拖拽模式：根据鼠标偏移移动相机
                // 如果在调试面板上，停止拖拽更新（防止抖动）
                if (!onDebugPanel)
                {
                    var currentMouse = GetGlobalMousePosition();
                    var offset = currentMouse - _dragStartMousePos;
                    Position = _dragStartCameraPos - offset;
                    // 拖拽时也对齐像素，防止子像素闪烁
                    Position = Position.Round();

                    // 调试：每60帧打印一次位置变化
                    if (DebugDrag && Engine.GetProcessFrames() % 60 == 0)
                        GD.Print("[Camera] 拖拽中 - 鼠标偏移: " + offset + " 当前位置: " + Position);
                }

                // 拖拽期间绝对不触发回归，重置计时器
                IsReturning = false;
                _dragTimer = 0.0f;
            }
            else if (IsReturning)
            {
                // 延迟恢复期
                _dragTimer += (float)delta;
                if (_dragTimer >= ReturnDelay)
                {
                    IsReturning = false;
                    if (DebugDrag)
                        GD.Print("[Camera] 恢复计时结束，恢复跟随模式");
                    else
                        GD.Print("[Camera] 开始回归...");
                }
                // 此期间不跟随，保持当前位置
            }
            else if (Target != null)
            {
                // 使用缓动曲线计算回退
                var distance = Position.DistanceTo(Target.Position);
                var t = Mathf.Clamp(ReturnSpeed * (float)delta, 0.0f, 1.0f);

                // 应用缓动曲线
                t = CurrentEaseType switch
                {
                    EaseType.Linear => t,
                    EaseType.SmoothStep => t * t * (3.0f - 2.0f * t),
                    EaseType.EaseOut => 1.0f - Mathf.Pow(1.0f - t, EasePower),
                    EaseType.EaseIn => Mathf.Pow(t, EasePower),
                    EaseType.EaseInOut => t < 0.5f
                        ? Mathf.Pow(t * 2.0f, EasePower) * 0.5f
                        : 1.0f - Mathf.Pow(2.0f - t * 2.0f, EasePower) * 0.5f,
                    _ => t
                };

                Position = Position.Lerp(Target.Position, t);
            }

            // 像素对齐：防止子像素偏移导致的闪烁
            // 只有当相机几乎静止时才对齐，保持移动时的平滑感
            if (Target != null && Position.DistanceSquaredTo(Target.Position) < 1.0f)
                Position = Position.Round();
        }

        // 切换调试输出
        public void SetDebugDrag(bool enabled)
        {
            DebugDrag = enabled;
            GD.Print("[Camera] 拖拽调试输出: " + enabled);
        }

        // 设置自由视角模式（正常模式下允许滚轮缩放）
        public void SetFreeLookMode(bool enabled)
        {
            FreeLookMode = enabled;
        }

        public void SetDragButtons(System.Collections.Generic.List<MouseButton> buttons)
        {
            // 设置拖动视野的按键（可由调试面板配置）
            DragButtons = buttons;
            GD.Print("[Camera] 拖动视野按键已设置为: [" + string.Join(", ", buttons) + "] (1=左键, 2=右键, 3=中键)");
        }
    }
}
