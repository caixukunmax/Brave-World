# ~~战斗 ATB 进度条设计文档（Combat ATB Panel Design）~~

> 版本：V0.2  
> 状态：**已废弃** — V0.5 战斗系统重构为纯 CD 即时制，ATB 面板已删除

---

## 1. 设计目标

玩家进入战斗后，在屏幕中上方显示一条水平的 ATB（Active Time Battle）进度条，实时展示**玩家自己**与**当前所有交战敌人**的行动顺序。

---

## 2. 面板基础规格

| 属性 | 默认值 |
|------|--------|
| 位置 | 屏幕中上方（距顶 80px，水平居中） |
| 尺寸 | 宽 600px，高 60px（含标记） |
| 进度条高度 | 4px（细直线） |
| 颜色 | 背景透明，进度条为深灰/白色，标记颜色区分阵营 |

### 2.1 结构布局

```
                    ┌────────────────────────────────────────┐
   [玩家] ●─────     │────────────────────────────────────────│     ─────● [野怪#1]
   [野怪#2]  ●──    │────────────────────────────────────────│  ──●
                    └────────────────────────────────────────┘
```

- **水平线**：贯穿面板中央的细线，左端 = ATB 0，右端 = ATB 100
- **标记（Marker）**：每个参战单位一个，由圆点/方块 + 名字标签组成
  - 玩家自己：绿色方块（`#00FF00`），名字在标记上方
  - 敌人：红色圆点（`#FF4444`），名字在标记下方
- **行动反馈**：当单位 ATB 到达 100 时，标记在右端闪烁一次（0.1s 放大到 1.3x），随后进入蓄力状态（ATB 保持 100），蓄力完成后才回到左端（0）
- **蓄力进度条**：每个标记下方显示蓄力信息：
  - 灰色背景条（60×4px）+ 金色填充条（`#FFD700`）
  - 蓄力技能名（10px 字号）
  - 仅在单位处于 CASTING 子状态时显示

---

## 3. 数据同步策略

### 3.1 核心原则

- ATB 计算在服务端 `CombatManager` 中是权威来源
- 客户端**本地插值**平滑移动标记，服务端**定期广播**修正位置
- 广播频率：每 100ms（与 combat tick 同频），但只发送给**参战玩家**

### 3.2 新增 Protobuf 协议

```protobuf
// game.proto

message CombatStateNotify {
  message CombatUnit {
    uint64 entity_id = 1;      // account_id 或 instance_id
    string entity_name = 2;    // 显示名称
    float atb = 3;             // 0.0 ~ 100.0
    bool is_player = 4;        // true=玩家, false=怪物
    int32 hp = 5;              // 当前血量
    int32 max_hp = 6;          // 最大血量
    string casting_skill = 7;  // 蓄力中的技能名（空=未蓄力）
    float cast_progress = 8;   // 蓄力进度 0.0 ~ 1.0
  }
  repeated CombatUnit units = 1;
}
```

### 3.3 消息 ID

- `GAME_COMBAT_STATE_NOTIFY = 381`

### 3.4 服务端广播逻辑

在 `CombatManager:tickATB(dt, maps)` 结束后，调用 `broadcastCombatState(maps)`：

1. 遍历 `maps` 中每个地图的所有**玩家**
2. 对该玩家，收集其所有 `relationIds` 关联的活跃关系中的单位：
   - 玩家自己
   - 每个 relation 中的 attacker / target（去重）
3. 如果收集到的单位数量 > 0（即玩家处于战斗中），打包 `CombatStateNotify` 并通过 Gateway 发送给该玩家
4. 如果玩家已脱战（无活跃 relation），发送空的 `units` 列表（客户端收到后隐藏面板）

> 注：当前每个地图参战单位数量极少（玩家 + 几只怪物），每 100ms 的广播数据量可控。

---

## 4. 客户端实现方案

### 4.1 新增文件

```
clinetcsharp/
├── Scripts/
│   └── CombatATBPanel.cs          # ATB 进度条逻辑
├── scenes/
│   └── combat_atb_panel.tscn      # 场景（CanvasLayer + ColorRect 进度条 + Marker 模板）
└── ...
```

### 4.2 主场景挂载

在 `main.tscn` 中新增 `CombatATBPanel` 场景，放在 `CanvasLayer` 下（层 = 50）。

### 4.3 NetworkManager 扩展

- 新增信号：`CombatStateReceivedEventHandler(Godot.Collections.Array units)`
- 注册 `GAME_COMBAT_STATE_NOTIFY` 解析与触发

### 4.4 CombatATBPanel.cs 关键行为

```csharp
public partial class CombatATBPanel : Control
{
    // 收到服务端 ATB 状态后更新目标位置
    public void UpdateState(Godot.Collections.Array units);
    
    // 每帧插值移动标记到目标 ATB 位置
    public override void _Process(double delta);
    
    // 创建/复用/销毁标记节点
    private void SyncMarkers(int count);
}
```

- **插值速度**：标记每秒移动 ATB 值的 15 倍（可调整），保证在 100ms 内基本跟上服务端
- **空列表处理**：`units.Count == 0` 时隐藏面板，非空时显示
- **行动闪烁**：当某个标记的目标 ATB 从 >=95 瞬间跳到 <10 时，判定为”刚刚行动”，触发缩放动画
- **蓄力显示**：从 `casting_skill` 和 `cast_progress` 字段读取蓄力状态：
  - `casting_skill` 非空 → 显示蓄力进度条
  - 金色填充宽度 = 60 × cast_progress
  - 进度条下方显示技能名

---

