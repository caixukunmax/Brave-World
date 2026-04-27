using Godot;

namespace ClinetCSharp
{
    /// <summary>
    /// DebugPanel partial — 面板边缘拖拽调整大小
    /// </summary>
    public partial class DebugPanel
    {
        private const float RESIZE_EDGE_SIZE = 8.0f;
        private const float MIN_PANEL_WIDTH = 250.0f;
        private const float MIN_PANEL_HEIGHT = 200.0f;

        private enum ResizeEdge
        {
            None, Left, Top, Right, Bottom,
            TopLeft, TopRight, BottomLeft, BottomRight
        }

        private ResizeEdge _activeResizeEdge = ResizeEdge.None;
        private bool _resizeCursorActive = false;
        private Vector2 _resizeDragStart;
        private float _resizeStartOL;
        private float _resizeStartOT;
        private float _resizeStartOR;
        private float _resizeStartOB;

        /// <summary>
        /// 当前是否正在调整面板大小
        /// </summary>
        public bool IsResizing => _activeResizeEdge != ResizeEdge.None;

        #region Edge Detection
        private ResizeEdge DetectResizeEdgeAtMouse()
        {
            if (_panel == null || !_panel.Visible) return ResizeEdge.None;

            Vector2 mouse = GetViewport().GetMousePosition();
            Rect2 rect = _panel.GetGlobalRect();

            // Mouse must be near the panel (within edge zone on both sides)
            Rect2 outer = rect.Grow(RESIZE_EDGE_SIZE);
            if (!outer.HasPoint(mouse)) return ResizeEdge.None;

            Rect2 inner = rect.Grow(-RESIZE_EDGE_SIZE);
            // If mouse is inside the inner area, not on an edge
            if (inner.HasPoint(mouse)) return ResizeEdge.None;

            bool onLeft   = mouse.X < rect.Position.X + RESIZE_EDGE_SIZE;
            bool onRight  = mouse.X > rect.End.X   - RESIZE_EDGE_SIZE;
            bool onTop    = mouse.Y < rect.Position.Y + RESIZE_EDGE_SIZE;
            bool onBottom = mouse.Y > rect.End.Y   - RESIZE_EDGE_SIZE;

            if (onTop    && onLeft)  return ResizeEdge.TopLeft;
            if (onTop    && onRight) return ResizeEdge.TopRight;
            if (onBottom && onLeft)  return ResizeEdge.BottomLeft;
            if (onBottom && onRight) return ResizeEdge.BottomRight;
            if (onLeft)   return ResizeEdge.Left;
            if (onRight)  return ResizeEdge.Right;
            if (onTop)    return ResizeEdge.Top;
            if (onBottom) return ResizeEdge.Bottom;

            return ResizeEdge.None;
        }
        #endregion

        #region Cursor Management
        private void ProcessResizeCursor()
        {
            if (_panel == null || !_panel.Visible)
            {
                ResetCursor();
                return;
            }

            // During active resize, keep showing the resize cursor
            if (_activeResizeEdge != ResizeEdge.None)
            {
                _panel.MouseDefaultCursorShape = EdgeToControlCursor(_activeResizeEdge);
                _resizeCursorActive = true;
                return;
            }

            ResizeEdge edge = DetectResizeEdgeAtMouse();
            if (edge != ResizeEdge.None)
            {
                _panel.MouseDefaultCursorShape = EdgeToControlCursor(edge);
                _resizeCursorActive = true;
            }
            else if (_resizeCursorActive)
            {
                ResetCursor();
            }
        }

        private void ResetCursor()
        {
            if (_panel != null)
                _panel.MouseDefaultCursorShape = Control.CursorShape.Arrow;
            _resizeCursorActive = false;
        }

        private static Control.CursorShape EdgeToControlCursor(ResizeEdge edge)
        {
            return edge switch
            {
                ResizeEdge.Left or ResizeEdge.Right
                    => Control.CursorShape.Hsize,
                ResizeEdge.Top or ResizeEdge.Bottom
                    => Control.CursorShape.Vsize,
                ResizeEdge.TopLeft or ResizeEdge.BottomRight
                    => Control.CursorShape.Fdiagsize,
                ResizeEdge.TopRight or ResizeEdge.BottomLeft
                    => Control.CursorShape.Bdiagsize,
                _ => Control.CursorShape.Arrow,
            };
        }
        #endregion

        #region Resize Input Handling
        /// <summary>
        /// 处理调整大小的输入事件。返回 true 表示事件已被消费。
        /// </summary>
        private bool HandleResizeInput(InputEvent @event)
        {
            if (_panel == null || !_panel.Visible) return false;

            // --- Not currently resizing: check for start ---
            if (_activeResizeEdge == ResizeEdge.None)
            {
                if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
                {
                    ResizeEdge edge = DetectResizeEdgeAtMouse();
                    if (edge != ResizeEdge.None && !DraggablePanel.IsAnyDragging)
                    {
                        StartResize(edge, mb.GlobalPosition);
                        return true;
                    }
                }
                return false;
            }

            // --- Currently resizing ---
            if (@event is InputEventMouseMotion motion)
            {
                ApplyResize(motion.GlobalPosition);
                return true;
            }

            if (@event is InputEventMouseButton mbUp && !mbUp.Pressed && mbUp.ButtonIndex == MouseButton.Left)
            {
                EndResize();
                return true;
            }

            return true; // Block all other input during resize
        }

        private void StartResize(ResizeEdge edge, Vector2 mousePos)
        {
            _activeResizeEdge = edge;
            DraggablePanel.IsAnyDragging = true;
            _resizeDragStart = mousePos;
            _resizeStartOL = _panel.OffsetLeft;
            _resizeStartOT = _panel.OffsetTop;
            _resizeStartOR = _panel.OffsetRight;
            _resizeStartOB = _panel.OffsetBottom;
        }

        private void ApplyResize(Vector2 currentMouse)
        {
            Vector2 delta = currentMouse - _resizeDragStart;

            float oL = _resizeStartOL;
            float oT = _resizeStartOT;
            float oR = _resizeStartOR;
            float oB = _resizeStartOB;

            ResizeEdge e = _activeResizeEdge;
            if (e == ResizeEdge.Left   || e == ResizeEdge.TopLeft  || e == ResizeEdge.BottomLeft)  oL += delta.X;
            if (e == ResizeEdge.Right  || e == ResizeEdge.TopRight || e == ResizeEdge.BottomRight) oR += delta.X;
            if (e == ResizeEdge.Top    || e == ResizeEdge.TopLeft  || e == ResizeEdge.TopRight)    oT += delta.Y;
            if (e == ResizeEdge.Bottom || e == ResizeEdge.BottomLeft || e == ResizeEdge.BottomRight) oB += delta.Y;

            // Clamp minimum size
            if (oR - oL < MIN_PANEL_WIDTH)
            {
                if (e == ResizeEdge.Left || e == ResizeEdge.TopLeft || e == ResizeEdge.BottomLeft)
                    oL = oR - MIN_PANEL_WIDTH;
                else
                    oR = oL + MIN_PANEL_WIDTH;
            }
            if (oB - oT < MIN_PANEL_HEIGHT)
            {
                if (e == ResizeEdge.Top || e == ResizeEdge.TopLeft || e == ResizeEdge.TopRight)
                    oT = oB - MIN_PANEL_HEIGHT;
                else
                    oB = oT + MIN_PANEL_HEIGHT;
            }

            _panel.OffsetLeft   = oL;
            _panel.OffsetTop    = oT;
            _panel.OffsetRight  = oR;
            _panel.OffsetBottom = oB;
        }

        private void EndResize()
        {
            _activeResizeEdge = ResizeEdge.None;
            DraggablePanel.IsAnyDragging = false;
            ResetCursor();
        }
        #endregion
    }
}
