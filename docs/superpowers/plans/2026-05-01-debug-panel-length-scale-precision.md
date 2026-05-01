# Debug Panel Length Scale Precision Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make `LengthScale` the only editable and persisted source for player, monster, and NPC bar lengths, with 0.001 precision.

**Architecture:** Keep runtime bar length as calculated data from `GridSize × LengthScale`. Debug panel tabs expose only `length_scale` sliders for bar length; old editable direct `length` controls are removed. A small pure policy centralizes length-scale step and direct-length config cleanup rules.

**Tech Stack:** Godot C# / .NET 8 / xUnit client tests.

---

### Task 1: Add pure length-scale policy and tests

**Files:**
- Create: `clinetcsharp/Scripts/DebugPanelLengthScalePolicy.cs`
- Modify: `clinetcsharp/ClinetCSharp.Tests/RoleControlCenterXResolverTests.cs`
- Modify: `clinetcsharp/ClinetCSharp.Tests/ClinetCSharp.Tests.csproj`

- [x] **Step 1: Write failing tests**
  - Add tests for `DebugPanelLengthScalePolicy.Step == 0.001`, three-decimal formatting, and direct `length` key rejection.

- [x] **Step 2: Run focused tests and verify red**
  - Run `dotnet test .\ClinetCSharp.Tests\ClinetCSharp.Tests.csproj -nologo -v minimal --filter DebugPanelLengthScalePolicy` from `clinetcsharp`.
  - Expected: compile failure because `DebugPanelLengthScalePolicy` does not exist.

- [x] **Step 3: Implement policy**
  - Add `DebugPanelLengthScalePolicy` with `Step`, `Format`, `ShouldPersistLengthKey`, and `ShouldRemoveDirectLengthKey`.

- [x] **Step 4: Link file in tests and verify green**
  - Add the new source file to the test csproj.
  - Re-run focused tests; expected: pass.

### Task 2: Apply 0.001 precision to existing player and monster length-scale controls

**Files:**
- Modify: `clinetcsharp/Scripts/DebugPanelPlayerTab.cs`
- Modify: `clinetcsharp/Scripts/DebugPanelMonsterTab.cs`
- Modify: `clinetcsharp/Scripts/DebugPanelTab.cs` if shared helper needs policy constants.

- [x] **Step 1: Replace hard-coded `0.05` length-scale steps**
  - Player HP/MP length-scale sliders use `DebugPanelLengthScalePolicy.Step`.
  - Monster HP/MP length-scale sliders use `DebugPanelLengthScalePolicy.Step`.

- [x] **Step 2: Format all length-scale labels through policy**
  - Use `DebugPanelLengthScalePolicy.Format(value)` for HP/MP/Cast length-scale labels.
  - Keep non-length scales unchanged unless they already use the generic formatter.

### Task 3: Remove direct player length editing and persistence

**Files:**
- Modify: `clinetcsharp/Scripts/DebugPanelPlayerTab.cs`

- [x] **Step 1: Remove editable direct length rows**
  - Remove player HP direct `长度` slider row.
  - Remove player MP direct `长度` slider row.
  - Replace player Cast direct `长度` with a `长度比例` slider because cast length currently lacks one.

- [x] **Step 2: Remove direct length handlers and syncing**
  - Remove or stop using `OnHealthBarLengthChanged`, `OnMpBarLengthChanged`, and `OnCastBarLengthChanged`.
  - Length-scale change updates only `Player.Set*LengthScale` and the length-scale label.

- [x] **Step 3: Remove direct length persistence**
  - Do not save `healthbar.length` or `castbar.length`; add `mpbar.length_scale` if missing.
  - Do not read direct `length` keys during load.
  - Clean old direct `length` keys before saving.

### Task 4: Add NPC HP/MP length-scale controls and persist monster/NPC scales

**Files:**
- Modify: `clinetcsharp/Scripts/DebugPanelNpcTab.cs`
- Modify: `clinetcsharp/Scripts/DebugPanelMonsterTab.cs`
- Modify: `clinetcsharp/Scripts/MonsterManager.cs`

- [x] **Step 1: Save/load monster and NPC HP/MP length scales**
  - Save `hp_bar_length_scale` and `mp_bar_length_scale` in monster and NPC config sections.
  - Load those fields in `MonsterManager.LoadStyleConfigFromSection`.

- [x] **Step 2: Add NPC HP/MP length-scale UI**
  - Add NPC HP/MP visibility, length-scale, height-scale, fill, color, center-X and offset controls consistent with monster controls.
  - Use `DebugPanelLengthScalePolicy.Step` and `DebugPanelLengthScalePolicy.Format` for NPC HP/MP length-scale labels.

### Task 5: Verify and clean up

**Files:**
- All modified files.

- [x] **Step 1: Run focused tests**
  - `dotnet test .\ClinetCSharp.Tests\ClinetCSharp.Tests.csproj -nologo -v minimal --filter DebugPanelLengthScalePolicy`

- [x] **Step 2: Run full client tests and build**
  - `dotnet test .\ClinetCSharp.Tests\ClinetCSharp.Tests.csproj -nologo -v minimal`
  - `dotnet build .\clinetcsharp.csproj -nologo -v minimal`

- [x] **Step 3: Check changed files for errors**
  - Check all edited C# files and the design/plan docs.

- [x] **Step 4: Update repo memory if a reusable rule emerged**
  - Record that debug panel direct bar lengths are forbidden; only `length_scale` may persist.
