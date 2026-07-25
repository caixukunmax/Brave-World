# Godot 编辑器插件版调试面板（对齐 Unity EditorWindow）

> 姊妹篇：`docs/design/debug-panel-unity-migration.md`
> 那份文档定义了「Runtime + Editor 双模式、共享逻辑层」的范式，并把它落地到 **Unity 侧**（Godot Runtime 面板 → Unity EditorWindow）。
> 本文是反向补全：**Godot 侧的 Runtime 面板（F11）已经存在，缺的是 Editor 插件这一半**。补上后即构成 Godot 侧的「Runtime + Editor 双模式」，与那份文档的范式精神完全对齐。

## 1. 背景与目标

- **现状**
  - Unity：`Assets/Editor/DebugPanel/DebugPanelWindow.cs`（`EditorWindow`，菜单 `Window/BraveWorld/调试面板`）——编辑期即可改 `EntityProfile` / 组件 / 预览，**不开游戏**。
  - Godot：已有运行时调试面板（`DebugPanel.*.cs`，游戏内按 `F11`），含「地图 / 实体 / 系统 / UI / 建筑」五个页签，也能改 `EntityProfile`。**必须跑游戏才能用**。
- **目标**
  - 在 Godot 编辑器内提供一个 **EditorPlugin**，提供与 Unity `EditorWindow` 对等的「实体配置」编辑体验：**不开游戏**就能管理 `EntityProfile`、增删/编辑组件、预览。
  - 体验对齐 Unity：面板作为一个编辑器 Dock（类比 Unity 右侧 EditorWindow），数据就地落盘。
- **范围说明**
  - 本期核心是「实体配置」（Profile 列表 + 组件编辑 + 保存），与 Unity `DebugPanelWindow` 的「实体配置」Tab 对齐。
  - 不直接搬运 Unity 的「面板设置」等其他 Tab（Godot 无对应 Runtime 概念）；如后续需要再扩展。

## 2. 现状盘点（Godot 侧可复用资产）

| 资产 | 位置 | 结论 |
|------|------|------|
| `EntityProfile` 及组件数据类（`LabelGroupData`/`AppearanceData`/`HealthBarData`…） | `clinetcsharp/Scripts/EntityProfile.cs` 等 | 纯 C# 数据类，仅依赖 `Godot.Color`。EditorPlugin 运行在 Godot 编辑器进程内，可直接使用。 |
| `IComponentData` | `clinetcsharp/Scripts/IComponentData.cs` | 纯数据接口（`Clone`/`Equals`），直接复用。 |
| `ComponentRegistry` + `IEntityTabComponent` | `clinetcsharp/Scripts/ComponentRegistry.cs`、`IEntityTabComponent.cs` | `BuildUI(VBoxContainer)` / `SyncFromData(IComponentData)` / `SyncToData()` / `ConnectSignals(Action)` **只操作 Control + 数据，不依赖运行时 `EntityBase` 实例**（`SyncFromEntity` 仅为可选的服务端推送场景）。EditorPlugin 可直接 `ComponentRegistry.Create(name)` 复用每个组件的现成编辑器。 |
| 序列化 I/O | `clinetcsharp/Scripts/EntityProfileManager.Serialization.cs` | `WriteProfileConfig` / `WriteComponentData` / `ReadComponentData` / `LoadNewFormat` / `MigrateConfig` **全部基于 `ConfigFile`，不依赖 `GetTree()` / 运行场景**。编辑器期可对 `res://debug_panel_config.cfg` 直接读写。 |
| 预览实现 | `clinetcsharp/Scripts/EntityProfilePreviewPanel.cs` | 基于 `SubViewport` 的 1:1 预览。Godot 编辑器内 `SubViewport` 同样可用（分阶段实现）。 |

**唯一障碍**：`EntityProfileManager` 是运行时 Autoload 单例（`Instance`），其 `_Ready()` 含运行时副作用（`DecorationConfigUtil.RefreshFromProfileManager()`、`NormalizeBuiltInProfiles()` 等）。不开游戏时 `Instance == null`，EditorPlugin 不能直接 `Instance` 它，也不能 `new` 后误触发这些副作用。

## 3. 目标架构

```
┌─────────────────────────────────────────────────────────┐
│  Godot 编辑器 (Editor)                                    │
│                                                           │
│  ┌─────────────── EditorPlugin (新增) ─────────────────┐  │
│  │  DebugPanelEditorPlugin                             │  │
│  │   └─ AddControlToDock(RightUl, EditorProfilePanel)  │  │
│  │        ├─ EditorProfileList    (Profile CRUD)       │  │
│  │        └─ EditorProfileEdit    (复用 IEntityTabComp) │  │
│  │             └─ 各组件 IEntityTabComponent.BuildUI   │  │
│  │  数据: ProfileConfigIO 读写 res://debug_panel_config.cfg│
│  └──────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────┐
│  运行时 (游戏内 F11)                                      │
│  EntityProfileManager(Autoload) ── 同一份 res://配置文件 ──┘
│  Runtime DebugPanel → IEntityTabComponent（已复用同一框架）
└─────────────────────────────────────────────────────────┘

共享逻辑层: EntityProfile / IComponentData / ComponentRegistry / ProfileConfigIO
```

- EditorPlugin 通过 `AddControlToDock(DockSlot.RightUl, control)` 嵌入编辑器，最接近 Unity `EditorWindow` 的右侧停靠体验。
- 数据读写复用 **同一份** `res://debug_panel_config.cfg`，与 Runtime 面板共享真相，互不冲突（谁保存谁落盘）。

## 4. 核心工程决策：解耦数据 I/O（消除"拿不到 Instance"的障碍）

**问题**：序列化逻辑当前定义在 `EntityProfileManager`（Node）的 partial 方法里，依赖 `_profiles` 实例字段；EditorPlugin 没有 `Instance`，无法调用。

**方案 B（推荐，结构级解耦）**：把纯序列化逻辑抽成一个**不依赖 Node 的静态工具类** `ProfileConfigIO`。

```csharp
// clinetcsharp/Scripts/ProfileConfigIO.cs  (新增, 不依赖 Node/GetTree)
public static class ProfileConfigIO
{
    public const string ConfigPath = "res://debug_panel_config.cfg"; // 与现有 const 对齐

    public static void Write(ConfigFile cfg, Dictionary<int, EntityProfile> profiles);
    public static void Read(ConfigFile cfg, out Dictionary<int, EntityProfile> profiles);

    // 内部复用现有纯逻辑: WriteProfileConfig / WriteComponentData /
    // ReadComponentData / LoadNewFormat / MigrateConfig
}
```

- `EntityProfileManager` 改为调用 `ProfileConfigIO`（薄封装），Runtime 面板零行为变化。
- EditorPlugin 直接 `new ConfigFile()` + `ProfileConfigIO.Read/Write`，**完全不碰 `EntityProfileManager` 的 Node 本质**，规避所有运行时副作用。
- 收益：职责清晰、可单测、符合「逻辑层 / UI 层分离」，也为未来导出工具等复用铺路。

**方案 A（备选，最小侵入）**：EditorPlugin 内 `new EntityProfileManager()` 当纯数据容器用，仅调 `LoadConfig`/`WriteProfileConfig`（这些方法不依赖 `GetTree`，且 `new` 的 Node 不会触发 `_Ready` 副作用）。
- 优点：改动极小。
- 缺点：「用 Node 当数据容器」属反模式；若日后有人在 `LoadConfig` 里加入树相关逻辑会悄然破坏插件。

**本设计主推方案 B**。理由见上，且符合 AGENTS.md「结构级修复 / 解耦」原则。

## 5. EditorPlugin 文件布局

```
clinetcsharp/
  addons/
    debug_panel_editor/
      plugin.cfg                      # 仿 addons/openclaw/plugin.cfg，script 指向 C# 插件类
      DebugPanelEditorPlugin.cs       # EditorPlugin 入口: _EnterTree 注册 Dock, _ExitTree 清理
      EditorProfilePanel.cs           # 主面板容器 (类比 Unity DebugPanelWindow 实体配置 Tab)
      EditorProfileList.cs            # Profile 列表: 新建/删除/复制/选择 (类比 EntityProfileTab)
      EditorProfileComponentList.cs   # 组件增删 + 编辑 (复用 IEntityTabComponent)
      EditorProfilePreview.cs         # (可选, 阶段4) SubViewport 1:1 预览
  Scripts/
    ProfileConfigIO.cs                # (新增) 从 EntityProfileManager 抽出的静态 I/O
    EntityProfileManager.Serialization.cs  # (改造) 改调 ProfileConfigIO
    # 其余 Script 直接复用, 不改
```

**注册方式**（参考 `addons/openclaw/plugin.cfg`）：
- `plugin.cfg`：`[plugin] name="DebugPanelEditor" script="DebugPanelEditorPlugin.cs" ...`
- C# 插件类 `DebugPanelEditorPlugin : EditorPlugin`，`Godot.NET.Sdk` 默认自动编译 `addons/` 下的 `.cs`（已确认 `clinetcsharp.csproj` 用 `Godot.NET.Sdk/4.6.2` 且无排除 `addons/**`，实现时验证）。
- 在 Godot 编辑器 `Project Settings → Plugins` 启用（或脚本化启用）。

## 6. 能力落地（对齐 Unity 实体配置 Tab）

| 能力 | 实现 | 对应 Unity |
|------|------|-----------|
| Profile 列表 / 分组 / 选择 | `EditorProfileList` 读取 `ProfileConfigIO.Read` 得到的集合，OptionButton/ItemList 展示分类 | `EntityProfileTab` 列表 |
| 新建 / 删除 / 复制 / 重命名 Profile | 操作 `Dictionary<int,EntityProfile>`，保存时 `ProfileConfigIO.Write` | Profile CRUD |
| 组件增删 | `ComponentRegistry` + 默认组件列表（对齐 memory 18716452、85190332） | `AddComponentUI` |
| 组件编辑 UI | 对每个组件 `ComponentRegistry.Create(name)` → `BuildUI(parent)` + `SyncFromData/SyncToData`，**完全复用现有组件编辑器** | `ComponentInspectors` |
| 保存语义 | 结构性变更（增删组件/Profile）**立即** Save；属性微调防抖保存（对齐 memory 28653953） | 同 |
| 预览 | 阶段4 可选：SubViewport 渲染（Godot 编辑器可渲染，比 Unity 更自然） | Editor 预览 |

## 7. 与 Runtime 面板的协同约定

- **单一写入者**：编辑器插件保存后，`res://debug_panel_config.cfg` 更新；Runtime 面板下次启动/重新 `LoadConfig` 自动加载最新。避免两边同时写造成覆盖。
- **共享同一框架**：组件 UI 只有 `IEntityTabComponent` 这一份真相，Runtime 与 Editor 都复用，未来组件改动只需改一处。
- **不重复 Autoload**：EditorPlugin 不依赖 `EntityProfileManager.Instance`，避免运行时耦合。

## 8. 实现阶段拆分

- **阶段 0（解耦）**：新增 `ProfileConfigIO`，把 `EntityProfileManager.Serialization.cs` 的纯逻辑迁入；`EntityProfileManager` 改为调用它。**跑现有 Runtime 面板回归**（F11 打开、改配置、保存、重开验证）确认零行为变化。
- **阶段 1（插件骨架）**：`plugin.cfg` + `DebugPanelEditorPlugin` + `EditorProfilePanel`，能加载/保存 `res://debug_panel_config.cfg`（先用日志验证读写正确）。
- **阶段 2（Profile 列表）**：`EditorProfileList` 实现 Profile CRUD，保存语义落地。
- **阶段 3（组件编辑）**：`EditorProfileComponentList` 复用 `IEntityTabComponent` + `ComponentRegistry`，含组件增删、防抖保存。
- **阶段 4（可选预览）**：`EditorProfilePreview` 用 SubViewport 做 1:1 预览。

## 9. 风险与待确认

- **R1**：C# `EditorPlugin` 的 `plugin.cfg` 的 `script` 字段语法（C# 插件与 GDScript 插件写法略有差异）。实现时参考官方 C# 插件模板 + `openclaw` 的 `plugin.cfg` 验证。
- **R2**：`Godot.NET.Sdk` 是否自动编译 `addons/` 下的 `.cs`。默认会（未显式排除），实现时构建验证。
- **R3**：预览在编辑器下能否实例化 `MapDecoration`/纹理等场景资源。阶段 4 验证，失败则本期不做预览（对齐 unity-migration 文档中「Editor 模式无预览」的已有决策）。
- **R4**：EditorPlugin 严禁触发 `EntityProfileManager` 运行时副作用 —— 通过方案 B 的 `ProfileConfigIO` 解耦天然规避。
- **R5**：编辑器插件与正在运行的游戏同时修改配置可能冲突 —— 约定章节 7 的单一写入者原则。

## 10. 已确认决策（用户已确认）

1. 解耦方式：**方案 B**——抽 `ProfileConfigIO` 静态工具类，Runtime 与 Editor 共用序列化逻辑。
2. 预览：**本期不做**，留作阶段 4 可选（编辑器内 SubViewport 预览）。
3. 停靠位置：**右侧 `RightUl`**（对齐 Unity EditorWindow 右侧体验）。
4. 范围：**只做「实体配置」Tab**（Profile CRUD + 组件编辑 + 保存）。

## 11. 实现状态

- 已完成阶段 0–3 全部代码，并通过 `dotnet build`（EXIT:0）编译校验（Godot 4.6.1 / Godot.NET.Sdk 4.6.2）。
- 新增文件：
  - `clinetcsharp/Scripts/ProfileConfigIO.cs` — 纯数据 I/O（定点序列化 / 组件读写 / 新格式加载 / 迁移 / 默认补齐）。
  - `clinetcsharp/addons/debug_panel_editor/plugin.cfg` — 插件注册。
  - `clinetcsharp/addons/debug_panel_editor/DebugPanelEditorPlugin.cs` — EditorPlugin 入口（`AddDock` / `RemoveDock`，停靠 `RightUl`）。
  - `clinetcsharp/addons/debug_panel_editor/EditorProfilePanel.cs` — 主面板（加载/保存、Profile 列表 CRUD、防抖保存）。
  - `clinetcsharp/addons/debug_panel_editor/EditorProfileComponentList.cs` — 组件编辑区（复用 `ComponentRegistry` + `CollapsibleContainer` + `IEntityTabComponent`）。
- 改造文件：`EntityProfileManager.{Config,Serialization,Legacy}.cs` 与 `EntityProfileManager.cs` —— 序列化逻辑委托 `ProfileConfigIO`，保留 `EntityProfileManager.FromFp/ToFp/ToFpD/ReadFp` 转发以维持现有调用者（MonsterManager、各 RenderComponent 等）。
- **第二轮迭代（用户反馈 + 自检补全）**：
  - **修复「组件栏鼠标无法拖动」**：组件持有容器原本是裸 `VBoxContainer`，组件一多就溢出且无法滚动。已在 `EditorProfileComponentList` 中用 `ScrollContainer`（禁用横向滚动、竖向 ExpandFill）包裹，恢复滚轮/拖拽滚动。
  - **补齐 CRUD**：工具栏新增「复制」（深拷贝 `EntityProfile.Clone`）、删除改为 `ConfirmationDialog` 二次确认，防误删丢配置。
  - **根面板填满 Dock**：`EditorProfilePanel` 设置 `SizeFlagsHorizontal/Vertical = ExpandFill`。
  - **修复改名导致组件编辑丢失的隐患**：`OnNameChanged` 原本触发整页 `RefreshProfileList → Bind → Rebuild`，而 `Bind` 重建前未把当前组件改动 flush 到内存（组件字段是防抖保存才写 `_profile`），整页重建会从旧数据重绘、吞掉未落盘的组件编辑。已在 `Bind` 开头先 `Flush()`，并让 `OnNameChanged` 只更新列表项文本（避免闪烁/焦点丢失）。
- **第三轮迭代（预览功能，原阶段 4）**：
  - **目标**：编辑器内实时预览，要求与游戏中渲染 1:1 一致，且复用同一套代码。
  - **做法（最大化复用）**：把 `EntityProfileManager.ApplyProfile(entity, profileId)` 的核心抽成 **`public static ApplyProfileToEntity(EntityBase, EntityProfile)`**（不依赖运行时 `Instance`）；游戏侧 `ApplyProfile` 改为查对象后委托它（零行为变化）。运行时预览面板 `EntityProfilePreviewPanel` 用的就是 `PreviewMap`（`SubViewportContainer > SubViewport > PreviewMap`）+ `MapDecoration.SetEntity`——编辑器预览**完全镜像这套**：在 `EditorProfilePanel` 右侧顶部放 `SubViewportContainer/SubViewport/PreviewMap`，用同一个 `MapDecoration` 预览实体，调用 `MapDecoration.SetupFromProfile(profile)`（内部走 `ApplyProfileToEntity` + `EnsureRenderComponents` + `QueueRedraw`），并经 `PreviewMap.SetEntity` 居中。渲染链路（模板/尺寸/边框/铭牌/血条/标签）与游戏**同一套 `IRenderComponent` 绘制**，故视觉完全一致。
  - **实时刷新**：组件列表新增 `Changed` 事件（字段编辑/增删/启用均触发）；`EditorProfilePanel.RefreshPreview` 先 `_componentEditor.Flush()` 把控件当前值写回内存 `profile`，再 `SetupFromProfile`，使编辑即时反映到预览；选中不同 Profile 时也延迟一帧刷新（等预览地图子树就绪）。
  - **改的文件**：`EntityProfileManager.cs`（`ApplyProfileToEntity` 静态化、`ResetEntityToDefaults`/`SyncRenderComponents`/`SyncRenderComponent<T>` 全部静态化）、`MapDecoration.cs`（新增 `SetupFromProfile`）、`EditorProfilePanel.cs`（预览区 + `RefreshPreview`）、`EditorProfileComponentList.cs`（`Changed` 事件）。
  - **范围说明**：预览实体统一用 `MapDecoration`（游戏里建筑/装饰的渲染实体，也是运行时预览用的实体），因此对 decoration/building 类 Profile 预览与游戏**像素级一致**；对 player/monster/npc 类 Profile 会复用其共有的 appearance/标签/血条/铭牌组件渲染（这些组件本就共享），但不实例化各自专属实体壳（编辑器无游戏世界，实例化 `Monster`/`Player` 等会触碰运行时单例导致 NPE）。如需对这三类也做到「专属实体壳」级别一致，需在编辑器内构造对应实体（更高风险），可后续评估。
- **第四轮迭代（预览宿主环境对齐，根治「编辑器预览与游戏渲染不一致」）**：
  - **根因**：第三轮后预览实体构造已与游戏同源（`EntityPreviewFactory`），残留差异全部来自宿主渲染环境而非实体本身——①**像素密度**：编辑器显示缩放（hiDPI 常见 1.25~2.0）会把按容器逻辑尺寸渲染的 SubViewport 纹理双线性放大到物理屏，几何/血条/文字整体发虚（游戏根窗口无此二次放大）；②**主题**：SubViewport 内 Control（如 RichTextLabel 铭牌）的主题解析会穿过 SubViewport 落到编辑器主题（编辑器字体/字号/行高），且与实体排版量算用的 `ThemeDB.FallbackFont` 错配，表现为文字大小/间距与游戏不同。
  - **修复（通用）**：新增 `addons/debug_panel_editor/EditorPreviewEnvironment.cs` 静态工具：`GetRenderScaleFactor()` 按 `EditorInterface.GetEditorScale()` 向上取整得倍率 N；`ApplyTo()` 设 `Stretch=true + StretchShrink=N`（SubViewport 以容器尺寸×N 渲染、按 1/N 显示，texel 密度 ≥ 物理像素）并把游戏主题（`ThemeDB.GetProjectTheme() ?? ThemeDB.GetDefaultTheme()`）赋给预览容器；`SyncViewportSize()` 在容器 `Resized` 时按倍率同步视口尺寸。`EditorProfilePanel` 接入；实体/渲染组件/PreviewMap 零改动。后续编辑器内其他游戏画面预览（地图/演出）复用同一工具。
  - **改的文件**：新增 `EditorPreviewEnvironment.cs`；改 `EditorProfilePanel.cs`（接入环境对齐 + Resized 按倍率同步）。
  - **AGENTS.md**：新增对应「本项目特殊」约束条目（编辑器 SubViewport 预览必须对齐宿主渲染环境）。
- **待用户验证（无法在本环境运行 Godot）**：
  - 启用插件后确认：右侧 Dock 显示、Profile 编辑/复制/删除确认、组件增删/滚动、**预览区实时渲染且与游戏地图中实体一致**、保存落盘（`res://debug_panel_config.cfg`）均正常。
  - 已知风险：`PreviewMap` 在 `_Ready` 会创建 `GridManager`（兜底 `new GridManager{SkipAutoLoad=true}`）。若 `GridManager` 在编辑器进程里访问到不存在的游戏单例而报错，预览地图背景可能异常——届时退化为「纯 `SubViewport` 直接渲染 `MapDecoration`（无网格背景）」即可，渲染实体本身仍是同一套。运行时面板（F11）回归也需游戏内验证。
