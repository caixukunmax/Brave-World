# 角色背包面板问题分析

## 问题背景
根据 2026-07-01-角色UI碎碎念.md，角色面板（ItemInfoPopup）需要正确显示道具属性。当前面板已挂载，但内容可能为空或错误。

## 根因分析

### 1. 服务端 ItemInfoPopup 中属性未设置

检查 `clinetcsharp/Scripts/UI/Popups/ItemInfoPopup.cs`，重点关注 `Refresh()` 方法。

```csharp
// 当前可能的实现（推测）
public void Refresh(int itemId)
{
    var item = InventoryManager.GetItem(itemId); // 或类似获取
    if (item == null) return;
    
    NameLabel.Text = item.Name; // 有值
    // 但以下属性可能未设置：
    // AttackLabel.Text = ???
    // DefenseLabel.Text = ???
    // 其他属性...
}
```

### 2. 数据结构问题

`ItemData`（或类似）结构体可能不包含战斗属性，或者属性名与 UI 控件不匹配。

| 字段 | 可能来源 | 说明 |
|------|----------|------|
| Name | tbitem.name | 已配置 |
| Icon | tbitem.icon | 已配置 |
| Quality | tbitem.quality | 已配置 |
| Attack | ??? | 未确认来源 |
| Defense | ??? | 未确认来源 |
| HP | ??? | 未确认来源 |

### 3. 配置表缺失

`tbitem` 配置表可能没有 `attack`/`defense` 等战斗属性字段，或者角色面板需要从其他配置（如装备配置表）读取。

## 修复方案

### 方案 A：扩展 ItemData 结构（如果道具本身带属性）

在 `tbitem` 配置表增加字段：
```json
{
  "id": 1001,
  "name": "铁剑",
  "attack": 10,  // 新增
  "defense": 0,  // 新增
  "hp": 0        // 新增
}
```

同步修改 Luban 配置定义和 `ItemData` 结构体。

### 方案 B：从装备配置读取（如果装备才有属性）

若只有装备（equip）有属性，需：
1. 检查 `tbequip` 配置表
2. 在 `ItemInfoPopup` 中区分普通道具和装备
3. 装备从 `tbequip` 读取属性显示

### 方案 C：服务端下发完整属性（推荐）

服务端在 `Item` 或 `InventoryItem` 协议中增加属性字段，客户端直接显示。

```protobuf
// 在 Item.proto 或相关协议中增加
message Item {
    int32 item_id = 1;
    int32 count = 2;
    int32 quality = 3;
    // 新增：
    int32 attack = 10;
    int32 defense = 11;
    int32 hp = 12;
    // ... 其他属性
}
```

## 待确认事项

1. 角色面板需要显示的属性列表是什么？（攻击/防御/血量/暴击/...？）
2. 这些属性是道具自带还是装备特有？
3. 当前 `tbitem` 配置表有哪些字段？
4. 是否有 `tbequip` 或类似装备配置表？

## 建议

先确认配置表结构，再决定走方案 A/B/C。如需我检查具体代码，请提供：
- `ItemInfoPopup.cs` 完整代码
- `tbitem` 配置表字段列表
- `ItemData` 结构体定义
