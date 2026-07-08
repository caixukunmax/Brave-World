# 玩家自动战斗与一次性优先技能设计

> 版本：V1.0
> 状态：待实现
> 相关文档：[`docs/design/combat/战斗系统设计.md`](../../../design/combat/战斗系统设计.md)

---

## 1. 设计目标

让玩家在战斗中像怪物一样自动按 CD 释放技能，同时保留手动施法的控制权，并支持一次性优先技能选择。

- **默认自动战斗**：玩家进入战斗后，无需手动操作即可自动释放已装备技能。
- **手动施法保留**：玩家可以通过 SkillBar 主动释放技能，覆盖自动战斗。
- **一次性优先技能**：玩家可以单点 SkillBar 上的某个技能，使其在自动战斗中获得下一次优先释放权；成功释放一次后，优先状态自动清除。
- **双点立即释放**：玩家可以双点 SkillBar 上的技能，立即释放该技能；若当前正在施法/后摇，则先中断当前动作。

---

## 2. 核心机制

### 2.1 自动战斗触发条件

玩家进入战斗状态（`CombatContext.State == "COMBAT"`）后，每个战斗 tick 都会像怪物一样检查并释放技能：

1. 当前不处于 CASTING 或 POST_CAST 状态。
2. 从技能池中选择可用技能（CD 就绪、MP 足够、目标在射程内）。
3. 若存在一次性优先技能，优先尝试该技能；不可用时不释放其他技能，保持优先状态。
4. 无优先技能时，按现有 AI 规则选择技能释放。

### 2.2 一次性优先技能

- **设置方式**：SkillBar 单点某个技能槽。
- **清除时机**：该技能被成功释放一次后（无论由自动战斗还是手动触发）。
- **不可用行为**：若优先技能处于 CD、MP 不足或目标不在射程内，自动战斗本次不动作，优先状态继续保留，直到该技能被成功释放一次。
- **死亡/脱战**：玩家死亡或脱战时，优先技能自动清除。
- **切换目标/地图**：不影响优先技能，仅与技能 ID 相关。

### 2.3 双点立即释放

- **触发方式**：SkillBar 双点某个技能槽。
- **行为**：
  - 若当前未施法，立即释放该技能。
  - 若当前正在施法或后摇，先中断当前动作，再释放该技能。
- **与优先技能的关系**：双点释放也会清除该技能的一次性优先状态（如果它正好是优先技能）。

### 2.4 手动施法与自动战斗的协调

- 手动施法（双点/其他立即施法途径）和自动战斗共享同一套 CD、MP、射程校验。
- 手动施法成功后，该技能进入 CD，自动战斗在下一次 tick 会自然跳过该技能。
- 若手动施法的是一次性优先技能，释放成功后清除优先状态。

---

## 3. 服务端设计

### 3.1 复用 `PreferredSkillId` 为一次性优先

现有 `CombatContext.PreferredSkillId` 和 `MapPlayerState.PreferredSkillId` 的语义从"持久优先"改为"一次性优先"。

- `SetPreferredSkillRequest` 仍用于设置优先技能，`skill_id = 0` 表示取消。
- `SelectSkill` 中优先逻辑保持不变：若 `PreferredSkillId` 设置、在技能池、CD 就绪、在射程内，则返回它。
- 成功释放 `PreferredSkillId` 对应的技能后，立即将其清零（`ctx.PreferredSkillId = 0` 并同步到 `MapPlayerState`）。
- 玩家死亡或脱战时，`MapPlayerState.PreferredSkillId` 清零。

### 3.2 战斗 Tick 处理玩家

`CombatManager.TickMonsterSkills` 扩展为处理所有战斗实体（玩家 + 怪物）。

推荐做法：移除 `TickMonsterSkills` 中 `entityId >= CombatConstants.MonsterIdThreshold` 的怪物-only 判断（或根据代码风格重命名为 `TickCombatSkills`）。玩家和怪物走同一套 `SelectSkill` + `RequestCast` 流程。

### 3.3 中断当前施法

`CastRequest` 增加 `bool interrupt = 3` 字段。

`HandleCastRequest` 处理逻辑：

1. 校验玩家在战斗中且技能在技能池。
2. 若 `interrupt == true` 且当前处于 CASTING 或 POST_CAST 状态：
   - 调用 `SkillPipeline.InterruptCast(playerId)` 将 `SubState` 重置为 `NONE`，清除 `CastSkillId` 和 `CastEndTime`。
3. 调用 `_pipeline.Cast(skillId, playerId, maps)` 释放新技能。
4. 若释放的是 `PreferredSkillId`，释放成功后清零。

`InterruptCast` 方法实现：

```csharp
public void InterruptCast(long entityId)
{
    var ctx = _relations.Contexts.GetValueOrDefault(entityId);
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

### 3.4 优先技能清零同步

- 在 `RequestCast` 或 `HandleCastRequest` 成功后，若 `ctx.PreferredSkillId == skillId`，则：
  - `ctx.PreferredSkillId = 0`
  - 找到对应的 `MapPlayerState`，`mapPlayer.PreferredSkillId = 0`
- 下一帧 `SyncPreferredSkills` 会将 `0` 同步到 `ctx`（幂等）。
- 客户端通过 `CastStartNotify` 感知优先技能已被释放，并清除本地高亮。

### 3.5 日志与调试

- 增加 debug 日志记录自动战斗选中的技能。
- 记录一次性优先技能的设置、使用和清除。

---

## 4. 客户端设计

### 4.1 SkillBar 交互变更

- **单点技能槽**：
  - 发送 `SetPreferredSkillRequest { SkillId = slot.SkillId }`。
  - 本地高亮该技能槽（显示选中边框）。
- **双点技能槽**：
  - 发送 `CastRequest { SkillId = slot.SkillId, Interrupt = true }`。
  - 不依赖本地高亮。
- **取消优先**：
  - 单点已高亮的技能槽，发送 `SetPreferredSkillRequest { SkillId = 0 }`，清除高亮。

### 4.2 高亮显示

- 使用 `SkillSlot.SetSelected(bool)` 控制高亮框 `_highlight` 的显示。
- `_selectedSlot` 记录当前高亮的槽位索引。
- 高亮状态来源：
  - 玩家单点技能槽。
  - 收到 `SetPreferredSkillResponse` 后更新（如果返回的 `PreferredSkillId` 与本地不一致，以服务端为准）。
  - 收到 `CastStartNotify` 且 caster 是玩家、skill_id 与优先技能一致时，清除高亮。
  - 玩家死亡/脱战时清除高亮。

### 4.3 网络消息处理

- `SetPreferredSkillResponse`：
  - 若 `PreferredSkillId > 0`，高亮对应槽位。
  - 若 `PreferredSkillId == 0`，清除高亮。
- `CastStartNotify`：
  - 若 caster 是玩家且 skill_id 等于当前高亮技能的 skill_id，清除高亮。
- `CombatEndNotify` / `PlayerDeathNotify`：
  - 清除高亮。

### 4.4 双点检测

Godot 的 `InputEventMouseButton` 提供 `DoubleClick` 标志。在 `SkillSlot._GuiInput` 中：

```csharp
if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
{
    if (mb.DoubleClick)
        _bar.OnSlotDoubleClicked(_index);
    else
        _bar.OnSlotClicked(_index);
    AcceptEvent();
}
```

### 4.5 状态栏联动

- 自动战斗释放技能时，服务端会下发 `CastStartNotify`，客户端已有的状态栏/施法条联动逻辑会显示技能名和进度。
- 一次性优先技能被释放后，状态栏/施法条按现有逻辑更新。

---

## 5. 协议变更

### 5.1 `CastRequest`

```protobuf
message CastRequest {
  uint32 skill_id = 1;
  uint64 target_id = 2;
  bool   interrupt = 3;  // true = 中断当前施法/后摇并立即释放
}
```

### 5.2 `SetPreferredSkillRequest` / `SetPreferredSkillResponse`

语义不变，但 `preferred_skill_id` 从"持久优先"改为"一次性优先"。

```protobuf
message SetPreferredSkillRequest {
  uint32 skill_id = 1;  // 0 = 取消一次性优先
}

message SetPreferredSkillResponse {
  common.ErrorCode code = 1;
  string           message = 2;
  uint32           preferred_skill_id = 3;  // 当前的一次性优先技能，0 = 无
}
```

---

## 6. 边界情况

| 场景 | 预期行为 |
|------|----------|
| 优先技能在 CD 中 | 自动战斗不动作，保持优先状态 |
| 优先技能目标不在射程内 | 自动战斗不动作，保持优先状态；玩家可手动移动或双点释放 |
| 手动双点释放非优先技能 | 立即释放，优先状态保留 |
| 手动双点释放优先技能 | 立即释放，释放成功后清除优先状态 |
| 自动战斗释放优先技能 | 释放成功后清除优先状态，后续按普通 AI 选技能 |
| 玩家死亡 | 清除优先状态和高亮 |
| 玩家脱战 | 清除优先状态和高亮 |
| 玩家切换地图 | 战斗结束，清除优先状态和高亮 |
| 优先技能被卸下 | 服务端校验时失败或自动忽略；客户端收到响应后清除高亮 |

---

## 7. 与现有系统的兼容性

- **纯 CD 即时制**：完全兼容，不改变 CD、MP、射程校验逻辑。
- **怪物 AI**：怪物继续走现有 `TickMonsterSkills` 逻辑，不受玩家自动战斗影响。
- **手动施法**：`HandleCastRequest` 继续工作，新增 `interrupt` 字段扩展功能。
- **PreferredSkillId**：现有字段复用，语义从持久改为一次性；当前没有 UI 依赖其持久语义，影响范围可控。
- **状态栏/施法条**：复用已有的 `CastStartNotify` 和 `CombatStateNotify` 联动，无需额外修改。

---

## 8. 待技术设计确定的数值

| 项 | 说明 |
|----|------|
| 双点判定间隔 | Godot 默认双击间隔，通常 400ms；如需自定义，在 SkillBar 中配置 |
| 自动战斗 tick 频率 | 复用现有 `TickMonsterSkills` 频率 |
| 中断是否消耗额外资源 | 当前设计不额外消耗 MP/CD |

---

## 9. 实现范围

### 必做
- [ ] 修改 `protocols/proto/game.proto`：`CastRequest` 增加 `interrupt`。
- [ ] 服务端：`TickMonsterSkills` 处理玩家自动战斗。
- [ ] 服务端：`PreferredSkillId` 改为一次性优先，释放后清零。
- [ ] 服务端：`HandleCastRequest` 支持 `interrupt`。
- [ ] 服务端：`SkillPipeline` 增加 `InterruptCast`。
- [ ] 客户端：`SkillBar` 单点/双点交互改造。
- [ ] 客户端：`SkillBar` 高亮优先技能。
- [ ] 客户端：处理 `SetPreferredSkillResponse` 和 `CastStartNotify` 更新高亮。

### 可选
- [ ] 调试面板增加"自动战斗"开关（当前设计为始终开启，未来可扩展）。
- [ ] 优先技能不可用时，SkillBar 显示特殊提示（如灰闪、倒计时）。
