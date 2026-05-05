# 调试面板实体 Tab 组件化重构计划 v2（续）

## 6. 迁移步骤

### Phase 1: 数据层重构（不改 UI）

1. 创建 `IComponentData` 接口和各组件数据类
2. 创建 `EntityProfile` 类
3. 创建 `EntityProfileManager` 类
4. 在 `EntityBase` 添加 `ProfileId` 属性
5. 编写 `EntityStyleConfig → EntityProfile` 迁移方法
6. 编写配置文件兼容加载（旧格式 → 新格式自动转换）
7. **验证**：编译通过，旧配置文件能正确加载

### Phase 2: 组件接口 + 注册表

8. 创建 `IEntityTabComponent` 接口
9. 创建 `ComponentRegistry` 静态类
10. **验证**：编译通过

### Phase 3: 提取组件（逐步，新旧并行）

11. 提取 `AppearanceComponent` — 从 EntityStyleTabBase + PlayerTab
12. 提取 `LabelGroupComponent` — 从 EntityStyleTabBase + PlayerTab（含原 TextStyle）
13. 提取 `BarGroupComponent` — 整合 BarControlGroup + MonsterTab 血条
14. 提取 `CastBarComponent` — 从 PlayerTab
15. 提取 `ActionBarComponent` — 从 PlayerTab
16. 提取 `LevelBadgeComponent` — 从 PlayerTab
17. 提取 `MonsterAiComponent` — 从 MonsterTab
18. 提取 `NpcInteractComponent` — 从 NpcTab
19. 提取 `NpcInteractComponent` — 从 NpcTab
20. **每步验证**：新组件编译通过，旧代码仍可运行

### Phase 4: 新 EntityTab

21. 创建 `DebugPanelEntityTab`（含 Profile 选择器 + 组件管理器）
22. 创建 Profile 选择器 UI
23. 创建组件管理 UI（添加/删除/折叠）
24. 实现数据流：slider → component → profile → entity
25. **验证**：新 Tab 能正确显示和编辑数据

### Phase 5: 切换 + 清理

26. DebugPanel 中将三个 Tab 替换为单一 EntityTab
27. 运行验证：所有功能正常
28. 删除旧文件：
    - `DebugPanelPlayerTab.cs`
    - `DebugPanelMonsterTab.cs`
    - `DebugPanelNpcTab.cs`
    - `DebugPanelEntityStyleTabBase.cs`
29. 清理 MonsterManager/NpcManager 中旧的 StyleConfigs 相关代码
30. 更新文档

## 7. 配置文件兼容性

### 7.1 旧格式

```ini
[monster_1]
visual_size_scale=1.0
border_width_scale=0.027
hp_bar_visible=true
...

[npc_1]
visual_size_scale=1.0
...
interact_menu_offset_ax=60
```

### 7.2 新格式

```ini
[profile_1]
name=玩家
entity_type=player
components=appearance,labels,healthbar,mpbar,castbar,actionbar,levelbadge

[profile_1.appearance]
visual_size_scale=1.0
border_width_scale=0.027
...

[profile_1.healthbar]
visible=true
length_scale=0.919
...

[profile_2]
name=精英怪
entity_type=monster
components=appearance,labels,healthbar,mpbar,monster_ai

[profile_2.appearance]
...

[profile_2.monster_ai]
move_speed_ms=800
...
```

### 7.3 兼容策略

- LoadConfig 检测旧格式 section（`monster_*` / `npc_*` / `player` / `labels` / `actionbar` / `levelbadge`）
- 自动转换为 EntityProfile + 组件数据
- 保存时写新格式
- 一次性迁移，不保留旧格式写入能力

## 8. 风险与注意事项

### 8.1 统一配置驱动（重要决策）

**决策：所有实体统一走配置驱动，包括 Player。**

理由：
- 游戏未来支持多人，Player 不再是单实例
- 每个玩家实例绑定自己的 ProfileId，跟 Monster/NPC 一致
- 面板改的是 Profile 数据，通过 ApplyProfile 刷到实体
- 多人时天然支持：玩家A 用 Profile 1，玩家B 用 Profile 2

**统一 ApplyProfile 逻辑：**
```
改 Profile 数据 → EntityProfileManager.ApplyProfile(entity, profileId)
    → 读取 Profile 的各组件数据
    → 写入 entity 属性（VisualSizeScale, BorderWidthScale, ...）
    → entity.QueueRedraw()
```

不管是 Player/Monster/NPC，都是同一条路径。ApplyStyleToAll 只是遍历所有用该 Profile 的实体逐个 ApplyProfile。

**对现有代码的影响：**
- PlayerTab 里所有 `Player.SetXxx()` 调用要改成 `profile.SetData() + ApplyProfile()`
- Player 实体需要像 Monster/NPC 一样支持 `ProfileId`
- Player 的属性设置方法保留（ApplyProfile 内部调用它们），但面板不再直接调用

### 8.2 双向同步 + 服务端权威（重要决策）

**决策：调试面板保存的只是预览值，服务端推送的是真值。真值覆盖预览，真值到达后面板锁定不可编辑。**

**核心逻辑：**
- Profile 数据 = 预览/覆盖层，临时调整样式看效果
- 服务端推送 = 真数据，真数据来了直接覆盖预览值
- 真数据到达后，面板显示真值且不可编辑（灰色/锁定）
- 用户在面板里改的只是预览，不会影响服务端的真值

**场景举例：**
```
1. 服务端推送：玩家名字="张三"，颜色=红色
   → 实体显示"张三"红色
   → 面板显示"张三"红色，输入框锁定（灰色）

2. 用户在面板里改名字为"李四"（预览）
   → 实体临时显示"李四"（预览覆盖）
   → 面板显示"李四"，输入框可编辑（预览模式标记）

3. 服务端再次推送：玩家名字="王五"
   → 实体显示"王五"（真值覆盖预览）
   → 面板显示"王五"，输入框重新锁定
   → 之前的"李四"预览被丢弃
```

**属性状态标记：**
每个属性有两种状态：
- **服务端权威**（locked）：面板只读，灰色背景，显示真值
- **本地预览**（preview）：面板可编辑，有预览标记（小图标或不同边框颜色）

**EntityBase 加信号：**
```csharp
[Signal]
public delegate void StyleChangedEventHandler();

public virtual void SetLabelText(int index, string text, bool fromServer = false)
{
    LabelTexts[index] = text;
    QueueRedraw();
    if (fromServer)
        EmitSignal(SignalName.StyleChanged);
}
```

**数据流（双向）：**
```
服务端推送 → entity.SetXxx(真值, fromServer: true) → EmitSignal(StyleChanged)
    → 面板收到信号 → SyncFromEntity() 刷新 UI
    → 标记该属性为 locked，输入框变灰

用户编辑 → profile.SetData(预览值) → ApplyProfile(entity, previewOnly: true)
    → 实体临时显示预览值（不发信号）
    → 面板标记该属性为 preview，输入框可编辑
```

**防循环**：面板推值给实体时 `fromServer: false`，不发信号。只有服务端推送 `fromServer: true` 才发信号。

**对组件接口的影响：**
```csharp
public interface IEntityTabComponent : IDisposable
{
    // ... 原有方法
    
    /// <summary>实体真值变了，从实体同步回 UI 并锁定</summary>
    void SyncFromEntity(EntityBase entity);
    
    /// <summary>获取/设置属性的锁定状态</summary>
    void SetPropertyLocked(string propertyName, bool locked);
}
```

### 8.3 Undo 系统

当前 Undo 按 TabKey 存储状态。重构后：
- EntityTab 的 CaptureUndoState 收集所有 component 的 SyncToData() 快照
- ApplyUndoState 对每个 component 调 SyncFromData()
- 组件增删本身也需要 undo（记录操作前后的 component 列表）

### 8.4 组件增删的副作用

删除一个组件 = 删除其数据 + UI。如果实体正在使用该组件的数据（比如血条），需要决定：
- 方案 A：删除组件 = 隐藏（数据保留，只是 UI 不显示）
- 方案 B：删除组件 = 真删（数据也移除，实体回退到默认值）

**建议**：方案 B，真删。调试面板本身就是实验性质，删了再加就是了。

### 8.5 组件间依赖

某些组件可能隐式依赖其他组件。比如：
- BarGroupComponent 需要 AppearanceComponent 的 VisualSizeScale 来计算实际长度
- LevelBadgeComponent 需要外观组件的 Scale

**策略**：组件通过 EntityProfile.GetData<T>() 读取其他组件数据，不直接依赖其他组件实例。如果依赖的组件不存在，使用默认值。

### 8.6 BarControlGroup 复用

现有 BarControlGroup + IBarEntityAdapter 设计良好，新 BarGroupComponent 应**内部复用** BarControlGroup 的 UI 构建逻辑，而不是重写。具体做法：BarGroupComponent 持有一个 BarControlGroup 实例，通过适配器桥接到 EntityProfile 的 BarData。

### 8.7 服务端权威粒度

当前设计是给每个属性单独加 bool 标记（ContentLocked/ColorLocked），但实际场景中哪些属性是服务端权威的应该由服务端决定。

**建议**：每个组件数据类加一个 `HashSet<string> LockedProperties`，统一记录被服务端锁定的属性名。例如：
- LabelGroupData: `LockedProperties = {"content_0", "color_0"}` — 第0行的内容和颜色被锁定
- BarData: `LockedProperties = {"fill_percent"}` — 血条填充被锁定

比单独 bool 数组更灵活，也方便服务端动态加锁/解锁。

### 8.8 EntityProfileManager 生命周期

EntityProfileManager 需要被 DebugPanel 和 EntityBase 都访问到，文档未明确其生命周期。

**建议**：作为 Godot Autoload 单例（类似 MonsterManager/NpcManager 的现有模式），全局可访问。

### 8.9 MonsterAiComponent 数据归属

MonsterAi 的数据（移速/巡逻范围/仇恨范围）目前走 MonsterConfigManager.SaveConfig() 保存到 JSON，不走 EntityStyleConfig。重构后需要明确：

**建议**：MonsterAiData 放进 EntityProfile，和其他组件数据统一管理。MonsterConfigManager 降级为纯运行时管理器（不再负责持久化），配置保存/加载全走 EntityProfileManager。

### 8.10 血条填充也是服务端权威

BarData 的 FillPercent（HP/MP 比例）是服务端推送的，也需要锁定/预览机制。当前 BarData 没有这个设计。

**建议**：BarData 也加 LockedProperties，fill_percent 被服务端锁定时面板只读。

### 8.11 默认 Profile 初始化

首次运行（无配置文件）时，需要创建默认 Profile：
- id=1, name="玩家", type=player, components=appearance+labels+healthbar+mpbar+castbar+actionbar+levelbadge
- id=2, name="怪物", type=monster, components=appearance+labels+healthbar+mpbar+monster_ai
- id=3, name="NPC", type=npc, components=appearance+labels+npc_interact

### 8.12 组件排序

组件在 UI 中的显示顺序需要可控。建议给每个组件一个 SortOrder：
- appearance: 100
- labels: 200
- healthbar: 400
- mpbar: 410
- castbar: 420
- actionbar: 500
- levelbadge: 510
- monster_ai: 600
- npc_interact: 700

用户添加的组件追加到末尾（SortOrder=800+）。

## 9. 预期收益

| 指标 | 重构前 | 重构后 |
|------|--------|--------|
| PlayerTab 代码量 | 85 KB | ~15 KB（组装代码） |
| 重复外观代码 | 3 份 | 1 份（AppearanceComponent） |
| 重复标签代码 | 3 份 | 1 份（LabelGroupComponent） |
| 重复血条代码 | 2 套实现 | 1 套（BarGroupComponent） |
| 新增实体类型 | 复制粘贴 | 写 DataAdapter + 挂组件 |
| 组件复用 | 无 | 任意组合 |
| 运行时增删组件 | 不支持 | 支持 |
| 配置可读性 | 只有数字 ID | 名称+ID |

## 10. 新文件结构

```
Scripts/
├── DebugPanel.cs (不变)
├── DebugPanel.*.cs (不变)
├── DebugPanelTab.cs (不变)
├── DebugPanelEntityTab.cs          ← 新：统一实体 Tab
├── EntityProfile.cs                ← 新：配置档案
├── EntityProfileManager.cs         ← 新：Profile 管理器
├── IComponentData.cs               ← 新：组件数据接口
├── ComponentData/                   ← 新目录
│   ├── AppearanceData.cs
│   ├── LabelGroupData.cs
│   ├── BarData.cs
│   ├── CastBarData.cs
│   ├── ActionBarData.cs
│   ├── LevelBadgeData.cs
│   ├── MonsterAiData.cs
│   └── NpcInteractData.cs
├── IEntityTabComponent.cs          ← 新：组件 UI 接口
├── ComponentRegistry.cs            ← 新：组件注册表
├── Components/                      ← 新目录
│   ├── AppearanceComponent.cs
│   ├── LabelGroupComponent.cs
│   ├── BarGroupComponent.cs
│   ├── CastBarComponent.cs
│   ├── ActionBarComponent.cs
│   ├── LevelBadgeComponent.cs
│   ├── MonsterAiComponent.cs
│   └── NpcInteractComponent.cs
├── BarControlGroup.cs              ← 保留，内部复用
├── BarControlGroup.Main.cs         ← 保留
├── DebugPanelLengthScalePolicy.cs  ← 保留
└── ... (其他不变)
```

删除：
- `DebugPanelPlayerTab.cs`
- `DebugPanelMonsterTab.cs`
- `DebugPanelNpcTab.cs`
- `DebugPanelEntityStyleTabBase.cs`

## 11. 组件详细设计

### 11.1 LabelGroupComponent（合并原 TextStyleComponent）

全局设置作为默认值，每行标签可覆盖。内容和颜色是服务端权威属性，面板里只是预览。

```
LabelGroupComponent
├── 全局设置
│   ├── 字体选择 + 加载
│   ├── 默认字号（0=自动）
│   ├── 默认颜色
│   └── 加粗 / 斜体 / 阴影
└── 每行标签
    ├── 可见性
    ├── 名称
    ├── 内容预览（服务端权威时锁定🔒，本地可预览修改✏️）
    ├── 字号（0=跟随全局）
    ├── 颜色预览（服务端权威时锁定🔒，本地可预览修改✏️）
    ├── X偏移 / 居中开关
    ├── Y偏移
    └── 重置
```

**已移除的属性：**
- 行间距 → 每行独立 Y 偏移替代
- 字间距 → 暂不需要
- 默认对齐 → 每行独立居中开关替代

**服务端权威 vs 本地预览：**
- 锁定状态：输入框/颜色按钮灰色背景，旁边显示 🔒 或"服务端"标签
- 预览状态：正常可编辑，旁边显示 ✏️ 或"预览"标签
- 服务端推送真值 → 覆盖预览 → 锁定
- 用户修改 → 仅预览生效 → 服务端再推则丢弃预览

**数据类：**
```csharp
public class LabelGroupData : IComponentData
{
    // 全局
    public string FontName = "";
    public int DefaultFontSize = 0;       // 0=自动
    public Color DefaultColor = Colors.Black;
    public bool Bold = false;
    public bool Italic = false;
    public bool Shadow = false;

    // 每行（索引 0~N-1）
    public bool[] Visible = { true, true, true, true };
    public string[] Names = { "", "", "", "" };
    public string[] ContentPreview = { "", "", "", "" };  // 预览值
    public int[] FontSizes = { 0, 0, 0, 0 };                 // 0=跟随全局
    public Color[] ColorPreview = { Colors.Black, ... };      // 预览值
    public float[] XOffset = { 0, 0, 0, 0 };
    public bool[] CenterX = { true, true, true, true };
    public float[] YOffset = { 0, 0, 0, 0 };

    // 服务端权威标记（哪些属性被服务端锁定了）
    // 格式："content_0" 表示第0行内容被锁定，"color_1" 表示第1行颜色被锁定
    public HashSet<string> LockedProperties = new();

    public bool IsContentLocked(int i) => LockedProperties.Contains($"content_{i}");
    public bool IsColorLocked(int i) => LockedProperties.Contains($"color_{i}");
    public void LockContent(int i) => LockedProperties.Add($"content_{i}");
    public void UnlockContent(int i) => LockedProperties.Remove($"content_{i}");
    public void LockColor(int i) => LockedProperties.Add($"color_{i}");
    public void UnlockColor(int i) => LockedProperties.Remove($"color_{i}");

    public IComponentData Clone()
    {
        var clone = (LabelGroupData)MemberwiseClone();
        clone.Visible = (bool[])Visible.Clone();
        clone.Names = (string[])Names.Clone();
        clone.ContentPreview = (string[])ContentPreview.Clone();
        clone.FontSizes = (int[])FontSizes.Clone();
        clone.ColorPreview = (Color[])ColorPreview.Clone();
        clone.XOffset = (float[])XOffset.Clone();
        clone.CenterX = (bool[])CenterX.Clone();
        clone.YOffset = (float[])YOffset.Clone();
        clone.LockedProperties = new HashSet<string>(LockedProperties);
        return clone;
    }
}
```