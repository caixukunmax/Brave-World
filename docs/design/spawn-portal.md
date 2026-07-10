# 出生点建筑与共享传送门设计

> 版本：v1.0  
> 状态：设计中  
> 依赖：[map-decoration-placement.md](./map-decoration-placement.md)、[extend-map.md](./extend-map.md)

---

## 1. 需求概述

### 1.1 出生点建筑

- 出生点是一个 1×1 建筑，**仅在地图编辑器中可见**，游戏运行时隐身。
- 它标记玩家登录、死亡复活、切图落地的目标位置。
- **出生点建筑的位置即为实际出生点来源**。服务端 `MapDataProvider.GetSpawnPoint()` 会优先扫描地图中的 `60000` 出生点建筑，找不到时才回退到 `map_registry.json` 的 `spawn` 字段。
- 登录、死亡复活、切图落地、`return` GM 命令都会回到出生点建筑所在格子。
- 不阻塞移动。

### 1.2 共享传送门

- 共享传送门是一个 1×1 建筑，游戏内和编辑器内均可见。
- 玩家**靠近**传送门时自动弹出地图选择菜单。
- 菜单列出所有已注册地图（排除当前地图）。
- 选择目标地图后，玩家被传送到该地图的 `spawn` 坐标。
- 当前只有一张地图时，菜单仍可显示，但仅作占位。
- 不阻塞移动。

---

## 2. 关键设计决策

### 2.1 建筑类型与 ID

沿用现有建筑类型区间规则：

| 建筑 | BuildingType 常量 | 配置 ID (build_cfg_id) | 占地 | 阻塞移动 |
|---|---|---|---|---|
| 出生点 | `SpawnPoint = 6` | `60000` | 1×1 | false |
| 共享传送门 | `Portal = 7` | `70000` | 1×1 | false |

### 2.2 出生点可见性

- 游戏运行时：`MapDecorationManager.SpawnDecorations(...)` 遇到 `DecorationType == 60000` 时直接跳过，不创建实例，因此完全隐身。
- 地图编辑器：`_editDecorationManager.SpawnDecorations(...)` 正常创建，使用醒目的视觉样式（例如青色光圈/石碑）。
- 配置文件 `debug_panel_config.cfg` 中不保留 `60000` 的持久化 section，避免被用户误改；每次启动由 `EntityProfileManager.EnsureDefaultProfiles()` 重建。

### 2.3 传送门交互

- 触发条件：玩家进入传送门周围 8 邻域（距离 ≤ 1 格）时，弹出地图选择菜单。
- 菜单样式：参考 `NpcManager.ShowInteractMenu(...)` 的 VBoxContainer + PanelContainer + Button 风格，附加在传送门实体上。
- 地图列表来源：
  - 客户端启动时从 `res://maps/` 扫描所有地图，读取 `map.json` 的 `display_name`。
  - 排除当前所在地图。
- 选择后：构造 `Game.ChangeMapRequest { TargetMap = targetMapName }` 发送给服务端。
- 服务端沿用现有 `ChangeMapHandler`，目标坐标取目标地图的 `spawn` 字段。

### 2.4 地图生成

- `scripts/regenerate_luoyexiang_village.js` 生成城镇后：
  - 在 `mapData.spawn` 坐标放置 `60000` 出生点建筑（仅编辑器可见，不影响游戏）。
  - 在出生点旁边找一个空位放置 `70000` 共享传送门（例如 `(52,50)`）。
- 出生点建筑的位置决定实际出生点；`mapData.spawn` 字段与之保持一致，作为冗余和兼容性数据。

---

## 3. 数据模型

### 3.1 客户端默认 Profile

在 `EntityProfileManager.EnsureDefaultProfiles()` 中新增：

```csharp
// 60000 = 出生点（编辑器专用标记）
_profiles[BuildingType.GetConfigBaseId(BuildingType.SpawnPoint)] = EntityProfile.CreateDecorationDefault(
    BuildingType.GetConfigBaseId(BuildingType.SpawnPoint), "SpawnPoint", "出生点", BuildingType.SpawnPoint,
    new Color(0.2f, 0.8f, 0.9f, 0.9f),
    new Color(0.1f, 0.5f, 0.6f), false, sizeX: 1, sizeY: 1);

// 70000 = 共享传送门
_profiles[BuildingType.GetConfigBaseId(BuildingType.Portal)] = EntityProfile.CreateDecorationDefault(
    BuildingType.GetConfigBaseId(BuildingType.Portal), "Portal", "共享传送门", BuildingType.Portal,
    new Color(0.6f, 0.2f, 0.9f, 0.9f),
    new Color(0.4f, 0.1f, 0.7f), false, sizeX: 1, sizeY: 1);
```

### 3.2 服务端建筑配置

`servercsharp/data/buildings.json` 新增：

```json
{
  "60000": { "sizeX": 1, "sizeY": 1, "blockMovement": false, "name": "spawn_point" },
  "70000": { "sizeX": 1, "sizeY": 1, "blockMovement": false, "name": "portal" }
}
```

### 3.3 建筑类型常量

`clinetcsharp/Scripts/BuildingType.cs`：

```csharp
public const int SpawnPoint = 6;
public const int Portal = 7;
public static bool IsValid(int type) => type is House or Shop or Well or Farm or Tavern or SpawnPoint or Portal;
```

---

## 4. 客户端实现

### 4.1 新增/修改文件

| 文件 | 修改点 |
|---|---|
| `clinetcsharp/Scripts/BuildingType.cs` | 新增 `SpawnPoint`、`Portal` 常量 |
| `clinetcsharp/Scripts/EntityProfileManager.cs` | 新增 `60000`、`70000` 默认 Profile |
| `clinetcsharp/Scripts/MapDecorationManager.cs` | 游戏运行时跳过 `60000` 出生点；为 `70000` 传送门附加交互组件 |
| `clinetcsharp/Scripts/MapDecoration.cs` | 支持 `PortalInteractionComponent` 或内嵌传送门菜单逻辑 |
| `clinetcsharp/Scripts/MapEditor.cs` | 编辑器模式下正常生成出生点和传送门；建筑列表显示新类型 |
| `clinetcsharp/Scripts/GridManager.cs` | 编辑器模式下 `IsWalkable` 对出生点/传送门返回 true |

### 4.2 传送门菜单实现

在 `MapDecoration` 中检测建筑类型：

```csharp
if (BuildingType.GetTypeFromConfigId(ProfileId) == BuildingType.Portal)
{
    // 每帧或按距离检测玩家，进入范围则弹出菜单
}
```

菜单 UI 参考 `NpcManager.ShowInteractMenu(...)`，选项为地图名列表。点击后：

```csharp
NetworkManager.Instance?.SendPacket(MessageId.GameChangeMapReq,
    new Game.ChangeMapRequest { TargetMap = mapName });
```

### 4.3 编辑器可见性

`MapDecorationManager.SpawnDecorations(...)` 中：

```csharp
if (!SpawnEditable && profileId == BuildingType.GetConfigBaseId(BuildingType.SpawnPoint))
    continue; // 游戏运行时跳过出生点
```

---

## 5. 服务端实现

服务端无需新增 handler，复用 `ChangeMapHandler`：

- 收到 `ChangeMapRequest` 后，读取 `targetMap`。
- 通过 `MapDataProvider.GetSpawnPoint(targetMap)` 获取目标地图 `spawn` 坐标。
- 调用现有 `PlayerEnter` 流程。

唯一调整：若请求目标就是当前地图，服务端可直接返回成功但不做切换（或视为无操作）。

---

## 6. 地图生成器修改

`scripts/regenerate_luoyexiang_village.js`：

1. 放置城镇后，`mapData.spawn` 已确定。
2. 在 `mapData.spawn` 坐标写入 `decoration: 60000`。
3. 在出生点旁边找一个可行走空位写入 `decoration: 70000`。
4. 保存并同步。

---

## 7. 风险与缓解

| 风险 | 说明 | 缓解 |
|---|---|---|
| 出生点建筑被误删 | 编辑器中删除后，地图上无可见标记，但 `spawn` 字段仍生效 | 在编辑器 UI 中突出显示出生点的重要性；删除时给出提示 |
| 传送门菜单遮挡 | 多地图时菜单可能很长 | 菜单使用滚动容器；当前只有一张图，暂时无影响 |
| 玩家站在传送门格被阻塞 | 传送门 `blockMovement=false`，不会阻塞 | 服务端和客户端配置保持一致 |
| 地图扫描失败 | 客户端读取 `res://maps/` 可能为空 | 失败时菜单显示“无可用地图” |

---

## 8. 验收标准

- [ ] 落叶乡地图的 `spawn` 坐标上有一个 `60000` 出生点建筑（编辑器可见，游戏不可见）。
- [ ] 落叶乡地图的出生点旁边有一个 `70000` 共享传送门（编辑器和游戏均可见）。
- [ ] 玩家靠近传送门时弹出地图选择菜单。
- [ ] 选择目标地图后，服务端将玩家传送到该地图的 `spawn` 坐标。
- [ ] 登录、死亡复活仍然使用 `map.json` 的 `spawn` 字段，不受出生点建筑显隐影响。
- [ ] 服务端 `dotnet test` 和客户端 `dotnet build` 均通过。
