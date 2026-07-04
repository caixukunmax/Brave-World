# 地图编辑器：摆件/房屋放置功能设计

> 版本：v1.0  
> 状态：已实现  
> 依赖：[extend-map.md](./extend-map.md)、[地图编辑器多地图支持.md](./地图编辑器多地图支持.md)  

---

## 1. 需求概述

在现有地图编辑器中，新增**地图摆件放置**能力。本期先聚焦最简单的情况：

- 每个摆件占 **1×1 格子**。
- 摆件类型暂时只有 **1 种：房舍**。
- 房舍的渲染方式与玩家实体极其接近，可复用现有的 `EntityBase` + `AppearanceComponent` 渲染管线，仅参数和组件不同。
- 编辑时能在网格上放置、删除房舍；退出编辑模式后保存到地图文件并在游戏中可见。
- 后续可能扩展为多格子摆件，但数据格式和接口预留扩展空间。

---

## 2. 关键设计决策

### 2.1 数据存储：每个格子一个 `decoration` 字段

沿用 `extend-map.md` 确立的稀疏 cell 结构，在 `cells["x_y"]` 中新增字段：

```json
{
  "12_25": { "terrain": 0, "height": 0, "custom": "", "decoration": 1 }
}
```

- `decoration = 0` 表示无摆件。
- `decoration = 1` 表示房舍，后续其他摆件依次递增。

**为什么不用全局 `decorations` 列表？**

虽然全局列表更适合未来多格子摆件，但本期需求是严格的 1×1，且现有编辑器、选区、撤销重做都是围绕**格子**建模的。新增 cell 字段改动最小，也能自然支持框选批量放置/删除。未来多格子摆件出现时，再引入顶层 `decorations` 列表，并把 1×1 摆件作为两种表示的兼容子集即可。

### 2.2 渲染：房舍是一个轻量 `EntityBase` 子类

用户明确说“房舍的渲染方式和玩家实体是极其接近的，可以完全复用”。因此：

- 新增 `MapDecoration : EntityBase`。
- 默认挂载 `AppearanceComponent`（DrawOrder = 0），用圆角矩形画房舍主体。
- 用 `LabelGroupComponent` 显示名称“房舍”。
- 通过 `EntityProfileManager` 给 `MapDecoration` 一个默认 profile，便于后续在调试面板调样式。

> 不使用 `Sprite2D` 贴图：当前项目没有 Sprite 动画管线，所有实体都是程序化绘制。先复用现有 `AppearanceComponent`，后续可再做一个 `HouseRoofComponent` 画出屋顶造型。

### 2.3 运行时管理：新增 `MapDecorationManager`

参考 `ChestManager`、`NpcManager` 的管理方式：

- 在 `main.tscn` 中新增 `MapDecorationManager` 节点，挂在 `GridManager` 之后、`Player` 之前（使房舍默认位于玩家身后）。
- 负责从 `map.json` 加载摆件列表、创建/删除 `MapDecoration` 实例、统一控制显隐。
- 在 `MapEditor.EnterEditMode()` 中隐藏游戏内所有摆件（与隐藏怪物/NPC/宝箱保持一致），避免编辑时与编辑预览重叠。

### 2.4 编辑交互：新增“摆件”工具模式

当前编辑器的左键逻辑：

- 无 Ctrl：相机拖拽
- Ctrl+左键：框选/点选格子
- Delete：把选区设为墙（terrain=9）

新增摆件放置需要引入**工具模式切换**，避免和相机拖拽冲突：

- 在编辑器面板增加一个 `OptionButton`：**工具模式**。
  - 选择（默认）
  - 放置房舍
- 选择“放置房舍”后：
  - 左键点击/框选格子直接在该格子放置房舍（不再触发相机拖拽）。
  - Delete 键删除选中格子的摆件。
  - 已放置房舍的格子再次放置无效果（避免误覆盖）。

### 2.5 撤销/重做

新增 `DecorationEditCommand`，结构与 `TerrainEditCommand` 一致：

```csharp
private class DecorationEditCommand : EditCommand
{
    public int NewDecorationType;
    public List<(Vector2I Pos, int OldDecorationType)> Changes = new();
    // Undo/Redo 中恢复/应用 OldDecorationType / NewDecorationType
}
```

### 2.6 服务端：房舍阻塞移动，双端同步

房舍需要**阻塞玩家移动**，因此服务端也必须把 `decoration` 字段纳入移动判定：

- `DecorationConfig` 增加 `BlockMovement` 字段，房舍为 `true`。
- 服务端 `MapDataProvider` 解析 `decoration` 字段，提供 `IsBlockedByDecoration(mapName, x, y)`。
- `WorldState.IsWalkable` / `TryReserveMove` 接入装饰阻塞检查。
- 客户端 `GridManager.IsWalkable` 同步接入，保证本地预测与服务端权威一致。
- 协议 `MapInfoSyncNotify` 中的 `TileInfo` 增加可选 `decoration_type`，使新进玩家能看到带摆件的格子。

> 由于房舍是静态地图数据，服务器启动时从 `map.json` 加载即可，不需要运行时同步放置/删除协议。

---

## 3. 数据模型

### 3.1 `GridCell` 扩展

```csharp
public partial class GridCell : RefCounted
{
    // ... 现有字段

    /// <summary>装饰类型：0=无，1=房舍</summary>
    public int DecorationType { get; set; } = 0;
}
```

同时更新 `ToDict` / `FromDict` / `ToCsvValues` / `FromCsvValues` / `CopyTo`。

### 3.2 地图 JSON（v2 → v3）

```json
{
  "version": 3,
  "display_name": "新手村",
  "bounds": { "x": 0, "y": 0, "w": 50, "h": 50 },
  "spawn": { "x": 25, "y": 25 },
  "cells": {
    "12_25": { "terrain": 0, "height": 0, "custom": "", "decoration": 1 }
  }
}
```

- 提升 `version` 到 `3`，并在 `MapDataManager.ValidateMapJson` 中兼容旧版：读取时若缺少 `decoration` 字段则默认为 `0`。
- 保存时只对 `DecorationType != 0` 的格子写出 `decoration` 字段，保持稀疏。

### 3.3 装饰配置（策划表）

新增 `DecorationConfig` Luban 配置（或先在 `clinetcsharp/data/decoration_config.json` 中硬编码一期数据）：

| Id | Name | DisplayName | BlockMovement | ClientColor |
|---|---|---|---|---|
| 0 | None | 无 | false | - |
| 1 | House | 房舍 | true | #8B5A2B |

后续可扩展 `Width`、`Height`、`PrefabPath`、`SortOrder` 等字段。

---

## 4. 客户端实现

### 4.1 新增文件

| 文件 | 职责 |
|---|---|
| `clinetcsharp/Scripts/MapDecoration.cs` | `MapDecoration : EntityBase` 子类 |
| `clinetcsharp/Scripts/MapDecorationManager.cs` | 摆件的加载、创建、显隐控制 |
| `clinetcsharp/Scripts/DecorationConfigUtil.cs` | 装饰配置加载/查询（一期可内嵌默认数据） |

### 4.2 修改文件

| 文件 | 修改点 |
|---|---|
| `clinetcsharp/Scripts/GridCell.cs` | 新增 `DecorationType` 字段及序列化 |
| `clinetcsharp/Scripts/MapDataManager.cs` | 加载/保存 `decoration` 字段；兼容 v2/v3 |
| `clinetcsharp/Scripts/MapEditor.cs` | 新增摆件工具模式、放置/删除逻辑、`DecorationEditCommand`、编辑器面板 |
| `clinetcsharp/Scripts/GridManager.cs` | 加载后通知 `MapDecorationManager` 生成摆件；`IsWalkable` 接入装饰阻塞检查 |
| `clinetcsharp/Scripts/MapManager.cs` | 切图/进入地图时触发摆件生成；解析 `TileInfo.decoration_type` |
| `clinetcsharp/Scripts/ComponentRegistry.cs` | 注册 `map_decoration` 默认组件 |
| `clinetcsharp/Scripts/EntityProfileManager.cs` | 提供 `MapDecoration` 默认 profile |
| `clinetcsharp/scenes/main.tscn` | 添加 `MapDecorationManager` 节点 |

### 4.3 `MapDecoration` 实现要点

```csharp
public partial class MapDecoration : EntityBase
{
    public int DecorationTypeId { get; private set; }
    public Vector2I GridPos { get; private set; }

    public void Setup(int decorationTypeId, Vector2I gridPos, int gridSize)
    {
        DecorationTypeId = decorationTypeId;
        GridPos = gridPos;
        GridSize = gridSize;

        var cfg = DecorationConfigUtil.Get(decorationTypeId);
        Name = cfg?.DisplayName ?? "MapDecoration";
        BgColor = cfg?.Color ?? Colors.Brown;
        BorderColor = Colors.DarkBrown;

        Position = UiUtils.GridToWorld(gridPos.X, gridPos.Y, gridSize);
        EnsureRenderComponents();
        ProfileId = EntityProfileManager.HouseDefaultProfileId;
        EntityProfileManager.Instance?.ApplyProfile(this, ProfileId);
    }

    protected override void EnsureRenderComponents()
    {
        AddRenderComponent<AppearanceComponent>();
        AddRenderComponent<LabelGroupComponent>();
    }
}
```

### 4.4 `MapDecorationManager` 实现要点

```csharp
public partial class MapDecorationManager : Node
{
    private readonly Dictionary<Vector2I, MapDecoration> _decorations = new();

    public void SpawnDecorations(Dictionary<Vector2I, int> decorationData, int gridSize)
    {
        ClearDecorations();
        foreach (var (pos, typeId) in decorationData)
        {
            var dec = new MapDecoration();
            dec.Setup(typeId, pos, gridSize);
            AddChild(dec);
            _decorations[pos] = dec;
        }
    }

    public void ClearDecorations()
    {
        foreach (var dec in _decorations.Values)
            dec.QueueFree();
        _decorations.Clear();
    }

    public void SetAllDecorationsVisible(bool visible)
    {
        foreach (var dec in _decorations.Values)
            if (dec is CanvasItem ci) ci.Visible = visible;
    }
}
```

### 4.5 编辑器 UI 扩展

在 `MapEditor.CreateEditorPanel()` 的“地形类型”行之后新增：

```
工具模式: [选择 ▼]
装饰类型: [房舍 ▼]
[放置到选中] [删除摆件]
```

- `工具模式` 控制当前编辑器行为：选择 / 放置房舍。
- `装饰类型` 一期只有“房舍”，但保留扩展性。
- 选择“放置房舍”时，左键直接放置；Delete 删除摆件。

### 4.6 编辑器内摆件预览

编辑模式下，摆件由 `MapDecorationManager` 从 `_editGridManager.GridData` 加载并显示，与游戏内保持一致。这样退出编辑模式时只需要销毁 `_editGridManager` 及其子节点即可，无需额外处理预览。

---

## 5. 服务端实现

### 5.1 本期改动

房舍阻塞移动，服务端需要同步感知：

1. **`MapDataProvider` 解析 `decoration` 字段**
   - 在 `MapCellJson` DTO 中增加 `public int decoration { get; set; }`。
   - 加载 `map.json` 时把每个格子的 `decoration` 缓存到 `MapData` 中。

2. **新增 `IsBlockedByDecoration(mapName, x, y)`**
   - 查询该格子的 `decoration` 类型。
   - 通过 `DecorationConfig`（或临时硬编码）判断该类型是否阻塞移动。
   - 房舍（`decoration = 1`）返回 `true`。

3. **`WorldState` / `MoveStartHandler` 接入检查**
   - 在 `IsWalkable` / `TryReserveMove` 中，除了地形和实体占用，再检查 `IsBlockedByDecoration`。
   - 移动请求若目标格是房舍，服务端返回移动失败或 `MoveCollisionNotify`。

4. **协议扩展**
   - `TileInfo` 增加 `int32 decoration_type = 4`。
   - `MapInfoSyncNotify` 下发非零 `decoration_type` 的格子，确保新进客户端看到与服务端一致的地形/摆件状态。

### 5.2 数据同步

`MapDataManager.SaveMapToJson()` 已会把 `map.json` 同时写入：

- `clinetcsharp/maps/<name>/map.json`
- `tables/datas/maps/<name>/map.json`

再由 `tables/scripts/sync-maps.ts` 同步到 `servercsharp/data/maps/<name>/map.json`。服务端重启后自动读取最新的 `decoration` 字段。

### 5.3 未来扩展

- 多格子摆件：引入顶层 `decorations` 数组，服务端阻塞检查需按占用的多个格子分别判断。
- 非阻塞装饰（如花坛、路标）：`BlockMovement = false`，仅客户端渲染，服务端不阻塞。
- 可破坏/可交互摆件：需要运行时状态同步，超出本期范围。

---

## 6. 数据流

```
地图编辑器 (MapEditor)
  ├─ Ctrl+左键 选中格子
  ├─ 工具模式=放置房舍 → 左键放置 → GridCell.DecorationType = 1
  ├─ DecorationEditCommand 入 undo 栈
  └─ 退出编辑模式 → MapDataManager.SaveMapToJson(map.json v3)
        ├─ clinetcsharp/maps/<name>/map.json
        └─ tables/datas/maps/<name>/map.json

游戏运行时
  GridManager.LoadMap(map.json)
    → MapDecorationManager.SpawnDecorations(GridData)
      → 实例化 MapDecoration (EntityBase + AppearanceComponent + LabelGroupComponent)
        → 场景中显示房舍
  移动请求
    → 客户端 GridManager.IsWalkable 检查 decoration 阻塞
    → 服务端 WorldState.IsWalkable / TryReserveMove 检查 IsBlockedByDecoration
      → 不一致时服务端权威回弹/碰撞通知
```

---

## 7. 已确认设计决策

| 问题 | 决策 |
|---|---|
| 1. 房舍是否阻塞移动？ | **阻塞**，服务端和客户端同步判定。 |
| 2. 房舍是否可被玩家点击/交互？ | **可交互**，复用 `EntityBase.CheckEntityClick`，点击可选中房舍（用于后续扩展信息面板）。 |
| 3. 房舍默认视觉样式？ | **棕色圆角矩形 + “房舍”文字**，后续可细化屋顶造型。 |
| 4. 是否单独配置表？ | **一期内嵌默认数据**，类型增多后再走 Luban 表。 |
| 5. 多格子摆件预留？ | **认可 cell 字段 + 未来顶层 `decorations` 数组共存**。 |

---

## 8. 影响文件汇总

| 类别 | 路径 |
|---|---|
| 新增 | `clinetcsharp/Scripts/MapDecoration.cs` |
| 新增 | `clinetcsharp/Scripts/MapDecorationManager.cs` |
| 新增 | `clinetcsharp/Scripts/DecorationConfigUtil.cs` |
| 修改 | `clinetcsharp/Scripts/GridCell.cs` |
| 修改 | `clinetcsharp/Scripts/MapDataManager.cs` |
| 修改 | `clinetcsharp/Scripts/MapEditor.cs` |
| 修改 | `clinetcsharp/Scripts/GridManager.cs` |
| 修改 | `clinetcsharp/Scripts/MapManager.cs` |
| 修改 | `clinetcsharp/Scripts/ComponentRegistry.cs` |
| 修改 | `clinetcsharp/Scripts/EntityProfileManager.cs` |
| 修改 | `clinetcsharp/scenes/main.tscn` |
| 修改 | `servercsharp/src/GameServer.Common/Config/MapDataProvider.cs` |
| 修改 | `servercsharp/src/GameServer.Services/Core/WorldState.cs`（或移动处理相关文件） |
| 修改 | `protocols/proto/game.proto`（`TileInfo` 增加 `decoration_type`） |
| 修改 | `clinetcsharp/Scripts/NetworkManager.Dispatch.Map.cs`（解析 `TileInfo.decoration_type`） |
| 新增文档 | `docs/design/map-decoration-placement.md` |

---

## 9. 风险与缓解

| 风险 | 说明 | 缓解 |
|---|---|---|
| 地图 JSON 版本兼容 | 新增 `decoration` 字段后旧 v2 地图可能无法加载 | 读取时兼容：缺少字段默认 0；保存时升级到 v3 |
| 编辑器输入冲突 | 放置模式与相机拖拽共用左键 | 引入明确的“工具模式”切换 |
| 性能 | 大量摆件实例化 `EntityBase` 可能较重 | 一期 1×1 摆件数量通常较少；如变多可改用 `Sprite2D` 批量绘制 |
| Z 序 | 房舍与玩家/monster 的遮挡关系 | `MapDecorationManager` 放在 `Player` 之前，默认房舍在玩家身后；后续可按 grid Y 动态调整 ZIndex |
| 服务端不同步 | 需要服务端同步解析 decoration 并阻塞移动 | 在 `MapDataProvider` / `WorldState` 中接入检查，并扩展 `TileInfo` 协议 |
