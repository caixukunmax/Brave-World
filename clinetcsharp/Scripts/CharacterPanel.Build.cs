using Godot;

namespace ClinetCSharp
{
    public partial class CharacterPanel
    {
        private void BuildAttributeRows()
        {
            foreach (var definition in AttrDefs)
                _content.AddChild(BuildAttributeRow(definition));
        }

        private HBoxContainer BuildAttributeRow(AttrDefinition definition)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);

            var nameLabel = new Label
            {
                Text = definition.Label,
                CustomMinimumSize = new Vector2(60, 0),
            };
            row.AddChild(nameLabel);

            var spinBox = new SpinBox
            {
                MinValue = definition.MinValue,
                MaxValue = definition.MaxValue,
                Step = 1,
                CustomMinimumSize = new Vector2(100, 0),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            row.AddChild(spinBox);
            _spinBoxes[definition.Key] = spinBox;

            var applyButton = new Button
            {
                Text = "应用",
                CustomMinimumSize = new Vector2(50, 0),
            };
            applyButton.Pressed += () => OnApplyAttr(definition.GmName, (int)spinBox.Value);
            row.AddChild(applyButton);

            return row;
        }

        private void BuildBottomActions()
        {
            var bottomRow = new HBoxContainer();
            bottomRow.AddThemeConstantOverride("separation", 8);

            var applyAllButton = new Button
            {
                Text = "全部应用",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            applyAllButton.Pressed += OnApplyAll;
            bottomRow.AddChild(applyAllButton);

            var refreshButton = new Button
            {
                Text = "刷新",
            };
            refreshButton.Pressed += RefreshFromPlayer;
            bottomRow.AddChild(refreshButton);

            _content.AddChild(bottomRow);
        }
    }
}
