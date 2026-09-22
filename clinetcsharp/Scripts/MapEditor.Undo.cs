using Godot;
using System.Collections.Generic;
using System.Linq;

namespace ClinetCSharp
{
    public partial class MapEditor : Node2D
    {
        // ============ 撤销系统 ============

        /// <summary>
        /// 创建撤销命令 — 记录当前选中格子中将被修改的格子及其旧地形值。
        /// 必须在修改格子之前调用。
        /// </summary>
        private TerrainEditCommand CreateUndoCommand(int newTerrainType)
        {
            var cmd = new TerrainEditCommand { NewTerrainType = newTerrainType };
            if (GridManager == null) return cmd;

            foreach (var pos in SelectedCells.Keys)
            {
                if (!GridManager.IsInBounds(pos)) continue;
                var cell = GridManager.GetCell(pos);
                if (cell == null) continue;
                // 跳过没有实际变化的格子，避免撤销栈被无意义命令占满
                if (cell.TerrainType == newTerrainType) continue;
                cmd.Changes.Add((pos, cell.TerrainType));
            }
            return cmd;
        }

        private void Undo()
        {
            if (_undoStack.Count == 0 || GridManager == null)
            {
                GD.Print("[MapEditor] 没有可撤销的操作");
                return;
            }

            var cmd = _undoStack[_undoStack.Count - 1];
            _undoStack.RemoveAt(_undoStack.Count - 1);

            GD.Print($"[MapEditor.Undo] 撤销命令类型={cmd.GetType().Name}, 剩余undo={_undoStack.Count}");
            cmd.Undo(GridManager);
            if (cmd is not SelectionEditCommand)
                SelectedCells.Clear();
            QueueRedraw();

            _redoStack.Add(cmd);
            if (_redoStack.Count > MaxUndoSteps)
                _redoStack.RemoveAt(0);

            GD.Print("[MapEditor] 撤销操作完成");
        }

        private void Redo()
        {
            if (_redoStack.Count == 0 || GridManager == null)
            {
                GD.Print("[MapEditor] 没有可重做的操作");
                return;
            }

            var cmd = _redoStack[_redoStack.Count - 1];
            _redoStack.RemoveAt(_redoStack.Count - 1);

            GD.Print($"[MapEditor.Redo] 重做命令类型={cmd.GetType().Name}");
            cmd.Redo(GridManager);
            if (cmd is not SelectionEditCommand)
                SelectedCells.Clear();
            QueueRedraw();

            _undoStack.Add(cmd);
            if (_undoStack.Count > MaxUndoSteps)
                _undoStack.RemoveAt(0);

            GD.Print("[MapEditor] 重做操作完成");
        }
    }
}
