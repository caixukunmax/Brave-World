# 综合面板设计文档（Integrated Panel Design）

> 版本：V0.1  
> 状态：已实现

---

## 1. 设计目标

为 Godot 客户端增加一个**可拖动、位置可持久化**的左下角综合面板，用于聚合显示游戏内的各类频道信息。当前第一版仅实现**战斗频道**。

---

## 2. 面板基础规格

| 属性 | 默认值 |
|------|--------|
| 默认位置 | 左下角（距左 20px，距底 20px） |
| 默认尺寸 | 宽 400px，高 220px |
| 最小尺寸 | 宽 300px，高 150px |
| 最大尺寸 | 宽 600px，高 400px |
| 最小化后高度 | 仅标题栏 32px |
| 边框限制 | 不能完全拖出屏幕可视区域 |

### 2.1 结构布局

```
┌────────────────────────────────────────┐ ← 标题栏（可拖动区域，高 32px）
│  [−]  [战斗]  [频道2] ...        [□] [×] │
├────────────────────────────────────────┤
│                                        │
│  战斗频道内容区域（RichTextLabel）      │
│  ────────────────────────────────────  │
│  [10:23] 你与野怪#1进入了战斗！        │
│  [10:23] 野怪#1 使用了 普通攻击        │
│  [10:24] 你 使用了 普通攻击            │
│  [10:24] 你 对 野怪#1 造成了 8 点伤害  │
│                                        │
└────────────────────────────────────────┘
```

- **标题栏左侧**：[−] 最小化按钮 + 频道标签页（当前仅"战斗"）
- **标题栏右侧**：[□] 还原/最大化切换按钮（可选，第一版可只做最小化） + [×] 关闭按钮（隐藏面板）
- **内容区**：只读日志流，自动滚动到底部，支持最多保留 **200 条**历史记录，超出时移除最旧记录。

### 2.2 视觉风格（与现有 DebugPanel 保持一致）

- 背景：半透明黑色面板 `Color(0, 0, 0, 0.85)`
- 边框：1px 深灰色 `#333333`
- 字体：默认思源黑体（参考 `DebugPanel.FONT_LIST`）
- 字号：正文 14px，标题栏 14px 加粗
- 战斗频道不同事件的颜色区分：
  - 战斗开始：`#FFD700`（金色）
  - 自身行为：`#FFFFFF`（白色）
  - 敌方行为：`#FF6B6B`（浅红）
  - 伤害数值：`#FFA500`（橙色）
  - 系统/脱战：`#AAAAAA`（灰色）

---

## 3. 拖动与位置持久化

### 3.1 拖动机制

- 鼠标按下标题栏 → 开始拖动
- 鼠标移动时 → 面板跟随鼠标偏移量移动
- 鼠标松开 → 停止拖动，触发**防抖保存**（延迟 1 秒后向服务器发送保存请求）
- 限制：面板至少有 100px 宽/高区域必须留在屏幕内，不能完全拖出屏幕

### 3.2 位置持久化

面板位置保存在**服务端角色数据**中，而不是本地 `user://` 配置。

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `ui_panel_pos_x` | float | 20 | 面板左上角 X 坐标 |
| `ui_panel_pos_y` | float | -1 | 面板左上角 Y 坐标，-1 表示使用默认左下角 |
| `ui_panel_width` | float | 400 | 面板宽度 |
| `ui_panel_height` | float | 220 | 面板高度 |

- 进入游戏时，客户端从 `EnterGameResponse.role_info`（或额外请求）读取上述字段并恢复面板位置。
- 若 `ui_panel_pos_y == -1`，则按屏幕高度计算左下角默认位置。

### 3.3 服务端存储

- 在 `game/db` 服务的角色数据中增加上述 4 个字段。
- 提供接口 `updateUIPanelPos(accountId, x, y, w, h)` 供客户端保存。
- 提供接口 `getUIPanelPos(accountId)` 在 `enterGame` 时读取。

---

## 4. 战斗频道内容设计

### 4.1 数据来源

战斗频道的所有内容均来自**服务端推送**的 `CombatLogNotify` 协议消息。客户端不本地生成战斗日志。

### 4.2 新增 Protobuf 协议

```protobuf
// game.proto

// 战斗日志条目类型
enum CombatLogType {
  COMBAT_LOG_START = 0;      // 战斗开始
  COMBAT_LOG_SKILL = 1;      // 释放技能
  COMBAT_LOG_DAMAGE = 2;     // 造成伤害
  COMBAT_LOG_HEAL = 3;       // 治疗/回血
  COMBAT_LOG_BUFF = 4;       // 获得 Buff/Debuff
  COMBAT_LOG_DODGE = 5;      // 闪避/打空
  COMBAT_LOG_DEATH = 6;      // 死亡
  COMBAT_LOG_END = 7;        // 战斗结束/脱战
}

// 单条战斗日志
message CombatLogEntry {
  uint32 log_type = 1;           // CombatLogType
  uint64 timestamp = 2;          // 服务器时间戳（秒）
  string actor_name = 3;         // 行为发起者名称
  string target_name = 4;        // 目标名称（可选）
  string skill_name = 5;         // 技能名称（可选）
  int32 value = 6;               // 数值（伤害/治疗量，可选）
  string extra = 7;              // 额外文本（可选）
}

// 战斗日志批量推送
message CombatLogNotify {
  repeated CombatLogEntry entries = 1;
}
```

### 4.3 服务端发送时机（map_pool/combat）

| 事件 | 生成的日志条目示例 |
|------|-------------------|
| 碰撞进入战斗 | `{START, "你与", "野怪#1", "", 0, "进入了战斗！"}` |
| 先手攻击 | `{SKILL, "你", "", "普通攻击", 0, ""}` |
| 普攻命中 | `{DAMAGE, "你", "野怪#1", "", 8, ""}` |
| 怪物普攻 | `{SKILL, "野怪#1", "", "普通攻击", 0, ""}` |
| 怪物命中玩家 | `{DAMAGE, "野怪#1", "你", "", 3, ""}` |
| 打空 | `{DODGE, "你", "野怪#1", "", 0, "目标已脱离范围"}` |
| 脱战 | `{END, "你", "", "", 0, "已脱离战斗"}` |
| 死亡 | `{DEATH, "野怪#1", "", "", 0, "被击败了"}` |

### 4.4 客户端格式化规则

客户端根据 `log_type` 将 `CombatLogEntry` 渲染为带颜色标签的 BBCode：

| 类型 | 渲染格式 |
|------|----------|
| `START` | `[color=#FFD700]{timestamp} [{actor_name}] 与 [{target_name}] 进入了战斗！[/color]` |
| `SKILL` | `[color=#FFFFFF]{timestamp} [{actor_name}] 使用了 {skill_name}[/color]` |
| `DAMAGE` | `[color=#FFA500]{timestamp} [{actor_name}] 对 [{target_name}] 造成了 {value} 点伤害[/color]` |
| `HEAL` | `[color=#00FF00]{timestamp} [{actor_name}] 恢复了 {value} 点生命[/color]` |
| `DODGE` | `[color=#AAAAAA]{timestamp} [{actor_name}] 的攻击打空了 ({extra})[/color]` |
| `END` | `[color=#AAAAAA]{timestamp} [{actor_name}] {extra}[/color]` |
| `DEATH` | `[color=#FF0000]{timestamp} [{actor_name}] {extra}[/color]` |

---

## 5. 客户端实现方案

### 5.1 新增文件

```
clinetcsharp/
├── Scripts/
│   └── IntegratedPanel.cs          # 综合面板主逻辑（拖动、频道切换、日志追加）
├── scenes/
│   └── integrated_panel.tscn       # 场景文件（PanelContainer + TabBar + RichTextLabel）
└── ...
```

### 5.2 主场景挂载

- 在 `main.tscn` 中实例化 `IntegratedPanel.tscn`，作为 `CanvasLayer` 或直接附加到现有 UI 节点下。

### 5.3 NetworkManager 扩展

- 新增信号：`CombatLogReceivedEventHandler(List<CombatLogEntry> entries)`
- 在消息分发中注册 `CombatLogNotify` 的解析与触发。

### 5.4 关键 API 设计（IntegratedPanel.cs）

```csharp
public partial class IntegratedPanel : PanelContainer
{
    // 追加一条或多条战斗日志
    public void AppendCombatLogs(Godot.Collections.Array<CombatLogEntry> entries);
    
    // 从服务器恢复位置
    public void RestorePosition(float x, float y, float w, float h);
    
    // 保存位置到服务器（内部防抖）
    private void SavePositionDebounced();
}
```

---

## 6. 服务端实现方案

### 6.1 DB 层

- `skynet_src/game/db/service.lua` 中角色表增加字段：
  - `ui_panel_pos_x`, `ui_panel_pos_y`, `ui_panel_width`, `ui_panel_height`
- 已有角色自动迁移默认值。

### 6.2 map_pool/combat 层

- `CombatManager` 在以下时机调用 `broadcastCombatLog`：
  - `onCollision`（战斗开始）
  - `executeFirstStrike`（先手攻击）
  - `applyDamage`（造成伤害）
  - `removeRelation` / 脱战（战斗结束）
  - `onDeath`（死亡）

- `broadcastCombatLog` 构造 `CombatLogEntry` 列表，通过 `map_pool` 的 `broadcastToMap` 发送给地图内所有玩家。

### 6.3 消息 ID

- 需要在 `message_id.proto` 中新增 `GAME_COMBAT_LOG_NOTIFY = 4XX`（待分配具体 ID）。

---

## 7. 下一步任务拆分

1. **协议层**：更新 `game.proto` 和 `message_id.proto`，重新生成 C# / Lua 协议代码。
2. **服务端 DB**：增加 `ui_panel_pos_*` 字段，提供读写接口。
3. **服务端战斗日志**：在 `map_pool/combat/manager.lua` 中插入日志广播。
4. **客户端 UI**：创建 `IntegratedPanel` 场景和脚本。
5. **客户端网络**：在 `NetworkManager` 中解析 `CombatLogNotify` 并连接信号。
6. **位置持久化**：客户端进入游戏恢复位置，拖动结束后保存位置。

---

## 8. 待确认问题

1. **面板是否必须支持调整大小（右下角拖拽）？**  
   → 当前设计为支持（宽/高都保存到服务端），如果只需要固定尺寸可简化实现。

2. **战斗日志是否只显示"与玩家自己相关"的战斗？还是同屏所有战斗？**  
   → 当前设计为**广播给同地图所有玩家**（所有人看到同屏发生的所有战斗），如果只需要看到自己的战斗，服务端过滤条件需要修改。

3. **`EnterGameResponse` 是否直接带上 `ui_panel_pos_*`？还是单独一个请求获取？**  
   → 推荐直接扩展 `FullRoleInfo`，减少一次请求往返。

4. **时间戳显示格式**：精确到秒（`HH:MM:SS`）还是只显示分钟（`MM:SS`）？  
   → 当前设计为 `HH:MM:SS`。
