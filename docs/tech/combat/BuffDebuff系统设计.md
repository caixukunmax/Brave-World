# Buff/Debuff 系统设计

> 状态：设计中
> 日期：2026-05-05

## 1. 目标

为技能管线引入 Buff/Debuff 机制，使技能不再只有即时伤害/治疗，而是支持持续效果、状态控制、属性增减等范式。

**第一批实现的技能范式：**
1. **DOT（持续伤害）** — 毒雾：每 tick 扣血
2. **硬控** — 冰冻：目标无法行动
3. **友方 Buff** — 战吼：范围内友方加攻
4. **吸收盾** — 护盾：吸收 N 点伤害

## 2. 核心数据结构

### 2.1 Buff 实例

```csharp
/// <summary>Buff 实例 — 挂在实体上的运行时状态</summary>
public class BuffInstance
{
    public int BuffId { get; set; }              // 配置 ID
    public long CasterId { get; set; }           // 施法者 ID
    public long TargetId { get; set; }           // 挂载目标 ID
    public int Stacks { get; set; } = 1;         // 叠加层数
    public long ApplyTime { get; set; }          // 施加时刻（ms）
    public long ExpireTime { get; set; }         // 过期时刻（ms），-1=永久
    public long LastTickTime { get; set; }       // 上次 tick 时刻（ms）
    public int TickCount { get; set; }           // 已 tick 次数
    public int ShieldRemaining { get; set; }     // 吸收盾剩余量（仅 shield 类型）
    public int SnapshotAtk { get; set; }         // 施加时快照的攻击力（DOT 伤害用）
}
```

### 2.2 Buff 容器

每个战斗实体（CombatContext）挂一个 BuffContainer：

```csharp
public class BuffContainer
{
    private readonly List<BuffInstance> _buffs = new();
    private readonly LubanTableLoader? _tables;
    
    public BuffContainer(LubanTableLoader? tables = null) { _tables = tables; }
    
    public IReadOnlyList<BuffInstance> Buffs => _buffs;
    
    // 添加/移除/查询
    public BuffInstance? AddBuff(BuffInstance buff);
    public void RemoveBuff(int buffId);  // 仅删除列表项，OnRemove 由 CombatManager.TickBuffs 触发
    public BuffInstance? GetBuff(int buffId);
    public bool HasBuff(int buffId);
    public bool HasTag(string tag);  // 如 "stun", "dot", "shield" — 查配置表 Tags
    
    // 获取某属性的修正值总和（如 patk_bonus, pdef_bonus）— 查配置表 AttrModifiers
    public int GetAttrModifier(string attrName);
    // 获取吸收盾剩余量
    public int GetShieldAmount();
    // 吸收盾扣减，返回实际吸收量
    public int AbsorbShield(int damage);
    // 清除所有 Buff（脱战时调用，不触发 OnRemove）
    public void ClearAll();
}
```

### 2.3 Buff 配置（Luban 表）

新增 Luban 表 `TbBuff`，走标准 Luban 工作流：
- `tables/defines/common.xml` 声明 bean 和 table
- `tables/datas/__beans__.xlsx` 注册新 bean 定义
- `tables/datas/__tables__.xlsx` 注册 TbBuff 表
- `tables/datas/common/#Buff-Buff表.xlsx` 填数据
- 运行 `build_tables.ts` 生成 `common_tbbuff.json`

#### Bean 定义（common.xml）

```xml
<bean name="BuffEffectBean">
    <var name="trigger" type="string" comment="触发时机: OnTick/OnApply/OnRemove"/>
    <var name="action_type" type="int" comment="1=DealDamage, 2=InterruptCast, 3=Heal, 4=ApplyBuff, 5=ApplyShield"/>
    <var name="damage_type" type="int" comment="1=物理, 2=魔法"/>
    <var name="coefficient" type="double" comment="系数"/>
    <var name="buff_id" type="int" comment="嵌套Buff ID（action_type=4/5时使用）"/>
</bean>

<bean name="BuffAttrModifierBean">
    <var name="attr" type="string" comment="属性名: patk/matk/pdef/mdef等"/>
    <var name="value" type="double" comment="修正值"/>
    <var name="is_pct" type="bool" comment="true=百分比, false=固定值"/>
</bean>

<bean name="BuffConfigRow">
    <var name="id" type="int" comment="Buff ID"/>
    <var name="name" type="string" comment="Buff名称"/>
    <var name="buff_type" type="string" comment="Buff/Debuff"/>
    <var name="tags" type="(list#sep=|),string" comment="标签: stun/dot/shield等"/>
    <var name="duration" type="double" comment="持续时间(秒), 0=即时"/>
    <var name="max_stacks" type="int" comment="最大叠加层数"/>
    <var name="stack_rule" type="string" comment="Refresh/Add/Ignore"/>
    <var name="tick_interval" type="double" comment="Tick间隔(秒), 0=不tick"/>
    <var name="effects" type="(list#sep=|),common.BuffEffectBean" comment="效果列表"/>
    <var name="attr_modifiers" type="(list#sep=|),common.BuffAttrModifierBean" comment="属性修正列表"/>
    <var name="shield_base" type="int" comment="吸收盾基础值（0=非盾）"/>
</bean>

<table name="TbBuff" value="BuffConfigRow" input="common/#Buff-Buff表.xlsx"/>
```

#### 数据示例（#Buff-Buff表.xlsx）

| id | name | buff_type | tags | duration | max_stacks | stack_rule | tick_interval | effects | attr_modifiers | shield_base |
|----|------|-----------|------|----------|------------|------------|---------------|---------|----------------|-------------|
| 1 | 中毒 | Debuff | dot\|poison | 6.0 | 1 | Refresh | 1.0 | OnTick,1,2,0.3,0 | | 0 |
| 2 | 冰冻 | Debuff | stun\|freeze | 2.0 | 1 | Refresh | 0 | | | 0 |
| 3 | 战吼 | Buff | stat_boost | 8.0 | 1 | Refresh | 0 | | patk,20,false | 0 |
| 4 | 护盾 | Buff | shield | 10.0 | 1 | Refresh | 0 | | | 50 |

> 注：Luban xlsx 中 list 用 `|` 分隔，bean 多字段用 `,` 分隔

### 2.4 Buff 配置行（C#）

由 Luban 生成，无需手写。服务端 `LubanBeans.cs` 中自动包含：
- `BuffEffectBean`（trigger, action_type, damage_type, coefficient, buff_id）
- `BuffAttrModifierBean`（attr, value, is_pct）
- `BuffConfigRow`（id, name, buff_type, tags, duration, max_stacks, stack_rule, tick_interval, effects, attr_modifiers, shield_base）

`LubanTableLoader.cs` 新增：
```csharp
public Dictionary<int, BuffConfigRow> Buffs { get; private set; } = new();
// 加载：Buffs = LoadTable<BuffConfigRow>(dataDir, "common_tbbuff.json", opts);
// 查询：public BuffConfigRow? GetBuff(int id) => Buffs.GetValueOrDefault(id);
```

## 3. 新 Action 类型

### 3.1 ApplyBuffAction（action_type=4）

给目标挂 Buff，处理叠加规则，触发 OnApply 效果，广播战斗日志。

技能配置示例：
```json
{
  "id": 22,
  "name": "毒雾",
  "actions": [
    { "action_type": 4, "buff_id": 1 }
  ]
}
```

### 3.2 ApplyShieldAction（action_type=5）

给目标挂吸收盾（本质是带 shield_base 的特殊 Buff）。

技能配置示例：
```json
{
  "id": 21,
  "name": "石肤术",
  "actions": [
    { "action_type": 5, "buff_id": 4 }
  ]
}
```

## 4. 管线集成

### 4.1 CombatContext 扩展
- 新增 `Buffs` 字段（BuffContainer）

### 4.2 Buff Tick（CombatManager.TickBuffs）
- 遍历所有战斗实体的 BuffContainer
- 过期 Buff → 触发 OnRemove → 移除
- Tick Buff → 触发 OnTick 效果（如 DOT 伤害）
- 在 CombatManager.Tick 中调用

### 4.3 硬控（Stun）集成
- 怪物自动施法前检查：`if (ctx.Buffs.HasTag("stun")) return 0;` — Stun 状态下不释放技能
- Stun 不影响技能 CD 的自然流逝，仅阻止主动释放行为
- Stun 不影响被选为目标（冰冻的目标仍可被打）

### 4.4 吸收盾集成
- ApplyDamage 中：先扣盾再扣血
- 盾耗尽自动移除 Buff

### 4.5 属性修正集成
- DealDamageAction.CalcDamage 中：读取属性时加上 Buff 修正
- `patk += ctx.Buffs.GetAttrModifier("patk")`
- HealAction.CalcHeal 同理：`matk += ctx.Buffs.GetAttrModifier("matk")`

## 5. 协议扩展

### 5.1 CombatStateNotify 扩展
```protobuf
message BuffInfo {
  int32 buff_id = 1;
  string buff_name = 2;
  int32 stacks = 3;
  float remaining_time = 4;
  int32 shield_amount = 5;
}
// CombatUnit 新增: repeated BuffInfo buffs = 12;
```

### 5.2 CombatLogType 扩展
```protobuf
COMBAT_LOG_BUFF_APPLY = 8;
COMBAT_LOG_BUFF_REMOVE = 9;
COMBAT_LOG_BUFF_TICK = 10;
COMBAT_LOG_SHIELD_ABSORB = 11;
```

## 6. 技能配置扩展

### 6.1 CombatActionBeanRow 扩展

在 `__beans__.xlsx` 中给 CombatActionBean 新增 `buff_id` 字段：

```xml
<!-- common.xml 中 CombatActionBean 补充 -->
<var name="buff_id" type="int" comment="Buff配置ID（action_type=4/5时使用）"/>
```

这样技能配置中可以直接引用 Buff 表：
```json
{ "action_type": 4, "buff_id": 1 }  // ApplyBuff, 挂中毒
{ "action_type": 5, "buff_id": 4 }  // ApplyShield, 挂护盾
```

### 6.2 新增技能

**战士（job=1）：**
| ID | 名称 | TargetType | Actions | 说明 |
|----|------|-----------|---------|------|
| 20 | 战吼 | AllAlliesInRange | ApplyBuff(buff_id=3) | 范围内友方+20攻击力，8秒 |
| 21 | 石肤术 | Self | ApplyShield(buff_id=4) | 自身50点吸收盾，10秒 |

**法师（job=2）：**
| ID | 名称 | TargetType | Actions | 说明 |
|----|------|-----------|---------|------|
| 22 | 毒雾 | AllEnemiesInRange | ApplyBuff(buff_id=1) | 范围内敌人中毒，6秒每秒30%matk |
| 23 | 冰冻术 | SingleEnemy | DealDamage(0.5)+ApplyBuff(buff_id=2) | 伤害+冰冻2秒 |

**牧师（job=3）：**
| ID | 名称 | TargetType | Actions | 说明 |
|----|------|-----------|---------|------|
| 24 | 神圣护盾 | Self | ApplyShield(buff_id=4) | 自身50点吸收盾 |
| 25 | 祝福 | AllAlliesInRange | ApplyBuff(buff_id=3) | 范围内友方+20攻击力 |

**怪物（job=4）：**
| ID | 名称 | TargetType | Actions | 说明 |
|----|------|-----------|---------|------|
| 26 | 猛毒撕咬 | SingleEnemy | DealDamage(1.0)+ApplyBuff(buff_id=1) | 伤害+中毒 |
| 27 | 寒冰吐息 | AllEnemiesInRange | DealDamage(0.4)+ApplyBuff(buff_id=2) | 范围伤害+冰冻 |

## 8. 文件变更清单

### 新增文件
| 文件 | 说明 |
|------|------|
| `Combat/Buffs/BuffInstance.cs` | Buff 实例 |
| `Combat/Buffs/BuffContainer.cs` | Buff 容器 |
| `Combat/Actions/ApplyBuffAction.cs` | 施加 Buff Action |
| `Combat/Actions/ApplyBuffAction.cs` | 施加 Buff/护盾 Action（action_type=4/5 复用） |
| `tables/datas/common/#Buff-Buff表.xlsx` | Buff 数据表 |
| `data/tables/common_tbbuff.json` | Luban 生成的 Buff 配置数据 |

### 修改文件
| 文件 | 变更 |
|------|------|
| `Tables/LubanBeans.cs` | 新增 BuffConfigRow 等（对齐 Luban 生成物） |
| `Tables/LubanTableLoader.cs` | 加载 Buff 表 |
| `Combat/CombatTypes.cs` | CombatContext 增加 Buffs |
| `Combat/CombatManager.cs` | TickBuffs + ApplyDamage 护盾 + Stun 技能锁定检查 |
| `Combat/SkillPipeline.cs` | ExecuteActions 支持 action_type 4/5 |
| `Combat/Actions/ICombatAction.cs` | ActionContext 新增 Tables 字段 |
| `Combat/Actions/DealDamageAction.cs` | 属性修正集成（CalcDamage 加 casterCtx 参数） |
| `Combat/Actions/HealAction.cs` | 属性修正集成（CalcHeal 加 casterCtx 参数） |
| `Combat/CombatRelationManager.cs` | GetOrCreateContext 注入 LubanTableLoader |
| `tables/defines/common.xml` | 新增 BuffEffectBean/BuffAttrModifierBean/BuffConfigRow/TbBuff |
| `tables/datas/__beans__.xlsx` | 注册新 bean 定义 |
| `tables/datas/__tables__.xlsx` | 注册 TbBuff 表 |
| `protocols/proto/game.proto` | BuffInfo + CombatLogType 扩展 |
| `data/tables/common_tbskill.json` | 新增技能 20-27 |
| `data/tables/common_tbbuff.json` | CombatLogText 新增 4 条模板（id 8-11） |
| `tables/scripts/build_tables.ts` | 新增 common_tbbuff.json → 客户端复制 |

## 9. 设计决策

| 决策 | 选择 | 理由 |
|------|------|------|
| Buff 存储位置 | CombatContext | Buff 只在战斗中存在，脱战自动清除 |
| DOT 伤害公式 | matk * coefficient | 复用现有伤害公式，施法者 matk |
| Stun 实现 | 阻止主动技能释放（CD 正常流逝） | 纯控制而非惩罚，解冻后可立即释放已冷却技能 |
| 护盾实现 | 特殊 Buff + shield_base | 统一在 Buff 框架内，不需要独立系统 |
| 叠加规则 | Refresh/Add/Ignore 三种 | 覆盖常见需求，配置驱动 |
| 脱战清 Buff | 是 | 战斗结束所有 Buff 清 |
| 友方判断 | 暂留空（TODO） | 多人组队/友方判定系统未开发，AllAlliesInRange 暂只对自己生效 |
| 配置方式 | Luban 表 | 和技能/怪物等统一走 Luban 工作流 |

## 10. TODO

- [ ] **友方判定系统**：当前 AllAlliesInRange 的 SelectTargets 逻辑基于同地图同类型实体（玩家找玩家、怪物找怪物），没有组队/阵营概念。需要后续开发：
  - 队伍系统（组队后队友算友方）
  - 阵营系统（同阵营算友方）
  - 友方 Buff 技能（战吼、祝福）的 TargetSelection 需要基于友方判定而非同类型
  - 当前临时方案：AllAlliesInRange 对玩家只对自己生效，对怪物对同地图所有怪物生效


## 11. 遗留问题与扩展

### 11.1 脱战清 Buff 的时机

当前设计说"脱战清 Buff"，但需要明确时机：
- **脱战时**：在 CombatManager.Tick 的脱战处理中，对脱战实体的 CombatContext.Buffs 调 ClearAll()
- **死亡时**：死亡实体的 Buff 自然随 CombatContext 一起失效
- **怪物重生时**：重生创建新的 CombatContext，Buff 为空，无需额外处理

### 11.2 DOT 伤害的施法者引用

DOT Buff 的 OnTick 效果需要知道"谁施放的"来计算伤害。BuffInstance 已有 `CasterId` 字段，但施法者可能已经脱战/死亡，CombatContext 可能已被清理。

**方案**：ApplyBuff 时快照施法者的攻击力到 `BuffInstance.SnapshotAtk`，DOT tick 时用快照值计算伤害，不依赖施法者实时状态。

- 物理DOT：`floor(SnapshotAtk * coefficient * (1 - targetPdef * 0.01))`
- 魔法DOT：`floor(SnapshotAtk * coefficient * (1 - targetMdef * 0.01))`

### 11.3 怪物 AI 技能选择与 Buff 技能

当前怪物 AI 的 SelectSkill 是按 SkillPool 顺序选第一个不在 CD 的技能。新增 Buff 技能后，怪物需要能使用这些技能。

- 怪物的 SkillPool 来自 Luban 配置（MonsterRow.Skills）
- 新增的怪物 Buff 技能需要加入对应怪物的 Skills 配置
- **不需要改 AI 逻辑**，只要技能 ID 在 SkillPool 里就行

### 11.4 CombatLogFormatter 扩展

新增的 CombatLogType（8-11）需要在 CombatLogText 表中添加对应的文本模板：

| id | template | color |
|----|----------|-------|
| 8 | {actor} 对 {target} 施加了 {extra} | #88ff88 |
| 9 | {target} 的 {extra} 效果消失了 | #888888 |
| 10 | {target} 受到 {extra} 的持续伤害 {value} | #ff6666 |
| 11 | {target} 的护盾吸收了 {value} 点伤害 | #66aaff |

### 11.5 Buff 效果的嵌套施放

BuffEffectBean 中有 `buff_id` 字段，意味着 Buff 的 OnTick/OnApply 可以触发另一个 Buff（嵌套）。

- 第一批不需要实现嵌套，但数据结构已预留
- 实现时注意防止无限递归（Buff A 的 OnTick 触发 Buff B，B 的 OnApply 又触发 A）
- **建议**：加一个最大嵌套深度限制（如 3 层）

### 11.6 客户端技能配置同步

当前 build_tables.ts 会把 `common_tbskill.json` 复制到客户端 `data/skill_config.json`。新增 Buff 技能后：
- 技能表 JSON 会自动同步
- Buff 表 JSON 也需要同步到客户端（用于显示 Buff 名称/图标等）
- 需要在 build_tables.ts 中增加 `common_tbbuff.json` → `data/buff_config.json` 的复制

### 11.7 Stun 期间能否被选为目标

**可以**。Stun 只阻止被控方主动释放技能（怪物 AI 跳过、玩家 SkillBar 点击被拒绝），不影响被攻击。冰冻的目标仍然可以被选为 SingleEnemy/AllEnemiesInRange 的目标。

### 11.8 HealAction 也需要属性修正

战吼加 matk 的话，治疗量也应该提高。HealAction.CalcHeal 中读取 matk 时也要加上 Buff 修正。
