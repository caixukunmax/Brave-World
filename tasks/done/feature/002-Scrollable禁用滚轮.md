# 002-Scrollable禁用滚轮

- **类型**: feature
- **状态**: done
- **优先级**: P1
- **模块**: 调试面板
- **创建**: 2026-05-01
- **完成**: 2026-05-02
- **阻塞**: 无
- **关联文件**: DebugPanelTab.cs, DebugPanelPlayerTab.cs, DebugPanelMonsterTab.cs, DebugPanelNpcTab.cs, DebugPanelMapTab.cs
- **修复提交**: (待提交)

## 描述

所有 HSlider 禁用鼠标滚轮，防止滚动页面时误改参数。

## 改动

- 基类 CreateSliderRow / CreateMonsterSliderRow: 加 `Scrollable = false, FocusMode = Click`
- PlayerTab: 27 处手动 HSlider 加 `Scrollable = false`
- MonsterTab: 13 处
- NpcTab: 3 处
- MapTab: 1 处

## 验证

- 编译 0 错误
- 滚轮不再误触滑条

## 修改记录

### 2026-05-02 ✅
**问题**: 滚动页面时鼠标滚轮误改滑条值
**改法**: 基类 CreateSliderRow/CreateMonsterSliderRow 加 Scrollable = false + FocusMode = Click；各 Tab 手动 HSlider 共 44 处加 Scrollable = false
**结果**: 编译通过，滚轮不再误触
