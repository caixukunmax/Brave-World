# 调试面板架构对齐方案

> 状态：已实现
> 日期：2026-04-29
> 范围：`clinetcsharp/` 客户端调试面板与通用面板体系对齐

## 1. 背景

当前客户端已经形成两套面板实现路径：

1. 通用面板体系：`DraggablePanel` + `PanelManager`
2. 调试面板专用体系：`DebugPanel` 自己维护拖拽、缩放、焦点、节点查找和部分动态 UI 创建

这导致调试面板虽然功能丰富，但在架构上已经偏离客户端现有的通用面板模型。继续在现状上叠加功能，维护成本会持续升高。

## 2. 当前现状

### 2.1 结构现状

- `DebugPanel` 当前根节点是 `Control`，实际交互对象是内部的 `Control/Panel`
- `DebugPanel` 自己实现了拖拽、缩放、焦点状态和可见性切换
- `DebugPanel` 同时又接入了 `PanelManager` 的 `IPanel` 注册链
- 其他调试/业务面板，如 `GMPanel`、`IntegratedPanel`，已经直接继承 `DraggablePanel`

### 2.2 已确认问题

1. **拖拽/缩放逻辑重复**
   - `DebugPanel.cs` 和 `DebugPanel.Resize.cs` 维护自己的拖拽/缩放逻辑
   - `DraggablePanel.cs` 已提供同类能力

2. **节点路径脆弱**
   - `DebugPanel.cs` 通过硬编码路径查找 `地图/玩家/怪物/NPC/系统/UI` tab 容器
   - 场景改名或节点层级调整时容易直接失效

3. **场景与代码职责混杂**
   - `debug_panel.tscn` 只提供基本外壳和 tab 容器
   - 底部预设区由 `DebugPanel.DynamicUI.cs` 在运行时生成

4. **状态来源分散**
   - `_isPanelVisible`、`_isPanelFocused`、`_isDragging`、`IsResizing` 等状态与通用面板体系并存
   - 输入拦截依赖 `DraggablePanel.IsAnyDragging` 全局标志，但自身并不继承 `DraggablePanel`

5. **配置兼容性存在隐藏耦合**
   - `MonsterManager` 与 `NpcManager` 会直接读取 `user://debug_panel_config.cfg`
   - `DebugPanel` 当前几何保存使用 `panel_geo.offset_*`
   - 若架构调整不考虑兼容，容易破坏怪物/NPC 样式加载

### 2.3 文档现状

- `clinetcsharp/docs/调试面板说明.md` 仍主要描述“地图 / 玩家”两页结构
- 实际代码已经扩展为 `地图 / 玩家 / 怪物 / NPC / 系统 / UI` 六个 tab

## 3. 本次目标

本轮只做“架构对齐”，不做大规模功能扩张。

目标如下：

1. 让 `DebugPanel` 接入 `DraggablePanel` 统一面板体系
2. 删除调试面板自维护的拖拽/缩放实现
3. 降低节点查找对硬编码路径的依赖
4. 统一面板显示、焦点、关闭、最小化的行为模型
5. 保持现有 tab 功能、配置文件和快捷键习惯尽量不变

## 4. 非目标

本轮明确不做以下事项：

1. 不重写 `PlayerTab`、`MapTab`、`MonsterTab` 的所有业务逻辑
2. 不在本轮拆分所有大文件
3. 不在本轮做怪物/NPC 全量刷新策略优化
4. 不改变调试项含义、配置字段名和预设存储语义
5. 不顺带统一所有客户端 UI 风格

## 5. 方案对比

### 方案 A：保持 `DebugPanel` 现状，只抽取部分公共逻辑

优点：

- 改动小
- 短期风险低

缺点：

- 仍然保留两套面板体系
- 不能真正消除重复拖拽/缩放逻辑
- 后续继续演进时仍会分叉

### 方案 B：让 `DebugPanel` 继承 `DraggablePanel`，按通用面板结构重建场景骨架

优点：

- 真正接入统一面板体系
- 可删除 `DebugPanel.Resize.cs` 和部分焦点/显示状态逻辑
- 与 `GMPanel`、`IntegratedPanel` 保持一致

缺点：

- 需要调整 `debug_panel.tscn` 结构
- 需要处理旧配置与新几何结构的兼容

## 6. 选型

采用 **方案 B**。

原因：

1. 当前重复逻辑已经足够明显，继续修补旧结构性价比低
2. 仓库里已有成熟的 `DraggablePanel` 模式，可以直接对齐
3. 对齐后再做性能和拆分优化，边界会更清楚

## 7. 目标架构

### 7.1 继承关系

`DebugPanel` 从：

- `Control, IPanel`

调整为：

- `DraggablePanel`

这样拖拽、缩放、最小化、关闭、焦点、快捷键注册都统一交给基类与 `PanelManager`。

### 7.2 场景骨架

`debug_panel.tscn` 调整为与通用面板一致的骨架：

```text
DebugPanel (PanelContainer, script=DebugPanel)
└── VBoxContainer
    ├── TitleBar
    │   └── HBoxContainer
    │       ├── MinimizeButton
    │       ├── TitleLabel
    │       ├── Spacer
    │       └── CloseButton
    └── Content
        └── Margin/VBoxContainer
            ├── ScrollContainer
            │   └── TabContainer
            └── PresetFooter
```

说明：

1. tab 页内容保留现有结构，不在本轮重设计
2. 预设区从代码动态创建改为场景静态骨架 + 脚本绑定
3. 原有“顶部 36px 可拖拽区”的语义改为显式 `TitleBar`

### 7.3 脚本职责划分

#### `DebugPanel.cs`

保留职责：

- tab 实例创建
- tab 生命周期协调
- 配置文件 orchestration
- F12 切换快捷键

删除职责：

- 自定义拖拽逻辑
- 自定义缩放逻辑
- 自定义焦点状态维护

#### `DraggablePanel`

继续承担：

- 标题栏拖拽
- 边缘缩放
- 最小化 / 关闭
- 与 `PanelManager` 的注册与焦点联动

#### `DebugPanel.DynamicUI.cs`

目标状态：

- 不再负责运行时拼装整块底部预设 UI
- 若仍保留，只负责极小量辅助节点初始化

#### `DebugPanel.Resize.cs`

目标状态：

- 删除

### 7.4 节点引用策略

从当前的硬编码路径切换到更稳定的方式，优先级如下：

1. 场景唯一名节点（推荐）
2. 固定字段引用集中在 `DebugPanel.Nodes.cs`
3. 避免在 `DebugPanel.cs` 中直接写完整路径字符串

目标是让 tab 容器和底部预设区的查找集中在一个文件中，而不是散落在初始化逻辑里。

### 7.5 状态模型

以下状态应尽量收口到通用体系：

- 可见性：直接使用 `Visible` / `Toggle()` / `ShowPanel()` / `HidePanel()`
- 焦点：通过 `PanelManager.RequestFocus()` 与基类焦点通知处理
- 拖拽中：仅使用 `DraggablePanel` 内部状态
- 缩放中：仅使用 `DraggablePanel` 内部状态

`DebugPanel` 不再额外维护 `_isPanelVisible`、`_isPanelFocused` 这类与基类语义重复的状态。

## 8. 配置兼容策略

这是本次最需要谨慎处理的部分。

### 8.1 兼容要求

必须继续兼容：

- `user://debug_panel_config.cfg`
- `monster_*` / `npc_*` 样式 section
- 现有预设与基础调试配置

### 8.2 几何兼容

当前几何保存依赖 `panel_geo.offset_*`，而拖拽位置实际上由 `_outerControl.Position` 承担。

架构对齐后需要统一几何真相源，并处理以下问题：

1. 新结构下应该由面板根节点保存位置与尺寸
2. 旧配置读取时要兼容 `panel_geo.offset_*`
3. 若发现旧结构没有有效位置数据，允许回退到默认位置

### 8.3 风险点

`MonsterManager` 与 `NpcManager` 会直接读取同一个配置文件，因此不能随意改 section 名或删除既有字段。

## 9. 分阶段实施建议

### Phase 1：场景与继承对齐

目标：让 `DebugPanel` 继承 `DraggablePanel`，并使用统一场景骨架。

包含：

- 改 `DebugPanel` 继承关系
- 调整 `debug_panel.tscn`
- 接入标题栏、最小化、关闭按钮
- 用基类接管拖拽/缩放

### Phase 2：节点引用与动态 UI 收口

目标：减少路径脆弱性。

包含：

- 将 tab 容器引用集中到 `DebugPanel.Nodes.cs`
- 底部预设 UI 静态化
- 精简 `DebugPanel.DynamicUI.cs`

### Phase 3：状态与生命周期清理

目标：消除重复状态。

包含：

- 删除 `_isPanelVisible`、`_isPanelFocused` 等冗余状态
- 把开关、焦点、显示行为统一到基类与 `PanelManager`
- 审查各 tab 的 `ConnectSignals/DisconnectSignals` 约定

### Phase 4：文档同步

目标：让文档回到与实现一致。

包含：

- 更新 `clinetcsharp/docs/调试面板说明.md`
- 补充当前六个 tab 的职责说明
- 记录调试面板已接入通用面板体系

## 10. 影响范围

预计会涉及以下文件：

- `clinetcsharp/scenes/debug_panel.tscn`
- `clinetcsharp/Scripts/DebugPanel.cs`
- `clinetcsharp/Scripts/DebugPanel.Nodes.cs`
- `clinetcsharp/Scripts/DebugPanel.DynamicUI.cs`
- `clinetcsharp/Scripts/DebugPanel.Config.cs`
- `clinetcsharp/Scripts/DebugPanel.Resize.cs`
- `clinetcsharp/Scripts/PanelManager.cs`（如需最小适配）
- `clinetcsharp/docs/调试面板说明.md`

## 11. 验证方式

本轮方案落地后，至少需要验证：

1. `F12` 仍能切换调试面板
2. 调试面板可拖拽、可缩放、可最小化、可关闭
3. 与 `GMPanel`、`IntegratedPanel` 同时存在时，焦点和层级行为一致
4. tab UI 正常初始化，不因节点路径调整而丢失控件
5. 旧 `debug_panel_config.cfg` 不会导致怪物/NPC 样式加载失败
6. `main.tscn` 与 `role_select_scene.tscn` 两个场景里的调试面板都能正常工作

## 12. 验收标准

满足以下条件视为本轮“架构对齐”完成：

1. `DebugPanel` 已进入 `DraggablePanel` 体系
2. `DebugPanel.Resize.cs` 被删除或不再承担运行职责
3. 调试面板不再维护与基类重复的拖拽/焦点状态
4. tab 节点引用不再散落在主初始化逻辑的硬编码路径中
5. 现有功能和配置兼容性基本保持不变

## 13. 后续事项

本轮完成后，再进入下一层优化更合适：

1. 怪物/NPC 页的全量刷新节流与分级应用
2. `DebugPanelPlayerTab`、`DebugPanelMapTab` 的进一步拆分
3. 调试面板说明文档的功能与架构双线同步

当前顺序不建议反过来做。若先拆 tab 或先做性能修补，而不先统一面板体系，后续会重复返工。