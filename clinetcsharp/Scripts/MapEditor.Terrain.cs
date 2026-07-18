using Godot;
using System.Collections.Generic;
using System.Linq;

namespace ClinetCSharp
{
    public partial class MapEditor : Node2D
    {
        // 地形绘制诊断日志开关
        private const bool EnableDiagnostics = false;

        // ============ 属性应用 ============

        private void ApplyToSelection()
        {
            if (SelectedCells.Count == 0 || GridManager == null)
                return;

            // 额外诊断：对比 PaintTerrainDecoration 与下拉栏当前实际选中的 id
            var terrainOption = _editorPanel?.GetNodeOrNull<OptionButton>("VBoxContainer/ToolContentContainer/TerrainRow/TerrainOption");
            int dropdownSelectedId = -1;
            if (terrainOption != null)
            {
                int selectedIdx = terrainOption.Selected;
                if (selectedIdx >= 0 && selectedIdx < terrainOption.ItemCount)
                    dropdownSelectedId = terrainOption.GetItemId(selectedIdx);
            }
            LogDiagnostic($"[MapEditor.Apply] 准备应用: PaintTerrainDecoration={PaintTerrainDecoration}, dropdownSelectedId={dropdownSelectedId}, selectedCells={SelectedCells.Count}");

            // 防御：禁止把玩家/怪物/NPC Profile 或遗留岩石应用到格子
            if (!IsValidTerrainDecorationId(PaintTerrainDecoration))
            {
                GD.PushError($"[MapEditor.Apply] 拒绝应用非法 decoration id={PaintTerrainDecoration}（玩家/怪物/NPC Profile 或遗留岩石）");
                ShowToast("不能选择玩家/怪物/NPC 作为地形建筑", Colors.Red);
                return;
            }

            // 在修改前记录撤销命令
            var cmd = CreateDecorationUndoCommand(PaintTerrainDecoration);
            if (cmd.Changes.Count == 0) return;

            _undoStack.Add(cmd);
            if (_undoStack.Count > MaxUndoSteps)
                _undoStack.RemoveAt(0);
            _redoStack.Clear(); // 新操作后清空重做栈

            foreach (var (pos, _, _) in cmd.Changes)
            {
                var cell = GridManager.GetCell(pos);
                if (cell == null) continue;
                cell.DecorationType = PaintTerrainDecoration;
                LogDiagnostic($"[MapEditor.Apply] pos=({pos.X},{pos.Y}) 应用地形建筑 decoration={PaintTerrainDecoration}");
            }

            GridManager.NotifyTerrainChanged();
            GridManager.SyncDecorations();
            LogDiagnostic("[MapEditor] 已应用地形建筑到 " + cmd.Changes.Count + " 个格子");
        }

        /// <summary>
        /// 创建装饰撤销命令 — 记录当前选中格子中将被修改的格子及其旧装饰值。
        /// </summary>
        private DecorationEditCommand CreateDecorationUndoCommand(int newDecorationType)
        {
            var cmd = new DecorationEditCommand();
            if (GridManager == null) return cmd;

            foreach (var pos in SelectedCells.Keys)
            {
                if (!GridManager.IsInBounds(pos)) continue;
                var cell = GridManager.GetCell(pos);
                if (cell == null) continue;
                if (cell.DecorationType == newDecorationType) continue;
                cmd.Changes.Add((pos, cell.DecorationType, newDecorationType));
            }
            return cmd;
        }

        // ============ UI回调 ============

        private void OnTerrainDecorationSelected(long index)
        {
            var terrainOption = _editorPanel?.GetNodeOrNull<OptionButton>("VBoxContainer/ToolContentContainer/TerrainRow/TerrainOption");
            if (terrainOption != null)
            {
                int selectedId = terrainOption.GetItemId((int)index);
                // 防御：不允许选择玩家/怪物/NPC 的 Profile ID 或遗留岩石
                if (!IsValidTerrainDecorationId(selectedId))
                {
                    GD.PushError($"[MapEditor] 非法地形建筑选择: id={selectedId} 是玩家/怪物/NPC Profile 或遗留岩石");
                    return;
                }
                PaintTerrainDecoration = selectedId;
                LogDiagnostic($"[MapEditor] 选择地形建筑: index={index}, id={PaintTerrainDecoration}, name={terrainOption.GetItemText((int)index)}");
            }
        }

        /// <summary>
        /// 判断一个 decoration id 是否是合法的地形类建筑。
        /// 只允许 Category == "Terrain" 的配置（树、草地、水、岩石）以及 0（清除）。
        /// </summary>
        private bool IsValidTerrainDecorationId(int id)
        {
            if (id == 0) return true; // 清除选项
            if (id >= 1 && id <= 3) return false; // 玩家/怪物/NPC Profile
            var cfg = DecorationConfigUtil.Get(id);
            return cfg != null && cfg.Id == id && cfg.Category == "Terrain";
        }

        /// <summary>
        /// 条件性输出诊断日志，仅在 EnableDiagnostics 为 true 时打印。
        /// </summary>
        private void LogDiagnostic(string message)
        {
#pragma warning disable CS0162
            if (EnableDiagnostics)
                GD.Print(message);
#pragma warning restore CS0162
        }
    }
}
