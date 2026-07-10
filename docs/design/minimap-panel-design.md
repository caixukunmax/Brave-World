# 地图小窗（Minimap）与大地图（BigMap）设计文档

## 1. 目标

在屏幕右上角提供一个**常驻**的地图小窗，玩家点击小窗可打开一个更大的大地图面板。

显示内容：
- 自己在当前地图中的位置（绿色圆点）
- 当前地图的边界与可行走/障碍区域概览
- 周围怪物（红色）、NPC（蓝色）位置标记
- 主相机当前视野范围框（黄色细线，仅小窗）
- 预留特殊标记接口：任务目标、传送点、队友等（当前未开发，后续接入）

## 2. 整体方案

拆成两个独立 UI 组件：

| 组件 | 类型 | 位置 | 可见性 | 打开方式 |
|------|------|------|--------|----------|
| **Minimap** | `CanvasLayer` + `Control` | 屏幕右上角，锚定 | 进游戏常驻 | 无 |
| **BigMap** | `DraggablePanel` 子类 | 屏幕中央，可拖拽 | 点击小窗打开 | 点击小窗 / 功能按钮栏“地图”按钮 |

不把小窗做成 `DraggablePanel` 的原因：用户明确要求“进游戏就常驻右上角”，HUD 形式最自然；如果做成可拖拽面板，初始位置难以保证始终贴边，且会被其他面板遮挡/跟随焦点置顶，破坏“常驻角落”的体验。

大地图则需要能拖拽移动、关闭，所以继承 `DraggablePanel`。

## 3. 复用渲染逻辑

新增一个可复用的 `MapViewControl`：
- 输入：目标 `Control` 尺寸、地图数据、标记过滤器（是否显示怪物/NPC/相机框）。
- 输出：在目标 `Control` 的 `_Draw()` 中完成绘制。
- 内部：
  1. 监听 `GridManager.CurrentMapName` 变化，变化时重新生成地形缩略图 `ImageTexture`。
  2. 在 `_Draw()` 中：
     - 绘制地形缩略图（按地图边界等比缩放适配控件）。
     - 绘制玩家点、怪物点、NPC 点。
     - （小窗）绘制相机视野框。
     - 预留 `IMapMarker` 接口供未来特殊标记接入。

这样 `MinimapHud` 和 `BigMapPanel` 都只需挂载一个 `MapViewControl`，通过配置区分细节。

## 4. 数据结构

- 玩家位置：`Player.GridPos` / `Player.Position`
- 地图数据：`GridManager.GridData`、`MapBounds`、`GridSize`、`CurrentMapName`
- 怪物：`MonsterManager.GetMonsters()`
- NPC：需要给 `NpcManager` 增加 `GetNpcs()` 公共方法（当前 `_npcs` 私有且无访问器）
- 相机：`CameraController` 的 `Position` 和 `Zoom`

## 5. UI 结构

### Minimap（常驻小窗）
```
MinimapCanvas (CanvasLayer, layer = 75)
  MinimapHud (Control, script = MinimapHud.cs)
    Background (PanelContainer，暗色半透明背景)
    MapView (Control, script = MapViewControl.cs)
    TitleLabel (Label，显示地图名，可选)
```
- 初始尺寸：`240 x 180`
- 初始位置：屏幕右上角，距右/上边缘各 `16 px`
- 点击整个小窗区域打开大地图
- 不显示标题栏/关闭按钮，保持简洁

### BigMap（大地图面板）
```
big_map_panel.tscn (BigMapPanel)
  PanelContainer
    VBoxContainer
      TitleBar
        [标题 Label：大地图 - 地图名]
        Spacer
        CloseButton
      Content (Control)
        MapView (Control, script = MapViewControl.cs)
```
- 初始尺寸：`700 x 500`
- 初始位置：屏幕居中
- 支持拖拽、resize、关闭

## 6. 交互

| 操作 | 行为 |
|------|------|
| 点击右上角小窗任意位置 | 打开大地图面板 |
| 点击功能按钮栏“地图”按钮 | 切换大地图面板显示/隐藏 |
| 大地图面板关闭按钮 | 隐藏大地图 |
| 大地图面板拖拽/resize | 使用 `DraggablePanel` 默认行为 |

大地图当前版本**不实现点击移动**（需求未要求），仅作查看。

## 7. 需要修改/新增的文件

| 类型 | 路径 | 说明 |
|------|------|------|
| 新增 | `clinetcsharp/Scripts/MapViewControl.cs` | 可复用地图绘制控件 |
| 新增 | `clinetcsharp/Scripts/MinimapHud.cs` | 右上角常驻小窗逻辑 |
| 新增 | `clinetcsharp/Scripts/BigMapPanel.cs` | 大地图可拖拽面板 |
| 新增 | `clinetcsharp/scenes/minimap_hud.tscn` | 小窗场景 |
| 新增 | `clinetcsharp/scenes/big_map_panel.tscn` | 大地图场景 |
| 修改 | `clinetcsharp/scenes/main.tscn` | 添加 MinimapCanvas，给 PanelManager 赋值 BigMapPanelScene |
| 修改 | `clinetcsharp/Scripts/PanelManager.cs` | 注册 `BigMapPanelScene` |
| 修改 | `clinetcsharp/Scripts/FunctionButtonBar.cs` | 将“地图”按钮改为切换大地图 |
| 修改 | `clinetcsharp/Scripts/NpcManager.cs` | 增加 `GetNpcs()` 公共方法 |

## 8. 性能与边界

- 地形缩略图在地图切换时生成一次，分辨率按地图长边限制在 `256 px`（小窗）和 `512 px`（大地图）。
- 标记点每帧刷新仅触发 `QueueRedraw`，绘制开销很小。
- 怪物/NPC 过多时只绘制距离玩家一定范围内的点（半径 30 格，可配置）。
- `GridManager` 没有地图加载事件，通过每帧检测 `CurrentMapName` 变化来触发缩略图重建。

## 9. 用户确认结论

1. 进游戏常驻右上角 ✅
2. 需要特殊标记但当前未开发，预留接口 ✅
3. 点击小窗打开大地图，大地图也需开发 ✅
4. 默认尺寸 240×180 可先这样 ✅
