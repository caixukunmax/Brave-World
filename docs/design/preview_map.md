# 预览地图设计文档

## 概述

预览地图（PreviewMap）是模板预览系统的核心组件。设计原则是：**预览地图完全等同于一张真实地图的迷你版本**，确保模板预览效果与主地图 1:1 一致。

## 设计原则

### 1. 完全等同于真实地图

预览地图包含真实地图的所有核心渲染组件：
- **Background**（ColorRect）：与主地图一致的黑色背景
- **GridManager**：复用主地图的网格渲染参数（GridSize、线颜色、线宽、抗锯齿等）
- **Camera2D**：自动计算 zoom 使地图完整显示在预览视口中
- **实体**：根据模板类型创建的玩家/怪物/NPC 预览实体

### 2. 参数共享

不同地图之间完全共享同一套渲染参数：

| 参数类别 | 共享参数 | 差异参数 |
|---------|---------|---------|
| 网格渲染 | GridSize、LineColor、LineWidth、DashLength、GapLength、AutoLineWidth、LineWidthScale、Min/MaxScreenLineWidth、GridAntiAliasSoftness | 无 |
| 地图数据 | 无 | 地图名称、格子数据（Walkable/Visible/TerrainType）、地图大小 |
| 角色 | 无 | 地图上的具体角色（玩家、怪物、NPC） |
| 背景 | 颜色 | 尺寸（随地图大小变化） |

### 3. 预览地图的特殊约束

预览地图虽然是"真实地图"，但有以下特殊约束：

- **无输入处理**：实体 `SetProcessInput(false)`
- **无 AI 逻辑**：实体 `SetProcess(false)`，怪物不巡逻、NPC 不行为
- **无网络同步**：预览实体不参与任何网络通信
- **无物理**：实体 `SetPhysicsProcess(false)`
- **独立视口**：只能在模板预览窗口的 SubViewport 中查看
- **固定尺寸**：默认 5×5 格子的小型地图，足够展示实体和网格效果

## 技术实现

### 类结构

```
EntityProfilePreviewPanel (SubViewportContainer)
└── SubViewport (300×200)
    └── PreviewMap (Node2D)
        ├── Background (ColorRect, 黑色)
        ├── GridManager (复制主地图参数, 5×5 地图)
        │   └── GridShaderOverlay (网格线 shader)
        ├── Camera2D (自动 zoom)
        └── EntityBase (预览实体)
```

### 关键文件

| 文件 | 职责 |
|------|------|
| `PreviewMap.cs` | 预览地图核心类：创建背景、GridManager、相机，管理实体生命周期 |
| `EntityProfilePreviewPanel.cs` | 预览面板 UI：SubViewport 容器，持有 PreviewMap |
| `DebugPanelEntityTab.Profile.cs` | 调试面板实体标签页：创建预览实体，调用 PreviewMap 显示 |

### 相机 zoom 计算

```
zoom = min(viewportWidth / mapWidth, viewportHeight / mapHeight) × 0.9
```

- viewport = SubViewport 尺寸（300×200）
- map = PreviewMapWidth × GridSize（默认 5×111 = 555×555）
- 0.9 系数留出 10% 边距，确保地图完整可见

### 实体位置

实体始终放置在地图中心格子：

```
centerX = PreviewMapWidth / 2  // 2
centerY = PreviewMapHeight / 2 // 2
position = UiUtils.GridToWorld(new Vector2I(2, 2), GridSize)
```

## 与旧实现的对比

| 方面 | 旧实现（直接放实体） | 新实现（PreviewMap） |
|------|---------------------|---------------------|
| 背景 | 纯色 ColorRect（深灰） | 完整 Background + GridManager |
| 网格线 | 无 | 有（和主地图同一套参数） |
| 实体坐标 | 视口中心像素坐标 | 地图格子世界坐标 |
| 相机 | 无 | Camera2D 自动适配 zoom |
| 渲染一致性 | 需要手动对齐 | 天然一致（复用同一套渲染系统） |

## 未来扩展

### 多地图支持

当项目支持多地图时，PreviewMap 自动从当前活跃地图的 GridManager 读取参数：

```csharp
var mainGrid = GetTree()?.GetFirstNodeInGroup("grid_manager") as GridManager;
int gridSize = mainGrid?.GridSize ?? 111;
Color lineColor = mainGrid?.LineColor ?? new Color(0.7f, 0.7f, 0.7f);
// ... 复制所有参数
```

切换地图时，GridManager 的参数会自动变化，PreviewMap 下次打开时会使用新参数。

### 可配置的预览地图大小

当前固定为 5×5。未来可通过配置或 UI 调整：

```csharp
[Export] public int PreviewMapWidth { get; set; } = 5;
[Export] public int PreviewMapHeight { get; set; } = 5;
```

## 约束规则

1. **禁止在 PreviewMap 中添加网络同步逻辑**
2. **禁止在 PreviewMap 中添加 AI 行为**
3. **禁止修改主地图 GridManager 的参数**
4. **PreviewMap 必须使用独立地图名**（`__preview_map__`），避免加载真实地图数据
5. **编辑器插件内嵌预览必须对齐宿主渲染环境**（统一走 `addons/debug_panel_editor/EditorPreviewEnvironment`）：实体/渲染组件与游戏同源只是前提，宿主环境两处偏差必须校准——①像素密度：编辑器显示缩放会把 SubViewport 纹理双线性放大到物理屏（整体发虚），需按 `EditorInterface.GetEditorScale()` 向上取整倍率 N 做 N× 超采样渲染 + `StretchShrink=N` 按 1/N 显示；②主题：SubViewport 内 Control 主题解析会穿过 SubViewport 落到编辑器主题，预览容器须显式赋游戏主题（`ThemeDB.GetProjectTheme() ?? ThemeDB.GetDefaultTheme()`）。
