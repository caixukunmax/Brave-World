using Godot;
using System.Collections.Generic;

namespace ClinetCSharp
{
    /// <summary>
    /// 撤销/重做命令抽象基类（与运行时 MapEditor 原有命令逻辑一致，但只操作 GridManager，
    /// 不再直接调用 SyncDecorations——渲染刷新统一交给 MapEditController.OnVisualChanged，
    /// 以便运行时与编辑器插件各自决定如何刷新装饰显示）。
    /// </summary>
    public abstract class EditCommand
    {
        public abstract void Undo(GridManager grid);
        public abstract void Redo(GridManager grid);
    }

    public class TerrainEditCommand : EditCommand
    {
        public int NewTerrainType;
        public List<(Vector2I Pos, int OldTerrainType)> Changes = new();

        public override void Undo(GridManager grid)
        {
            foreach (var (pos, oldType) in Changes)
            {
                if (!grid.IsInBounds(pos)) continue;
                var cell = grid.GetCell(pos);
                if (cell == null) continue;
                cell.TerrainType = oldType;
                cell.TerrainConfig = TerrainConfigUtil.Get(oldType);
            }
            grid.NotifyTerrainChanged();
        }

        public override void Redo(GridManager grid)
        {
            foreach (var (pos, _) in Changes)
            {
                if (!grid.IsInBounds(pos)) continue;
                var cell = grid.GetCell(pos);
                if (cell == null) continue;
                cell.TerrainType = NewTerrainType;
                cell.TerrainConfig = TerrainConfigUtil.Get(NewTerrainType);
            }
            grid.NotifyTerrainChanged();
        }
    }

    public class DecorationEditCommand : EditCommand
    {
        public List<(Vector2I Pos, int OldDecorationType, int NewDecorationType)> Changes = new();

        public override void Undo(GridManager grid)
        {
            foreach (var (pos, oldType, _) in Changes)
            {
                if (!grid.IsInBounds(pos)) continue;
                var cell = grid.GetCell(pos);
                if (cell == null) continue;
                cell.DecorationType = oldType;
            }
            grid.NotifyTerrainChanged();
        }

        public override void Redo(GridManager grid)
        {
            foreach (var (pos, _, newType) in Changes)
            {
                if (!grid.IsInBounds(pos)) continue;
                var cell = grid.GetCell(pos);
                if (cell == null) continue;
                cell.DecorationType = newType;
            }
            grid.NotifyTerrainChanged();
        }
    }

    public class ExtendMapCommand : EditCommand
    {
        public Dictionary<Vector2I, GridCell> OldGridData = new();
        public Dictionary<Vector2I, GridCell> NewGridData = new();

        public override void Undo(GridManager grid) => ApplySnapshot(grid, OldGridData);
        public override void Redo(GridManager grid) => ApplySnapshot(grid, NewGridData);

        private static void ApplySnapshot(GridManager grid, Dictionary<Vector2I, GridCell> data)
        {
            grid.GridData = data;
            grid.RecalculateMapBounds();
            grid.SaveMapBoundsToConfig();
            grid.SaveCurrentMap();
            grid.UpdateGridShaderOverlay();
            grid.NotifyTerrainChanged();
            grid.SyncBackgroundSize();
            grid.QueueRedraw();
        }
    }

    public class SelectionEditCommand : EditCommand
    {
        public Dictionary<Vector2I, bool> OldSelection = new();
        public Dictionary<Vector2I, bool> NewSelection = new();
        public MapEditController Controller;

        public override void Undo(GridManager grid)
        {
            if (Controller == null) return;
            Controller.SelectedCells = new Dictionary<Vector2I, bool>(OldSelection);
            Controller.TriggerVisualChanged();
        }

        public override void Redo(GridManager grid)
        {
            if (Controller == null) return;
            Controller.SelectedCells = new Dictionary<Vector2I, bool>(NewSelection);
            Controller.TriggerVisualChanged();
        }
    }
}
