# 实体系统组件化重构 — Phase 3: 协议统一实体消息

## 类型
feature

## 来源
docs/design/实体系统组件化重构.md Phase 3

## 目标
新增统一实体协议（EntityType + 组件列表），为 Phase 4 客户端组件化做准备

## 具体任务

### 1. game.proto 新增实体协议
- EntityType 枚举
- ComponentType 枚举
- CombatComponentData（hp/max_hp/mp/max_mp/attrs）
- MoveComponentData（speed_ms/is_moving/target_x/target_y）
- CastComponentData（equipped_skills/casting_skill_id）
- InteractComponentData（npc_id/npc_type）
- EntitySnapshot（entity_id/entity_type/components/x/y + 组件数据）
- MapEntityListNotify（进入地图时下发）
- EntityCreateNotify（新实体出现）
- EntityDestroyNotify（实体销毁）

### 2. message_id.proto 新增消息 ID
- GAME_ENTITY_LIST_NOTIFY = 410
- GAME_ENTITY_CREATE_NOTIFY = 411
- GAME_ENTITY_DESTROY_NOTIFY = 412

### 3. 服务端适配
- MapService 进入地图时发送 MapEntityListNotify
- MonsterManager 怪物创建/销毁时发送 EntityCreateNotify/EntityDestroyNotify
- NpcManager NPC 创建时发送 EntityCreateNotify

### 4. 客户端适配
- NetworkManager 解析新协议
- 暂时只解析，不影响现有渲染逻辑（Phase 4 再用）

## 验证
- 协议生成无报错
- 服务端编译通过
- 客户端编译通过
- 进入地图时收到 MapEntityListNotify

## 风险
低 — 新增协议，不替换现有消息
