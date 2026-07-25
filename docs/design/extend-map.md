# 地图编辑器：框选扩展地图（不规则地图方案）

## 需求

在地图编辑器中，允许用户通过「刷地形」工具拖拽框选当前地图外部区域，然后点击「开辟地图」按钮，将选区并入当前地图。要求：

1. **只新增框选的格子**，不强制填充中间空白。
2. 新增格子默认填充普通地形（`terrain=0`）。
3. 支持向任意方向扩展，包括产生负坐标。
4. 支持撤销 / 重做。
5. 新增区域视觉上与地图外部一致（统一灰色）。
6. **完全抛弃旧 CSV 格式**，新格式具备良好的可扩展性。

## 核心设计

### 地图存储改为稀疏字典

`GridManager` 的核心存储从 `List<List<GridCell>>` 改为：

```csharp
public System.Collections.Generic.Dictionary<Vector2I, GridCell> GridData { get; set; } = new();
```

- 只有真正存在的格子才会被加入字典。
- 每个 `GridCell` 的 `Pos` 和 `Uid` 基于其实际逻辑坐标。

### 地图边界 `MapBounds`

使用 `Rect2I MapBounds` 表示所有存在格子的轴对齐包围盒：

- 用于渲染尺寸、相机居中、JSON 保存范围。
- 扩展 / 删除格子后重新计算：`MapBounds = 包含所有存在格子的最小矩形`。
- 当没有任何格子时，使用默认 `Rect2I(0, 0, 50, 50)`。
- `MapWidth`、`MapHeight`、`GridOrigin` 作为兼容属性，实际映射到 `MapBounds.Size / Position`，均为只读。

### 坐标转换

- `GridToWorld(gridPos)`：`(gridPos * GridSize) + (GridSize / 2)`，不再依赖 `MapBounds`。
- `WorldToGrid(worldPos)`：`FloorToInt(worldPos / GridSize)`。
- `IsInBounds(gridPos)`：`GridData.ContainsKey(gridPos)`。
- `GetCell(gridPos)`：从字典获取，不存在返回 `null`。

### 渲染

`GridShaderOverlay` 继续绘制矩形，但矩形起点和尺寸基于 `MapBounds`：

- `GridShaderOverlay` 的 `Position` 设置为 `MapBounds.Position * GridSize`。
- `map_size_world = MapBounds.Size * GridSize`。
- `terrain_mask` 纹理尺寸 = `MapBounds.Size`。
- mask 中存在的格子按地形渲染；不存在的格子按 `outside_map_color` 渲染。
- `Background` 的 `Position` / `Size` 也同步为 `MapBounds` 的世界范围，保证负坐标区域被背景覆盖。

### 扩展算法

1. 遍历框选区域 `selectionBounds` 内每个坐标。
2. 若坐标不在 `GridData` 中，创建 `GridCell` 并加入字典，`TerrainType = 0`。
3. 重新计算 `MapBounds`。
4. 保存 `map.json`。
5. 刷新 shader mask、重绘。

### 撤销 / 重做

`ExtendMapCommand` 深拷贝扩展前后的完整 `GridData` 字典。撤销 / 重做时恢复字典，并重新计算 `MapBounds`、保存 `map.json`。

### 保存 / 加载（v2 JSON 格式）

完全抛弃旧的 `map.csv` 与 `config.cfg`，统一使用 `maps/<name>/map.json`：

```json
{
  "version": 2,
  "display_name": "新手村",
  "bounds": { "x": 0, "y": 0, "w": 50, "h": 50 },
  "spawn": { "x": 25, "y": 25 },
  "cells": {
    "0_0": { "terrain": 0, "height": 0, "custom": "" },
    "-1_5": { "terrain": 0, "height": 0, "custom": "" }
  }
}
```

设计要点：

- `version`：版本号，未来可通过升级脚本迁移到 v3/v4。
- `display_name`：地图显示名称。
- `bounds`：地图包围盒，支持负坐标。
- `spawn`：默认出生点。
- `cells`：稀疏存储，键为 `"x_y"`（支持负坐标），值为格子数据对象。
- 每个 cell 是独立对象，未来可扩展字段（如 `flags`、`layer`、`tags` 等）而不破坏旧读取逻辑。
- 不存在的坐标直接不出现在 `cells` 中，不再使用 `terrain = -1` 标记。

#### 加载规则

- 只识别 `version >= 2` 的 `map.json`。
- 若 `map.json` 不存在，视为新地图，创建默认 50x50 全普通地形。
- `bounds` 优先以 JSON 中声明为准；若缺失则从 `cells` 重新计算。
- 不再兼容旧 `map.csv` / `config.cfg`。

#### 同步规则

- `tables/scripts/sync-maps.ts` 负责把 `tables/datas/maps/<name>/map.json` 同步到：
  - 服务端：`servercsharp/data/maps/<name>/map.json`
  - 客户端：`clinetcsharp/maps/<name>/map.json`
- 同时生成 `servercsharp/data/map_registry.json`，字段从 `map.json` 的 `display_name`、`bounds`、`spawn` 提取。

#### 历史地图迁移

- 一次性迁移脚本：`tables/scripts/migrate-maps-to-json.ts`
- 已把 `tables/datas/maps/` 下所有旧 `map.csv` + `config.cfg` 迁移为 `map.json`。
- 旧 CSV 和 config.cfg 已从 `tables/datas/maps`、`clinetcsharp/maps`、`servercsharp/data/maps` 中删除。

## 影响文件

- `docs/design/extend-map.md`
- `clinetcsharp/Scripts/GridManager.cs`
- `clinetcsharp/Scripts/GridShaderOverlay.cs`
- `clinetcsharp/Scripts/MapDataManager.cs`
- `clinetcsharp/Scripts/MapEditor.cs`
- `clinetcsharp/Scripts/PreviewMap.cs`
- `servercsharp/src/GameServer.Common/Config/MapDataProvider.cs`
- `servercsharp/src/GameServer.Tests/CombatChaseBehaviorTests.cs`
- `tables/scripts/sync-maps.ts`
- `tables/scripts/migrate-maps-to-json.ts`
- `clinetcsharp/assets/shaders/grid_overlay.gdshader`

## 已知限制 / 后续工作

- 删除格子（`Delete` 键）当前仍把格子设为墙（`terrain=9`）；后续如需"从地图中移除格子"可再增加功能。
- 未来若 cell 字段扩展，需在 `GridCell` 类、客户端 JSON 序列化/反序列化、以及服务端 `MapDataProvider` 中同步支持。
