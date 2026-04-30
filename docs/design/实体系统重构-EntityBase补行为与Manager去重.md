# 实体系统重构设计 — EntityBase 补行为 + Manager 去重

> 状态：Phase 1 ✅ 已完成（Scale 重构时一起实现），Phase 2 ✅ 核心已完成（ApplyDefaultStyle → ApplyStyle）
> 创建：2026-04-30
> 前置：无（独立于其他重构）

## 1. 问题

### 1.1 EntityBase 太瘦，Player 太胖

| 类 | 行数 | 大小 |
|---|---|---|
| Player | 1196 | 52KB |
| Monster | 154 | 6.5KB |
| Npc | 64 | 2.5KB |
| EntityBase | 173 | 10KB |

Player 一个类顶 Monster+Npc 的 5 倍。不是因为 Player 功能复杂 5 倍，而是 Player 吞了大量本该在基类的逻辑。

### 1.2 具体问题

#### A. 标签系统分裂

- EntityBase 有 `_labelTexts[4]` + `DrawLabels()`（DrawString 绘制）
- Player 另搞了一套 `_playerLabelTexts[4]` + `RichTextLabel[]` + `_labelContainers[]` + `_labelOffsets[]`
- Player override 了 `GetLabelText/SetLabelText/GetLabelFontSize/SetLabelFontSize`
- Player 用 `new` 覆盖了 `GetLabelCenterX/SetLabelCenterX`
- 基类的 `_labelTexts` 数组在 Player 里成了死数据

#### B. 移动系统重复

- Player 有 `MoveTo/RollbackTo/PlayBounceBack/_currentTween/IsMoving/_bouncingBack/_movePending/_collisionMove/_checkTimer`
- Monster 有 `MoveTo/RollbackTo/PlayBounceBack/_currentTween/IsMoving`
- 逻辑几乎一样，但 Player 多了服务端校验和碰撞检测

#### C. HitTest 重复

- Monster 和 Npc 各写了一遍完全一样的 `HitTest`
- Player 没有 HitTest

#### D. GridPos 不统一

- Player: `public Vector2I GridPos { get; set; }`（可写）
- Monster: `public Vector2I GridPos => new Vector2I(GridX, GridY)`（只读计算属性）
- Npc: 同 Monster
- 基类没有 GridPos

#### E. SetGridSize 重复

- Monster/Npc/Player 各写了一遍

#### F. CastBar/ActionBar 概念混乱

- Player 同时有 CastBar（施法条，血条上方）和 ActionBar（动作栏，角色下方）
- Monster 只有 ActionBar
- CastBar 绘制逻辑直接写在 Player._Draw 里
- ActionBar 绘制逻辑在 EntityDrawUtils.DrawActionBar 里
- 属性命名不统一（`CastBarFillPercent` vs `CastProgress`）

#### G. Manager 层大量复制粘贴

- MonsterManager 和 NpcManager 的 `ApplyDefaultStyle` 几乎一模一样
- `SetHealthBarXxxAll/SetMpBarXxxAll` 各 14 个方法，逻辑全是 `foreach entity set; SyncStyleConfig`
- `SyncStyleConfigHpBar` 两个 Manager 各写一遍
- `LoadDefaultStyleConfig` 结构一样，只是 section 前缀不同
- `StyleConfigs/GetStyleConfig/GetOrCreateStyleConfig` 完全一样的字典操作

#### H. EntityStyleConfig 承载了不属于它的东西

- NPC 交互面板偏移（`InteractMenuOffsetAX/Y/BX/Y`）是 NPC 特有的交互逻辑，不应混在通用样式配置里

## 2. 重构方案

### 2.1 Phase 1: EntityBase 补行为（影响面最广，优先做）

#### 改动文件：EntityBase.cs, Monster.cs, Npc.cs, Player.cs

**EntityBase 新增：**

```csharp
// 格子坐标 — 子类必须实现
public abstract Vector2I GridPos { get; }

// 移动基础
protected Tween _currentTween;
public bool IsMoving { get; set; } = false;

// 施法条（从 Player 下沉）
public Vector2 CastBarOffset { get; set; } = new Vector2(0, -80);
public bool CastBarCenterX { get; set; } = true;
public float CastBarLengthScale { get; set; } = 60.0f / 111.0f;
public float CastBarHeightScale { get; set; } = 4.0f / 111.0f;
public Color CastBarColor { get; set; } = new Color(0.3f, 0.5f, 1, 1);
public Color CastBarBgColor { get; set; } = new Color(0.3f, 0.3f, 0.3f, 0.4f);
public bool CastBarVisible { get; set; } = true;
public float CastBarFillPercent { get; set; } = 0.0f;
public float CastBarLength => Mathf.Clamp(GridSize * CastBarLengthScale, 10.0f, GridSize * 2.0f);
public float CastBarHeight => Mathf.Clamp(GridSize * CastBarHeightScale, 2.0f, GridSize);

// 动作栏（Monster 已在用，Player 也有）
public string CastingSkill { get; set; } = "";
public float CastProgress { get; set; } = 0f;
public float ActionBarTextYOffset { get; set; } = 0f;
public float ActionBarProgressHeight { get; set; } = 4f;

// 通用方法
public virtual void SetGridSize(int size) { ... }
public virtual bool HitTest(Vector2 worldPos) { ... }
public virtual void ApplyStyle(EntityStyleConfig cfg) { ... }
public virtual void MoveTo(Vector2I targetGridPos, float duration = 0.15f) { ... }
public virtual void RollbackTo(Vector2I pos) { ... }
public virtual void PlayBounceBack(Vector2I originPos, float duration = 0.12f) { ... }

// 绘制辅助
protected void DrawCastBar() { ... }  // 从 Player._Draw 提取
protected void DrawActionBar() { ... } // 包装 EntityDrawUtils.DrawActionBar
```

**Monster 瘦身：**

- 删除 `HitTest`（用基类的）
- 删除 `SetGridSize`（用基类的）
- 删除 `MoveTo/RollbackTo/PlayBounceBack`（用基类的，只 override MoveTo 更新 _gridX/_gridY）
- 删除 `CastingSkill/CastProgress/ActionBarTextYOffset/ActionBarProgressHeight` 属性（用基类的）
- 删除 `SetActionBarTextYOffset/SetActionBarProgressHeight`（用基类的）
- `_Draw` 中改用 `DrawCastBar()` + `DrawActionBar()`
- GridX/GridY 改为 private 字段，GridPos 改为 `override`

**Npc 瘦身：**

- 删除 `HitTest`（用基类的）
- 删除 `SetGridSize`（用基类的）
- GridX/GridY 改为 private 字段，GridPos 改为 `override`

**Player 适配：**

- `GridPos` 改为 `override`（从 `public Vector2I GridPos { get; set; }` 改为 `public override Vector2I GridPos { get; set; }`）
- 删除 CastBar 相关属性声明（用基类的）
- 删除 CastingSkill/CastProgress/ActionBarTextYOffset/ActionBarProgressHeight（用基类的）
- 删除 CastBar/ActionBar 的 setter 方法（用基类的）
- `_Draw` 中 CastBar 绘制改用 `DrawCastBar()`
- `_Draw` 中 ActionBar 绘制改用 `DrawActionBar()`
- 保留 Player 特有的移动逻辑（override MoveTo/RollbackTo/PlayBounceBack）
- 保留 Player 特有的 LevelBadge、RichTextLabel 标签系统

### 2.2 Phase 2: Manager 去重

#### 改动文件：MonsterManager.cs, NpcManager.cs

**MonsterManager.ApplyDefaultStyle → entity.ApplyStyle(cfg)：**

```csharp
// Before:
public void ApplyDefaultStyle(Monster monster)
{
    if (monster == null) return;
    var cfg = GetStyleConfig((int)monster.MonsterId);
    monster.SetVisualSizeScale(cfg.VisualSizeScale);
    monster.SetBorderWidthScale(cfg.BorderWidthScale);
    // ... 20+ 行逐个属性复制
}

// After:
public void ApplyDefaultStyle(Monster monster)
{
    if (monster == null) return;
    var cfg = GetStyleConfig((int)monster.MonsterId);
    monster.ApplyStyle(cfg);
}
```

NpcManager 同理。

**SetXxxAll 方法模板化（可选，Phase 2 后期）：**

如果需要进一步去重，可以提取 `EntityManagerBase<TEntity>` 泛型基类。但当前 Manager 的 SetXxxAll 方法数量有限（14 个），且 Monster/Npc 的 SyncStyleConfig 逻辑略有差异，建议先不做泛型化，等有第三个实体类型时再提取。

### 2.3 Phase 3: EntityStyleConfig 拆分（可选）

#### 改动文件：EntityStyleConfig.cs, NpcManager.cs, DebugPanelNpcTab.cs

```csharp
// 通用样式（所有实体共享）
public class EntityStyleConfig { ... }

// NPC 专用样式
public class NpcStyleConfig : EntityStyleConfig
{
    public bool InteractMenuCenterXA = false;
    public bool InteractMenuCenterXB = false;
    public float InteractMenuOffsetAX = 60f;
    public float InteractMenuOffsetAY = -20f;
    public float InteractMenuOffsetBX = -60f;
    public float InteractMenuOffsetBY = -20f;
}
```

NpcManager.StyleConfigs 改为 `Dictionary<int, NpcStyleConfig>`。

**优先级低**——当前 InteractMenu 字段放在 EntityStyleConfig 里不影响功能，只是概念不干净。

### 2.4 Phase 4: 标签系统统一（最复杂，最后做）

#### 问题

Player 用 RichTextLabel（支持富文本、字体自定义、拖拽偏移），Monster/Npc 用 DrawString（简单轻量）。两套系统共存导致：
- 基类的 `_labelTexts` 在 Player 里是死数据
- Player override 了 `GetLabelText/SetLabelText`，走自己的数据源
- Player 用 `new` 隐藏了 `GetLabelCenterX/SetLabelCenterX`

#### 方案

保持两套绘制方式，但统一数据源：

1. 基类的 `_labelTexts` 是唯一数据源
2. Player 的 `_playerLabelTexts` 删除，改用基类的 `_labelTexts`
3. Player override `SetLabelText/GetLabelText` 时操作基类数据
4. Player 的 `_labelCenterX` 删除，改用基类的 `_labelCenterX`
5. Player 的 `DrawLabels()` 不调用（因为用 RichTextLabel），`_Draw` 中直接用 RichTextLabel 渲染

**风险**：Player 的标签拖拽系统依赖 `_labelOffsets`（Vector2[]），而基类用的是 `_labelXOffsets` + `_labelYOffsets`（分开的 float[]）。需要统一偏移数据结构。

**建议**：这个 Phase 改动量大且容易引入回归，建议在 Phase 1-2 完成并稳定后再做。

## 3. 实现顺序与验证

| Phase | 改动文件 | 风险 | 验证方式 |
|---|---|---|---|
| 1 | EntityBase, Monster, Npc, Player | 中 | Godot 编辑器运行，确认 Monster/Npc/Player 渲染正常、移动正常、调试面板正常 |
| 2 | MonsterManager, NpcManager | 低 | 确认样式应用、调试面板滑块、预设保存/加载正常 |
| 3 | EntityStyleConfig, NpcManager, DebugPanelNpcTab | 低 | 确认 NPC 交互面板偏移正常 |
| 4 | EntityBase, Player | 高 | 确认 Player 标签拖拽、富文本、字体切换正常 |

每个 Phase 独立提交，出问题可以单独回滚。

## 4. 注意事项

- **编译验证**：项目需要从 Godot 编辑器构建（`dotnet build` 单独跑有 Godot 源码生成器缓存问题），修改后需在 Godot 编辑器中重新构建验证
- **Player 移动系统**：Player 的 MoveTo 有服务端校验逻辑（_movePending, _collisionMove, _checkTimer 等），不能简单用基类替换。基类的 MoveTo 是通用版本，Player override 保留自己的复杂逻辑
- **Player 标签系统**：Player 的 RichTextLabel 系统比基类的 DrawString 复杂得多（支持 BBCode、字体切换、阴影、拖拽偏移），Phase 4 需要特别小心
- **DebugPanel**：DebugPanel 的 MonsterTab/NpcTab/PlayerTab 通过 Manager 的 SetXxxAll 方法控制实体样式，Phase 2 改 Manager 时需同步检查 DebugPanel 的调用是否兼容
