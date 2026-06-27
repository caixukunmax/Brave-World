# 文字背包面板 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将现有图标格子背包改为纯文字流式布局，支持拖动排序、右键菜单、容量显示。

**Architecture：** 新增 `TextInventoryLayout` 负责字符宽度计算与换行；新增 `TextInventoryItem` 负责单条条目显示与拖拽数据；新增 `TextInventoryContainer` 负责流式排布、接收拖拽、重排顺序；改造现有 `InventoryUI` 组装这些组件并绑定服务器数据。

**Tech Stack：** Godot 4 C#, xUnit

---

## 文件结构

| 文件 | 责任 |
| --- | --- |
| `clinetcsharp/Scripts/TextInventoryLayout.cs` | 字符宽度计算、换行、容量检查 |
| `clinetcsharp/Scripts/TextInventoryItem.cs` | 单条条目显示、品质色、拖拽数据 |
| `clinetcsharp/Scripts/TextInventoryContainer.cs` | 流式布局容器、拖拽落点、插入指示器 |
| `clinetcsharp/Scripts/InventoryUI.Build.cs` | 改造面板构建：头部容量条 + 文字容器 |
| `clinetcsharp/Scripts/InventoryUI.Actions.cs` | 右键菜单、刷新、事件绑定 |
| `clinetcsharp/Scripts/InventoryUI.cs` | 生命周期、尺寸配置、容量更新 |
| `clinetcsharp/ClinetCSharp.Tests/TextInventoryLayoutTests.cs` | 布局算法的单元测试 |

---

## Task 1: 创建 `TextInventoryLayout` 并测试

**Files:**
- Create: `clinetcsharp/Scripts/TextInventoryLayout.cs`
- Test: `clinetcsharp/ClinetCSharp.Tests/TextInventoryLayoutTests.cs`

- [ ] **Step 1: 编写失败测试**

```csharp
using Xunit;
using System.Collections.Generic;

namespace ClinetCSharp.Tests;

public class TextInventoryLayoutTests
{
    [Fact]
    public void MeasureItemWidth_ChineseNameAndCount_ReturnsExpected()
    {
        float width = TextInventoryLayout.MeasureItemWidth("魔法剑", 1);
        Assert.Equal(3f + 1f + 0.6f, width, 3);
    }

    [Fact]
    public void MeasureItemWidth_TwoDigitCount_AddsCorrectDigitWidth()
    {
        float width = TextInventoryLayout.MeasureItemWidth("高精宝石", 30);
        Assert.Equal(4f + 1f + 1.2f, width, 3);
    }

    [Fact]
    public void Reflow_SingleItemFits_ReturnsOneLine()
    {
        var items = new List<TextInventoryLayout.ItemEntry>
        {
            new TextInventoryLayout.ItemEntry { Name = "魔法剑", Count = 1, Quality = 0 }
        };
        var lines = TextInventoryLayout.Reflow(items, 30f);
        Assert.Single(lines);
        Assert.Single(lines[0]);
    }

    [Fact]
    public void Reflow_ItemExceedsWidth_ThrowsInvalidOperation()
    {
        var items = new List<TextInventoryLayout.ItemEntry>
        {
            new TextInventoryLayout.ItemEntry { Name = "这是一个非常长的道具名字", Count = 1, Quality = 0 }
        };
        Assert.Throws<System.InvalidOperationException>(() => TextInventoryLayout.Reflow(items, 10f));
    }

    [Fact]
    public void CanFit_ItemsWithinCapacity_ReturnsTrue()
    {
        var items = new List<TextInventoryLayout.ItemEntry>
        {
            new TextInventoryLayout.ItemEntry { Name = "魔法剑", Count = 1, Quality = 0 },
            new TextInventoryLayout.ItemEntry { Name = "高精宝石", Count = 30, Quality = 0 }
        };
        Assert.True(TextInventoryLayout.CanFit(items, 30f, 10));
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

Run: `dotnet test clinetcsharp/ClinetCSharp.Tests/ClinetCSharp.Tests.csproj --filter TextInventoryLayoutTests -v n`

Expected: 编译失败，`TextInventoryLayout` 不存在。

- [ ] **Step 3: 实现最小代码**

```csharp
using System;
using System.Collections.Generic;

namespace ClinetCSharp
{
    public static class TextInventoryLayout
    {
        public const float ChineseCharWidth = 1.0f;
        public const float MultiplierWidth = 1.0f;
        public const float DigitWidth = 0.6f;

        public class ItemEntry
        {
            public uint ItemId;
            public string Name = "";
            public uint Count;
            public int Quality;
        }

        public static float MeasureItemWidth(string name, uint count)
        {
            int digits = count <= 0 ? 1 : (int)Math.Floor(Math.Log10(count)) + 1;
            return name.Length * ChineseCharWidth + MultiplierWidth + digits * DigitWidth;
        }

        public static List<List<ItemEntry>> Reflow(List<ItemEntry> items, float lineWidth)
        {
            var lines = new List<List<ItemEntry>>();
            var currentLine = new List<ItemEntry>();
            float currentWidth = 0f;

            foreach (var item in items)
            {
                float itemWidth = MeasureItemWidth(item.Name, item.Count);
                if (itemWidth > lineWidth)
                    throw new InvalidOperationException($"Item '{item.Name}' is too wide for the inventory panel.");

                if (currentLine.Count > 0)
                    itemWidth += GetSpacingWidth();

                if (currentWidth + itemWidth > lineWidth)
                {
                    lines.Add(currentLine);
                    currentLine = new List<ItemEntry>();
                    currentWidth = 0f;
                    itemWidth = MeasureItemWidth(item.Name, item.Count);
                }

                currentLine.Add(item);
                currentWidth += itemWidth;
            }

            if (currentLine.Count > 0)
                lines.Add(currentLine);

            return lines;
        }

        public static bool CanFit(List<ItemEntry> items, float lineWidth, int lineCount)
        {
            try
            {
                var lines = Reflow(items, lineWidth);
                return lines.Count <= lineCount;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private static float GetSpacingWidth()
        {
            return 0.5f;
        }
    }
}
```

- [ ] **Step 4: 运行测试确认通过**

Run: `dotnet test clinetcsharp/ClinetCSharp.Tests/ClinetCSharp.Tests.csproj --filter TextInventoryLayoutTests -v n`

Expected: 4 tests pass。

- [ ] **Step 5: 提交**

```bash
git add clinetcsharp/Scripts/TextInventoryLayout.cs clinetcsharp/ClinetCSharp.Tests/TextInventoryLayoutTests.cs
git commit -m "feat: add text inventory layout calculator"
```

---

## Task 2: 创建 `TextInventoryItem`

**Files:**
- Create: `clinetcsharp/Scripts/TextInventoryItem.cs`

- [ ] **Step 1: 实现单条条目控件**

```csharp
using Godot;

namespace ClinetCSharp
{
    [GlobalClass]
    public partial class TextInventoryItem : Label
    {
        [Export] public string ItemName = "";
        [Export] public uint ItemCount;
        [Export] public int Quality;
        [Export] public uint ItemId;

        private Color _normalColor;
        private Color _hoverColor = Colors.Yellow;

        public override void _Ready()
        {
            MouseFilter = MouseFilterEnum.Pass;
            VerticalAlignment = VerticalAlignment.Center;
            AddThemeFontSizeOverride("font_size", 16);
            MouseEntered += () => AddThemeColorOverride("font_color", _hoverColor);
            MouseExited += () => AddThemeColorOverride("font_color", _normalColor);
            UpdateVisuals();
        }

        public void Setup(string name, uint count, int quality, uint itemId)
        {
            ItemName = name;
            ItemCount = count;
            Quality = quality;
            ItemId = itemId;
            UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            Text = $"{ItemName}×{ItemCount}";
            _normalColor = ItemIconCatalog.GetQualityColor(Quality);
            AddThemeColorOverride("font_color", _normalColor);
        }

        [Signal]
        public delegate void RightClickedEventHandler(uint itemId, uint count);

        public override void _GuiInput(InputEvent @event)
        {
            if (@event is InputEventMouseButton mouse && mouse.Pressed && mouse.ButtonIndex == MouseButton.Right)
            {
                EmitSignal(SignalName.RightClicked, ItemId, ItemCount);
                AcceptEvent();
            }
        }

        public override Variant _GetDragData(Vector2 atPosition)
        {
            SetDragPreview(CreateDragPreview());
            return this;
        }

        private Control CreateDragPreview()
        {
            var label = new Label
            {
                Text = Text,
                Modulate = new Color(1, 1, 1, 0.7f)
            };
            label.AddThemeFontSizeOverride("font_size", 16);
            label.AddThemeColorOverride("font_color", ItemIconCatalog.GetQualityColor(Quality));
            return label;
        }
    }
}
```

- [ ] **Step 2: 编译检查**

Run: `dotnet build clinetcsharp/ClinetCSharp.csproj`

Expected: 编译成功。

- [ ] **Step 3: 提交**

```bash
git add clinetcsharp/Scripts/TextInventoryItem.cs
git commit -m "feat: add text inventory item control"
```

---

## Task 3: 创建 `TextInventoryContainer`

**Files:**
- Create: `clinetcsharp/Scripts/TextInventoryContainer.cs`

- [ ] **Step 1: 实现流式容器**

```csharp
using Godot;
using System.Collections.Generic;

namespace ClinetCSharp
{
    [GlobalClass]
    public partial class TextInventoryContainer : Control
    {
        [Export] public int LineWidth = 30;
        [Export] public int LineCount = 10;
        [Export] public int FontSize = 16;
        [Export] public float LineSpacing = 8f;

        private List<TextInventoryLayout.ItemEntry> _entries = new();
        private List<TextInventoryItem> _items = new();
        private int _dragTargetIndex = -1;

        public void SetItems(List<TextInventoryLayout.ItemEntry> entries)
        {
            _entries = entries;
            Rebuild();
        }

        public List<TextInventoryLayout.ItemEntry> GetItems()
        {
            return _entries;
        }

        public (int used, int total) GetCapacity()
        {
            int total = LineWidth * LineCount;
            int used = 0;
            foreach (var entry in _entries)
            {
                used += (int)Mathf.Ceil(TextInventoryLayout.MeasureItemWidth(entry.Name, entry.Count));
            }
            return (used, total);
        }

        private void Rebuild()
        {
            foreach (var child in _items)
                child.QueueFree();
            _items.Clear();

            foreach (var entry in _entries)
            {
                var item = new TextInventoryItem();
                item.Setup(entry.Name, entry.Count, entry.Quality, entry.ItemId);
                item.RightClicked += OnItemRightClicked;
                AddChild(item);
                _items.Add(item);
            }

            ArrangeItems();
        }

        private void ArrangeItems()
        {
            float xUnit = FontSize;
            float yOffset = 0f;
            float xOffset = 0f;

            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                float itemWidth = TextInventoryLayout.MeasureItemWidth(item.ItemName, item.ItemCount) * xUnit;
                float spacing = i > 0 ? GetSpacingPixels() : 0f;

                if (xOffset + spacing + itemWidth > LineWidth * xUnit && xOffset > 0)
                {
                    xOffset = 0f;
                    yOffset += FontSize + LineSpacing;
                }

                xOffset += spacing;
                item.Position = new Vector2(xOffset, yOffset);
                item.Size = new Vector2(itemWidth, FontSize);
                xOffset += itemWidth;
            }
        }

        private float GetSpacingPixels()
        {
            return FontSize * 0.5f;
        }

        public override void _Draw()
        {
            if (_dragTargetIndex >= 0 && _dragTargetIndex <= _items.Count)
            {
                float y = GetInsertY(_dragTargetIndex);
                DrawLine(new Vector2(0, y), new Vector2(Size.X, y), Colors.Yellow, 2f);
            }
        }

        private float GetInsertY(int index)
        {
            if (index <= 0) return 0f;
            if (index >= _items.Count) return _items[_items.Count - 1].Position.Y + FontSize;
            return _items[index].Position.Y;
        }

        [Signal]
        public delegate void ItemRightClickedEventHandler(uint itemId, uint count);

        public override bool _CanDropData(Vector2 atPosition, Variant data)
        {
            return data.VariantType == Variant.Type.Object && data.AsGodotObject() is TextInventoryItem;
        }

        private void OnItemRightClicked(uint itemId, uint count)
        {
            EmitSignal(SignalName.ItemRightClicked, itemId, count);
        }

        public override void _DropData(Vector2 atPosition, Variant data)
        {
            if (data.AsGodotObject() is not TextInventoryItem draggedItem)
                return;

            int fromIndex = _items.IndexOf(draggedItem);
            if (fromIndex < 0) return;

            int toIndex = CalculateInsertIndex(atPosition);
            if (toIndex == fromIndex || toIndex == fromIndex + 1) return;

            var entry = _entries[fromIndex];
            _entries.RemoveAt(fromIndex);
            if (toIndex > fromIndex) toIndex--;
            _entries.Insert(toIndex, entry);

            Rebuild();
            _dragTargetIndex = -1;
            QueueRedraw();
        }

        private int CalculateInsertIndex(Vector2 position)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                if (position.Y < item.Position.Y + item.Size.Y / 2)
                    return i;
            }
            return _items.Count;
        }
    }
}
```


- [ ] **Step 2: 编译检查**

Run: `dotnet build clinetcsharp/ClinetCSharp.csproj`

Expected: 编译成功。

- [ ] **Step 3: 提交**

```bash
git add clinetcsharp/Scripts/TextInventoryContainer.cs
git commit -m "feat: add text inventory flow container"
```

---

## Task 4: 改造 `InventoryUI.Build.cs`

**Files:**
- Modify: `clinetcsharp/Scripts/InventoryUI.Build.cs`

- [ ] **Step 1: 替换网格构建为文字容器**

```csharp
using Godot;

namespace ClinetCSharp
{
    public partial class InventoryUI
    {
        private Label _capacityLabel;
        private TextInventoryContainer _inventoryContainer;

        private void BuildContent()
        {
            _content.AddChild(new HSeparator());

            _capacityLabel = new Label
            {
                Text = "0 / 300",
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            _capacityLabel.AddThemeFontSizeOverride("font_size", 12);
            _capacityLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
            _content.AddChild(_capacityLabel);

            _inventoryContainer = new TextInventoryContainer
            {
                LineWidth = 30,
                LineCount = 10,
                FontSize = 16,
                CustomMinimumSize = new Vector2(480, 260),
            };
            _inventoryContainer.ItemRightClicked += OnItemRightClicked;
            _content.AddChild(_inventoryContainer);
        }

        private void UpdateCapacityLabel()
        {
            var (used, total) = _inventoryContainer.GetCapacity();
            _capacityLabel.Text = $"{used} / {total}";

            if (used >= total)
                _capacityLabel.AddThemeColorOverride("font_color", new Color(1f, 0.4f, 0.4f));
            else if (used >= total * 0.8f)
                _capacityLabel.AddThemeColorOverride("font_color", new Color(1f, 0.7f, 0.2f));
            else
                _capacityLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
        }
    }
}
```

- [ ] **Step 2: 删除旧方法**

从 `InventoryUI.Build.cs` 中删除：
- `BuildItemButton`
- `BuildEmptySlotButton`
- `CreateSlotBackground`

- [ ] **Step 3: 编译检查**

Run: `dotnet build clinetcsharp/ClinetCSharp.csproj`

Expected: 编译成功（此时 Actions 仍引用旧方法，会失败，先跳过 Actions 的编译，或下一步一起修）。

---

## Task 5: 改造 `InventoryUI.Actions.cs`

**Files:**
- Modify: `clinetcsharp/Scripts/InventoryUI.Actions.cs`

- [ ] **Step 1: 重写刷新与交互**

```csharp
using Godot;
using System.Collections.Generic;

namespace ClinetCSharp
{
    public partial class InventoryUI
    {
        private void OnInventoryChanged()
        {
            RefreshInventory();
        }

        private void RefreshInventory()
        {
            if (_inventoryContainer == null)
                return;

            var manager = UiServices.GetInventoryManager(this);
            if (manager == null)
            {
                _inventoryContainer.SetItems(new List<TextInventoryLayout.ItemEntry>());
                UpdateCapacityLabel();
                return;
            }

            var entries = new List<TextInventoryLayout.ItemEntry>();
            foreach (var slot in manager.Items)
            {
                entries.Add(new TextInventoryLayout.ItemEntry
                {
                    ItemId = slot.ItemId,
                    Name = slot.Name,
                    Count = slot.Count,
                    Quality = manager.GetItemQuality(slot.ItemId),
                });
            }

            _inventoryContainer.SetItems(entries);
            UpdateCapacityLabel();
        }

        private void OnItemRightClicked(uint itemId, uint count)
        {
            var popup = new PopupMenu();
            popup.AddItem("使用 x1", 0);
            popup.AddItem("丢弃 x1", 1);
            popup.AddItem("丢弃全部", 2);

            popup.IdPressed += id =>
            {
                var manager = UiServices.GetInventoryManager(this);
                if (manager == null)
                    return;

                if (id == 0)
                    manager.SendUseItem(itemId, 1);
                else if (id == 1)
                    manager.SendDropItem(itemId, 1);
                else if (id == 2)
                    manager.SendDropItem(itemId, count);

                popup.QueueFree();
            };

            popup.PopupHide += () => popup.QueueFree();
            AddChild(popup);
            popup.Position = new Vector2I(
                Mathf.RoundToInt(GetViewport().GetMousePosition().X),
                Mathf.RoundToInt(GetViewport().GetMousePosition().Y));
            popup.Popup();
        }
    }
}
```

- [ ] **Step 2: 删除旧方法**

从 `InventoryUI.Actions.cs` 中删除：
- `RefreshGrid`
- `FillEmptySlots`
- `OnItemClicked`

- [ ] **Step 3: 编译检查**

Run: `dotnet build clinetcsharp/ClinetCSharp.csproj`

Expected: 编译成功。

- [ ] **Step 4: 提交**

```bash
git add clinetcsharp/Scripts/InventoryUI.Build.cs clinetcsharp/Scripts/InventoryUI.Actions.cs
git commit -m "feat: refactor InventoryUI to text flow layout"
```

---

## Task 6: 更新 `InventoryUI.cs` 生命周期

**Files:**
- Modify: `clinetcsharp/Scripts/InventoryUI.cs`

- [ ] **Step 1: 调整初始化与焦点刷新**

```csharp
protected override void OnPanelInitialized()
{
    _content = GetNodeOrNull<VBoxContainer>("VBoxContainer/Content");
    if (_content == null)
        return;

    CenterOnViewport();
    BuildContent();

    var inventoryManager = UiServices.GetInventoryManager(this);
    if (inventoryManager != null)
        inventoryManager.InventoryChanged += OnInventoryChanged;

    SetToggleKey(Key.I);
    RefreshInventory();
}

protected internal override void NotifyFocusGained()
{
    RefreshInventory();
}
```

- [ ] **Step 2: 编译检查**

Run: `dotnet build clinetcsharp/ClinetCSharp.csproj`

Expected: 编译成功。

- [ ] **Step 3: 提交**

```bash
git add clinetcsharp/Scripts/InventoryUI.cs
git commit -m "feat: wire InventoryUI lifecycle to text container"
```

---

## Task 7: 空背包提示

**Files:**
- Modify: `clinetcsharp/Scripts/TextInventoryContainer.cs`

- [ ] **Step 1: 在 `_Draw` 中绘制空状态**

在 `TextInventoryContainer._Draw()` 方法开头追加：

```csharp
public override void _Draw()
{
    if (_items.Count == 0)
    {
        DrawString(GetThemeDefaultFont(), new Vector2(Size.X / 2 - 20, Size.Y / 2), "（空）", HorizontalAlignment.Left, -1, 16, new Color(0.4f, 0.4f, 0.4f));
        return;
    }

    if (_dragTargetIndex >= 0 && _dragTargetIndex <= _items.Count)
    {
        float y = GetInsertY(_dragTargetIndex);
        DrawLine(new Vector2(0, y), new Vector2(Size.X, y), Colors.Yellow, 2f);
    }
}
```

- [ ] **Step 2: 编译检查**

Run: `dotnet build clinetcsharp/ClinetCSharp.csproj`

Expected: 编译成功。

- [ ] **Step 3: 提交**

```bash
git add clinetcsharp/Scripts/TextInventoryContainer.cs
git commit -m "feat: show empty hint in text inventory"
```

---

## Task 8: 运行测试并验证

**Files:**
- All of the above

- [ ] **Step 1: 运行单元测试**

Run: `dotnet test clinetcsharp/ClinetCSharp.Tests/ClinetCSharp.Tests.csproj`

Expected: 所有测试通过。

- [ ] **Step 2: 启动 Godot 进行视觉验证**

Run: 打开 Godot 编辑器，运行项目，按 `I` 打开背包。

Checklist:
- [ ] 道具以“名字×数量”形式显示
- [ ] 不同品质显示不同颜色
- [ ] 顶部容量显示正确
- [ ] 左键拖动可重新排序
- [ ] 右键弹出使用/丢弃菜单
- [ ] 空背包显示“（空）”

- [ ] **Step 3: 提交最终版本**

```bash
git add .
git commit -m "feat: text-based inventory panel v1"
```

---

## 非本版范围（后续迭代）

- ~~服务器端按文字长度校验容量~~（已实现）
- ~~部分拾取逻辑与服务器同步~~（已实现）
- DropManager 接入主流程（怪物死亡掉落 + 自动拾取）
- 玩家自定义排序持久化
- 移动端长按拖动适配
- 乘号与数字的等宽字体替换

---

## 自检

- **Spec coverage:**
  - 文字流式布局 ✓ Task 4
  - 字符宽度规则 ✓ Task 1
  - 拖动重排 ✓ Task 3
  - 右键菜单 ✓ Task 2（信号）、Task 3（转发）、Task 4（订阅）、Task 5（弹出菜单）
  - 容量显示 ✓ Task 4
  - 空状态 ✓ Task 7
  - 满状态 ✓ Task 4
  - 单条不允许截断 ✓ Task 1 (`InvalidOperationException`)

- **Placeholder scan:** 无 TBD/TODO。

- **Type consistency:** `ItemEntry` 在 Task 1 定义，Task 3/5 使用；字段名一致。
