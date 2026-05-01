# Debug Panel Role Control CenterX Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 为玩家、怪物、NPC 身上的所有可调 X 轴附着控件引入统一的 CenterX 锁定能力，并在开启后禁用对应 X 调整。

**Architecture:** 先抽一个纯规则 helper 承载“CenterX 是否锁定最终 X”和“UI 是否禁用 X 编辑”的语义，并用 xUnit 固定行为。再把 EntityBase、Player、NpcManager、调试面板各 tab 接入同一模型，最后补配置兼容和文档同步。

**Tech Stack:** C# 12, .NET 8, Godot 4 C#, xUnit

---

### Task 1: 建立客户端测试入口与纯规则 helper

**Files:**
- Create: `clinetcsharp/ClinetCSharp.Tests/ClinetCSharp.Tests.csproj`
- Create: `clinetcsharp/ClinetCSharp.Tests/RoleControlCenterXResolverTests.cs`
- Create: `clinetcsharp/Scripts/RoleControlCenterXResolver.cs`

- [ ] **Step 1: 写失败测试**

```csharp
using Xunit;

namespace ClinetCSharp.Tests;

public class RoleControlCenterXResolverTests
{
    [Fact]
    public void ResolveX_WhenCenterXEnabled_ReturnsCenterAnchor()
    {
        var resolved = RoleControlCenterXResolver.ResolveX(manualX: 24f, centerX: true);
        Assert.Equal(0f, resolved);
    }

    [Fact]
    public void ResolveX_WhenCenterXDisabled_ReturnsManualX()
    {
        var resolved = RoleControlCenterXResolver.ResolveX(manualX: 24f, centerX: false);
        Assert.Equal(24f, resolved);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void IsManualXEditable_MatchesCenterXState(bool centerX, bool expected)
    {
        Assert.Equal(expected, RoleControlCenterXResolver.IsManualXEditable(centerX));
    }
}
```

- [ ] **Step 2: 运行测试并确认失败**

Run: `dotnet test .\clinetcsharp\ClinetCSharp.Tests\ClinetCSharp.Tests.csproj -nologo -v q`
Expected: 编译失败，提示 `RoleControlCenterXResolver` 不存在。

- [ ] **Step 3: 写最小实现**

```csharp
namespace ClinetCSharp;

public static class RoleControlCenterXResolver
{
    public static float ResolveX(float manualX, bool centerX)
    {
        return centerX ? 0f : manualX;
    }

    public static bool IsManualXEditable(bool centerX)
    {
        return !centerX;
    }
}
```

- [ ] **Step 4: 再跑测试并确认通过**

Run: `dotnet test .\clinetcsharp\ClinetCSharp.Tests\ClinetCSharp.Tests.csproj -nologo -v q`
Expected: 所有测试通过。

### Task 2: 把怪物 / NPC 的条形控件与交互面板接入 CenterX

**Files:**
- Modify: `clinetcsharp/Scripts/EntityStyleConfig.cs`
- Modify: `clinetcsharp/Scripts/EntityBase.cs`
- Modify: `clinetcsharp/Scripts/NpcManager.cs`
- Modify: `clinetcsharp/Scripts/DebugPanelMonsterTab.cs`
- Modify: `clinetcsharp/Scripts/DebugPanelNpcTab.cs`

- [ ] **Step 1: 先写覆盖配置与规则的失败测试**

```csharp
[Fact]
public void ResolveX_WhenCenterXEnabled_IgnoresExistingOffset()
{
    Assert.Equal(0f, RoleControlCenterXResolver.ResolveX(-60f, true));
}
```

- [ ] **Step 2: 运行测试确认是红灯**

Run: `dotnet test .\clinetcsharp\ClinetCSharp.Tests\ClinetCSharp.Tests.csproj -nologo -v q --filter RoleControlCenterXResolverTests`
Expected: 新测试失败或未实现。

- [ ] **Step 3: 最小实现怪物 / NPC 接入**

```csharp
// EntityStyleConfig
public bool HpBarCenterX = true;
public bool MpBarCenterX = true;
public bool InteractMenuCenterX_A = false;
public bool InteractMenuCenterX_B = false;

// EntityBase.DrawBars
var healthBarOffset = new Vector2(RoleControlCenterXResolver.ResolveX(HealthBarOffset.X, HealthBarCenterX), HealthBarOffset.Y);

// NpcManager.RefreshInteractMenuPosition
float offsetX = useB
    ? RoleControlCenterXResolver.ResolveX(cfg.InteractMenuOffsetBX, cfg.InteractMenuCenterX_B)
    : RoleControlCenterXResolver.ResolveX(cfg.InteractMenuOffsetAX, cfg.InteractMenuCenterX_A);
```

- [ ] **Step 4: 运行测试与编译**

Run: `dotnet test .\clinetcsharp\ClinetCSharp.Tests\ClinetCSharp.Tests.csproj -nologo -v q`
Expected: 测试通过。

Run: `Set-Location .\clinetcsharp; .\cli.ps1 rebuild`
Expected: 客户端编译通过。

### Task 3: 把玩家独有控件与逐行标签接入 CenterX

**Files:**
- Modify: `clinetcsharp/Scripts/Player.cs`
- Modify: `clinetcsharp/Scripts/DebugPanelPlayerTab.cs`

- [ ] **Step 1: 先为玩家逐行标签语义补失败测试**

```csharp
[Theory]
[InlineData(true, 18f, 0f)]
[InlineData(false, 18f, 18f)]
public void ResolveX_UsesPerControlCenterX(bool centerX, float manualX, float expected)
{
    Assert.Equal(expected, RoleControlCenterXResolver.ResolveX(manualX, centerX));
}
```

- [ ] **Step 2: 运行测试确认失败原因正确**

Run: `dotnet test .\clinetcsharp\ClinetCSharp.Tests\ClinetCSharp.Tests.csproj -nologo -v q --filter ResolveX_UsesPerControlCenterX`
Expected: 红灯，说明 helper 或调用方语义尚未满足。

- [ ] **Step 3: 最小实现玩家接入**

```csharp
// Player fields
private bool[] _labelCenterX = new bool[LabelCount] { true, true, true, true };
public bool CastBarCenterX { get; set; } = true;
public bool LevelBadgeCenterX { get; set; } = true;

// UpdateAllLabelPositions
var resolvedX = RoleControlCenterXResolver.ResolveX(_labelOffsets[i].X, _labelCenterX[i]);

// _Draw cast bar / level badge
var castBarX = RoleControlCenterXResolver.ResolveX(CastBarOffset.X, CastBarCenterX);
var levelBadgeX = RoleControlCenterXResolver.ResolveX(LevelBadgeOffset.X, LevelBadgeCenterX);
```

- [ ] **Step 4: 运行测试与客户端编译**

Run: `dotnet test .\clinetcsharp\ClinetCSharp.Tests\ClinetCSharp.Tests.csproj -nologo -v q`
Expected: 测试通过。

Run: `Set-Location .\clinetcsharp; .\cli.ps1 rebuild`
Expected: 客户端编译通过。

### Task 4: 调试面板 UI 锁定 X 编辑与配置兼容

**Files:**
- Modify: `clinetcsharp/Scripts/DebugPanelPlayerTab.cs`
- Modify: `clinetcsharp/Scripts/DebugPanelMonsterTab.cs`
- Modify: `clinetcsharp/Scripts/DebugPanelNpcTab.cs`
- Modify: `clinetcsharp/docs/调试面板说明.md`

- [ ] **Step 1: 先写失败测试，固定 UI 锁定规则**

```csharp
[Theory]
[InlineData(true, false)]
[InlineData(false, true)]
public void IsManualXEditable_ControlsSliderState(bool centerX, bool editable)
{
    Assert.Equal(editable, RoleControlCenterXResolver.IsManualXEditable(centerX));
}
```

- [ ] **Step 2: 运行测试确认红灯**

Run: `dotnet test .\clinetcsharp\ClinetCSharp.Tests\ClinetCSharp.Tests.csproj -nologo -v q --filter IsManualXEditable_ControlsSliderState`
Expected: 若 helper 未覆盖对应语义则失败。

- [ ] **Step 3: 最小实现 UI 锁定和配置兼容**

```csharp
// DebugPanel tabs
xSlider.Editable = RoleControlCenterXResolver.IsManualXEditable(centerCheck.ButtonPressed);

// Player legacy config fallback
bool legacyAutoCenter = (bool)cfg.GetValue("player", "label_auto_center_x", false);
for (int i = 0; i < LabelCount; i++)
    _labelCenterX[i] = (bool)cfg.GetValue("player", $"label_center_x_{i}", legacyAutoCenter);
```

- [ ] **Step 4: 运行最终验证**

Run: `dotnet test .\clinetcsharp\ClinetCSharp.Tests\ClinetCSharp.Tests.csproj -nologo -v q`
Expected: 测试通过。

Run: `Set-Location .\clinetcsharp; .\cli.ps1 rebuild`
Expected: 客户端编译通过。

Run: `dotnet build .\clinetcsharp\clinetcsharp.csproj -nologo -v q`
Expected: `0 Warning(s)` 和 `0 Error(s)`。
