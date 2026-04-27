# 未开发功能清单（Pending Features）

> 版本：V0.1
> 状态：待开发，由 `combat-system-tdd.md` 及各设计文档中尚未实现的部分整理而来

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

当前完全 AI 驱动：ATB 满时自动选择技能。设计目标是"玩家通过走位间接指挥"，但预留手动技能选择的可能。

### 7.2 待确认

- 是否需要手动技能栏 UI？
- 手动选择是否消耗 ATB？
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

## 10. 优先级建议

| 优先级 | 模块 | 理由 |
|--------|------|------|
| P0 | 战斗协议消息 | 补齐后可大幅降低 CombatStateNotify 推送频率 |
| P1 | Buff/Debuff 系统 | 多数中高级技能依赖此系统 |
| P1 | 剩余 Action 类型 | Heal、ApplyBuff 等是核心技能循环所需 |
| P2 | 投射物系统 | 远程技能必需 |
| P2 | 奖励/掉落系统 | 游戏循环闭环必需 |
| P3 | Armor 属性 | 平衡性调优，可后补 |
| P3 | 战斗视觉特效 | 体验优化，不影响玩法 |
| P3 | 玩家技能选择 UI | 当前 AI 模式已可玩 |
