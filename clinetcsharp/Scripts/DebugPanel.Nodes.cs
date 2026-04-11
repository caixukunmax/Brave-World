using Godot;

namespace ClinetCSharp
{
    /// <summary>
    /// DebugPanel partial — 节点初始化、分区折叠系统
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

            // Map tab - section buttons
            _sectionBasicBtn = GetNode<Button>("Control/Panel/ScrollContainer/TabContainer/地图/SectionBasic");
            _sectionGridBtn = GetNode<Button>("Control/Panel/ScrollContainer/TabContainer/地图/SectionGrid");
            _sectionCameraBtn = GetNode<Button>("Control/Panel/ScrollContainer/TabContainer/地图/SectionCamera");
            _sectionDebugBtn = GetNode<Button>("Control/Panel/ScrollContainer/TabContainer/地图/SectionDebug");

            // Map tab - basic settings
            _gridSizeSlider = GetNode<HSlider>("Control/Panel/ScrollContainer/TabContainer/地图/BasicGroup/GridSizeSlider");
            _gridSizeValue = GetNode<Label>("Control/Panel/ScrollContainer/TabContainer/地图/BasicGroup/GridSizeRow/ValueLabel");
            _zoomSlider = GetNode<HSlider>("Control/Panel/ScrollContainer/TabContainer/地图/BasicGroup/ZoomSlider");
            _zoomValue = GetNode<Label>("Control/Panel/ScrollContainer/TabContainer/地图/BasicGroup/ZoomRow/ValueLabel");

            // Map tab - grid line settings
            _gridLineWidthSlider = GetNode<HSlider>("Control/Panel/ScrollContainer/TabContainer/地图/GridGroup/GridLineWidthSlider");
            _gridLineWidthValue = GetNode<Label>("Control/Panel/ScrollContainer/TabContainer/地图/GridGroup/GridLineWidthRow/ValueLabel");
            _gridLineBrightnessSlider = GetNode<HSlider>("Control/Panel/ScrollContainer/TabContainer/地图/GridGroup/GridLineBrightnessSlider");
            _gridLineBrightnessValue = GetNode<Label>("Control/Panel/ScrollContainer/TabContainer/地图/GridGroup/GridLineBrightnessRow/ValueLabel");
            _gridCoordsCheck = GetNode<CheckButton>("Control/Panel/ScrollContainer/TabContainer/地图/GridGroup/GridCoordsRow/GridCoordsCheck");

            // Map tab - camera settings
            _cameraReturnDelaySlider = GetNode<HSlider>("Control/Panel/ScrollContainer/TabContainer/地图/CameraGroup/CameraReturnDelaySlider");
            _cameraReturnDelayValue = GetNode<Label>("Control/Panel/ScrollContainer/TabContainer/地图/CameraGroup/CameraReturnDelayRow/ValueLabel");
            _cameraReturnSpeedSlider = GetNode<HSlider>("Control/Panel/ScrollContainer/TabContainer/地图/CameraGroup/CameraReturnSpeedSlider");
            _cameraReturnSpeedValue = GetNode<Label>("Control/Panel/ScrollContainer/TabContainer/地图/CameraGroup/CameraReturnSpeedRow/ValueLabel");
            _cameraEaseTypeOption = GetNode<OptionButton>("Control/Panel/ScrollContainer/TabContainer/地图/CameraGroup/CameraEaseTypeRow/EaseTypeOption");
            _cameraEasePowerSlider = GetNode<HSlider>("Control/Panel/ScrollContainer/TabContainer/地图/CameraGroup/CameraEasePowerSlider");
            _cameraEasePowerValue = GetNode<Label>("Control/Panel/ScrollContainer/TabContainer/地图/CameraGroup/CameraEasePowerRow/ValueLabel");

            // Map tab - debug toggles
            _debugInfoCheck = GetNode<CheckButton>("Control/Panel/ScrollContainer/TabContainer/地图/DebugGroup/DebugInfoRow/DebugInfoCheck");
            _cameraDebugCheck = GetNode<CheckButton>("Control/Panel/ScrollContainer/TabContainer/地图/DebugGroup/CameraDebugRow/CameraDebugCheck");

            // Player tab - section buttons
            _sectionLookBtn = GetNode<Button>("Control/Panel/ScrollContainer/TabContainer/玩家/SectionLook");
            _sectionTextBtn = GetNode<Button>("Control/Panel/ScrollContainer/TabContainer/玩家/SectionText");
            _sectionLabelCtrlBtn = GetNode<Button>("Control/Panel/ScrollContainer/TabContainer/玩家/SectionLabelCtrl");
            _sectionEffectBtn = GetNode<Button>("Control/Panel/ScrollContainer/TabContainer/玩家/SectionEffect");

            // Player tab - appearance
            _playerSizeSlider = GetNode<HSlider>("Control/Panel/ScrollContainer/TabContainer/玩家/LookGroup/PlayerSizeSlider");
            _playerSizeValue = GetNode<Label>("Control/Panel/ScrollContainer/TabContainer/玩家/LookGroup/PlayerSizeRow/ValueLabel");
            _borderWidthSlider = GetNode<HSlider>("Control/Panel/ScrollContainer/TabContainer/玩家/LookGroup/BorderWidthSlider");
            _borderWidthValue = GetNode<Label>("Control/Panel/ScrollContainer/TabContainer/玩家/LookGroup/BorderRow/ValueLabel");
            _cornerRadiusSlider = GetNode<HSlider>("Control/Panel/ScrollContainer/TabContainer/玩家/LookGroup/CornerRadiusSlider");
            _cornerRadiusValue = GetNode<Label>("Control/Panel/ScrollContainer/TabContainer/玩家/LookGroup/CornerRadiusRow/ValueLabel");
            _bgOpacitySlider = GetNode<HSlider>("Control/Panel/ScrollContainer/TabContainer/玩家/LookGroup/BgOpacitySlider");
            _bgOpacityValue = GetNode<Label>("Control/Panel/ScrollContainer/TabContainer/玩家/LookGroup/BgOpacityRow/ValueLabel");

            // Player tab - text style
            _fontOption = GetNode<OptionButton>("Control/Panel/ScrollContainer/TabContainer/玩家/TextGroup/FontRow/FontOption");
            _loadFontBtn = GetNode<Button>("Control/Panel/ScrollContainer/TabContainer/玩家/TextGroup/FontRow/LoadFontBtn");
            _fontSizeSlider = GetNode<HSlider>("Control/Panel/ScrollContainer/TabContainer/玩家/TextGroup/FontSizeSlider");
            _fontSizeValue = GetNode<Label>("Control/Panel/ScrollContainer/TabContainer/玩家/TextGroup/FontSizeRow/ValueLabel");
            _lineSpacingSlider = GetNode<HSlider>("Control/Panel/ScrollContainer/TabContainer/玩家/TextGroup/LineSpacingSlider");
            _lineSpacingValue = GetNode<Label>("Control/Panel/ScrollContainer/TabContainer/玩家/TextGroup/LineSpacingRow/ValueLabel");
            _letterSpacingSlider = GetNode<HSlider>("Control/Panel/ScrollContainer/TabContainer/玩家/TextGroup/LetterSpacingSlider");
            _letterSpacingValue = GetNode<Label>("Control/Panel/ScrollContainer/TabContainer/玩家/TextGroup/LetterSpacingRow/ValueLabel");
            _alignLeftBtn = GetNode<Button>("Control/Panel/ScrollContainer/TabContainer/玩家/TextGroup/AlignRow/AlignLeft");
            _alignCenterBtn = GetNode<Button>("Control/Panel/ScrollContainer/TabContainer/玩家/TextGroup/AlignRow/AlignCenter");
            _alignRightBtn = GetNode<Button>("Control/Panel/ScrollContainer/TabContainer/玩家/TextGroup/AlignRow/AlignRight");

            // Player tab - visual effects
            _boldCheck = GetNode<CheckButton>("Control/Panel/ScrollContainer/TabContainer/玩家/EffectGroup/FontStyleRow/BoldCheck");
            _italicCheck = GetNode<CheckButton>("Control/Panel/ScrollContainer/TabContainer/玩家/EffectGroup/FontStyleRow/ItalicCheck");
            _shadowCheck = GetNode<CheckButton>("Control/Panel/ScrollContainer/TabContainer/玩家/EffectGroup/FontStyleRow/ShadowCheck");

            // Other references
            _fontFileDialog = GetNode<FileDialog>("Control/FontFileDialog");
        }

        private void SetupPanel()
        {
            GD.Print("[DebugPanel] SetupPanel() starting...");
            _panel.Visible = false;
            GD.Print("[DebugPanel] Panel initialized, button controlled externally");
        }

        private void InitSectionSystem()
        {
            // Map tab groups
            _mapGroups["SectionBasic"] = GetNode("Control/Panel/ScrollContainer/TabContainer/地图/BasicGroup");
            _mapGroups["SectionGrid"] = GetNode("Control/Panel/ScrollContainer/TabContainer/地图/GridGroup");
            _mapGroups["SectionCamera"] = GetNode("Control/Panel/ScrollContainer/TabContainer/地图/CameraGroup");
            _mapGroups["SectionDebug"] = GetNode("Control/Panel/ScrollContainer/TabContainer/地图/DebugGroup");

            // Player tab groups
            _playerGroups["SectionLook"] = GetNode("Control/Panel/ScrollContainer/TabContainer/玩家/LookGroup");
            _playerGroups["SectionText"] = GetNode("Control/Panel/ScrollContainer/TabContainer/玩家/TextGroup");
            _playerGroups["SectionLabelCtrl"] = GetNode("Control/Panel/ScrollContainer/TabContainer/玩家/LabelCtrlGroup");
            _playerGroups["SectionEffect"] = GetNode("Control/Panel/ScrollContainer/TabContainer/玩家/EffectGroup");

            // Connect section title buttons
            _sectionBasicBtn.Pressed += () => ToggleGroup("SectionBasic", _sectionBasicBtn);
            _sectionGridBtn.Pressed += () => ToggleGroup("SectionGrid", _sectionGridBtn);
            _sectionCameraBtn.Pressed += () => ToggleGroup("SectionCamera", _sectionCameraBtn);
            _sectionDebugBtn.Pressed += () => ToggleGroup("SectionDebug", _sectionDebugBtn);
            _sectionLookBtn.Pressed += () => ToggleGroup("SectionLook", _sectionLookBtn);
            _sectionTextBtn.Pressed += () => ToggleGroup("SectionText", _sectionTextBtn);
            _sectionLabelCtrlBtn.Pressed += () => ToggleGroup("SectionLabelCtrl", _sectionLabelCtrlBtn);
            _sectionEffectBtn.Pressed += () => ToggleGroup("SectionEffect", _sectionEffectBtn);

            // Initialize all groups to expanded state
            foreach (var groupName in _mapGroups.Keys)
                _sectionStates[groupName] = true;
            foreach (var groupName in _playerGroups.Keys)
                _sectionStates[groupName] = true;
        }

        private void ToggleGroup(string groupName, Button button)
        {
            Node targetGroup = _mapGroups.ContainsKey(groupName) ? (Node)_mapGroups[groupName] : (_playerGroups.ContainsKey(groupName) ? (Node)_playerGroups[groupName] : null);
            if (targetGroup != null)
            {
                bool isExpanded = _sectionStates.ContainsKey(groupName) ? (bool)_sectionStates[groupName] : true;
                _sectionStates[groupName] = !isExpanded;
                targetGroup.Set("visible", !isExpanded);
                button.Text = (isExpanded ? "▶ " : "▼ ") + button.Text.Substring(2);
            }
        }

        private void SetupSliders()
        {
            _gridSizeSlider.MinValue = 32; _gridSizeSlider.MaxValue = 256; _gridSizeSlider.Step = 1;
            _zoomSlider.MinValue = 0.2; _zoomSlider.MaxValue = 3.0; _zoomSlider.Step = 0.1;
            _gridLineWidthSlider.MinValue = 0.1; _gridLineWidthSlider.MaxValue = 5.0; _gridLineWidthSlider.Step = 0.1;
            _gridLineBrightnessSlider.MinValue = 0.1; _gridLineBrightnessSlider.MaxValue = 1.0; _gridLineBrightnessSlider.Step = 0.1;
            _cameraReturnDelaySlider.MinValue = 0.0; _cameraReturnDelaySlider.MaxValue = 3.0; _cameraReturnDelaySlider.Step = 0.1;
            _cameraReturnSpeedSlider.MinValue = 1.0; _cameraReturnSpeedSlider.MaxValue = 20.0; _cameraReturnSpeedSlider.Step = 1.0;
            _cameraEasePowerSlider.MinValue = 1.0; _cameraEasePowerSlider.MaxValue = 5.0; _cameraEasePowerSlider.Step = 0.1;
            _playerSizeSlider.MinValue = 32; _playerSizeSlider.MaxValue = 256; _playerSizeSlider.Step = 1;
            _borderWidthSlider.MinValue = 1.0; _borderWidthSlider.MaxValue = 10.0; _borderWidthSlider.Step = 0.5;
            _cornerRadiusSlider.MinValue = 0.0; _cornerRadiusSlider.MaxValue = 30.0; _cornerRadiusSlider.Step = 1.0;
            _bgOpacitySlider.MinValue = 0.0; _bgOpacitySlider.MaxValue = 1.0; _bgOpacitySlider.Step = 0.05;
            _fontSizeSlider.MinValue = 0; _fontSizeSlider.MaxValue = 48; _fontSizeSlider.Step = 1;
            _lineSpacingSlider.MinValue = 0.5; _lineSpacingSlider.MaxValue = 1.5; _lineSpacingSlider.Step = 0.1;
            _letterSpacingSlider.MinValue = -5; _letterSpacingSlider.MaxValue = 10; _letterSpacingSlider.Step = 1;

            _alignLeftBtn.Pressed += () => OnTextAlignChanged(HorizontalAlignment.Left);
            _alignCenterBtn.Pressed += () => OnTextAlignChanged(HorizontalAlignment.Center);
            _alignRightBtn.Pressed += () => OnTextAlignChanged(HorizontalAlignment.Right);
            _loadFontBtn.Pressed += OnLoadFontPressed;
        }

        private void SetupLineColorButtons()
        {
            _lineColorButtons = new System.Collections.Generic.List<Button>
            {
                GetNode<Button>("Control/Panel/ScrollContainer/TabContainer/玩家/EffectGroup/LineColorRow1/ColorButton"),
                GetNode<Button>("Control/Panel/ScrollContainer/TabContainer/玩家/EffectGroup/LineColorRow2/ColorButton"),
                GetNode<Button>("Control/Panel/ScrollContainer/TabContainer/玩家/EffectGroup/LineColorRow3/ColorButton"),
                GetNode<Button>("Control/Panel/ScrollContainer/TabContainer/玩家/EffectGroup/LineColorRow4/ColorButton"),
            };

            for (int i = 0; i < _lineColorButtons.Count; i++)
            {
                int index = i;
                Button btn = _lineColorButtons[i];
                btn.Modulate = COLOR_PRESETS[0];
                btn.Pressed += () => OnLineColorButtonPressed(index);
            }
        }

        private void SetupFontOptions()
        {
            _fontOption.Clear();
            foreach (string fontName in FONT_NAMES)
                _fontOption.AddItem(fontName);
            _fontOption.AddItem("自定义...");
        }

        private void SetupEaseOptions()
        {
            _cameraEaseTypeOption.Clear();
            foreach (string easeName in EASE_TYPE_NAMES)
                _cameraEaseTypeOption.AddItem(easeName);
        }
        #endregion

        #region Signal Connections
        private void ConnectSignals()
        {
            _gridSizeSlider.ValueChanged += OnGridSizeChanged;
            _gridSizeSlider.DragEnded += OnGridSizeDragEnded;
            _zoomSlider.ValueChanged += OnZoomChanged;
            _zoomSlider.DragEnded += OnZoomDragEnded;
            _zoomSlider.DragStarted += OnZoomDragStarted;

            _gridLineWidthSlider.ValueChanged += OnGridLineWidthChanged;
            _gridLineWidthSlider.DragEnded += OnGridLineWidthDragEnded;
            _gridLineBrightnessSlider.ValueChanged += OnGridLineBrightnessChanged;
            _gridLineBrightnessSlider.DragEnded += OnGridLineBrightnessDragEnded;
            _gridCoordsCheck.Toggled += OnGridCoordsToggled;
            if (_lineWidthScaleSlider != null)
            {
                _lineWidthScaleSlider.ValueChanged += OnLineWidthScaleChanged;
                _lineWidthScaleSlider.DragEnded += OnLineWidthScaleDragEnded;
            }

            _cameraReturnDelaySlider.ValueChanged += OnCameraReturnDelayChanged;
            _cameraReturnDelaySlider.DragEnded += OnCameraReturnDelayDragEnded;
            _cameraReturnSpeedSlider.ValueChanged += OnCameraReturnSpeedChanged;
            _cameraReturnSpeedSlider.DragEnded += OnCameraReturnSpeedDragEnded;
            _cameraEaseTypeOption.ItemSelected += OnCameraEaseTypeChanged;
            _cameraEasePowerSlider.ValueChanged += OnCameraEasePowerChanged;
            _cameraEasePowerSlider.DragEnded += OnCameraEasePowerDragEnded;
            if (_freeLookCheck != null)
                _freeLookCheck.Toggled += OnFreeLookToggled;

            _debugInfoCheck.Toggled += OnDebugInfoToggled;
            _cameraDebugCheck.Toggled += OnCameraDebugToggled;

            _playerSizeSlider.ValueChanged += OnPlayerSizeChanged;
            _playerSizeSlider.DragEnded += OnPlayerSizeDragEnded;
            _borderWidthSlider.ValueChanged += OnBorderWidthChanged;
            _borderWidthSlider.DragEnded += OnBorderWidthDragEnded;
            _cornerRadiusSlider.ValueChanged += OnCornerRadiusChanged;
            _cornerRadiusSlider.DragEnded += OnCornerRadiusDragEnded;
            _bgOpacitySlider.ValueChanged += OnBgOpacityChanged;
            _bgOpacitySlider.DragEnded += OnBgOpacityDragEnded;

            _fontOption.ItemSelected += OnFontSelected;
            _fontSizeSlider.ValueChanged += OnFontSizeChanged;
            _fontSizeSlider.DragEnded += OnFontSizeDragEnded;
            _lineSpacingSlider.ValueChanged += OnLineSpacingChanged;
            _lineSpacingSlider.DragEnded += OnLineSpacingDragEnded;
            _letterSpacingSlider.ValueChanged += OnLetterSpacingChanged;
            _letterSpacingSlider.DragEnded += OnLetterSpacingDragEnded;
            _boldCheck.Toggled += OnBoldToggled;
            _italicCheck.Toggled += OnItalicToggled;
            _shadowCheck.Toggled += OnShadowToggled;

            _fontFileDialog.FileSelected += OnFontFileSelected;
        }
        #endregion
    }
}
