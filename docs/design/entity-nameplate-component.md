# 实体铭牌组件设计文档

## 背景

调试面板的「实体」标签页目前支持外观、4 行标签、血条/MP 条等组件。用户希望增加一种新的标签背景样式：

- 上下两条横贯角色外框宽度的填充色块（无边框），不超过角色边框。
- 中间一个较窄的居中色块，作为某一行文字标签的背景板。
- 三个色块均可在调试面板中调整颜色、高度、间距、垂直偏移等参数。
- 以可复用组件形式提供，手动添加到实体 Profile 中。

## 设计目标

1. 不破坏现有标签系统，仅作为**背景板**存在。
2. 所有视觉参数均可通过调试面板实时调整并持久化。
3. 作为独立组件接入现有的 `ComponentRegistry` / `EntityProfileManager` 体系。
4. 支持所有实体类型（player / monster / npc / decoration），默认 Profile 中不自动添加，由用户手动启用。

## 组件命名

- 组件名（唯一标识）：`nameplate`
- 显示名：`铭牌背景`
- 分类：`外观`

## 数据结构 NameplateData

```csharp
public class NameplateData : IComponentData
{
    public bool Visible = false;          // 是否启用
    public float YOffset = -80f;          // 整体垂直偏移（相对于实体中心）
    public float Spacing = 4f;            // 顶栏/中块/底栏之间的间距

    public float BarHeight = 6f;          // 顶栏与底栏的共用高度
    public Color BarColor = new Color(0.1f, 0.1f, 0.1f, 0.7f);

    public float CenterBoxHeight = 24f;   // 中块高度
    public float CenterBoxWidthScale = 0.6f; // 中块宽度占实体外框宽度的比例 (0.1~1.0)，默认居中
    public Color CenterBoxColor = new Color(0.1f, 0.1f, 0.1f, 0.85f);
}
```

> 注：
> - 中块不渲染文字，仅提供背景；文字仍由现有 `labels` 组件负责。
> - 顶栏与底栏**共用**一个高度参数 `BarHeight`。
> - 铭牌整体宽度以**实体视觉外框宽度**（`VisualOuterSizeX` / `VisualOuterSize`）为约束，不超出角色边框；顶栏/底栏宽度等于实体外框宽度，中块按 `CenterBoxWidthScale` 居中缩放。

## 渲染组件 NameplateRenderComponent

- 实现 `IRenderComponent`。
- `DrawOrder = 290`：在血条/施法条（100~200）之后、文字标签（300）之前绘制，确保文字压在上面。
- 绘制内容：
  1. 顶栏：宽度 = 实体视觉外框宽度（`VisualOuterSizeX` / `VisualOuterSize`），高度 = `BarHeight`，填充 `BarColor`。
  2. 中块：宽度 = 实体视觉外框宽度 * `CenterBoxWidthScale`，高度 = `CenterBoxHeight`，填充 `CenterBoxColor`，水平居中。
  3. 底栏：同顶栏。
- 垂直布局以 `YOffset` 为基准，按 `BarHeight -> Spacing -> CenterBoxHeight -> Spacing -> BarHeight` 顺序排列，整体以 `YOffset` 为中心对称分布。

## 调试面板组件 NameplateComponent

实现 `IEntityTabComponent`，提供以下 UI：

| 参数 | 控件 | 范围/说明 |
|------|------|-----------|
| 启用 | CheckButton | 开关 |
| 垂直偏移 | HSlider + 数值 | -200 ~ 200 |
| 间距 | HSlider + 数值 | 0 ~ 20 |
| 顶/底栏高度 | HSlider + 数值 | 1 ~ 30（共用） |
| 顶/底栏颜色 | ColorPickerButton | RGBA（共用） |
| 中块高度 | HSlider + 数值 | 1 ~ 60 |
| 中块宽度比例 | HSlider + 数值 | 0.1 ~ 1.0，默认居中 |
| 中块颜色 | ColorPickerButton | RGBA |

## 接入点

1. `ComponentRegistry`：注册 `nameplate` 工厂。
2. `EntityProfileManager.ApplyProfile`：读取 `NameplateData` 写入 `EntityBase` 的属性，并调用 `SyncRenderComponent<NameplateRenderComponent>`。
2.1 `EntityProfileManager.Serialization`：`WriteComponentData` / `ReadComponentData` 增加 `NameplateData` / `"nameplate"` 分支，负责铭牌配置的持久化（颜色需连 alpha 一起读写）。**新增任何组件都必须同步接入此处，否则配置无法落盘、重启后丢失。**
3. `EntityBase`：新增铭牌相关属性（`NameplateVisible`, `NameplateYOffset`, `NameplateSpacing`, `NameplateBarHeight/Color`, `NameplateCenterBoxHeight/WidthScale/Color`）。
4. 默认 Profile 不添加 `nameplate` 组件，保持向后兼容。

## 文件变更清单

- 新增 `Scripts/ComponentData/NameplateData.cs`
- 新增 `Scripts/Components/NameplateComponent.cs`
- 新增 `Scripts/RenderComponents/NameplateRenderComponent.cs`
- 修改 `Scripts/ComponentRegistry.cs`（注册组件）
- 修改 `Scripts/EntityBase.cs`（新增属性）
- 修改 `Scripts/EntityProfileManager.cs`（ApplyProfile 与 SyncRenderComponents）
- 修改 `Scripts/EntityProfileManager.Serialization.cs`（WriteComponentData / ReadComponentData 增加 nameplate 序列化）
- 修改 `docs/调试面板说明.md`（补充铭牌组件说明）

## 兼容性

- 旧配置中没有 `nameplate` 组件，不会触发渲染，也不影响现有实体。
- 新增组件不会自动加入默认 Profile，用户需手动在「组件管理」中添加。
