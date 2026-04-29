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
            _panel = this;
            _content = GetNodeOrNull<VBoxContainer>("VBoxContainer/Content");
            _scrollContainer = GetNodeOrNull<ScrollContainer>("VBoxContainer/Content/ScrollContainer");
            _tabContainer = GetNodeOrNull<TabContainer>("VBoxContainer/Content/ScrollContainer/TabContainer");

            // 动态创建的预设 UI 在 CreatePresetUI() 中赋值
            _presetOption = null;
            _savePresetBtn = null;
            _deletePresetBtn = null;
            _presetNameEdit = null;
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