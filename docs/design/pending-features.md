# 未开发功能清单（Pending Features）

> 版本：V0.1
> 状态：待确认

---

## 1. 概述

本文档汇总已设计但尚未进入开发的功能模块，供后续排期参考。每个条目标注来源设计文档和依赖关系。

---

## 2. 战斗协议消息

> 来源：`combat-system-tdd.md` §11.3
> 依赖：Protobuf 协议更新

当前战斗状态通过 `CombatStateNotify`（100ms 推送）和 `CombatLogNotify` 覆盖，但缺少以下独立事件通知。补齐后客户端可以更精确地响应战斗事件，减少对高频推送的依赖。

### 2.1 待实现消息

| 消息 | 方向 | 用途 |
|------|------|------|
| `CombatStartNotify` | S→C | 碰撞触发战斗，含双方 entityId、碰撞坐标、先手标记 |
| `CastStartNotify` | S→C | 读条开始，含 casterId、skillId、castTime、目标坐标 |
| `CastResultNotify` | S→C | 技能释放结果（成功/打空），含目标列表 |
| `CombatEventNotify` | S→C | 伤害/Buff/Debuff 事件，含 hpDelta、mpDelta |
| `ProjectileSpawnNotify` | S→C | 弹道生成，含起点、终点、速度 |
| `ProjectileHitNotify` | S→C | 弹道命中，含命中目标和二次 Action 结果 |
| `DisengageNotify` | S→C | 脱战通知 |
| `DeathNotify` | S→C | 通用死亡通知（玩家 + 怪物） |
| `RespawnNotify` | S→C | 通用复活通知 |

### 2.2 协议定义

详见 `combat-system-tdd.md` §11.3 中的 protobuf 定义。

---

## 3. 剩余 Action 类型

> 来源：`skill-pipeline.md` §5.2
> 依赖：`ICombatAction` 接口、`ActionRegistry`

当前仅实现 `DealDamage` 和 `InterruptCast`。框架已支持扩展，以下 Action 需逐个实现：

| Action | 作用 | 关键参数 |
|--------|------|----------|
| `Heal` | 恢复目标生命 | `formulaId`, `isPercent`, `value` |
| `ApplyBuff` | 施加增益状态 | `buffId`, `duration`, `stacks` |
| `ApplyDebuff` | 施加减益状态 | `debuffId`, `duration` |
| `Teleport` | 瞬移到目标位置 | `targetPos` |
| `Knockback` | 击退目标 | `distance` |
| `SpawnProjectile` | 生成投射物 | `projectileId`, `speed`, `onHitActions` |
| `SpawnArea` | 生成 AOE 区域 | `radius`, `duration`, `tickInterval`, `tickActions` |
| `RestoreMP` | 恢复 MP | `value` |
| `ConsumeHP` | 消耗自身 HP 作为代价 | `value` |

### 3.1 实现模板

每个 Action 实现 `ICombatAction` 接口：

```csharp
public class XxxAction : ICombatAction
{
    public string ActionType => "Xxx";
    public ActionResult Execute(long casterId, List<long> targets, ActionContext context) { ... }
}
```

在 `SkillPipeline` 初始化时注册到 `ActionRegistry`。

---

## 4. Buff / Debuff 系统

> 来源：`combat-system-tdd.md` §12.2
> 依赖：`ApplyBuff` / `ApplyDebuff` Action

### 4.1 配置表结构

**TbBuff（增益表）**

| 字段 | 类型 | 说明 |
|------|------|------|
| `id` | int | 状态唯一 ID |
| `name` | string | 状态名称 |
| `type` | string | "buff" / "debuff" |
| `duration` | float | 持续时间（秒） |
| `max_stacks` | int | 最大叠加层数 |
| `effect` | json | 具体效果（加攻/减防/持续伤害等） |

### 4.2 运行时管理

- 每个 `CombatContext` 新增 `ActiveBuffs` / `ActiveDebuffs` 列表
- CombatManager tick 中检查过期 Buff，触发移除
- Buff 效果影响属性计算（如 +20% PATK）

---

## 5. 投射物系统

> 来源：`skill-pipeline.md` §5.2（SpawnProjectile）、`combat-system-tdd.md` §6.3
> 依赖：`SpawnProjectile` Action、独立 ProjectileManager

### 5.1 核心设计

- 投射物从施法者位置生成，沿直线飞向目标位置
- 飞行期间独立 tick 更新位置
- 命中时执行 `onHitActions`（二次验证：目标存活、距离判定）
- 超出 `maxRange` 自动销毁

### 5.2 配置表

**TbProjectile（投射物表）**

| 字段 | 类型 | 说明 |
|------|------|------|
| `id` | int | 投射物类型 ID |
| `speed` | float | 飞行速度（格/秒） |
| `max_range` | int | 最大飞行距离 |
| `hit_radius` | float | 命中判定半径 |
| `on_hit_actions` | json | 命中时触发的 Action 序列 |

### 5.3 网络同步

- 生成时广播 `ProjectileSpawnNotify`（起点、终点、速度）
- 命中时广播 `ProjectileHitNotify`（命中目标、Action 结果）
- 客户端自主模拟飞行轨迹，命中时等待服务器确认

---

## 6. 奖励 / 掉落系统

> 来源：`combat-system.md` §8
> 依赖：怪物死亡处理、物品系统

### 6.1 功能描述

- 怪物死亡时根据配置表生成掉落物
- 经验分配：按伤害贡献分配（可考虑最高伤害者优先）
- 掉落物生成到地图上，玩家碰撞拾取

### 6.2 待确认

- 掉落表结构（按怪物 ID 关联）
- 经验公式
- 掉落物存在时间
- 是否需要队伍分配机制

---

## 7. 玩家技能选择 UI

> 来源：`combat-system.md` §5
> 依赖：客户端 UI 系统、技能协议

### 7.1 当前状态

当前怪物完全由 AI 驱动：技能 CD 好时自动选择并释放。玩家通过走位间接影响战斗，同时支持手动点击 SkillBar 释放技能。

### 7.2 待确认

- 是否需要手动技能栏 UI？
- 手动选择是否受后摇限制？
- 手动选择与 AI 自动选择如何切换？

---

## 8. 战斗视觉特效

> 来源：`combat-system.md` §9
> 依赖：客户端渲染系统

### 8.1 待实现特效

| 特效 | 描述 |
|------|------|
| 战斗光环 | 进入战斗后角色脚下出现光效，脱战后消失 |
| 先手突进增强 | 碰撞 30% 弹回动画的打击感增强 |
| 角色动作栏 | 角色下方磁吸动作栏，显示蓄力技能名 + 蓄力进度条 |

---

## 9. Armor（护甲）属性

> 来源：`combat-system.md` §3
> 依赖：属性系统、伤害公式

### 9.1 设计描述

设计文档列出 9 项战斗属性，其中 Armor（护甲）为"物理减伤层"。当前代码使用 PDEF 替代，未实现独立护甲机制。

### 9.2 待确认

- Armor 与 PDEF 的关系（独立？覆盖？叠加？）
- 护甲减伤公式（固定减伤 / 百分比减伤 / 穿透机制）
- 怪物是否需要护甲属性

---

## 11. 移动系统与战斗交互改进

> 来源：`移动系统设计.md` §10 审查、`技能管线设计.md` §6.2
> 依赖：Luban 配置表、AI Tick 调度、网络协议

2026-05-14 代码审查中识别出的三项结构性改进，当前硬编码实现会在引入远程怪物/方向技能时成为阻塞点。

### 11.1 碰撞范围可配置

**现状：** `CollisionDetector.CheckEntityCollision` 和 `CombatManager.ExecuteFirstStrike` 的射程判定均硬编码为曼哈顿距离 `≤ 1`。

**阻塞场景：**
- 远程怪物（弓箭手、法师）应在距离 2~3 时触发碰撞战斗，而非必须贴身
- 长矛兵攻击范围=2，碰撞应在隔一格时触发
- 冲锋类技能的碰撞触发范围应与技能配置一致

**方案：**
- 在 `TbMonster` 或 `TbAi` 配置表中增加 `collision_range` 字段（默认=1）
- `CheckEntityCollision` 读取目标实体的 `collision_range` 做判定
- `ExecuteFirstStrike` 读取当前普攻/技能的 `cast_range` 做判定

### 11.2 移动中目标追踪更新

**现状：** `MonsterManager.Tick` 中移动中的怪物直接 `continue`，800ms 移动期间不做任何 AI 评估。

**导致的问题：**
- 怪物读条期间玩家走位，移动完成后才发现目标已跑出射程 → 技能打空，进入完整 CD
- 目标在怪物移动期间已走进射程，怪物仍走完无意义的移动路径 → 反应迟钝

**方案：**
- 在移动检查点（30% / 100%）之间增加**轻量目标追踪**：只更新目标坐标、判断当前距离是否已进入 `cast_range`
- 若已进入射程且无需继续靠近，提前 `CancelMove` 并切换至 `combat_hold` 状态
- 不执行完整 AI 决策（寻路、选技能），避免与现有 Tick 流程冲突

### 11.3 朝向系统

**现状：** 服务端和客户端均不记录实体面朝方向。移动动画由客户端根据位移方向推断。

**限制的设计：**
| 功能 | 没有朝向的问题 |
|------|---------------|
| 背刺伤害加成 | 无法判断攻击者是否在目标背后 |
| 盾墙/格挡方向 | 盾牌只挡正面，不知道正面朝哪 |
| 击退方向 | 无法计算"朝攻击者反方向"的击退向量 |
| 技能特效朝向 | 火球术应从面朝方向射出，客户端只能猜测 |
| 怪物转身动画 | 无朝向字段，无法判断何时播放转身过渡 |

**方案：**
- 增加 `enum Direction { Up, Down, Left, Right }`
- `MapPlayerState` / `MapMonsterState` 增加 `Facing` 字段
- 移动完成时根据 `from→to` 更新朝向；施法时根据目标位置更新朝向
- 在位置同步协议中追加 `facing` 字段，客户端据此调整特效朝向和动画

---

## 10. 未来开发计划（高概念）

> 记录者：AI Agent（2026-07-14）
> 状态：待细化/待排期

以下为用户提出的高概念方向，当前尚未形成详细设计，供后续不知道做什么时参考。

### 10.1 导演模式

- **状态（2026-07-22）**：第一期已实现，待游戏内验收。设计文档：`docs/design/导演模式设计.md`。
- **已落地**：客户端本地驱动的演出运行时（`clinetcsharp/Scripts/Cutscene/`，13 种 cue、气泡、黑边、镜头、假人、区域触发、本地进度、GM 命令 `cutscene,<id>` 热重载）+ Godot 编辑器插件（`clinetcsharp/addons/cutscene_editor/`，可视化编排、地图拾取坐标、触发区框选）。
- **第二期方向**：音频管理器（激活 sfx/bgm cue）、选项分支对话树、剧情进度上服务器、服务器权威演出/多人同步、插件增强（拖拽排序、示意回放）。
- **历史描述**（已按设计文档实现，供参考）：时间轴编辑器、镜头路径、NPC 走位指令、对话气泡、触发器、预览回放；关联系统：剧情系统（StoryPanel）、地图编辑器、NPC AI、摄像机控制。

### 10.2 战斗视觉优化

- **一句话描述**：提升战斗过程中的视觉反馈与打击感，让玩家更容易理解战场状态。
- **可能包含**：
  - 受击闪烁 / 击退动画 / 受击停顿（hit stop）
  - 技能释放范围指示器、弹道特效、命中特效
  - 血条/数字跳字/Buff 图标视觉增强
  - 战斗光环、先手突进、蓄力条等已有概念落地
- **关联系统**：战斗系统、技能管线、Buff/Debuff 系统、实体渲染。
- **下一步**：先明确优先级最高的子项，再做单项原型验证。

### 10.3 剧情视觉优化

- **一句话描述**：让剧情展示从“文字框”升级为更具沉浸感的演出效果。
- **可能包含**：
  - 角色立绘 / 表情差分 / 半身像对话
  - 背景图 / 场景虚化 / 镜头切换
  - 打字机效果增强（当前已实现逐字 + 音效）
  - 选项分支、对话历史回溯（当前已实现滚轮查看历史）
  - 剧情演出时的界面隐藏、镜头聚焦
- **关联系统**：StoryPanel、导演模式、UI 可见性管理、资源管线。
- **下一步**：先确定第一章剧情演出的目标效果，再拆解所需美术资源与代码改动。

---

## 11. 优先级建议

| 优先级 | 模块 | 理由 |
|--------|------|------|
| P0 | 战斗协议消息 | 补齐后可大幅降低 CombatStateNotify 推送频率 |
| P1 | Buff/Debuff 系统 | 多数中高级技能依赖此系统 |
| P1 | 剩余 Action 类型 | Heal、ApplyBuff 等是核心技能循环所需 |
| P1 | 碰撞范围可配置 | 引入远程怪物前必须解除硬编码限制 |
| P2 | 投射物系统 | 远程技能必需 |
| P2 | 奖励/掉落系统 | 游戏循环闭环必需 |
| P2 | 移动中目标追踪更新 | 优化战斗手感，减少"反应迟钝"感知 |
| P2 | 朝向系统 | 背刺/击退/方向技能的前置基础设施 |
| P3 | Armor 属性 | 平衡性调优，可后补 |
| P3 | 战斗视觉特效 | 体验优化，不影响玩法 |
| P3 | 玩家技能选择 UI | 当前 AI 模式已可玩 |
