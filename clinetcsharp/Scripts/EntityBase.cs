using Godot;

namespace ClinetCSharp
{
    /// <summary>
    /// 实体基类 — 玩家、怪物、NPC 共享的外观、标签、血条/MP条
    /// 设计原则：Scale 是唯一真相源，绝对值是计算属性，不可能不一致
    /// </summary>
    public abstract partial class EntityBase : Node2D
    {
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
        public int VisualSize => Mathf.Clamp((int)(GridSize * VisualSizeScale), 10, GridSize);
        public float BorderWidth => Mathf.Clamp(GridSize * BorderWidthScale, 1.0f, 20.0f);

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
        public float HealthBarLength => Mathf.Clamp(GridSize * HealthBarLengthScale, 10.0f, GridSize * 2.0f);
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
        public float MpBarLength => Mathf.Clamp(GridSize * MpBarLengthScale, 10.0f, GridSize * 2.0f);
        public float MpBarHeight => Mathf.Clamp(GridSize * MpBarHeightScale, 2.0f, GridSize);

        // ========== 4 行文字标签 ==========
        public string[] LabelTexts = new string[4] { "", "", "", "" };
        public int[] LabelFontSizes = new int[4] { 0, 0, 0, 0 };
        public float[] LabelXOffsets = new float[4] { 0, 0, 0, 0 };
        public bool[] LabelCenterX = new bool[4] { true, true, true, true };
        public float[] LabelYOffsets = new float[4] { 0, 0, 0, 0 };

        // ========== 格子坐标 ==========
        public Vector2I GridPos => GetGridPos();
        protected virtual Vector2I GetGridPos() => Vector2I.Zero;

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
        public float CastBarLength => Mathf.Clamp(GridSize * CastBarLengthScale, 10.0f, GridSize * 2.0f);
        public float CastBarHeight => Mathf.Clamp(GridSize * CastBarHeightScale, 2.0f, GridSize);

        // ========== 动作栏（Monster 已在用，Player 也有） ==========
        public string CastingSkill { get; set; } = "";
        public float CastProgress { get; set; } = 0f;
        public float ActionBarTextYOffset { get; set; } = 0f;
        public float ActionBarProgressHeight { get; set; } = 4f;

        // ========== GridSize ==========
        private int _gridSize = 111;
        public int GridSize { get => GetGridSize(); set => SetGridSizeValue(value); }
        protected virtual int GetGridSize() => _gridSize;
        protected virtual void SetGridSizeValue(int value) => _gridSize = value;

        // ========== 外观 setter ==========
        public virtual void SetVisualSizeScale(float scale) { VisualSizeScale = scale; QueueRedraw(); }
        public virtual void SetBorderWidthScale(float scale) { BorderWidthScale = scale; QueueRedraw(); }
        public void SetBorderColor(Color color) { BorderColor = color; QueueRedraw(); }
        public void SetBgColor(Color color) { BgColor = color; QueueRedraw(); }
        public void SetTextColor(Color color) { TextColor = color; QueueRedraw(); }
        public virtual void SetCornerRadius(float radius) { CornerRadius = radius; QueueRedraw(); }
        public virtual void SetBgOpacity(float opacity) { BgOpacity = opacity; QueueRedraw(); }
        public virtual void SetFontSize(int size) { FontSize = size; QueueRedraw(); }

        // ========== 血条 setter ==========
        public Vector2 GetHealthBarOffset() => HealthBarOffset;
        public void SetHealthBarOffset(Vector2 offset) { HealthBarOffset = offset; QueueRedraw(); }
        public void SetHealthBarLengthScale(float scale) { HealthBarLengthScale = scale; QueueRedraw(); }
        public void SetHealthBarHeightScale(float scale) { HealthBarHeightScale = scale; QueueRedraw(); }
        public void SetHealthBarColor(Color color) { HealthBarColor = color; QueueRedraw(); }
        public void SetHealthBarBgColor(Color color) { HealthBarBgColor = color; QueueRedraw(); }
        public void SetHealthBarFillPercent(float percent) { HealthBarFillPercent = Mathf.Clamp(percent, 0, 1); QueueRedraw(); }
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
            QueueRedraw();
        }
        public virtual void SetLabelFontSize(int index, int size)
        {
            if (index < 0 || index >= 4) return;
            LabelFontSizes[index] = size;
            QueueRedraw();
        }
        public void SetLabelXOffset(int index, float offset)
        {
            if (index < 0 || index >= 4) return;
            LabelXOffsets[index] = offset;
            QueueRedraw();
        }
        public void SetLabelCenterX(int index, bool center)
        {
            if (index < 0 || index >= 4) return;
            LabelCenterX[index] = center;
            QueueRedraw();
        }
        public void SetLabelYOffset(int index, float offset)
        {
            if (index < 0 || index >= 4) return;
            LabelYOffsets[index] = offset;
            QueueRedraw();
        }

        // ========== 点击检测 ==========
        /// <summary>实体被点击时触发，参数为被点击的实体</summary>
        public static event System.Action<EntityBase> EntityClicked;

        /// <summary>检测鼠标点击是否命中实体，子类 override _Input 时应调用此方法</summary>
        protected void CheckEntityClick(InputEvent @event)
        {
            if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left && mb.Pressed)
            {
                if (UiUtils.IsMouseOverAnyUi(GetViewport())) return;
                var localMouse = ToLocal(mb.GlobalPosition);
                float half = VisualSize / 2.0f;
                var rect = new Rect2(new Vector2(-half, -half), new Vector2(VisualSize, VisualSize));
                GD.Print($"[EntityBase] CheckEntityClick: localMouse={localMouse:F1}, half={half:F1}, hit={rect.HasPoint(localMouse)}");
                if (rect.HasPoint(localMouse))
                {
                    GD.Print($"[EntityBase] EntityClicked: {GetType().Name}");
                    EntityClicked?.Invoke(this);
                }
            }
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
            int baseFs = FontSize > 0 ? FontSize : Mathf.Max((int)(VisualSize / 4.0f * 0.7f), 8);
            float baseLineHeight = baseFs * 1.1f;
            float totalHeight = baseLineHeight * 4;
            float startY = -(totalHeight / 2.0f) + baseLineHeight * 0.5f;

            for (int i = 0; i < 4; i++)
            {
                if (string.IsNullOrEmpty(LabelTexts[i])) continue;
                int fs = LabelFontSizes[i] > 0 ? LabelFontSizes[i] : baseFs;
                float posY = startY + i * baseLineHeight + LabelYOffsets[i];
                float posX = LabelCenterX[i] ? 0 : LabelXOffsets[i];

                var textSize = font.GetStringSize(LabelTexts[i], HorizontalAlignment.Left, -1, fs);
                float drawX = posX - textSize.X / 2f;
                float baselineY = posY + (font.GetAscent(fs) - font.GetDescent(fs)) * 0.5f;
                DrawString(font, new Vector2(drawX, baselineY), LabelTexts[i], HorizontalAlignment.Left, -1, fs, TextColor);
            }
        }

        // ========== 通用方法 ==========

        public virtual void SetGridSize(int size)
        {
            GridSize = size;
            Position = UiUtils.GridToWorld(GridPos, GridSize);
            QueueRedraw();
        }

        public virtual bool HitTest(Vector2 worldPos)
        {
            float half = GridSize / 2.0f;
            var worldCenter = UiUtils.GridToWorld(GridPos, GridSize);
            return Mathf.Abs(worldPos.X - worldCenter.X) < half &&
                   Mathf.Abs(worldPos.Y - worldCenter.Y) < half;
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
                    LabelTexts[i] = cfg.LabelTexts[i];
                LabelFontSizes[i] = cfg.LabelFontSizes[i];
                LabelXOffsets[i] = cfg.LabelXOffsets[i];
                LabelCenterX[i] = cfg.LabelCenterX[i];
                LabelYOffsets[i] = cfg.LabelYOffsets[i];
            }

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
            _currentTween.TweenProperty(this, "position", UiUtils.GridToWorld(targetGridPos, GridSize), duration);
            _currentTween.Finished += () => { IsMoving = false; _currentTween = null; };
        }

        public virtual void RollbackTo(Vector2I pos)
        {
            _currentTween?.Kill();
            _currentTween = null;
            IsMoving = false;
            Position = UiUtils.GridToWorld(pos, GridSize);
            QueueRedraw();
        }

        public virtual void PlayBounceBack(Vector2I originPos, float duration = 0.12f)
        {
            _currentTween?.Kill();
            IsMoving = true;
            var originWorld = UiUtils.GridToWorld(originPos, GridSize);
            _currentTween = CreateTween();
            _currentTween.SetTrans(Tween.TransitionType.Quad);
            _currentTween.SetEase(Tween.EaseType.In);
            _currentTween.TweenProperty(this, "position", originWorld, duration);
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
