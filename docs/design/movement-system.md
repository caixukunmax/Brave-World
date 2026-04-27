# 移动系统设计稿（Movement System Design）

> 版本：V1.1
> 状态：已实现 — 30% 检查点碰撞检测，客户端直接碰撞通知
> 相关文档：`combat-system.md`（§2 碰撞触发）、`combat-system-tdd.md`（§8 碰撞检测）

---

## 1. 概述

大地图采用**基于格子的离散移动**系统。角色移动时经历三个阶段：

```
0% ───────────── 30% ───────────── 70% ───────────── 100%
启动移动          检查点确认         双格区间结束       移动完成
(预占目标格)      (ConfirmMove)      (只占目标格)       (CompleteMove)
                  碰撞检测
```

### 1.1 设计原则

- **服务端权威**：坐标、碰撞判定、战斗触发全部由服务端决定。
- **客户端预测**：正常移动时客户端预测 GridPos，服务端校验后修正。
- **碰撞性移动**：目标格有敌人时仍允许启动移动，30% 时再做碰撞判定。
- **双格区间**：30%~70% 期间角色同时占据起点和终点，用于技能判定。

---

## 2. 核心参数

| 参数 | 值 | 说明 |
|------|-----|------|
| `BaseMoveSpeedMs` | 150 | 玩家默认移动速度（毫秒/格） |
| `MonsterMoveSpeedMs` | 800 | 怪物默认移动速度 |
| `MoveCheckRatio` | 30 | 检查点触发位置（30%） |
| `MoveDualGridStartRatio` | 30 | 双格区间起始 |
| `MoveDualGridEndRatio` | 70 | 双格区间结束 |
| `MonsterAiTickMs` | 500 | 怪物 AI tick 间隔 |

---

## 3. 移动状态机

```
IDLE ──(MoveStart)──► MOVING ──(30% ConfirmMove)──► CONFIRMED ──(100% CompleteMove)──► IDLE
                        │                               │
                        │(碰撞/取消)                     │(碰撞/取消)
                        ▼                               ▼
                     BOUNCE_BACK                     BOUNCE_BACK
```

### 3.1 移动预约系统（MovementReservation）

服务端通过 `WorldState` 维护移动预约，确保格子不被两个实体同时占用：

| 字段 | 类型 | 说明 |
|------|------|------|
| `EntityId` | long | 移动实体 ID |
| `FromX/FromY` | int | 起始格子 |
| `TargetX/TargetY` | int | 目标格子 |
| `StartTimeMs` | long | 移动开始时间 |
| `DurationMs` | int | 移动总时长 |
| `CollisionPending` | bool | 碰撞性移动标志 |

两种预约类型：
- **普通预约**（`TryReserveMove`）：检查目标格空闲，占格。移动正常完成。
- **碰撞预约**（`TryReserveCollisionMove`）：不检查目标格，不占格。30% 时判定碰撞。

---

## 4. 玩家移动流程

### 4.1 正常移动（目标格空闲）

1. **客户端 0%**：`MoveTo()` → 预测 `GridPos = target`，启动 tween 动画，发送 `MoveStartRequest`。
2. **服务端 0%**：`MoveStartHandler` → `TryReserveMove` 成功 → 返回 `MoveResponse(code=Success, durationMs, checkRatio, ...)`。
3. **客户端收到响应**：保存 timing，启动 checkpoint 定时器（`durationMs * checkRatio / 100`）。
4. **客户端 30%**：`OnMoveCheckPoint()` → 检查目标可行走 → 发送 `MoveConfirmRequest`。
5. **服务端 30%**：`MoveConfirmHandler` → `ConfirmMove` → 更新权威坐标到目标格 → `CheckEntityCollision` 检查相邻敌人。
6. **客户端 100%**：tween 完成 → `OnMoveFinished()` → 发送 `MoveCompleteRequest`。
7. **服务端 100%**：`MoveCompleteHandler` → 释放预约，持久化坐标到数据库。

### 4.2 碰撞性移动（目标格有敌人）

1. **客户端 0%**：`MoveTo()` → 检测 `IsBlockedByMonster` → 设 `_collisionMove = true`，**不预测 GridPos**，启动 tween → 发送 `MoveStartRequest`。
2. **服务端 0%**：`MoveStartHandler` → `TryReserveMove` 失败 → `TryReserveCollisionMove`（不占格，标记 `CollisionPending`）→ 返回成功 + timing。
3. **客户端 30%**：`OnMoveCheckPoint()` → 检测目标格有怪物 → `PlayBounceBack()`（弹回动画）→ 发送 `MoveCollisionNotify`（直接通知服务端碰撞，不等回滚）。
4. **服务端收到碰撞通知**：`MoveCollisionHandler` → `CancelMove` + `HasEnemyAt` 校验 → 若有敌人则 `CheckEntityCollision` 触发战斗；若校验失败则发 `MoveCancelNotify` 强制回滚。
5. **客户端收到 `MoveCancelNotify`**：若已弹回（`!IsMoving`）则只做轻量状态同步，不 snap；若仍在移动中则 `RollbackTo()`。

如果 30% 时敌人已移走：客户端检测不到敌人 → 发正常 `MoveConfirmRequest`，服务端 `ConfirmMoveEx` 返回 `Failed` → 发 `MoveCancelNotify`。

### 4.3 服务端拒绝（非碰撞性阻挡）

当目标格被非敌人实体占据时：`TryReserveMove` 失败，`TryReserveCollisionMove` 也无法解决 → 返回 `MoveResponse(code=Forbidden)` → 客户端 `RollbackTo`。

---

## 5. 怪物移动流程

怪物的移动由服务端 `MonsterManager.Tick()` 全权处理（每 500ms tick 一次），无需客户端参与。

### 5.1 正常移动

1. AI 选择下一个格子 → `TryReserveMove` 成功 → 设 `IsMoving = true`，记录 move state。
2. 广播 `MonsterMoveNotify` → 客户端播放怪物移动动画。
3. Tick 到 30% → `ConfirmMove` → 更新权威坐标 → `CheckEntityCollision`。
4. Tick 到 100% → `CompleteMove` → `CheckEntityCollision`。

### 5.2 碰撞性移动

1. AI 选择下一个格子 → `TryReserveMove` 失败 → `TryReserveCollisionMove` → 广播 `MonsterMoveNotify`。
2. Tick 到 30% → `ConfirmMoveEx` 返回 `Collision` → `CancelMove` → 广播 `MonsterMoveCancelNotify`（弹回）→ `CheckEntityCollision` → 开战。

---

## 6. 双格区间（Dual Grid）

30%~70% 期间，移动中的角色同时占据起点和终点。用于：
- **技能判定**：`GetCombatPositions()` 在双格区间返回两个位置，攻击方取最短距离。
- **碰撞检测**：这段时间内角色可以被相邻格子上的敌人锁定。

```
进度 < 30%  → 只占 from 格
进度 30~70% → 占 from + to 两个格
进度 > 70%  → 只占 to 格
```

---

## 7. 客户端动画

### 7.1 正常移动

- Tween 动画：`Quad` 缓动，`EaseType.Out`。
- 时长由服务端 `durationMs` 决定（默认 150ms）。
- 客户端预测 `GridPos` 立即更新。

### 7.2 碰撞弹回（`PlayBounceBack`）

- 30% 时客户端检测到敌人 → 从当前位置弹回原点。
- Tween：`Quad` 缓动，`EaseType.In`，约 100ms。
- 不预测 `GridPos`（碰撞移动期间 `GridPos` 保持在起点）。

### 7.3 服务端取消（`MoveCancelNotify`）

- 服务端发送 rollback 坐标 → 客户端立即 `RollbackTo`（无动画，snap 到原位）。
- 清理所有移动状态（`IsMoving`、`_collisionMove`、timer、tween）。

---

## 8. 协议消息

| 消息 | 方向 | ID | 关键字段 |
|------|------|-----|----------|
| `MoveRequest` | C→S | 324 | `from_x, from_y, to_x, to_y, map_name` |
| `MoveResponse` | S→C | 325 | `code, message, x, y, duration_ms, check_ratio, dual_start_ratio, dual_end_ratio` |
| `MoveConfirmRequest` | C→S | 326 | `target_x, target_y` |
| `MoveCompleteRequest` | C→S | 327 | `target_x, target_y` |
| `MoveCancelNotify` | S→C | 328 | `entity_id, rollback_x, rollback_y` |
| `MoveCollisionNotify` | C→S | 329 | `target_x, target_y`（客户端30%检测到敌人后直接发送） |
| `MonsterMoveNotify` | S→C | 370 | `instance_id, from_x, from_y, to_x, to_y, state, duration_ms` |
| `MonsterMoveCancelNotify` | S→C | 372 | `instance_id, rollback_x, rollback_y` |

---

## 9. 文件索引

| 文件 | 职责 |
|------|------|
| `servercsharp/src/GameServer.Services/World/WorldState.cs` | 移动预约系统（TryReserveMove, TryReserveCollisionMove, ConfirmMoveEx, HasEnemyAt） |
| `servercsharp/src/GameServer.Services/World/CollisionDetector.cs` | 碰撞检测（CheckEntityCollision） |
| `servercsharp/src/GameServer.GameLogic/Player/Handlers/MoveHandler.cs` | 玩家移动请求处理（MoveStart, MoveConfirm, MoveComplete, MoveCollision） |
| `servercsharp/src/GameServer.GameLogic/Monster/MonsterManager.cs` | 怪物移动 + AI tick + 30% 检查点 |
| `servercsharp/src/GameServer.Services/Core/GameConstants.cs` | 移动常量 |
| `clinetcsharp/Scripts/Player.cs` | 客户端移动动画 + 检查点 + 弹回 |
| `clinetcsharp/Scripts/Monster.cs` | 客户端怪物移动动画 |
| `clinetcsharp/Scripts/MonsterManager.cs` | 客户端怪物位置管理 |
