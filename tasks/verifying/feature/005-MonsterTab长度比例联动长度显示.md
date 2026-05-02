# 005-MonsterTab长度比例联动长度显示

- **类型**: feature
- **状态**: verifying
- **优先级**: P2
- **模块**: 调试面板-MonsterTab
- **创建**: 2026-05-02
- **完成**: -
- **阻塞**: 无
- **关联文件**: DebugPanelMonsterTab.cs
- **修复提交**: -

## 描述

MonsterTab 调节长度比例时，应同步显示计算后的长度值（GridSize × Scale），类似 PlayerTab 的做法。

## 当前状态

- MonsterTab 血条/MP条只有"长度比例"滑条，没有"长度"显示
- PlayerTab 有"长度"滑条 + "长度比例"滑条，双向联动
- MonsterTab 注释写了"Length 是计算值，只读显示"但实际没实现

## 方案

1. 在血条和MP条的"长度比例"行上方，各加一行"长度"只读标签显示
2. 长度比例滑条变化时，计算 `GridSize × Scale` 并更新长度标签
3. SyncMonsterDebugUI 时同步更新长度标签

## 验证

- 编译 0 错误
- 调节长度比例时，长度值实时更新
- 切换配置时，长度值正确显示
