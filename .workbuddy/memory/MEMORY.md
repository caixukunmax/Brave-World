# tslua2 项目长期笔记

## 项目概况
- Godot 4.6 客户端（clinetcsharp/，目录名拼写错误，应为 clientcsharp）+ C# 服务端（servercsharp/）MMORPG
- 技术栈：net8.0, GodotSharp 4.6.2, MongoDB, Serilog, Protobuf, Luban 配置表
- net8.0, Nullable enable, TreatWarningsAsErrors=true（但 NoWarn 抑制了一批 CA 警告）
- Central Package Management（Directory.Packages.props）

## 架构核心约束（来自 AGENTS.md，务必遵守）
1. **单线程游戏循环**：所有修改核心状态（WorldState、移动预占、CombatManager/MonsterManager 集合、PlayerSessionManager 在线状态）的代码必须通过 IGameLoopScheduler 入队，在单一逻辑线程串行执行。禁止直接在多线程 handler/tick/callback 修改。
2. **热路径禁 LogInformation**：MonsterTick/CombatTick 等高频 tick 禁止 LogInformation，用 LogDebug。
3. **shader fragment() 禁 return**：CanvasItem shader fragment() 中禁止 return，会导致未定义行为；用 if-else-if-else 链；无地形颜色格子背景不要设透明（网格线会看不清）。
4. **PlayerEnter 调用点**：登录、切图、死亡重生三个入口必须完整填充 PlayerSnapshot 的 EquippedSkills/Job/MoveSpeedMs 等字段；新增 MapPlayerState 字段要同步检查所有 PlayerEnter 调用点。
5. **数值放 Luban 配置表**，不要硬编码到 GameConstants。
6. **版本控制铁律**：未经用户明确指令禁止 git commit/push/merge/rebase/reset。

## 已知技术债
- CombatManager.cs 1656 行 God Object，应拆分为 DamageService/BuffService/RegenService/CombatBroadcaster
- MapEditor.cs 2819 行超大类，应拆分
- clinetcsharp 拼写已渗透 namespace/csproj，改名成本中等
- GameConstants 大量 const 硬编码（StartGold/BaseHp 等），LoadFromConfig 是空壳
- 测试缺口大：所有 Player Handler、Tick 主流程、DeathResponder 无测试

## 重要文件位置
- 服务端入口：servercsharp/src/.../Program.cs（含 PlayerAutoSaveLoop 跨线程问题）
- 战斗核心：CombatManager.cs + CombatTrace.cs
- 地图服务：MapService.cs（PlayerEnter、RefreshCombatPositions）
- 客户端移动预测：clinetcsharp/.../Player.Movement.cs
- 配置表流程：tables/（Luban），构建 scripts/build_tables.ts
- 协议生成：protocols/（build_proto.ts 仅生成 C#，TS/Lua 链已废弃）

## 2026-07-09 审查发现的待办（按优先级）
1. CombatTrace/CombatManager 热路径日志降级 LogDebug
2. screenshots 改存 user:// + gitignore + 清缓存
3. 删违规 shader（test6/test6a/.bak）
4. 删 protocols/include/include/ 重复 proto
5. 删 @ai-sdk/openai-compatible 死依赖
6. PlayerAutoSaveLoop 入队 GameLoop
7. PlayerEnter 三处 EquippedSkills 复制统一 + PreferredSkillId 补字段
