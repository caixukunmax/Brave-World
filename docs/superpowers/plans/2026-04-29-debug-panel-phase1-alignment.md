# Debug Panel Phase 1 Alignment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Align the client debug panel with the shared `DraggablePanel` architecture without changing its existing tab behavior.

**Architecture:** Convert `DebugPanel` from a standalone `Control`-based panel into a `DraggablePanel`-based panel, update `debug_panel.tscn` to the shared panel skeleton, and preserve config compatibility by continuing to read/write the existing `panel_geo` section. This phase intentionally does not split tab files or optimize monster/NPC refresh behavior.

**Tech Stack:** Godot 4.6 C#, `.tscn` scene files, existing `PanelManager` / `DraggablePanel` framework

---

### Task 1: Align Scene Skeleton

**Files:**
- Modify: `clinetcsharp/scenes/debug_panel.tscn`
- Verify: `clinetcsharp/scenes/main.tscn`
- Verify: `clinetcsharp/scenes/role_select_scene.tscn`

- [ ] **Step 1: Rebuild the debug panel scene around the shared panel skeleton**

Update `clinetcsharp/scenes/debug_panel.tscn` so the root node is a `PanelContainer` with this structure:

```text
DebugPanel (PanelContainer, script=DebugPanel.cs, visible=false)
└── VBoxContainer
    ├── TitleBar
    │   └── HBoxContainer
    │       ├── MinimizeButton
    │       ├── TitleLabel
    │       ├── Spacer
    │       └── CloseButton
    └── Content (VBoxContainer)
        └── ScrollContainer
            └── TabContainer
```

- [ ] **Step 2: Preserve the existing tab container names**

Keep the six existing tab node names exactly as:

```text
地图
玩家
怪物
NPC
系统
UI
```

This preserves tab lookup and config behavior for this phase.

- [ ] **Step 3: Verify scene references remain valid**

Confirm the existing scene instances in these files still point to `res://scenes/debug_panel.tscn` and need no path changes:

```text
clinetcsharp/scenes/main.tscn
clinetcsharp/scenes/role_select_scene.tscn
```

Expected result: only the internals of `debug_panel.tscn` change, not the instance paths.

### Task 2: Move DebugPanel Onto DraggablePanel

**Files:**
- Modify: `clinetcsharp/Scripts/DebugPanel.cs`
- Modify: `clinetcsharp/Scripts/DebugPanel.Nodes.cs`
- Modify: `clinetcsharp/Scripts/DebugPanel.DynamicUI.cs`
- Modify: `clinetcsharp/Scripts/DebugPanel.Config.cs`
- Delete: `clinetcsharp/Scripts/DebugPanel.Resize.cs`

- [ ] **Step 1: Write the failing verification expectation**

The first compile check should fail if the scene/script contract is broken after the inheritance change.

Run:

```powershell
dotnet build c:\code\tslua2\clinetcsharp\clinetcsharp.csproj -nologo -v q
```

Expected before implementation: build may fail after the scene skeleton change because `DebugPanel` still assumes the old `Control/Panel/...` hierarchy.

- [ ] **Step 2: Change DebugPanel inheritance and remove duplicated panel state**

Update `clinetcsharp/Scripts/DebugPanel.cs` so that:

```csharp
public partial class DebugPanel : DraggablePanel
```

Then remove the first-phase duplicate state and behavior:

```text
_outerControl
_isPanelVisible
_isPanelFocused
_isDragging
_dragOffset
custom drag handling in _Input
custom resize handling entry point in _Input
```

Keep these responsibilities:

```text
F12 toggle
Ctrl+Z undo shortcut
tab creation
tab sync on open
config orchestration
PanelManager.RegisterPanel(this)
```

- [ ] **Step 3: Rebind node references to the new scene structure**

Update `clinetcsharp/Scripts/DebugPanel.Nodes.cs` to use the new paths:

```csharp
_panel = this;
_content = GetNodeOrNull<VBoxContainer>("VBoxContainer/Content");
_scrollContainer = GetNodeOrNull<ScrollContainer>("VBoxContainer/Content/ScrollContainer");
_tabContainer = GetNodeOrNull<TabContainer>("VBoxContainer/Content/ScrollContainer/TabContainer");
```

And update `DebugPanel.cs` tab lookup paths to the new hierarchy under `VBoxContainer/Content/ScrollContainer/TabContainer`.

- [ ] **Step 4: Move footer UI creation under the content container**

Update `clinetcsharp/Scripts/DebugPanel.DynamicUI.cs` so the preset footer is added under the content container rather than `_panel.AddChild(...)`.

Expected direction:

```csharp
_content.AddChild(mainContainer);
```

Do not redesign the footer yet; only reattach it to the new container structure.

- [ ] **Step 5: Keep config compatibility for panel geometry**

Update `clinetcsharp/Scripts/DebugPanel.Config.cs` so `panel_geo.offset_*` stays compatible, but geometry is applied to the root `DebugPanel` node:

```text
save offset_left  = Position.X
save offset_top   = Position.Y
save offset_right = Position.X + Size.X
save offset_bottom= Position.Y + Size.Y
```

On load:

```text
left   = panel_geo.offset_left
top    = panel_geo.offset_top
right  = panel_geo.offset_right
bottom = panel_geo.offset_bottom

Position = (left, top)
Size = (right - left, bottom - top)
```

This keeps old config keys usable without changing `MonsterManager` / `NpcManager` expectations.

- [ ] **Step 6: Delete the obsolete resize partial**

Delete:

```text
clinetcsharp/Scripts/DebugPanel.Resize.cs
```

Reason: resize is now handled by `DraggablePanel`.

- [ ] **Step 7: Re-run compile verification**

Run:

```powershell
dotnet build c:\code\tslua2\clinetcsharp\clinetcsharp.csproj -nologo -v q
```

Expected: build succeeds.

### Task 3: Smoke Validation and Documentation Sync

**Files:**
- Modify: `clinetcsharp/docs/调试面板说明.md`

- [ ] **Step 1: Validate static correctness**

Run editor diagnostics on the touched files and confirm there are no new script errors.

Expected: no new errors in the modified client files.

- [ ] **Step 2: Update the debug panel doc to reflect the actual tab set and architecture status**

At minimum, update `clinetcsharp/docs/调试面板说明.md` to say the panel now includes:

```text
地图 / 玩家 / 怪物 / NPC / 系统 / UI
```

And note that the panel has been aligned to the shared draggable panel framework.

- [ ] **Step 3: Final verification**

Run:

```powershell
dotnet build c:\code\tslua2\clinetcsharp\clinetcsharp.csproj -nologo -v q
```

If available, also run file-level diagnostics for:

```text
clinetcsharp/Scripts/DebugPanel.cs
clinetcsharp/Scripts/DebugPanel.Nodes.cs
clinetcsharp/Scripts/DebugPanel.DynamicUI.cs
clinetcsharp/Scripts/DebugPanel.Config.cs
```

Expected: compile succeeds and there are no new file errors.