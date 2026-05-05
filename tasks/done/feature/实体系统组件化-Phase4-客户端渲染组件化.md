# 实体系统组件化重构 — Phase 4: 客户端渲染组件化

## 类型
feature

## 来源
docs/design/实体系统组件化重构.md Phase 4

## 目标
客户端 EntityBase 瘦身，渲染逻辑拆成组件，按 DrawOrder 统一绘制

## 具体任务

### 1. IRenderComponent 接口 + DrawOrder 约定
- IRenderComponent: DrawOrder/OnAttach/OnDetach/Draw
- DrawOrder: 0=外观, 100=血条/MP条, 200=施法条/动作栏, 300=标签, 400=等级徽章

### 2. EntityBase 改造
- 添加 _renderComponents 列表 + AddRenderComponent/GetRenderComponent
- _Draw 中按 DrawOrder 遍历渲染组件
- 保留所有现有 setter 做 Facade 转发（外部零改动）

### 3. 渲染组件实现
- AppearanceComponent (DrawOrder=0) — DrawBody
- HealthBarComponent (DrawOrder=100) — 血条
- MpBarComponent (DrawOrder=100) — MP条
- CastBarComponent (DrawOrder=200) — 施法条
- ActionBarComponent (DrawOrder=200) — 动作栏
- LabelComponent (DrawOrder=300) — Monster/Npc 名字标签
- RichLabelComponent (DrawOrder=300) — Player 名字标签
- LevelBadgeComponent (DrawOrder=400) — 等级徽章

### 4. Player/Monster/Npc 适配
- 构造时按 EntityType 添加渲染组件
- 移除子类中的重复绘制逻辑

## 验证
- 客户端编译通过
- 实体显示正常（血条、MP条、名字标签、施法条）
- DebugPanel 无需改动（Facade 模式）

## 风险
中 — 改动面大，但 Facade 模式保证外部接口不变
