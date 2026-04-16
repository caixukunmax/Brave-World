using Godot;

namespace ClinetCSharp
{
    /// <summary>
    /// DebugPanel partial — 节点初始化
    /// </summary>
    public partial class DebugPanel
    {
        #region Initialization Methods
        private void InitializeNodeReferences()
        {
            // Main controls
            _panel = GetNode<Panel>("Control/Panel");
            _scrollContainer = GetNode<ScrollContainer>("Control/Panel/ScrollContainer");
            _tabContainer = GetNode<TabContainer>("Control/Panel/ScrollContainer/TabContainer");

            // Other references
            _fontFileDialog = GetNodeOrNull<FileDialog>("Control/FontFileDialog");
        }

        private void SetupPanel()
        {
            GD.Print("[DebugPanel] SetupPanel() starting...");
            _panel.Visible = false;
            GD.Print("[DebugPanel] Panel initialized, button controlled externally");
        }

        private void SetupSliders()
        {
            if (_gridSizeSlider != null) { _gridSizeSlider.MinValue = 32; _gridSizeSlider.MaxValue = 256; _gridSizeSlider.Step = 1; }
            if (_zoomSlider != null) { _zoomSlider.MinValue = 0.2; _zoomSlider.MaxValue = 3.0; _zoomSlider.Step = 0.1; }
            if (_gridLineWidthSlider != null) { _gridLineWidthSlider.MinValue = 0.1; _gridLineWidthSlider.MaxValue = 5.0; _gridLineWidthSlider.Step = 0.1; }
            if (_gridLineBrightnessSlider != null) { _gridLineBrightnessSlider.MinValue = 0.1; _gridLineBrightnessSlider.MaxValue = 1.0; _gridLineBrightnessSlider.Step = 0.1; }
            if (_cameraReturnDelaySlider != null) { _cameraReturnDelaySlider.MinValue = 0.0; _cameraReturnDelaySlider.MaxValue = 3.0; _cameraReturnDelaySlider.Step = 0.1; }
            if (_cameraReturnSpeedSlider != null) { _cameraReturnSpeedSlider.MinValue = 1.0; _cameraReturnSpeedSlider.MaxValue = 20.0; _cameraReturnSpeedSlider.Step = 1.0; }
            if (_cameraEasePowerSlider != null) { _cameraEasePowerSlider.MinValue = 1.0; _cameraEasePowerSlider.MaxValue = 5.0; _cameraEasePowerSlider.Step = 0.1; }
            if (_playerSizeSlider != null) { _playerSizeSlider.MinValue = 32; _playerSizeSlider.MaxValue = 256; _playerSizeSlider.Step = 1; }
            if (_borderWidthSlider != null) { _borderWidthSlider.MinValue = 1.0; _borderWidthSlider.MaxValue = 10.0; _borderWidthSlider.Step = 0.5; }
            if (_cornerRadiusSlider != null) { _cornerRadiusSlider.MinValue = 0.0; _cornerRadiusSlider.MaxValue = 30.0; _cornerRadiusSlider.Step = 1.0; }
            if (_bgOpacitySlider != null) { _bgOpacitySlider.MinValue = 0.0; _bgOpacitySlider.MaxValue = 1.0; _bgOpacitySlider.Step = 0.05; }
            if (_fontSizeSlider != null) { _fontSizeSlider.MinValue = 0; _fontSizeSlider.MaxValue = 48; _fontSizeSlider.Step = 1; }
            if (_lineSpacingSlider != null) { _lineSpacingSlider.MinValue = 0.5; _lineSpacingSlider.MaxValue = 1.5; _lineSpacingSlider.Step = 0.1; }
            if (_letterSpacingSlider != null) { _letterSpacingSlider.MinValue = -5; _letterSpacingSlider.MaxValue = 10; _letterSpacingSlider.Step = 1; }

            if (_alignLeftBtn != null)
                _alignLeftBtn.Pressed += () => OnTextAlignChanged(HorizontalAlignment.Left);
            if (_alignCenterBtn != null)
                _alignCenterBtn.Pressed += () => OnTextAlignChanged(HorizontalAlignment.Center);
            if (_alignRightBtn != null)
                _alignRightBtn.Pressed += () => OnTextAlignChanged(HorizontalAlignment.Right);
            if (_loadFontBtn != null)
                _loadFontBtn.Pressed += OnLoadFontPressed;
        }

        private void SetupLineColorButtons()
        {
            if (_lineColorButtons == null || _lineColorButtons.Count == 0) return;
            for (int i = 0; i < _lineColorButtons.Count; i++)
            {
                int index = i;
                Button btn = _lineColorButtons[i];
                if (btn != null)
                {
                    btn.Modulate = COLOR_PRESETS[0];
                    btn.Pressed += () => OnLineColorButtonPressed(index);
                }
            }
        }

        private void SetupFontOptions()
        {
            if (_fontOption == null) return;
            _fontOption.Clear();
            foreach (string fontName in FONT_NAMES)
                _fontOption.AddItem(fontName);
            _fontOption.AddItem("自定义...");
        }

        private void SetupEaseOptions()
        {
            if (_cameraEaseTypeOption == null) return;
            _cameraEaseTypeOption.Clear();
            foreach (string easeName in EASE_TYPE_NAMES)
                _cameraEaseTypeOption.AddItem(easeName);
        }
        #endregion

        #region Signal Connections
        private void ConnectSignals()
        {
            if (_gridSizeSlider != null)
            {
                _gridSizeSlider.ValueChanged += OnGridSizeChanged;
                _gridSizeSlider.DragEnded += OnGridSizeDragEnded;
            }
            if (_zoomSlider != null)
            {
                _zoomSlider.ValueChanged += OnZoomChanged;
                _zoomSlider.DragEnded += OnZoomDragEnded;
                _zoomSlider.DragStarted += OnZoomDragStarted;
            }

            if (_gridLineWidthSlider != null)
            {
                _gridLineWidthSlider.ValueChanged += OnGridLineWidthChanged;
                _gridLineWidthSlider.DragEnded += OnGridLineWidthDragEnded;
            }
            if (_gridLineBrightnessSlider != null)
            {
                _gridLineBrightnessSlider.ValueChanged += OnGridLineBrightnessChanged;
                _gridLineBrightnessSlider.DragEnded += OnGridLineBrightnessDragEnded;
            }
            if (_gridCoordsCheck != null)
                _gridCoordsCheck.Toggled += OnGridCoordsToggled;

            if (_cameraReturnDelaySlider != null)
            {
                _cameraReturnDelaySlider.ValueChanged += OnCameraReturnDelayChanged;
                _cameraReturnDelaySlider.DragEnded += OnCameraReturnDelayDragEnded;
            }
            if (_cameraReturnSpeedSlider != null)
            {
                _cameraReturnSpeedSlider.ValueChanged += OnCameraReturnSpeedChanged;
                _cameraReturnSpeedSlider.DragEnded += OnCameraReturnSpeedDragEnded;
            }
            if (_cameraEaseTypeOption != null)
                _cameraEaseTypeOption.ItemSelected += OnCameraEaseTypeChanged;
            if (_cameraEasePowerSlider != null)
            {
                _cameraEasePowerSlider.ValueChanged += OnCameraEasePowerChanged;
                _cameraEasePowerSlider.DragEnded += OnCameraEasePowerDragEnded;
            }
            if (_debugInfoCheck != null)
                _debugInfoCheck.Toggled += OnDebugInfoToggled;
            if (_cameraDebugCheck != null)
                _cameraDebugCheck.Toggled += OnCameraDebugToggled;

            if (_playerSizeSlider != null)
            {
                _playerSizeSlider.ValueChanged += OnPlayerSizeChanged;
                _playerSizeSlider.DragEnded += OnPlayerSizeDragEnded;
            }
            if (_playerSizeScaleSlider != null)
            {
                _playerSizeScaleSlider.ValueChanged += OnPlayerSizeScaleChanged;
                _playerSizeScaleSlider.DragEnded += OnPlayerSizeScaleDragEnded;
            }
            if (_borderWidthSlider != null)
            {
                _borderWidthSlider.ValueChanged += OnBorderWidthChanged;
                _borderWidthSlider.DragEnded += OnBorderWidthDragEnded;
            }
            if (_borderWidthScaleSlider != null)
            {
                _borderWidthScaleSlider.ValueChanged += OnBorderWidthScaleChanged;
                _borderWidthScaleSlider.DragEnded += OnBorderWidthScaleDragEnded;
            }
            if (_cornerRadiusSlider != null)
            {
                _cornerRadiusSlider.ValueChanged += OnCornerRadiusChanged;
                _cornerRadiusSlider.DragEnded += OnCornerRadiusDragEnded;
            }
            if (_bgOpacitySlider != null)
            {
                _bgOpacitySlider.ValueChanged += OnBgOpacityChanged;
                _bgOpacitySlider.DragEnded += OnBgOpacityDragEnded;
            }

            if (_fontOption != null)
                _fontOption.ItemSelected += OnFontSelected;
            if (_fontSizeSlider != null)
            {
                _fontSizeSlider.ValueChanged += OnFontSizeChanged;
                _fontSizeSlider.DragEnded += OnFontSizeDragEnded;
            }
            if (_lineSpacingSlider != null)
            {
                _lineSpacingSlider.ValueChanged += OnLineSpacingChanged;
                _lineSpacingSlider.DragEnded += OnLineSpacingDragEnded;
            }
            if (_letterSpacingSlider != null)
            {
                _letterSpacingSlider.ValueChanged += OnLetterSpacingChanged;
                _letterSpacingSlider.DragEnded += OnLetterSpacingDragEnded;
            }
            if (_boldCheck != null)
                _boldCheck.Toggled += OnBoldToggled;
            if (_italicCheck != null)
                _italicCheck.Toggled += OnItalicToggled;
            if (_shadowCheck != null)
                _shadowCheck.Toggled += OnShadowToggled;
            if (_labelAutoCenterXCheck != null)
                _labelAutoCenterXCheck.Toggled += OnLabelAutoCenterXToggled;

            if (_fontFileDialog != null)
                _fontFileDialog.FileSelected += OnFontFileSelected;
        }
        #endregion
    }
}
