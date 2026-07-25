using System.Collections.Generic;
using UnityClientSharp.Entity;
using UnityClientSharp.Map.Core;
using UnityClientSharp.Map.Rendering;
using UnityEngine;

namespace BraveWorld.Editor.MapEditing
{
    /// <summary>
    /// 地图编辑操作层：放/移/删建筑（footprint 三查，移植 Godot MapEditor.Decoration）、
    /// 批量应用刷子、开辟地图。操作成功即入撤销栈（CellEditCommand），调用方负责刷新。
    /// </summary>
    public static class MapEditOps
    {
        /// <summary>装饰占地尺寸（EntityProfile appearance.SizeX/Y，缺省 1x1）。</summary>
        public static (int sx, int sy) GetDecoSize(int profileId)
        {
            var app = EntityProfileManager.GetProfile(profileId)?.GetData<AppearanceData>("appearance");
            return (Mathf.Max(1, app?.SizeX ?? 1), Mathf.Max(1, app?.SizeY ?? 1));
        }

        /// <summary>footprint 包含的所有格子（锚点为左上角）。</summary>
        public static List<Vector2Int> Footprint(Vector2Int anchor, int sx, int sy)
        {
            var cells = new List<Vector2Int>(sx * sy);
            for (int dy = 0; dy < Mathf.Max(1, sy); dy++)
                for (int dx = 0; dx < Mathf.Max(1, sx); dx++)
                    cells.Add(new Vector2Int(anchor.x + dx, anchor.y + dy));
            return cells;
        }

        /// <summary>目标 footprint 是否与任意已有建筑占地重叠（多格建筑只在锚点记 DecorationType，必须按 footprint 查）。</summary>
        public static bool IsFootprintOverlapping(GridManager grid, Vector2Int anchor, int sx, int sy, Vector2Int? excludeAnchor)
        {
            var target = new HashSet<Vector2Int>(Footprint(anchor, sx, sy));
            foreach (var cell in grid.GridData.Values)
            {
                if (cell.DecorationType == 0) continue;
                if (excludeAnchor.HasValue && cell.Pos == excludeAnchor.Value) continue;
                var (osx, osy) = GetDecoSize(cell.DecorationType);
                foreach (var pos in Footprint(cell.Pos, osx, osy))
                    if (target.Contains(pos)) return true;
            }
            return false;
        }

        /// <summary>放置合法性三查：越界 / 占用 / 与其他建筑 footprint 重叠。返回 null = 合法。</summary>
        public static string ValidatePlacement(GridManager grid, Vector2Int anchor, int sx, int sy, Vector2Int? excludeAnchor)
        {
            foreach (var pos in Footprint(anchor, sx, sy))
                if (!grid.IsInBounds(pos)) return "目标位置超出地图范围";

            HashSet<Vector2Int> exclude = null;
            if (excludeAnchor.HasValue)
                exclude = new HashSet<Vector2Int>(Footprint(excludeAnchor.Value, sx, sy));

            foreach (var pos in Footprint(anchor, sx, sy))
            {
                if (exclude != null && exclude.Contains(pos)) continue;
                var cell = grid.GetCell(pos);
                if (cell != null && cell.DecorationType != 0) return "目标位置已有装饰";
            }
            if (IsFootprintOverlapping(grid, anchor, sx, sy, excludeAnchor))
                return "目标位置与其他建筑占地重叠";
            return null;
        }

        /// <summary>从 palette 放置建筑。</summary>
        public static bool TryPlace(GridManager grid, Vector2Int anchor, int type, out string error)
        {
            error = null;
            if (type >= 1 && type <= 3) { error = "不能放置玩家/怪物/NPC Profile"; return false; }
            var (sx, sy) = GetDecoSize(type);
            error = ValidatePlacement(grid, anchor, sx, sy, null);
            if (error != null) return false;

            var cell = grid.GetCell(anchor);
            var cmd = new CellEditCommand();
            cmd.Changes.Add((anchor, cell.TerrainType, cell.TerrainType, cell.DecorationType, type));
            MapEditUndoStack.Push(cmd);
            cell.DecorationType = type;
            return true;
        }

        /// <summary>移动已放置建筑（src 清零 + dst 落子，一条命令）。原地放置返回 false 不记撤销。</summary>
        public static bool TryMove(GridManager grid, Vector2Int srcAnchor, Vector2Int dstAnchor, int type, out string error)
        {
            error = null;
            if (srcAnchor == dstAnchor) return false;
            var (sx, sy) = GetDecoSize(type);
            error = ValidatePlacement(grid, dstAnchor, sx, sy, srcAnchor);
            if (error != null) return false;

            var srcCell = grid.GetCell(srcAnchor);
            var dstCell = grid.GetCell(dstAnchor);
            if (srcCell == null || dstCell == null) { error = "格子不存在"; return false; }

            var cmd = new CellEditCommand();
            cmd.Changes.Add((srcAnchor, srcCell.TerrainType, srcCell.TerrainType, srcCell.DecorationType, 0));
            cmd.Changes.Add((dstAnchor, dstCell.TerrainType, dstCell.TerrainType, dstCell.DecorationType, type));
            MapEditUndoStack.Push(cmd);
            srcCell.DecorationType = 0;
            dstCell.DecorationType = type;
            return true;
        }

        /// <summary>删除悬停位置所属建筑（footprint 命中，支持多格）。</summary>
        public static bool TryDelete(GridManager grid, MapDecorationManager decoMgr, Vector2Int hoverPos)
        {
            var dec = decoMgr.GetDecorationAt(hoverPos);
            if (dec == null) return false;
            var anchor = new Vector2Int(dec.GridX, dec.GridY);
            var cell = grid.GetCell(anchor);
            if (cell == null || cell.DecorationType == 0) return false;

            var cmd = new CellEditCommand();
            cmd.Changes.Add((anchor, cell.TerrainType, cell.TerrainType, cell.DecorationType, 0));
            MapEditUndoStack.Push(cmd);
            cell.DecorationType = 0;
            return true;
        }

        /// <summary>把当前地形装饰刷子应用到选区（界外格自动跳过），一条命令。返回实际修改格数。</summary>
        public static int ApplyPaintToSelection(GridManager grid, HashSet<Vector2Int> selection, int decoId)
        {
            var cmd = new CellEditCommand();
            foreach (var pos in selection)
            {
                var cell = grid.GetCell(pos);
                if (cell == null || cell.DecorationType == decoId) continue;
                cmd.Changes.Add((pos, cell.TerrainType, cell.TerrainType, cell.DecorationType, decoId));
            }
            if (cmd.Changes.Count == 0) return 0;

            foreach (var (pos, _, _, _, newD) in cmd.Changes)
                grid.GetCell(pos).DecorationType = newD;
            MapEditUndoStack.Push(cmd);
            return cmd.Changes.Count;
        }

        /// <summary>开辟地图：把选区内的界外格并入 GridData（快照式撤销）。返回新增格数。</summary>
        public static int ExtendBySelection(GridManager grid, HashSet<Vector2Int> selection)
        {
            var toAdd = new List<Vector2Int>();
            foreach (var pos in selection)
                if (!grid.IsInBounds(pos)) toAdd.Add(pos);
            if (toAdd.Count == 0) return 0;

            var cmd = new ExtendMapEditCommand
            {
                OldGridData = ExtendMapEditCommand.DeepCopy(grid.GridData),
            };
            int added = grid.ExtendMap(toAdd);
            if (added > 0)
            {
                cmd.NewGridData = ExtendMapEditCommand.DeepCopy(grid.GridData);
                MapEditUndoStack.Push(cmd);
            }
            return added;
        }
    }
}
