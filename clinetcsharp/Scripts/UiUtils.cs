using Godot;
using Godot.Collections;

namespace ClinetCSharp
{
    /// <summary>
    /// UI 工具类 - 共用的 UI 检测、绘制、坐标转换方法
    /// </summary>
    public static class UiUtils
    {
        /// <summary>
        /// Godot.Collections.Dictionary 的 GetValueOrDefault 扩展方法
        /// </summary>
        public static Variant GetValueOrDefault(this Dictionary dict, string key, Variant defaultValue)
        {
            return dict.ContainsKey(key) ? dict[key] : defaultValue;
        }

        /// <summary>
        /// 清空节点的所有子节点（替代重复的 foreach + QueueFree 模式）。
        /// </summary>
        public static void ClearChildren(this Node parent)
        {
            if (parent == null) return;
            foreach (var child in parent.GetChildren())
                child.QueueFree();
        }

        /// <summary>
        /// 创建一个水平排列的 Label + 控件行。
        /// labelWidth 为 0 表示自适应。
        /// </summary>
        public static HBoxContainer MakeLabelRow(string labelText, Control child, float labelWidth = 0)
        {
            var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            var label = new Label { Text = labelText };
            if (labelWidth > 0)
                label.CustomMinimumSize = new Vector2(labelWidth, 0);
            row.AddChild(label);
            if (child != null)
            {
                child.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                row.AddChild(child);
            }
            return row;
        }

        /// <summary>
        /// 将控件及其所有子控件设为 MouseFilter.Ignore（鼠标事件穿透）。
        /// 用于纯展示面板（如战斗信息条、血条等），避免拦截鼠标事件影响其他面板拖拽。
        /// </summary>
        public static void SetMousePassthrough(Control root)
        {
            if (root == null) return;
            root.MouseFilter = Control.MouseFilterEnum.Ignore;
            foreach (var child in root.GetChildren())
            {
                if (child is Control c)
                    SetMousePassthrough(c);
            }
        }

        /// <summary>
        /// 统一输入门控：判断鼠标是否在任何 UI 面板/控件上。
        /// 新版实现委托给 UIInputPolicy；如果单例尚未就绪，则退回到 viewport 检测。
        /// </summary>
        public static bool IsMouseOverAnyUi(Viewport viewport)
        {
            var policy = UIInputPolicy.Instance;
            if (policy != null)
                return policy.IsMouseOverInteractiveUi();

            // 兼容兜底：UIInputPolicy 初始化前按原逻辑判断
            if (viewport == null) return false;
            return viewport.GuiGetHoveredControl() != null;
        }

        /// <summary>
        /// 判断鼠标是否位于交互式 UI 上（优先使用 UIInputPolicy 的统一策略）。
        /// </summary>
        public static bool IsMouseOverInteractiveUi(Viewport viewport)
        {
            return IsMouseOverAnyUi(viewport);
        }

        /// <summary>
        /// 检查控件是否为交互式 UI（应阻止游戏输入的控件类型）
        /// </summary>
        public static bool IsInteractiveControl(Control control)
        {
            if (control == null)
                return false;

            string[] interactiveTypes = new[]
            {
                "Slider", "HSlider", "VSlider", "SpinBox", "ProgressBar",
                "ScrollBar", "HScrollBar", "VScrollBar",
                "Button", "CheckButton", "CheckBox", "OptionButton", "MenuButton",
                "LineEdit", "TextEdit", "CodeEdit",
                "TabBar", "TabContainer", "ItemList", "Tree"
            };

            var className = control.GetClass();
            foreach (var type in interactiveTypes)
            {
                if (type == className || control.IsClass(type))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 检查当前 GUI 焦点是否在文本输入控件上（LineEdit、TextEdit、CodeEdit、SpinBox）。
        /// 当玩家正在与这些控件交互时，游戏层应屏蔽方向键/字符键输入，避免输入穿透。
        /// </summary>
        public static bool IsGuiTextInputFocused(Viewport viewport)
        {
            if (viewport == null) return false;
            var focusOwner = viewport.GuiGetFocusOwner();
            if (focusOwner == null) return false;
            return focusOwner is LineEdit or TextEdit or CodeEdit or SpinBox;
        }

        /// <summary>
        /// Disable keyboard/focus retention for transient drag controls such as sliders and scroll bars.
        /// These controls should react only while the pointer is actively dragging them; after release,
        /// mouse movement must not keep affecting them through retained focus.
        /// </summary>
        public static void ConfigureTransientDragControlFocus(Control root)
        {
            if (root == null) return;

            if (DebugPanelTransientFocusPolicy.ShouldDisableFocusMode(root.GetClass()))
                root.FocusMode = Control.FocusModeEnum.None;

            if (root is ScrollContainer scroll)
            {
                ConfigureTransientDragControlFocus(scroll.GetVScrollBar());
                ConfigureTransientDragControlFocus(scroll.GetHScrollBar());
            }

            foreach (var child in root.GetChildren())
            {
                if (child is Control c)
                    ConfigureTransientDragControlFocus(c);
            }
        }

        // ========== 坐标转换 ==========

        /// <summary>
        /// 网格坐标 → 世界坐标（格子中心）
        /// </summary>
        public static Vector2 GridToWorld(int x, int y, int gridSize)
        {
            // 对齐到整数像素，避免半像素位置导致亚像素抖动
            return new Vector2(Mathf.RoundToInt(x * gridSize + gridSize / 2.0f),
                               Mathf.RoundToInt(y * gridSize + gridSize / 2.0f));
        }

        public static Vector2 GridToWorld(Vector2I pos, int gridSize)
        {
            return GridToWorld(pos.X, pos.Y, gridSize);
        }

        // ========== 绘制辅助（CanvasItem 扩展方法）==========

        /// <summary>
        /// 绘制扇形（用于圆角矩形四角）
        /// </summary>
        public static void DrawCornerSector(this CanvasItem canvas, float cx, float cy, float r, float startAngle, float endAngle, Color color)
        {
            var points = new Vector2[10];
            points[0] = new Vector2(cx, cy);
            const int segments = 8;
            for (int i = 0; i <= segments; i++)
            {
                var angle = startAngle + (endAngle - startAngle) * (i / (float)segments);
                points[i + 1] = new Vector2(cx + Mathf.Cos(angle) * r, cy + Mathf.Sin(angle) * r);
            }
            var colorArray = new Color[points.Length];
            for (int i = 0; i < colorArray.Length; i++)
                colorArray[i] = color;
            canvas.DrawPolygon(points, colorArray);
        }

        /// <summary>
        /// 绘制圆角矩形（填充或描边）
        /// </summary>
        public static void DrawRoundedRect(this CanvasItem canvas, Rect2 rect, Color color, bool filled, float radius, float width = -1.0f)
        {
            var x = rect.Position.X;
            var y = rect.Position.Y;
            var w = rect.Size.X;
            var h = rect.Size.Y;
            var r = Mathf.Min(radius, Mathf.Min(w, h) / 2.0f);

            if (filled)
            {
                canvas.DrawRect(new Rect2(x + r, y + r, w - r * 2, h - r * 2), color, true);
                canvas.DrawRect(new Rect2(x + r, y, w - r * 2, r), color, true);
                canvas.DrawRect(new Rect2(x + r, y + h - r, w - r * 2, r), color, true);
                canvas.DrawRect(new Rect2(x, y + r, r, h - r * 2), color, true);
                canvas.DrawRect(new Rect2(x + w - r, y + r, r, h - r * 2), color, true);
                canvas.DrawCornerSector(x + r, y + r, r, Mathf.Pi, 1.5f * Mathf.Pi, color);
                canvas.DrawCornerSector(x + w - r, y + r, r, 1.5f * Mathf.Pi, 2 * Mathf.Pi, color);
                canvas.DrawCornerSector(x + r, y + h - r, r, 0.5f * Mathf.Pi, Mathf.Pi, color);
                canvas.DrawCornerSector(x + w - r, y + h - r, r, 0, 0.5f * Mathf.Pi, color);
            }
            else
            {
                const int segments = 8;
                var points = new System.Collections.Generic.List<Vector2>();
                for (int i = 0; i <= segments; i++)
                {
                    var angle = Mathf.Pi + (Mathf.Pi / 2) * (i / (float)segments);
                    points.Add(new Vector2(x + r + Mathf.Cos(angle) * r, y + r + Mathf.Sin(angle) * r));
                }
                for (int i = 0; i <= segments; i++)
                {
                    var angle = 1.5f * Mathf.Pi + (Mathf.Pi / 2) * (i / (float)segments);
                    points.Add(new Vector2(x + w - r + Mathf.Cos(angle) * r, y + r + Mathf.Sin(angle) * r));
                }
                for (int i = 0; i <= segments; i++)
                {
                    var angle = 0 + (Mathf.Pi / 2) * (i / (float)segments);
                    points.Add(new Vector2(x + w - r + Mathf.Cos(angle) * r, y + h - r + Mathf.Sin(angle) * r));
                }
                for (int i = 0; i <= segments; i++)
                {
                    var angle = 0.5f * Mathf.Pi + (Mathf.Pi / 2) * (i / (float)segments);
                    points.Add(new Vector2(x + r + Mathf.Cos(angle) * r, y + h - r + Mathf.Sin(angle) * r));
                }
                if (points.Count > 0)
                    points.Add(points[0]);
                canvas.DrawPolyline(points.ToArray(), color, width);
            }
        }
    }
}
