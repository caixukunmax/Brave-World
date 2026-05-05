# Buff/Debuff 系统设计

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
- TickATB 中：`if (ctx.Buffs.HasTag("stun")) continue;` — 跳过 ATB 增长但保留当前值
- Stun 状态不涨 ATB = 无法行动，解冻后从断点继续
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

## 7. 实施计划

### Phase 1: Buff 基础框架
- 新增 BuffInstance、BuffContainer 类（BuffContainer 注入 LubanTableLoader）
- Luban 表定义（common.xml + xlsx）+ 生成 JSON
- LubanBeans.cs 对齐生成物（或手写对齐）
- LubanTableLoader 加载 Buff 表
- CombatContext 增加 Buffs 字段
- ActionContext 增加 Tables 字段
- CombatRelationManager.GetOrCreateContext 注入 LubanTableLoader 给 BuffContainer
- **验证**：编译通过

### Phase 2: ApplyBuffAction + DOT + 属性修正
- 新增 ApplyBuffAction（action_type=4）
- SkillPipeline.ExecuteActions 支持 action_type=4
- Buff Tick 逻辑（CombatManager.TickBuffs）
- DOT Buff（中毒）+ 战吼 Buff（属性加成）
- 伤害计算集成属性修正
- **验证**：毒雾持续扣血，战吼加攻击力

### Phase 3: 硬控（Stun）
- TickATB 中 Stun 检查
- 冰冻 Buff 实现
- **验证**：冰冻目标 ATB 停止增长

### Phase 4: 吸收盾
- 新增 ApplyShieldAction（action_type=5）
- ApplyDamage 中护盾吸收逻辑
- BuffContainer 盾量管理
- **验证**：护盾先吸收伤害，耗尽后扣血

### Phase 5: 协议 + 客户端
- CombatStateNotify 增加 BuffInfo
- CombatLogType 增加 8-11
- 客户端渲染 Buff 图标/状态
- **验证**：客户端能看到 Buff 图标和剩余时间

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
| `Combat/CombatManager.cs` | TickBuffs + ApplyDamage 护盾 + ATB Stun 检查 |
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
| Stun 实现 | ATB 暂停（保留当前值） | 纯控制而非惩罚，解冻后从断点继续 |
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

## 11. 遗漏与补充

### 11.1 ~~BuffInstance 缺少护盾剩余量字段~~ → 已合并到 2.1

已修正：BuffInstance 新增 `ShieldRemaining` 和 `SnapshotAtk` 字段。

### 11.2 脱战清 Buff 的时机

当前设计说"脱战清 Buff"，但需要明确时机：
- **脱战时**（DisengageSystem 检测到脱战）：在 CombatManager.Tick 的脱战处理中，对脱战实体的 CombatContext.Buffs 调 ClearAll()
- **死亡时**（OnDeath）：死亡实体的 Buff 自然随 CombatContext 一起失效（因为 _relations.OnEntityRemoved 会清理 Context）
- **怪物重生时**：重生创建新的 CombatContext，Buff 为空，无需额外处理

### 11.3 SkillPipeline.ExecuteActions 需要传递 buff_id

当前 ExecuteActions 构建 paramDict 时只传了 type/damageType/coefficient。新增 action_type=4/5 后需要传 buff_id：

```csharp
string actionType = actionCfg.ActionType switch
{
    1 => "DealDamage",
    2 => "InterruptCast",
    3 => "Heal",
    4 => "ApplyBuff",     // 新增
    5 => "ApplyShield",   // 新增
    _ => null!
};

var paramDict = new Dictionary<string, object>
{
    ["type"] = actionType,
    ["damageType"] = actionCfg.DamageType == 2 ? "magical" : "physical",
    ["coefficient"] = actionCfg.Coefficient,
    ["buff_id"] = actionCfg.BuffId,   // 新增
};
```

### 11.4 ApplyBuffAction 的依赖注入

ApplyBuffAction 需要两个外部依赖：
1. **BuffContainer** — 通过 `context.CombatManager.RelationsMgr.Contexts[targetId].Buffs` 获取
2. **BuffConfigRow** — 通过 ActionContext 传递 LubanTableLoader

**方案**：在 ActionContext 中新增 `LubanTableLoader? Tables` 字段，SkillPipeline 在构建 ActionContext 时传入。这样所有 Action 都可以通过 context 访问配置表，未来扩展也更方便。

### 11.5 DOT 伤害的施法者引用

DOT Buff 的 OnTick 效果需要知道"谁施放的"来计算伤害。BuffInstance 已有 `CasterId` 字段，但施法者可能已经脱战/死亡，CombatContext 可能已被清理。

**方案**：ApplyBuff 时快照施法者的攻击力到 `BuffInstance.SnapshotAtk`，DOT tick 时用快照值计算伤害，不依赖施法者实时状态。

- 物理DOT：`floor(SnapshotAtk * coefficient * (1 - targetPdef * 0.01))`
- 魔法DOT：`floor(SnapshotAtk * coefficient * (1 - targetMdef * 0.01))`
- SnapshotAtk 在 ApplyBuff 时设为 patk 或 matk（根据 damage_type）

### 11.6 怪物 AI 技能选择与 Buff 技能

当前怪物 AI 的 SelectSkill 是按 SkillPool 顺序选第一个不在 CD 的技能。新增 Buff 技能后，怪物需要能使用这些技能。

- 怪物的 SkillPool 来自 Luban 配置（MonsterRow.Skills）
- 新增的怪物 Buff 技能（猛毒撕咬/寒冰吐息）需要加入对应怪物的 Skills 配置
- **不需要改 AI 逻辑**，只要技能 ID 在 SkillPool 里就行

### 11.7 CombatLogFormatter 扩展

新增的 CombatLogType（8-11）需要在 CombatLogText 表中添加对应的文本模板：

| id | template | color |
|----|----------|-------|
| 8 | {actor} 对 {target} 施加了 {extra} | #88ff88 |
| 9 | {target} 的 {extra} 效果消失了 | #888888 |
| 10 | {target} 受到 {extra} 的持续伤害 {value} | #ff6666 |
| 11 | {target} 的护盾吸收了 {value} 点伤害 | #66aaff |

### 11.8 ~~Stun 期间是否应该清 ATB~~ → 已统一为保留

已修正：Stun 只暂停 ATB 增长（`continue`），不清零。解冻后从断点继续。冰冻的目标仍可被选为攻击目标。

### 11.9 Buff 效果的嵌套施放

BuffEffectBean 中有 `buff_id` 字段，意味着 Buff 的 OnTick/OnApply 可以触发另一个 Buff（嵌套）。例如：中毒 3 层后自动触发"剧毒" Buff。

- 第一批不需要实现嵌套，但数据结构已预留
- 实现时注意防止无限递归（Buff A 的 OnTick 触发 Buff B，B 的 OnApply 又触发 A）
- **建议**：加一个最大嵌套深度限制（如 3 层）

### 11.10 客户端技能配置同步

当前 build_tables.ts 会把 `common_tbskill.json` 复制到客户端 `data/skill_config.json`。新增 Buff 技能后：
- 技能表 JSON 会自动同步
- Buff 表 JSON 也需要同步到客户端（用于显示 Buff 名称/图标等）
- 需要在 build_tables.ts 中增加 `common_tbbuff.json` → `data/buff_config.json` 的复制

### 11.11 Stun 期间能否被选为目标

**可以**。Stun 只影响 ATB 增长（被控方无法主动行动），不影响被攻击。冰冻的目标仍然可以被选为 SingleEnemy/AllEnemiesInRange 的目标。

### 11.12 HealAction 也需要属性修正

战吼加 matk 的话，治疗量也应该提高。HealAction.CalcHeal 中读取 matk 时也要加上 Buff 修正。

**问题**：CalcHeal 和 DealDamageAction.CalcDamage 都是 static 方法，没有 CombatManager 引用，无法访问 BuffContainer。

**方案**：将 CalcDamage/CalcHeal 改为实例方法或传入 CombatManager 引用。具体做法：
- CalcDamage/CalcHeal 增加 `CombatContext? casterCtx` 参数
- 调用处通过 `context.CombatManager.RelationsMgr.Contexts[casterId]` 获取
- 在计算属性时加上 `casterCtx.Buffs.GetAttrModifier(attrName)`

已合并到 4.5。

### 11.13 BuffContainer.HasTag 需要 LubanTableLoader

`HasTag(string tag)` 需要查询每个 Buff 的配置（BuffConfigRow.Tags），但 BuffContainer 本身不持有 LubanTableLoader 引用。

**方案**：BuffContainer 构造时注入 `LubanTableLoader?`，HasTag/GetAttrModifier 等方法内部查配置表。

```csharp
public class BuffContainer
{
    private readonly LubanTableLoader? _tables;
    public BuffContainer(LubanTableLoader? tables = null) { _tables = tables; }
    
    public bool HasTag(string tag)
    {
        foreach (var buff in _buffs)
        {
            var cfg = _tables?.GetBuff(buff.BuffId);
            if (cfg != null && cfg.Tags.Contains(tag)) return true;
        }
        return false;
    }
}
```

### 11.14 Stun 检查的插入位置

TickATB 中 Stun 检查应插在 `if (ctx.State == "COMBAT" && ctx.SubState != "CASTING")` 条件之后、ATB 增长（`ctx.AtbValue += delta`）之前：

```csharp
if (ctx.State == "COMBAT" && ctx.SubState != "CASTING")
{
    // ... POST_CAST 处理 ...
    // ... ATB boost 衰减 ...
    
    // Stun 检查：跳过 ATB 增长但保留当前值
    if (ctx.Buffs.HasTag("stun")) continue;
    
    double agilityCoef = GetAgilityCoefficient(entityId, maps);
    double delta = ...;
    ctx.AtbValue = Math.Min(100, ctx.AtbValue + delta);
    // ...
}
```

注意：Stun 期间 ATB boost 衰减仍然执行（合理，boost 是临时增益，不应因 Stun 而冻结）。

### 11.15 BuffContainer 需要在 CombatContext 构造时注入 LubanTableLoader

CombatContext.Buffs 是 `BuffContainer` 类型，需要在创建 CombatContext 时传入 LubanTableLoader。

当前 CombatContext 是在 CombatRelationManager.GetOrCreateContext 中 new 的：
```csharp
public CombatContext GetOrCreateContext(long entityId)
{
    if (!Contexts.ContainsKey(entityId))
        Contexts[entityId] = new CombatContext();
    return Contexts[entityId];
}
```

**方案**：CombatRelationManager 构造时注入 LubanTableLoader，GetOrCreateContext 中传给 BuffContainer：
```csharp
Contexts[entityId] = new CombatContext { Buffs = new BuffContainer(_tables) };
```

或者：BuffContainer 延迟绑定，在 CombatManager.Tick 中首次访问时初始化。