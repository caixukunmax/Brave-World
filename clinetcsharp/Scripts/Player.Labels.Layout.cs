using Godot;

namespace ClinetCSharp
{
    public partial class Player
    {
        protected void ClearLabelNodes()
        {
            for (int i = 0; i < LabelCount; i++)
            {
                if (_labelContainers[i] != null && IsInstanceValid(_labelContainers[i]))
                {
                    _labelContainers[i].QueueFree();
                    _labelContainers[i] = null;
                    _labels[i] = null;
                }
            }
        }

        protected void CreateLabelNodes()
        {
            for (int i = 0; i < LabelCount; i++)
            {
                var container = new Control();
                container.Name = $"LabelContainer_{i}";
                AddChild(container);

                var label = CreateLabelNode(i);
                container.AddChild(label);

                _labelContainers[i] = container;
                _labels[i] = label;
            }
        }

        protected RichTextLabel CreateLabelNode(int index)
        {
            var label = new RichTextLabel();
            label.Name = $"Label_{index}";
            label.FitContent = true;
            label.ScrollActive = false;
            label.BbcodeEnabled = true;
            label.Text = PlayerLabelTexts[index];
            label.HorizontalAlignment = TextAlignment;
            label.VerticalAlignment = VerticalAlignment.Center;
            label.AutowrapMode = TextServer.AutowrapMode.Off;
            label.CustomMinimumSize = new Vector2(1, 1);
            label.AddThemeColorOverride("font_color", GetLineColor(index));
            return label;
        }

        protected Color GetLineColor(int index)
        {
            if (index >= 0 && index < LineColors.Count)
                return LineColors[index];
            return TextColor;
        }

        protected void UpdateAllLabelPositions()
        {
            int baseFontSize = EntityLabelLayout.ResolveBaseFontSize(FontSizeOverride, VisualSize);
            var font = ThemeDB.FallbackFont;

            for (int i = 0; i < LabelCount; i++)
            {
                if (_labelContainers[i] == null)
                    continue;

                _labelContainers[i].Visible = GlobalLabelsVisible && _labelVisible[i];
                if (!_labelVisible[i])
                    continue;

                var label = _labels[i];
                if (label == null)
                    continue;

                // 使用同步的 GetStringSize 避免 RichTextLabel 异步布局（FitContent）导致的大小计算延迟
                int fs = _labelFontSizes[i] > 0 ? _labelFontSizes[i] : baseFontSize;
                var textSize = font.GetStringSize(PlayerLabelTexts[i], HorizontalAlignment.Center, -1, fs);
                bool centerX = LabelCenterX[i] || LabelAutoCenterX;
                Vector2 lineCenter = EntityLabelLayout.ResolveLineCenter(
                    i,
                    baseFontSize,
                    VisualSize,
                    centerX,
                    centerX ? 0.0f : LabelXOffsets[i],
                    LabelYOffsets[i]);
                var pos = lineCenter - textSize / 2;
                _labelContainers[i].Position = pos;
            }
        }
    }
}
