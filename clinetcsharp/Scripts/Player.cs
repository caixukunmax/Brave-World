using Godot;
using Godot.Collections;

namespace ClinetCSharp
{
    /// <summary>
    /// 玩家角色类
    /// </summary>
    [GlobalClass]
    public partial class Player : Node2D
    {
        [Export] public int GridSize { get; set; } = 111;
        [Export] public int VisualSize { get; set; } = 111;  // 独立的视觉大小，不影响网格
        [Export] public float BorderWidth { get; set; } = 3.0f;
        [Export] public Color BorderColor { get; set; } = Colors.White;
        [Export] public Color BgColor { get; set; } = new Color(1, 1, 1, 0.1f);
        [Export] public Color TextColor { get; set; } = Colors.Black;
        [Export] public float MoveDuration { get; set; } = 0.15f;
        [Export] public int FontSizeOverride { get; set; } = 0;  // 0 表示自动计算
        [Export] public float LineSpacing { get; set; } = 0.8f;  // 行间距系数 (0.5-1.5)
        [Export] public float CornerRadius { get; set; } = 0.0f;  // 圆角半径
        [Export] public float LetterSpacing { get; set; } = 0.0f;  // 字间距
        [Export] public float BgOpacity { get; set; } = 0.1f;  // 背景透明度 (0-1)

        // 字体设置
        public string CurrentFontPath { get; set; } = "";

        // 字体样式
        public bool FontBold { get; set; } = false;
        public bool FontItalic { get; set; } = false;
        public bool FontShadow { get; set; } = false;

        // 调试信息显示
        public bool ShowDebugInfo { get; set; } = true;

        // 每行文字的颜色（4行）
        [Export] public Array<Color> LineColors { get; set; } = new Array<Color> { Colors.Black, Colors.Black, Colors.Black, Colors.Black };

        // 角色信息（4行）
        public int Level { get; set; } = 1;
        public string CharacterName { get; set; } = "王建国";
        public string Job { get; set; } = "农夫";
        public string Title { get; set; } = "普通人";
        public string Status { get; set; } = "闲逛中...";

        public Vector2I GridPos { get; set; } = new Vector2I(25, 25);
        public bool IsMoving { get; set; } = false;
        public HorizontalAlignment TextAlignment { get; set; } = HorizontalAlignment.Center;

        private VBoxContainer _labelContainer;

        public override void _Ready()
        {
            Position = GridToWorld(GridPos);
            _labelContainer = GetNodeOrNull<VBoxContainer>("LabelContainer");
            SetupLabels();
            QueueRedraw();
        }

        /// <summary>
        /// 公共方法：刷新标签显示（供 DebugPanel 调用）
        /// </summary>
        public async void RefreshLabels()
        {
            await SetupLabelsInternal();
        }

        private async void SetupLabels()
        {
            await SetupLabelsInternal();
        }

        private async System.Threading.Tasks.Task SetupLabelsInternal()
        {
            if (_labelContainer != null)
            {
                // 清空现有标签
                foreach (Node child in _labelContainer.GetChildren())
                {
                    child.QueueFree();
                }

                // 设置 VBoxContainer 对齐方式为居中
                _labelContainer.Alignment = BoxContainer.AlignmentMode.Center;

                // 创建4行标签
                var lines = new[]
                {
                    $"Lv.{Level} {CharacterName}",
                    Job,
                    Title,
                    Status
                };

                for (int i = 0; i < lines.Length; i++)
                {
                    var label = new RichTextLabel();
                    label.FitContent = true;
                    label.ScrollActive = false;
                    label.BbcodeEnabled = true;
                    label.Text = lines[i];
                    label.HorizontalAlignment = TextAlignment;
                    label.VerticalAlignment = VerticalAlignment.Center;
                    // 使用每行单独的颜色，如果没有设置则使用默认 text_color
                    var lineColor = TextColor;
                    if (i < LineColors.Count)
                        lineColor = LineColors[i];
                    label.AddThemeColorOverride("font_color", lineColor);
                    _labelContainer.AddChild(label);
                }

                // 等待一帧让 Godot 完成布局计算
                await ToSignal(GetTree(), "process_frame");
                UpdateLabelFontSize();
            }
        }

        private void UpdateLabelFontSize()
        {
            if (_labelContainer == null) return;

            // 先应用字体设置（确保在设置字号前字体已就绪）
            ApplyFontToLabels();

            // 使用 visual_size 计算字体大小，而不是 grid_size
            var availableHeight = VisualSize - BorderWidth * 4;
            var lineCount = 4;

            // 计算字号
            int fontSize;
            if (FontSizeOverride > 0)
                fontSize = FontSizeOverride;
            else
                fontSize = (int)(availableHeight / lineCount * 0.8f);

            // 最小字号限制
            const int minFontSize = 8;
            if (fontSize < minFontSize)
                fontSize = minFontSize;

            // 设置字体大小和 VBoxContainer 行间距
            _labelContainer.AddThemeConstantOverride("separation", (int)(fontSize * (LineSpacing - 0.5f)));

            foreach (Node child in _labelContainer.GetChildren())
            {
                if (child is RichTextLabel label)
                {
                    label.AddThemeFontSizeOverride("normal_font_size", fontSize);
                    // 获取原始文本（去掉之前的 BBCode）
                    var originalText = label.GetParsedText();
                    // 应用粗体和斜体 BBCode
                    var bbcodeText = "";
                    if (FontBold)
                        bbcodeText += "[b]";
                    if (FontItalic)
                        bbcodeText += "[i]";
                    bbcodeText += originalText;
                    if (FontItalic)
                        bbcodeText += "[/i]";
                    if (FontBold)
                        bbcodeText += "[/b]";
                    label.Text = bbcodeText;
                    // 阴影
                    if (FontShadow)
                    {
                        label.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.5f));
                        label.AddThemeConstantOverride("shadow_offset_x", 2);
                        label.AddThemeConstantOverride("shadow_offset_y", 2);
                    }
                    else
                    {
                        label.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0));
                    }
                    // 字间距
                    label.AddThemeConstantOverride("character_spacing", (int)LetterSpacing);
                }
            }

            // 更新容器大小 - 使用 visual_size
            var containerSize = new Vector2(VisualSize - BorderWidth * 2, VisualSize - BorderWidth * 2);
            _labelContainer.CustomMinimumSize = containerSize;
            _labelContainer.Size = containerSize;
            _labelContainer.Position = new Vector2(-containerSize.X / 2, -containerSize.Y / 2);
        }

        public void SetGridSize(int newSize)
        {
            // grid_size 改变时，同时更新 visual_size 保持一致
            GridSize = newSize;
            VisualSize = newSize;
            Position = GridToWorld(GridPos);
            SetupLabels();
            QueueRedraw();
            GD.Print("[Player] Grid size changed to " + newSize + ", repositioned to " + Position);
        }

        public void SetVisualSize(int newSize)
        {
            // 只改变视觉大小，不影响网格计算和位置
            VisualSize = newSize;
            SetupLabels();
            QueueRedraw();
            GD.Print("[Player] Visual size changed to " + newSize);
        }

        public void SetBorderWidth(float newWidth)
        {
            BorderWidth = newWidth;
            SetupLabels();
            QueueRedraw();
        }

        public void SetTextAlignment(HorizontalAlignment newAlignment)
        {
            TextAlignment = newAlignment;
            SetupLabels();
        }

        public void SetFontSize(int size)
        {
            FontSizeOverride = size;
            UpdateLabelFontSize();
        }

        public void SetLineSpacing(float spacing)
        {
            LineSpacing = spacing;
            UpdateLabelFontSize();
        }

        public void SetCornerRadius(float radius)
        {
            CornerRadius = radius;
            QueueRedraw();
        }

        public void SetLineColor(int lineIndex, Color color)
        {
            if (lineIndex >= 0 && lineIndex < LineColors.Count)
            {
                LineColors[lineIndex] = color;
                SetupLabels();
            }
        }

        public void SetLetterSpacing(float spacing)
        {
            LetterSpacing = spacing;
            UpdateLabelFontSize();
        }

        public void SetFontBold(bool enabled)
        {
            FontBold = enabled;
            UpdateLabelFontSize();
        }

        public void SetFontItalic(bool enabled)
        {
            FontItalic = enabled;
            UpdateLabelFontSize();
        }

        public void SetFontShadow(bool enabled)
        {
            FontShadow = enabled;
            UpdateLabelFontSize();
        }

        public void SetFont(string fontPath)
        {
            CurrentFontPath = fontPath;
            ApplyFontToLabels();
        }

        private void ApplyFontToLabels()
        {
            if (_labelContainer == null) return;

            Font font = null;
            if (string.IsNullOrEmpty(CurrentFontPath))
            {
                font = null;  // 使用默认字体
            }
            else if (!FileAccess.FileExists(CurrentFontPath))
            {
                GD.PushWarning("[Player] 字体文件不存在: " + CurrentFontPath);
                font = null;
            }
            else
            {
                // 尝试加载字体
                var loadedResource = GD.Load(CurrentFontPath);

                if (loadedResource == null)
                {
                    GD.PushError("[Player] 无法加载字体文件: " + CurrentFontPath);
                    font = null;
                    CurrentFontPath = "";
                }
                else if (loadedResource is Font loadedFont)
                {
                    font = loadedFont;
                    GD.Print("[Player] 字体加载成功: " + CurrentFontPath);
                }
                else
                {
                    GD.PushError("[Player] 文件不是有效字体: " + CurrentFontPath);
                    font = null;
                    CurrentFontPath = "";
                }
            }

            foreach (Node child in _labelContainer.GetChildren())
            {
                if (child is RichTextLabel label)
                {
                    if (font != null)
                    {
                        // RichTextLabel 需要设置所有字体变体
                        label.AddThemeFontOverride("normal_font", font);
                        label.AddThemeFontOverride("bold_font", font);
                        label.AddThemeFontOverride("italics_font", font);
                        label.AddThemeFontOverride("bold_italics_font", font);
                    }
                    else
                    {
                        label.RemoveThemeFontOverride("normal_font");
                        label.RemoveThemeFontOverride("bold_font");
                        label.RemoveThemeFontOverride("italics_font");
                        label.RemoveThemeFontOverride("bold_italics_font");
                    }
                }
            }
        }

        public void SetBgOpacity(float opacity)
        {
            BgOpacity = opacity;
            QueueRedraw();
        }

        public void SetShowDebugInfo(bool show)
        {
            ShowDebugInfo = show;
            QueueRedraw();
        }

        public override void _Draw()
        {
            // 使用 visual_size 绘制角色，而不是 grid_size
            // 这样改变视觉大小时不会影响角色在世界中的位置
            var margin = BorderWidth * 2 + 8.0f;
            var drawSize = VisualSize - margin * 2;  // 实际可绘制区域

            // 确保最小尺寸
            if (drawSize < 10)
                drawSize = 10;

            var halfDraw = drawSize / 2.0f;
            var rect = new Rect2(new Vector2(-halfDraw, -halfDraw), new Vector2(drawSize, drawSize));

            // 计算实际背景色（应用透明度）
            var actualBgColor = new Color(BgColor.R, BgColor.G, BgColor.B, BgOpacity);

            if (CornerRadius > 0)
            {
                // 限制圆角半径
                var maxRadius = halfDraw - BorderWidth;
                var actualRadius = Mathf.Min(CornerRadius, Mathf.Max(maxRadius, 0));
                DrawRoundedRect(rect, actualBgColor, true, actualRadius);
                DrawRoundedRect(rect, BorderColor, false, actualRadius, BorderWidth);
            }
            else
            {
                // 普通矩形
                DrawRect(rect, actualBgColor, true);
                DrawRect(rect, BorderColor, false, BorderWidth);
            }

            // 调试绘制：辅助分析线
            if (ShowDebugInfo)
                DrawDebugOverlay();
        }

        private void DrawCornerSector(float cx, float cy, float r, float startAngle, float endAngle, Color color)
        {
            // 绘制圆角扇形（90度圆弧填充）
            var points = new Vector2[10];  // 圆心 + 8分段 + 闭合点
            points[0] = new Vector2(cx, cy);  // 圆心
            const int segments = 8;
            for (int i = 0; i <= segments; i++)
            {
                var angle = startAngle + (endAngle - startAngle) * (i / (float)segments);
                points[i + 1] = new Vector2(cx + Mathf.Cos(angle) * r, cy + Mathf.Sin(angle) * r);
            }
            var colorArray = new Color[points.Length];
            for (int i = 0; i < colorArray.Length; i++)
                colorArray[i] = color;
            DrawPolygon(points, colorArray);
        }

        private void DrawRoundedRect(Rect2 rect, Color color, bool filled, float radius, float width = -1.0f)
        {
            var x = rect.Position.X;
            var y = rect.Position.Y;
            var w = rect.Size.X;
            var h = rect.Size.Y;
            var r = Mathf.Min(radius, Mathf.Min(w, h) / 2.0f);  // 确保半径不超过矩形一半

            if (filled)
            {
                // 绘制填充：中心矩形 + 四个圆角扇形
                // 中心矩形（不覆盖四角区域）
                DrawRect(new Rect2(x + r, y + r, w - r * 2, h - r * 2), color, true);
                // 四条边矩形（上下左右）
                DrawRect(new Rect2(x + r, y, w - r * 2, r), color, true);  // 上
                DrawRect(new Rect2(x + r, y + h - r, w - r * 2, r), color, true);  // 下
                DrawRect(new Rect2(x, y + r, r, h - r * 2), color, true);  // 左
                DrawRect(new Rect2(x + w - r, y + r, r, h - r * 2), color, true);  // 右

                // 四个圆角扇形
                DrawCornerSector(x + r, y + r, r, Mathf.Pi, 1.5f * Mathf.Pi, color);  // 左上
                DrawCornerSector(x + w - r, y + r, r, 1.5f * Mathf.Pi, 2 * Mathf.Pi, color);  // 右上
                DrawCornerSector(x + r, y + h - r, r, 0.5f * Mathf.Pi, Mathf.Pi, color);  // 左下
                DrawCornerSector(x + w - r, y + h - r, r, 0, 0.5f * Mathf.Pi, color);  // 右下
            }
            else
            {
                // 绘制边框线
                const int segments = 8;  // 每角弧线分段数
                var points = new System.Collections.Generic.List<Vector2>();

                // 左上角弧线 (从左侧到顶部)
                for (int i = 0; i <= segments; i++)
                {
                    var angle = Mathf.Pi + (Mathf.Pi / 2) * (i / (float)segments);
                    points.Add(new Vector2(x + r + Mathf.Cos(angle) * r, y + r + Mathf.Sin(angle) * r));
                }

                // 右上角弧线 (从顶部到右侧)
                for (int i = 0; i <= segments; i++)
                {
                    var angle = 1.5f * Mathf.Pi + (Mathf.Pi / 2) * (i / (float)segments);
                    points.Add(new Vector2(x + w - r + Mathf.Cos(angle) * r, y + r + Mathf.Sin(angle) * r));
                }

                // 右下角弧线 (从右侧到底部)
                for (int i = 0; i <= segments; i++)
                {
                    var angle = 0 + (Mathf.Pi / 2) * (i / (float)segments);
                    points.Add(new Vector2(x + w - r + Mathf.Cos(angle) * r, y + h - r + Mathf.Sin(angle) * r));
                }

                // 左下角弧线 (从底部到左侧)
                for (int i = 0; i <= segments; i++)
                {
                    var angle = 0.5f * Mathf.Pi + (Mathf.Pi / 2) * (i / (float)segments);
                    points.Add(new Vector2(x + r + Mathf.Cos(angle) * r, y + h - r + Mathf.Sin(angle) * r));
                }

                // 添加第一个点来闭合多边形（修复左边线缺失问题）
                if (points.Count > 0)
                    points.Add(points[0]);

                DrawPolyline(points.ToArray(), color, width);
            }
        }

        private void DrawDebugOverlay()
        {
            // 绘制调试辅助线，帮助分析边框与格子的对齐
            var gridHalf = GridSize / 2.0f;

            // 1. 红色虚线框 - 显示格子边界（角色所在格子的完整区域）
            var gridRect = new Rect2(new Vector2(-gridHalf, -gridHalf), new Vector2(GridSize, GridSize));
            DrawRect(gridRect, new Color(1, 0, 0, 0.5f), false, 1.0f);

            // 在左上角标注
            var labelColor = new Color(1, 0.5f, 0.5f, 0.8f);
            DrawString(ThemeDB.FallbackFont, new Vector2(-gridHalf + 2, -gridHalf + 12), "Grid", HorizontalAlignment.Left, -1, 10, labelColor);

            // 2. 绿色实线框 - 显示安全区域（margin 内）
            var margin = BorderWidth * 2 + 8.0f;
            var safeSize = GridSize - margin * 2;
            var safeHalf = safeSize / 2.0f;
            var safeRect = new Rect2(new Vector2(-safeHalf, -safeHalf), new Vector2(safeSize, safeSize));
            DrawRect(safeRect, new Color(0, 1, 0, 0.6f), false, 1.5f);

            // 3. 黄色测量线 - 显示边距大小
            var measureColor = new Color(1, 1, 0, 0.7f);
            var arrowSize = 5.0f;
            var marginHalf = margin / 2.0f;

            // 左侧边距标注线
            var leftX = -gridHalf + marginHalf;
            DrawLine(new Vector2(-gridHalf, 0), new Vector2(-safeHalf, 0), measureColor, 1.0f);
            // 箭头
            DrawLine(new Vector2(-gridHalf, 0), new Vector2(-gridHalf + arrowSize, -arrowSize), measureColor, 1.0f);
            DrawLine(new Vector2(-gridHalf, 0), new Vector2(-gridHalf + arrowSize, arrowSize), measureColor, 1.0f);
            DrawLine(new Vector2(-safeHalf, 0), new Vector2(-safeHalf - arrowSize, -arrowSize), measureColor, 1.0f);
            DrawLine(new Vector2(-safeHalf, 0), new Vector2(-safeHalf - arrowSize, arrowSize), measureColor, 1.0f);
            // 标注文字
            DrawString(ThemeDB.FallbackFont, new Vector2(leftX - 15, -15), $"m={margin:F1}", HorizontalAlignment.Center, -1, 9, measureColor);

            // 4. 蓝色点 - 显示中心点
            DrawCircle(Vector2.Zero, 3.0f, new Color(0, 0.5f, 1, 0.8f));

            // 5. 白色文字 - 显示当前尺寸信息（右下角）
            var infoColor = new Color(1, 1, 1, 0.9f);
            DrawString(ThemeDB.FallbackFont, new Vector2(gridHalf - 120, gridHalf - 5),
                $"Grid:{GridSize} | Margin:{margin:F1} | Draw:{safeSize:F1}",
                HorizontalAlignment.Left, -1, 9, infoColor);

            // 6. 吸附状态指示（左上角）- 已改为自动校正，始终显示绿色
            var snapColor = new Color(0, 1, 0, 0.9f);
            DrawString(ThemeDB.FallbackFont, new Vector2(-gridHalf + 2, -gridHalf + 25),
                "[AUTO SNAP]", HorizontalAlignment.Left, -1, 10, snapColor);

            // 7. 十字准星 - 标记格子中心
            var crossColor = new Color(1, 0, 1, 0.5f);  // 紫色
            var crossSize = 8.0f;
            DrawLine(new Vector2(-crossSize, 0), new Vector2(crossSize, 0), crossColor, 1.0f);
            DrawLine(new Vector2(0, -crossSize), new Vector2(0, crossSize), crossColor, 1.0f);

            // 8. 实时坐标信息（左下角）
            var posColor = new Color(0, 1, 1, 0.9f);  // 青色
            var targetPos = GridToWorld(GridPos);
            DrawString(ThemeDB.FallbackFont, new Vector2(-gridHalf + 2, gridHalf - 20),
                $"Pos:{Position.X:F1},{Position.Y:F1} | Target:{targetPos.X:F1},{targetPos.Y:F1}",
                HorizontalAlignment.Left, -1, 9, posColor);
        }

        public override void _Process(double _delta)
        {
            // 自动校正位置：确保始终位于当前格子的中心
            // 只有在非移动状态下才校正，避免干扰移动动画
            if (!IsMoving)
            {
                var targetPos = GridToWorld(GridPos);
                // 使用距离判断而非精确相等，避免浮点精度问题
                if (Position.DistanceTo(targetPos) > 0.5f)
                    Position = targetPos;
            }

            HandleInput();
        }

        private void HandleInput()
        {
            if (IsMoving)
                return;

            var direction = Vector2I.Zero;

            if (Input.IsActionJustPressed("move_up"))
                direction.Y = -1;
            else if (Input.IsActionJustPressed("move_down"))
                direction.Y = 1;
            else if (Input.IsActionJustPressed("move_left"))
                direction.X = -1;
            else if (Input.IsActionJustPressed("move_right"))
                direction.X = 1;

            if (direction != Vector2I.Zero)
                MoveTo(GridPos + direction);
        }

        private void MoveTo(Vector2I targetGridPos)
        {
            var gridManager = GetParent()?.GetNode<GridManager>("GridManager");
            if (gridManager != null && !gridManager.IsWalkable(targetGridPos))
                return;

            GridPos = targetGridPos;
            var targetWorldPos = GridToWorld(GridPos);

            IsMoving = true;

            var tween = CreateTween();
            tween.SetTrans(Tween.TransitionType.Quad);
            tween.SetEase(Tween.EaseType.Out);
            tween.TweenProperty(this, "position", targetWorldPos, MoveDuration);
            tween.Finished += OnMoveFinished;
        }

        private void OnMoveFinished()
        {
            IsMoving = false;
            // 强制精确对齐到网格中心，消除浮点误差
            Position = GridToWorld(GridPos);
        }

        private Vector2 GridToWorld(Vector2I pos)
        {
            return new Vector2(pos.X * GridSize + GridSize / 2.0f,
                               pos.Y * GridSize + GridSize / 2.0f);
        }
    }
}
