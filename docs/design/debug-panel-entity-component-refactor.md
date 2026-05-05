# 调试面板实体 Tab 组件化重构计划 v2

## 1. 现状分析

### 1.1 当前架构

```
DebugPanelTab (基类，通用 slider/helper)
├── DebugPanelEntityStyleTabBase (怪物/NPC 共用基类)
│   ├── DebugPanelMonsterTab
│   └── DebugPanelNpcTab
├── DebugPanelPlayerTab (独立，直接继承 DebugPanelTab)
├── DebugPanelMapTab
├── DebugPanelSystemTab
└── DebugPanelUITab
```

### 1.2 核心问题

1. **PlayerTab 是 85KB 巨无霸**，Monster/NPC 虽已抽象但 Player 没走这条路
2. **重复代码**：外观/标签/血条三份，配置保存/加载三份
3. **数据模型不统一**：Monster/NPC → EntityStyleConfig（多 ID），Player → 直接操作实体（单实例）
4. **组件固定**：编译时写死"怪物有血条+AI"，不能运行时增删
5. **配置只有 ID 没有名称**：Monster/NPC 的 StyleConfig 只有数字 ID，不直观

### 1.3 文件规模

| 文件 | 大小 |
|------|------|
| DebugPanelPlayerTab.cs | **85 KB** |
| DebugPanelMonsterTab.cs | 28 KB |
| DebugPanelNpcTab.cs | 7 KB |
| DebugPanelEntityStyleTabBase.cs | 23 KB |
| EntityStyleConfig.cs | 5 KB |

## 2. 重构目标

**一句话**：所有实体配置统一成一套系统，每个配置有名称+ID，组件运行时可增删。

### 2.1 核心概念

- **EntityProfile**（实体配置档案）：一个命名的配置，包含 ID、名称、和一组组件
- **IEntityTabComponent**（组件）：一个独立的功能区（外观/标签/血条/施法条...）
- **ComponentRegistry**（组件注册表）：所有可用组件的目录，面板通过它知道能添加什么
- **运行时管理**：在调试面板里可以给某个 Profile 增删组件

### 2.2 目标架构

```
DebugPanelTab (基类)
└── DebugPanelEntityTab (统一实体 Tab)
    │
    │  数据层：
    │   EntityProfile (id + name + components[])
    │   ├── id=1, name="玩家"
    │   ├── id=2, name="精英怪"
    │   └── id=3, name="商人"
    │
    │  组件层（IEntityTabComponent，运行时可增删）：
    │   ├── AppearanceComponent    — 大小/比例/边框/圆角/背景/颜色
    │   ├── LabelGroupComponent    — 标签+文字样式（合并原TextStyle，含服务端权威/预览）
    │   ├── BarGroupComponent      — 血条/MP条/施法条
    │   ├── ActionBarComponent     — 动作栏
    │   ├── LevelBadgeComponent    — 等级徽章
    │   ├── MonsterAiComponent     — 怪物 AI/移动
    │   └── NpcInteractComponent   — NPC 交互面板偏移
    │
    │  UI 层：
    │   ├── ProfileSelector        — 选择/新建/删除/重命名 Profile
    │   ├── ComponentManager       — 添加/移除组件（运行时）
    │   └── ComponentPanel[]       — 各组件的 UI 区域（可折叠/可删除）
```

### 2.3 运行时组件管理 UI 示意

```
┌─────────────────────────────────────┐
│  实体配置: [▼ 玩家(1)        ] [+]  │  ← Profile 选择器
│  名称: [玩家_____]                   │  ← 可编辑名称
├─────────────────────────────────────┤
│  组件: [▼ 添加组件...] [添加]        │  ← 组件管理区
│  ┌─ ✅ 外观 ──────── [×] ────┐     │
│  │  大小/比例/边框/圆角...     │     │
│  └────────────────────────────┘     │
│  ┌─ ✅ 标签 ──────── [×] ────┐     │
│  │  行1/行2/行3/行4...        │     │
│  └────────────────────────────┘     │
│  ┌─ ✅ 血条 ──────── [×] ────┐     │
│  │  可见/颜色/比例/偏移...     │     │
│  └────────────────────────────┘     │
└─────────────────────────────────────┘
```

## 3. 数据模型设计

### 3.1 EntityProfile（替代 EntityStyleConfig + Player 散落属性）

```csharp
public class EntityProfile
{
    public int Id;
    public string Name = "";           // 配置名称："玩家""精英怪""商人"
    public string EntityType = "";     // "player" / "monster" / "npc" / 自定义

    // 组件数据：key = 组件名, value = 组件专属数据
    private Dictionary<string, IComponentData> _componentData = new();

    public bool HasComponent(string name) => _componentData.ContainsKey(name);
    public T GetData<T>(string name) where T : IComponentData
        => _componentData.TryGetValue(name, out var d) ? (T)d : default;
    public void SetData(string name, IComponentData data) => _componentData[name] = data;
    public void RemoveComponent(string name) => _componentData.Remove(name);
    public IEnumerable<string> ComponentNames => _componentData.Keys;

    /// <summary>从旧 EntityStyleConfig 迁移</summary>
    public static EntityProfile FromStyleConfig(int id, EntityStyleConfig cfg,
        string name, string entityType) { ... }
}
```

### 3.2 IComponentData（组件数据标记接口）

```csharp
public interface IComponentData { IComponentData Clone(); }

public class AppearanceData : IComponentData { ... }   // VisualSizeScale, BorderWidthScale, etc.
public class LabelGroupData : IComponentData { ... }   // 合并原 TextStyle，见 11.1
public class BarData : IComponentData { ... }          // Visible, LengthScale, HeightScale, etc.
public class CastBarData : IComponentData { ... }
public class ActionBarData : IComponentData { ... }
public class LevelBadgeData : IComponentData { ... }
public class MonsterAiData : IComponentData { ... }    // MoveSpeed, PatrolRange, etc.
public class NpcInteractData : IComponentData { ... }  // InteractMenuOffsetA/B
```

### 3.3 EntityProfileManager（统一管理所有 Profile）

```csharp
public class EntityProfileManager
{
    private Dictionary<int, EntityProfile> _profiles = new();
    private int _nextId = 1;

    public EntityProfile GetProfile(int id);
    public EntityProfile GetOrCreateProfile(int id, string name, string entityType);
    public IEnumerable<EntityProfile> GetProfilesByType(string entityType);
    public EntityProfile CreateProfile(string name, string entityType);
    public void DeleteProfile(int id);
    public void SaveConfig(ConfigFile cfg);
    public void LoadConfig(ConfigFile cfg);
    public void ApplyProfile(EntityBase entity, int profileId);
    public void ApplyProfileToAll(int profileId);
}
```

### 3.4 Profile 与实体的绑定

```
现有：
  Monster.UiConfigId → MonsterManager.StyleConfigs[UiConfigId]
  Npc.UiConfigId     → NpcManager.StyleConfigs[UiConfigId]
  Player             → 无配置，直接操作属性

重构后（统一配置驱动）：
  EntityBase.ProfileId → EntityProfileManager.GetProfile(ProfileId)
  Player 也有 ProfileId，默认绑定 id=1 的 "玩家" Profile
  所有实体统一走 Profile → ApplyProfile → 刷新实体
  未来多人时：每个玩家实例绑定自己的 ProfileId
```

## 4. 组件接口设计

### 4.1 IEntityTabComponent

```csharp
public interface IEntityTabComponent : IDisposable
{
    string ComponentName { get; }       // "appearance", "labels", "healthbar"
    string DisplayName { get; }         // "外观", "标签", "血条"
    Type DataType { get; }              // typeof(AppearanceData)

    void BuildUI(VBoxContainer parent);
    void SyncFromData(IComponentData data);   // 数据 → UI
    IComponentData SyncToData();              // UI → 数据
    void ConnectSignals(Action onChanged);     // 通知 Tab 数据变了
    void DisconnectSignals();
    void SyncFromEntity(EntityBase entity);    // 实体真值 → UI（服务端推送时调用）
    void SetPropertyLocked(string propertyName, bool locked);  // 锁定/解锁属性
    void SetCollapsed(bool collapsed);
    void Dispose();
}
```

### 4.2 ComponentRegistry

```csharp
public static class ComponentRegistry
{
    private static Dictionary<string, Func<IEntityTabComponent>> _factories = new();

    public static void Register(string name, Func<IEntityTabComponent> factory);
    public static IEntityTabComponent Create(string name);
    public static IEnumerable<(string name, string displayName)> GetAllComponents();
    public static IEnumerable<(string, string)> GetComponentsForType(string entityType);

    static ComponentRegistry()
    {
        Register("appearance",    () => new AppearanceComponent());
        Register("labels",        () => new LabelGroupComponent());
        Register("healthbar",     () => new BarGroupComponent("healthbar", "血条"));
        Register("mpbar",         () => new BarGroupComponent("mpbar", "MP条"));
        Register("castbar",       () => new CastBarComponent());
        Register("actionbar",     () => new ActionBarComponent());
        Register("levelbadge",    () => new LevelBadgeComponent());
        Register("monster_ai",    () => new MonsterAiComponent());
        Register("npc_interact",  () => new NpcInteractComponent());
    }
}
```

## 5. 调试面板 UI — DebugPanelEntityTab

### 5.1 整体结构

```csharp
public class DebugPanelEntityTab : DebugPanelTab
{
    private EntityProfileManager _profileMgr;
    private EntityProfile _currentProfile;

    // Profile 选择器
    private OptionButton _profileOption;
    private Button _addProfileBtn, _deleteProfileBtn;
    private LineEdit _profileNameEdit;

    // 组件管理
    private OptionButton _addComponentOption;
    private VBoxContainer _componentContainer;

    // 当前活跃组件
    private Dictionary<string, IEntityTabComponent> _activeComponents = new();

    public override void BuildUI(VBoxContainer tabContainer)
    {
        BuildProfileSelector(tabContainer);
        BuildComponentManager(tabContainer);
        _componentContainer = new VBoxContainer();
        tabContainer.AddChild(_componentContainer);
    }
}
```

### 5.2 Profile 选择器

- 下拉框列出所有 Profile（显示 "名称(ID)"）
- [+] 新建 Profile（弹出对话框输入名称和类型）
- [🗑] 删除 Profile
- 名称输入框：实时编辑当前 Profile 名称

### 5.3 组件管理区

- 下拉框列出 ComponentRegistry 中当前 Profile 还没挂载的组件
- [添加] 按钮：给当前 Profile 添加选中组件（创建默认数据 + 创建 UI）
- 每个已挂载组件有 [×] 删除按钮

### 5.4 组件 UI 区域

每个组件渲染为一个可折叠面板：
```
┌─ ✅ 外观 ─────────────── [×] ┐
│  角色大小: [====●====] 111    │
│  角色比例: [====●====] 1.000  │
│  边框粗细: [==●======] 3      │
│  ...                          │
└───────────────────────────────┘
```

- 点击标题栏折叠/展开
- [×] 移除组件（确认后删除数据和 UI）

### 5.5 数据流

```
用户操作 slider → component.ConnectSignals(onChanged) → onChanged()
    → EntityTab 收到通知
    → component.SyncToData() → 写回 Profile 的 IComponentData
    → EntityProfileManager.ApplyProfileToAll() → 实体更新

Profile 切换 → EntityTab.RefreshComponents()
    → 清除旧组件 UI
    → 遍历 Profile.ComponentNames
    → ComponentRegistry.Create(name) → component.BuildUI()
    → component.SyncFromData(profile.GetData(name))
    → component.ConnectSignals(onChanged)
```