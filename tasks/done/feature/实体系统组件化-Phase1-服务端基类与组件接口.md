# 实体系统组件化重构 — Phase 1: 服务端统一实体基类 + 组件接口

## 类型
feature

## 来源
docs/design/实体系统组件化重构.md Phase 1

## 目标
建立服务端 ServerEntity 和组件接口，不改变现有行为。纯新增代码。

## 具体任务

### 1. 新增 ServerEntity.cs
路径: `servercsharp/src/GameServer.GameLogic/Entity/ServerEntity.cs`
- 实体基类，包含 EntityId/Type/MapName/X/Y
- 组件字典 `_components` + GetComponent/AddComponent/HasComponent/GetAllComponents
- 事件系统：`_eventSubscribers` 字典 + FireEvent/SubscribeEvent/UnsubscribeEvent
- FireEvent 按事件类型分发，支持 Handled 短路

### 2. 新增 IEntityComponent.cs + EntityComponentBase.cs
路径: `servercsharp/src/GameServer.GameLogic/Entity/`
- IEntityComponent: OnAttach/OnDetach/OnEvent
- EntityComponentBase: Owner + InterestedEvents 声明 + 自动订阅/取消

### 3. 新增 EntityEventType.cs + EntityEvent.cs
路径: `servercsharp/src/GameServer.GameLogic/Entity/`
- EntityEventType 枚举: Damage/Death/Heal/MoveStart/MoveComplete/CastStart/CastComplete/CastCancel/Aggro/Disengage/Interact/Gather
- EntityEvent: Type/Source/Target/Data/Handled

### 4. 新增 EntityType.cs（扩展版）
路径: `servercsharp/src/GameServer.GameLogic/Entity/EntityType.cs`
- Player=0, Monster=1, Npc=2, Boss=3, Pet=4, Guard=5, GatherNode=6, Portal=7, Chest=8

### 5. 新增 EntityComponentConfig.cs
路径: `servercsharp/src/GameServer.GameLogic/Entity/EntityComponentConfig.cs`
- DefaultComponents 字典：按 EntityType 配置组件组合

### 6. 新增 6 个组件
路径: `servercsharp/src/GameServer.GameLogic/Entity/Components/`
- CombatComponent.cs — 战斗属性 + 战斗状态（从 CombatEntityState 迁移属性字典）
- MoveComponent.cs — 移动速度 + 移动状态
- AiComponent.cs — AI 配置 + AI 状态
- CastComponent.cs — 施法状态
- InteractComponent.cs — 交互配置
- GatherComponent.cs — 采集配置

## 验证
- 编译通过
- 不影响现有功能（新代码，旧代码不动）

## 风险
低 — 纯新增
