using Godot;

namespace ClinetCSharp
{
    /// <summary>
    /// Debug panel UI tab. The tab shell keeps only fields and identity;
    /// build, signal wiring, and config synchronization live in partial files.
    /// </summary>
    public partial class DebugPanelUITab : DebugPanelTab
    {
        private HSlider _skillBarIconSizeSlider;
        private Label _skillBarIconSizeValue;
        private HSlider _skillBarSpacingSlider;
        private Label _skillBarSpacingValue;
        private HSlider _skillBarMarginRightSlider;
        private Label _skillBarMarginRightValue;
        private HSlider _skillBarMarginBottomSlider;
        private Label _skillBarMarginBottomValue;
        private HSlider _skillBarNameFontSizeSlider;
        private Label _skillBarNameFontSizeValue;

        private HSlider _fnBarOffsetXSlider;
        private Label _fnBarOffsetXValue;
        private HSlider _fnBarOffsetYSlider;
        private Label _fnBarOffsetYValue;
        private HSlider _fnBarSpacingSlider;
        private Label _fnBarSpacingValue;

        private HSlider _buffBarIconSizeSlider;
        private Label _buffBarIconSizeValue;
        private HSlider _buffBarSpacingSlider;
        private Label _buffBarSpacingValue;
        private HSlider _buffBarOffsetXSlider;
        private Label _buffBarOffsetXValue;
        private HSlider _buffBarOffsetYSlider;
        private Label _buffBarOffsetYValue;
        private Button _buffBarForceShowBtn;
        private CheckBox _buffBarRightAlignCheck;

        public DebugPanelUITab(DebugPanel owner) : base(owner) { }

        public override string TabKey => "ui";
    }
}
