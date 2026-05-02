using Godot;
using System.Collections.Generic;
using System.Linq;

namespace ClinetCSharp
{
    /// <summary>
    /// DebugPanel Monster Tab — 怪物全局样式、标签、AI/移动配置相关控件和逻辑
    /// 继承 EntityStyleTabBase，复用公共样式逻辑，保留血条/MP条/AI等子类特有内容
    /// </summary>
    public class DebugPanelMonsterTab : DebugPanelEntityStyleTabBase
    {
        #region Fields - AI / Movement Config
        private HSlider _monsterMoveSpeedSlider;
        private Label _monsterMoveSpeedValue;
        private HSlider _monsterPatrolRangeSlider;
        private Label _monsterPatrolRangeValue;
        private HSlider _monsterAggroRangeSlider;
        private Label _monsterAggroRangeValue;
        private HSlider _monsterMoveIntervalSlider;
        private Label _monsterMoveIntervalValue;
        private Button _saveMonsterConfigBtn;
        #endregion

        #region Fields - HP Bar
        private CheckButton _monsterHpBarVisibleCheck;
        private Button _monsterHpBarColorBtn;
        private Label _monsterHpBarLengthValue;
        private HSlider _monsterHpBarLengthScaleSlider;
        private Label _monsterHpBarLengthScaleValue;
        private Label _monsterHpBarHeightValue;
        private HSlider _monsterHpBarHeightScaleSlider;
        private Label _monsterHpBarHeightScaleValue;
        private HSlider _monsterHpBarFillSlider;
        private Label _monsterHpBarFillValue;
        private HSlider _monsterHpBarOffsetXSlider;
        private Label _monsterHpBarOffsetXValue;
        private CheckButton _monsterHpBarOffsetXCenterCheck;
        private HSlider _monsterHpBarOffsetYSlider;
        private Label _monsterHpBarOffsetYValue;
        #endregion

        #region Fields - MP Bar
        private CheckButton _monsterMpBarVisibleCheck;
        private Button _monsterMpBarColorBtn;
        private Label _monsterMpBarLengthValue;
        private HSlider _monsterMpBarLengthScaleSlider;
        private Label _monsterMpBarLengthScaleValue;
        private Label _monsterMpBarHeightValue;
        private HSlider _monsterMpBarHeightScaleSlider;
        private Label _monsterMpBarHeightScaleValue;
        private HSlider _monsterMpBarFillSlider;
        private Label _monsterMpBarFillValue;
        private HSlider _monsterMpBarOffsetXSlider;
        private Label _monsterMpBarOffsetXValue;
        private CheckButton _monsterMpBarOffsetXCenterCheck;
        private HSlider _monsterMpBarOffsetYSlider;
        private Label _monsterMpBarOffsetYValue;
        #endregion

        public DebugPanelMonsterTab(DebugPanel owner) : base(owner) { }

        public override string TabKey => "monster";

        #region Abstract implementations
        protected override string ConfigSectionPrefix => "monster";
        protected override EntityStyleConfig GetStyleConfig(int id) => MonsterManager.GetStyleConfig(id);
        protected override EntityStyleConfig GetOrCreateStyleConfig(int id) => MonsterManager.GetOrCreateStyleConfig(id);
        protected override Dictionary<int, EntityStyleConfig> GetAllStyleConfigs() => MonsterManager.StyleConfigs;
        protected override void ApplyStyleToAll() => MonsterManager?.ApplyStyleToAll();
        #endregion

        #region BuildUI
        public override void BuildUI(VBoxContainer tabContainer)
        {
            BuildEntityStyleUI(tabContainer, "怪物全局样式");
            _borderColorPicker.Color = new Color(0.9f, 0.3f, 0.3f);
            _bgColorPicker.Color = new Color(0.8f, 0.2f, 0.2f);
            _textColorPicker.Color = new Color(1, 0.95f, 0.95f);
            BuildSubclassUI(tabContainer);
            ConnectStyleSignals();
            ConnectBorderWidthLinkageSignals();
            ConnectSubclassSignals();
        }
        #endregion

        #region BorderWidth Linkage
        private void ConnectBorderWidthLinkageSignals()
        {
            _borderWidthSlider.ValueChanged += OnBorderWidthChanged;
            _borderWidthSlider.DragEnded += OnBorderWidthDragEnded;
            _borderWidthScaleSlider.ValueChanged += OnBorderWidthScaleChanged;
            _borderWidthScaleSlider.DragEnded += OnBorderWidthScaleDragEnded;
        }

        private void OnBorderWidthChanged(double value)
        {
            if (_borderWidthValue != null)
                _borderWidthValue.Text = ((int)value).ToString();
        }

        private void OnBorderWidthDragEnded(bool valueChanged)
        {
            if (!valueChanged) return;
            int gridSize = (int)Owner._gridSizeSlider.Value;
            float scale = gridSize > 0 ? (float)(_borderWidthSlider.Value / gridSize) : 0.0f;
            if (_borderWidthScaleSlider != null)
            {
                _borderWidthScaleSlider.SetBlockSignals(true);
                _borderWidthScaleSlider.Value = scale;
                _borderWidthScaleSlider.SetBlockSignals(false);
                _borderWidthScaleValue.Text = scale.ToString(DebugPanelLengthScalePolicy.FormatStr);
            }
            ApplyStyleChanges();
        }

        private void OnBorderWidthScaleChanged(double value)
        {
            if (_borderWidthScaleValue != null)
                _borderWidthScaleValue.Text = value.ToString(DebugPanelLengthScalePolicy.FormatStr);
        }

        private void OnBorderWidthScaleDragEnded(bool valueChanged)
        {
            if (!valueChanged) return;
            int gridSize = (int)Owner._gridSizeSlider.Value;
            float newWidth = Mathf.Clamp((float)_borderWidthScaleSlider.Value * gridSize, 1.0f, 20.0f);
            if (_borderWidthSlider != null)
            {
                _borderWidthSlider.SetBlockSignals(true);
                _borderWidthSlider.Value = newWidth;
                _borderWidthSlider.SetBlockSignals(false);
                _borderWidthValue.Text = ((int)newWidth).ToString();
            }
            ApplyStyleChanges();
        }
        #endregion

        #region BuildSubclassUI
        protected override void BuildSubclassUI(VBoxContainer tabContainer)
        {
            // ---- 血条 ----
            tabContainer.AddChild(new HSeparator());
            var hpTitleRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            hpTitleRow.AddChild(new Label { Text = "血条", CustomMinimumSize = new Vector2(45, 0) });
            _monsterHpBarVisibleCheck = new CheckButton { ButtonPressed = true };
            hpTitleRow.AddChild(_monsterHpBarVisibleCheck);
            hpTitleRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
            _monsterHpBarColorBtn = new Button { Text = "色", CustomMinimumSize = new Vector2(30, 24) };
            _monsterHpBarColorBtn.Modulate = new Color(0, 0.8f, 0, 1);
            hpTitleRow.AddChild(_monsterHpBarColorBtn);
            tabContainer.AddChild(hpTitleRow);

            _monsterHpBarLengthValue = CreateBarReadOnlyRow(tabContainer, "长度", "102");
            (_monsterHpBarLengthScaleSlider, _monsterHpBarLengthScaleValue) = CreateBarSliderRow(
                tabContainer, "长度比例", 0.1, 2.0, 102.0 / 111.0,
                DebugPanelLengthScalePolicy.Step, v => v.ToString(DebugPanelLengthScalePolicy.FormatStr));
            _monsterHpBarHeightValue = CreateBarReadOnlyRow(tabContainer, "高度", "6");
            (_monsterHpBarHeightScaleSlider, _monsterHpBarHeightScaleValue) = CreateBarSliderRow(
                tabContainer, "高度比例", 0.01, 0.3, 6.0 / 111.0,
                DebugPanelLengthScalePolicy.Step, v => v.ToString(DebugPanelLengthScalePolicy.FormatStr));
            (_monsterHpBarFillSlider, _monsterHpBarFillValue) = CreateBarSliderRow(
                tabContainer, "填充", 0, 100, 100, 1, v => $"{(int)v}%", labelMinWidth: 35);
            _monsterHpBarOffsetXCenterCheck = new CheckButton { Text = "居中", ButtonPressed = true };
            (_monsterHpBarOffsetXSlider, _monsterHpBarOffsetXValue) = CreateBarSliderRow(
                tabContainer, "X偏移", -150, 150, 0, 1, v => ((int)v).ToString(), labelMinWidth: 45, centerCheck: _monsterHpBarOffsetXCenterCheck);
            (_monsterHpBarOffsetYSlider, _monsterHpBarOffsetYValue) = CreateBarSliderRow(
                tabContainer, "Y偏移", -150, 150, -70, 1, v => ((int)v).ToString(), labelMinWidth: 45);

            // ---- MP 条 ----
            tabContainer.AddChild(new HSeparator());
            var mpTitleRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            mpTitleRow.AddChild(new Label { Text = "MP条", CustomMinimumSize = new Vector2(45, 0) });
            _monsterMpBarVisibleCheck = new CheckButton { ButtonPressed = true };
            mpTitleRow.AddChild(_monsterMpBarVisibleCheck);
            mpTitleRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
            _monsterMpBarColorBtn = new Button { Text = "色", CustomMinimumSize = new Vector2(30, 24) };
            _monsterMpBarColorBtn.Modulate = new Color(0.2f, 0.4f, 1.0f, 1);
            mpTitleRow.AddChild(_monsterMpBarColorBtn);
            tabContainer.AddChild(mpTitleRow);

            _monsterMpBarLengthValue = CreateBarReadOnlyRow(tabContainer, "长度", "80");
            (_monsterMpBarLengthScaleSlider, _monsterMpBarLengthScaleValue) = CreateBarSliderRow(
                tabContainer, "长度比例", 0.1, 2.0, 80.0 / 111.0,
                DebugPanelLengthScalePolicy.Step, v => v.ToString(DebugPanelLengthScalePolicy.FormatStr));
            _monsterMpBarHeightValue = CreateBarReadOnlyRow(tabContainer, "高度", "4");
            (_monsterMpBarHeightScaleSlider, _monsterMpBarHeightScaleValue) = CreateBarSliderRow(
                tabContainer, "高度比例", 0.01, 0.3, 4.0 / 111.0,
                DebugPanelLengthScalePolicy.Step, v => v.ToString(DebugPanelLengthScalePolicy.FormatStr));
            (_monsterMpBarFillSlider, _monsterMpBarFillValue) = CreateBarSliderRow(
                tabContainer, "填充", 0, 100, 100, 1, v => $"{(int)v}%", labelMinWidth: 35);
            _monsterMpBarOffsetXCenterCheck = new CheckButton { Text = "居中", ButtonPressed = true };
            (_monsterMpBarOffsetXSlider, _monsterMpBarOffsetXValue) = CreateBarSliderRow(
                tabContainer, "X偏移", -150, 150, 0, 1, v => ((int)v).ToString(), labelMinWidth: 45, centerCheck: _monsterMpBarOffsetXCenterCheck);
            (_monsterMpBarOffsetYSlider, _monsterMpBarOffsetYValue) = CreateBarSliderRow(
                tabContainer, "Y偏移", -150, 150, -62, 1, v => ((int)v).ToString(), labelMinWidth: 45);

            // ---- 怪物配置（AI / 移动） ----
            tabContainer.AddChild(new HSeparator());
            var configTitle = new Label { Text = "怪物配置 (JSON)", HorizontalAlignment = HorizontalAlignment.Center };
            configTitle.AddThemeFontSizeOverride("font_size", 13);
            tabContainer.AddChild(configTitle);

            var cm = Owner.GetTree()?.GetFirstNodeInGroup("monster_config_manager") as MonsterConfigManager;
            int moveSpeed = cm?.GetMoveSpeedMs() ?? 800;
            int patrolRange = 3;
            int aggroRange = 5;
            int moveInterval = 2000;
            if (cm != null)
            {
                var ai = cm.GetAiDefaults("patrol_chase");
                patrolRange = ai.PatrolRange ?? 3;
                aggroRange = ai.AggroRange ?? 5;
                moveInterval = (int)(ai.MoveIntervalMs ?? 2000);
            }

            (_monsterMoveSpeedSlider, _monsterMoveSpeedValue) = CreateMonsterSliderRow(tabContainer, "怪物移速(ms)", 100, 3000, moveSpeed, 50f);
            (_monsterPatrolRangeSlider, _monsterPatrolRangeValue) = CreateMonsterSliderRow(tabContainer, "巡逻范围", 0, 10, patrolRange, 1f);
            (_monsterAggroRangeSlider, _monsterAggroRangeValue) = CreateMonsterSliderRow(tabContainer, "仇恨范围", 0, 20, aggroRange, 1f);
            (_monsterMoveIntervalSlider, _monsterMoveIntervalValue) = CreateMonsterSliderRow(tabContainer, "移动间隔(ms)", 100, 10000, moveInterval, 100f);

            _saveMonsterConfigBtn = new Button { Text = "保存配置到 JSON", CustomMinimumSize = new Vector2(0, 32) };
            _saveMonsterConfigBtn.Pressed += OnSaveMonsterConfigPressed;
            tabContainer.AddChild(_saveMonsterConfigBtn);
        }
        #endregion

        #region ConnectSubclassSignals
        private void ConnectSubclassSignals()
        {
            _monsterHpBarVisibleCheck.Toggled += _ => ApplyStyleChanges();
            _monsterHpBarColorBtn.Pressed += OnMonsterHpBarColorPressed;
            _monsterHpBarLengthScaleSlider.ValueChanged += _ => ApplyStyleChanges();
            AttachValueLineEdit(_monsterHpBarLengthScaleSlider, _monsterHpBarLengthScaleValue);
            _monsterHpBarHeightScaleSlider.ValueChanged += _ => ApplyStyleChanges();
            AttachValueLineEdit(_monsterHpBarHeightScaleSlider, _monsterHpBarHeightScaleValue);
            _monsterHpBarFillSlider.ValueChanged += _ => ApplyStyleChanges();
            AttachValueLineEdit(_monsterHpBarFillSlider, _monsterHpBarFillValue);
            _monsterHpBarOffsetXSlider.ValueChanged += _ => ApplyStyleChanges();
            AttachValueLineEdit(_monsterHpBarOffsetXSlider, _monsterHpBarOffsetXValue);
            _monsterHpBarOffsetXCenterCheck.Toggled += _ => ApplyStyleChanges();
            _monsterHpBarOffsetYSlider.ValueChanged += _ => ApplyStyleChanges();
            AttachValueLineEdit(_monsterHpBarOffsetYSlider, _monsterHpBarOffsetYValue);

            _monsterMpBarVisibleCheck.Toggled += _ => ApplyStyleChanges();
            _monsterMpBarColorBtn.Pressed += OnMonsterMpBarColorPressed;
            _monsterMpBarLengthScaleSlider.ValueChanged += _ => ApplyStyleChanges();
            AttachValueLineEdit(_monsterMpBarLengthScaleSlider, _monsterMpBarLengthScaleValue);
            _monsterMpBarHeightScaleSlider.ValueChanged += _ => ApplyStyleChanges();
            AttachValueLineEdit(_monsterMpBarHeightScaleSlider, _monsterMpBarHeightScaleValue);
            _monsterMpBarFillSlider.ValueChanged += _ => ApplyStyleChanges();
            AttachValueLineEdit(_monsterMpBarFillSlider, _monsterMpBarFillValue);
            _monsterMpBarOffsetXSlider.ValueChanged += _ => ApplyStyleChanges();
            AttachValueLineEdit(_monsterMpBarOffsetXSlider, _monsterMpBarOffsetXValue);
            _monsterMpBarOffsetXCenterCheck.Toggled += _ => ApplyStyleChanges();
            _monsterMpBarOffsetYSlider.ValueChanged += _ => ApplyStyleChanges();
            AttachValueLineEdit(_monsterMpBarOffsetYSlider, _monsterMpBarOffsetYValue);
        }
        #endregion

        #region ApplySubclassChanges
        protected override void ApplySubclassChanges(EntityStyleConfig cfg)
        {
            cfg.HpBarVisible = _monsterHpBarVisibleCheck.ButtonPressed;
            cfg.HpBarLengthScale = (float)_monsterHpBarLengthScaleSlider.Value;
            cfg.HpBarHeightScale = (float)_monsterHpBarHeightScaleSlider.Value;
            cfg.HpBarFillPercent = (float)(_monsterHpBarFillSlider.Value / 100.0);
            cfg.HpBarOffsetX = _monsterHpBarOffsetXCenterCheck?.ButtonPressed == true ? 0 : (float)_monsterHpBarOffsetXSlider.Value;
            cfg.HpBarCenterX = _monsterHpBarOffsetXCenterCheck?.ButtonPressed ?? true;
            cfg.HpBarOffsetY = (float)_monsterHpBarOffsetYSlider.Value;
            cfg.MpBarVisible = _monsterMpBarVisibleCheck.ButtonPressed;
            cfg.MpBarLengthScale = (float)_monsterMpBarLengthScaleSlider.Value;
            cfg.MpBarHeightScale = (float)_monsterMpBarHeightScaleSlider.Value;
            cfg.MpBarFillPercent = (float)(_monsterMpBarFillSlider.Value / 100.0);
            cfg.MpBarOffsetX = _monsterMpBarOffsetXCenterCheck?.ButtonPressed == true ? 0 : (float)_monsterMpBarOffsetXSlider.Value;
            cfg.MpBarCenterX = _monsterMpBarOffsetXCenterCheck?.ButtonPressed ?? true;
            cfg.MpBarOffsetY = (float)_monsterMpBarOffsetYSlider.Value;

            int gridSize = (int)Owner._gridSizeSlider.Value;
            if (_monsterHpBarLengthValue != null)
                _monsterHpBarLengthValue.Text = ((int)(gridSize * cfg.HpBarLengthScale)).ToString();
            if (_monsterHpBarHeightValue != null)
                _monsterHpBarHeightValue.Text = ((int)(gridSize * cfg.HpBarHeightScale)).ToString();
            if (_monsterMpBarLengthValue != null)
                _monsterMpBarLengthValue.Text = ((int)(gridSize * cfg.MpBarLengthScale)).ToString();
            if (_monsterMpBarHeightValue != null)
                _monsterMpBarHeightValue.Text = ((int)(gridSize * cfg.MpBarHeightScale)).ToString();
        }
        #endregion

        #region SyncAfterStyleUI
        protected override void SyncAfterStyleUI()
        {
            var cfg = MonsterManager.GetStyleConfig(_selectedConfigId);

            // 血条
            _monsterHpBarVisibleCheck.SetBlockSignals(true);
            _monsterHpBarVisibleCheck.ButtonPressed = cfg.HpBarVisible;
            _monsterHpBarVisibleCheck.SetBlockSignals(false);
            _monsterHpBarColorBtn.Modulate = cfg.HpBarColor;
            _monsterHpBarLengthScaleSlider.SetBlockSignals(true);
            _monsterHpBarLengthScaleSlider.Value = cfg.HpBarLengthScale;
            _monsterHpBarLengthScaleSlider.SetBlockSignals(false);
            UpdateAttachedValue(_monsterHpBarLengthScaleSlider, cfg.HpBarLengthScale.ToString(DebugPanelLengthScalePolicy.FormatStr));
            _monsterHpBarHeightScaleSlider.SetBlockSignals(true);
            _monsterHpBarHeightScaleSlider.Value = cfg.HpBarHeightScale;
            _monsterHpBarHeightScaleSlider.SetBlockSignals(false);
            UpdateAttachedValue(_monsterHpBarHeightScaleSlider, cfg.HpBarHeightScale.ToString(DebugPanelLengthScalePolicy.FormatStr));
            int gs = (int)Owner._gridSizeSlider.Value;
            if (_monsterHpBarLengthValue != null)
                _monsterHpBarLengthValue.Text = ((int)(gs * cfg.HpBarLengthScale)).ToString();
            if (_monsterHpBarHeightValue != null)
                _monsterHpBarHeightValue.Text = ((int)(gs * cfg.HpBarHeightScale)).ToString();
            _monsterHpBarFillSlider.SetBlockSignals(true);
            _monsterHpBarFillSlider.Value = cfg.HpBarFillPercent * 100;
            _monsterHpBarFillSlider.SetBlockSignals(false);
            UpdateAttachedValue(_monsterHpBarFillSlider, $"{(int)(cfg.HpBarFillPercent * 100)}%");
            _monsterHpBarOffsetXSlider.SetBlockSignals(true);
            _monsterHpBarOffsetXSlider.Value = cfg.HpBarOffsetX;
            _monsterHpBarOffsetXSlider.SetBlockSignals(false);
            UpdateAttachedValue(_monsterHpBarOffsetXSlider, ((int)cfg.HpBarOffsetX).ToString());
            _monsterHpBarOffsetYSlider.SetBlockSignals(true);
            _monsterHpBarOffsetYSlider.Value = cfg.HpBarOffsetY;
            _monsterHpBarOffsetYSlider.SetBlockSignals(false);
            UpdateAttachedValue(_monsterHpBarOffsetYSlider, ((int)cfg.HpBarOffsetY).ToString());

            if (_monsterHpBarOffsetXCenterCheck != null)
            {
                _monsterHpBarOffsetXCenterCheck.SetBlockSignals(true);
                _monsterHpBarOffsetXCenterCheck.ButtonPressed = cfg.HpBarCenterX;
                _monsterHpBarOffsetXCenterCheck.SetBlockSignals(false);
            }
            _monsterHpBarOffsetXSlider.Editable = !cfg.HpBarCenterX;
            _monsterHpBarOffsetXSlider.Modulate = cfg.HpBarCenterX ? new Color(0.5f, 0.5f, 0.5f, 1) : new Color(1, 1, 1, 1);

            // MP 条
            _monsterMpBarVisibleCheck.SetBlockSignals(true);
            _monsterMpBarVisibleCheck.ButtonPressed = cfg.MpBarVisible;
            _monsterMpBarVisibleCheck.SetBlockSignals(false);
            _monsterMpBarColorBtn.Modulate = cfg.MpBarColor;
            _monsterMpBarLengthScaleSlider.SetBlockSignals(true);
            _monsterMpBarLengthScaleSlider.Value = cfg.MpBarLengthScale;
            _monsterMpBarLengthScaleSlider.SetBlockSignals(false);
            UpdateAttachedValue(_monsterMpBarLengthScaleSlider, cfg.MpBarLengthScale.ToString(DebugPanelLengthScalePolicy.FormatStr));
            _monsterMpBarHeightScaleSlider.SetBlockSignals(true);
            _monsterMpBarHeightScaleSlider.Value = cfg.MpBarHeightScale;
            _monsterMpBarHeightScaleSlider.SetBlockSignals(false);
            UpdateAttachedValue(_monsterMpBarHeightScaleSlider, cfg.MpBarHeightScale.ToString(DebugPanelLengthScalePolicy.FormatStr));
            if (_monsterMpBarLengthValue != null)
                _monsterMpBarLengthValue.Text = ((int)(gs * cfg.MpBarLengthScale)).ToString();
            if (_monsterMpBarHeightValue != null)
                _monsterMpBarHeightValue.Text = ((int)(gs * cfg.MpBarHeightScale)).ToString();
            _monsterMpBarFillSlider.SetBlockSignals(true);
            _monsterMpBarFillSlider.Value = cfg.MpBarFillPercent * 100;
            _monsterMpBarFillSlider.SetBlockSignals(false);
            UpdateAttachedValue(_monsterMpBarFillSlider, $"{(int)(cfg.MpBarFillPercent * 100)}%");
            _monsterMpBarOffsetXSlider.SetBlockSignals(true);
            _monsterMpBarOffsetXSlider.Value = cfg.MpBarOffsetX;
            _monsterMpBarOffsetXSlider.SetBlockSignals(false);
            UpdateAttachedValue(_monsterMpBarOffsetXSlider, ((int)cfg.MpBarOffsetX).ToString());
            _monsterMpBarOffsetYSlider.SetBlockSignals(true);
            _monsterMpBarOffsetYSlider.Value = cfg.MpBarOffsetY;
            _monsterMpBarOffsetYSlider.SetBlockSignals(false);
            UpdateAttachedValue(_monsterMpBarOffsetYSlider, ((int)cfg.MpBarOffsetY).ToString());

            if (_monsterMpBarOffsetXCenterCheck != null)
            {
                _monsterMpBarOffsetXCenterCheck.SetBlockSignals(true);
                _monsterMpBarOffsetXCenterCheck.ButtonPressed = cfg.MpBarCenterX;
                _monsterMpBarOffsetXCenterCheck.SetBlockSignals(false);
            }
            _monsterMpBarOffsetXSlider.Editable = !cfg.MpBarCenterX;
            _monsterMpBarOffsetXSlider.Modulate = cfg.MpBarCenterX ? new Color(0.5f, 0.5f, 0.5f, 1) : new Color(1, 1, 1, 1);
        }
        #endregion

        #region SyncToCurrentValues
        public override void SyncToCurrentValues()
        {
            RefreshConfigIdList();
            SyncStyleUI();
            SyncAfterStyleUI();
        }
        #endregion

        #region Monster Config Handlers
        private void OnSaveMonsterConfigPressed()
        {
            var cm = Owner.GetTree()?.GetFirstNodeInGroup("monster_config_manager") as MonsterConfigManager;
            if (cm == null)
            {
                GD.PushError("[DebugPanel] MonsterConfigManager not found");
                return;
            }

            cm.SetMoveSpeedMs((int)_monsterMoveSpeedSlider.Value);
            var ai = new AiDefaults
            {
                PatrolRange = (int)_monsterPatrolRangeSlider.Value,
                AggroRange = (int)_monsterAggroRangeSlider.Value,
                MoveIntervalMs = (int)_monsterMoveIntervalSlider.Value,
                ChaseIntervalMs = (int)_monsterMoveIntervalSlider.Value / 4,
            };
            cm.SetAiDefaults("patrol_chase", ai);
            cm.SetAiDefaults("patrol", new AiDefaults
            {
                PatrolRange = (int)_monsterPatrolRangeSlider.Value,
                MoveIntervalMs = (int)_monsterMoveIntervalSlider.Value,
            });
            cm.SetAiDefaults("guard", new AiDefaults
            {
                PatrolRange = 0,
                AggroRange = (int)_monsterAggroRangeSlider.Value,
                MoveIntervalMs = (int)_monsterMoveIntervalSlider.Value,
            });
            var moveVals = Owner._systemTab?.GetMoveSystemValues() ?? (0, 0, 0);
            cm.SetMoveSystem(new MoveSystem
            {
                CheckRatio = moveVals.checkRatio,
                DualGridStartRatio = moveVals.dualStart,
                DualGridEndRatio = moveVals.dualEnd,
            });
            cm.SaveConfig();
            GD.Print("[DebugPanel] Monster + Move config saved to JSON");
        }
        #endregion

        #region SaveConfig
        public override void SaveConfig(ConfigFile cfg)
        {
            var mm = MonsterManager;
            if (mm == null) return;

            foreach (var kv in mm.StyleConfigs)
            {
                string sec = $"monster_{kv.Key}";
                var c = kv.Value;
                SaveCommonStyleConfig(cfg, sec, c);
                SaveBarConfig(cfg, sec, "hp", c);
                SaveBarConfig(cfg, sec, "mp", c);
                SaveSubclassConfig(cfg, sec, c);
            }
        }
        #endregion

        #region LoadConfig
        public override void LoadConfig(ConfigFile cfg, bool configLoaded)
        {
            var mm = MonsterManager;
            if (mm == null) return;

            RefreshConfigIdList();
            mm.ApplyStyleToAll();
            SyncStyleUI();
            SyncAfterStyleUI();
        }
        #endregion

        #region SaveSubclassConfig / ExportSubclassConfigData
        protected override void SaveSubclassConfig(ConfigFile cfg, string section, EntityStyleConfig c) { }
        protected override void ExportSubclassConfigData(Godot.Collections.Dictionary dict, EntityStyleConfig c) { }
        #endregion

        #region ExportConfigData
        public override Godot.Collections.Dictionary ExportConfigData()
        {
            var mm = MonsterManager;
            if (mm == null) return null;

            var data = new Godot.Collections.Dictionary();
            foreach (var kv in mm.StyleConfigs)
            {
                var c = kv.Value;
                var dict = new Godot.Collections.Dictionary();
                ExportCommonConfigData(dict, c);
                ExportSubclassConfigData(dict, c);
                data[$"config_{kv.Key}"] = dict;
            }
            return data;
        }
        #endregion

        #region HP/MP Bar Color Handlers
        private static readonly Color[] HpColors = {
            new Color(0, 0.8f, 0, 1),
            new Color(1, 0.2f, 0.2f, 1),
            new Color(1, 0.8f, 0, 1),
            new Color(0.5f, 0.5f, 0.5f, 1),
        };
        private static readonly Color[] MpColors = {
            new Color(0.2f, 0.4f, 1.0f, 1),
            new Color(0.6f, 0.2f, 0.8f, 1),
            new Color(0.2f, 0.8f, 0.8f, 1),
            new Color(0.8f, 0.4f, 0.2f, 1),
        };

        private void OnMonsterHpBarColorPressed()
        {
            var mm = MonsterManager;
            if (mm == null) return;
            var cfg = mm.GetOrCreateStyleConfig(_selectedConfigId);
            int nextIdx = 0;
            for (int i = 0; i < HpColors.Length; i++)
                if (HpColors[i].IsEqualApprox(cfg.HpBarColor)) { nextIdx = (i + 1) % HpColors.Length; break; }
            cfg.HpBarColor = HpColors[nextIdx];
            _monsterHpBarColorBtn.Modulate = HpColors[nextIdx];
            mm.ApplyStyleToAll();
        }

        private void OnMonsterMpBarColorPressed()
        {
            var mm = MonsterManager;
            if (mm == null) return;
            var cfg = mm.GetOrCreateStyleConfig(_selectedConfigId);
            int nextIdx = 0;
            for (int i = 0; i < MpColors.Length; i++)
                if (MpColors[i].IsEqualApprox(cfg.MpBarColor)) { nextIdx = (i + 1) % MpColors.Length; break; }
            cfg.MpBarColor = MpColors[nextIdx];
            _monsterMpBarColorBtn.Modulate = MpColors[nextIdx];
            mm.ApplyStyleToAll();
        }
        #endregion
    }
}
