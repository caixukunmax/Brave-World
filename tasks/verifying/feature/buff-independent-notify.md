# Buff 独立广播协议

## 现象
GM addbuff/removebuff 命令执行后，客户端 BuffBar 不更新，因为 buff 通知绑在 CombatStateNotify 里，非战斗场景无法推送。

## 根因
Buff 通知耦合在 CombatStateNotify 中，只有战斗 tick 时才推送。Buff 的来源不只有战斗（GM、道具、区域效果等），需要独立广播。

## 修复方案

### Step 1: Proto 加 BuffUpdateNotify
- game.proto 新增 `BuffUpdateNotify` 消息，复用 `CombatStateNotify.BuffInfo` 结构
- 新增 MessageId 枚举值

### Step 2: 重新生成双端协议代码
- 运行 proto 生成脚本，更新客户端和服务端的 generated 代码

### Step 3: 服务端推送
- GmAddBuff/GmRemoveBuff 执行后，构建 BuffUpdateNotify 推给玩家
- CombatStateNotify 里的 BuffInfo 保留（战斗 tick 仍推完整状态）

### Step 4: 客户端订阅
- NetworkManager 加 BuffUpdateNotify 事件
- BuffBar 订阅 BuffUpdateNotify 更新显示

## 影响范围
- `protocols/proto/game.proto` — 新增 BuffUpdateNotify
- `protocols/proto/message_id.proto` — 新增枚举值
- 服务端 `GmCommandHandler.cs` — addbuff/removebuff 后推送
- 服务端 `GameLogicFactory.cs` — GmAddBuff/GmRemoveBuff 可能需要返回 buff 信息
- 客户端 `NetworkManager.cs` — 新增事件
- 客户端 `BuffBar.cs` — 订阅新事件

## 验证方式
1. GM addbuff 命令执行后，BuffBar 立即显示对应 buff 图标
2. GM removebuff 命令执行后，BuffBar 立即移除对应 buff
3. 战斗中技能施加 buff 仍然正常显示（CombatStateNotify 不受影响）