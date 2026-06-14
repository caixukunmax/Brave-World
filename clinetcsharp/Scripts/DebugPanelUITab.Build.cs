using Godot;

namespace ClinetCSharp
{
    public partial class DebugPanelUITab
    {
        public override void BuildUI(VBoxContainer tabContainer)
        {
            _tabContainer = tabContainer;

            AddTabTitle(tabContainer, "UI 配置", 13);
            AddSectionSeparator(tabContainer);

            BuildSkillBarSection(tabContainer);
            BuildFunctionBarSection(tabContainer);
            BuildBuffBarSection(tabContainer);
        }

        private void BuildSkillBarSection(VBoxContainer tabContainer)
        {
            BuildSectionTitle(tabContainer, "技能栏", 12);

            var skillBar = Owner.GetTree()?.GetFirstNodeInGroup("skill_bar") as SkillBar;
            float iconSizeDefault = skillBar?.IconSize ?? 44;
            float spacingDefault = skillBar?.SlotSpacing ?? 6;
            float marginRightDefault = skillBar?.MarginRight ?? 20;
            float marginBottomDefault = skillBar?.MarginBottom ?? 20;
            float nameFontSizeDefault = skillBar?.NameFontSize ?? 10;

            (_skillBarIconSizeSlider, _skillBarIconSizeValue) = CreateSliderRow(tabContainer, "图标大小", 24, 96, iconSizeDefault, 1f);
            (_skillBarSpacingSlider, _skillBarSpacingValue) = CreateSliderRow(tabContainer, "槽位间距", 0, 40, spacingDefault, 1f);
            (_skillBarNameFontSizeSlider, _skillBarNameFontSizeValue) = CreateSliderRow(tabContainer, "技能名字号", 6, 28, nameFontSizeDefault, 1f);
            (_skillBarMarginRightSlider, _skillBarMarginRightValue) = CreateSliderRow(tabContainer, "右边距", 0, 200, marginRightDefault, 1f);
            (_skillBarMarginBottomSlider, _skillBarMarginBottomValue) = CreateSliderRow(tabContainer, "底边距", 0, 200, marginBottomDefault, 1f);
        }

        private void BuildFunctionBarSection(VBoxContainer tabContainer)
        {
            AddSectionSeparator(tabContainer);
            BuildSectionTitle(tabContainer, "功能按钮栏(左上)", 12);

            var functionBar = Owner.GetTree()?.GetFirstNodeInGroup("function_bar") as FunctionButtonBar;
            float offsetXDefault = functionBar?.OffsetX ?? 8;
            float offsetYDefault = functionBar?.OffsetY ?? 8;
            float spacingDefault = functionBar?.ButtonSpacing ?? 3;

            (_fnBarOffsetXSlider, _fnBarOffsetXValue) = CreateSliderRow(tabContainer, "水平偏移", 0, 300, offsetXDefault, 1f);
            (_fnBarOffsetYSlider, _fnBarOffsetYValue) = CreateSliderRow(tabContainer, "垂直偏移", 0, 300, offsetYDefault, 1f);
            (_fnBarSpacingSlider, _fnBarSpacingValue) = CreateSliderRow(tabContainer, "按钮间距", 0, 20, spacingDefault, 1f);
        }

        private void BuildBuffBarSection(VBoxContainer tabContainer)
        {
            AddSectionSeparator(tabContainer);
            BuildSectionTitle(tabContainer, "Buff 栏", 12);

            var buffBar = Owner.GetTree()?.GetFirstNodeInGroup("buff_bar") as BuffBar;
            float iconSizeDefault = buffBar?.IconSize ?? 36;
            float spacingDefault = buffBar?.SlotSpacing ?? 4;
            float offsetXDefault = buffBar?.OffsetX ?? 0;
            float offsetYDefault = buffBar?.OffsetY ?? 0;

            (_buffBarIconSizeSlider, _buffBarIconSizeValue) = CreateSliderRow(tabContainer, "图标大小", 16, 64, iconSizeDefault, 1f);
            (_buffBarSpacingSlider, _buffBarSpacingValue) = CreateSliderRow(tabContainer, "槽位间距", 0, 20, spacingDefault, 1f);
            (_buffBarOffsetXSlider, _buffBarOffsetXValue) = CreateSliderRow(tabContainer, "X 偏移", -500, 500, offsetXDefault, 1f);
            (_buffBarOffsetYSlider, _buffBarOffsetYValue) = CreateSliderRow(tabContainer, "Y 偏移", -500, 500, offsetYDefault, 1f);

            _buffBarForceShowBtn = new Button { Text = "强制显示" };
            _buffBarForceShowBtn.Pressed += OnBuffBarForceShow;
            tabContainer.AddChild(_buffBarForceShowBtn);

            _buffBarRightAlignCheck = new CheckBox { Text = "靠右对齐(新buff压栈)" };
            _buffBarRightAlignCheck.ButtonPressed = buffBar?.RightAlign ?? false;
            tabContainer.AddChild(_buffBarRightAlignCheck);
        }

        private static void BuildSectionTitle(VBoxContainer tabContainer, string text, int fontSize)
        {
            var title = new Label
            {
                Name = "_lbl",
                Text = text,
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            title.AddThemeFontSizeOverride("font_size", fontSize);
            tabContainer.AddChild(title);
        }
    }
}
