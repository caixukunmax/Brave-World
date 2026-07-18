using Godot;
using ClinetCSharp.RenderComponents;
using System.Collections.Generic;
using System.Linq;

namespace ClinetCSharp
{
    /// <summary>
    /// 实体基类 — 玩家、怪物、NPC 共享的外观、标签、血条/MP条
    /// 设计原则：Scale 是唯一真相源，绝对值是计算属性，不可能不一致
    /// </summary>
    public abstract partial class EntityBase : Node2D
    {
        // ========== 回弹动画全局配置（可通过 DebugPanel 实时调整）==========
        public static float BounceBackDuration { get; set; } = 0.06f;
        public static float BounceBackOvershootRatio { get; set; } = 0.08f;
        public static float BounceBackOvershootThreshold { get; set; } = 0.40f;

        // ========== 受击特效全局配置（可通过 DebugPanel 实时调整）==========
        public static float HitEffectDuration { get; set; } = 0.12f;
        public static float HitShakeAmplitude { get; set; } = 3.0f;
        public static float HitFlashIntensity { get; set; } = 0.3f;

        // ========== 方向箭头全局配置（可通过 DebugPanel 实时调整）==========
        public static int DirectionArrowStyle { get; set; } = 0;       // 0-5 六种样式
        public static float DirectionArrowSize { get; set; } = 0.35f;  // 相对 VisualSize 的比例
        public static Color DirectionArrowColor { get; set; } = new Color(1f, 0.9f, 0.2f, 0.9f); // 金黄色
        public static float DirectionArrowAlpha { get; set; } = 0.9f;  // 半透明度
        /// <summary>四个方向的偏移: 右[0], 下[1], 左[2], 上[3]</summary>
        public static Vector2[] DirectionArrowOffsets { get; set; } = new Vector2[4]
        {
            new Vector2(15, 0),   // 右: 箭头在角色右侧
            new Vector2(0, 15),   // 下: 箭头在角色下方
            new Vector2(-15, 0),  // 左: 箭头在角色左侧
            new Vector2(0, -15),  // 上: 箭头在角色上方
        };
        /// <summary>四个方向的旋转角度(度): 右[0], 下[1], 左[2], 上[3]</summary>
        public static float[] DirectionArrowAngles { get; set; } = new float[4] { 0f, 90f, 180f, 270f };

        // ========== 攻击抖动全局配置（可通过 DebugPanel 实时调整）==========
        public static float AttackShakeDuration { get; set; } = 0.08f;
        public static float AttackShakeDistance { get; set; } = 12.0f;

        // ========== 渲染组件系统 ==========
        protected readonly List<IRenderComponent> _renderComponents = new();

        /// <summary>添加渲染组件（自动按 DrawOrder 排序）</summary>
        public void AddRenderComponent(IRenderComponent component)
        {
            _renderComponents.Add(component);
            _renderComponents.Sort((a, b) => a.DrawOrder.CompareTo(b.DrawOrder));
            component.OnAttach(this);
        }

        /// <summary>获取指定类型的渲染组件</summary>
        public T? GetRenderComponent<T>() where T : class, IRenderComponent
            => _renderComponents.OfType<T>().FirstOrDefault();

        /// <summary>移除指定类型的渲染组件</summary>
        public void RemoveRenderComponent<T>() where T : class, IRenderComponent
        {
            var comp = GetRenderComponent<T>();
            if (comp != null)
            {
                comp.OnDetach(this);
                _renderComponents.Remove(comp);
            }
        }

        // ========== Profile 绑定 ==========
        /// <summary>实体绑定的配置档案 ID，-1 表示未绑定</summary>
        public int ProfileId { get; set; } = -1;

        // ========== 外观 ==========
        public float VisualSizeScale { get; set; } = 1.0f;
        public float BorderWidthScale { get; set; } = 3.0f / 111.0f;
        public Color BorderColor { get; set; } = Colors.White;
        public Color BgColor { get; set; } = new Color(1, 1, 1, 0.1f);
        public Color TextColor { get; set; } = Colors.Black;
        public float CornerRadius { get; set; } = 12.0f;
        public float BgOpacity { get; set; } = 0.1f;
        public int FontSize { get; set; } = 0; // 0 = 自动

        // ========== 外观 — 计算属性（只读） ==========
        // 注意：以下三个属性保持旧的正方形语义，用于 Player/Monster 等 1x1 实体以及血条/标签等附属组件。
        public int VisualOuterSize
        {
            get
            {
                int baseSize = Mathf.Min(GridSize * Mathf.Max(1, GridSizeX), GridSize * Mathf.Max(1, GridSizeY));
                return Mathf.Clamp(Mathf.RoundToInt(baseSize * VisualSizeScale), 10, baseSize);
            }
        }
        public int BorderWidth => Mathf.Clamp(Mathf.RoundToInt(GridSize * BorderWidthScale), 1, Mathf.Max(1, VisualOuterSize / 2));
        public int VisualSize => Mathf.Max(2, VisualOuterSize - BorderWidth * 2);

        // 多格建筑使用矩形尺寸，按 footprint 实际宽高填充。
        public int VisualOuterSizeX => Mathf.Clamp(Mathf.RoundToInt(GridSize * Mathf.Max(1, GridSizeX) * VisualSizeScale), 10, GridSize * Mathf.Max(1, GridSizeX));
        public int VisualOuterSizeY => Mathf.Clamp(Mathf.RoundToInt(GridSize * Mathf.Max(1, GridSizeY) * VisualSizeScale), 10, GridSize * Mathf.Max(1, GridSizeY));
        public int VisualSizeX => Mathf.Max(2, VisualOuterSizeX - BorderWidth * 2);
        public int VisualSizeY => Mathf.Max(2, VisualOuterSizeY - BorderWidth * 2);

        // ========== 血量（绝对值 + 百分比） ==========
        public int CurrentHp { get; set; } = 0;
        public int CurrentMaxHp { get; set; } = 0;

        // ========== 血条 ==========
        public Vector2 HealthBarOffset { get; set; } = new Vector2(0, -70);
        public float HealthBarLengthScale { get; set; } = 102.0f / 111.0f;
        public float HealthBarHeightScale { get; set; } = 6.0f / 111.0f;
        public Color HealthBarColor { get; set; } = new Color(0, 0.8f, 0, 1);
        public Color HealthBarBgColor { get; set; } = new Color(0.3f, 0.3f, 0.3f, 0.5f);
        public bool HealthBarVisible { get; set; } = true;
        public bool HealthBarCenterX { get; set; } = true;
        public float HealthBarFillPercent { get; set; } = 1.0f;

        // ========== 血条 — 计算属性（只读） ==========
        public float HealthBarLength => Mathf.Clamp(VisualOuterSize * HealthBarLengthScale, 10.0f, GridSize * 2.0f);
        public float HealthBarHeight => Mathf.Clamp(GridSize * HealthBarHeightScale, 2.0f, GridSize);

        // ========== MP条 ==========
        public Vector2 MpBarOffset { get; set; } = new Vector2(0, -62);
        public float MpBarLengthScale { get; set; } = 80.0f / 111.0f;
        public float MpBarHeightScale { get; set; } = 4.0f / 111.0f;
        public Color MpBarColor { get; set; } = new Color(0.2f, 0.4f, 1.0f, 1);
        public Color MpBarBgColor { get; set; } = new Color(0.3f, 0.3f, 0.3f, 0.5f);
        public bool MpBarVisible { get; set; } = true;
        public bool MpBarCenterX { get; set; } = true;
        public float MpBarFillPercent { get; set; } = 1.0f;

        // ========== MP条 — 计算属性（只读） ==========
        public float MpBarLength => Mathf.Clamp(VisualOuterSize * MpBarLengthScale, 10.0f, GridSize * 2.0f);
        public float MpBarHeight => Mathf.Clamp(GridSize * MpBarHeightScale, 2.0f, GridSize);

        // ========== 4 行文字标签 ==========
        public string[] LabelTexts = new string[4] { "", "", "", "" };
        public int[] LabelFontSizes = new int[4] { 0, 0, 0, 0 };
        public float[] LabelXOffsets = new float[4] { 0, 0, 0, 0 };
        public bool[] LabelCenterX = new bool[4] { true, true, true, true };
        public float[] LabelYOffsets = new float[4] { 0, 0, 0, 0 };

        // ========== 铭牌背景 ==========
        public bool NameplateVisible { get; set; } = false;
        public float NameplateYOffset { get; set; } = -80f;
        public float NameplateSpacing { get; set; } = 4f;
        public float NameplateBarHeight { get; set; } = 6f;
        public Color NameplateBarColor { get; set; } = new Color(0.1f, 0.1f, 0.1f, 0.7f);
        public float NameplateCenterBoxHeight { get; set; } = 24f;
        public float NameplateCenterBoxWidthScale { get; set; } = 0.6f;
        public Color NameplateCenterBoxColor { get; set; } = new Color(0.1f, 0.1f, 0.1f, 0.85f);

        // ========== 全局标签可见性开关 ==========
        /// <summary>全局标签可见性（调试面板控制，影响所有实体的 RichTextLabel）</summary>
        public static bool GlobalLabelsVisible = true;
        public static bool GlobalDebugOverlayVisible = false;

        // ========== RichTextLabel 控件（所有实体共用） ==========
        private const int LabelCount = 4;
        private RichTextLabel?[] _labels = new RichTextLabel?[LabelCount];
        private Control?[] _labelContainers = new Control?[LabelCount];
        private bool[] _labelVisible = new bool[LabelCount] { true, true, true, true };
        private bool _useRichLabels = false; // 子类 SetupRichLabels() 后为 true

        // ========== 格子坐标 ==========
        public Vector2I GridPos => GetGridPos();
        protected virtual Vector2I GetGridPos() => Vector2I.Zero;

        // ========== 占地大小（格子数） ==========
        public int GridSizeX { get; set; } = 1;
        public int GridSizeY { get; set; } = 1;

        /// <summary>占地范围的左上角格子坐标（GridPos 表示占地中心）</summary>
        public Vector2I GridAnchor => new(
            GridPos.X - (GridSizeX - 1) / 2,
            GridPos.Y - (GridSizeY - 1) / 2);

        /// <summary>把占地中心格子坐标转成世界坐标（渲染中心）</summary>
        public Vector2 GetWorldPositionForGridPos(Vector2I gridPos)
        {
            int sizeX = Mathf.Max(1, GridSizeX);
            int sizeY = Mathf.Max(1, GridSizeY);
            var anchor = new Vector2I(
                gridPos.X - (sizeX - 1) / 2,
                gridPos.Y - (sizeY - 1) / 2);
            return GetWorldPositionForGridAnchor(anchor);
        }

        /// <summary>把占地左上角格子坐标转成世界坐标（渲染中心）</summary>
        public Vector2 GetWorldPositionForGridAnchor(Vector2I anchor)
        {
            int sizeX = Mathf.Max(1, GridSizeX);
            int sizeY = Mathf.Max(1, GridSizeY);
            return new Vector2(
                anchor.X * GridSize + GridSize * sizeX / 2.0f,
                anchor.Y * GridSize + GridSize * sizeY / 2.0f);
        }

        /// <summary>占地大小变化后的回调，子类可重写以更新渲染位置</summary>
        public virtual void OnGridSizeChanged() { }

        // ========== 朝向 ==========
        private int _direction = 1; // 默认向下
        /// <summary>朝向: 0=右, 1=下, 2=左, 3=上</summary>
        public int Direction
        {
            get => _direction;
            set { _direction = Mathf.PosMod(value, 4); QueueRedraw(); }
        }

        /// <summary>从移动向量推导 4 方向索引 (0=右, 1=下, 2=左, 3=上)</summary>
        public static int DirectionFromVector(int dx, int dy)
        {
            if (dx > 0) return 0;
            if (dx < 0) return 2;
            if (dy > 0) return 1;
            if (dy < 0) return 3;
            return 1;
        }

        // ========== 移动基础 ==========
        protected Tween _currentTween;
        public bool IsMoving { get; set; } = false;

        // ========== 施法条（从 Player 下沉） ==========
        public Vector2 CastBarOffset { get; set; } = new Vector2(0, -80);
        public bool CastBarCenterX { get; set; } = true;
        public float CastBarLengthScale { get; set; } = 60.0f / 111.0f;
        public float CastBarHeightScale { get; set; } = 4.0f / 111.0f;
        public Color CastBarColor { get; set; } = new Color(0.3f, 0.5f, 1, 1);
        public Color CastBarBgColor { get; set; } = new Color(0.3f, 0.3f, 0.3f, 0.4f);
        public bool CastBarVisible { get; set; } = true;
        public float CastBarFillPercent { get; set; } = 0.0f;
        public float CastBarLength => Mathf.Clamp(VisualOuterSize * CastBarLengthScale, 10.0f, GridSize * 2.0f);
        public float CastBarHeight => Mathf.Clamp(GridSize * CastBarHeightScale, 2.0f, GridSize);

        // ========== 战斗状态 ==========
        public bool IsInCombat { get; set; } = false;

        // ========== 动作栏（Monster 已在用，Player 也有） ==========
        public string CastingSkill { get; set; } = "";
        public float CastProgress { get; set; } = 0f;
        public bool ActionBarForceShow { get; set; } = false;
        public float ActionBarTextYOffset { get; set; } = 0f;
        public float ActionBarProgressHeight { get; set; } = 4f;

        private Tween _castTween;

        /// <summary>设置施法进度，同时同步施法条填充百分比并重绘</summary>
        public void SetCastProgress(float progress)
        {
            CastProgress = Mathf.Clamp(progress, 0f, 1f);
            CastBarFillPercent = CastProgress;
            QueueRedraw();
        }

        /// <summary>启动本地施法读条动画，从左到右填充</summary>
        public void StartCastAnimation(float duration)
        {
            StopCastAnimation();
            if (duration <= 0f)
            {
                SetCastProgress(1f);
                return;
            }

            SetCastProgress(0f);
            _castTween = CreateTween();
            _castTween.TweenMethod(Callable.From<float>(SetCastProgress), 0.0f, 1.0f, duration)
                .SetTrans(Tween.TransitionType.Linear)
                .SetEase(Tween.EaseType.InOut);
        }

        /// <summary>停止本地施法读条动画</summary>
        public void StopCastAnimation()
        {
            if (_castTween != null && GodotObject.IsInstanceValid(_castTween))
            {
                _castTween.Kill();
                _castTween = null;
            }
        }

        // ========== GridSize ==========
        private int _gridSize = 111;
        public int GridSize { get => GetGridSize(); set => SetGridSizeValue(value); }
        protected virtual int GetGridSize() => _gridSize;
        protected virtual void SetGridSizeValue(int value) => _gridSize = value;

        // ========== 外观 setter ==========
        public virtual void SetVisualSizeScale(float scale) { VisualSizeScale = scale; UpdateRichLabelFontSize(); QueueRedraw(); }
        public virtual void SetBorderWidthScale(float scale) { BorderWidthScale = scale; QueueRedraw(); }
        public void SetBorderColor(Color color) { BorderColor = color; QueueRedraw(); }
        public void SetBgColor(Color color) { BgColor = color; QueueRedraw(); }
        public void SetTextColor(Color color) { TextColor = color; UpdateRichLabelColors(); QueueRedraw(); }
        public virtual void SetCornerRadius(float radius) { CornerRadius = radius; QueueRedraw(); }
        public virtual void SetBgOpacity(float opacity) { BgOpacity = opacity; QueueRedraw(); }
        public virtual void SetFontSize(int size) { FontSize = size; UpdateRichLabelFontSize(); QueueRedraw(); }

        // ========== 血条 setter ==========
        public Vector2 GetHealthBarOffset() => HealthBarOffset;
        public void SetHealthBarOffset(Vector2 offset) { HealthBarOffset = offset; QueueRedraw(); }
        public void SetHealthBarLengthScale(float scale) { HealthBarLengthScale = scale; QueueRedraw(); }
        public void SetHealthBarHeightScale(float scale) { HealthBarHeightScale = scale; QueueRedraw(); }
        public void SetHealthBarColor(Color color) { HealthBarColor = color; QueueRedraw(); }
        public void SetHealthBarBgColor(Color color) { HealthBarBgColor = color; QueueRedraw(); }
        public void SetHealthBarFillPercent(float percent) { HealthBarFillPercent = Mathf.Clamp(percent, 0, 1); QueueRedraw(); }

        /// <summary>同步 HP 绝对值和百分比，返回是否发生了受击（HP 下降）</summary>
        public bool SyncHp(int hp, int maxHp)
        {
            bool wasHit = hp < CurrentHp && CurrentHp > 0;
            CurrentHp = hp;
            CurrentMaxHp = maxHp;
            if (maxHp > 0)
                HealthBarFillPercent = Mathf.Clamp((float)hp / maxHp, 0, 1);
            return wasHit;
        }
        public void SetHealthBarVisible(bool visible) { HealthBarVisible = visible; QueueRedraw(); }

        // ========== MP条 setter ==========
        public Vector2 GetMpBarOffset() => MpBarOffset;
        public void SetMpBarOffset(Vector2 offset) { MpBarOffset = offset; QueueRedraw(); }
        public void SetMpBarLengthScale(float scale) { MpBarLengthScale = scale; QueueRedraw(); }
        public void SetMpBarHeightScale(float scale) { MpBarHeightScale = scale; QueueRedraw(); }
        public void SetMpBarColor(Color color) { MpBarColor = color; QueueRedraw(); }
        public void SetMpBarBgColor(Color color) { MpBarBgColor = color; QueueRedraw(); }
        public void SetMpBarFillPercent(float percent) { MpBarFillPercent = Mathf.Clamp(percent, 0, 1); QueueRedraw(); }
        public void SetMpBarVisible(bool visible) { MpBarVisible = visible; QueueRedraw(); }

        // ========== 施法条 setter ==========
        public Vector2 GetCastBarOffset() => CastBarOffset;
        public void SetCastBarOffset(Vector2 offset) { CastBarOffset = offset; QueueRedraw(); }
        public void SetCastBarCenterX(bool center) { CastBarCenterX = center; QueueRedraw(); }
        public void SetCastBarLengthScale(float scale) { CastBarLengthScale = scale; QueueRedraw(); }
        public void SetCastBarHeightScale(float scale) { CastBarHeightScale = scale; QueueRedraw(); }
        public void SetCastBarColor(Color color) { CastBarColor = color; QueueRedraw(); }
        public void SetCastBarBgColor(Color color) { CastBarBgColor = color; QueueRedraw(); }
        public void SetCastBarFillPercent(float percent) { CastBarFillPercent = Mathf.Clamp(percent, 0, 1); QueueRedraw(); }
        public void SetCastBarVisible(bool visible) { CastBarVisible = visible; QueueRedraw(); }

        // ========== 动作栏 setter ==========
        public void SetActionBarTextYOffset(float offset) { ActionBarTextYOffset = offset; QueueRedraw(); }
        public void SetActionBarProgressHeight(float height) { ActionBarProgressHeight = Mathf.Max(height, 1f); QueueRedraw(); }

        // ========== 标签 setter ==========
        public virtual void SetLabelText(int index, string text)
        {
            if (index < 0 || index >= 4) return;
            LabelTexts[index] = text;
            if (_useRichLabels && _labels[index] != null)
            {
                _labels[index]!.Text = text;
                UpdateRichLabelPositions();
            }
            QueueRedraw();
        }
        public virtual void SetLabelFontSize(int index, int size)
        {
            if (index < 0 || index >= 4) return;
            LabelFontSizes[index] = size;
            UpdateRichLabelFontSize();
            QueueRedraw();
        }
        public void SetLabelXOffset(int index, float offset)
        {
            if (index < 0 || index >= 4) return;
            LabelXOffsets[index] = offset;
            UpdateRichLabelPositions();
            QueueRedraw();
        }
        public void SetLabelCenterX(int index, bool center)
        {
            if (index < 0 || index >= 4) return;
            LabelCenterX[index] = center;
            UpdateRichLabelPositions();
            QueueRedraw();
        }
        public void SetLabelYOffset(int index, float offset)
        {
            if (index < 0 || index >= 4) return;
            LabelYOffsets[index] = offset;
            UpdateRichLabelPositions();
            QueueRedraw();
        }

        // ========== 点击检测 ==========
        /// <summary>实体被点击时触发，参数为被点击的实体</summary>
        public static event System.Action<EntityBase> EntityClicked;

        /// <summary>检测鼠标点击是否命中实体，子类 override _Input 时应调用此方法</summary>
        /// <returns>命中并触发 EntityClicked 时返回 true</returns>
        protected bool CheckEntityClick(InputEvent @event)
        {
            if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left && mb.Pressed)
            {
                // GetGlobalMousePosition 返回世界坐标，ToLocal 转为本地坐标
                var worldMouse = GetGlobalMousePosition();
                var localMouse = ToLocal(worldMouse);
                float halfW = GridSize * Mathf.Max(1, GridSizeX) / 2.0f;
                float halfH = GridSize * Mathf.Max(1, GridSizeY) / 2.0f;
                var rect = new Rect2(new Vector2(-halfW, -halfH), new Vector2(halfW * 2, halfH * 2));
                if (rect.HasPoint(localMouse))
                {
                    EntityClicked?.Invoke(this);
                    return true;
                }
            }
            return false;
        }

        // ========== 绘制 ==========
        protected void DrawBars()
        {
            EntityDrawUtils.DrawHealthBar(this, HealthBarOffset, HealthBarLength, HealthBarHeight,
                HealthBarFillPercent, HealthBarBgColor, HealthBarColor, HealthBarVisible);

            EntityDrawUtils.DrawHealthBar(this, MpBarOffset, MpBarLength, MpBarHeight,
                MpBarFillPercent, MpBarBgColor, MpBarColor, MpBarVisible);
        }

        protected void DrawLabels()
        {
            var font = ThemeDB.FallbackFont;
            int baseFs = EntityLabelLayout.ResolveBaseFontSize(FontSize, VisualSize);

            for (int i = 0; i < 4; i++)
            {
                if (string.IsNullOrEmpty(LabelTexts[i])) continue;
                int fs = LabelFontSizes[i] > 0 ? LabelFontSizes[i] : baseFs;
                Vector2 lineCenter = EntityLabelLayout.ResolveLineCenter(
                    i,
                    FontSize,
                    VisualSize,
                    LabelCenterX[i],
                    LabelXOffsets[i],
                    LabelYOffsets[i]);

                var textSize = font.GetStringSize(LabelTexts[i], HorizontalAlignment.Left, -1, fs);
                float drawX = lineCenter.X - textSize.X / 2f;
                float baselineY = lineCenter.Y + (font.GetAscent(fs) - font.GetDescent(fs)) * 0.5f;
                DrawString(font, new Vector2(drawX, baselineY), LabelTexts[i], HorizontalAlignment.Left, -1, fs, TextColor);
            }
        }

        // ========== RichTextLabel 控件管理 ==========

        /// <summary>初始化 RichTextLabel 控件（替代 DrawString 画文字）</summary>
        protected void SetupRichLabels()
        {
            _useRichLabels = true;
            var font = ThemeDB.FallbackFont;
            for (int i = 0; i < LabelCount; i++)
            {
                if (_labelContainers[i] != null && IsInstanceValid(_labelContainers[i]))
                {
                    _labelContainers[i]!.QueueFree();
                    _labelContainers[i] = null;
                    _labels[i] = null;
                }

                // 根据当前文本预计算合理的最小尺寸，避免 FitContent 延迟导致 Size 为 (1,1)
                int baseFs = FontSize > 0 ? FontSize : Mathf.Max((int)(VisualSize / 4.0f * 0.7f), 8);
                int fs = LabelFontSizes[i] > 0 ? LabelFontSizes[i] : baseFs;
                var textSize = font.GetStringSize(LabelTexts[i], HorizontalAlignment.Center, -1, fs);
                var minSize = new Vector2(Mathf.Max(1, textSize.X), Mathf.Max(1, textSize.Y));

                var container = new Control
                {
                    Name = $"LabelContainer_{i}",
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                };
                var label = new RichTextLabel
                {
                    Name = $"Label_{i}",
                    FitContent = true,
                    ScrollActive = false,
                    BbcodeEnabled = false,
                    Text = LabelTexts[i],
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    AutowrapMode = TextServer.AutowrapMode.Off,
                    CustomMinimumSize = minSize,
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                };
                label.AddThemeColorOverride("font_color", TextColor);
                container.AddChild(label);
                AddChild(container);
                _labelContainers[i] = container;
                _labels[i] = label;
            }
            UpdateRichLabelPositions();
            UpdateRichLabelFontSize();
        }

        /// <summary>更新所有 RichTextLabel 的位置</summary>
        public void UpdateRichLabelPositions()
        {
            if (!_useRichLabels) return;

            // 计算行高和起始 Y（和旧 LabelComponent 一致：4 行垂直居中）
            int baseFs = FontSize > 0 ? FontSize : Mathf.Max((int)(VisualSize / 4.0f * 0.7f), 8);
            float baseLineHeight = baseFs * 1.1f;
            float totalHeight = baseLineHeight * 4;
            float startY = -(totalHeight / 2.0f) + baseLineHeight * 0.5f;
            var font = ThemeDB.FallbackFont;

            for (int i = 0; i < LabelCount; i++)
            {
                if (_labelContainers[i] == null) continue;
                _labelContainers[i]!.Visible = GlobalLabelsVisible && _labelVisible[i] && !string.IsNullOrEmpty(LabelTexts[i]);
                if (!_labelVisible[i]) continue;

                var label = _labels[i];
                if (label == null) continue;

                // 同步计算文本大小，避免 RichTextLabel 异步布局（FitContent）导致 GetMinimumSize 延迟/错误
                int fs = LabelFontSizes[i] > 0 ? LabelFontSizes[i] : baseFs;
                var textSize = font.GetStringSize(LabelTexts[i], HorizontalAlignment.Center, -1, fs);
                // Y: 居中排列 + 用户偏移
                float posY = startY + i * baseLineHeight + LabelYOffsets[i];
                // X: 居中或用户偏移
                float posX = LabelCenterX[i] ? 0 : LabelXOffsets[i];
                var pos = new Vector2(posX, posY) - textSize / 2;
                if (LabelCenterX[i])
                    pos.X = -textSize.X / 2;
                _labelContainers[i]!.Position = pos;
            }
        }

        /// <summary>更新所有 RichTextLabel 的字号</summary>
        public void UpdateRichLabelFontSize()
        {
            if (!_useRichLabels) return;
            int baseFs = FontSize > 0 ? FontSize : Mathf.Max((int)(VisualSize / 4.0f * 0.7f), 8);
            for (int i = 0; i < LabelCount; i++)
            {
                if (_labels[i] == null) continue;
                int fontSize = LabelFontSizes[i] > 0 ? LabelFontSizes[i] : baseFs;
                _labels[i]!.AddThemeFontSizeOverride("normal_font_size", fontSize);
            }
            UpdateRichLabelPositions();
        }

        /// <summary>更新所有 RichTextLabel 的颜色</summary>
        public void UpdateRichLabelColors()
        {
            if (!_useRichLabels) return;
            for (int i = 0; i < LabelCount; i++)
            {
                if (_labels[i] == null) continue;
                _labels[i]!.AddThemeColorOverride("font_color", TextColor);
            }
        }

        /// <summary>
        /// 强制刷新标签位置和字号（用于预览面板在 AddChild 后重新计算）。
        /// 子类（如 Player）可 override 以适配自己的标签系统。
        /// </summary>
        public virtual void RefreshLabels()
        {
            UpdateRichLabelFontSize();
            UpdateRichLabelPositions();
        }

        /// <summary>设置标签可见性</summary>
        public virtual void SetLabelVisible(int index, bool visible)
        {
            if (index < 0 || index >= LabelCount) return;
            _labelVisible[index] = visible;
            UpdateRichLabelPositions();
        }

        /// <summary>获取标签可见性</summary>
        public virtual bool GetLabelVisible(int index)
        {
            if (index < 0 || index >= LabelCount) return false;
            return _labelVisible[index];
        }

        /// <summary>同步更新 RichTextLabel 的 CustomMinimumSize，避免 FitContent 异步计算延迟</summary>
        private void SyncLabelMinSize(int index)
        {
            if (_labels[index] == null) return;
            var font = ThemeDB.FallbackFont;
            int baseFs = FontSize > 0 ? FontSize : Mathf.Max((int)(VisualSize / 4.0f * 0.7f), 8);
            int fs = LabelFontSizes[index] > 0 ? LabelFontSizes[index] : baseFs;
            var textSize = font.GetStringSize(LabelTexts[index], HorizontalAlignment.Center, -1, fs);
            _labels[index]!.CustomMinimumSize = new Vector2(Mathf.Max(1, textSize.X), Mathf.Max(1, textSize.Y));
        }

        /// <summary>设置标签文字（RichTextLabel 版）</summary>
        public void SetRichLabelText(int index, string text)
        {
            if (index < 0 || index >= LabelCount) return;
            LabelTexts[index] = text;
            if (_labels[index] != null)
            {
                _labels[index]!.Text = text;
                SyncLabelMinSize(index);
                UpdateRichLabelPositions();
            }
        }

        /// <summary>是否使用 RichTextLabel 控件</summary>
        public bool UseRichLabels => _useRichLabels;

        /// <summary>刷新标签可见性（子类可 override，Player 用自己的标签系统）</summary>
        public virtual void RefreshLabelVisibility()
        {
            UpdateRichLabelPositions();
        }

        // ========== 通用方法 ==========

        public virtual void SetGridSize(int size)
        {
            GridSize = size;
            Position = GetWorldPositionForGridPos(GridPos);
            UpdateRichLabelFontSize();
            QueueRedraw();
        }

        public virtual bool HitTest(Vector2 worldPos)
        {
            float halfW = GridSize * Mathf.Max(1, GridSizeX) / 2.0f;
            float halfH = GridSize * Mathf.Max(1, GridSizeY) / 2.0f;
            var worldCenter = GetWorldPositionForGridPos(GridPos);
            return Mathf.Abs(worldPos.X - worldCenter.X) < halfW &&
                   Mathf.Abs(worldPos.Y - worldCenter.Y) < halfH;
        }

        public virtual void ApplyStyle(EntityStyleConfig cfg)
        {
            if (cfg == null) return;
            VisualSizeScale = cfg.VisualSizeScale;
            BorderWidthScale = cfg.BorderWidthScale;
            CornerRadius = cfg.CornerRadius;
            BgOpacity = cfg.BgOpacity;
            FontSize = cfg.FontSize;
            BorderColor = cfg.BorderColor;
            BgColor = cfg.BgColor;
            TextColor = cfg.TextColor;

            for (int i = 0; i < 4; i++)
            {
                if (!string.IsNullOrEmpty(cfg.LabelTexts[i]))
                    SetRichLabelText(i, cfg.LabelTexts[i]);
                LabelFontSizes[i] = cfg.LabelFontSizes[i];
                LabelXOffsets[i] = cfg.LabelXOffsets[i];
                LabelCenterX[i] = cfg.LabelCenterX[i];
                LabelYOffsets[i] = cfg.LabelYOffsets[i];
            }

            UpdateRichLabelFontSize();
            UpdateRichLabelPositions();

            HealthBarVisible = cfg.HpBarVisible;
            HealthBarCenterX = cfg.HpBarCenterX;
            HealthBarLengthScale = cfg.HpBarLengthScale;
            HealthBarHeightScale = cfg.HpBarHeightScale;
            HealthBarFillPercent = cfg.HpBarFillPercent;
            HealthBarOffset = new Vector2(cfg.HpBarOffsetX, cfg.HpBarOffsetY);
            HealthBarColor = cfg.HpBarColor;

            MpBarVisible = cfg.MpBarVisible;
            MpBarCenterX = cfg.MpBarCenterX;
            MpBarLengthScale = cfg.MpBarLengthScale;
            MpBarHeightScale = cfg.MpBarHeightScale;
            MpBarFillPercent = cfg.MpBarFillPercent;
            MpBarOffset = new Vector2(cfg.MpBarOffsetX, cfg.MpBarOffsetY);
            MpBarColor = cfg.MpBarColor;

            QueueRedraw();
        }

        public virtual void MoveTo(Vector2I targetGridPos, float duration = 0.15f)
        {
            IsMoving = true;
            _currentTween?.Kill();
            _currentTween = CreateTween();
            _currentTween.SetTrans(Tween.TransitionType.Quad);
            _currentTween.SetEase(Tween.EaseType.Out);
            _currentTween.TweenProperty(this, "position", GetWorldPositionForGridPos(targetGridPos), duration);
            _currentTween.Finished += () => { IsMoving = false; _currentTween = null; };
        }

        public virtual void RollbackTo(Vector2I pos)
        {
            _currentTween?.Kill();
            _currentTween = null;
            IsMoving = false;
            Position = GetWorldPositionForGridPos(pos);
            QueueRedraw();
        }

        /// <summary>
        /// 播放受击特效：颜色闪烁 + 原地抖动
        /// </summary>
        public virtual void PlayHitEffect()
        {
            PlayHitFlash();
            if (!IsMoving)
                PlayHitShake();
        }

        private void PlayHitFlash()
        {
            var flashTween = CreateTween();
            flashTween.TweenProperty(this, "modulate", new Color(1, HitFlashIntensity, HitFlashIntensity), HitEffectDuration * 0.3f);
            flashTween.TweenProperty(this, "modulate", Colors.White, HitEffectDuration * 0.7f);
        }

        private void PlayHitShake()
        {
            var originalX = Position.X;
            var shakeTween = CreateTween();
            shakeTween.SetTrans(Tween.TransitionType.Sine);

            // 快速左右抖动
            shakeTween.TweenProperty(this, "position:x", originalX + HitShakeAmplitude, 0.02f);
            shakeTween.TweenProperty(this, "position:x", originalX - HitShakeAmplitude, 0.02f);
            shakeTween.TweenProperty(this, "position:x", originalX + HitShakeAmplitude * 0.5f, 0.02f);
            shakeTween.TweenProperty(this, "position:x", originalX - HitShakeAmplitude * 0.5f, 0.02f);
            shakeTween.TweenProperty(this, "position:x", originalX, 0.02f);

            shakeTween.Finished += () =>
            {
                // 确保 X 回到原位，Y 保持当前值（可能已被其他逻辑更新）
                Position = new Vector2(originalX, Position.Y);
            };
        }

        /// <summary>
        /// 播放攻击抖动：向目标方向快速前冲后回弹
        /// </summary>
        public virtual void PlayAttackShake(Vector2I? targetGridPos = null)
        {
            if (IsMoving) return;

            var originalPos = Position;
            Vector2 dir;

            if (targetGridPos.HasValue)
            {
                var targetWorld = GetWorldPositionForGridPos(targetGridPos.Value);
                dir = (targetWorld - originalPos).Normalized();
            }
            else
            {
                dir = Vector2.Right; // 默认向右
            }

            var attackPos = originalPos + dir * AttackShakeDistance;

            var tween = CreateTween();
            tween.SetTrans(Tween.TransitionType.Quad);
            tween.SetEase(Tween.EaseType.Out);
            tween.TweenProperty(this, "position", attackPos, AttackShakeDuration * 0.4f);
            tween.TweenProperty(this, "position", originalPos, AttackShakeDuration * 0.6f);

            tween.Finished += () =>
            {
                Position = originalPos;
            };
        }

        public virtual void PlayBounceBack(Vector2I originPos, float duration = -1f)
        {
            _currentTween?.Kill();
            IsMoving = true;
            var originWorld = GetWorldPositionForGridPos(originPos);
            _currentTween = CreateTween();
            _currentTween.SetTrans(Tween.TransitionType.Cubic);
            _currentTween.SetEase(Tween.EaseType.In);
            float d = duration < 0 ? BounceBackDuration : duration;
            _currentTween.TweenProperty(this, "position", originWorld, d);
            _currentTween.Finished += () =>
            {
                IsMoving = false;
                _currentTween = null;
                Position = originWorld;
            };
        }

        // ========== 绘制辅助 ==========

        protected void DrawCastBar()
        {
            if (!CastBarVisible) return;
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

        protected void DrawActionBar()
        {
            EntityDrawUtils.DrawActionBar(this, VisualSize, CastingSkill, CastProgress, ActionBarTextYOffset, ActionBarProgressHeight);
        }
    }
}
