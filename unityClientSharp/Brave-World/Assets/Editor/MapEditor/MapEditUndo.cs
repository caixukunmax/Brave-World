using System.Collections.Generic;
using UnityClientSharp.Map.Core;
using UnityClientSharp.Map.Rendering;
using UnityEngine;

namespace BraveWorld.Editor.MapEditing
{
    /// <summary>差分编辑命令（移植 Godot MapEditor.Undo：只记录被改格子的旧值，不全量快照）。</summary>
    public abstract class MapEditCommand
    {
        public abstract void Undo(GridManager grid);
        public abstract void Redo(GridManager grid);
    }

    /// <summary>
    /// 格子属性编辑命令：地形类型/装饰的修改（刷地形、放/移/删建筑通用）。
    /// 一次笔刷 stroke 或一次建筑操作 = 一条命令。
    /// </summary>
    public class CellEditCommand : MapEditCommand
    {
        public readonly List<(Vector2Int Pos, int OldTerrain, int NewTerrain, int OldDeco, int NewDeco)> Changes = new();

        public override void Undo(GridManager grid) => Apply(grid, undo: true);
        public override void Redo(GridManager grid) => Apply(grid, undo: false);

        private void Apply(GridManager grid, bool undo)
        {
            foreach (var (pos, oldT, newT, oldD, newD) in Changes)
            {
                var cell = grid.GetCell(pos);
                if (cell == null) continue;
                cell.TerrainType = undo ? oldT : newT;
                cell.RefreshTerrainConfig();
                cell.DecorationType = undo ? oldD : newD;
            }
        }
    }

    /// <summary>地图扩展命令（快照式，同 Godot ExtendMapCommand：格子集合结构变化，无法差分）。</summary>
    public class ExtendMapEditCommand : MapEditCommand
    {
        public Dictionary<Vector2Int, GridCell> OldGridData;
        public Dictionary<Vector2Int, GridCell> NewGridData;

        public override void Undo(GridManager grid)
        {
            grid.GridData = DeepCopy(OldGridData);
            grid.RecalculateMapBounds();
            grid.NotifyTerrainChanged();
        }

        public override void Redo(GridManager grid)
        {
            grid.GridData = DeepCopy(NewGridData);
            grid.RecalculateMapBounds();
            grid.NotifyTerrainChanged();
        }

        public static Dictionary<Vector2Int, GridCell> DeepCopy(Dictionary<Vector2Int, GridCell> src)
        {
            var copy = new Dictionary<Vector2Int, GridCell>(src.Count);
            foreach (var kvp in src)
            {
                var c = new GridCell(kvp.Key.x, kvp.Key.y);
                kvp.Value.CopyTo(c);
                copy[kvp.Key] = c;
            }
            return copy;
        }
    }

    /// <summary>撤销/重做栈（20 步上限，与 Godot MaxUndoSteps 一致）。</summary>
    public static class MapEditUndoStack
    {
        public const int MaxSteps = 20;

        private static readonly List<MapEditCommand> _undo = new();
        private static readonly List<MapEditCommand> _redo = new();

        public static int UndoCount => _undo.Count;
        public static int RedoCount => _redo.Count;

        public static void Push(MapEditCommand cmd)
        {
            if (cmd == null) return;
            _undo.Add(cmd);
            if (_undo.Count > MaxSteps) _undo.RemoveAt(0);
            _redo.Clear();
        }

        /// <summary>弹出待撤销命令（调用方负责 cmd.Undo(grid) 与刷新）。</summary>
        public static MapEditCommand PopUndo()
        {
            if (_undo.Count == 0) return null;
            var c = _undo[_undo.Count - 1];
            _undo.RemoveAt(_undo.Count - 1);
            _redo.Add(c);
            if (_redo.Count > MaxSteps) _redo.RemoveAt(0);
            return c;
        }

        /// <summary>弹出待重做命令（调用方负责 cmd.Redo(grid) 与刷新）。</summary>
        public static MapEditCommand PopRedo()
        {
            if (_redo.Count == 0) return null;
            var c = _redo[_redo.Count - 1];
            _redo.RemoveAt(_redo.Count - 1);
            _undo.Add(c);
            if (_undo.Count > MaxSteps) _undo.RemoveAt(0);
            return c;
        }

        public static void Clear()
        {
            _undo.Clear();
            _redo.Clear();
        }
    }
}
