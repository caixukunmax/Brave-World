using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ClinetCSharp
{
    public partial class MapEditor : Node2D
    {
        // ============ 文件操作 ============

        private void OnSavePressed()
        {
            if (GridManager == null)
                return;

            var mapName = GridManager.CurrentMapName;
            var confirmDialog = new ConfirmationDialog();
            confirmDialog.Title = "保存地图";
            confirmDialog.DialogText = $"确定要保存地图 '{mapName}' 吗？\n这将覆盖现有的地图文件。";
            confirmDialog.Confirmed += () =>
            {
                var err = GridManager.SaveCurrentMap();
                if (err == Error.Ok)
                {
                    GD.Print($"[MapEditor] 地图 '{mapName}' 保存成功");
                    ShowToast($"地图 '{mapName}' 保存成功!");
                }
                else
                {
                    GD.PushError($"[MapEditor] 地图 '{mapName}' 保存失败: " + err);
                    ShowToast($"保存失败: {err}", Colors.Red);
                }
                confirmDialog.QueueFree();
            };
            confirmDialog.Canceled += () => confirmDialog.QueueFree();
            AddChild(confirmDialog);
            confirmDialog.PopupCentered();
        }

        private void OnExportPressed()
        {
            var dialog = new FileDialog();
            dialog.FileMode = FileDialog.FileModeEnum.SaveFile;
            dialog.Access = FileDialog.AccessEnum.Filesystem;
            dialog.Filters = new[] { "*.json" };
            dialog.CurrentFile = GridManager.CurrentMapName + ".json";
            dialog.FileSelected += (path) =>
            {
                DoExport(path);
                dialog.QueueFree();
            };
            dialog.Canceled += () => dialog.QueueFree();
            AddChild(dialog);
            dialog.PopupCentered(new Vector2I(800, 600));
        }

        private void DoExport(string path)
        {
            if (GridManager == null)
                return;
            var err = GridManager.ExportJson(path);
            if (err == Error.Ok)
                GD.Print("[MapEditor] 导出成功: " + path);
            else
                GD.PushError("[MapEditor] 导出失败: " + err);
        }

        private void OnImportPressed()
        {
            var dialog = new FileDialog();
            dialog.FileMode = FileDialog.FileModeEnum.OpenFile;
            dialog.Access = FileDialog.AccessEnum.Filesystem;
            dialog.Filters = new[] { "*.json" };
            dialog.FileSelected += (path) =>
            {
                DoImport(path);
                dialog.QueueFree();
            };
            dialog.Canceled += () => dialog.QueueFree();
            AddChild(dialog);
            dialog.PopupCentered(new Vector2I(800, 600));
        }

        private void DoImport(string path)
        {
            if (GridManager == null)
                return;
            var err = GridManager.ImportJson(path);
            if (err == Error.Ok)
                GD.Print("[MapEditor] 导入成功: " + path);
            else
                GD.PushError("[MapEditor] 导入失败: " + err);
        }

        // ============ 地图管理 ============

        private void RefreshMapList()
        {
            var mapOption = _editorPanel?.GetNodeOrNull<OptionButton>("VBoxContainer/MapSelectHbox/MapOption");
            if (mapOption == null) return;

            mapOption.Clear();
            var maps = MapDataManager.GetMapList();
            maps.Sort();

            int selectedIndex = 0;
            for (int i = 0; i < maps.Count; i++)
            {
                mapOption.AddItem(maps[i], i);
                if (GridManager != null && maps[i] == GridManager.CurrentMapName)
                    selectedIndex = i;
            }

            if (maps.Count > 0)
                mapOption.Select(selectedIndex);
        }

        private void OnSwitchMap()
        {
            DoSwitchToSelectedMap();
        }

        private void OnMapOptionSelected(long index)
        {
            DoSwitchToSelectedMap();
        }

        private void DoSwitchToSelectedMap()
        {
            var mapOption = _editorPanel?.GetNodeOrNull<OptionButton>("VBoxContainer/MapSelectHbox/MapOption");
            if (mapOption == null || GridManager == null) return;

            var selectedName = mapOption.GetItemText(mapOption.Selected);
            if (string.IsNullOrEmpty(selectedName) || selectedName == GridManager.CurrentMapName)
                return;

            SwitchToMap(selectedName);
        }

        private void SwitchToMap(string mapName)
        {
            if (GridManager == null) return;

            // 诊断：检查游戏 GridManager 是否被意外显示
            GD.Print($"[MapEditor] SwitchToMap: 请求切换 to '{mapName}', 当前='{GridManager.CurrentMapName}'");
            GD.Print($"[MapEditor] SwitchToMap: _gameGridManager.Visible={_gameGridManager?.Visible}, _editGridManager.Visible={_editGridManager?.Visible}");

            // 诊断：检查场景中所有 GridManager 实例
            var allGrids = GetTree()?.GetNodesInGroup("grid_manager");
            GD.Print($"[MapEditor] SwitchToMap: 场景中 grid_manager 数量={allGrids?.Count ?? 0}");
            if (allGrids != null)
            {
                foreach (var n in allGrids)
                {
                    if (n is GridManager gm)
                        GD.Print($"[MapEditor] SwitchToMap:   GridManager '{gm.Name}' Visible={gm.Visible} Map={gm.CurrentMapName} Pos={gm.GlobalPosition}");
                }
            }

            if (!GridManager.LoadMap(mapName))
            {
                ShowToast($"加载地图 '{mapName}' 失败", Colors.Red);
                GD.PrintErr($"[MapEditor] SwitchToMap: LoadMap('{mapName}') 返回 false, 未切换");
                return;
            }
            GD.Print($"[MapEditor] SwitchToMap: LoadMap 成功, GridData={GridManager.MapWidth}x{GridManager.MapHeight}");

            // 将相机移动到新地图中心，确保新地图在视野内
            if (Camera != null && GridManager != null)
            {
                var bounds = GridManager.MapBounds;
                var mapCenter = GridManager.GlobalPosition +
                                new Vector2(
                                    (bounds.Position.X + bounds.Size.X / 2.0f) * GridManager.GridSize,
                                    (bounds.Position.Y + bounds.Size.Y / 2.0f) * GridManager.GridSize);
                Camera.GlobalPosition = mapCenter;
                GD.Print($"[MapEditor] SwitchToMap: 相机移动到新地图中心 {mapCenter}");
            }

            // 清空编辑状态
            SelectedCells.Clear();
            IsSelecting = false;
            _undoStack.Clear();
            _redoStack.Clear();
            _selectionHistory.Clear();

            // 更新面板显示
            var title = _editorPanel?.GetNodeOrNull<Label>("VBoxContainer/EditorTitle");
            if (title != null)
                title.Text = $"🗺️ 地图编辑器 - {GridManager.CurrentMapName}";

            // 同步下拉栏选中项
            var mapOption = _editorPanel?.GetNodeOrNull<OptionButton>("VBoxContainer/MapSelectHbox/MapOption");
            if (mapOption != null)
            {
                for (int i = 0; i < mapOption.ItemCount; i++)
                {
                    if (mapOption.GetItemText(i) == GridManager.CurrentMapName)
                    {
                        mapOption.Select(i);
                        break;
                    }
                }
            }

            QueueRedraw();
            ShowToast($"已切换到地图 '{mapName}'");
            GD.Print($"[MapEditor] 切换到地图: {mapName}");
        }

        private void OnCreateNewMapClicked()
        {
            ShowCreateMapDialog();
        }

        private void ShowCreateMapDialog()
        {
            var dialog = new AcceptDialog();
            dialog.Title = "新建地图";
            dialog.Size = new Vector2I(350, 200);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 8);

            // 名称
            var nameRow = new HBoxContainer();
            var nameLabel = new Label { Text = "地图名称:", CustomMinimumSize = new Vector2(80, 0) };
            var nameEdit = new LineEdit { Name = "NameEdit", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            nameRow.AddChild(nameLabel);
            nameRow.AddChild(nameEdit);
            vbox.AddChild(nameRow);

            // 宽度
            var widthRow = new HBoxContainer();
            var widthLabel = new Label { Text = "宽度:", CustomMinimumSize = new Vector2(80, 0) };
            var widthSpin = new SpinBox { Name = "WidthSpin", MinValue = 10, MaxValue = 200, Value = 50, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            widthRow.AddChild(widthLabel);
            widthRow.AddChild(widthSpin);
            vbox.AddChild(widthRow);

            // 高度
            var heightRow = new HBoxContainer();
            var heightLabel = new Label { Text = "高度:", CustomMinimumSize = new Vector2(80, 0) };
            var heightSpin = new SpinBox { Name = "HeightSpin", MinValue = 10, MaxValue = 200, Value = 50, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            heightRow.AddChild(heightLabel);
            heightRow.AddChild(heightSpin);
            vbox.AddChild(heightRow);

            dialog.AddChild(vbox);

            dialog.Confirmed += () =>
            {
                var name = nameEdit.Text.StripEdges();
                if (string.IsNullOrEmpty(name))
                {
                    ShowToast("地图名称不能为空", Colors.Red);
                    return;
                }
                if (Regex.IsMatch(name, @"[\\/:*?""<>|]"))
                {
                    ShowToast("地图名称包含非法字符", Colors.Red);
                    return;
                }
                if (MapDataManager.MapExists(name))
                {
                    ShowToast($"地图 '{name}' 已存在", Colors.Red);
                    return;
                }

                int width = (int)widthSpin.Value;
                int height = (int)heightSpin.Value;

                var err = MapDataManager.CreateNewMap(name, width, height);
                if (err == Error.Ok)
                {
                    ShowToast($"地图 '{name}' 创建成功");
                    RefreshMapList();
                    SwitchToMap(name);
                }
                else
                {
                    ShowToast($"创建失败: {err}", Colors.Red);
                }

                dialog.QueueFree();
            };

            dialog.Canceled += () => dialog.QueueFree();

            AddChild(dialog);
            dialog.PopupCentered();
        }

        private void OnDeleteMapClicked()
        {
            var mapOption = _editorPanel?.GetNodeOrNull<OptionButton>("VBoxContainer/MapSelectHbox/MapOption");
            if (mapOption == null) return;

            var selectedName = mapOption.GetItemText(mapOption.Selected);
            if (string.IsNullOrEmpty(selectedName))
                return;

            var maps = MapDataManager.GetMapList();
            if (maps.Count <= 1)
            {
                ShowToast("至少保留一张地图", Colors.Red);
                return;
            }

            var confirm = new ConfirmationDialog();
            confirm.Title = "删除地图";
            confirm.DialogText = $"确定要删除地图 '{selectedName}' 吗？\n此操作不可撤销！";
            confirm.Confirmed += () =>
            {
                var err = MapDataManager.DeleteMap(selectedName);
                if (err == Error.Ok)
                {
                    ShowToast($"地图 '{selectedName}' 已删除");
                    RefreshMapList();

                    // 若删除的是当前地图，切换到第一个可用地图
                    if (GridManager != null && selectedName == GridManager.CurrentMapName)
                    {
                        var remaining = MapDataManager.GetMapList();
                        if (remaining.Count > 0)
                            SwitchToMap(remaining[0]);
                    }
                }
                else
                {
                    ShowToast($"删除失败: {err}", Colors.Red);
                }
                confirm.QueueFree();
            };
            confirm.Canceled += () => confirm.QueueFree();
            AddChild(confirm);
            confirm.PopupCentered();
        }

        private void OnRenameMapClicked()
        {
            var mapOption = _editorPanel?.GetNodeOrNull<OptionButton>("VBoxContainer/MapSelectHbox/MapOption");
            if (mapOption == null) return;

            var selectedName = mapOption.GetItemText(mapOption.Selected);
            if (string.IsNullOrEmpty(selectedName))
                return;

            ShowRenameMapDialog(selectedName);
        }

        private void ShowRenameMapDialog(string oldName)
        {
            var dialog = new AcceptDialog();
            dialog.Title = "重命名地图";
            dialog.Size = new Vector2I(350, 150);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 8);

            var oldRow = new HBoxContainer();
            oldRow.AddChild(new Label { Text = "原名称:", CustomMinimumSize = new Vector2(80, 0) });
            oldRow.AddChild(new Label { Text = oldName, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
            vbox.AddChild(oldRow);

            var newRow = new HBoxContainer();
            newRow.AddChild(new Label { Text = "新名称:", CustomMinimumSize = new Vector2(80, 0) });
            var newEdit = new LineEdit { Name = "NewNameEdit", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            newRow.AddChild(newEdit);
            vbox.AddChild(newRow);

            dialog.AddChild(vbox);

            dialog.Confirmed += () =>
            {
                var newName = newEdit.Text.StripEdges();
                if (string.IsNullOrEmpty(newName))
                {
                    ShowToast("新名称不能为空", Colors.Red);
                    return;
                }
                if (Regex.IsMatch(newName, @"[\\/:*?""<>|]"))
                {
                    ShowToast("名称包含非法字符", Colors.Red);
                    return;
                }
                if (MapDataManager.MapExists(newName))
                {
                    ShowToast($"地图 '{newName}' 已存在", Colors.Red);
                    return;
                }

                var err = MapDataManager.RenameMap(oldName, newName);
                if (err == Error.Ok)
                {
                    ShowToast($"重命名成功: {oldName} -> {newName}");
                    // 若重命名的是当前地图，同步更新 CurrentMapName
                    if (GridManager != null && oldName == GridManager.CurrentMapName)
                    {
                        GridManager.CurrentMapName = newName;
                    }
                    RefreshMapList();
                }
                else
                {
                    ShowToast($"重命名失败: {err}", Colors.Red);
                }
                dialog.QueueFree();
            };

            dialog.Canceled += () => dialog.QueueFree();

            AddChild(dialog);
            dialog.PopupCentered();
        }
    }
}
