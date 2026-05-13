using Godot;

namespace ClinetCSharp
{
    public partial class Player
    {
        public override void _Input(InputEvent @event)
        {
            CheckEntityClick(@event);

            if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
            {
                if (mb.Pressed)
                {
                    if (UiUtils.IsMouseOverAnyUi(GetViewport()))
                        return;

                    var localMouse = ToLocal(mb.GlobalPosition);
                    for (int i = LabelCount - 1; i >= 0; i--)
                    {
                        if (_labelContainers[i] == null || !_labelVisible[i])
                            continue;

                        var rect = GetLabelRect(i);
                        if (!rect.HasPoint(localMouse))
                            continue;

                        _dragging = true;
                        _dragIndex = i;
                        _dragStartMouse = localMouse;
                        _dragStartOffset = GetLabelOffset(i);
                        GetViewport().SetInputAsHandled();
                        return;
                    }
                }
                else if (_dragging)
                {
                    _dragging = false;
                    _dragIndex = -1;
                    GetViewport().SetInputAsHandled();
                }
            }
            else if (@event is InputEventMouseMotion mm && _dragging && _dragIndex >= 0)
            {
                var localMouse = ToLocal(mm.GlobalPosition);
                var delta = localMouse - _dragStartMouse;
                bool centerX = LabelCenterX[_dragIndex] || LabelAutoCenterX;
                var nextOffset = centerX
                    ? new Vector2(0, _dragStartOffset.Y + delta.Y)
                    : _dragStartOffset + delta;
                SetLabelOffset(_dragIndex, nextOffset);
                GetViewport().SetInputAsHandled();
            }
        }

        private Rect2 GetLabelRect(int index)
        {
            if (_labels[index] == null)
                return new Rect2();

            var textSize = _labels[index].GetMinimumSize();
            int baseFontSize = EntityLabelLayout.ResolveBaseFontSize(FontSizeOverride, VisualSize);
            bool centerX = LabelCenterX[index] || LabelAutoCenterX;
            Vector2 lineCenter = EntityLabelLayout.ResolveLineCenter(
                index,
                baseFontSize,
                VisualSize,
                centerX,
                centerX ? 0.0f : LabelXOffsets[index],
                LabelYOffsets[index]);
            var pos = lineCenter - textSize / 2;
            return new Rect2(pos, textSize);
        }
    }
}
