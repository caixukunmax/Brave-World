# 动作栏强制显示链路修复

> 版本：v0.1
> 状态：实施中
> 范围：`clinetcsharp/Scripts/EntityBase*`、`RenderComponents/ActionBarComponent.cs`

## 1. 问题

当前调试面板里的“动作栏”组件暴露了：

1. 强制显示
2. 文字 Y 偏移
3. 进度条高度

但运行时链路不完整：

1. `force_show` 会保存到 `ActionBarData`
2. `EntityProfileManager` 没有把它重新应用回实体
3. `ActionBarForceShow` 只存在于 `Player`
4. 真正的强制预览逻辑又写在 `Player.Rendering._Draw()` 的特殊分支里

结果就是：

1. 调试面板里勾了“动作栏”，实体上没反应
2. `文字Y偏移 / 进度条高度` 只有在恰好已有施法数据时才看得见
3. 玩家和怪物的动作栏能力不一致

## 2. 目标

把动作栏强制显示收口成一条统一链路：

1. `ActionBarData.ForceShow`
2. `EntityProfileManager.ApplyProfileToAll`
3. `EntityBase.ActionBarForceShow`
4. `RenderComponents.ActionBarComponent.Draw`

## 3. 约束

1. `ActionBarForceShow` 是所有实体通用能力，不只属于 `Player`
2. 预览逻辑不能再藏在 `Player.Rendering` 特判里
3. 开启强制显示时，即使 `CastingSkill` 为空，也要给出稳定预览内容

## 4. 预览策略

强制显示开启但实体当前没有真实施法数据时：

1. 技能名使用 `"动作栏预览"`
2. 进度使用 `0.6`

这样调试偏移和高度时总能看到即时反馈。
