# 实体条形长度比例基准统一

> 版本：v0.1
> 状态：实施中
> 范围：`clinetcsharp/Scripts/EntityBase.cs`、`BarGroupComponent.cs`、`CastBarComponent.cs`

## 1. 问题

当前血条、MP 条、施法条的 `长度比例` 是按 `GridSize` 计算的：

`Length = GridSize × LengthScale`

这会带来一个语义偏差：

1. 实体本体视觉宽度已经允许通过 `VisualSizeScale` 调整
2. 但条形长度仍然以格子宽度为基准
3. 结果就是当用户把 `长度比例=1` 时，视觉上不一定等于“和当前实体一样宽”

对怪物和 NPC 来说，这个体验尤其怪，因为用户直觉上会把“长度比例=1”理解成“正好铺满这只实体”。

## 2. 目标

统一条形长度比例的语义：

1. `长度比例 = 1`
2. 代表条形长度 = 当前实体的总外轮廓宽度
3. 而不是固定等于地图格子宽度

## 3. 方案

将长度计算基准从 `GridSize` 改为 `VisualOuterSize`：

- 血条：`HealthBarLength = VisualOuterSize × HealthBarLengthScale`
- MP 条：`MpBarLength = VisualOuterSize × MpBarLengthScale`
- 施法条：`CastBarLength = VisualOuterSize × CastBarLengthScale`

这样：

1. 当实体本体缩小或放大时，条形控件会保持同一套相对语义
2. `长度比例=1` 会稳定表现为“和实体一样宽”
3. 怪物、NPC、玩家都走同一条规则

## 4. UI 同步

调试面板里的“长度”只读值也必须改成按同一基准显示，避免：

1. 真实绘制长度已经变了
2. 只读显示还在按 `GridSize × Scale`
3. 造成“数值和视觉不一致”
