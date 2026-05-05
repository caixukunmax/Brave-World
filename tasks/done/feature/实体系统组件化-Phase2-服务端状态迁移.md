# 实体系统组件化重构 — Phase 2: 服务端状态迁移到组件系统

## 类型
feature

## 来源
docs/design/实体系统组件化重构.md Phase 2

## 目标
将 MapPlayerState/MapMonsterState/MapNpcState/MonsterRuntimeState 的数据迁移到 ServerEntity + 组件系统，WorldState 统一实体索引

## 具体任务

### 1. MapPlayerState → 继承 ServerEntity
- AccountId/RoleId/ServerId/RoleName/Job → ServerEntity 上的 Player 特有字段
- GridX/GridY → ServerEntity.X/Y
- MoveSpeedMs → MoveComponent.SpeedMs
- EquippedSkills → CastComponent.EquippedSkills
- MpRegen → CombatComponent 属性字典
- 战斗属性(Hp/MaxHp等) → CombatComponent

### 2. MapMonsterState → 继承 ServerEntity
- InstanceId → ServerEntity.EntityId
- MonsterId/Name → ServerEntity 上的 Monster 特有字段
- X/Y → ServerEntity.X/Y
- 战斗属性 → CombatComponent

### 3. MapNpcState → 继承 ServerEntity
- InstanceId → ServerEntity.EntityId
- NpcId/Name/NpcType → InteractComponent
- InCombat → InteractComponent.InCombat
- 战斗属性 → CombatComponent

### 4. MonsterRuntimeState → 拆分到组件
- AiId/AiType/AiConfig/State/TargetId → AiComponent
- MoveSpeedMs/IsMoving/MoveTargetX/Y/MoveStartTime → MoveComponent
- CheckpointConfirmed/InCombat → MoveComponent + CombatComponent
- 战斗属性 → CombatComponent

### 5. WorldState 统一实体索引
- 三个字典(Players/Monsters/Npcs) → 一个 Entities 字典
- 便捷查询方法保留
- 移动预占系统适配

### 6. Manager 适配
- MonsterManager → 读组件而非直接读状态字段
- NpcManager → 读组件
- PlayerSessionManager → 适配 ServerEntity
- Player/Handlers/*.cs → 适配组件接口

## 验证
- servercsharp/restart.bat 启动验证
- 客户端连接、移动、战斗、NPC 交互全部正常

## 风险
中 — 改动面广，按方法逐个迁移
