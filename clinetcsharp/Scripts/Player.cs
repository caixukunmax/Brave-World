using Godot;
using Godot.Collections;
using Protocol;

namespace ClinetCSharp
{
    /// <summary>
    /// 玩家角色类
    /// </summary>
    [GlobalClass]
    public partial class Player : Node2D
    {
        [Export] public int GridSize { get; set; } = 111;
        [Export] public int VisualSize { get; set; } = 111;
        [Export] public float VisualSizeScale { get; set; } = 1.0f;
        [Export] public float BorderWidth { get; set; } = 3.0f;
        [Export] public float BorderWidthScale { get; set; } = 3.0f / 111.0f;
        [Export] public Color BorderColor { get; set; } = Colors.White;
        [Export] public Color BgColor { get; set; } = new Color(1, 1, 1, 0.1f);
        [Export] public Color TextColor { get; set; } = Colors.Black;
        [Export] public float MoveDuration { get; set; } = 0.15f;
        [Export] public int FontSizeOverride { get; set; } = 0;
        [Export] public float LineSpacing { get; set; } = 0.8f;
        [Export] public float CornerRadius { get; set; } = 0.0f;
        [Export] public float LetterSpacing { get; set; } = 0.0f;
        [Export] public float BgOpacity { get; set; } = 0.1f;

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

        // ========== 战斗属性（从服务器 attrs 同步） ==========
        public System.Collections.Generic.Dictionary<uint, int> CombatAttrs { get; } = new();

        // ========== 血条 ==========
        public Vector2 HealthBarOffset { get; set; } = new Vector2(0, -70);
        public float HealthBarLength { get; set; } = 80;
        public float HealthBarHeight { get; set; } = 6;
        public float HealthBarLengthScale { get; set; } = 80.0f / 111.0f;
        public float HealthBarHeightScale { get; set; } = 6.0f / 111.0f;
        public Color HealthBarColor { get; set; } = new Color(0, 0.8f, 0, 1);
        public Color HealthBarBgColor { get; set; } = new Color(0.3f, 0.3f, 0.3f, 0.5f);
        public bool HealthBarVisible { get; set; } = true;
        public float HealthBarFillPercent { get; set; } = 1.0f;

        // ========== 施法条 ==========
        public Vector2 CastBarOffset { get; set; } = new Vector2(0, -80);
        public float CastBarLength { get; set; } = 60;
        public float CastBarHeight { get; set; } = 4;
        public Color CastBarColor { get; set; } = new Color(0.3f, 0.5f, 1, 1); // 蓝色
        public Color CastBarBgColor { get; set; } = new Color(0.3f, 0.3f, 0.3f, 0.4f);
        public bool CastBarVisible { get; set; } = true;
        public float CastBarFillPercent { get; set; } = 0.6f;

        // ========== 等级徽章 ==========
        public Vector2 LevelBadgeOffset { get; set; } = new Vector2(-35, -35);
        public float LevelBadgeFontSize { get; set; } = 12;
        public Color LevelBadgeTextColor { get; set; } = Colors.Yellow;
        public bool LevelBadgeVisible { get; set; } = true;
        public string LevelBadgeText { get; set; } = "Lv.{level}";

        public Vector2I GridPos { get; set; } = new Vector2I(25, 25);
        public bool IsMoving { get; set; } = false;
        public HorizontalAlignment TextAlignment { get; set; } = HorizontalAlignment.Center;

        // ========== 独立文本系统 ==========
        private const int LabelCount = 4;
        private RichTextLabel[] _labels = new RichTextLabel[LabelCount];
        private Control[] _labelContainers = new Control[LabelCount];
        private Vector2[] _labelOffsets = new Vector2[LabelCount];
        private bool[] _labelVisible = new bool[LabelCount] { true, true, true, true };
        private int[] _labelFontSizes = new int[LabelCount]; // 0 = 使用全局字号

        // 默认偏移：4行文字从角色上方向下排列
        public static readonly Vector2[] DefaultOffsets = new Vector2[LabelCount]
        {
            new Vector2(0, -55), // 名称
            new Vector2(0, -38), // 职业
            new Vector2(0, -21), // 称号
            new Vector2(0, -4),  // 状态
        };

        // 自动居中
        public bool LabelAutoCenterX { get; set; } = false;

        // 拖拽状态
        private bool _dragging = false;
        private int _dragIndex = -1;
        private Vector2 _dragStartMouse;
        private Vector2 _dragStartOffset;

        // 文本名称（可编辑，调试面板标题）
        public string[] LabelNames = new string[LabelCount] { "名称", "职业", "称号", "状态" };

        // 文本内容（可编辑）
        public string[] LabelTexts = new string[LabelCount] { $"Lv.1 王建国", "农夫", "普通人", "闲逛中..." };

        public override void _Ready()
        {
            Position = UiUtils.GridToWorld(GridPos, GridSize);
            // 初始化默认偏移
            for (int i = 0; i < LabelCount; i++)
                _labelOffsets[i] = DefaultOffsets[i];
            SetupLabels();
            QueueRedraw();

            var nm = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
            if (nm != null)
            {
                nm.MoveCancelNotify += OnMoveCancelReceived;
                nm.MoveResponse += OnMoveResponse;
                nm.RoleAttrUpdated += OnRoleAttrUpdated;
                nm.CombatStateNotify += OnCombatStateNotify;

                // 应用缓存的角色数据
                if (nm.CachedRoleInfo != null)
                    ApplyRoleInfo(nm.CachedRoleInfo);
            }
        }

        public override void _ExitTree()
        {
            var nm = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
            if (nm != null)
            {
                nm.MoveCancelNotify -= OnMoveCancelReceived;
                nm.MoveResponse -= OnMoveResponse;
                nm.RoleAttrUpdated -= OnRoleAttrUpdated;
                nm.CombatStateNotify -= OnCombatStateNotify;
            }
            _checkTimer?.Stop();
            _checkTimer?.QueueFree();
            _checkTimer = null;
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
            // 清理旧节点
            for (int i = 0; i < LabelCount; i++)
            {
                if (_labelContainers[i] != null && IsInstanceValid(_labelContainers[i]))
                {
                    _labelContainers[i].QueueFree();
                    _labelContainers[i] = null;
                    _labels[i] = null;
                }
            }

            // 创建文本行
            var lines = new string[LabelCount];
            for (int i = 0; i < LabelCount; i++)
                lines[i] = LabelTexts[i];

            for (int i = 0; i < LabelCount; i++)
            {
                var container = new Control();
                container.Name = $"LabelContainer_{i}";
                AddChild(container);

                var label = new RichTextLabel();
                label.Name = $"Label_{i}";
                label.FitContent = true;
                label.ScrollActive = false;
                label.BbcodeEnabled = true;
                label.Text = lines[i];
                label.HorizontalAlignment = TextAlignment;
                label.VerticalAlignment = VerticalAlignment.Center;
                label.AutowrapMode = TextServer.AutowrapMode.Off;
                label.CustomMinimumSize = new Vector2(1, 1);

                // 颜色
                var lineColor = TextColor;
                if (i < LineColors.Count)
                    lineColor = LineColors[i];
                label.AddThemeColorOverride("font_color", lineColor);

                container.AddChild(label);
                _labelContainers[i] = container;
                _labels[i] = label;
            }

            // 等待一帧让布局完成
            await ToSignal(GetTree(), "process_frame");
            UpdateAllLabelPositions();
            ApplyFontToLabels();
            UpdateLabelFontSize();
        }

        private void UpdateAllLabelPositions()
        {
            for (int i = 0; i < LabelCount; i++)
            {
                if (_labelContainers[i] == null) continue;
                _labelContainers[i].Visible = _labelVisible[i];
                if (!_labelVisible[i]) continue;

                var label = _labels[i];
                if (label == null) continue;

                // 让 RichTextLabel 自适应内容大小
                var textSize = label.GetMinimumSize();
                var pos = _labelOffsets[i] - textSize / 2;
                if (LabelAutoCenterX)
                    pos.X = -textSize.X / 2;
                _labelContainers[i].Position = pos;
            }
        }

        private void UpdateLabelFontSize()
        {
            var availableHeight = VisualSize - BorderWidth * 4;
            int globalFontSize;
            if (FontSizeOverride > 0)
                globalFontSize = FontSizeOverride;
            else
                globalFontSize = (int)(availableHeight / LabelCount * 0.8f);

            const int minFontSize = 8;
            if (globalFontSize < minFontSize)
                globalFontSize = minFontSize;

            for (int i = 0; i < LabelCount; i++)
            {
                if (_labels[i] == null) continue;

                int fontSize = _labelFontSizes[i] > 0 ? _labelFontSizes[i] : globalFontSize;
                if (fontSize < minFontSize) fontSize = minFontSize;

                _labels[i].AddThemeFontSizeOverride("normal_font_size", fontSize);

                // BBCode 样式
                var originalText = _labels[i].GetParsedText();
                var bbcodeText = "";
                if (FontBold) bbcodeText += "[b]";
                if (FontItalic) bbcodeText += "[i]";
                bbcodeText += originalText;
                if (FontItalic) bbcodeText += "[/i]";
                if (FontBold) bbcodeText += "[/b]";
                _labels[i].Text = bbcodeText;

                // 阴影
                if (FontShadow)
                {
                    _labels[i].AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.5f));
                    _labels[i].AddThemeConstantOverride("shadow_offset_x", 2);
                    _labels[i].AddThemeConstantOverride("shadow_offset_y", 2);
                }
                else
                {
                    _labels[i].AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0));
                }

                // 字间距
                _labels[i].AddThemeConstantOverride("character_spacing", (int)LetterSpacing);
            }

            UpdateAllLabelPositions();
        }

        // ========== 拖拽系统 ==========

        public override void _Input(InputEvent @event)
        {
            if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
            {
                if (mb.Pressed)
                {
                    var localMouse = ToLocal(mb.GlobalPosition);
                    // 从最上面的文本开始检测（后创建的在上面）
                    for (int i = LabelCount - 1; i >= 0; i--)
                    {
                        if (_labelContainers[i] == null || !_labelVisible[i]) continue;
                        var rect = GetLabelRect(i);
                        if (rect.HasPoint(localMouse))
                        {
                            _dragging = true;
                            _dragIndex = i;
                            _dragStartMouse = localMouse;
                            _dragStartOffset = _labelOffsets[i];
                            GetViewport().SetInputAsHandled();
                            return;
                        }
                    }
                }
                else if (_dragging)
                {
                    _dragging = false;
                    _dragIndex = -1;
                    GetViewport().SetInputAsHandled();
                }
            }
            else if (@event is InputEventMouseMotion mm && _dragging && _dragIndex >= 0)
            {
                var localMouse = ToLocal(mm.GlobalPosition);
                var delta = localMouse - _dragStartMouse;
                if (LabelAutoCenterX)
                    _labelOffsets[_dragIndex] = new Vector2(0, _dragStartOffset.Y + delta.Y);
                else
                    _labelOffsets[_dragIndex] = _dragStartOffset + delta;
                UpdateAllLabelPositions();
                GetViewport().SetInputAsHandled();
            }
        }

        private Rect2 GetLabelRect(int index)
        {
            if (_labels[index] == null) return new Rect2();
            var textSize = _labels[index].GetMinimumSize();
            var pos = _labelOffsets[index] - textSize / 2;
            return new Rect2(pos, textSize);
        }

        // ========== 公共接口（保留旧接口 + 新接口） ==========

        // --- 独立文本控制 ---

        public Vector2 GetLabelOffset(int index)
        {
            if (index < 0 || index >= LabelCount) return Vector2.Zero;
            return _labelOffsets[index];
        }

        public void SetLabelOffset(int index, Vector2 offset)
        {
            if (index < 0 || index >= LabelCount) return;
            if (LabelAutoCenterX)
                offset.X = 0;
            _labelOffsets[index] = offset;
            UpdateAllLabelPositions();
        }

        public void ResetLabelOffset(int index)
        {
            if (index < 0 || index >= LabelCount) return;
            _labelOffsets[index] = DefaultOffsets[index];
            UpdateAllLabelPositions();
        }

        public void ResetAllLabelOffsets()
        {
            for (int i = 0; i < LabelCount; i++)
                _labelOffsets[i] = DefaultOffsets[i];
            UpdateAllLabelPositions();
        }

        public void SetLabelAutoCenterX(bool enabled)
        {
            LabelAutoCenterX = enabled;
            if (enabled)
            {
                for (int i = 0; i < LabelCount; i++)
                    _labelOffsets[i] = new Vector2(0, _labelOffsets[i].Y);
            }
            UpdateAllLabelPositions();
        }

        public bool GetLabelVisible(int index)
        {
            if (index < 0 || index >= LabelCount) return false;
            return _labelVisible[index];
        }

        public string GetLabelName(int index)
        {
            if (index < 0 || index >= LabelCount) return "";
            return LabelNames[index];
        }

        public void SetLabelName(int index, string name)
        {
            if (index < 0 || index >= LabelCount) return;
            LabelNames[index] = name;
        }

        public string GetLabelText(int index)
        {
            if (index < 0 || index >= LabelCount) return "";
            return LabelTexts[index];
        }

        public void SetLabelText(int index, string text)
        {
            if (index < 0 || index >= LabelCount) return;
            LabelTexts[index] = text;
            if (_labels[index] != null)
            {
                _labels[index].Text = text;
                UpdateAllLabelPositions();
            }
        }

        public void SetLabelVisible(int index, bool visible)
        {
            if (index < 0 || index >= LabelCount) return;
            _labelVisible[index] = visible;
            UpdateAllLabelPositions();
        }

        public int GetLabelFontSize(int index)
        {
            if (index < 0 || index >= LabelCount) return 0;
            return _labelFontSizes[index];
        }

        public void SetLabelFontSize(int index, int size)
        {
            if (index < 0 || index >= LabelCount) return;
            _labelFontSizes[index] = size;
            UpdateLabelFontSize();
        }

        // --- 保留的旧接口 ---

        public void SetGridSize(int newSize)
        {
            GridSize = newSize;
            VisualSize = Mathf.Clamp((int)(newSize * VisualSizeScale), 10, newSize);
            BorderWidth = Mathf.Clamp(newSize * BorderWidthScale, 1.0f, 20.0f);
            HealthBarLength = Mathf.Clamp(newSize * HealthBarLengthScale, 10.0f, newSize * 2.0f);
            HealthBarHeight = Mathf.Clamp(newSize * HealthBarHeightScale, 2.0f, newSize);
            Position = UiUtils.GridToWorld(GridPos, GridSize);
            SetupLabels();
            QueueRedraw();
        }

        public void SetVisualSize(int newSize)
        {
            VisualSize = newSize;
            SetupLabels();
            QueueRedraw();
        }

        public void SetVisualSizeScale(float scale)
        {
            VisualSizeScale = scale;
            VisualSize = Mathf.Clamp((int)(GridSize * VisualSizeScale), 10, GridSize);
            SetupLabels();
            QueueRedraw();
        }

        public void SetBorderWidth(float newWidth)
        {
            BorderWidth = newWidth;
            SetupLabels();
            QueueRedraw();
        }

        public void SetBorderWidthScale(float scale)
        {
            BorderWidthScale = scale;
            BorderWidth = Mathf.Clamp(GridSize * BorderWidthScale, 1.0f, 20.0f);
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
                if (_labels[lineIndex] != null)
                    _labels[lineIndex].AddThemeColorOverride("font_color", color);
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
            Font font = null;
            if (string.IsNullOrEmpty(CurrentFontPath))
            {
                font = null;
            }
            else if (!FileAccess.FileExists(CurrentFontPath))
            {
                GD.PushWarning("[Player] 字体文件不存在: " + CurrentFontPath);
                font = null;
            }
            else
            {
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
                }
                else
                {
                    GD.PushError("[Player] 文件不是有效字体: " + CurrentFontPath);
                    font = null;
                    CurrentFontPath = "";
                }
            }

            for (int i = 0; i < LabelCount; i++)
            {
                if (_labels[i] == null) continue;
                if (font != null)
                {
                    _labels[i].AddThemeFontOverride("normal_font", font);
                    _labels[i].AddThemeFontOverride("bold_font", font);
                    _labels[i].AddThemeFontOverride("italics_font", font);
                    _labels[i].AddThemeFontOverride("bold_italics_font", font);
                }
                else
                {
                    _labels[i].RemoveThemeFontOverride("normal_font");
                    _labels[i].RemoveThemeFontOverride("bold_font");
                    _labels[i].RemoveThemeFontOverride("italics_font");
                    _labels[i].RemoveThemeFontOverride("bold_italics_font");
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

        // --- 血条控制 ---

        public Vector2 GetHealthBarOffset() => HealthBarOffset;

        public void SetHealthBarOffset(Vector2 offset)
        {
            HealthBarOffset = offset;
            QueueRedraw();
        }

        public void SetHealthBarLength(float length)
        {
            HealthBarLength = length;
            QueueRedraw();
        }

        public void SetHealthBarHeight(float height)
        {
            HealthBarHeight = height;
            QueueRedraw();
        }

        public void SetHealthBarLengthScale(float scale)
        {
            HealthBarLengthScale = scale;
            HealthBarLength = Mathf.Clamp(GridSize * scale, 10.0f, GridSize * 2.0f);
            QueueRedraw();
        }

        public void SetHealthBarHeightScale(float scale)
        {
            HealthBarHeightScale = scale;
            HealthBarHeight = Mathf.Clamp(GridSize * scale, 2.0f, GridSize);
            QueueRedraw();
        }

        public void SetHealthBarColor(Color color)
        {
            HealthBarColor = color;
            QueueRedraw();
        }

        public void SetHealthBarFillPercent(float percent)
        {
            HealthBarFillPercent = Mathf.Clamp(percent, 0, 1);
            QueueRedraw();
        }

        public void SetHealthBarVisible(bool visible)
        {
            HealthBarVisible = visible;
            QueueRedraw();
        }

        // --- 施法条控制 ---

        public Vector2 GetCastBarOffset() => CastBarOffset;

        public void SetCastBarOffset(Vector2 offset)
        {
            CastBarOffset = offset;
            QueueRedraw();
        }

        public void SetCastBarLength(float length)
        {
            CastBarLength = length;
            QueueRedraw();
        }

        public void SetCastBarHeight(float height)
        {
            CastBarHeight = height;
            QueueRedraw();
        }

        public void SetCastBarColor(Color color)
        {
            CastBarColor = color;
            QueueRedraw();
        }

        public void SetCastBarFillPercent(float percent)
        {
            CastBarFillPercent = Mathf.Clamp(percent, 0, 1);
            QueueRedraw();
        }

        public void SetCastBarVisible(bool visible)
        {
            CastBarVisible = visible;
            QueueRedraw();
        }

        // --- 等级徽章控制 ---

        public Vector2 GetLevelBadgeOffset() => LevelBadgeOffset;

        public void SetLevelBadgeOffset(Vector2 offset)
        {
            LevelBadgeOffset = offset;
            QueueRedraw();
        }

        public void SetLevelBadgeFontSize(float size)
        {
            LevelBadgeFontSize = size;
            QueueRedraw();
        }

        public void SetLevelBadgeTextColor(Color color)
        {
            LevelBadgeTextColor = color;
            QueueRedraw();
        }

        public void SetLevelBadgeText(string text)
        {
            LevelBadgeText = text;
            QueueRedraw();
        }

        public void SetLevelBadgeVisible(bool visible)
        {
            LevelBadgeVisible = visible;
            QueueRedraw();
        }

        /// <summary>
        /// 从服务器返回的角色数据更新显示
        /// </summary>
        public void ApplyRoleInfo(Game.FullRoleInfo roleInfo)
        {
            if (roleInfo == null) return;

            CharacterName = roleInfo.RoleName;
            Level = (int)roleInfo.Level;
            Job = roleInfo.Job;
            Title = roleInfo.Title;
            Status = roleInfo.Status;

            // 更新4个标签内容
            LabelTexts[0] = $"Lv.{Level} {CharacterName}";
            LabelTexts[1] = Job;
            LabelTexts[2] = Title;
            LabelTexts[3] = Status;

            // 更新等级徽章
            LevelBadgeText = "Lv.{level}";

            GD.Print($"[Player] ApplyRoleInfo: name={CharacterName}, level={Level}, job={Job}, title={Title}, status={Status}");

            // 解析战斗属性集合
            CombatAttrs.Clear();
            foreach (var attr in roleInfo.Attrs)
                CombatAttrs[attr.Key] = attr.Value;
            if (roleInfo.Attrs.Count > 0)
                GD.Print($"[Player] ApplyRoleInfo: attrs loaded, count={roleInfo.Attrs.Count}");

            // 从属性中读取移动速度（EAttr.MOVE_SPEED=10，单位ms → 转为秒）
            if (CombatAttrs.TryGetValue(10, out var moveSpeedMs) && moveSpeedMs > 0)
                MoveDuration = moveSpeedMs / 1000f;

            // 从服务器设置初始位置
            if (roleInfo.GridX != 0 || roleInfo.GridY != 0)
            {
                GridPos = new Vector2I(roleInfo.GridX, roleInfo.GridY);
                _moveFromPos = GridPos;
                Position = UiUtils.GridToWorld(GridPos, GridSize);
                GD.Print($"[Player] Set position from server: ({GridPos.X}, {GridPos.Y})");
            }

            SetupLabels();
            QueueRedraw();
        }

        // ========== 绘制 ==========

        public override void _Draw()
        {
            var drawSize = VisualSize;
            if (drawSize < 10) drawSize = 10;

            var halfDraw = drawSize / 2.0f;
            var rect = new Rect2(new Vector2(-halfDraw, -halfDraw), new Vector2(drawSize, drawSize));
            var actualBgColor = new Color(BgColor.R, BgColor.G, BgColor.B, BgOpacity);

            if (CornerRadius > 0)
            {
                var maxRadius = halfDraw - BorderWidth;
                var actualRadius = Mathf.Min(CornerRadius, Mathf.Max(maxRadius, 0));
                this.DrawRoundedRect(rect, actualBgColor, true, actualRadius);
                this.DrawRoundedRect(rect, BorderColor, false, actualRadius, BorderWidth);
            }
            else
            {
                DrawRect(rect, actualBgColor, true);
                DrawRect(rect, BorderColor, false, BorderWidth);
            }

            // 血条
            if (HealthBarVisible)
            {
                float halfLen = HealthBarLength / 2.0f;
                float halfH = HealthBarHeight / 2.0f;
                var bgRect = new Rect2(
                    HealthBarOffset.X - halfLen,
                    HealthBarOffset.Y - halfH,
                    HealthBarLength,
                    HealthBarHeight);
                DrawRect(bgRect, HealthBarBgColor, true);

                float fillWidth = HealthBarLength * Mathf.Clamp(HealthBarFillPercent, 0, 1);
                if (fillWidth > 0)
                {
                    var fillRect = new Rect2(
                        HealthBarOffset.X - halfLen,
                        HealthBarOffset.Y - halfH,
                        fillWidth,
                        HealthBarHeight);
                    DrawRect(fillRect, HealthBarColor, true);
                }
            }

            // 施法条
            if (CastBarVisible)
            {
                float cHalfLen = CastBarLength / 2.0f;
                float cHalfH = CastBarHeight / 2.0f;
                var cBgRect = new Rect2(
                    CastBarOffset.X - cHalfLen,
                    CastBarOffset.Y - cHalfH,
                    CastBarLength,
                    CastBarHeight);
                DrawRect(cBgRect, CastBarBgColor, true);

                float cFillWidth = CastBarLength * Mathf.Clamp(CastBarFillPercent, 0, 1);
                if (cFillWidth > 0)
                {
                    var cFillRect = new Rect2(
                        CastBarOffset.X - cHalfLen,
                        CastBarOffset.Y - cHalfH,
                        cFillWidth,
                        CastBarHeight);
                    DrawRect(cFillRect, CastBarColor, true);
                }
            }

            // 等级徽章
            if (LevelBadgeVisible)
            {
                var levelText = LevelBadgeText.Replace("{level}", Level.ToString())
                    .Replace("{name}", CharacterName)
                    .Replace("{job}", Job)
                    .Replace("{title}", Title)
                    .Replace("{status}", Status);
                var font = ThemeDB.FallbackFont;
                int fontSize = Mathf.Max((int)LevelBadgeFontSize, 6);
                var textSize = font.GetStringSize(levelText, HorizontalAlignment.Center, -1, fontSize);
                var textPos = LevelBadgeOffset - textSize / 2.0f + new Vector2(0, fontSize * 0.15f);
                DrawString(font, textPos, levelText, HorizontalAlignment.Center, -1, fontSize, LevelBadgeTextColor);
            }

            if (ShowDebugInfo)
                DrawDebugOverlay();
        }


        private void DrawDebugOverlay()
        {
            var gridHalf = GridSize / 2.0f;

            var gridRect = new Rect2(new Vector2(-gridHalf, -gridHalf), new Vector2(GridSize, GridSize));
            DrawRect(gridRect, new Color(1, 0, 0, 0.5f), false, 1.0f);
            var labelColor = new Color(1, 0.5f, 0.5f, 0.8f);
            DrawString(ThemeDB.FallbackFont, new Vector2(-gridHalf + 2, -gridHalf + 12), "Grid", HorizontalAlignment.Left, -1, 10, labelColor);

            var margin = BorderWidth * 2 + 8.0f;
            var safeSize = GridSize - margin * 2;
            var safeHalf = safeSize / 2.0f;
            var safeRect = new Rect2(new Vector2(-safeHalf, -safeHalf), new Vector2(safeSize, safeSize));
            DrawRect(safeRect, new Color(0, 1, 0, 0.6f), false, 1.5f);

            var measureColor = new Color(1, 1, 0, 0.7f);
            var arrowSize = 5.0f;
            var marginHalf = margin / 2.0f;
            DrawLine(new Vector2(-gridHalf, 0), new Vector2(-safeHalf, 0), measureColor, 1.0f);
            DrawLine(new Vector2(-gridHalf, 0), new Vector2(-gridHalf + arrowSize, -arrowSize), measureColor, 1.0f);
            DrawLine(new Vector2(-gridHalf, 0), new Vector2(-gridHalf + arrowSize, arrowSize), measureColor, 1.0f);
            DrawLine(new Vector2(-safeHalf, 0), new Vector2(-safeHalf - arrowSize, -arrowSize), measureColor, 1.0f);
            DrawLine(new Vector2(-safeHalf, 0), new Vector2(-safeHalf - arrowSize, arrowSize), measureColor, 1.0f);
            DrawString(ThemeDB.FallbackFont, new Vector2(marginHalf - 15, -15), $"m={margin:F1}", HorizontalAlignment.Center, -1, 9, measureColor);

            DrawCircle(Vector2.Zero, 3.0f, new Color(0, 0.5f, 1, 0.8f));

            var infoColor = new Color(1, 1, 1, 0.9f);
            DrawString(ThemeDB.FallbackFont, new Vector2(gridHalf - 120, gridHalf - 5),
                $"Grid:{GridSize} | Margin:{margin:F1} | Draw:{safeSize:F1}",
                HorizontalAlignment.Left, -1, 9, infoColor);

            var snapColor = new Color(0, 1, 0, 0.9f);
            DrawString(ThemeDB.FallbackFont, new Vector2(-gridHalf + 2, -gridHalf + 25),
                "[AUTO SNAP]", HorizontalAlignment.Left, -1, 10, snapColor);

            var crossColor = new Color(1, 0, 1, 0.5f);
            var crossSize = 8.0f;
            DrawLine(new Vector2(-crossSize, 0), new Vector2(crossSize, 0), crossColor, 1.0f);
            DrawLine(new Vector2(0, -crossSize), new Vector2(0, crossSize), crossColor, 1.0f);

            var posColor = new Color(0, 1, 1, 0.9f);
            var targetPos = UiUtils.GridToWorld(GridPos, GridSize);
            DrawString(ThemeDB.FallbackFont, new Vector2(-gridHalf + 2, gridHalf - 20),
                $"Pos:{Position.X:F1},{Position.Y:F1} | Target:{targetPos.X:F1},{targetPos.Y:F1}",
                HorizontalAlignment.Left, -1, 9, posColor);
        }

        // ========== 移动系统 ==========

        // 移动状态机
        private Vector2I _moveFromPos;
        private Vector2I _moveTargetPos;
        private int _moveDurationMs;
        private int _moveCheckRatio;
        private int _moveDualStartRatio;
        private int _moveDualEndRatio;
        private Tween? _currentTween;
        private Timer? _checkTimer;
        private bool _movePending = false;

        public override void _Process(double _delta)
        {
            if (!IsMoving)
            {
                var targetPos = UiUtils.GridToWorld(GridPos, GridSize);
                if (Position.DistanceTo(targetPos) > 0.5f)
                    Position = targetPos;
            }
            HandleInput();
        }

        private void HandleInput()
        {
            if (IsMoving || _movePending) return;

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
            {
                // 被怪物阻挡 → 先做 bump 动画，同时发送请求让服务器判定
                var mm = GetTree()?.GetFirstNodeInGroup("monster_manager") as MonsterManager;
                if (mm != null && mm.IsBlockedByMonster(targetGridPos))
                {
                    _moveFromPos = GridPos;
                    _moveTargetPos = targetGridPos;
                    PlayBumpAnimation(GridPos, targetGridPos);
                    SendMoveStartRequest(GridPos, targetGridPos);
                    return;
                }

                // 被宝箱阻挡 → 自动开箱
                if (gridManager.IsBlockedByChest(targetGridPos))
                {
                    var chestMgr = GetTree()?.GetFirstNodeInGroup("chest_manager") as ChestManager;
                    chestMgr?.TryOpenChestAt(targetGridPos);
                }
                return;
            }

            _moveFromPos = GridPos;
            _moveTargetPos = targetGridPos;
            _movePending = true;

            // 客户端预测：立即开始动画
            GridPos = targetGridPos;
            var targetWorldPos = UiUtils.GridToWorld(GridPos, GridSize);
            IsMoving = true;

            _currentTween = CreateTween();
            _currentTween.SetTrans(Tween.TransitionType.Quad);
            _currentTween.SetEase(Tween.EaseType.Out);
            _currentTween.TweenProperty(this, "position", targetWorldPos, MoveDuration);
            _currentTween.Finished += OnMoveFinished;

            SendMoveStartRequest(_moveFromPos, targetGridPos);
        }

        private void OnMoveFinished()
        {
            IsMoving = false;
            Position = UiUtils.GridToWorld(GridPos, GridSize);
            SendMoveCompleteRequest();
        }

        private void SendMoveStartRequest(Vector2I from, Vector2I to)
        {
            var nm = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
            if (nm == null || !nm.IsServerConnected())
                return;
            if (string.IsNullOrEmpty(nm.GatewayToken))
                return;

            var req = new Game.MoveRequest
            {
                FromX = from.X,
                FromY = from.Y,
                ToX = to.X,
                ToY = to.Y,
                MapName = GetMapName(),
            };
            nm.SendPacket(MessageId.GameMoveReq, req);
        }

        private string GetMapName()
        {
            var nm = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
            return nm?.CurrentMapName ?? "xinshoucun";
        }

        private void OnMoveResponse(Game.MoveResponse rsp)
        {
            _movePending = false;

            if (rsp.Message == "attack")
            {
                // 服务器判定目标格被占 → bump 动画弹回原位
                GD.Print($"[Player] Server acknowledged attack at ({rsp.X}, {rsp.Y})");
                var originPos = new Vector2I((int)rsp.X, (int)rsp.Y);
                GridPos = originPos;
                PlayBumpAnimation(originPos, _moveTargetPos);
                return;
            }

            if (rsp.Code != Common.ErrorCode.Success || rsp.DurationMs <= 0)
            {
                // 服务器拒绝，回滚
                var rollbackPos = new Vector2I((int)rsp.X, (int)rsp.Y);
                if (rollbackPos.X == 0 && rollbackPos.Y == 0)
                    rollbackPos = _moveFromPos;
                GD.Print($"[Player] Move rejected by server, rollback to ({rollbackPos.X}, {rollbackPos.Y})");
                RollbackTo(rollbackPos);
                return;
            }

            // 保存服务器返回的移动参数
            _moveDurationMs = rsp.DurationMs;
            _moveCheckRatio = rsp.CheckRatio;
            _moveDualStartRatio = rsp.DualStartRatio;
            _moveDualEndRatio = rsp.DualEndRatio;

            // 重新调整 tween 时长为服务器指定的时长
            float durationSec = _moveDurationMs / 1000.0f;
            if (Mathf.Abs(durationSec - MoveDuration) > 0.01f && _currentTween != null && GodotObject.IsInstanceValid(_currentTween))
            {
                _currentTween?.Kill();
                var targetWorldPos = UiUtils.GridToWorld(GridPos, GridSize);
                _currentTween = CreateTween();
                _currentTween.SetTrans(Tween.TransitionType.Quad);
                _currentTween.SetEase(Tween.EaseType.Out);
                _currentTween.TweenProperty(this, "position", targetWorldPos, durationSec);
                _currentTween.Finished += OnMoveFinished;
            }

            // 启动检查点定时器
            float checkDelay = _moveDurationMs * _moveCheckRatio / 100.0f / 1000.0f;
            _checkTimer = new Timer();
            _checkTimer.WaitTime = checkDelay;
            _checkTimer.OneShot = true;
            _checkTimer.Timeout += OnMoveCheckPoint;
            AddChild(_checkTimer);
            _checkTimer.Start();
        }

        private void OnMoveCheckPoint()
        {
            // 客户端本地检查目标格是否可行走
            var gridManager = GetParent()?.GetNode<GridManager>("GridManager");
            if (gridManager != null)
            {
                if (!gridManager.IsWalkable(_moveTargetPos))
                {
                    GD.Print($"[Player] Checkpoint: target {_moveTargetPos} not walkable, rolling back");
                    RollbackTo(_moveFromPos);
                    return;
                }
            }

            // 通知服务器确认
            var nm = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
            if (nm != null && nm.IsServerConnected())
            {
                var req = new Game.MoveConfirmRequest
                {
                    TargetX = _moveTargetPos.X,
                    TargetY = _moveTargetPos.Y,
                };
                nm.SendPacket(MessageId.GameMoveConfirmReq, req);
            }
        }

        private void OnMoveCancelReceived(Game.MoveCancelNotify notify)
        {
            if (notify.EntityId != (ulong)GetInstanceId()) return;
            GD.Print($"[Player] Server cancelled move, rollback to ({notify.RollbackX}, {notify.RollbackY})");
            RollbackTo(new Vector2I(notify.RollbackX, notify.RollbackY));
        }

        private void OnRoleAttrUpdated(Game.FullRoleInfo roleInfo)
        {
            ApplyRoleInfo(roleInfo);
        }

        private void OnCombatStateNotify(Game.CombatStateNotify notify)
        {
            var nm = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
            if (nm == null || nm.AccountId == 0) return;

            foreach (var unit in notify.Units)
            {
                if (unit.IsPlayer && unit.EntityId == nm.AccountId)
                {
                    // 更新血条
                    if (unit.MaxHp > 0)
                    {
                        HealthBarFillPercent = (float)unit.Hp / unit.MaxHp;
                        CombatAttrs[1] = unit.Hp;   // HP
                        CombatAttrs[2] = unit.MaxHp; // MaxHp
                        QueueRedraw();
                    }
                    break;
                }
            }
        }

        /// <summary>
        /// 播放撞墙弹回动画：先向目标移动30%，再弹回原位
        /// </summary>
        private void PlayBumpAnimation(Vector2I fromPos, Vector2I targetPos)
        {
            _currentTween?.Kill();
            _checkTimer?.Stop();
            _checkTimer?.QueueFree();
            _checkTimer = null;

            var fromWorld = UiUtils.GridToWorld(fromPos, GridSize);
            var toWorld = UiUtils.GridToWorld(targetPos, GridSize);
            // 30% 位置
            var bumpPos = fromWorld + (toWorld - fromWorld) * 0.3f;

            IsMoving = true;
            float bumpDuration = 0.08f;  // 前30%用时
            float returnDuration = 0.07f; // 弹回用时

            _currentTween = CreateTween();
            _currentTween.SetTrans(Tween.TransitionType.Sine);
            _currentTween.SetEase(Tween.EaseType.Out);
            _currentTween.TweenProperty(this, "position", bumpPos, bumpDuration);
            _currentTween.SetTrans(Tween.TransitionType.Sine);
            _currentTween.SetEase(Tween.EaseType.In);
            _currentTween.TweenProperty(this, "position", fromWorld, returnDuration);
            _currentTween.Finished += () =>
            {
                IsMoving = false;
                Position = fromWorld;
                GridPos = fromPos;
            };
        }

        private void RollbackTo(Vector2I pos)
        {
            _currentTween?.Kill();
            _currentTween = null;
            _checkTimer?.Stop();
            _checkTimer?.QueueFree();
            _checkTimer = null;

            GridPos = pos;
            Position = UiUtils.GridToWorld(GridPos, GridSize);
            IsMoving = false;
            _movePending = false;
        }

        private void SendMoveCompleteRequest()
        {
            var nm = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
            if (nm != null && nm.IsServerConnected())
            {
                var req = new Game.MoveCompleteRequest
                {
                    TargetX = _moveTargetPos.X,
                    TargetY = _moveTargetPos.Y,
                };
                nm.SendPacket(MessageId.GameMoveCompleteReq, req);
            }
        }

    }
}
