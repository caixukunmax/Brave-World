using Godot;
using System;

namespace ClinetCSharp
{
    /// <summary>
    /// Debug panel shell. Constants, shared state, and cross-partial fields live here;
    /// lifecycle, panel state, config, and tab-specific behavior are split into partial files.
    /// </summary>
    public partial class DebugPanel : DraggablePanel
    {
        #region Constants
        private const int CONFIG_VERSION = 4;
        internal const string CONFIG_PATH = "res://debug_panel_config.cfg";
        private const string PRESET_PATH = "res://debug_panel_presets.cfg";

        public static readonly Color[] COLOR_PRESETS =
        {
            Colors.Black,
            Colors.White,
            new Color(1, 0, 0, 1),
            new Color(0, 1, 0, 1),
            new Color(0, 0, 1, 1),
            new Color(1, 1, 0, 1),
        };

        internal static readonly string[] EASE_TYPE_NAMES =
        {
            "线性", "平滑", "缓出", "缓入", "缓入缓出"
        };

        public static readonly (string name, string path)[] FONTS =
        {
            ("思源黑体", "res://assets/fonts/SourceHanSansCN-Bold.otf"),
            ("阿里巴巴普惠体", "res://assets/fonts/Alibaba-PuHuiTi-Bold.otf"),
            ("霞鹜文楷", "res://assets/fonts/LXGW WenKai TC-Bold.ttf"),
        };
        #endregion

        #region Node References
        internal Control _panel;
        internal VBoxContainer _content;
        private ScrollContainer _scrollContainer;
        private TabContainer _tabContainer;
        internal MonsterManager _monsterManager;
        internal NpcManager _npcManager;
        #endregion

        #region Tabs
        internal DebugPanelTab[] _tabs;
        internal DebugPanelMapTab _mapTab;
        internal DebugPanelEntityTab _entityTab;
        internal DebugPanelSystemTab _systemTab;
        internal DebugPanelUITab _uiTab;
        internal DebugPanelDecorationTab _decorationTab;
        #endregion

        #region State
        internal GridManager _gridManager;
        internal Player _player;
        internal CameraController _camera;
        internal bool _calibrationEnabled;

        private LineEdit _activeLineEdit;
        private Action _activeLineEditApply;
        internal bool _isZoomSliderDragging;
        #endregion

        #region Cross-Tab References
        internal HSlider _gridSizeSlider;
        #endregion

        #region Preset Controls
        private OptionButton _presetOption;
        private Button _savePresetBtn;
        private Button _deletePresetBtn;
        private LineEdit _presetNameEdit;
        #endregion

        internal void SetActiveLineEdit(LineEdit edit, Action applyAction)
        {
            if (_activeLineEdit != null && _activeLineEdit != edit)
                _activeLineEditApply?.Invoke();

            _activeLineEdit = edit;
            _activeLineEditApply = applyAction;
        }

        internal void ClearActiveLineEdit()
        {
            _activeLineEdit = null;
            _activeLineEditApply = null;
        }



        internal void SelectTab(DebugPanelTab targetTab)
        {
            if (_tabContainer == null || _tabs == null || targetTab == null)
                return;

            for (int i = 0; i < _tabs.Length; i++)
            {
                if (ReferenceEquals(_tabs[i], targetTab))
                {
                    _tabContainer.CurrentTab = i;
                    return;
                }
            }
        }

        /// <summary>打开建筑工坊页签并显示 DebugPanel</summary>
        internal void SelectDecorationTab()
        {
            if (!Visible)
                Visible = true;
            SelectTab(_decorationTab);
            PanelManager.Instance?.RequestFocus(this);

            // 注意：地图编辑器暂停游戏时，UIInputPolicy 会自动保证本面板继续处理输入，
            // 此处不再手动修改 ProcessMode。
        }

        protected override void OnClosed()
        {
            base.OnClosed();
        }


    }
}
