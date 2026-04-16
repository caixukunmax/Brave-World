# 战斗系统技术设计文档（Combat System TDD）

> 版本：V0.1  
> 状态：技术设计阶段，可直接进入开发

---

## 1. 概述

本文档基于 `combat-system.md`（玩法设计）和 `skill-pipeline.md`（技能管线设计），输出可直接落地的技术实现方案。

### 1.1 核心原则

- **单角色单 ATB**：一个角色无论同时与多少目标交战，只有一条 ATB 行动条。
- **碰撞即开战**：大地图上发生主动碰撞时立即触发简化普攻，随后进入 ATB 循环。
- **独立战斗关系**：每对（角色 ↔ 目标）之间维护独立的战斗关系（CombatRelation）。
- **移动友好**：默认读条期间允许移动；后摇期间允许移动且不影响移速。
- **打空补偿**：Final Validation 失败时进入完整 CD，ATB 清零，下次积累加速（有上限）。
- **Combat 内嵌 map_pool**：战斗模块作为 `map_pool` 的子模块运行，地图数据零跨服务访问。

---

## 2. 状态机设计（State Machine）

### 2.1 角色战斗状态

每个角色在同一时刻只能处于以下**主状态**之一：

```
IDLE ──(碰撞/被技能命中)──► COMBAT
  ▲                            │
  │                            │(满足脱战条件)
  └────────────────────────────┘
```

在 `COMBAT` 主状态下，角色可能同时处于以下**子状态**（可组合）：

| 子状态 | 说明 | 可移动 | 可主动释放技能 |
|--------|------|--------|----------------|
| `NONE` | 正常战斗中，非读条非后摇 | ✅ | ✅ (ATB 满时) |
| `CASTING` | 正在读条 | ✅ (默认) | ❌ |
| `POST_CAST` | 后摇中 | ✅ | ❌ |

### 2.2 状态转换规则

```
IDLE ──collide──► COMBAT/NONE
COMBAT/NONE ──ATB满──► CASTING ──读条完成──► POST_CAST ──后摇结束──► COMBAT/NONE
COMBAT/NONE ──ATB满且瞬发──► POST_CAST ──后摇结束──► COMBAT/NONE
COMBAT/ANY ──脱战条件满足──► IDLE
CASTING ──被打断(移动/眩晕/死亡)──► COMBAT/NONE (或 IDLE 若死亡)
```

### 2.3 状态组合约束

- `CASTING` 和 `POST_CAST` 不能同时存在。
- 死亡时强制清除所有战斗关系，状态变为 `IDLE`（或 `DEAD`，取决于死亡状态是否单独设计）。
- 脱战时强制中断当前读条和后摇。

---

## 3. 核心数据结构

### 3.1 战斗关系（CombatRelation）

```lua
CombatRelation = {
    relationId      = number,        -- 全局唯一关系 ID
    attackerId      = number,        -- 发起方角色/怪物实例 ID
    targetId        = number,        -- 目标方角色/怪物实例 ID
    startTime       = number,        -- 战斗开始时间戳（秒）
    lastDamageTime  = number,        -- 最后一次互伤时间戳
    lastDistance    = number,        -- 最近一次计算的距离（用于脱战判定）
    isActive        = boolean,       -- 是否仍生效
}
```

### 3.2 角色战斗上下文（CombatContext）

```lua
CombatContext = {
    entityId        = number,        -- 角色/怪物唯一 ID
    state           = "IDLE" | "COMBAT",
    subState        = "NONE" | "CASTING" | "POST_CAST",
    
    -- ATB
    atbValue        = number,        -- 当前 ATB 值 [0, 100]
    atbBoost        = number,        -- 补偿加速累计值（百分比，默认 0）
    atbBoostStacks  = number,        -- 当前补偿层数（默认 0，上限 2）
    
    -- 读条/后摇
    castSkillId     = number | nil,  -- 当前读条中的技能 ID
    castEndTime     = number | nil,  -- 读条结束时间
    postCastEndTime = number | nil,  -- 后摇结束时间
    
    -- 关系索引
    relationIds     = { [relationId] = true },  -- 当前生效的战斗关系 ID 集合
    
    -- 冷却
    skillCooldowns  = { [skillId] = expiryTime },
}
```

### 3.3 全局战斗管理器（CombatManager）

```lua
CombatManager = {
    relations       = { [relationId] = CombatRelation },
    contexts        = { [entityId]   = CombatContext },
    nextRelationId  = 1,
}
```

---

## 4. ATB 模块

### 4.1 计算公式

```lua
-- 每帧（或每次 tick）增加的 ATB 值
deltaATB = baseRate * agilityCoefficient * (1 + atbBoost)
```

| 参数 | 说明 |
|------|------|
| `baseRate` | 全局基础速率，例如 10 点/秒 |
| `agilityCoefficient` | 根据角色"迅捷"属性查表/公式得到，例如 `1.0 + agility * 0.005` |
| `atbBoost` | 补偿加速百分比，例如 0.5 表示加速 50% |

### 4.2 补偿加速规则

- 触发条件：技能管线在阶段 4（Final Validation）失败，即"打空"。
- 补偿效果：
  - `atbBoostStacks + 1`
  - `atbBoost = min(atbBoost + 0.25, 0.50)`  -- 每次 +25%，上限 50%
  - `atbBoostStacks` 上限为 2。
- 消耗时机：
  - 下一次 ATB 从 0 积累到 100 的过程中享受加速。
  - **ATB 满并发起 CastRequest 时**，`atbBoost` 和 `atbBoostStacks` 清零。
  - 即使下一次 Cast 又打空，会重新计算新的补偿（可覆盖）。

### 4.3 ATB 调度器伪代码

```lua
function CombatManager:tickATB(dt)
    for entityId, ctx in pairs(self.contexts) do
        if ctx.state == "COMBAT" and ctx.subState ~= "CASTING" then
            -- 后摇期间 ATB 正常积累
            local agility = getAttr(entityId, "agility")
            local coef = 1.0 + agility * 0.005
            local delta = BASE_RATE * coef * (1 + ctx.atbBoost) * dt
            ctx.atbValue = math.min(100, ctx.atbValue + delta)
            
            if ctx.atbValue >= 100 then
                -- 若在后摇中，需等后摇结束
                if ctx.subState == "POST_CAST" and skynet.now() < ctx.postCastEndTime then
                    ctx.atbValue = 100
                    -- 等待后摇结束
                else
                    ctx.atbValue = 0
                    ctx.atbBoost = 0
                    ctx.atbBoostStacks = 0
                    self:requestCast(entityId)
                end
            end
        end
    end
end
```

### 4.4 `requestCast` 流程

```lua
function CombatManager:requestCast(entityId)
    local ctx = self.contexts[entityId]
    if not ctx then return end
    
    -- AI 选择最优技能（在 skill-pipeline.md 中描述）
    local skillId = AISelectSkill(entityId, ctx)
    if not skillId then
        -- AI 连普攻都选不出 → 跳过，但给予最小补偿？
        ctx.atbBoost = 0.15
        ctx.atbBoostStacks = 1
        return
    end
    
    -- 进入技能管线
    local result = SkillPipeline:cast(skillId, entityId)
    
    if result == "MISS" then
        ctx.atbBoost = math.min(ctx.atbBoost + 0.25, 0.50)
        ctx.atbBoostStacks = math.min(ctx.atbBoostStacks + 1, 2)
    elseif result == "INTERRUPT" then
        -- 打断：无补偿
    end
end
```

---

## 5. 技能管线接口

### 5.1 对外接口

```lua
-- SkillPipeline.lua
local SkillPipeline = {}

--- 发起一次完整的技能释放流程
-- @param skillId  技能配置 ID
-- @param casterId 施法者实例 ID
-- @return string  "SUCCESS" | "MISS" | "FAILURE" | "INTERRUPT"
function SkillPipeline:cast(skillId, casterId)
    -- 1. Pre-Check
    local ok, err = self:preCheck(skillId, casterId)
    if not ok then
        return "FAILURE"
    end
    
    -- 2. Target Selection
    local targets = self:selectTargets(skillId, casterId)
    if not targets or #targets == 0 then
        return "FAILURE"
    end
    
    -- 3. Cast Start
    local castTime = getSkillConfig(skillId).castTime
    local interruptOnMove = getSkillConfig(skillId).interruptOnMove
    self:startCast(casterId, skillId, castTime, interruptOnMove)
    
    -- 读条等待（若 castTime > 0）
    if castTime > 0 then
        -- 此处是协程等待或定时器回调，根据框架决定
        local interrupted = self:waitCast(casterId, castTime)
        if interrupted then
            self:clearCast(casterId)
            return "INTERRUPT"
        end
    end
    
    -- 4. Final Validation
    local ok2 = self:finalValidation(skillId, casterId, targets)
    if not ok2 then
        self:endCast(casterId, skillId, true)  -- true = 打空
        return "MISS"
    end
    
    -- 5. Main Execution
    self:executeActions(skillId, casterId, targets)
    
    -- 6. Cast End
    self:endCast(casterId, skillId, false)
    return "SUCCESS"
end
```

### 5.2 阶段实现

#### 5.2.1 Pre-Check

```lua
function SkillPipeline:preCheck(skillId, casterId)
    local cfg = getSkillConfig(skillId)
    local ctx = CombatManager.contexts[casterId]
    
    -- CD 检查
    if ctx.skillCooldowns[skillId] and skynet.now() < ctx.skillCooldowns[skillId] then
        return false, "cooldown"
    end
    
    -- 状态检查（沉默/眩晕）
    if hasStatus(casterId, "SILENCE") and cfg.requiresVoice then
        return false, "silenced"
    end
    if hasStatus(casterId, "STUN") then
        return false, "stunned"
    end
    
    -- MP 检查
    local mp = getAttr(casterId, "mp")
    if mp < cfg.mpCost then
        return false, "not_enough_mp"
    end
    
    return true
end
```

#### 5.2.2 Target Selection

```lua
function SkillPipeline:selectTargets(skillId, casterId)
    local cfg = getSkillConfig(skillId)
    local casterPos = getPosition(casterId)
    local relations = CombatManager:getActiveRelations(casterId)
    
    if cfg.targetType == "Self" then
        return { casterId }
    end
    
    if cfg.targetType == "SingleEnemy" then
        local candidates = {}
        for _, rel in ipairs(relations) do
            if isEnemy(casterId, rel.targetId) then
                local dist = distance(casterPos, getPosition(rel.targetId))
                if dist <= cfg.castRange then
                    table.insert(candidates, { id = rel.targetId, dist = dist })
                end
            end
        end
        -- 按优先级排序：最近优先（或仇恨最高优先）
        table.sort(candidates, function(a, b) return a.dist < b.dist end)
        if #candidates > 0 then
            return { candidates[1].id }
        end
        return nil
    end
    
    if cfg.targetType == "AllEnemiesInRange" then
        local targets = {}
        for _, rel in ipairs(relations) do
            if isEnemy(casterId, rel.targetId) then
                local dist = distance(casterPos, getPosition(rel.targetId))
                if dist <= cfg.castRange then
                    table.insert(targets, rel.targetId)
                end
            end
        end
        return #targets > 0 and targets or nil
    end
    
    -- ... 其他 targetType 扩展
    return nil
end
```

#### 5.2.3 Final Validation（含 AOE 目标切换）

```lua
function SkillPipeline:finalValidation(skillId, casterId, targets)
    local cfg = getSkillConfig(skillId)
    local casterPos = getPosition(casterId)
    
    -- 校验所有当前目标
    local validTargets = {}
    for _, targetId in ipairs(targets) do
        if isAlive(targetId) and not isInvulnerable(targetId) then
            local dist = distance(casterPos, getPosition(targetId))
            if dist <= cfg.castRange and lineOfSight(casterPos, getPosition(targetId)) then
                table.insert(validTargets, targetId)
            end
        end
    end
    
    -- 对于 AOE/范围类，如果原目标全死了，尝试重选
    if #validTargets == 0 and (cfg.targetType == "SingleEnemy" or cfg.targetType == "AllEnemiesInRange") then
        local fallback = self:selectTargets(skillId, casterId)
        if fallback and #fallback > 0 then
            -- 将新目标写回 targets 引用
            for i, v in ipairs(fallback) do targets[i] = v end
            for i = #fallback + 1, #targets do targets[i] = nil end
            return true
        end
    end
    
    -- 对于定点类技能 (GroundPos)，只需校验坐标是否仍在射程内
    if cfg.targetType == "GroundPos" then
        -- GroundPos 的目标列表里存的是 { x, y }
        local ground = targets[1]
        if ground and distance(casterPos, ground) <= cfg.castRange then
            return true
        end
        return false
    end
    
    return #validTargets > 0
end
```

#### 5.2.4 Cast Start / End

```lua
function SkillPipeline:startCast(casterId, skillId, castTime, interruptOnMove)
    local ctx = CombatManager.contexts[casterId]
    local cfg = getSkillConfig(skillId)
    
    -- 扣 MP
    consumeMP(casterId, cfg.mpCost)
    
    -- 标记状态
    ctx.subState = "CASTING"
    ctx.castSkillId = skillId
    ctx.castEndTime = skynet.now() + castTime * 100
    
    -- 广播读条开始（若有读条时间）
    if castTime > 0 then
        broadcastCastStart(casterId, skillId, castTime)
    end
end

function SkillPipeline:endCast(casterId, skillId, isMiss)
    local ctx = CombatManager.contexts[casterId]
    local cfg = getSkillConfig(skillId)
    
    -- 进 CD（即使是 MISS 也要进完整 CD）
    ctx.skillCooldowns[skillId] = skynet.now() + cfg.cooldown * 100
    
    -- 进入后摇
    ctx.subState = "POST_CAST"
    ctx.castSkillId = nil
    ctx.castEndTime = nil
    ctx.postCastEndTime = skynet.now() + cfg.postCastTime * 100
    
    -- 广播施法结束
    broadcastCastEnd(casterId, skillId, isMiss)
    
    -- 后摇结束时清理状态（由 CombatManager.tick 统一处理）
end
```

---

## 6. Action 注册与扩展机制

### 6.1 Action 注册表

```lua
-- ActionRegistry.lua
local ActionRegistry = {}

function ActionRegistry:register(name, actionModule)
    self.handlers[name] = actionModule
end

function ActionRegistry:get(name)
    return self.handlers[name]
end
```

### 6.2 Action 接口

```lua
-- DealDamage.lua
local DealDamage = {}

--- @param casterId number
--- @param targets  table<number|Vector2>  目标 ID 列表或坐标列表
--- @param context  table  { skillId=..., skillLevel=..., actionParams=... }
function DealDamage:execute(casterId, targets, context)
    local params = context.actionParams
    local formula = DamageFormulas[params.formulaId]
    
    for _, target in ipairs(targets) do
        local targetId = target
        if type(target) == "table" then
            -- 地面坐标，需要扫描范围内目标
            targetId = nil  -- 由 AOE 扫描逻辑处理
        end
        
        if targetId then
            local damage = formula(casterId, targetId, params)
            applyDamage(casterId, targetId, damage, params.damageType)
        end
    end
    
    return { success = true }
end

return DealDamage
```

### 6.3 内置 Action 注册

```lua
-- 在初始化时注册
ActionRegistry:register("DealDamage", require "game.actions.deal_damage")
ActionRegistry:register("Heal", require "game.actions.heal")
ActionRegistry:register("ApplyBuff", require "game.actions.apply_buff")
ActionRegistry:register("ApplyDebuff", require "game.actions.apply_debuff")
ActionRegistry:register("Teleport", require "game.actions.teleport")
ActionRegistry:register("Knockback", require "game.actions.knockback")
ActionRegistry:register("SpawnProjectile", require "game.actions.spawn_projectile")
ActionRegistry:register("SpawnArea", require "game.actions.spawn_area")
ActionRegistry:register("RestoreMP", require "game.actions.restore_mp")
ActionRegistry:register("ConsumeHP", require "game.actions.consume_hp")
```

### 6.4 执行 Action 序列

```lua
function SkillPipeline:executeActions(skillId, casterId, targets)
    local cfg = getSkillConfig(skillId)
    local context = {
        skillId = skillId,
        skillLevel = getSkillLevel(casterId, skillId),
    }
    
    for _, actionCfg in ipairs(cfg.actions) do
        local handler = ActionRegistry:get(actionCfg.type)
        if handler then
            context.actionParams = actionCfg
            local ok, result = pcall(handler.execute, handler, casterId, targets, context)
            if not ok then
                skynet.error("[SkillPipeline] action error: " .. tostring(result))
            end
        else
            skynet.error("[SkillPipeline] unknown action: " .. tostring(actionCfg.type))
        end
    end
end
```

---

## 7. 架构部署：Combat 内嵌于 map_pool

### 7.1 为什么放在 map_pool 下

战斗过程中需要极高频地访问地图数据：
- `Target Selection` 和 `Final Validation` 需要实时计算距离
- AOE 技能需要扫描范围内所有敌人
- 脱战 tick 每秒都要计算双方坐标距离
- 碰撞触发战斗需要同时知道玩家和怪物的位置

如果 Combat 是独立服务，上述操作都会变成高频跨服务调用（`map_pool → combat` 或 `combat → map_pool`），不仅代码琐碎，还容易产生时序不一致。

因此，**Combat 模块作为 `map_pool` 服务内部的子模块运行**。`map_pool` 升级为"单张地图的战场权威服务"：
- 聚合玩家快照（由 `player_pool` 同步）
- 聚合怪物快照（由 `monster_pool` 同步）
- 在本地进程内直接执行 ATB tick、脱战 tick、技能管线、碰撞判定
- 只有**战斗结果**（扣血、死亡、Buff）需要回写给 `player_pool` / `monster_pool`

### 7.2 monster_pool → map_pool 同步接口

`monster_pool` 需要像 `player_pool` 一样，将怪物状态同步到 `map_pool`。

```lua
-- monster_pool/service.lua 初始化时
for instanceId, m in pairs(monsters) do
    platform.serviceSend(common.getMapPoolName(mapId), "monsterEnter", {
        instance_id = instanceId,
        monster_id  = m.monsterId,
        x           = m.x,
        y           = m.y,
        hp          = m.hp or 100,
        max_hp      = m.maxHp or 100,
        level       = m.level or 1,
    })
end

-- monster_pool/logic.lua 中怪物移动后
function logic.tick(monsters, mapId)
    -- ... AI 计算 ...
    if nx and ny and (nx ~= m.x or ny ~= m.y) then
        m.x, m.y = nx, ny
        platform.serviceSend(common.getMapPoolName(mapId), "monsterMove", instanceId, nx, ny)
    end
end

-- monster_pool 怪物死亡/移除时
platform.serviceSend(common.getMapPoolName(mapId), "monsterLeave", instanceId)
```

### 7.3 map_pool 接收怪物同步

```lua
-- map_pool/service.lua
function handlers.monsterEnter(snapshot)
    local mapName = snapshot.map_name or "xinshoucun"
    local map = maps[mapName]
    if not map then
        map = { map_id = common.getMapIdByName(mapName), players = {}, monsters = {} }
        maps[mapName] = map
    end
    map.monsters[snapshot.instance_id] = {
        instance_id = snapshot.instance_id,
        monster_id  = snapshot.monster_id,
        x           = snapshot.x or 0,
        y           = snapshot.y or 0,
        hp          = snapshot.hp or 100,
        max_hp      = snapshot.max_hp or 100,
        level       = snapshot.level or 1,
    }
end

function handlers.monsterMove(instanceId, x, y)
    for mapName, map in pairs(maps) do
        if map.monsters[instanceId] then
            map.monsters[instanceId].x = x
            map.monsters[instanceId].y = y
            break
        end
    end
end

function handlers.monsterLeave(instanceId)
    for mapName, map in pairs(maps) do
        if map.monsters[instanceId] then
            map.monsters[instanceId] = nil
            -- 同时通知 combat 模块清除相关战斗关系
            CombatManager:onEntityRemoved(instanceId)
            break
        end
    end
end
```

### 7.4 战斗结果回写流向

`map_pool` 内的 Combat 模块计算出战斗结果后，通过异步消息回写给数据所有者：

| 结果类型 | 回写目标 | 方式 | 说明 |
|---------|---------|------|------|
| 玩家扣血/死亡 | `player_pool` | `serviceSend` | player_pool 更新 onlinePlayers 中的玩家数据 |
| 玩家获得经验/掉落 | `player_pool` | `serviceCall` 或事件 | 需要持久化时可用 call |
| 怪物扣血/死亡 | `monster_pool` | `serviceSend` | monster_pool 更新 monsters 表状态 |
| 怪物脱战回血 | `monster_pool` | `serviceSend` | 通知 monster_pool 恢复怪物 HP |
| Buff/Debuff 状态 | `player_pool` / `monster_pool` | `serviceSend` | 由各自服务维护状态持续时间 |

---

## 8. 碰撞与战斗触发

### 8.1 碰撞检测入口

```lua
-- 由大地图移动系统调用（在 map_pool 进程内直接处理）
function CombatManager:onCollision(entityA, entityB)
    -- 排除同阵营的非敌对碰撞（如玩家撞玩家是否 PVP，由业务规则决定）
    if not canCombat(entityA, entityB) then
        return
    end
    
    -- 判断谁撞谁（由移动发起方决定）
    local mover, target = getMoverAndTarget(entityA, entityB)
    
    -- 建立双向战斗关系
    self:createRelation(mover, target)
    self:createRelation(target, mover)
    
    -- 若碰撞时双方至少有一方未在战斗状态，先手方执行一次简化普攻
    local moverCtx = self.contexts[mover]
    local targetCtx = self.contexts[target]
    
    if (not moverCtx or moverCtx.state == "IDLE") or (not targetCtx or targetCtx.state == "IDLE") then
        -- 简化普攻：不走完整管线，直接 DealDamage
        self:executeFirstStrike(mover, target)
    end
    
    -- 确保双方进入 COMBAT 状态
    self:setState(mover, "COMBAT")
    self:setState(target, "COMBAT")
end
```

### 7.2 简化普攻（First Strike）

```lua
function CombatManager:executeFirstStrike(attackerId, targetId)
    local action = require "game.actions.deal_damage"
    action:execute(attackerId, { targetId }, {
        skillId = 1,  -- 普攻 ID
        actionParams = {
            damageType = "physical",
            coefficient = 1.0,
            formulaId = 2,
        }
    })
    
    -- 更新最后伤害时间（用于脱战判定）
    local rel = self:getRelation(attackerId, targetId)
    if rel then rel.lastDamageTime = skynet.time() end
end
```

---

## 9. 脱战机制

### 8.1 脱战判定 Tick

```lua
function CombatManager:tickDisengage(dt)
    local now = skynet.time()
    
    for relationId, rel in pairs(self.relations) do
        if not rel.isActive then goto continue end
        
        local posA = getPosition(rel.attackerId)
        local posB = getPosition(rel.targetId)
        local dist = distance(posA, posB)
        rel.lastDistance = dist
        
        -- 条件 2：彻底逃离 (B 距离)
        if dist > DISENGAGE_DISTANCE_B then
            -- 需要维持 S2 秒，这里简化为用 lastDistanceTick 计数
            rel.disengageTimer2 = (rel.disengageTimer2 or 0) + dt
            if rel.disengageTimer2 >= DISENGAGE_TIME_S2 then
                self:removeRelation(relationId)
                goto continue
            end
        else
            rel.disengageTimer2 = 0
        end
        
        -- 条件 1：安全脱离 (A 距离 + 无伤)
        if dist > DISENGAGE_DISTANCE_A then
            local noDamage = (now - (rel.lastDamageTime or 0)) >= DISENGAGE_NO_DAMAGE_TIME_T1
            if noDamage then
                rel.disengageTimer1 = (rel.disengageTimer1 or 0) + dt
                if rel.disengageTimer1 >= DISENGAGE_TIME_S1 then
                    self:removeRelation(relationId)
                    goto continue
                end
            else
                rel.disengageTimer1 = 0
            end
        else
            rel.disengageTimer1 = 0
        end
        
        ::continue::
    end
end
```

### 8.2 常数建议

```lua
DISENGAGE_DISTANCE_A        = 15   -- 安全脱离距离
DISENGAGE_NO_DAMAGE_TIME_T1 = 3    -- 最近 3 秒内无互伤
DISENGAGE_TIME_S1           = 5    -- 维持 5 秒
DISENGAGE_DISTANCE_B        = 30   -- 彻底逃离距离
DISENGAGE_TIME_S2           = 3    -- 维持 3 秒
```

### 8.3 关系移除与状态回退

```lua
function CombatManager:removeRelation(relationId)
    local rel = self.relations[relationId]
    if not rel then return end
    rel.isActive = false
    
    -- 从双方的 context 中移除
    local ctxA = self.contexts[rel.attackerId]
    local ctxB = self.contexts[rel.targetId]
    if ctxA then ctxA.relationIds[relationId] = nil end
    if ctxB then ctxB.relationIds[relationId] = nil end
    
    -- 检查是否还有别的战斗关系，没有则回退到 IDLE
    if ctxA and next(ctxA.relationIds) == nil then
        self:setState(rel.attackerId, "IDLE")
    end
    if ctxB and next(ctxB.relationIds) == nil then
        self:setState(rel.targetId, "IDLE")
    end
end
```

---

## 10. 死亡与复活

### 9.1 死亡处理

```lua
function CombatManager:onDeath(entityId)
    local ctx = self.contexts[entityId]
    if not ctx then return end
    
    -- 清除所有战斗关系
    for relationId, _ in pairs(ctx.relationIds) do
        self:removeRelation(relationId)
    end
    
    -- 中断读条/后摇
    ctx.subState = "NONE"
    ctx.castSkillId = nil
    ctx.castEndTime = nil
    ctx.postCastEndTime = nil
    ctx.atbValue = 0
    ctx.atbBoost = 0
    
    -- 状态设为 IDLE（死亡状态由外部 HP 系统管理）
    self:setState(entityId, "IDLE")
    
    -- 广播死亡
    broadcastDeath(entityId)
    
    -- 延迟复活
    skynet.timeout(RESPAWN_DELAY * 100, function()
        respawnAtBirthPoint(entityId)
    end)
end
```

### 9.2 复活点

```lua
function respawnAtBirthPoint(entityId)
    local mapId = getEntityMapId(entityId)
    local birthPos = getMapBirthPoint(mapId)  -- 地图配置中的出生点
    setPosition(entityId, birthPos.x, birthPos.y)
    resetHP(entityId)
    resetMP(entityId)
    broadcastRespawn(entityId, birthPos)
end
```

---

## 11. 怪物脱战后回血

```lua
function CombatManager:tickMonsterRegen(dt)
    for entityId, ctx in pairs(self.contexts) do
        if isMonster(entityId) and ctx.state == "IDLE" then
            local hp = getAttr(entityId, "hp")
            local maxHp = getAttr(entityId, "maxHp")
            if hp < maxHp then
                local regen = maxHp * MONSTER_REGEN_PERCENT_PER_SEC * dt
                setAttr(entityId, "hp", math.min(maxHp, hp + regen))
            end
        end
    end
end
```

### 10.1 常数建议

```lua
MONSTER_REGEN_PERCENT_PER_SEC = 0.05  -- 每秒回血 5%
```

---

## 12. 网络同步策略

### 11.1 服务端广播原则

| 事件 | 同步对象 | 同步内容 | 频率/时机 |
|------|----------|----------|-----------|
| **碰撞进入战斗** | 碰撞双方及周围可见玩家 | 双方 entityId、碰撞坐标、先手动画 | 即时 |
| **ATB 满触发读条** | 施法者自己 | 可选（用于客户端预测） | 即时 |
| **读条开始** | 周围可见玩家 | entityId、skillId、castTime、目标坐标 | 即时（仅 castTime > 0） |
| **技能释放/打空** | 周围可见玩家 | entityId、skillId、目标列表、命中结果 | 读条结束时 |
| **Action 效果（伤害/治疗/Buff）** | 受影响的目标及周围玩家 | 伤害数值、BuffId、血量变化 | 即时（随技能释放一起打包） |
| **弹道生成** | 周围可见玩家 | projectileId、起点、终点、速度 | 即时 |
| **弹道命中** | 周围可见玩家 | projectileId、命中目标、命中 Action 结果 | 命中时 |
| **后摇结束** | 施法者自己 | 可选 | 不需要广播 |
| **脱战** | 该角色自己 | 状态变为 IDLE | 即时 |
| **死亡** | 周围可见玩家 | entityId、死亡坐标 | 即时 |
| **复活** | 周围可见玩家 | entityId、复活坐标 | 即时 |

### 11.2 客户端预测

- **读条动画**：客户端在收到 `CastStart` 后立即播放读条动画，不需要等服务器确认。
- **弹道飞行**：客户端根据 `SpawnProjectile` 广播自主模拟飞行轨迹，命中时等待服务器 `ProjectileHit` 确认后再播放命中特效和伤害数字。
- **自身 ATB 条**：客户端根据角色"迅捷"属性和补偿状态本地估算 ATB 进度，用于 UI 显示；但真正的施法时机由服务器决定。

### 11.3 消息协议建议

```protobuf
// 碰撞进入战斗
message CombatStartNotify {
    uint32 attacker_id = 1;
    uint32 target_id = 2;
    uint32 x = 3;
    uint32 y = 4;
    bool first_strike = 5;  // 是否是先手碰撞触发
}

// 读条开始
message CastStartNotify {
    uint32 caster_id = 1;
    uint32 skill_id = 2;
    float cast_time = 3;
    uint32 target_id = 4;    // 主目标
    uint32 target_x = 5;     // 若是 GroundPos
    uint32 target_y = 6;
}

// 技能释放结果（成功或打空）
message CastResultNotify {
    uint32 caster_id = 1;
    uint32 skill_id = 2;
    repeated uint32 target_ids = 3;
    bool is_miss = 4;        // true = 打空
}

// 战斗伤害/Buff 事件（可由 CastResultNotify 内嵌或单独发送）
message CombatEventNotify {
    uint32 target_id = 1;
    int32 hp_delta = 2;
    int32 mp_delta = 3;
    repeated BuffInfo buffs = 4;
    repeated DebuffInfo debuffs = 5;
}

// 弹道
message ProjectileSpawnNotify {
    uint64 projectile_id = 1;
    uint32 skill_id = 2;
    uint32 from_x = 3;
    uint32 from_y = 4;
    uint32 to_x = 5;
    uint32 to_y = 6;
    float speed = 7;
}

message ProjectileHitNotify {
    uint64 projectile_id = 1;
    uint32 target_id = 2;
    repeated CombatEventNotify events = 3;
}

// 脱战
message DisengageNotify {
    uint32 entity_id = 1;
}

// 死亡
message DeathNotify {
    uint32 entity_id = 1;
    uint32 x = 2;
    uint32 y = 3;
}

// 复活
message RespawnNotify {
    uint32 entity_id = 1;
    uint32 x = 2;
    uint32 y = 3;
}
```

---

## 13. 配置表 Schema

### 12.1 TbSkill（技能表）

| 字段 | 类型 | 说明 |
|------|------|------|
| `id` | int | 技能唯一 ID（1 为普攻） |
| `name` | string | 技能名称 |
| `cast_range` | int | 施法距离（格数） |
| `cast_time` | float | 读条时间（秒，0 为瞬发） |
| `interrupt_on_move` | bool | 移动是否打断读条（默认 false） |
| `post_cast_time` | float | 后摇时间（秒） |
| `cooldown` | float | 冷却时间（秒） |
| `mp_cost` | int | MP 消耗 |
| `target_type` | string | 目标类型枚举 |
| `actions` | json | Action 序列配置 |

### 12.2 TbBuff / TbDebuff（状态表）

| 字段 | 类型 | 说明 |
|------|------|------|
| `id` | int | 状态唯一 ID |
| `name` | string | 状态名称 |
| `type` | string | "buff" / "debuff" |
| `duration` | float | 持续时间（秒） |
| `max_stacks` | int | 最大叠加层数 |
| `effect` | json | 具体效果（加攻/减防/持续伤害等） |

### 12.3 TbProjectile（投射物表，可选）

| 字段 | 类型 | 说明 |
|------|------|------|
| `id` | int | 投射物类型 ID |
| `speed` | float | 飞行速度（格/秒） |
| `max_range` | int | 最大飞行距离（超出自动销毁） |
| `hit_radius` | float | 命中判定半径 |
| `on_hit_actions` | json | 命中时触发的 Action 序列 |

---

## 14. 目录结构建议

```
skynet_src/game/map_pool/
├── service.lua           -- map_pool 原有入口，新增 monsterEnter/monsterMove/monsterLeave handler
├── combat/
│   ├── manager.lua       -- CombatManager：关系管理、状态机、ATB tick、脱战 tick
│   ├── pipeline.lua      -- SkillPipeline：6 阶段技能管线
│   ├── atb.lua           -- ATB 计算与补偿加速
│   ├── relation.lua      -- CombatRelation CRUD
│   ├── disengage.lua     -- 脱战判定逻辑
│   ├── actions/
│   │   ├── init.lua      -- ActionRegistry
│   │   ├── deal_damage.lua
│   │   ├── heal.lua
│   │   ├── apply_buff.lua
│   │   ├── apply_debuff.lua
│   │   ├── teleport.lua
│   │   ├── knockback.lua
│   │   ├── spawn_projectile.lua
│   │   ├── spawn_area.lua
│   │   ├── restore_mp.lua
│   │   └── consume_hp.lua
│   └── projectiles/
│       └── manager.lua   -- 独立投射物管理器
└── ... 其他 map_pool 文件
├── manager.lua           -- CombatManager：关系管理、状态机、ATB tick、脱战 tick
├── pipeline.lua          -- SkillPipeline：6 阶段技能管线
├── atb.lua               -- ATB 计算与补偿加速（可被 manager 直接包含）
├── relation.lua          -- CombatRelation CRUD
├── disengage.lua         -- 脱战判定逻辑（可被 manager 直接包含）
├── actions/
│   ├── init.lua          -- ActionRegistry
│   ├── deal_damage.lua
│   ├── heal.lua
│   ├── apply_buff.lua
│   ├── apply_debuff.lua
│   ├── teleport.lua
│   ├── knockback.lua
│   ├── spawn_projectile.lua
│   ├── spawn_area.lua
│   ├── restore_mp.lua
│   └── consume_hp.lua
└── projectiles/
    └── manager.lua       -- 独立投射物管理器（飞行、命中检测、二次验证）
```

---

## 15. 下一步

1. 确认 TDD 中的常数取值（脱战距离、回血速率、ATB 基础速率等）。
2. 开始开发第一批核心文件：
   - `map_pool/combat/manager.lua`
   - `map_pool/combat/pipeline.lua`
   - `map_pool/combat/actions/init.lua`
   - 修改 `monster_pool/service.lua` 和 `logic.lua` 增加怪物同步到 `map_pool`。
   - 修改 `map_pool/service.lua` 增加怪物同步 handler 和 Combat 初始化。
