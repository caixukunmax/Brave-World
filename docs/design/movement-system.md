# 角色移动系统设计文档

> 适用范围：玩家移动（Player）与怪物移动（Monster）
> 最后更新：2026-05-11

---

## 1. 设计目标与核心原则

### 1.1 设计目标

- **服务端权威**：所有坐标变更的最终决定权在服务端，客户端只做预测和表现。
- **防穿人**：目标格被其他实体（玩家/怪物/NPC）占据时，移动请求应当被阻止或降级为碰撞移动。
- **延迟碰撞判定**：允许向有敌人的格子发起移动，在移动过程中（30% 检查点）再判定是否触发战斗，避免"走不上去"的僵硬感。
- **视觉平滑**：通过 Tween 动画和提前广播移动通知，让客户端表现流畅；碰撞/取消时通过回弹动画减少突兀感。

### 1.2 核心原则

| 原则 | 说明 |
|------|------|
| 服务端权威 | 坐标以服务端 `WorldState` 为准，客户端预测可被服务端纠正 |
| 预占机制 | 移动发起时先"预占"目标格，防止并发抢占 |
| 双格存在 | 移动中的实体在 30%~70% 进度期间同时存在于原格和目标格（用于战斗位置判定） |
| 检查点确认 | 30% 时执行一次权威校验，确认目标格状态是否仍然合法 |
| NPC 即墙壁 | NPC 格子在普通移动和碰撞移动中均完全阻挡，不触发战斗 |

---

## 2. 服务端移动预占系统（WorldState）

### 2.1 核心数据结构

```csharp
public class MovementReservation
{
    public long   EntityId;              // 实体唯一 ID
    public string MapName;               // 所在地图
    public int    FromX, FromY;          // 起始格
    public int    TargetX, TargetY;      // 目标格
    public long   StartTimeMs;           // 移动开始时间
    public int    DurationMs;            // 移动总时长（毫秒）
    public int    CheckRatio;            // 检查点比例（默认 30）
    public int    DualStartRatio;        // 双格开始比例（默认 30）
    public int    DualEndRatio;          // 双格结束比例（默认 70）
    public bool   Confirmed;             // 是否已通过 30% 检查点
    public bool   Completed;             // 是否已完成移动
    public bool   CollisionPending;      // 是否为碰撞性移动（false=普通，true=碰撞）
}
```

```csharp
// WorldState 中的两个核心容器
ConcurrentDictionary<long, MovementReservation> _moveReservations; // entityId -> reservation
Dictionary<string, HashSet<(int,int)>>          _reservedCells;      // mapName -> 被预占的格子
```

### 2.2 预占 API

#### TryReserveMove —— 标准预占

```csharp
public bool TryReserveMove(long entityId, string mapName,
    int fromX, int fromY, int targetX, int targetY,
    int durationMs, int checkRatio, int dualStartRatio, int dualEndRatio)
```

**执行流程：**
1. 若该实体已有预占，先 `CancelMove` 清理旧预占。
2. 检查 `_reservedCells` 中目标格是否已被其他实体的移动预占。
3. 检查地图上目标格是否已有其他**玩家**、**怪物**或 **NPC**（不含自己）。
4. 全部通过后，创建 `MovementReservation` 并加入 `_moveReservations` 和 `_reservedCells`。

**返回值：** `true` = 预占成功；`false` = 目标格被占。

**关键特性：**
- 预占成功的目标格会被锁定，其他实体无法再预占该格。
- 实体的当前逻辑坐标**不会立即变更**，仍保留在 `FromX/FromY`。

#### TryReserveCollisionMove —— 碰撞性预占

```csharp
public bool TryReserveCollisionMove(long entityId, string mapName,
    int fromX, int fromY, int targetX, int targetY,
    int durationMs, int checkRatio, int dualStartRatio, int dualEndRatio)
```

**执行流程：**
1. NPC 仍然阻挡（NPC 不是敌人，不触发碰撞战斗）。
2. 若该实体已有预占，先 `CancelMove` 清理。
3. 创建 `MovementReservation`，**`CollisionPending = true`**。
4. 加入 `_moveReservations`，但**不加入 `_reservedCells`**。

**返回值：** 几乎总是 `true`（除非目标格是 NPC）。

| 特性 | TryReserveMove | TryReserveCollisionMove |
|------|----------------|-------------------------|
| 目标格被实体占据 | 拒绝 | 允许 |
| 加入 `_reservedCells` | 是 | 否 |
| `CollisionPending` | `false` | `true` |
| NPC 阻挡 | 是 | 是 |
| 用途 | 正常移动 | 撞向敌人触发战斗 |

### 2.3 确认与完成

#### ConfirmMoveEx —— 检查点确认（核心）

```csharp
public enum ConfirmResult { Ok, Collision, Failed }
public ConfirmResult ConfirmMoveEx(long entityId)
```

**普通预约分支（`CollisionPending = false`）：**
1. 再次确认目标格没有被其他实体抢占（防止预约期间有人进入）。
2. 若被抢占 → `CancelMove` + 返回 `Failed`。
3. 若未被占 → `UpdateEntityPosition` 将实体坐标更新到目标格 + `Confirmed = true` + 返回 `Ok`。

**碰撞预约分支（`CollisionPending = true`）：**
1. 检查目标格是否有敌人（玩家或怪物）。
2. 无论结果，都执行 `CancelMove`（碰撞预约不保留）。
3. 有敌人 → 返回 `Collision`（触发战斗）。
4. 无敌人 → 返回 `Failed`（目标格已空，弹回）。

**注意：碰撞性移动在 30% 检查时永远不会成功进入目标格，实体始终"弹回"原格。**

#### CompleteMove —— 移动完成

```csharp
public bool CompleteMove(long entityId)
```

- 清理预占记录（从 `_moveReservations` 和 `_reservedCells` 移除）。
- 标记 `Completed = true`。

#### CancelMove —— 取消移动

```csharp
public void CancelMove(long entityId)
```

- 从 `_moveReservations` 移除记录。
- 从 `_reservedCells` 移除目标格占用（仅普通预约会加入）。

### 2.4 辅助查询

| 方法 | 用途 | 检查内容 |
|------|------|----------|
| `IsOccupied(map, x, y)` | 通用占用检查 | 怪物、玩家、NPC、`_reservedCells` |
| `HasEnemyAt(map, x, y, excludeId)` | 碰撞/战斗检查 | 玩家、怪物（排除自身，不含 NPC） |
| `FindEntityPosition(entityId)` | 查找实体当前坐标 | 遍历所有地图的玩家/怪物 |

---

## 3. 玩家移动流程

### 3.1 协议定义

| 协议 | 方向 | 职责 |
|------|------|------|
| `GameMoveReq` | C->S | 玩家请求移动，携带 From/To/MapName |
| `GameMoveRsp` | S->C | 服务端响应：Success / Forbidden + 坐标 + 时长 |
| `GameMoveConfirmReq` | C->S | 客户端在 30% 检查点发送的确认 |
| `GameMoveCompleteReq` | C->S | 客户端移动动画 100% 完成时发送 |
| `GameMoveCollisionNotify` | C->S | 客户端在 30% 检测到敌人时发送的碰撞通知 |
| `GameMoveCancelNotify` | S->C | 服务端通知客户端回滚到指定坐标 |

### 3.2 服务端处理（MoveHandler）

#### MoveStartHandler —— 移动请求

**流程：**
1. **距离校验**：曼哈顿距离必须恰好为 1（四方向相邻）。
2. **可行走校验**：目标格必须在地图数据中可行走。
3. **施法拦截**：若玩家正在蓄力（`IsCasting`），拒绝移动。
4. **速度计算**：`durationMs = player.MoveSpeedMs ?? BaseMoveSpeedMs`（默认 150ms）。
5. **预占目标格**：调用 `TryReserveMove`。
   - 成功 -> 返回 `MoveResponse { Code=Success, X=toX, Y=toY, DurationMs }`。
   - 失败 -> 调用 `TryReserveCollisionMove`。
     - 碰撞成功 -> 返回 `MoveResponse { Code=Success, X=fromX, Y=fromY, ... }`（**注意：返回的是原格坐标**）。
     - 碰撞也失败 -> 返回 `MoveResponse { Code=Forbidden, X=fromX, Y=fromY }`。

**响应中 X/Y 的语义差异：**
- 普通预占成功：`X/Y = toX/toY`（告诉客户端"你走过去了"）。
- 碰撞性移动：`X/Y = fromX/fromY`（告诉客户端"你被弹回"，但客户端仍会继续播放向目标格移动的动画，到 30% 再判定）。

#### MoveConfirmHandler —— 30% 检查点

**流程：**
1. 调用 `ConfirmMoveEx(entityId)` 获取结果。
2. 结果处理：
   - `Collision` -> 发送 `MoveCancelNotify`（回滚到当前权威坐标）+ 触发 `CheckEntityCollision` 进入战斗。
   - `Failed` -> 发送 `MoveCancelNotify`（回滚到 `FromX/FromY`）+ 触发 `CheckEntityCollision`。
   - `Ok` -> 坐标已更新到目标格 + 触发 `CheckEntityCollision` 检查相邻敌人。

#### MoveCompleteHandler —— 100% 完成

- 调用 `CompleteMove(entityId)` 清理预占。
- 将坐标同步到 `PlayerSessionManager` 中的玩家数据（用于持久化）。

#### MoveCollisionHandler —— 客户端主动碰撞通知

- `CancelMove(entityId)` 取消移动预占。
- **防作弊校验**：通过 `HasEnemyAt` 确认目标格是否真的有敌人。
  - 无敌人 -> 发送 `MoveCancelNotify` 强制回滚到当前权威坐标。
  - 有敌人 -> 触发 `CheckEntityCollision` 进入战斗。

### 3.3 客户端处理（Player.Movement）

#### 输入与本地预测

```csharp
private void HandleInput()
{
    if (IsMoving || _movePending) return;
    // 读取方向键 -> direction
    if (direction != Vector2I.Zero)
        MoveTo(_gridPos + direction);
}
```

**MoveTo 分支：**
- 目标格可行走 -> `BeginPredictedMove(targetGridPos)`
- 目标格被怪物占据 -> `TryStartMonsterCollisionMove(targetGridPos)`
- 目标格被地图阻挡 -> 尝试开箱（若有宝箱）

**BeginPredictedMove（普通移动）：**
1. 保存 `_moveFromPos` 和 `_moveTargetPos`。
2. `_collisionMove = false`。
3. **`_gridPos = targetGridPos`**（本地立即预测更新坐标）。
4. 启动 Tween 动画。
5. 发送 `GameMoveReq`。

**TryStartMonsterCollisionMove（碰撞移动）：**
1. 保存 `_moveFromPos` 和 `_moveTargetPos`。
2. **`_collisionMove = true`**。
3. **不更新 `_gridPos`**（逻辑坐标仍保留在原格，等待动画完成或弹回）。
4. 启动 Tween 动画。
5. 发送 `GameMoveReq`。

**关键差异：** 普通移动在发起时就更新了本地逻辑坐标，碰撞移动则保留原坐标。这意味着如果网络延迟导致服务端拒绝，普通移动需要回滚，而碰撞移动本来就没改坐标。

#### 服务端响应处理（OnMoveResponse）

```csharp
private void OnMoveResponse(Game.MoveResponse rsp)
{
    _movePending = false;

    if (rsp.Code != Success || rsp.DurationMs <= 0)
    {
        // 服务端拒绝 -> 回滚
        RollbackTo(new Vector2I((int)rsp.X, (int)rsp.Y));
        return;
    }

    // 应用服务端计算的时长
    ApplyServerMoveTiming(rsp);
    StartMoveCheckpointTimer();
}
```

#### 30% 检查点（OnMoveCheckPoint）

```csharp
private void OnMoveCheckPoint()
{
    if (_collisionMove)
    {
        // 碰撞移动：检查目标格是否仍有怪物
        if (mm.IsBlockedByMonster(_moveTargetPos))
        {
            PlayBounceBack(_moveFromPos);     // 视觉弹回
            SendMoveCollisionNotify(_moveTargetPos); // 通知服务端
            return;
        }
        SendMoveConfirmRequest(); // 怪物已离开，正常通过
    }
    else
    {
        // 普通移动：检查目标格是否仍可走
        if (!gridManager.IsWalkable(_moveTargetPos))
        {
            RollbackTo(_moveFromPos);
            return;
        }
        SendMoveConfirmRequest();
    }
}
```

#### 移动完成（OnMoveFinished）

```csharp
private void OnMoveFinished()
{
    IsMoving = false;
    if (_collisionMove)
    {
        _gridPos = _moveTargetPos; // 碰撞移动完成时才更新逻辑坐标
        _collisionMove = false;
    }
    Position = UiUtils.GridToWorld(_gridPos, GridSize);
    SendMoveCompleteRequest();
}
```

#### 回滚（RollbackTo / OnMoveCancelReceived）

```csharp
private void OnMoveCancelReceived(Game.MoveCancelNotify notify)
{
    if (notify.EntityId != (ulong)GetInstanceId()) return;
    var rollbackPos = new Vector2I(notify.RollbackX, notify.RollbackY);
    RollbackTo(rollbackPos);
}
```

`RollbackTo` 逻辑：
- 终止当前 Tween。
- 停止检查点计时器。
- 若距离较远（>1.0f），播放平滑回滚动画（0.05s~0.12s）。
- 若距离很近，直接瞬移。
- 重置 `_movePending = false`、`_collisionMove = false`。

#### 角色属性同步与坐标回写约束

- `GameRoleAttrNotify` / `FullRoleInfo` 的职责以角色属性、UI 展示数据同步为主，**不能在本地移动 Tween、待确认状态或回弹状态进行中直接回写 `Position` / `_gridPos`**。
- 原因：连续移动时客户端会提前预测下一格，而服务端 `FullRoleInfo` 中的 `GridX/GridY` 往往仍是上一帧权威格；如果此时直接落地坐标，会把角色瞬间拉回旧格，再被 Tween 拉回目标格，形成轻微抖动。
- 约束：只有在玩家处于稳定态（非 `IsMoving`、非 `_movePending`、非 `_bouncingBack`）时，才允许把 `FullRoleInfo.GridX/GridY` 作为位置同步来源。`GridX/GridY` 必须按权威坐标直接解释，**不能再用“非 0 才算有值”这类客户端哨兵规则过滤**，因为地图坐标 `0` 本身是合法格子。死亡复活、移动取消等强制纠正仍走专用通知流程，不依赖 `FullRoleInfo` 覆盖位置。
- 软校正规则：如果 `FullRoleInfo` 在本地移动进行中到达，客户端应缓存最新权威格子，等玩家回到稳定态后再应用；应用时不再直接瞬移，而是使用一个短时纠偏动画把当前位置拉回权威格，避免停下瞬间再出现一次硬跳。
- 软校正时长：纠偏动画时长按当前位置与权威格目标世界坐标的距离决定，并限制在一个很短的窗口内，只用于消除轻微漂移，不承担大范围回滚职责；大范围错误仍由 `MoveCancelNotify` / `RollbackTo` 处理。

### 3.4 玩家移动状态机

```
[Idle] --输入方向--> [Pending]
                       |
                       |-- 本地预测更新 _gridPos --> 启动 Tween
                       |
                       |-- 发送 MoveReq --> 等待 MoveRsp
                       |
                       v
                  [OnMoveResponse]
                       |
            +----------+----------+
            v                     v
        成功(Success)          失败(Forbidden)
            |                     |
            v                     v
    启动 CheckpointTimer      RollbackTo
            |                     |
            v                     |
    30% --> OnMoveCheckPoint      |
            |                     |
    +-------+-------+             |
    v               v             |
  正常移动       碰撞移动         |
    |               |             |
    v               v             |
 SendConfirm   有敌人？            |
    |         +----+----+         |
    |         v         v         |
    |     PlayBounce   SendConfirm|
    |     + CollisionNotify       |
    |                             |
    v                             v
 OnMoveFinished --> SendComplete  |
            |                     |
            v                     v
         [Idle]                [Idle]
```

---

## 4. 怪物移动流程

### 4.1 协议定义

| 协议 | 方向 | 职责 |
|------|------|------|
| `GameMonsterMoveNotify` | S->C | 服务端广播怪物开始移动 |
| `GameMonsterMoveCancelNotify` | S->C | 服务端广播怪物移动取消/弹回 |

**注意：怪物没有向服务端发送移动请求/确认/完成的协议。所有怪物移动完全由服务端 AI Tick 驱动。**

### 4.2 服务端处理（MonsterManager.Tick）

Tick 间隔：`MonsterAiTickMs = 500ms`。

#### 阶段 A：处理正在移动的怪物

```csharp
if (m.IsMoving)
{
    long elapsed = now - m.MoveStartTime;
    int moveDuration = m.MoveSpeedMs > 0 ? m.MoveSpeedMs : DefaultMonsterMoveSpeedMs; // 默认 800ms

    // --- 30% 检查点 ---
    if (!m.CheckpointConfirmed && elapsed >= moveDuration * MoveCheckRatio / 100)
    {
        // 碰撞性移动 -> ConfirmMoveEx -> Collision/Failed -> 取消移动
        // 普通移动 -> ConfirmMove -> true/false -> 确认成功或取消移动
    }

    // --- 100% 移动完成 ---
    if (elapsed >= moveDuration)
    {
        CompleteMove(instanceId);
        m.X = m.MoveTargetX;
        m.Y = m.MoveTargetY;
        m.IsMoving = false;
        m.CheckpointConfirmed = false;
        CheckEntityCollision(instanceId, mapName, m.X, m.Y);
    }

    continue; // 移动中的怪物不执行 AI 决策
}
```

**关键规则：**
- 移动中的怪物**不执行 AI 决策**（`continue`）。
- 30% 检查点只做一次（`CheckpointConfirmed` 标志）。
- 碰撞性移动在 30% 时必定被取消（要么触发战斗，要么弹回）。

#### 阶段 B：AI 决策

```csharp
var next = m.Mode == MonsterAiMode.Combat
    ? RunCombatBehavior(m, mapName, players)
    : RunOverworldBehavior(m, mapName, players);
```

`Mode` 是计算属性：`InCombat ? Combat : Overworld`。

#### 阶段 C：战斗中怪物的 0% 预判碰撞

```csharp
if (m.InCombat && _mapService.World.HasEnemyAt(mapName, nx, ny, instanceId))
{
    _mapService.CheckEntityCollision(instanceId, mapName, m.X, m.Y);
    continue;
}
```

**设计意图：** 已入战的怪物追击目标时，若目标格已有敌人，直接在当前格触发碰撞，**不走"移动->30%->弹回"流程**，避免视觉抖动。

#### 阶段 D：预占目标格

```csharp
bool reserved = _mapService.World.TryReserveMove(...);
if (reserved)
{
    m.IsMoving = true;
    m.MoveTargetX = nx;
    m.MoveTargetY = ny;
    m.MoveStartTime = now;
    movedMonsters.Add((instanceId, m.X, m.Y, nx, ny, m.State, durationMs));
}
else
{
    bool collisionMove = _mapService.World.TryReserveCollisionMove(...);
    if (collisionMove)
    {
        m.IsMoving = true;
        m.MoveTargetX = nx;
        m.MoveTargetY = ny;
        m.MoveStartTime = now;
        movedMonsters.Add((instanceId, m.X, m.Y, nx, ny, m.State, durationMs));
    }
}
```

#### 阶段 E：广播通知

```csharp
// 广播 MonsterMoveNotify（所有发起移动的怪物）
foreach (var (id, fx, fy, tx, ty, state, durationMs) in movedMonsters)
{
    var notify = new PGame.MonsterMoveNotify
    {
        InstanceId = (uint)id, FromX = fx, FromY = fy, ToX = tx, ToY = ty,
        State = state, DurationMs = durationMs,
    };
    BroadcastToMap(mapName, GameMonsterMoveNotify, notify);
}

// 广播 MonsterMoveCancelNotify（所有移动被取消的怪物）
foreach (var (id, rollbackX, rollbackY) in cancelledMonsters)
{
    var cancelNotify = new PGame.MonsterMoveCancelNotify
    {
        InstanceId = (uint)id, RollbackX = rollbackX, RollbackY = rollbackY,
    };
    BroadcastToMap(mapName, GameMonsterMoveCancelNotify, cancelNotify);
}
```

### 4.3 AI 行为对比

| 维度 | Overworld (`PatrolChaseBehavior`) | Combat (`CombatBehaviorBase`) |
|------|-----------------------------------|-------------------------------|
| 触发条件 | `InCombat = false` | `InCombat = true` |
| 目标选择 | 仇恨范围内最近玩家 | 锁定 `TargetId` 或最近玩家 |
| 停止距离 | 曼哈顿距离 <= 1 | `engageRange`（近战=1，远程读取配置） |
| 移动间隔 | `MoveIntervalMs`（默认 2000ms） / `ChaseIntervalMs`（默认 500ms） | `ChaseIntervalMs`（默认 500ms） |
| 0% 预判碰撞 | 无 | 有（`HasEnemyAt`） |
| 状态值 | `patrol` / `chase` / `return` / `idle` | `combat_chase` / `combat_hold` / `combat` |

**PatrolChaseBehavior 追击逻辑：**

```csharp
if (target != null)
{
    m.TargetId = target.AccountId;
    int targetDist = Pathfind.Manhattan(m.X, m.Y, target.GridX, target.GridY);
    // 已经贴身，停止移动，避免反复尝试进入玩家格导致弹回
    if (targetDist <= 1)
    {
        m.State = "idle";
        return null;
    }
    if (m.LastMoveTime == 0 || (now - m.LastMoveTime) >= (cfg.ChaseIntervalMs ?? 500))
    {
        var next = Pathfind.BfsNextStep(m.X, m.Y, target.GridX, target.GridY, mapName, _mapData, isBlocked);
        if (next != null) { m.State = "chase"; m.LastMoveTime = now; return next; }
    }
    m.State = "idle";
    return null;
}
```

**CombatBehaviorBase 追击逻辑：**

```csharp
int targetDist = Pathfind.Manhattan(m.X, m.Y, target.GridX, target.GridY);
int engageRange = Math.Max(1, ResolveEngageRange(m));
if (targetDist <= engageRange)
{
    m.State = HoldState;  // 如 "combat_hold"
    return null;          // 已在接战距离内，停止移动
}
// ... BFS 追击
```

### 4.4 客户端处理（Monster.cs / MonsterManager.cs）

#### 接收 MonsterMoveNotify

```csharp
// MapManager 接收网络消息 -> 调用 MonsterManager.OnMonsterMove
public void OnMonsterMove(uint instanceId, Vector2I from, Vector2I to, string state, int durationMs)
{
    var m = _monsters.Find(x => x.InstanceId == instanceId);
    if (m == null) return;

    _monsterPositions.Add(from);           // 保留原位置（移动中仍算被占据）
    _monsterReservedPositions.Add(to);     // 预留目标位置
    m.CurrentState = state;

    float durationSec = durationMs > 0 ? durationMs / 1000.0f : 0.15f;
    m.MoveTo(to, durationSec);             // 启动 Tween 动画
}
```

#### Monster.MoveTo

```csharp
public override void MoveTo(Vector2I targetGridPos, float duration = 0.15f)
{
    var fromGridPos = new Vector2I(_gridX, _gridY);
    _pendingGridPos = targetGridPos;       // 标记目标位置为"预留"
    base.MoveTo(targetGridPos, duration);  // 调用 EntityBase 的 Tween 动画
    if (_currentTween != null)
    {
        _currentTween.Finished += () =>
        {
            _gridX = targetGridPos.X;
            _gridY = targetGridPos.Y;
            _pendingGridPos = null;
            MoveVisualCompleted?.Invoke(this, fromGridPos, targetGridPos);
        };
    }
}
```

#### 接收 MonsterMoveCancelNotify

```csharp
public void OnMonsterMoveCancel(Game.MonsterMoveCancelNotify notify)
{
    var m = _monsters.Find(x => x.InstanceId == notify.InstanceId);
    if (m == null) return;

    var rollbackPos = new Vector2I(notify.RollbackX, notify.RollbackY);
    if (m.PendingGridPos.HasValue)
        _monsterReservedPositions.Remove(m.PendingGridPos.Value);
    _monsterPositions.Add(rollbackPos);

    if (m.IsMoving)
        m.PlayBounceBack(rollbackPos);     // 播放回弹动画
    else
        m.RollbackTo(rollbackPos);         // 直接瞬移
}
```

#### MoveVisualCompleted 回调

```csharp
private void OnMonsterMoveVisualCompleted(Monster monster, Vector2I fromGridPos, Vector2I targetGridPos)
{
    _monsterPositions.Remove(fromGridPos);           // 释放原位置
    _monsterPositions.Add(targetGridPos);            // 占据新位置
    _monsterReservedPositions.Remove(targetGridPos); // 清除预留
}
```

---

## 5. 碰撞检测与战斗触发

### 5.1 CheckEntityCollision

**文件：** `servercsharp/src/GameServer.Services/World/CollisionDetector.cs`

```csharp
public void CheckEntityCollision(long entityId, string mapName, int x, int y, MapState map)
{
    bool isPlayer = map.Players.ContainsKey(entityId);
    if (isPlayer)
    {
        // 玩家 -> 检查周围怪物（曼哈顿距离 <= 1）
        foreach (var (instanceId, m) in map.Monsters)
        {
            if (instanceId == entityId) continue;
            int dist = Math.Abs(m.X - x) + Math.Abs(m.Y - y);
            if (dist <= 1)
                _eventBus.Emit("CollisionDetected", (entityA: entityId, entityB: instanceId, mapName));
        }
        // 玩家 -> 检查周围 NPC（不触发战斗，触发交互）
        foreach (var (npcId, npc) in map.Npcs)
        {
            int dist = Math.Abs(npc.X - x) + Math.Abs(npc.Y - y);
            if (dist <= 1)
                _eventBus.Emit("NpcCollisionDetected", (playerId: entityId, npcInstanceId: npcId, mapName));
        }
    }
    else
    {
        // 怪物 -> 检查周围玩家（曼哈顿距离 <= 1）
        foreach (var (accountId, p) in map.Players)
        {
            if (accountId == entityId) continue;
            int dist = Math.Abs(p.GridX - x) + Math.Abs(p.GridY - y);
            if (dist <= 1)
                _eventBus.Emit("CollisionDetected", (entityA: entityId, entityB: accountId, mapName));
        }
    }
}
```

**检测规则：**
- 范围：**曼哈顿距离 <= 1**（四邻域 + 自身格）。
- 玩家-玩家：不检测。
- 怪物-怪物：不检测。
- NPC：不触发战斗，触发交互事件。

### 5.2 碰撞触发战斗（CombatManager.OnCollision）

```csharp
public void OnCollision(long entityA, long entityB, Dictionary<string, MapState> maps)
{
    if (_relations.HasActiveRelation(entityA, entityB)) return; // 已在战斗中则跳过

    var combatId = AllocCombatId();
    _entityCombatId[entityA] = combatId;
    _entityCombatId[entityB] = combatId;

    _relations.CreateRelation(entityA, entityB);
    _relations.CreateRelation(entityB, entityA);

    // 先手普攻
    if (ctxA.State == "IDLE" || ctxB.State == "IDLE")
    {
        ExecuteFirstStrike(entityA, entityB, maps);
        ExecuteFirstStrike(entityB, entityA, maps);
    }

    // 设置战斗状态
    _relations.SetState(entityA, "COMBAT");
    _relations.SetState(entityB, "COMBAT");
    SetPlayerInCombat(entityA, true, maps);  // 设置 InCombat = true
    SetPlayerInCombat(entityB, true, maps);

    SendCombatStartNotify(entityA, entityB, maps);
}
```

**怪物进入战斗后的影响：**
- `m.InCombat = true`
- `m.TargetId = attackerId`
- `m.State = "combat"`
- 下一 tick 开始执行 `CombatBehaviorBase`（而非 `PatrolChaseBehavior`）

### 5.3 触发时机汇总

| 场景 | 触发位置 | 调用条件 |
|------|----------|----------|
| 战斗中怪物 0% 预判 | `MonsterManager.Tick` | `m.InCombat && HasEnemyAt(target)` |
| 普通移动 30% 确认失败 | `MonsterManager.Tick` | `ConfirmMove` 返回 false |
| 碰撞移动 30% 检测到敌人 | `MonsterManager.Tick` | `ConfirmMoveEx` 返回 `Collision` |
| 移动 100% 完成 | `MonsterManager.Tick` | `elapsed >= moveDuration` |
| 玩家 30% 碰撞通知 | `MoveCollisionHandler` | 客户端发送 `MoveCollisionNotify` |
| 玩家 30% 确认失败 | `MoveConfirmHandler` | `ConfirmMoveEx` 返回 `Collision`/`Failed` |
| 玩家 30% 确认成功 | `MoveConfirmHandler` | `ConfirmMoveEx` 返回 `Ok` |

---

## 6. 双格机制

### 6.1 设计目的

解决"移动中的实体到底在哪一格"的问题。在 30%~70% 的进度区间内，实体同时被认为处于**原格**和**目标格**，使得范围技能、战斗位置判定等场景不会遗漏正在移动的实体。

### 6.2 实现

```csharp
public List<(string mapName, int x, int y)> GetCombatPositions(long entityId)
{
    var elapsed = Environment.TickCount64 - res.StartTimeMs;
    var progress = (int)(elapsed * 100 / res.DurationMs);

    if (progress < res.DualStartRatio)       // < 30%：视为还在原格
    {
        result.Add((res.MapName, res.FromX, res.FromY));
    }
    else if (progress >= res.DualEndRatio)   // >= 70%：视为已完全在新格
    {
        result.Add((res.MapName, res.TargetX, res.TargetY));
    }
    else                                      // 30%-70%：双格区间
    {
        result.Add((res.MapName, res.FromX, res.FromY));
        result.Add((res.MapName, res.TargetX, res.TargetY));
    }
}
```

### 6.3 双格区间图示

```
0%          30% (DualStart)       70% (DualEnd)        100%
|              |                       |                  |
|   原格       |      双格区间         |      目标格       |
|   (From)     |   (From + Target)    |    (Target)      |
|              |                       |                  |
|  开始移动    |  检查点确认           |                  |  移动完成
|  (预占Target)|  (ConfirmMove)        |                  |  (CompleteMove)
```

---

## 7. 移动取消与回滚

### 7.1 服务端取消场景

| 场景 | 取消方法 | 广播通知 | 回滚坐标 |
|------|----------|----------|----------|
| 普通移动 30% 目标格被抢占 | `CancelMove` | `MonsterMoveCancelNotify` | `m.X/m.Y`（原格） |
| 碰撞移动 30% 检测到敌人 | `CancelMove` (ConfirmMoveEx 内部) | `MonsterMoveCancelNotify` | `m.X/m.Y`（原格） |
| 碰撞移动 30% 无敌人 | `CancelMove` (ConfirmMoveEx 内部) | `MonsterMoveCancelNotify` | `m.X/m.Y`（原格） |
| 客户端主动发送碰撞通知 | `CancelMove` | `MoveCancelNotify` (玩家) | 当前权威坐标 |
| 实体死亡或眩晕 | `CancelMove` | 无 | - |
| 新移动覆盖旧移动 | `CancelMove` (TryReserveMove 开头) | 无 | - |

### 7.2 客户端回滚表现

**怪物回滚（Monster）：**
- 收到 `MonsterMoveCancelNotify` 时，若怪物仍在移动中（`IsMoving = true`），调用 `PlayBounceBack(originPos, 0.12f)` 播放平滑回弹动画。
- 若已完成移动（`IsMoving = false`），直接 `RollbackTo(pos)` 瞬移。

**玩家回滚（Player）：**
- 收到 `MoveCancelNotify` 时，若正在移动，调用 `RollbackTo(pos)`。
- `RollbackTo` 内部根据距离选择平滑动画或瞬移。
- 碰撞移动的 30% 检查点检测到敌人时，客户端主动调用 `PlayBounceBack(_moveFromPos)` 并发送 `MoveCollisionNotify`。

---

## 8. 脱战机制

### 8.1 脱战触发条件

**文件：** `servercsharp/src/GameServer.GameLogic/Combat/DisengageSystem.cs`

| 路径 | 距离条件 | 时间条件 | 说明 |
|------|----------|----------|------|
| **安全脱离** | `dist > 5` (`DisengageDistanceA`) | 连续 **2秒** 无伤害 (`DisengageNoDamageTimeT1`) + 等待 **3秒** (`DisengageTimeS1`) | 常规脱战 |
| **彻底逃离** | `dist > 10` (`DisengageDistanceB`) | 等待 **2秒** (`DisengageTimeS2`) | 远距离直接脱战 |

### 8.2 脱战后的状态重置

```csharp
// CombatManager 清理
relations.RemoveRelation(relationId);
_entityCombatId.Remove(attackerId);
_entityCombatId.Remove(targetId);

// 怪物脱战
monsterRegistry.OnDisengage(instanceId);
// -> m.InCombat = false
// -> m.TargetId = null
// -> m.State = "return"
// -> m.LastMoveTime = 0

// 玩家脱战
p.InCombat = false;
p.Buffs.ClearOnDisengage();
// 发送 CombatEndNotify
```

### 8.3 脱战对移动的影响

- 怪物脱战后 `InCombat = false`，下一 tick 开始执行 `PatrolChaseBehavior`（Overworld 模式）。
- `PatrolChaseBehavior` 看到 `State == "return"` 且距离出生点较远时，会执行返回逻辑（BFS 寻路回出生点）。

---

## 9. 配置常量汇总

**文件：** `servercsharp/src/GameServer.Services/Core/GameConstants.cs`

| 常量名 | 值 | 说明 |
|--------|-----|------|
| `BaseMoveSpeedMs` | `150` | 玩家基础移动耗时（毫秒/格） |
| `DefaultMonsterMoveSpeedMs` | `800` | 怪物默认移动耗时（毫秒/格） |
| `MoveCheckRatio` | `30` | 移动检查点比例（30% 时触发 Confirm） |
| `MoveDualGridStartRatio` | `30` | 双格占用起始比例 |
| `MoveDualGridEndRatio` | `70` | 双格占用结束比例 |
| `MonsterAiTickMs` | `500` | 怪物 AI Tick 间隔（毫秒） |
| `DisengageDistanceA` | `5` | 安全脱离距离（格） |
| `DisengageNoDamageTimeT1` | `2` | 安全脱离所需无伤害时间（秒） |
| `DisengageTimeS1` | `3` | 安全脱离等待时间（秒） |
| `DisengageDistanceB` | `10` | 彻底逃离距离（格） |
| `DisengageTimeS2` | `2` | 彻底逃离等待时间（秒） |

---

## 10. 已知问题与设计陷阱

### 10.1 怪物 Overworld 追击抖动（已修复）

**问题：** `PatrolChaseBehavior` 未检查接战距离，当怪物走到与玩家相邻格（曼哈顿距离=1）后，BFS 仍返回玩家所在格作为下一步；`TryReserveMove` 因玩家占据目标格而失败，降级为 `TryReserveCollisionMove`；30% 检查点检测到碰撞后取消移动并弹回；下一 tick 重复，导致无限抖动。

**修复：** 在 `PatrolChaseBehavior` 中加入 `targetDist <= 1` 时提前返回 `null`，停止移动。

```csharp
if (targetDist <= 1)
{
    m.State = "idle";
    return null;
}
```

### 10.2 玩家预测坐标与碰撞移动不一致

**问题：** 普通预测移动在 `BeginPredictedMove` 中立即更新了 `_gridPos`，但碰撞移动在发起时**不更新** `_gridPos`。如果服务端对普通移动的响应延迟较高，玩家在视觉上已经走到了目标格，但服务端权威坐标仍在原格；此时若被其他系统查询坐标，会产生不一致。

**缓解：** `RollbackTo` 可以在收到服务端拒绝时快速修正，但延迟期间的不一致无法完全避免。

### 10.3 怪物移动取消时序竞争

**问题：** `MonsterMoveCancelNotify` 到达客户端时，怪物的 Tween 动画可能刚好完成（`!IsMoving`）。此时客户端直接调用 `RollbackTo` 瞬移，可能出现"怪物已经走到目标格又突然跳回原格"的视觉跳跃。

**缓解：** 客户端在 `OnMonsterMoveCancel` 中区分了 `IsMoving` 状态，但仍存在时序窗口。

### 10.4 BFS 目标格排除陷阱

**问题：** `PatrolChaseBehavior` 和 `CombatBehaviorBase` 的 `isBlocked` 回调都显式排除了目标坐标（玩家所在格），否则 BFS 永远到不了目标。这个设计的副作用是：BFS 返回的"下一步"可能是玩家格本身（当怪物与玩家相邻时），必须由调用方（`targetDist <= 1` 检查）兜底阻止。

**注意：** 如果任何新的行为逻辑遗漏了这个距离检查，抖动 bug 会复现。

### 10.5 AI Tick 间隔与移动时长的错位

**问题：** 怪物 AI Tick 间隔为 500ms，怪物移动时长为 800ms。这意味着一个怪物在移动期间会跳过 1 次 AI Tick。如果玩家在怪物移动期间走位，怪物要到下一次 tick 才能重新寻路，可能产生"反应迟钝"的感觉。

### 10.6 碰撞移动的双向不一致

**问题：** 玩家向怪物发起碰撞移动时，客户端主动检测 `IsBlockedByMonster` 并标记 `_collisionMove = true`。但如果怪物恰好在玩家发起移动后、30% 检查点前离开目标格，客户端会发送 `MoveConfirmRequest` 而非 `MoveCollisionNotify`，服务端 `ConfirmMoveEx` 会返回 `Ok`（因为目标格已空），玩家将"穿过"原本有怪物的格子。这在当前设计中是被允许的（怪物主动让开），但可能不符合预期。

---

## 附录：关键代码文件索引

| 文件路径 | 职责 |
|----------|------|
| `servercsharp/src/GameServer.Services/World/WorldState.cs` | 移动预占、确认、完成、取消 |
| `servercsharp/src/GameServer.Services/World/CollisionDetector.cs` | 碰撞检测、EventBus 发布 |
| `servercsharp/src/GameServer.GameLogic/Player/Handlers/MoveHandler.cs` | 玩家移动请求/确认/完成/碰撞处理 |
| `servercsharp/src/GameServer.GameLogic/Monster/MonsterManager.cs` | 怪物 AI Tick、移动调度、广播 |
| `servercsharp/src/GameServer.GameLogic/Monster/AI/PatrolChaseBehavior.cs` | Overworld 巡逻+追击行为 |
| `servercsharp/src/GameServer.GameLogic/Monster/AI/CombatBehaviorBase.cs` | 战斗态行为基类 |
| `servercsharp/src/GameServer.GameLogic/Combat/CombatManager.cs` | 战斗触发、先手普攻、入战状态 |
| `servercsharp/src/GameServer.GameLogic/Combat/DisengageSystem.cs` | 脱战判定 |
| `clinetcsharp/Scripts/Player.Movement.cs` | 玩家输入、本地预测、Tween 动画 |
| `clinetcsharp/Scripts/Player.Movement.Network.cs` | 玩家移动网络交互、检查点 |
| `clinetcsharp/Scripts/Player.Movement.Recovery.cs` | 玩家回滚、取消处理 |
| `clinetcsharp/Scripts/Monster.cs` | 怪物移动动画、回弹、Rollback |
| `clinetcsharp/Scripts/MonsterManager.cs` | 怪物位置管理、网络消息处理 |
| `clinetcsharp/Scripts/EntityBase.cs` | 实体基类（MoveTo、RollbackTo） |
