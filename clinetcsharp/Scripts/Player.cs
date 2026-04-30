using Godot;
using Godot.Collections;
using Protocol;

namespace ClinetCSharp
{
    /// <summary>
    /// 玩家角色类
    /// </summary>
    [GlobalClass]
    public partial class Player : EntityBase
    {
        private int _gridSize = 111;
        public override int GridSize { get => _gridSize; set => _gridSize = value; }
        // 外观属性已移至 EntityBase，Player 特有属性如下：
        [Export] public float MoveDuration { get; set; } = 0.15f;
        [Export] public int FontSizeOverride { get; set; } = 0;
        [Export] public float LineSpacing { get; set; } = 0.8f;
        [Export] public float LetterSpacing { get; set; } = 0.0f;

        // 字体设置
        public string CurrentFontPath { get; set; } = "";

        // 字体样式
        public bool FontBold { get; set; } = false;
        public bool FontItalic { get; set; } = false;
        public bool FontShadow { get; set; } = false;

        // 调试信息显示
        public bool ShowDebugInfo { get; set; } = true;

        // 每行文字的颜色（4行）
        public Array<Color> LineColors { get; set; } = new Array<Color> { Colors.Black, Colors.Black, Colors.Black, Colors.Black };

        // 角色信息（4行）
        public int Level { get; set; } = 1;
        public string CharacterName { get; set; } = "王建国";
        public string Job { get; set; } = "农夫";
        public string Title { get; set; } = "普通人";
        public string Status { get; set; } = "闲逛中...";

        // ========== 战斗属性（从服务器 attrs 同步） ==========
        public System.Collections.Generic.Dictionary<uint, int> CombatAttrs { get; } = new();

        // ========== 施法条 & 动作栏 — 已下沉到 EntityBase ==========

        /// <summary>调试：强制显示动作栏（忽略 CastingSkill 为空的条件）</summary>
        public bool ActionBarForceShow { get; set; } = false;

        // ========== 等级徽章 ==========
        public Vector2 LevelBadgeOffset { get; set; } = new Vector2(-35, -35);
        public float LevelBadgeFontSize { get; set; } = 12;
        public Color LevelBadgeTextColor { get; set; } = Colors.Yellow;
        public bool LevelBadgeVisible { get; set; } = true;
        public string LevelBadgeText { get; set; } = "Lv.{level}";

        private Vector2I _gridPos = new Vector2I(25, 25);
        public override Vector2I GridPos => _gridPos;

        /// <summary>
        /// 直接传送到指定格子坐标（用于 GM 命令、死亡重生等场景）
        /// </summary>
        public void TeleportToGrid(int x, int y)
        {
            // 停止当前移动
            _currentTween?.Kill();
            _checkTimer?.Stop();
            _checkTimer?.QueueFree();
            _checkTimer = null;
            IsMoving = false;
            _bouncingBack = false;
            _collisionMove = false;
            _movePending = false;

            _gridPos = new Vector2I(x, y);
            _moveFromPos = _gridPos;
            Position = UiUtils.GridToWorld(_gridPos, GridSize);
        }
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

        // 文本内容（可编辑）— Player 用 RichTextLabel 系统，不使用基类的 DrawLabels()
        public string[] PlayerLabelTexts = new string[LabelCount] { $"Lv.1 王建国", "农夫", "普通人", "闲逛中..." };

        public override void _Ready()
        {
            // 提前加载配置，确保 VisualSizeScale 等值在场景显示前就绪
            LoadStyleConfig();

            Position = UiUtils.GridToWorld(_gridPos, GridSize);
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
                nm.PlayerDeathNotify += OnPlayerDeath;
                nm.LevelUpNotify += OnLevelUp;

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
                nm.PlayerDeathNotify -= OnPlayerDeath;
                nm.LevelUpNotify -= OnLevelUp;
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
                lines[i] = PlayerLabelTexts[i];

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
                    // 鼠标在 UI 上时不拖动标签（防止面板穿透）
                    if (UiUtils.IsMouseOverAnyUi(GetViewport())) return;
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
            return PlayerLabelTexts[index];
        }

        public override void SetLabelText(int index, string text)
        {
            if (index < 0 || index >= LabelCount) return;
            PlayerLabelTexts[index] = text;
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

        public override void SetLabelFontSize(int index, int size)
        {
            if (index < 0 || index >= LabelCount) return;
            _labelFontSizes[index] = size;
            UpdateLabelFontSize();
        }

        // --- 保留的旧接口 ---

        public override void SetGridSize(int newSize)
        {
            base.SetGridSize(newSize);
            SetupLabels();
        }

        /// <summary>
        /// 从配置文件加载样式参数，确保 VisualSizeScale 等值在场景显示前就绪
        /// </summary>
        private void LoadStyleConfig()
        {
            var config = new ConfigFile();
            if (config.Load("user://debug_panel_config.cfg") != Error.Ok)
                return;
            if (!config.HasSection("player"))
                return;

            float scale = (float)(double)config.GetValue("player", "visual_size_scale", 1.0);
            if (scale != 1.0f)
                SetVisualSizeScale(scale);

            float borderScale = (float)(double)config.GetValue("player", "border_width_scale", 3.0 / 111.0);
            if (borderScale != (3.0f / 111.0f))
                SetBorderWidthScale(borderScale);
        }

        public override void SetVisualSizeScale(float scale)
        {
            VisualSizeScale = scale;
            SetupLabels();
            QueueRedraw();
        }

        public override void SetBorderWidthScale(float scale)
        {
            BorderWidthScale = scale;
            SetupLabels();
            QueueRedraw();
        }

        public void SetTextAlignment(HorizontalAlignment newAlignment)
        {
            TextAlignment = newAlignment;
            SetupLabels();
        }

        public override void SetFontSize(int size)
        {
            FontSizeOverride = size;
            UpdateLabelFontSize();
        }

        public void SetLineSpacing(float spacing)
        {
            LineSpacing = spacing;
            UpdateLabelFontSize();
        }

        public override void SetCornerRadius(float radius)
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

        public override void SetBgOpacity(float opacity)
        {
            BgOpacity = opacity;
            QueueRedraw();
        }

        public void SetShowDebugInfo(bool show)
        {
            ShowDebugInfo = show;
            QueueRedraw();
        }

        // --- 血条/MP条/施法条/动作栏控制已移至 EntityBase ---

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
            PlayerLabelTexts[0] = $"Lv.{Level} {CharacterName}";
            PlayerLabelTexts[1] = Job;
            PlayerLabelTexts[2] = Title;
            PlayerLabelTexts[3] = Status;

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
                _gridPos = new Vector2I(roleInfo.GridX, roleInfo.GridY);
                _moveFromPos = _gridPos;
                Position = UiUtils.GridToWorld(_gridPos, GridSize);
                GD.Print($"[Player] Set position from server: ({_gridPos.X}, {_gridPos.Y})");
            }

            SetupLabels();
            QueueRedraw();
        }

        // ========== 绘制 ==========

        public override void _Draw()
        {
            var drawSize = VisualSize;
            if (drawSize < 10) drawSize = 10;

            EntityDrawUtils.DrawBody(this, drawSize, BgColor, BgOpacity, BorderColor, BorderWidth, CornerRadius);
            DrawBars();

            // 施法条
            DrawCastBar();

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

            // 动作栏（角色下方）
            if (ActionBarForceShow)
            {
                // 调试模式：临时覆盖基类属性以强制显示
                var origSkill = CastingSkill;
                var origProgress = CastProgress;
                CastingSkill = "烈斩";
                CastProgress = 0.6f;
                DrawActionBar();
                CastingSkill = origSkill;
                CastProgress = origProgress;
            }
            else
            {
                DrawActionBar();
            }
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
            var targetPos = UiUtils.GridToWorld(_gridPos, GridSize);
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
        private Godot.Timer? _checkTimer;
        private bool _movePending = false;
        private bool _collisionMove = false;
        private bool _bouncingBack = false;

        public override void _Process(double _delta)
        {
            if (!IsMoving)
            {
                var targetPos = UiUtils.GridToWorld(_gridPos, GridSize);
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
                MoveTo(_gridPos + direction);
        }

        private void MoveTo(Vector2I target_gridPos)
        {
            var gridManager = GetParent()?.GetNode<GridManager>("GridManager");
            if (gridManager != null && !gridManager.IsWalkable(target_gridPos))
            {
                // 被怪物阻挡 → 碰撞性移动：开始动画，30% 时再检测
                var mm = GetTree()?.GetFirstNodeInGroup("monster_manager") as MonsterManager;
                if (mm != null && mm.IsBlockedByMonster(target_gridPos))
                {
                    _moveFromPos = _gridPos;
                    _moveTargetPos = target_gridPos;
                    _collisionMove = true;
                    _movePending = true;

                    // 不预测 _gridPos（碰撞移动大概率弹回）
                    var targetWorldPos = UiUtils.GridToWorld(target_gridPos, GridSize);
                    IsMoving = true;

                    _currentTween = CreateTween();
                    _currentTween.SetTrans(Tween.TransitionType.Quad);
                    _currentTween.SetEase(Tween.EaseType.Out);
                    _currentTween.TweenProperty(this, "position", targetWorldPos, MoveDuration);
                    _currentTween.Finished += OnMoveFinished;

                    SendMoveStartRequest(_moveFromPos, target_gridPos);
                    return;
                }

                // 被宝箱阻挡 → 自动开箱
                if (gridManager.IsBlockedByChest(target_gridPos))
                {
                    var chestMgr = GetTree()?.GetFirstNodeInGroup("chest_manager") as ChestManager;
                    chestMgr?.TryOpenChestAt(target_gridPos);
                }

                return;
            }

            _moveFromPos = _gridPos;
            _moveTargetPos = target_gridPos;
            _collisionMove = false;
            _movePending = true;

            // 客户端预测：立即开始动画
            _gridPos = target_gridPos;
            var targetWorldPos2 = UiUtils.GridToWorld(_gridPos, GridSize);
            IsMoving = true;

            _currentTween = CreateTween();
            _currentTween.SetTrans(Tween.TransitionType.Quad);
            _currentTween.SetEase(Tween.EaseType.Out);
            _currentTween.TweenProperty(this, "position", targetWorldPos2, MoveDuration);
            _currentTween.Finished += OnMoveFinished;

            SendMoveStartRequest(_moveFromPos, target_gridPos);
        }

        private void OnMoveFinished()
        {
            IsMoving = false;
            if (_collisionMove)
            {
                // 碰撞移动到了终点（敌人已移走），更新 _gridPos
                _gridPos = _moveTargetPos;
                _collisionMove = false;
            }
            Position = UiUtils.GridToWorld(_gridPos, GridSize);
            SendMoveCompleteRequest();

            // 移动完成后检测邻格 NPC，自动弹出交互面板
            CheckAdjacentNpc();
        }

        private void CheckAdjacentNpc()
        {
            var npcMgr = GetTree()?.GetFirstNodeInGroup("npc_manager") as NpcManager;
            if (npcMgr == null) return;
            var npc = npcMgr.GetAdjacentNpc(_gridPos);
            if (npc != null)
                npcMgr.ShowInteractMenu(npc, npc.NpcType, _gridPos);
            else
                npcMgr.CloseInteractMenu();
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
                _gridPos = originPos;
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
                // 碰撞移动用 _moveTargetPos（_gridPos 没预测到目标）
                var tweenTarget = _collisionMove ? _moveTargetPos : _gridPos;
                var targetWorldPos = UiUtils.GridToWorld(tweenTarget, GridSize);
                _currentTween?.Kill();
                _currentTween = CreateTween();
                _currentTween.SetTrans(Tween.TransitionType.Quad);
                _currentTween.SetEase(Tween.EaseType.Out);
                _currentTween.TweenProperty(this, "position", targetWorldPos, durationSec);
                _currentTween.Finished += OnMoveFinished;
            }

            // 启动检查点定时器
            float checkDelay = _moveDurationMs * _moveCheckRatio / 100.0f / 1000.0f;
            _checkTimer = new Godot.Timer();
            _checkTimer.WaitTime = checkDelay;
            _checkTimer.OneShot = true;
            _checkTimer.Timeout += OnMoveCheckPoint;
            AddChild(_checkTimer);
            _checkTimer.Start();
        }

        private void OnMoveCheckPoint()
        {
            // 碰撞性移动：30% 时检测目标格是否有敌人
            if (_collisionMove)
            {
                var mm = GetTree()?.GetFirstNodeInGroup("monster_manager") as MonsterManager;
                if (mm != null && mm.IsBlockedByMonster(_moveTargetPos))
                {
                    GD.Print($"[Player] Checkpoint: enemy detected at {_moveTargetPos}, bouncing back + collision notify");
                    // 弹回原位动画
                    PlayBounceBack(_moveFromPos);
                    // 直接发送碰撞通知，服务端校验后触发战斗
                    // 不再发 MoveConfirmRequest 等待 MoveCancelNotify 回滚
                    SendMoveCollisionNotify(_moveTargetPos);
                }
                else
                {
                    // 30% 时敌人已移走，发正常 ConfirmRequest
                    SendMoveConfirmRequest();
                }
                return;
            }

            // 正常移动：客户端本地检查目标格是否可行走
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

            SendMoveConfirmRequest();
        }

        /// <summary>
        /// 碰撞移动弹回：从当前位置平滑弹回原位
        /// </summary>
        private void PlayBounceBack(Vector2I originPos)
        {
            _currentTween?.Kill();
            _checkTimer?.Stop();
            _checkTimer?.QueueFree();
            _checkTimer = null;

            var originWorld = UiUtils.GridToWorld(originPos, GridSize);
            float duration = 0.1f;

            _bouncingBack = true;
            _currentTween = CreateTween();
            _currentTween.SetTrans(Tween.TransitionType.Quad);
            _currentTween.SetEase(Tween.EaseType.In);
            _currentTween.TweenProperty(this, "position", originWorld, duration);
            _currentTween.Finished += () =>
            {
                IsMoving = false;
                _bouncingBack = false;
                Position = originWorld;
                _gridPos = originPos;
                _collisionMove = false;
            };
        }

        private void SendMoveConfirmRequest()
        {
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

        private void SendMoveCollisionNotify(Vector2I targetPos)
        {
            var nm = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
            if (nm != null && nm.IsServerConnected())
            {
                var notify = new Game.MoveCollisionNotify
                {
                    TargetX = targetPos.X,
                    TargetY = targetPos.Y,
                };
                nm.SendPacket(MessageId.GameMoveCollisionNotify, notify);
            }
        }

        private void OnMoveCancelReceived(Game.MoveCancelNotify notify)
        {
            if (notify.EntityId != (ulong)GetInstanceId()) return;
            GD.Print($"[Player] Server cancelled move, rollback to ({notify.RollbackX}, {notify.RollbackY})");
            _collisionMove = false;

            var rollbackPos = new Vector2I(notify.RollbackX, notify.RollbackY);

            if (_bouncingBack)
            {
                // 正在弹回动画中，只同步逻辑坐标，不中断动画
                _gridPos = rollbackPos;
                return;
            }

            if (!IsMoving)
            {
                _gridPos = rollbackPos;
                Position = UiUtils.GridToWorld(rollbackPos, GridSize);
                return;
            }

            RollbackTo(rollbackPos);
        }

        private void OnRoleAttrUpdated(Game.FullRoleInfo roleInfo)
        {
            ApplyRoleInfo(roleInfo);
        }

        private void OnCombatStateNotify(Game.CombatStateNotify notify)
        {
            var nm = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
            if (nm == null || nm.AccountId == 0) return;

            bool found = false;
            foreach (var unit in notify.Units)
            {
                if (unit.IsPlayer && unit.EntityId == nm.AccountId)
                {
                    if (unit.MaxHp > 0)
                    {
                        HealthBarFillPercent = (float)unit.Hp / unit.MaxHp;
                        CombatAttrs[1] = unit.Hp;
                        CombatAttrs[2] = unit.MaxHp;
                    }
                    if (unit.MaxMp > 0)
                    {
                        MpBarFillPercent = (float)unit.Mp / unit.MaxMp;
                        CombatAttrs[3] = unit.Mp;
                        CombatAttrs[4] = unit.MaxMp;
                    }
                    CastingSkill = unit.CastingSkill;
                    CastProgress = unit.CastProgress;
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                CastingSkill = "";
                CastProgress = 0;
            }
            QueueRedraw();
        }

        private void OnLevelUp(Game.LevelUpNotify notify)
        {
            GD.Print("[Player] Level Up! ", notify.OldLevel, " → ", notify.NewLevel);
            // LevelBadgeText 保持模板 "Lv.{level}"，_Draw 里会替换 {level}
            QueueRedraw();
        }

        private void OnPlayerDeath(Game.PlayerDeathNotify notify)
        {
            GD.Print("[Player] Death! Respawning at (", notify.SpawnX, ",", notify.SpawnY, ")");

            // 停止所有移动
            _currentTween?.Kill();
            _currentTween = null;
            _checkTimer?.Stop();
            _checkTimer?.QueueFree();
            _checkTimer = null;
            IsMoving = false;
            _bouncingBack = false;
            _collisionMove = false;
            _movePending = false;

            // 传送到出生点（死亡允许瞬移）
            _gridPos = new Vector2I(notify.SpawnX, notify.SpawnY);
            _moveFromPos = _gridPos;
            Position = UiUtils.GridToWorld(_gridPos, GridSize);

            // 恢复满血满蓝
            HealthBarFillPercent = 1.0f;
            if (notify.MaxHp > 0)
            {
                CombatAttrs[1] = notify.Hp;
                CombatAttrs[2] = notify.MaxHp;
            }
            if (notify.MaxMp > 0)
            {
                CombatAttrs[3] = notify.Mp;
                CombatAttrs[4] = notify.MaxMp;
            }

            // 重置战斗状态
            CastingSkill = "";
            CastProgress = 0;
            QueueRedraw();
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
                _gridPos = fromPos;
            };
        }

        public override void RollbackTo(Vector2I pos)
        {
            _currentTween?.Kill();
            _currentTween = null;
            _checkTimer?.Stop();
            _checkTimer?.QueueFree();
            _checkTimer = null;

            var targetWorld = UiUtils.GridToWorld(pos, GridSize);
            float dist = Position.DistanceTo(targetWorld);

            if (dist > 1.0f)
            {
                // 有视觉距离时平滑弹回，不瞬移
                float duration = Mathf.Clamp(dist / 800f, 0.05f, 0.12f);
                _bouncingBack = true;
                _currentTween = CreateTween();
                _currentTween.SetTrans(Tween.TransitionType.Quad);
                _currentTween.SetEase(Tween.EaseType.In);
                _currentTween.TweenProperty(this, "position", targetWorld, duration);
                _currentTween.Finished += () =>
                {
                    IsMoving = false;
                    _bouncingBack = false;
                    _gridPos = pos;
                    Position = UiUtils.GridToWorld(pos, GridSize);
                    _currentTween = null;
                };
                IsMoving = true;
            }
            else
            {
                IsMoving = false;
                _bouncingBack = false;
                _gridPos = pos;
                Position = targetWorld;
            }

            _movePending = false;
            _collisionMove = false;
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
