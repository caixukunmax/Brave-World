# 玩家自动战斗与一次性优先技能实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让玩家在战斗中默认自动施法，支持 SkillBar 单点设置一次性优先技能、双点立即释放并中断当前施法。

**Architecture:** 复用服务端现有的 `PreferredSkillId` 为一次性优先标记；`TickMonsterSkills` 扩展为处理玩家自动战斗；`CastRequest` 新增 `interrupt` 字段支持双点中断；客户端 SkillBar 区分单点/双点并高亮优先技能。

**Tech Stack:** C# (.NET 8), Godot 4, Protocol Buffers, gRPC-style TCP messaging.

## Global Constraints

- 服务端为战斗权威；所有 CD、MP、射程校验必须在服务端完成。
- 客户端只做输入和表现，不预测自动战斗。
- 优先技能为一次性：成功释放一次后服务端清零。
- 自动战斗始终开启（进入战斗后无法关闭）。
- 双点判定使用 Godot `InputEventMouseButton.DoubleClick`。

---

## File Mapping

| File | Responsibility |
|------|----------------|
| `protocols/proto/game.proto` | 在 `CastRequest` 中增加 `interrupt` 字段。 |
| `protocols/scripts/build_proto.ts` | 已存在，用于重新生成 C# 协议代码。 |
| `servercsharp/src/GameServer.GameLogic/Combat/SkillPipeline.cs` | 新增 `InterruptCast(long entityId)` 方法。 |
| `servercsharp/src/GameServer.GameLogic/Combat/CombatManager.cs` | 玩家自动战斗、优先技能清零、`CastRequest` 中断处理。 |
| `servercsharp/src/GameServer.GameLogic/Player/Handlers/SetPreferredSkillHandler.cs` | 现有处理器，基本无需改动（语义已变为一次性）。 |
| `clinetcsharp/Scripts/NetworkManager.cs` | 新增 `SetPreferredSkillResponse` 事件。 |
| `clinetcsharp/Scripts/NetworkManager.Dispatch.Gameplay.cs` | 在 `HandleSetPreferredSkillResponse` 中触发事件。 |
| `clinetcsharp/Scripts/SkillBar.cs` | 单点/双点交互、高亮显示、订阅网络事件。 |

---

## Task 1: Protocol — CastRequest 增加 interrupt 字段

**Files:**
- Modify: `protocols/proto/game.proto:666-669`
- Generated: `protocols/gen/*.cs` and server copied files

**Interfaces:**
- Consumes: existing `CastRequest`
- Produces: `CastRequest.Interrupt` boolean property in generated C#

- [ ] **Step 1: 修改 proto 定义**

```protobuf
message CastRequest {
  uint32 skill_id = 1;
  uint64 target_id = 2;
  bool   interrupt = 3;  // true = 中断当前施法/后摇并立即释放
}
```

- [ ] **Step 2: 重新生成协议代码**

Run:
```bash
cd protocols && npm run build
```

Expected: all `.proto` files compile successfully; generated C# files are updated in `clinetcsharp/Scripts/protos/` and `servercsharp/src/GameServer.Proto/generated/` (these directories are gitignored, so only the `.proto` source is committed).

- [ ] **Step 3: Commit**

```bash
git add protocols/proto/game.proto
git commit -m "feat(proto): add interrupt flag to CastRequest"
```

---

## Task 2: Server — SkillPipeline 增加 InterruptCast

**Files:**
- Modify: `servercsharp/src/GameServer.GameLogic/Combat/SkillPipeline.cs`

**Interfaces:**
- Consumes: `CombatContext` from `_relations`
- Produces: `public void InterruptCast(long entityId)`

- [ ] **Step 1: 添加 InterruptCast 方法**

Locate `StartCast` in `SkillPipeline.cs`. Add immediately after it:

```csharp
public void InterruptCast(long entityId)
{
    var ctx = CombatManager!.RelationsMgr.Contexts.GetValueOrDefault(entityId);
    if (ctx == null) return;

    if (ctx.SubState == "CASTING" || ctx.SubState == "POST_CAST")
    {
        ctx.SubState = "NONE";
        ctx.CastSkillId = null;
        ctx.CastEndTime = null;
        ctx.PostCastEndTime = null;
    }
}
```

- [ ] **Step 2: 编译服务端**

Run:
```bash
cd servercsharp && dotnet build src/GameServer/GameServer.csproj
```

Expected: build succeeds with no errors.

- [ ] **Step 3: Commit**

```bash
git add servercsharp/src/GameServer.GameLogic/Combat/SkillPipeline.cs
git commit -m "feat(combat): add InterruptCast to SkillPipeline"
```

---

## Task 3: Server — 玩家自动战斗

**Files:**
- Modify: `servercsharp/src/GameServer.GameLogic/Combat/CombatManager.cs:496-567`

**Interfaces:**
- Consumes: `CombatContext`, `SelectSkill`, `RequestCast`
- Produces: players auto-cast via existing `RequestCast` path

- [ ] **Step 1: 移除怪物-only 判断**

Find `TickMonsterSkills` around line 514. Change:

```csharp
// 只处理怪物自动施法（玩家由手动 CastRequest 驱动）
if (entityId >= CombatConstants.MonsterIdThreshold)
{
    int skillId = SelectSkillWithLog(ctx, entityId, maps);
    if (skillId > 0)
    {
        RequestCast(entityId, skillId, maps);
    }
}
```

To:

```csharp
// 处理所有战斗实体自动施法（玩家 + 怪物）
int skillId = SelectSkillWithLog(ctx, entityId, maps);
if (skillId > 0)
{
    RequestCast(entityId, skillId, maps);
}
```

Also update the method docstring/comment from "怪物自动施法" to "战斗实体自动施法".

- [ ] **Step 2: 编译并运行服务端测试**

Run:
```bash
cd servercsharp && dotnet build src/GameServer/GameServer.csproj
```

Expected: build succeeds.

- [ ] **Step 3: Commit**

```bash
git add servercsharp/src/GameServer.GameLogic/Combat/CombatManager.cs
git commit -m "feat(combat): enable auto-cast for players"
```

---

## Task 4: Server — 一次性优先技能清零

**Files:**
- Modify: `servercsharp/src/GameServer.GameLogic/Combat/CombatManager.cs:569-617` and `:620-643`

**Interfaces:**
- Consumes: `ctx.PreferredSkillId`, `ctx.SkillPool`
- Produces: `ClearPreferredSkillIfCast(long entityId, int skillId)` helper

- [ ] **Step 1: 添加清零辅助方法**

Add a private helper in `CombatManager`:

```csharp
private void ClearPreferredSkillIfCast(long entityId, int skillId, Dictionary<string, MapState> maps)
{
    var ctx = _relations.Contexts.GetValueOrDefault(entityId);
    if (ctx == null || ctx.PreferredSkillId != skillId) return;

    ctx.PreferredSkillId = 0;

    // 同步回 MapPlayerState
    string? mapName = SkillPipeline.GetEntityMapName(entityId, maps);
    if (mapName != null && maps.TryGetValue(mapName, out var map))
    {
        if (map.Players.TryGetValue(entityId, out var p))
            p.PreferredSkillId = 0;
    }
}
```

- [ ] **Step 2: 在 RequestCast 成功后清零**

In `RequestCast`, after `_pipeline.Cast(skillId, entityId, maps)` returns "SUCCESS" or "PENDING":

```csharp
private void RequestCast(long entityId, int skillId, Dictionary<string, MapState> maps)
{
    var result = _pipeline.Cast(skillId, entityId, maps);
    if (result == "SUCCESS" || result == "PENDING")
    {
        ClearPreferredSkillIfCast(entityId, skillId, maps);
        // existing logging...
    }
    // ... rest unchanged
}
```

- [ ] **Step 3: 在 HandleCastRequest 成功后清零**

In `HandleCastRequest`, after `_pipeline.Cast(skillId, playerId, maps)` returns "SUCCESS" or "PENDING":

```csharp
var result = _pipeline.Cast(skillId, playerId, maps);
if (result == "SUCCESS" || result == "PENDING")
{
    ClearPreferredSkillIfCast(playerId, skillId);
    return new PGame.CastResponse { Success = true };
}
```

- [ ] **Step 4: 编译并测试**

Run:
```bash
cd servercsharp && dotnet build src/GameServer/GameServer.csproj
```

Expected: build succeeds.

- [ ] **Step 5: Commit**

```bash
git add servercsharp/src/GameServer.GameLogic/Combat/CombatManager.cs
git commit -m "feat(combat): clear one-shot preferred skill after cast"
```

---

## Task 5: Server — CastRequest 支持中断

**Files:**
- Modify: `servercsharp/src/GameServer.GameLogic/Combat/CombatManager.cs:620-643`

**Interfaces:**
- Consumes: `CastRequest.Interrupt`, `SkillPipeline.InterruptCast`
- Produces: `CastResponse` with success/error

- [ ] **Step 1: 修改 HandleCastRequest 签名/解析**

The handler receives raw bytes; parse the request and use `Interrupt`:

```csharp
public PGame.CastResponse HandleCastRequest(long playerId, int skillId, bool interrupt, long? targetId, Dictionary<string, MapState> maps)
{
    var ctx = _relations.Contexts.GetValueOrDefault(playerId);
    if (ctx == null || ctx.State != "COMBAT")
        return new PGame.CastResponse { Success = false, Error = "not_in_combat" };

    if (ctx.SubState == "CASTING" || ctx.SubState == "POST_CAST")
    {
        if (!interrupt)
            return new PGame.CastResponse { Success = false, Error = ctx.SubState == "CASTING" ? "already_casting" : "post_cast" };

        _pipeline.InterruptCast(playerId);
    }

    if (!ctx.SkillPool.Contains(skillId))
        return new PGame.CastResponse { Success = false, Error = "invalid_skill" };

    var result = _pipeline.Cast(skillId, playerId, maps);

    if (result == "SUCCESS" || result == "PENDING")
    {
        ClearPreferredSkillIfCast(playerId, skillId, maps);
        return new PGame.CastResponse { Success = true };
    }
    if (result == "MISS")
    {
        ClearPreferredSkillIfCast(playerId, skillId, maps);
        return new PGame.CastResponse { Success = true };
    }

    return new PGame.CastResponse { Success = false, Error = "cast_failed" };
}
```

- [ ] **Step 2: 更新调用处解析 Interrupt**

Find the handler that calls `HandleCastRequest` (likely `CastHandler.cs`). Update it to parse `CastRequest.Interrupt` and pass it:

```csharp
var req = PGame.CastRequest.Parser.ParseFrom(data);
var response = _combatManager.HandleCastRequest(
    accountId,
    (int)req.SkillId,
    req.Interrupt,
    req.TargetId == 0 ? null : (long?)req.TargetId,
    maps);
```

- [ ] **Step 3: 编译并测试**

Run:
```bash
cd servercsharp && dotnet build src/GameServer/GameServer.csproj
```

Expected: build succeeds.

- [ ] **Step 4: Commit**

```bash
git add servercsharp/src/GameServer.GameLogic/Combat/CombatManager.cs servercsharp/src/GameServer.GameLogic/Player/Handlers/CastHandler.cs
git commit -m "feat(combat): support interrupt in CastRequest"
```

---

## Task 6: Client — NetworkManager 暴露 SetPreferredSkillResponse 事件

**Files:**
- Modify: `clinetcsharp/Scripts/NetworkManager.cs`
- Modify: `clinetcsharp/Scripts/NetworkManager.Dispatch.Gameplay.cs`

**Interfaces:**
- Consumes: `Game.SetPreferredSkillResponse`
- Produces: `public event Action<Game.SetPreferredSkillResponse> SetPreferredSkillResponse;`

- [ ] **Step 1: 在 NetworkManager 添加事件**

Add to `clinetcsharp/Scripts/NetworkManager.cs` near other events:

```csharp
public event Action<Game.SetPreferredSkillResponse> SetPreferredSkillResponse;
```

- [ ] **Step 2: 在 Dispatch 中触发事件**

In `clinetcsharp/Scripts/NetworkManager.Dispatch.Gameplay.cs`, update `HandleSetPreferredSkillResponse`:

```csharp
private void HandleSetPreferredSkillResponse(ByteString data)
{
    var rsp = Game.SetPreferredSkillResponse.Parser.ParseFrom(data);
    GD.Print($"[Network] SetPreferredSkill: skill={rsp.PreferredSkillId} code={rsp.Code}");
    SetPreferredSkillResponse?.Invoke(rsp);
}
```

- [ ] **Step 3: 编译客户端**

Run:
```bash
cd clinetcsharp && dotnet build clinetcsharp.csproj
```

Expected: build succeeds (protocol gen must already include `CastRequest.Interrupt`).

- [ ] **Step 4: Commit**

```bash
git add clinetcsharp/Scripts/NetworkManager.cs clinetcsharp/Scripts/NetworkManager.Dispatch.Gameplay.cs
git commit -m "feat(client): expose SetPreferredSkillResponse event"
```

---

## Task 7: Client — SkillBar 单点/双点与高亮

**Files:**
- Modify: `clinetcsharp/Scripts/SkillBar.cs`

**Interfaces:**
- Consumes: `_network.SetPreferredSkillResponse`, `_network.CastStartNotify`, `_network.CombatEndNotify`, `_network.PlayerDeathNotify`
- Produces: `OnSlotClicked(int)`, `OnSlotDoubleClicked(int)`, `SetSelectedSlot(int)`

- [ ] **Step 1: 修改 _GuiInput 区分单点/双点**

In `SkillSlot._GuiInput`:

```csharp
public override void _GuiInput(InputEvent @event)
{
    if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
    {
        if (SkillId <= 0) return;

        if (mb.DoubleClick)
            _bar.OnSlotDoubleClicked(_index);
        else
            _bar.OnSlotClicked(_index);

        AcceptEvent();
    }
}
```

- [ ] **Step 2: 实现单点设置优先技能**

Add to `SkillBar`:

```csharp
internal void OnSlotClicked(int slotIndex)
{
    var slot = _slots[slotIndex];
    if (slot.SkillId <= 0) return;

    if (_network == null) return;

    // 点击已高亮的槽位 = 取消优先
    uint requestSkillId = _selectedSlot == slotIndex ? 0 : slot.SkillId;

    var req = new Game.SetPreferredSkillRequest { SkillId = requestSkillId };
    _network.SendPacket(Protocol.MessageId.GameSetPreferredSkillReq, req);
}
```

- [ ] **Step 3: 实现双点立即释放**

Add to `SkillBar`:

```csharp
internal void OnSlotDoubleClicked(int slotIndex)
{
    var slot = _slots[slotIndex];
    if (slot.SkillId <= 0) return;
    if (slot.IsOnCooldown) return;
    if (_network == null) return;

    var req = new Game.CastRequest
    {
        SkillId = slot.SkillId,
        Interrupt = true,
    };
    _network.SendPacket(Protocol.MessageId.GameCastReq, req);
}
```

- [ ] **Step 4: 实现高亮控制**

Add helper:

```csharp
private void SetSelectedSlot(int slotIndex)
{
    if (_selectedSlot >= 0 && _selectedSlot < MaxSlots)
        _slots[_selectedSlot].SetSelected(false);

    _selectedSlot = slotIndex;

    if (_selectedSlot >= 0 && _selectedSlot < MaxSlots)
        _slots[_selectedSlot].SetSelected(true);
}
```

- [ ] **Step 5: 订阅网络事件更新高亮**

In `_Ready`, add subscriptions:

```csharp
_network.SetPreferredSkillResponse += OnSetPreferredSkillResponse;
_network.CastStartNotify += OnCastStartNotifyForSelection;
_network.CombatEndNotify += OnCombatEndForSelection;
_network.PlayerDeathNotify += OnPlayerDeathForSelection;
```

In `_ExitTree`, unsubscribe:

```csharp
_network.SetPreferredSkillResponse -= OnSetPreferredSkillResponse;
_network.CastStartNotify -= OnCastStartNotifyForSelection;
_network.CombatEndNotify -= OnCombatEndForSelection;
_network.PlayerDeathNotify -= OnPlayerDeathForSelection;
```

Add handlers:

```csharp
private void OnSetPreferredSkillResponse(Game.SetPreferredSkillResponse rsp)
{
    if (rsp.Code != Common.ErrorCode.Success) return;

    int slotIndex = -1;
    for (int i = 0; i < MaxSlots; i++)
    {
        if (_slots[i].SkillId == rsp.PreferredSkillId)
        {
            slotIndex = i;
            break;
        }
    }
    SetSelectedSlot(slotIndex);
}

private void OnCastStartNotifyForSelection(Game.CastStartNotify notify)
{
    if (_network == null) return;
    if (notify.CasterId != _network.AccountId) return;

    // 释放的是当前高亮技能，清除高亮
    if (_selectedSlot >= 0 && _selectedSlot < MaxSlots)
    {
        if (_slots[_selectedSlot].SkillId == notify.SkillId)
            SetSelectedSlot(-1);
    }
}

private void OnCombatEndForSelection(Game.CombatEndNotify notify)
{
    if (_network == null) return;
    if (notify.EntityIds.Contains(_network.AccountId))
        SetSelectedSlot(-1);
}

private void OnPlayerDeathForSelection(Game.PlayerDeathNotify notify)
{
    SetSelectedSlot(-1);
}
```

- [ ] **Step 6: 编译客户端**

Run:
```bash
cd clinetcsharp && dotnet build clinetcsharp.csproj
```

Expected: build succeeds.

- [ ] **Step 7: Commit**

```bash
git add clinetcsharp/Scripts/SkillBar.cs
git commit -m "feat(client): skillbar single-click priority, double-click instant cast"
```

---

## Task 8: 联调验证

**Files:**
- All of the above

- [ ] **Step 1: 启动服务端**

Run:
```bash
cd servercsharp && dotnet run --project src/GameServer/GameServer.csproj
```

- [ ] **Step 2: 启动 Godot 客户端并进入游戏**

- [ ] **Step 3: 测试用例**

| # | 操作 | 预期结果 |
|---|------|----------|
| 1 | 玩家撞怪进入战斗 | 玩家自动释放技能（状态栏显示技能名，施法条填充） |
| 2 | 单点 SkillBar 技能 A | 该技能槽高亮 |
| 3 | 等待自动战斗 | 自动释放技能 A 一次，高亮消失，之后按普通 AI 选技能 |
| 4 | 单点 SkillBar 技能 B，双点技能 C | 技能 B 高亮，但双点 C 立即释放 C；B 的优先状态保留 |
| 5 | 玩家正在施法时双点技能 D | 中断当前施法，立即释放 D |
| 6 | 玩家死亡或脱战 | 高亮消失 |

- [ ] **Step 4: 提交最终版本**

```bash
git add .
git commit -m "feat: player auto-combat with one-shot priority skill"
```

---

## Self-Review Checklist

- [ ] **Spec coverage:** 每个设计章节都有对应任务（协议、服务端自动战斗、优先技能清零、中断、客户端交互、高亮、联调）。
- [ ] **Placeholder scan:** 无 TBD/TODO，所有步骤包含具体代码/命令。
- [ ] **Type consistency:** `CastRequest.Interrupt` 在协议、服务端、客户端命名一致；`SetPreferredSkillResponse` 事件名与 handler 名一致。
