using Godot;
using System.Collections.Generic;

namespace ClinetCSharp
{
    /// <summary>
    /// DebugPanel partial — 预设 UI 创建
    /// Tab UI creation is now handled by each tab's BuildUI method.
    /// </summary>
    public partial class DebugPanel
    {
        #region Preset UI Creation
        private void CreatePresetUI()
        {
            VBoxContainer mainContainer = new VBoxContainer();
            mainContainer.Name = "PresetContainer";
            mainContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            mainContainer.AddThemeConstantOverride("separation", 8);
            mainContainer.Alignment = BoxContainer.AlignmentMode.Center;

            HBoxContainer row1 = new HBoxContainer();
            row1.Name = "ConfigRow";
            row1.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            row1.AddThemeConstantOverride("separation", 20);
            row1.Alignment = BoxContainer.AlignmentMode.Center;

            Button discardButton = new Button();
            discardButton.Name = "DiscardButton";
            discardButton.Text = "↩ 放弃";
            discardButton.Pressed += OnDiscardChangesPressed;
            row1.AddChild(discardButton);

            Button saveConfigButton = new Button();
            saveConfigButton.Name = "SaveConfigButton";
            saveConfigButton.Text = "✓ 保存";
            saveConfigButton.Pressed += OnMapSavePressed;
            row1.AddChild(saveConfigButton);

            HBoxContainer row2 = new HBoxContainer();
            row2.Name = "PresetRow";
            row2.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            row2.AddThemeConstantOverride("separation", 8);
            row2.Alignment = BoxContainer.AlignmentMode.Center;

            row2.AddChild(CreateLabel("预设:"));

            _presetOption = new OptionButton();
            _presetOption.Name = "PresetOption";
            _presetOption.CustomMinimumSize = new Vector2(100, 0);
            _presetOption.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            _presetOption.ItemSelected += OnPresetSelected;
            row2.AddChild(_presetOption);

            _deletePresetBtn = new Button();
            _deletePresetBtn.Name = "DeletePresetBtn";
            _deletePresetBtn.Text = "🗑";
            _deletePresetBtn.TooltipText = "删除选中预设";
            _deletePresetBtn.Pressed += OnDeletePresetPressed;
            row2.AddChild(_deletePresetBtn);

            Control spacer = new Control();
            spacer.CustomMinimumSize = new Vector2(15, 0);
            row2.AddChild(spacer);

            _savePresetBtn = new Button();
            _savePresetBtn.Name = "SavePresetBtn";
            _savePresetBtn.Text = "+ 新建";
            _savePresetBtn.TooltipText = "将当前配置保存为新预设";
            _savePresetBtn.Pressed += OnSavePresetPressed;
            row2.AddChild(_savePresetBtn);

            _presetNameEdit = new LineEdit();
            _presetNameEdit.Name = "PresetNameEdit";
            _presetNameEdit.Visible = false;
            row2.AddChild(_presetNameEdit);

            mainContainer.AddChild(row1);
            mainContainer.AddChild(row2);
            _panel.AddChild(mainContainer);

            mainContainer.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
            mainContainer.OffsetLeft = 10;
            mainContainer.OffsetTop = -80;
            mainContainer.OffsetRight = -10;
            mainContainer.OffsetBottom = -5;

            RefreshPresetList();
        }

        private void RefreshPresetList()
        {
            if (_presetOption == null) return;

            int currentIndex = _presetOption.Selected;
            string currentMetadata = "";
            if (currentIndex >= 0 && currentIndex < _presetOption.ItemCount)
                currentMetadata = _presetOption.GetItemMetadata(currentIndex).AsString();

            _presetOption.Clear();
            _presetOption.AddItem("默认", 0);
            _presetOption.SetItemMetadata(0, "");

            List<string> presets = GetPresetList();
            int newSelectedIndex = 0;

            for (int i = 0; i < presets.Count; i++)
            {
                _presetOption.AddItem(presets[i], i + 1);
                _presetOption.SetItemMetadata(i + 1, presets[i]);
                if (presets[i] == currentMetadata)
                    newSelectedIndex = i + 1;
            }

            _presetOption.Select(newSelectedIndex);
        }
        #endregion

        #region UI Helper Methods
        private Label CreateLabel(string text, bool expand = false)
        {
            Label label = new Label();
            label.Text = text;
            if (expand)
                label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            return label;
        }
        #endregion
    }
}