# 文字背包拖拽排序（带动画与服务器持久化）实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 实现文字背包左键拖拽排序：拖动时其他道具自动避让并带平滑动画，落盘后通过新协议把排序持久化到服务器 `Role.inventory_order`。

**Architecture:** 客户端在拖拽过程中实时计算插入位置，用 Tween 把其他条目动画到预览位置；被拖条目半透明跟随鼠标，靠近插入位时吸附。松开左键后发送 `InventoryReorderRequest`，服务器校验并保存 `inventory_order`，返回完整背包；失败时客户端回滚本地顺序。

**Tech Stack:** Godot 4 C# / .NET 8 / MongoDB / Protobuf

---

## 文件结构

| 文件 | 职责 |
|------|------|
| `protocols/proto/game.proto` | 新增 `InventoryReorderRequest` / `InventoryReorderResponse` |
| `protocols/proto/message_id.proto` | 新增 `GAME_INVENTORY_REORDER_REQ` / `GAME_INVENTORY_REORDER_RSP` |
| `servercsharp/src/GameServer.Database/Models/Role.cs` | 新增 `InventoryOrder: List<int>` |
| `servercsharp/src/GameServer.Database/Repositories/RoleRepository.cs` | 新增 `UpdateInventoryOrder` |
| `servercsharp/src/GameServer.GameLogic/Inventory/InventoryOrderHelper.cs` | 排序应用/维护工具函数 |
| `servercsharp/src/GameServer.GameLogic/Player/Handlers/InventoryReorderHandler.cs` | 处理排序请求 |
| `servercsharp/src/GameServer.GameLogic/Player/PlayerProtoMapper.cs` | 返回背包时应用 `inventory_order` |
| `servercsharp/src/GameServer.GameLogic/Inventory/InventoryHelper.cs` | 拾取/消耗后维护 `inventory_order`（可选，视现有实现而定） |
| `clinetcsharp/Scripts/NetworkManager.cs` | 新增 `InventoryReorderResponse` 事件 |
| `clinetcsharp/Scripts/NetworkManager.Dispatch.cs` | 分发 `GameInventoryReorderRsp` |
| `clinetcsharp/Scripts/InventoryManager.cs` | 发送请求、处理响应、提供回滚 |
| `clinetcsharp/Scripts/TextInventoryContainer.cs` | 拖拽预览、Tween 动画、插入索引计算 |
| `clinetcsharp/Scripts/TextInventoryItem.cs` | 拖拽时半透明与跟随鼠标 |
| `clinetcsharp/Scripts/InventoryUI.cs` | 新增动画时长静态配置 |
| `clinetcsharp/Scripts/DebugPanelSystemTab.Build.cs` / `.Config.cs` | 动画时长调节滑块 |

---

## Task 1: 协议定义

**Files:**
- Modify: `protocols/proto/message_id.proto`
- Modify: `protocols/proto/game.proto`

- [ ] **Step 1: 在 `message_id.proto` 分配消息 ID**

在 `GAME_DROP_ITEM_RSP = 333;` 之后新增：

```protobuf
  GAME_INVENTORY_REORDER_REQ = 334;
  GAME_INVENTORY_REORDER_RSP = 335;
```

- [ ] **Step 2: 在 `game.proto` 新增请求/响应消息**

在 `DropItemResponse` 附近新增：

```protobuf
message InventoryReorderRequest {
  repeated int32 ordered_item_ids = 1; // 客户端期望的道具顺序
}

message InventoryReorderResponse {
  Common.ErrorCode code = 1;
  string message = 2;
  repeated ItemInfo items = 3; // 排序后的完整背包
}
```

- [ ] **Step 3: 重新生成 C# 协议代码**

Run: `npm run build:proto`
Expected: `clinetcsharp/Scripts/protos/game.cs` 与 `servercsharp/src/GameServer.Proto/generated/game.cs` 中出现新类型。

- [ ] **Step 4: Commit**

```bash
git add protocols/proto/game.proto protocols/proto/message_id.proto
# 生成的代码会在后续 build 后一起提交，或单独提交
```

---

## Task 2: 服务器数据模型

**Files:**
- Modify: `servercsharp/src/GameServer.Database/Models/Role.cs`
- Modify: `servercsharp/src/GameServer.Database/Repositories/RoleRepository.cs`

- [ ] **Step 1: 在 `Role` 中新增排序字段**

```csharp
[BsonElement("inventory_order")] public List<int> InventoryOrder { get; set; } = new();
```

- [ ] **Step 2: 在 `RoleRepository` 新增更新方法**

```csharp
public async Task UpdateInventoryOrder(long roleId, List<int> order)
{
    var filter = Builders<Role>.Filter.Eq(r => r.RoleId, roleId);
    var update = Builders<Role>.Update.Set(r => r.InventoryOrder, order);
    await _col.UpdateOneAsync(filter, update);
}
```

- [ ] **Step 3: Commit**

```bash
git add servercsharp/src/GameServer.Database/Models/Role.cs servercsharp/src/GameServer.Database/Repositories/RoleRepository.cs
git commit -m "feat(db): add Role.InventoryOrder for persistent inventory sorting"
```

---

## Task 3: 服务器排序工具

**Files:**
- Create: `servercsharp/src/GameServer.GameLogic/Inventory/InventoryOrderHelper.cs`

- [ ] **Step 1: 创建工具类**

```csharp
using GameServer.Database.Models;

namespace GameServer.GameLogic.Inventory;

public static class InventoryOrderHelper
{
    /// <summary>
    /// 按 order 数组对 items 排序；未在 order 中的 item 按原有相对顺序追加到末尾。
    /// </summary>
    public static List<InventoryItem> ApplyOrder(List<InventoryItem> items, List<int> order)
    {
        var orderIndex = new Dictionary<int, int>();
        for (int i = 0; i < order.Count; i++)
            orderIndex[order[i]] = i;

        var ordered = new List<InventoryItem>(items);
        ordered.Sort((a, b) =>
        {
            bool hasA = orderIndex.TryGetValue(a.ItemId, out var idxA);
            bool hasB = orderIndex.TryGetValue(b.ItemId, out var idxB);
            if (hasA && hasB) return idxA.CompareTo(idxB);
            if (hasA) return -1;
            if (hasB) return 1;
            return 0;
        });
        return ordered;
    }

    /// <summary>
    /// 新增 item_id 时追加到 order 末尾（若不存在）。
    /// </summary>
    public static void AppendItem(List<int> order, int itemId)
    {
        if (!order.Contains(itemId))
            order.Add(itemId);
    }

    /// <summary>
    /// 完全移除 item_id 时从 order 删除。
    /// </summary>
    public static void RemoveItem(List<int> order, int itemId)
    {
        order.Remove(itemId);
    }
}
```

- [ ] **Step 2: 写单元测试**

Create: `servercsharp/src/GameServer.Tests/InventoryOrderHelperTests.cs`

```csharp
using GameServer.Database.Models;
using GameServer.GameLogic.Inventory;
using Xunit;

namespace GameServer.Tests;

public class InventoryOrderHelperTests
{
    [Fact]
    public void ApplyOrder_MovesItemsByOrder()
    {
        var items = new List<InventoryItem>
        {
            new() { ItemId = 1 },
            new() { ItemId = 2 },
            new() { ItemId = 3 },
        };
        var ordered = InventoryOrderHelper.ApplyOrder(items, new() { 3, 1, 2 });
        Assert.Equal(3, ordered[0].ItemId);
        Assert.Equal(1, ordered[1].ItemId);
        Assert.Equal(2, ordered[2].ItemId);
    }

    [Fact]
    public void ApplyOrder_AppendsUnknownItemsToEnd()
    {
        var items = new List<InventoryItem>
        {
            new() { ItemId = 1 },
            new() { ItemId = 2 },
        };
        var ordered = InventoryOrderHelper.ApplyOrder(items, new() { 2 });
        Assert.Equal(2, ordered[0].ItemId);
        Assert.Equal(1, ordered[1].ItemId);
    }
}
```

Run: `dotnet test servercsharp/src/GameServer.Tests --nologo -v q`
Expected: new tests pass.

- [ ] **Step 3: Commit**

```bash
git add servercsharp/src/GameServer.GameLogic/Inventory/InventoryOrderHelper.cs servercsharp/src/GameServer.Tests/InventoryOrderHelperTests.cs
git commit -m "feat(server): inventory order helper + tests"
```

---

## Task 4: 在返回背包时应用排序

**Files:**
- Modify: `servercsharp/src/GameServer.GameLogic/Player/PlayerProtoMapper.cs`

- [ ] **Step 1: 定位 `BuildItemsProto` 或类似方法**

读取当前 `PlayerProtoMapper.cs`，找到从 `List<InventoryItem>` 构建 `List<ItemInfo>` 的方法。

- [ ] **Step 2: 应用 `inventory_order`**

在转换为 proto 之前调用 `InventoryOrderHelper.ApplyOrder(items, role.InventoryOrder)`。

示例（假设方法签名）：

```csharp
public static async Task<List<PGame.ItemInfo>> BuildItemsProto(
    IInventoryService inventory, long roleId, LubanTableLoader tables)
{
    var dbItems = await inventory.GetByRole(roleId);
    // 需要 role 的 InventoryOrder；若当前上下文无法获取 role，可改为上层传入 order
    ...
}
```

如果 `PlayerProtoMapper` 不持有 `Role`，需要在调用处传入 `List<int> order`，或在 `PlayerSession` 上暴露 `InventoryOrder`。

- [ ] **Step 3: 更新所有调用点**

搜索 `BuildItemsProto` 的调用（UseItemHandler、DropItemHandler、GmCommandHandler、InventoryReorderHandler 等），确保传入 order。

- [ ] **Step 4: Run server tests**

Run: `npm test`
Expected: pass.

- [ ] **Step 5: Commit**

```bash
git add servercsharp/src/GameServer.GameLogic/Player/PlayerProtoMapper.cs
# 以及所有调用点变更
git commit -m "feat(server): apply inventory order when building item proto"
```

---

## Task 5: 新增/移除道具时维护 order

**Files:**
- Modify: `servercsharp/src/GameServer.GameLogic/Inventory/InventoryHelper.cs`（或 InventoryRepository）

- [ ] **Step 1: 在 `AddItem` 成功后追加 order**

若 `AddItem` 导致新增了一种 `item_id`（之前没有），则调用 `InventoryOrderHelper.AppendItem(role.InventoryOrder, itemId)` 并更新数据库。

- [ ] **Step 2: 在 `RemoveItem` 成功后清理 order**

若 `RemoveItem` 导致该 `item_id` 数量归零，则调用 `InventoryOrderHelper.RemoveItem(role.InventoryOrder, itemId)` 并更新数据库。

- [ ] **Step 3: Commit**

```bash
git commit -m "feat(server): maintain inventory_order on item add/remove"
```

---

## Task 6: 服务器排序请求 Handler

**Files:**
- Create: `servercsharp/src/GameServer.GameLogic/Player/Handlers/InventoryReorderHandler.cs`

- [ ] **Step 1: 实现 handler**

```csharp
using GameServer.Common.Config;
using GameServer.Database.Repositories;
using GameServer.GameLogic.Inventory;
using GameServer.Services.Core;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using PGame = global::Game;
using PCommon = global::Common;
using PProtocol = global::Protocol;

namespace GameServer.GameLogic.Player.Handlers;

[HandlesMessage(PProtocol.MessageId.GameInventoryReorderReq)]
public class InventoryReorderHandler : IMessageHandler
{
    private readonly ILogger<InventoryReorderHandler> _logger;
    private readonly PlayerSessionManager _session;
    private readonly RoleRepository _roles;
    private readonly LubanTableLoader _tables;

    public InventoryReorderHandler(
        ILogger<InventoryReorderHandler> logger,
        PlayerSessionManager session,
        RoleRepository roles,
        LubanTableLoader tables)
    {
        _logger = logger;
        _session = session;
        _roles = roles;
        _tables = tables;
    }

    public async Task<byte[]> HandleAsync(long accountId, byte[] data)
    {
        var req = PGame.InventoryReorderRequest.Parser.ParseFrom(data);
        var player = _session.GetOnlinePlayer(accountId);
        if (player == null)
            return ErrorResponse(PCommon.ErrorCode.NotFound, "player not found");

        var role = await _roles.FindById(player.RoleId);
        if (role == null)
            return ErrorResponse(PCommon.ErrorCode.NotFound, "role not found");

        var dbItems = await _session.Inventory.GetByRole(player.RoleId);
        var orderedIds = req.OrderedItemIds.ToList();

        // 校验：order 中不能包含玩家没有的 item_id
        var ownedIds = new HashSet<int>(dbItems.Select(i => i.ItemId));
        foreach (var id in orderedIds)
        {
            if (!ownedIds.Contains(id))
                return ErrorResponse(PCommon.ErrorCode.InvalidRequest, $"invalid item id {id}");
        }

        // 保存新顺序
        await _roles.UpdateInventoryOrder(player.RoleId, orderedIds);
        role.InventoryOrder = orderedIds;

        // 返回排序后的完整背包
        var items = await PlayerProtoMapper.BuildItemsProto(
            _session.Inventory, player.RoleId, _tables, role.InventoryOrder);
        var rsp = new PGame.InventoryReorderResponse
        {
            Code = PCommon.ErrorCode.Success,
        };
        foreach (var item in items) rsp.Items.Add(item);
        return rsp.ToByteArray();
    }

    private static byte[] ErrorResponse(PCommon.ErrorCode code, string message)
    {
        return new PGame.InventoryReorderResponse { Code = code, Message = message }.ToByteArray();
    }
}
```

> 注意：`BuildItemsProto` 的签名需要按 Task 4 实际修改为准。

- [ ] **Step 2: Build & run tests**

Run: `npm run build:server && npm test`
Expected: pass.

- [ ] **Step 3: Commit**

```bash
git add servercsharp/src/GameServer.GameLogic/Player/Handlers/InventoryReorderHandler.cs
git commit -m "feat(server): add InventoryReorderHandler"
```

---

## Task 7: 客户端协议事件接入

**Files:**
- Modify: `clinetcsharp/Scripts/NetworkManager.cs`
- Modify: `clinetcsharp/Scripts/NetworkManager.Dispatch.cs`

- [ ] **Step 1: 在 `NetworkManager.cs` 新增事件**

```csharp
public event Action<Game.InventoryReorderResponse> InventoryReorderResponse;
```

- [ ] **Step 2: 在 `NetworkManager.Dispatch.cs` 增加分发**

```csharp
case MessageId.GameInventoryReorderRsp:
    HandleInventoryReorderResponse(data);
    break;
```

- [ ] **Step 3: 实现 handler**

```csharp
private void HandleInventoryReorderResponse(ByteString data)
{
    var rsp = Game.InventoryReorderResponse.Parser.ParseFrom(data);
    InventoryReorderResponse?.Invoke(rsp);
}
```

- [ ] **Step 4: Commit**

```bash
git add clinetcsharp/Scripts/NetworkManager.cs clinetcsharp/Scripts/NetworkManager.Dispatch.cs
git commit -m "feat(client): dispatch InventoryReorderResponse"
```

---

## Task 8: InventoryManager 发送请求与回滚

**Files:**
- Modify: `clinetcsharp/Scripts/InventoryManager.cs`

- [ ] **Step 1: 订阅响应事件**

在 `_Ready` 中：

```csharp
_network.InventoryReorderResponse += OnInventoryReorderResponse;
```

在 `_ExitTree` 中取消订阅。

- [ ] **Step 2: 发送排序请求**

```csharp
public void SendReorderItems(List<uint> orderedItemIds)
{
    if (_network == null || !_network.IsServerConnected()) return;
    var req = new Game.InventoryReorderRequest();
    foreach (var id in orderedItemIds)
        req.OrderedItemIds.Add((int)id);
    _network.SendPacket(MessageId.GameInventoryReorderReq, req);
}
```

- [ ] **Step 3: 处理响应**

```csharp
private void OnInventoryReorderResponse(Game.InventoryReorderResponse rsp)
{
    if (rsp.Code == Common.ErrorCode.Success)
    {
        UpdateFromProto(rsp.Items);
    }
    else
    {
        GD.PushWarning($"[InventoryManager] reorder failed: {rsp.Message}");
        // 触发回滚：TextInventoryContainer 在发起请求前保存了旧顺序，这里通知它恢复
        EmitSignal(SignalName.InventoryChanged);
    }
}
```

> 回滚细节由 `TextInventoryContainer` 负责，它在发起请求前保存快照。

- [ ] **Step 4: Commit**

```bash
git add clinetcsharp/Scripts/InventoryManager.cs
git commit -m "feat(client): InventoryManager sends reorder request and handles response"
```

---

## Task 9: 客户端拖拽预览与动画

**Files:**
- Modify: `clinetcsharp/Scripts/TextInventoryContainer.cs`
- Modify: `clinetcsharp/Scripts/TextInventoryItem.cs`
- Modify: `clinetcsharp/Scripts/InventoryUI.cs`

- [ ] **Step 1: 在 `InventoryUI.cs` 新增动画时长配置**

```csharp
public static float BackpackReorderAnimationDuration { get; set; } = 0.15f;
```

- [ ] **Step 2: 在 `TextInventoryItem.cs` 支持拖拽时半透明**

`_GetDragData` 中设置拖拽预览并调整自身 Modulate：

```csharp
public override Variant _GetDragData(Vector2 atPosition)
{
    SetDragPreview(CreateDragPreview());
    Modulate = new Color(1, 1, 1, 0.5f);
    return this;
}
```

在拖拽结束/取消时恢复不透明度。由于 Godot 没有内置 drag-end 信号，可通过 `_Notification(NotificationDragEnd)` 检测：

```csharp
public override void _Notification(int what)
{
    base._Notification(what);
    if (what == NotificationDragEnd)
        Modulate = new Color(1, 1, 1, 1f);
}
```

- [ ] **Step 3: 在 `TextInventoryContainer.cs` 实现预览与动画**

关键状态：

```csharp
private List<TextInventoryItem> _previewItems = new();
private List<TextInventoryLayout.ItemEntry> _previewEntries = new();
private bool _isDragging;
private TextInventoryItem _draggedItem = null!;
private List<TextInventoryLayout.ItemEntry> _dragStartEntries = new();
```

在 `_CanDropData` 中计算插入索引并更新预览：

```csharp
public override bool _CanDropData(Vector2 atPosition, Variant data)
{
    if (data.AsGodotObject() is not TextInventoryItem draggedItem)
    {
        CancelDragPreview();
        return false;
    }

    if (!_isDragging)
    {
        _isDragging = true;
        _draggedItem = draggedItem;
        _dragStartEntries = new List<TextInventoryLayout.ItemEntry>(_entries);
    }

    int insertIndex = CalculateInsertIndex(atPosition);
    ApplyPreviewOrder(draggedItem, insertIndex);
    return true;
}
```

`ApplyPreviewOrder` 构建新的 `_previewEntries`，然后使用 `Tween` 把每个 `TextInventoryItem` 动画到目标 `Position`：

```csharp
private void ApplyPreviewOrder(TextInventoryItem draggedItem, int insertIndex)
{
    var newEntries = new List<TextInventoryLayout.ItemEntry>(_entries);
    int fromIndex = _items.IndexOf(draggedItem);
    if (fromIndex >= 0)
    {
        var entry = newEntries[fromIndex];
        newEntries.RemoveAt(fromIndex);
        int toIndex = Mathf.Clamp(insertIndex, 0, newEntries.Count);
        newEntries.Insert(toIndex, entry);
    }

    _previewEntries = newEntries;
    AnimateToPreviewLayout();
}
```

`AnimateToPreviewLayout` 使用当前布局算法计算每个条目目标位置，并创建/复用 Tween：

```csharp
private void AnimateToPreviewLayout()
{
    float xUnit = FontSize;
    float yOffset = 0f;
    int itemIndex = 0;
    var lines = TextInventoryLayout.Reflow(_previewEntries, LineWidth, ItemSpacing / FontSize);

    foreach (var line in lines)
    {
        float xOffset = 0f;
        for (int i = 0; i < line.Count; i++)
        {
            var item = _items[itemIndex];
            float itemWidth = TextInventoryLayout.MeasureItemWidth(item.ItemName, item.ItemCount) * xUnit;
            float spacing = i > 0 ? ItemSpacing : 0f;
            xOffset += spacing;
            var targetPos = new Vector2(xOffset, yOffset);

            var tween = CreateTween();
            tween.SetEase(Tween.EaseType.Out);
            tween.SetTrans(Tween.TransitionType.Quad);
            tween.TweenProperty(item, "position", targetPos, InventoryUI.BackpackReorderAnimationDuration);

            xOffset += itemWidth;
            itemIndex++;
        }
        yOffset += FontSize + LineSpacing;
    }
}
```

- [ ] **Step 4: 处理落盘与取消**

在 `_DropData` 中：

```csharp
public override void _DropData(Vector2 atPosition, Variant data)
{
    if (data.AsGodotObject() is not TextInventoryItem draggedItem)
        return;

    int fromIndex = _items.IndexOf(draggedItem);
    if (fromIndex < 0) return;

    int toIndex = CalculateInsertIndex(atPosition);
    if (toIndex == fromIndex || toIndex == fromIndex + 1)
    {
        CancelDragPreview();
        return;
    }

    var entry = _entries[fromIndex];
    _entries.RemoveAt(fromIndex);
    if (toIndex > fromIndex) toIndex--;
    _entries.Insert(toIndex, entry);

    // 通知服务器
    var manager = UiServices.GetInventoryManager(this);
    manager?.SendReorderItems(_entries.Select(e => e.ItemId).ToList());

    _isDragging = false;
    _draggedItem = null!;
    _dragStartEntries.Clear();
}
```

`CancelDragPreview` 在取消时把条目动画回 `_dragStartEntries` 对应的布局。

- [ ] **Step 5: Commit**

```bash
git add clinetcsharp/Scripts/TextInventoryContainer.cs clinetcsharp/Scripts/TextInventoryItem.cs clinetcsharp/Scripts/InventoryUI.cs
git commit -m "feat(client): animated drag preview for inventory reorder"
```

---

## Task 10: DebugPanel 动画时长滑块

**Files:**
- Modify: `clinetcsharp/Scripts/DebugPanelSystemTab.Build.cs`
- Modify: `clinetcsharp/Scripts/DebugPanelSystemTab.cs`
- Modify: `clinetcsharp/Scripts/DebugPanelSystemTab.Config.cs`

- [ ] **Step 1: 在 `DebugPanelSystemTab.cs` 新增控件引用**

```csharp
private HSlider _backpackReorderDurationSlider = null!;
private Label _backpackReorderDurationValue = null!;
```

- [ ] **Step 2: 在 `Build.cs` 创建滑块**

在背包配置区域（与 hover 圆角滑块同区域）新增：

```csharp
(_backpackReorderDurationSlider, _backpackReorderDurationValue) =
    CreateSliderRow(parent, "重排动画时长(s)", 0.0f, 0.5f, InventoryUI.BackpackReorderAnimationDuration, 0.01f);
_backpackReorderDurationSlider.ValueChanged += OnBackpackReorderDurationChanged;
```

- [ ] **Step 3: 实现回调**

```csharp
private void OnBackpackReorderDurationChanged(double value)
{
    InventoryUI.BackpackReorderAnimationDuration = (float)value;
    _backpackReorderDurationValue.Text = value.ToString("F2");
}
```

- [ ] **Step 4: 在 `.Config.cs` 中保存/读取配置**

保存：

```csharp
cfg.SetValue("system_tab", "backpack_reorder_duration", InventoryUI.BackpackReorderAnimationDuration);
```

读取：

```csharp
InventoryUI.BackpackReorderAnimationDuration = (float)(double)cfg.GetValue("system_tab", "backpack_reorder_duration", 0.15);
```

- [ ] **Step 5: Commit**

```bash
git add clinetcsharp/Scripts/DebugPanelSystemTab.*
git commit -m "feat(ui): debug slider for inventory reorder animation duration"
```

---

## Task 11: 验证与收尾

- [ ] **Step 1: 重新生成协议并构建全部**

Run: `npm run build:all`
Expected: 0 errors.

- [ ] **Step 2: 运行服务器测试**

Run: `npm test`
Expected: 全部通过。

- [ ] **Step 3: 手动验证清单**

1. 进入游戏，打开背包，确保已有道具。
2. 左键按住某道具拖动，其他道具应平滑避让。
3. 被拖道具半透明并跟随鼠标，靠近插入位置时吸附。
4. 松开左键后顺序更新并同步到服务器。
5. 重新登录后顺序保持。
6. 在 DebugPanel-系统-背包 中调节动画时长，实时生效。

- [ ] **Step 4: 更新设计文档状态**

在 `docs/design/2026-06-16-text-inventory-panel-design.md` 的 `7. 未决问题` 中把「玩家自定义排序是否需要服务器持久化？」标记为「已实现」。

- [ ] **Step 5: Commit & final status**

```bash
git add docs/design/2026-06-16-text-inventory-panel-design.md
npm run build:all && npm test
git commit -m "docs: mark inventory reorder persistence as implemented"
```

---

## Self-Review Checklist

- [ ] Spec coverage: 拖拽动画、插入预览、服务器持久化、order 维护、Debug 调节、回滚均有任务对应。
- [ ] Placeholder scan: 无 `TBD`、`TODO`、无模糊步骤。
- [ ] Type consistency: `InventoryReorderRequest.OrderedItemIds` 为 `repeated int32`；客户端转 `uint` 发送、服务器校验 `int`。
- [ ] 跨文件依赖: `PlayerProtoMapper.BuildItemsProto` 签名变更需同步所有调用点。
