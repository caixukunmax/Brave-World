# 根治方案：消除跨 CanvasLayer 拖拽冲突

## 根因

Godot 中每个 CanvasLayer 有独立的 GUI 系统，`GuiGetHoveredControl()` 只在本层内查找。
DebugPanel 在 layer=110 的独立 CanvasLayer，IntegratedPanel 在 layer=100 的 UICanvas，
两者重叠时都认为鼠标在自己上方，导致同时拖拽/resize。

当前的补丁方案（`IsTopCanvasLayerAtMouse` 遮挡检测）是打地鼠——每个交互入口都要加，
漏一处就出 bug。

## 根治方案

**把 DebugPanel 移入 UICanvas**，通过 PanelManager 的 z-order（MoveChild）保证置顶。
所有可交互面板在同一 GUI 上下文中，`GuiGetHoveredControl()` 天然正确，不需要任何遮挡补丁。

## 修改步骤

### 1. 修改 DebugPanel.Nodes.cs — 不再创建独立 CanvasLayer

- 删除 `_debugCanvas` 字段
- `_outerControl` 直接作为面板根容器（不再需要 CanvasLayer 包裹）
- `BuildPanel()` 返回的节点直接是 `_outerControl`（PanelContainer）

### 2. 修改 DebugPanel.cs — 适配新结构

- 删除 `_debugCanvas` 相关代码（创建、layer 设置、Visible 控制）
- `_outerControl.Visible` 替代 `_debugCanvas.Visible`
- 拖拽/resize 逻辑不变（`_outerControl.Position` 仍然有效）
- 删除 `mouseOverSelf` 补丁检查（不再需要，同一 GUI 上下文）
- 删除 `skip_drag` 标签

### 3. 修改 main.tscn — DebugPanel 节点移入 UICanvas

- 删除场景中的 `DebugPanel` 节点（独立 CanvasLayer）
- 在 UICanvas 下添加 DebugPanel 节点

### 4. 修改 PanelManager.cs — DebugPanel 也纳入 z-order 管理

- `RequestFocus` 对 DebugPanel 也生效（BringToFront）
- DebugPanel toggle 时自动置顶

### 5. 删除 DraggablePanel.cs 中的遮挡补丁

- 删除 `IsTopCanvasLayerAtMouse()` 和 `HasInteractiveControlAt()` 方法
- 删除 `_Input` 和 `_Process` 中的遮挡检测代码
- 恢复简洁的原始逻辑

### 6. 验证

- 编译通过
- DebugPanel 拖拽不影响其他面板
- DebugPanel resize 不影响其他面板
- DebugPanel 始终渲染在其他面板之上（通过 z-order）
- 其他面板拖拽/resize 正常
