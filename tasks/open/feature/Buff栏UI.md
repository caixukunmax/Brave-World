---
type: feature
status: open
priority: P2
module: UI/Buff
created: 2026-05-04
completed:
blocked:
related_files:
  - clinetcsharp/Scripts/SkillBar.cs
  - clinetcsharp/Scripts/Player.cs
  - servercsharp/src/GameServer.GameLogic/Combat/BuffContainer.cs
design_doc:
fix_commit:
---

# Buff 栏 UI

## 目标
在技能栏左侧显示一排 Buff 图标方块，展示当前角色身上挂载的 Buff 列表。

## 需求
- 一排等大方块，不显示名字
- 可调间距（全局生效）
- 可调单个方块大小（改一个全部跟着改）
- 位置可在调试面板调节，默认在技能栏左面
- 按添加顺序排列
- Buff 过期直接消失，无动画
- 目前无美术素材，用替代物渲染（品质色块/图标占位）

## 依赖
- 服务端 BuffContainer 已实现（BuffInstance + TickBuffs）
- 协议已有 BuffInfo 在 CombatStateNotify.CombatUnit.buffs 里

## 已完成
- [x] BuffBar.cs — HBoxContainer，10个 BuffSlot
- [x] BuffSlot — PanelContainer + 色块 + 剩余时间 + 叠加层数
- [x] 订阅 CombatStateNotify 获取 Buff 列表
- [x] 调试面板 UI Tab 加 BuffBar 控制区（图标大小、间距）
- [x] buff_bar.tscn 场景 + main.tscn 挂载
- [x] 编译验证通过

## 待验证
- [ ] 实际运行效果（需要服务端推送 CombatStateNotify 含 buffs 数据）