using Godot;
namespace ClinetCSharp
{
    public partial class DebugPanelSystemTab
    {
        public override void BuildUI(VBoxContainer tabContainer)
        {
            _tabContainer = tabContainer;
            AddTabTitle(tabContainer, "全局系统配置", 13);
            AddSectionSeparator(tabContainer);

            var configManager = Owner.GetTree()?.GetFirstNodeInGroup("monster_config_manager") as MonsterConfigManager;

            // ---- 移动系统配置 (JSON) ----
            AddTabTitle(tabContainer, "移动系统配置 (JSON)", 12, HorizontalAlignment.Left);
            var moveSystem = configManager?.GetMoveSystem() ?? new MoveSystem();
            (_moveCheckRatioSlider, _moveCheckRatioValue) = CreateMonsterSliderRow(tabContainer, "检查点比例(%)", 0, 100, moveSystem.CheckRatio, 5f);
            (_moveDualStartSlider, _moveDualStartValue) = CreateMonsterSliderRow(tabContainer, "双格开始(%)", 0, 100, moveSystem.DualGridStartRatio, 5f);
            (_moveDualEndSlider, _moveDualEndValue) = CreateMonsterSliderRow(tabContainer, "双格结束(%)", 0, 100, moveSystem.DualGridEndRatio, 5f);

            // ---- 回弹动画配置 ----
            AddSectionSeparator(tabContainer);
            AddTabTitle(tabContainer, "回弹动画配置", 12, HorizontalAlignment.Left);
            (_bounceDurationSlider, _bounceDurationValue) = CreateMonsterSliderRow(tabContainer, "回弹时长(s)", 0.01f, 0.30f, EntityBase.BounceBackDuration, 0.01f);
            (_bounceOvershootRatioSlider, _bounceOvershootRatioValue) = CreateMonsterSliderRow(tabContainer, "前冲比例(%)", 0, 50, EntityBase.BounceBackOvershootRatio * 100f, 1f);
            (_bounceOvershootThresholdSlider, _bounceOvershootThresholdValue) = CreateMonsterSliderRow(tabContainer, "前冲阈值(%)", 0, 100, EntityBase.BounceBackOvershootThreshold * 100f, 5f);

            // ---- 怪物死亡效果 ----
            AddSectionSeparator(tabContainer);
            AddTabTitle(tabContainer, "怪物死亡效果", 12, HorizontalAlignment.Left);
            BuildDeathEffectSection(tabContainer);

            // ---- 背包调试配置 ----
            AddSectionSeparator(tabContainer);
            AddTabTitle(tabContainer, "背包", 12, HorizontalAlignment.Left);
            BuildInventorySection(tabContainer);

            // ---- 朝向箭头配置 ----
            AddSectionSeparator(tabContainer);
            AddTabTitle(tabContainer, "朝向箭头", 12, HorizontalAlignment.Left);
            BuildDirectionArrowSection(tabContainer);

            // ---- 剧情窗口配置 ----
            AddSectionSeparator(tabContainer);
            AddTabTitle(tabContainer, "剧情窗口", 12, HorizontalAlignment.Left);
            BuildStoryPanelSection(tabContainer);
        }

        private void BuildDeathEffectSection(Container parent)
        {
            var mm = MonsterManager;

            // 效果模式选择
            var modeRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            modeRow.AddChild(new Label { Text = "死亡效果:", CustomMinimumSize = new Vector2(80, 0) });
            _deathEffectOption = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            _deathEffectOption.AddItem("直接删除", 0);
            _deathEffectOption.AddItem("淡出消失", 1);
            _deathEffectOption.AddItem("变灰停留后淡出", 2);
            _deathEffectOption.Select(mm?.DeathEffectMode ?? 1);
            modeRow.AddChild(_deathEffectOption);
            parent.AddChild(modeRow);

            // 淡出持续时间
            (_deathFadeDurationSlider, _deathFadeDurationValue) = CreateMonsterSliderRow(parent, "淡出时长(秒)", 0.1f, 3.0f, mm?.DeathFadeDuration ?? 0.5f, 0.1f);
            // 变灰停留时间
            (_deathGrayDelaySlider, _deathGrayDelayValue) = CreateMonsterSliderRow(parent, "变灰停留(秒)", 0.5f, 10.0f, mm?.DeathGrayDelay ?? 3.0f, 0.5f);

            // 绑定事件
            _deathEffectOption.ItemSelected += OnDeathEffectModeChanged;
            _deathFadeDurationSlider.ValueChanged += OnDeathFadeDurationChanged;
            _deathGrayDelaySlider.ValueChanged += OnDeathGrayDelayChanged;
            _moveCheckRatioSlider.ValueChanged += OnMoveCheckRatioChanged;
            _moveDualStartSlider.ValueChanged += OnMoveDualStartChanged;
            _moveDualEndSlider.ValueChanged += OnMoveDualEndChanged;
            _bounceDurationSlider.ValueChanged += OnBounceDurationChanged;
            _bounceOvershootRatioSlider.ValueChanged += OnBounceOvershootRatioChanged;
            _bounceOvershootThresholdSlider.ValueChanged += OnBounceOvershootThresholdChanged;
        }

        private void BuildInventorySection(Container parent)
        {
            (_backpackPaddingHSlider, _backpackPaddingHValue) = CreateSliderRow(parent, "左右边距(px)", 0, 50, InventoryUI.BackpackPaddingH, 1f);
            (_backpackPaddingTopSlider, _backpackPaddingTopValue) = CreateSliderRow(parent, "上边距(px)", 0, 50, InventoryUI.BackpackPaddingTop, 1f);
            (_backpackContentWidthSlider, _backpackContentWidthValue) = CreateSliderRow(parent, "背包区总宽(px)", 200, 800, InventoryUI.BackpackContentWidth, InventoryUI.InventoryFontSize);
            (_backpackAreaHeightSlider, _backpackAreaHeightValue) = CreateSliderRow(parent, "背包区总高(px)", 50, 600, InventoryUI.BackpackAreaHeight, 1f);
            (_backpackItemSpacingSlider, _backpackItemSpacingValue) = CreateSliderRow(parent, "道具间距(px)", 0, 32, InventoryUI.BackpackItemSpacing, 1f);

            _lockHorizontalResizeCheck = CreateCheckRow(parent, "锁定水平缩放", InventoryUI.LockHorizontalResize);
            _lockVerticalResizeCheck = CreateCheckRow(parent, "锁定垂直缩放", InventoryUI.LockVerticalResize);
            _backpackDebugBorderCheck = CreateCheckRow(parent, "显示背包Debug边框", InventoryUI.DebugDrawItemBorder);
            _backpackShowDimensionsCheck = CreateCheckRow(parent, "显示道具占用像素", InventoryUI.DebugShowItemDimensions);
            (_backpackHoverCornerRadiusSlider, _backpackHoverCornerRadiusValue) = CreateSliderRow(parent, "悬停圆角(px)", 0, 16, InventoryUI.BackpackHoverCornerRadius, 1f);
            (_backpackReorderDurationSlider, _backpackReorderDurationValue) =
                CreateSliderRow(parent, "重排动画时长(s)", 0.0f, 0.5f, InventoryUI.BackpackReorderAnimationDuration, 0.01f);
            (_backpackHoverBorderWidthSlider, _backpackHoverBorderWidthValue) = CreateSliderRow(parent, "悬停边框宽(px)", 1, 4, InventoryUI.BackpackHoverBoxBorderWidth, 1f);
            BuildHoverColorRow(parent);

            _backpackPaddingHSlider.ValueChanged += OnBackpackPaddingHChanged;
            _backpackPaddingTopSlider.ValueChanged += OnBackpackPaddingTopChanged;
            _backpackContentWidthSlider.ValueChanged += OnBackpackContentWidthChanged;
            _backpackAreaHeightSlider.ValueChanged += OnBackpackAreaHeightChanged;
            _backpackItemSpacingSlider.ValueChanged += OnBackpackItemSpacingChanged;
            _lockHorizontalResizeCheck.Toggled += OnLockHorizontalResizeToggled;
            _lockVerticalResizeCheck.Toggled += OnLockVerticalResizeToggled;
            _backpackDebugBorderCheck.Toggled += OnBackpackDebugBorderToggled;
            _backpackShowDimensionsCheck.Toggled += OnBackpackShowDimensionsToggled;
            _backpackHoverCornerRadiusSlider.ValueChanged += OnBackpackHoverCornerRadiusChanged;
            _backpackReorderDurationSlider.ValueChanged += OnBackpackReorderDurationChanged;
            _backpackHoverBorderWidthSlider.ValueChanged += OnBackpackHoverBorderWidthChanged;
            _backpackHoverColorPicker.ColorChanged += OnBackpackHoverColorChanged;
        }

        private CheckButton CreateCheckRow(Container parent, string label, bool initial)
        {
            var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            var lbl = new Label { Text = label + ":", CustomMinimumSize = new Vector2(80, 0) };
            lbl.Name = "_lbl";
            row.AddChild(lbl);

            var check = new CheckButton { ButtonPressed = initial, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            row.AddChild(check);
            parent.AddChild(row);
            return check;
        }

        private void ApplyInventorySettings()
        {
            var inventory = Owner.GetTree()?.GetFirstNodeInGroup("inventory_ui") as InventoryUI;
            inventory?.ApplyRuntimeConfig();
            ScheduleSave();
        }

        private void OnBackpackPaddingHChanged(double value)
        {
            InventoryUI.BackpackPaddingH = (int)value;
            _backpackPaddingHValue.Text = ((int)value).ToString();
            ApplyInventorySettings();
        }

        private void OnBackpackPaddingTopChanged(double value)
        {
            InventoryUI.BackpackPaddingTop = (int)value;
            _backpackPaddingTopValue.Text = ((int)value).ToString();
            ApplyInventorySettings();
        }

        private void OnBackpackContentWidthChanged(double value)
        {
            InventoryUI.BackpackContentWidth = (int)value;
            _backpackContentWidthValue.Text = ((int)value).ToString();
            ApplyInventorySettings();
        }

        private void OnBackpackAreaHeightChanged(double value)
        {
            InventoryUI.BackpackAreaHeight = (int)value;
            _backpackAreaHeightValue.Text = ((int)value).ToString();
            ApplyInventorySettings();
        }

        private void OnBackpackItemSpacingChanged(double value)
        {
            InventoryUI.BackpackItemSpacing = (int)value;
            _backpackItemSpacingValue.Text = ((int)value).ToString();
            ApplyInventorySettings();
        }

        private void OnLockHorizontalResizeToggled(bool pressed)
        {
            InventoryUI.LockHorizontalResize = pressed;
            ApplyInventorySettings();
        }

        private void OnLockVerticalResizeToggled(bool pressed)
        {
            InventoryUI.LockVerticalResize = pressed;
            ApplyInventorySettings();
        }

        private void OnBackpackDebugBorderToggled(bool pressed)
        {
            InventoryUI.DebugDrawItemBorder = pressed;
            ApplyInventorySettings();
        }

        private void OnBackpackShowDimensionsToggled(bool pressed)
        {
            InventoryUI.DebugShowItemDimensions = pressed;
            ApplyInventorySettings();
        }

        private void OnBackpackHoverCornerRadiusChanged(double value)
        {
            InventoryUI.BackpackHoverCornerRadius = (int)value;
            _backpackHoverCornerRadiusValue.Text = ((int)value).ToString();
            ApplyInventorySettings();
        }

        private void OnBackpackHoverBorderWidthChanged(double value)
        {
            InventoryUI.BackpackHoverBoxBorderWidth = (int)value;
            _backpackHoverBorderWidthValue.Text = ((int)value).ToString();
            ApplyInventorySettings();
        }

        private void OnBackpackReorderDurationChanged(double value)
        {
            InventoryUI.BackpackReorderAnimationDuration = (float)value;
            _backpackReorderDurationValue.Text = value.ToString("F2");
            ApplyInventorySettings();
        }

        private void BuildHoverColorRow(Container parent)
        {
            var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            var lbl = new Label { Text = "悬停框颜色:", CustomMinimumSize = new Vector2(80, 0) };
            lbl.Name = "_lbl";
            row.AddChild(lbl);

            _backpackHoverColorPicker = new ColorPickerButton
            {
                CustomMinimumSize = new Vector2(60, 24),
                Color = InventoryUI.BackpackHoverBoxColor,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            row.AddChild(_backpackHoverColorPicker);
            parent.AddChild(row);
        }

        private void OnBackpackHoverColorChanged(Color color)
        {
            InventoryUI.BackpackHoverBoxColor = color;
            ApplyInventorySettings();
        }

        private void OnDeathEffectModeChanged(long index)
        {
            if (MonsterManager != null)
                MonsterManager.DeathEffectMode = (int)index;
        }
        private void OnDeathFadeDurationChanged(double value)
        {
            if (MonsterManager != null) MonsterManager.DeathFadeDuration = (float)value;
        }
        private void OnDeathGrayDelayChanged(double value)
        {
            if (MonsterManager != null) MonsterManager.DeathGrayDelay = (float)value;
        }
        private void OnMoveCheckRatioChanged(double value)
        {
            var configManager = Owner.GetTree()?.GetFirstNodeInGroup("monster_config_manager") as MonsterConfigManager;
            if (configManager != null) configManager.Config.MoveSystem.CheckRatio = (int)value;
        }
        private void OnMoveDualStartChanged(double value)
        {
            var configManager = Owner.GetTree()?.GetFirstNodeInGroup("monster_config_manager") as MonsterConfigManager;
            if (configManager != null) configManager.Config.MoveSystem.DualGridStartRatio = (int)value;
        }
        private void OnMoveDualEndChanged(double value)
        {
            var configManager = Owner.GetTree()?.GetFirstNodeInGroup("monster_config_manager") as MonsterConfigManager;
            if (configManager != null) configManager.Config.MoveSystem.DualGridEndRatio = (int)value;
        }
        private void OnBounceDurationChanged(double value)
        {
            EntityBase.BounceBackDuration = (float)value;
        }
        private void OnBounceOvershootRatioChanged(double value)
        {
            EntityBase.BounceBackOvershootRatio = (float)value / 100f;
        }
        private void OnBounceOvershootThresholdChanged(double value)
        {
            EntityBase.BounceBackOvershootThreshold = (float)value / 100f;
        }

        // ---- 朝向箭头配置 ----

        private void BuildDirectionArrowSection(Container parent)
        {
            // 箭头样式下拉
            var styleRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            styleRow.AddChild(new Label { Text = "箭头样式:", CustomMinimumSize = new Vector2(80, 0) });
            _directionArrowStyleOption = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            _directionArrowStyleOption.AddItem("三角箭头", 0);
            _directionArrowStyleOption.AddItem("V形双线", 1);
            _directionArrowStyleOption.AddItem("扇形锥形", 2);
            _directionArrowStyleOption.AddItem("细长指针", 3);
            _directionArrowStyleOption.AddItem("圆点指针", 4);
            _directionArrowStyleOption.AddItem("罗盘针", 5);
            _directionArrowStyleOption.Select(EntityBase.DirectionArrowStyle);
            styleRow.AddChild(_directionArrowStyleOption);
            parent.AddChild(styleRow);

            // 箭头大小滑块
            (_directionArrowSizeSlider, _directionArrowSizeValue) = CreateSliderRow(parent, "箭头大小", 0.1f, 2.0f, EntityBase.DirectionArrowSize, 0.05f);

            // 箭头颜色
            var colorRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            colorRow.AddChild(new Label { Text = "箭头颜色:", CustomMinimumSize = new Vector2(80, 0) });
            _directionArrowColorPicker = new ColorPickerButton
            {
                CustomMinimumSize = new Vector2(60, 24),
                Color = EntityBase.DirectionArrowColor,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            colorRow.AddChild(_directionArrowColorPicker);
            parent.AddChild(colorRow);

            // 箭头透明度
            (_directionArrowAlphaSlider, _directionArrowAlphaValue) = CreateSliderRow(parent, "透明度", 0f, 1f, EntityBase.DirectionArrowAlpha, 0.05f);

            // ---- 四个方向分别配置 ----
            string[] dirNames = { "右 (→)", "下 (↓)", "左 (←)", "上 (↑)" };
            for (int d = 0; d < 4; d++)
            {
                int dir = d; // capture for lambda
                AddSectionSeparator(parent);
                AddTabTitle(parent, dirNames[dir], 11, HorizontalAlignment.Left);

                (_directionArrowOffsetXSliders[dir], _directionArrowOffsetXValues[dir]) =
                    CreateSliderRow(parent, "X偏移", -100f, 100f, EntityBase.DirectionArrowOffsets[dir].X, 1f);
                (_directionArrowOffsetYSliders[dir], _directionArrowOffsetYValues[dir]) =
                    CreateSliderRow(parent, "Y偏移", -100f, 100f, EntityBase.DirectionArrowOffsets[dir].Y, 1f);
                (_directionArrowAngleSliders[dir], _directionArrowAngleValues[dir]) =
                    CreateSliderRow(parent, "角度", -180f, 180f, EntityBase.DirectionArrowAngles[dir], 1f);

                _directionArrowOffsetXSliders[dir].ValueChanged += v => OnDirectionOffsetChanged(dir, 0, v);
                _directionArrowOffsetYSliders[dir].ValueChanged += v => OnDirectionOffsetChanged(dir, 1, v);
                _directionArrowAngleSliders[dir].ValueChanged += v => OnDirectionAngleChanged(dir, v);
            }

            // 绑定事件
            _directionArrowStyleOption.ItemSelected += OnDirectionArrowStyleChanged;
            _directionArrowSizeSlider.ValueChanged += OnDirectionArrowSizeChanged;
            _directionArrowColorPicker.ColorChanged += OnDirectionArrowColorChanged;
            _directionArrowAlphaSlider.ValueChanged += OnDirectionArrowAlphaChanged;
        }

        private void OnDirectionArrowAlphaChanged(double value)
        {
            EntityBase.DirectionArrowAlpha = (float)value;
            _directionArrowAlphaValue.Text = value.ToString("F2");
            RefreshAllEntityRedraws();
        }

        private void OnDirectionArrowStyleChanged(long index)
        {
            EntityBase.DirectionArrowStyle = (int)index;
            RefreshAllEntityRedraws();
        }

        private void OnDirectionArrowSizeChanged(double value)
        {
            EntityBase.DirectionArrowSize = (float)value;
            _directionArrowSizeValue.Text = value.ToString("F2");
            RefreshAllEntityRedraws();
        }

        private void OnDirectionArrowColorChanged(Color color)
        {
            EntityBase.DirectionArrowColor = color;
            RefreshAllEntityRedraws();
        }

        private void OnDirectionOffsetChanged(int dir, int axis, double value)
        {
            var off = EntityBase.DirectionArrowOffsets[dir];
            if (axis == 0) off.X = (float)value;
            else off.Y = (float)value;
            EntityBase.DirectionArrowOffsets[dir] = off;
            if (axis == 0) _directionArrowOffsetXValues[dir].Text = value.ToString("F0");
            else _directionArrowOffsetYValues[dir].Text = value.ToString("F0");
            RefreshAllEntityRedraws();
        }

        private void OnDirectionAngleChanged(int dir, double value)
        {
            EntityBase.DirectionArrowAngles[dir] = (float)value;
            _directionArrowAngleValues[dir].Text = value.ToString("F0");
            RefreshAllEntityRedraws();
        }

        private void RefreshAllEntityRedraws()
        {
            // 遍历场景中所有实体刷新箭头
            foreach (var node in Owner.GetTree().GetNodesInGroup("monster"))
                if (node is EntityBase e) e.QueueRedraw();
            foreach (var node in Owner.GetTree().GetNodesInGroup("npc"))
                if (node is EntityBase e) e.QueueRedraw();
            var player = Owner.GetTree()?.GetFirstNodeInGroup("player") as EntityBase;
            player?.QueueRedraw();
        }

        private void BuildStoryPanelSection(Container parent)
        {
            (_storyPanelWidthSlider, _storyPanelWidthValue) = CreateSliderRow(parent, "窗口宽度(px)", 200, 800, StoryPanel.StoryPanelWidth, 10f);
            (_storyPanelHeightSlider, _storyPanelHeightValue) = CreateSliderRow(parent, "窗口高度(px)", 120, 500, StoryPanel.StoryPanelHeight, 10f);
            (_storyFontSizeSlider, _storyFontSizeValue) = CreateSliderRow(parent, "字号(px)", 12, 32, StoryPanel.StoryFontSize, 1f);
            (_storyPanelAlphaSlider, _storyPanelAlphaValue) = CreateSliderRow(parent, "背景透明度(%)", 0, 100, StoryPanel.StoryPanelAlpha, 1f);
            (_storyContentPaddingSlider, _storyContentPaddingValue) = CreateSliderRow(parent, "内容边距(px)", 0, 64, StoryPanel.StoryContentPadding, 1f);

            _storyTypewriterCheck = new CheckBox { Text = "逐字显示" };
            _storyTypewriterCheck.ButtonPressed = StoryPanel.StoryTypewriterEnabled;
            parent.AddChild(_storyTypewriterCheck);

            (_storyTypewriterIntervalSlider, _storyTypewriterIntervalValue) = CreateSliderRow(parent, "字间间隔(ms)", 20, 200, StoryPanel.StoryTypewriterIntervalMs, 5f);
            (_storyHistorySpacingSlider, _storyHistorySpacingValue) = CreateSliderRow(parent, "历史条目间距(px)", 0, 32, StoryPanel.StoryHistoryEntrySpacing, 1f);

            _storyPanelWidthSlider.ValueChanged += OnStoryPanelWidthChanged;
            _storyPanelHeightSlider.ValueChanged += OnStoryPanelHeightChanged;
            _storyFontSizeSlider.ValueChanged += OnStoryFontSizeChanged;
            _storyPanelAlphaSlider.ValueChanged += OnStoryPanelAlphaChanged;
            _storyContentPaddingSlider.ValueChanged += OnStoryContentPaddingChanged;
            _storyTypewriterCheck.Toggled += OnStoryTypewriterToggled;
            _storyTypewriterIntervalSlider.ValueChanged += OnStoryTypewriterIntervalChanged;
            _storyHistorySpacingSlider.ValueChanged += OnStoryHistorySpacingChanged;
        }
    }
}
