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
    }
}
