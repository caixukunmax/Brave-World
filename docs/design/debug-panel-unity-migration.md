# Debug 面板 Unity（团结引擎 Tuanjie）迁移设计（草案骨架）

> 状态：草案骨架，待评审补充。本文档遵循仓库"文档驱动"流程，先对齐设计再动手写代码。

## 1. 背景与目标

将 Godot 客户端的调试面板（`clinetcsharp/Scripts/DebugPanel*` 共 71 个文件、5 个 Tab）迁移到 Unity 客户端（`unityClientSharp/Brave-World`），在 Unity 运行时提供等价的调试能力。

**目标**
- 在 Unity 运行时提供等价的调试能力：地图网格 / 相机校准 / 实体 Profile 编辑 / 系统参数 / UI 调试 / 装饰（建筑工坊）。
- 做成**可独立编译、可一键开关**的模块，调试工具不进发布包。
- 最大化复用已在 Unity 端迁移好的核心数据层，只重写 UI 层。

**非目标**
- 不重新设计业务功能，仅做等价迁移 + 范式转换。
- 不做跨项目通用调试框架（无复用价值，见 §2）。

## 2. "做成插件"的含义澄清与选型

用户提出"做成插件"，实际有三种含义，利弊不同：

| 含义 | 说明 | 结论 |
|------|------|------|
| ① ASMDEF 独立程序集模块 | 用 `asmdef` 把 debug 面板隔离成独立 assembly，依赖核心层 | ✅ **采用**。低成本、高收益，纯工程组织 |
| ② Unity Editor 编辑器插件 | `EditorWindow` / IMGUI，在编辑器进程内运行 | ⚠️ 谨慎。仅"纯配置编辑"部分适合；运行时 overlay 类功能在 Editor 看不到效果 |
| ③ 可复用跨项目 UPM Package | 完全解耦、跨项目复用 | ❌ 过度工程。面板深度耦合本游戏专属系统，无跨项目价值 |

**选型结论（双模式）**：采用 **① + ② 组合**，形成"编辑器 + 运行时"双模式，共享同一逻辑层；不采用 ③。

### 2.1 双模式设计（Editor + Runtime）

需求：**不启动游戏时也能用（编辑配置），启动游戏后能在游戏内进入预览模式，也能直接看运行时可视化。**

前提已满足：Unity 端核心层 `EntityProfileManager` 是 `static class`、`EntityProfile`/`IComponentData` 是纯 POCO（只用 `UnityEngine.Color`，不依赖 MonoBehaviour / 场景对象），因此**同一逻辑层可被 Editor 与运行时两处调用**。

硬约束：**UGUI 只能在运行时用，无法在 Editor 窗口渲染**。因此"一套 UI 两处跑"不可行（除非改用 UI Toolkit，但需放弃现有全套 UGUI 基建，风险大，不采用）。结论是 **UI 前端写两套、逻辑层只写一套（共享）**。

| 模式 | 触发时机 | UI 技术 | 能力范围 |
|------|----------|---------|----------|
| **Editor 模式** | 不启动游戏 | `EditorWindow`（IMGUI / UIElements） | 仅**纯配置编辑**：EntityProfile 增删改、预设管理、导入导出。**无游戏内预览**（已接受此限制） |
| **运行时模式** | 游戏启动后 | UGUI 浮窗（复用 `DraggablePanel`） | **全量**：配置编辑 + 游戏内进入预览模式 + 直接看 overlay/网格/相机校准等运行时可视化 |

**工作量不翻倍的原因**：Editor 模式只做纯配置子集（不复刻 MapTab/相机/巡逻 overlay 等运行时功能——它们在 Editor 无意义）；真正"只写一遍、绝不重复"的是逻辑层（数据模型 + 序列化 + 保存语义），被两个前端共享。真实结构 = **1 个共享逻辑层 + 1 个完整运行时 UGUI 前端 + 1 个精简 Editor 前端**。

## 3. 现状与依赖分析

### Godot 端（待迁移）
- 71 个 `DebugPanel*` 文件，5 个 Tab：`MapTab` / `EntityTab` / `SystemTab` / `UITab` / `DecorationTab`。
- 深度耦合 Godot：`using Godot;`、节点树 `GetNodeOrNull<VBoxContainer>(...)`、`/root/UICanvas/PanelManager` 场景路径、`res://debug_panel_config.cfg` 配置、大量 Godot Signal（`ItemSelected +=`、`ValueChanged +=`、`DragEnded +=`、`Toggled +=`）。
- 依赖核心系统：`EntityProfile` / `ComponentRegistry` / `MapEditor` / `MapDecoration` / `GridManager` / `CameraController` / `Player` / `MonsterManager` / `NpcManager`。

### Unity 端已具备（可复用）
- `Assets/Scripts/Entity/Profile/EntityProfile.cs`、`EntityProfileManager.cs`（静态初始化版）。
- `Assets/Scripts/Entity/MapDecoration.cs`、`MapDecorationManager.cs`。
- `Assets/Scripts/UI/DraggablePanel.cs`（Godot `DraggablePanel` 已在 Unity 复刻，UI 范式已对齐）。
- UI 框架：运行时用 **UGUI**（`Assets/Scripts/UI/` 下大量 `*Hud.cs` / `*Panel.cs`）。
- **运行时依赖盘点（2026-07-18 实测）**：`GridManager`(Map/Rendering)、`MonsterManager`(Entity/Online)、`NpcManager`(Entity/Online)、`MapCameraController`(Map/Camera，注意 Godot 端名为 `CameraController`) → **已存在**。`MapEditor`、`Player` → **Unity 端尚未存在**（DecorationTab/部分预览逻辑将因此阻塞，需各自底层系统先到位）。
- **Newtonsoft.Json 已 vendoring**：`Assets/Plugins/NewtonsoftJson/Newtonsoft.Json.dll`；`TerrainConfigUtil.cs:11` 注释明示「本引擎不含 System.Text.Json」，全项目统一用 Newtonsoft。

### Unity 端缺失（⚠️ 关键前置阻塞，已精确定位）

**A. 持久化层（决定 Stage 0）**
- `Entity/Profile/` 目录仅有 `EntityProfile.cs` + `EntityProfileManager.cs`，**没有** `Serialization` / `Config` / `Legacy` partial，也未实现 `LoadConfig` / `SaveConfig`。
- Godot 端有完整持久化层（`EntityProfileManager.Serialization.cs` 的 `Write/ReadComponentData`、`EntityProfileManager.Config.cs` 的 `Save/Load/WriteProfileConfig`、`EntityProfileManager.Legacy.cs` 的迁移），Unity 端未移植。
- **移植难点**：Godot 持久化层全部基于 Godot `ConfigFile`（`config.Load/Save/SetValue/GetValue/GetSections/EraseSection/GetSectionKeys`）。必须替换为一层 JSON 存储抽象（见 §6）。

**B. Unity `EntityProfile` 是"裁剪版"孤岛（重要发现）**
`unityClientSharp/.../EntityProfile.cs` 注释明确「裁剪为装饰链路所需部分」，相比 Godot 端：
- **缺失 `ComponentRegistry`**：Unity 端整体无 `ComponentRegistry`（EntityTab「管理组件」下拉 UI 依赖它，顺延到 Stage 2）。
- **缺失 5 个组件数据类**：`ActionBarData` / `NameplateData` / `LevelBadgeData` / `MonsterAiData` / `NpcInteractData`（Godot 端 `Serialization.cs` 有对应 case）。代码内已用 TODO 标注 these 顺延到 战斗阶段 / 交互阶段。
- **`CastBarData` 在 Unity 端被折叠进 `BarData`**（`BarData.CreateCastBarDefault()`，`"castbar"` 组件存的是 `BarData`）——故 Godot 的 `case CastBarData` 在 Unity 端应改为 `case "castbar": return new BarData{...}`。
- **缺失"停用组件"功能**：Godot `EntityProfile` 有 `IsComponentDisabled`/`SetComponentDisabled`，且 `SaveConfig` 写 `disabled_components`；Unity 端 `EntityProfile` 无此功能，需补（否则含停用组件的配置无法 round-trip）。

**C. 影响与范围分叉（见 §9 阶段 0、§10 第 1 问）**
由于 B 的存在，Stage 0 不能只是"换存储底座"，必须决定：是仅补「持久化 + 停用组件功能」（与当前裁剪一致，Path B），还是顺带把 ComponentRegistry + 5 个缺失类一次性补齐到与 Godot 全等（Path A）。

### asmdef 现状
- 已存在 `BraveWorld.Runtime.asmdef`。双模式需**新增两个** asmdef（运行时前端 + Editor 前端）。

## 4. 目标架构（双模式，三层 asmdef）

```
BraveWorld.Runtime.asmdef              ← 已存在。共享逻辑层：
                                         EntityProfileManager / EntityProfile / ComponentRegistry
                                         / 序列化 / SaveConfig / LoadConfig
                                         【硬性约束：不依赖 UI、不依赖运行时世界对象，Editor 与运行时共用】
BraveWorld.DebugPanel.asmdef           ← 新增。运行时 UGUI 浮窗前端（全量 5 Tab + 游戏内预览）
                                         开发期编译进；发布期用 #if DEVELOPMENT_BUILD / asmdef 约束剔除
BraveWorld.DebugPanel.Editor.asmdef    ← 新增。Editor 前端（纯配置子集）
                                         includePlatforms: [Editor]，天然不进构建包
```

- **目录**：
  - `Assets/Scripts/DebugPanel/`（运行时 UGUI 前端，按 Tab 拆 partial，沿用 Godot 端组织）
  - `Assets/Scripts/DebugPanel/Editor/`（Editor 前端）
- **分层原则**：
  - 逻辑层（`BraveWorld.Runtime`）：数据模型 + 序列化 + 保存语义，**只写一遍**，两前端共享。
  - 运行时 UI 前端（UGUI）：DebugPanel shell + 5 Tab + 游戏内预览。
  - Editor UI 前端：仅 EntityProfile 编辑 + 预设管理，无预览。
- **关键约束**：逻辑层严禁引用 `GridManager`/`CameraController`/`Player` 等运行时世界对象；这类依赖只能存在于运行时 UGUI 前端。否则 Editor 前端无法编译/复用。

## 5. Godot → Unity 概念映射表

| Godot 概念 | Unity（Tuanjie）对应 | 备注 |
|------------|----------------------|------|
| `Control` / `VBoxContainer` / `ScrollContainer` / `TabContainer` | UGUI `GameObject` + `VerticalLayoutGroup` / `ScrollRect` / 自搭 Tab 切换 | TabContainer 需自实现或用现成 Tab 组件 |
| `HSlider` / `LineEdit` / `OptionButton` / `Button` / `CheckButton` / `Label` | UGUI `Slider` / `InputField` / `Dropdown` / `Button` / `Toggle` / `Text`(TMP) | 字体见 §8 |
| Godot Signal `xxx += handler` | UGUI `onValueChanged.AddListener` / `onEndEdit` / `onClick.AddListener` | C# event 模型一致 |
| `Slider.DragEnded` | UGUI 无原生 drag-end | 自包 `EventTrigger` 的 `PointerUp` 或 `ScrollRect`/`Slider` 的 `OnEndDrag` |
| `GetNodeOrNull<...>("路径")` | 序列化引用 / `GetComponentInChildren` / 依赖注入 | 不用路径查找，改用 Inspector 拖引用 |
| `/root/UICanvas/PanelManager` 场景单例 | Unity 单例 / `FindObjectOfType` / ServiceLocator | `DraggablePanel` 已有注册机制可复用 |
| `res://debug_panel_config.cfg`（ConfigFile） | 见 §6 | 统一到 Unity 机制 |
| `Colors.*` / `Color` | `UnityEngine.Color` | 注意 Godot Color 是 0-1 浮点，Unity 相同，可直接映射 |

## 6. 配置存储方案（已确认：JSON + Newtonsoft.Json）

**已确认**（用户 2026-07-18）：采用 **A. JSON 文件 + Newtonsoft.Json**。本引擎 `.NET BCL` 缺失 `System.Text.Json`（已 vendoring Newtonsoft.Json 13.0.3 于 `Assets/Plugins/NewtonsoftJson/`），统一使用 Newtonsoft。

### 6.1 存储底座抽象（替换 Godot `ConfigFile`）

Godot 持久化层全部基于 `ConfigFile` 的「section / key / value」模型。为最小化改动并保留 Godot 的 key 命名（便于未来可选地从 Godot `cfg` 一次性迁移），定义一层 `IProfileStore` 抽象：

```csharp
interface IProfileStore {
    void SetValue(string section, string key, object value);
    T GetValue<T>(string section, string key, T defaultValue);
    bool HasSection(string section);
    IEnumerable<string> GetSections();
    IEnumerable<string> GetSectionKeys(string section);
    void EraseSection(string section);
}
```

- **实现**：`JsonProfileStore` 内部用 `Newtonsoft.Linq.JObject` 保存 `section → { key → JToken }`；`SaveConfig()` 时 `JsonConvert.SerializeObject(jobj, Formatting.Indented)` 写文件，`LoadConfig()` 时 `JObject.Parse(File.ReadAllText(path))`。
- **文件位置**：`Application.persistentDataPath + "/entity_profiles.json"`（Editor 与运行时同一路径，双模式共享同一份配置）。
- **类型保真**：JSON 原生保存 float/bool/int/string，`Color` 仍按 Godot 既有约定拆 `r/g/b/a` 四个 key（保持 `Write/ReadComponentData` 结构与 Godot 一致，仅把 `ConfigFile` 参数替换为 `IProfileStore`）。
- **定点整数（ToFp/FromFp）可废弃**：Godot 用定点整数是因为 `ConfigFile` 存 double 会丢精度；JSON 精确保存 float，故 `Write/ReadComponentData` 改为直接读写 float，**删除** `ToFp/FromFp/ReadFp` 及 v3→v4 定点迁移分支。新格式 `ConfigVersion` 从基线（如 1）起，旧 Godot `cfg` 不自动兼容（如需兼容另写一次性迁移工具，不在 Stage 0 范围）。

### 6.2 保存语义（沿用 Godot）

- `SaveConfig()`：结构性变更（组件增删/启停、Profile 增删）立即落盘。
- `ScheduleSave()`：属性微调（拖 slider）防抖 1s 落盘（实现在运行时前端，逻辑层只提供 `SaveConfig`/`LoadConfig`）。
- 沿用仓库记忆规则：结构性变更用 `SaveConfig`，微调用 `ScheduleSave`。

## 7. 各 Tab 处理策略

| Tab | 性质 | 落地方式 | 复用点 |
|-----|------|----------|--------|
| `MapTab` | 运行时（网格 overlay / 相机 / 校准 / 巡逻 overlay） | UGUI 运行时 | `GridManager` / `CameraController`（待确认 Unity 端是否存在） |
| `EntityTab` | 配置编辑 + 运行时预览 | UGUI 运行时，核心层直接复用 | `EntityProfile` / `ComponentRegistry` / `MapDecoration`（**已迁移**） |
| `SystemTab` | 运行时 + 配置 | UGUI 运行时 | 待盘点依赖 |
| `UITab` | 运行时（UI 调试） | UGUI 运行时 | `UiEventSystemUtil` 等 |
| `DecorationTab` | 运行时（建筑工坊） | UGUI 运行时 | `MapDecoration` / `ComponentRegistry` / `DecorationConfigUtil`（**仅"进入地图编辑器放建筑"按钮引用 `MapEditor`，可留 TODO 跳过**；`DecorationConfigUtil` 在 Unity 端尚未存在） |

> 纯配置编辑部分（EntityProfile 编辑、预设管理）可在**二期**拆为 Unity Editor 工具（见 §2 ②），一期先全部运行时 UGUI。

## 8. 风险与待确认项

- ⚠️ **范围已定（第二轮决策 2026-07-18）**：原 §9 阶段 0「Path B（推迟注册表）」已升级为**含注册表层**——`ComponentRegistry` + 5 个缺失组件类（ActionBar/Nameplate/LevelBadge/MonsterAi/NpcInteract）+ 其 `Write/Read` case **随迁移一并提供**（数据层与 Godot 全等）。持久化底座仍为 Path B 的 JSON + 停用组件功能。故 EntityTab / DecorationTab 的「管理组件」对话框不再阻塞。
- ⚠️ **依赖缺失阻塞 Tab**：`DecorationTab` 主体（建筑 Profile CRUD、`ApplyProfileToAll`）**不依赖 `MapEditor`**；`DecorationConfigUtil` 仍缺失需移植（见 todo）。`MapEditor` 在 Unity 端不存在，`DecorationTab` 仅"进入地图编辑器放建筑"一键按钮引用它 —— 该按钮留 `// TODO: Unity 端暂无 MapEditor` 跳过，无需为迁 DecorationTab 而先做 MapEditor。`Player` 缺失主要影响部分预览 / `SystemTab` 预览。
- ⚠️ **InventoryUI / FunctionButtonBar 缺失**：Unity 端确认不存在（全工程搜 `Inventory`/`FunctionButton` 无对应类）。`SystemTab` 背包调试子节、`UITab` 功能按钮栏子节按「配置编辑解耦」原则**仍写出配置 UI + 持久化**，加 `// TODO: Unity 端暂无对应消费者（InventoryUI / FunctionButtonBar 未实现）`，不阻断编译。
- `Slider.DragEnded` 在 UGUI 无原生对应，需自包（`Slider.onPointerUp` / `EventTrigger` 的 `PointerUp`，见 §5）。
- 团结引擎 Tuanjie 的 .NET BCL **缺失** `System.Text.Json` 等（见仓库记忆），统一用 Newtonsoft.Json（已 vendoring，§3）。
- 字体：Godot 端用 `SourceHanSansCN` / `Alibaba-PuHuiTi` / `LXGW WenKai`（`res://assets/fonts/*.otf`），Unity 端已有 `NotoSansSC-VF.ttf` 但**无 SDF asset** → 已确认生成中文 TMP SDF 字体资源（见 todo `gen-cn-font`），否则 UI 中文显示方块。
- `Slider.DragEnded` 在 UGUI 无原生对应，需自包（§5）。
- 团结引擎 Tuanjie 的 .NET BCL **缺失** `System.Text.Json` 等（见仓库记忆），统一用 Newtonsoft.Json（已 vendoring，§3）。
- 字体：Godot 端用 `SourceHanSansCN` / `Alibaba-PuHuiTi` / `LXGW WenKai`（`res://assets/fonts/*.otf`），Unity 端需确认这些字体资源是否已导入，否则 UI 中文显示异常。
- 各 Tab 的 `Signals` 文件（Godot signal 接线）需整文件改写为 UGUI `AddListener` / `RemoveListener`。

## 9. 实现阶段拆分

- **阶段 0（前置阻塞，范围已定）**：移植 `EntityProfileManager` 持久化层（`IProfileStore` 抽象 + `Serialization` 的 `Write/ReadComponentData` + `Config` 的 `Save/Load/WriteProfileConfig`），落地 JSON 存储（§6）。**范围 = Path B（JSON + 停用组件功能）+ 注册表层**：持久化底座按 Path B，但 `ComponentRegistry` + 5 个缺失组件类（ActionBar/Nameplate/LevelBadge/MonsterAi/NpcInteract）+ 其 `Write/Read` case **一并补齐**（第二轮决策 2026-07-18：注册表是迁移的一部分，直接做）。即数据层与 Godot 全等，仅存储底座为 JSON。
  - **注**：`ComponentRegistry` 注册的是 `IEntityTabComponent`（UGUI 控件工厂），归 UI 层（`BraveWorld.DebugPanel.asmdef`）；5 个 `IComponentData` 及其序列化 case 归逻辑层（`BraveWorld.Runtime`）。两者配合完成组件数据闭环。
- **阶段 1（骨架）**：新增 `BraveWorld.DebugPanel.asmdef` + `DebugPanel` shell（`DraggablePanel` 载体、Tab 容器、配置读写打通）。
- **阶段 2（逐 Tab 迁移）**：先迁 `MapTab` 打通端到端（UI 构建 → signal 接线 → 配置读写），再 `EntityTab`(需 `ComponentRegistry`) → `SystemTab` → `UITab` → `DecorationTab`(需 `ComponentRegistry` + `DecorationConfigUtil`；`MapEditor` 仅一键跳转按钮，留 TODO)。
- **阶段 3（编译校验）**：用 `check_compile_errors.ps1`（Tuanjie 无头编译）验证，按仓库记忆的坑（8.3 短路径日志、杀残留 Tuanjie 进程等）执行。
- **阶段 4（可选，二期）**：纯配置编辑部分拆 Unity Editor 工具。

## 10. 待用户确认

1. **【Stage 0 范围】✅ 已确认（用户 2026-07-18）**：持久化底座采用 **Path B（JSON + 停用组件功能）**。**🔄 第二轮决策（同日）升级**：`ComponentRegistry` + 5 个缺失组件类（ActionBar/Nameplate/LevelBadge/MonsterAi/NpcInteract）+ 其 `Write/Read` case **不再推迟，随迁移一并提供**（等效 Path A 的注册表层）。即数据层与 Godot 全等，仅存储底座为 JSON。定点整数（ToFp/FromFp）随 JSON 落地废弃，`ConfigVersion` 从 1 起新基线，不兼容旧 Godot cfg。
2. 插件选型已确认 **ASMDEF 模块 + 运行时 UGUI**（不做跨项目 Package）✅。
3. 配置存储介质已确认 **JSON + Newtonsoft.Json** ✅；纯配置 Editor 工具建议二期再做（严格三层 asmdef 隔离留待该阶段）。
4. 运行时依赖盘点已完成 ✅：`GridManager`/`MonsterManager`/`NpcManager`/`MapCameraController` 已存在；`MapEditor`/`Player` 缺失（阻塞 DecorationTab 等）。
5. **【第二轮五决策 2026-07-18】**：① `ComponentRegistry` 直接做（含 5 组件类 + 序列化）；② DebugPanel 偏配置编辑、与其他模块解耦，无消费者也写出配置 UI + 持久化 + TODO；③ 生成中文 TMP SDF 字体（用现有 `NotoSansSC-VF.ttf`）；④ `InventoryUI`/`FunctionButtonBar` Unity 端不存在，按②写出配置 UI + TODO；⑤ asmdef 当前只新增 `BraveWorld.DebugPanel`（运行时），严格三层隔离留待二期 Editor 阶段。
