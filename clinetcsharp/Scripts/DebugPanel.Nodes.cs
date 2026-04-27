using Godot;

namespace ClinetCSharp
{
    /// <summary>
    /// DebugPanel partial — node initialization.
    /// Signal wiring is handled by each tab's ConnectSignals/DisconnectSignals.
    /// Slider/toggle setup is handled by each tab's BuildUI.
    /// </summary>
    public partial class DebugPanel
    {
        #region Initialize Node References
        internal void InitializeNodeReferences()
        {
            _panel = GetNodeOrNull<Panel>("Control/Panel");
            _outerControl = GetNodeOrNull<Control>("Control");
            _scrollContainer = GetNodeOrNull<ScrollContainer>("Control/Panel/ScrollContainer");
            _tabContainer = GetNodeOrNull<TabContainer>("Control/Panel/ScrollContainer/TabContainer");

            // 预设下拉框等
            _presetOption = GetNodeOrNull<OptionButton>("Control/Panel/ScrollContainer/TabContainer/地图/PresetOption");
            _savePresetBtn = GetNodeOrNull<Button>("Control/Panel/ScrollContainer/TabContainer/地图/SavePresetBtn");
            _deletePresetBtn = GetNodeOrNull<Button>("Control/Panel/ScrollContainer/TabContainer/地图/DeletePresetBtn");
            _presetNameEdit = GetNodeOrNull<LineEdit>("Control/Panel/ScrollContainer/TabContainer/地图/PresetNameEdit");
        }
        #endregion

        #region Setup Panel
        internal void SetupPanel()
        {
            if (_panel == null) return;

            // 注册面板
            var pm = GetNodeOrNull<PanelManager>("/root/UICanvas/PanelManager");
            pm?.RegisterPanel(this);
        }
        #endregion
    }
}