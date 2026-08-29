# 代码审查与优化 跟踪表（2026-08-28）

范围：Brave-World 客户端(C#)+服务器(C#)+性能+我最近改的代码。交付方式：先清单、经确认后逐项修，每改一组编译验证。所有改动**未提交**，且**逻辑修复需游戏内实测**（审查环境无法跑游戏循环）。

## 一、已修复（客户端 + 服务器均编译 0 错 0 警）

### 批 1（低风险）
- 服务器热路径日志 `LogInformation→LogDebug`：`MonsterManager.cs`(受伤/脱战/追击超时)、`WorldState.cs`(玩家移动)、`MoveHandler.cs`(全部 6 处)、`CollisionDetector.cs`(路过 NPC)。
- `DevServerLauncher.cs`：`Kill()` 后加 `WaitForExit(3000)`，消除快速重启 F5 的"附加到垂死服务器"竞态。
- `map_panel.gd`：删除确认改回调式（取消/关闭不再泄漏协程+残留对话框）；npm 命令加"路径含空格"守卫。
- `MeleeSlashEffect.cs`：Shader 静态只编译一次 + 发光纹理按尺寸静态缓存（战斗不再每次攻击逐像素生成）。

### 批 2（核心正确性）
- **#3** `Player.Movement.Recovery.cs:71`：`OnMoveCancelReceived` 身份比较由 `GetInstanceId()` 改为 `nm.AccountId`。此前服务端所有碰撞弹回/强制回滚通知被静默丢弃（权威脱同步第一根因）。
- **#5** `NetworkManager.Connection.cs`：新增 `ResetDecodeState()`，在断线检测与重连两处重置 `_bufferOffset/_bufferCount/_expectedLength`，消除半截包污染新连接 / 垃圾长度头撑爆缓冲。
- **#4(止血)** `Player.NetworkSync.cs`：订阅 `Disconnected`，断线时 `_moveSentCount=0`，避免重连后计数卡死导致永久无法移动。
- **#2** `CombatManager.OnCollision`：先建带玩家真实 `BuffContainer` 的 context 再 `CreateRelation`，修复 `ctx.Buffs` 与 `MapPlayerState.Buffs` 双数据源分裂（护盾吸收/buff 显示/过期）；并连带修复 `bothWereIdle` 在 `CreateRelation` 之后计算恒为假、导致**先手攻击 FirstStrike 从未触发**的 bug。⚠ 此改动会让 FirstStrike 从"从不触发"变为"会触发"，战斗手感会变，需实测。
- **#1** `GatewayService.cs` + `Program.cs`：新增 `OnPlayerDisconnected` 回调，连接关闭时把"玩家下线清理"投递到 `IGameLoopScheduler` 逻辑线程执行（`MapService.PlayerLeave` + `PlayerSessionManager.SetOffline`）。修复掉线/踢线后玩家残留在 WorldState/在线表、空间索引幽灵格泄漏。⚠ 战斗关系/怪物索敌的彻底清理需实测确认（怪物应在下 tick 因目标不在图中而脱战）。

## 二、评估后建议暂缓（附理由）

- **#4 的 MoveResponse session 精确关联**：审查初判为"乱序回滚错格"。深读后：TCP 同连接内响应有序；回滚位置由服务端权威下发（`MoveResponse.X/Y`、`MoveCancelNotify.RollbackX/Y`），客户端仅在 (0,0) 时才回退 `_moveFromPos`。故侵入式协议+管线改动收益低、风险高（无法运行验证）。**建议**：待有可运行回归环境再评估；#3/#5/#4止血 已覆盖实际 bug。

## 三、未做（性能 / 架构，按性价比排序，待后续批次）

性能：
- [高·服务器] `CollisionDetector.cs:35-104` 每次移动全实体扫描 + 每实体 LINQ（O(n²)）→ 改用 `GridEntities` 邻格查询。
- [高·服务器] `Pathfind.cs:18` 无界 BFS，目标不可达每 500ms 扫全图 → 搜索半径上限 + 失败缓存。
- [中高·服务器] `CombatManager.BroadcastCombatState`(100ms) 全量重建 + 线性找实体 → 变化驱动 + `_entityLocations` 索引。
- [高·客户端] `GridManager.UpdateTerrainMask:993` 涂刷每帧全图逐像素重建 → `byte[]`+`CreateFromData`+帧末合并增量。
- [中·客户端] 传送门/酒馆每帧 `GetFirstNodeInGroup("player")`、`Monster` 每次移动重建 4 行标签、战斗日志按名字线性扫表（应改按 id）。

架构 / 线程 / 安全：
- [高·服务器] 热重载在控制台线程直接改路由字典/服务引用（`Program.cs:432`+`HotReloader.cs:107`）→ 整段 Enqueue 到 gameLoop；reload 后补 `MonsterService.Init()`。
- [高·服务器] 所有 handler（含登录 bcrypt）在逻辑线程 await DB，慢查询冻结全部 tick → 登录/创角移出逻辑线程；tick 入队加积压保护。
- [中] `MoveCompleteHandler.cs:224` 采信客户端坐标写回 Role；移动预占无超时回收；`GmCommandHandler` 无权限校验；NPC/玩家 ID 空间靠阈值启发式易重叠。
- [中] 怪物 AI `PatrolChaseBehavior` 把追击/贴身/脱战堆进非战斗 AI（违反 AGENTS.md 的 Overworld/Combat 划分）。

## 四、待你实测确认（本环境无法跑游戏）

1. 战斗：先手攻击现在会触发，手感是否符合预期；受击 buff/护盾显示与生效是否正确。
2. 移动：撞墙/撞怪弹回是否正确；断线重连后能否继续移动。
3. 掉线：玩家掉线后其格子是否释放、怪物是否停止追打幽灵。
4. 特效：多次施法不再卡顿；地图编辑器/配置面板交互正常。
