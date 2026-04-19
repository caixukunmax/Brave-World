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
        /// 检查控件是否为交互式 UI（应阻止游戏输入的控件类型）
        /// </summary>
        public static bool IsInteractiveControl(Control control)
        {
            if (control == null)
                return false;

            string[] interactiveTypes = new[]
            {
                "Slider", "HSlider", "VSlider", "SpinBox", "ProgressBar",
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

        // ========== 坐标转换 ==========

        /// <summary>
        /// 网格坐标 → 世界坐标（格子中心）
        /// </summary>
        public static Vector2 GridToWorld(int x, int y, int gridSize)
        {
            return new Vector2(x * gridSize + gridSize / 2.0f,
                               y * gridSize + gridSize / 2.0f);
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
