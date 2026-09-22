using Godot;
using System.Collections.Generic;

namespace ClinetCSharp
{
    /// <summary>
    /// 选择 / 拖拽预览的绘制层。挂在 SubViewport 内、与 GridManager 同级（Position 默认 0,0），
    /// 因此 GridToWorld 返回的局部坐标即可直接用于绘制。
    /// 装饰本体由真实 MapDecoration 节点渲染（见 MapEditorBottomPanel.RefreshDecorations），
    /// 本层只画选择 / 拖拽预览等编辑器辅助高亮，保证与游戏内效果一致。
    /// </summary>
    [Tool]
    public partial class MapEditDecorationOverlay : Node2D
    {
        public GridManager Grid;
        public MapEditController Controller;

        // 拖拽预览（footprint 高亮）
        public bool ShowDropPreview;
        public Vector2I DropAnchor;
        public int DropSizeX = 1;
        public int DropSizeY = 1;
        public bool DropValid = true;

        public override void _Draw()
        {
            if (Grid == null || Grid.GridData == null) return;
            float cell = Grid.GridSize;

            // 装饰本体由真实 MapDecoration 节点渲染（见 MapEditorBottomPanel.RefreshDecorations），
            // 这里只叠加选择 / 拖拽预览等编辑器辅助层，保证与游戏内效果一致。

            // 选中高亮
            if (Controller != null)
            {
                foreach (var pos in Controller.SelectedCells.Keys)
                {
                    var center = Grid.GridToWorld(pos);
                    var rect = new Rect2(center - new Vector2(cell / 2f, cell / 2f), new Vector2(cell, cell));
                    bool inBounds = Grid.IsInBounds(pos);
                    DrawRect(rect, inBounds ? new Color(1, 0, 0, 0.22f) : new Color(1, 0.5f, 0, 0.35f), true);
                    DrawRect(rect, inBounds ? Colors.Red : new Color(1, 0.6f, 0, 1f), false, 3f);
                }
            }

            // 拖拽预览
            if (ShowDropPreview)
            {
                var center = Grid.GridToWorld(DropAnchor);
                var rect = new Rect2(center - new Vector2(cell / 2f, cell / 2f), new Vector2(DropSizeX * cell, DropSizeY * cell));
                DrawRect(rect, DropValid ? new Color(0, 1, 0, 0.25f) : new Color(1, 0, 0, 0.35f), true);
                DrawRect(rect, DropValid ? new Color(0, 1, 0, 0.85f) : new Color(1, 0, 0, 0.9f), false, 2f);
            }
        }
    }
}
