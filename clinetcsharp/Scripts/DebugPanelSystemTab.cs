using Godot;

namespace ClinetCSharp
{
    /// <summary>
    /// DebugPanel System tab — move system configuration sliders
    /// (CheckRatio, DualGridStartRatio, DualGridEndRatio).
    /// Owned by the "系统" tab in the TabContainer.
    /// </summary>
    public class DebugPanelSystemTab : DebugPanelTab
    {
        #region Fields — Move System Sliders
        private HSlider _moveCheckRatioSlider;
        private Label   _moveCheckRatioValue;
        private HSlider _moveDualStartSlider;
        private Label   _moveDualStartValue;
        private HSlider _moveDualEndSlider;
        private Label   _moveDualEndValue;
        #endregion

        #region Constructor
        public DebugPanelSystemTab(DebugPanel owner) : base(owner) { }

        public override string TabKey => "system";
        #endregion

        #region BuildUI
        public override void BuildUI(VBoxContainer tabContainer)
        {
            var title = new Label { Text = "全局系统配置", HorizontalAlignment = HorizontalAlignment.Center };
            title.AddThemeFontSizeOverride("font_size", 13);
            tabContainer.AddChild(title);
            tabContainer.AddChild(new HSeparator());

            var cm = Owner.GetTree()?.GetFirstNodeInGroup("monster_config_manager") as MonsterConfigManager;

            // 移动系统配置
            var moveTitle = new Label { Text = "移动系统配置 (JSON)", HorizontalAlignment = HorizontalAlignment.Left };
            moveTitle.AddThemeFontSizeOverride("font_size", 12);
            tabContainer.AddChild(moveTitle);

            var ms = cm?.GetMoveSystem() ?? new MoveSystem();
            (_moveCheckRatioSlider, _moveCheckRatioValue) = CreateMonsterSliderRow(tabContainer, "检查点比例(%)", 0, 100, ms.CheckRatio, 5f);
            (_moveDualStartSlider,  _moveDualStartValue)  = CreateMonsterSliderRow(tabContainer, "双格开始(%)",   0, 100, ms.DualGridStartRatio, 5f);
            (_moveDualEndSlider,    _moveDualEndValue)    = CreateMonsterSliderRow(tabContainer, "双格结束(%)",   0, 100, ms.DualGridEndRatio, 5f);
        }
        #endregion

        #region ConnectSignals / DisconnectSignals
        public override void ConnectSignals()
        {
            // No additional signals beyond the auto-wired slider labels
            // (CreateMonsterSliderRow already wires ValueChanged -> label text)
        }

        public override void DisconnectSignals()
        {
            // No additional signals to disconnect
        }
        #endregion

        #region Public API
        /// <summary>
        /// Returns the current move-system slider values so that the MonsterTab
        /// save handler can include them when persisting config via MonsterConfigManager.
        /// </summary>
        public (int checkRatio, int dualStart, int dualEnd) GetMoveSystemValues()
        {
            return (
                (int)_moveCheckRatioSlider.Value,
                (int)_moveDualStartSlider.Value,
                (int)_moveDualEndSlider.Value
            );
        }
        #endregion

        #region SaveConfig / LoadConfig
        public override void SaveConfig(ConfigFile cfg)
        {
            // Move system config is saved through MonsterConfigManager JSON,
            // not through the ConfigFile .cfg file. Nothing to write here.
        }

        public override void LoadConfig(ConfigFile cfg, bool configLoaded)
        {
            // Move system config is loaded from MonsterConfigManager JSON
            // during BuildUI when we read cm.GetMoveSystem(). Nothing to do here.
        }
        #endregion

        #region SyncToCurrentValues
        public override void SyncToCurrentValues()
        {
            // Re-read from MonsterConfigManager and update slider positions
            var cm = Owner.GetTree()?.GetFirstNodeInGroup("monster_config_manager") as MonsterConfigManager;
            if (cm == null) return;

            var ms = cm.GetMoveSystem();
            if (ms != null)
            {
                _moveCheckRatioSlider.SetBlockSignals(true);
                _moveCheckRatioSlider.Value = ms.CheckRatio;
                _moveCheckRatioSlider.SetBlockSignals(false);
                _moveCheckRatioValue.Text = ms.CheckRatio.ToString();

                _moveDualStartSlider.SetBlockSignals(true);
                _moveDualStartSlider.Value = ms.DualGridStartRatio;
                _moveDualStartSlider.SetBlockSignals(false);
                _moveDualStartValue.Text = ms.DualGridStartRatio.ToString();

                _moveDualEndSlider.SetBlockSignals(true);
                _moveDualEndSlider.Value = ms.DualGridEndRatio;
                _moveDualEndSlider.SetBlockSignals(false);
                _moveDualEndValue.Text = ms.DualGridEndRatio.ToString();
            }
        }
        #endregion

    }
}
