# 日志规范与排障手册

> 最后更新：2026-04-28

## 一、日志规范

### 1.1 当前日志基础设施

- **框架：** Serilog
- **输出：** Console + File（`logs/server-.log`，按天滚动）
- **级别：** Debug（全量输出）
- **实时查看：** `servercsharp/watch-logs.ps1`

### 1.2 日志标签约定

现有代码已广泛使用 `[Tag]` 前缀，保持一致：

| 模块 | 标签 | 示例 |
|------|------|------|
| 战斗系统 | `[Combat]` | `[Combat] damage: attacker={A} target={T} dmg={D}` |
| 死亡响应 | `[DeathResponder]` | `[DeathResponder] player death: account={Id}` |
| 脱战 | `[Disengage]` | `[Disengage] DistanceB triggered: ...` |
| 升级 | `[LevelUp]` | `[LevelUp] account={Id} leveled up!` |
| 技能管线 | `[SkillPipeline]` | `[SkillPipeline] action error: {Type}` |
| 怪物 | `[Monster]` | `[Monster] died: id={Id}` |
| NPC | `[Npc]` | `[Npc] Initialized NPC {Name}` |
| 移动 | `[Move]` / `[MoveConfirm]` / `[MoveCollision]` | `[Move] collision move: player={Id}` |
| 碰撞 | `[Collision]` | `[Collision] player={Id} collides monster={Mid}` |
| 热更新 | `[HotReload]` | `[HotReload] Loading {Dll}` |
| 配置表 | `[Tables]` | `[Tables] loaded: {Count} monsters` |

### 1.3 结构化日志字段

**必须使用 Serilog 结构化模板**（`{Key}` 占位符），禁止字符串插值：

```csharp
// ✅ 正确：结构化日志
_logger.LogInformation("[Combat] damage: attacker={Attacker} target={Target} dmg={Damage}", attackerId, targetId, damage);

// ❌ 错误：字符串插值（无法搜索/过滤）
_logger.LogInformation($"[Combat] damage: attacker={attackerId} target={targetId} dmg={damage}");
```

### 1.4 关键业务字段标准

排查跨模块问题时，以下字段应保持命名一致：

| 字段 | 含义 | 使用位置 |
|------|------|----------|
| `AccountId` | 账号 ID | 登录、移动、战斗、升级 |
| `EntityId` | 实体 ID（玩家/怪物/NPC） | 战斗、碰撞、死亡 |
| `MapId` / `Map` | 地图标识 | 切图、碰撞、怪物初始化 |
| `MsgId` | 消息 ID | 网关路由 |
| `ConnId` | 连接 ID | 网关连接管理 |
| `MonsterId` | 怪物模板 ID | 怪物生成、碰撞 |
| `InstanceId` | 怪物实例 ID | 怪物战斗、死亡 |

### 1.5 日志级别使用规范

| 级别 | 使用场景 | 示例 |
|------|----------|------|
| `LogDebug` | 高频细节，生产可关 | 收到每条消息、每帧 tick |
| `LogInformation` | 正常业务流程 | 登录成功、战斗触发、怪物死亡 |
| `LogWarning` | 可恢复的异常 | 找不到配置、验证失败、消息无路由 |
| `LogError` | 需要关注的错误 | 异常堆栈、构建失败、连接中断 |

---

## 二、排障手册

### 2.1 日志查看

```powershell
# 实时查看所有日志
.\servercsharp\watch-logs.ps1

# 只看错误和致命
.\servercsharp\watch-logs.ps1 --error

# 只看警告及以上
.\servercsharp\watch-logs.ps1 --warn

# 先看最后 50 行，再实时追踪
.\servercsharp\watch-logs.ps1 --tail 50
```

### 2.2 常见故障排查

#### 🔴 服务端启动失败

| 症状 | 查什么 | 关键字 |
|------|--------|--------|
| 启动闪退 | 看 console 输出 | `Game Server Starting` / 异常堆栈 |
| MongoDB 连不上 | `[Tables]` 或 console | `MongoDB` / `connected` |
| 配置表加载失败 | `[Tables]` 日志 | `not found` / `no Luban tables` |
| 端口占用 | console | `Socket` / `bind` / `AddressAlreadyInUse` |

**修复步骤：**
1. 确认 MongoDB 运行中
2. 确认 `servercsharp/data/tables/` 有 JSON 文件（需先 `npm run build:tables`）
3. 确认端口未被占用

#### 🟡 登录问题

| 症状 | 查什么 | 关键字 |
|------|--------|--------|
| 登录无响应 | 网关日志 | `New connection` / `No route for msgId` |
| 登录失败 | `[Login]` 日志 | `Login success` / `Auto-registered` |
| Token 过期 | `[Login]` 日志 | `expired` / `invalid token` |

**排查路径：** 客户端 → 网关（`ConnId`）→ LoginService → 数据库

#### 🟡 战斗问题

| 症状 | 查什么 | 关键字 |
|------|--------|--------|
| 碰撞不触发 | `[Collision]` + `[Move]` | `collides` / `collision detected` |
| 伤害不对 | `[Combat]` 日志 | `damage: attacker=... target=... dmg=` |
| 怪物不死亡 | `[Monster]` 日志 | `died` / `damaged` / `hp=` |
| 技能报错 | `[SkillPipeline]` 日志 | `action error` / `unknown action` |
| 脱战异常 | `[Disengage]` 日志 | `DistanceA triggered` / `DistanceB triggered` |

**排查路径：** 移动碰撞 → CombatManager → SkillPipeline → DeathResponder → LevelUp

#### 🟡 移动/切图问题

| 症状 | 查什么 | 关键字 |
|------|--------|--------|
| 移动卡住 | `[Move]` + `[MoveConfirm]` | `collision move` / `confirmed` |
| 切图失败 | `[Map]` 日志 | `PlayerEnter` |
| 位置不同步 | `[MoveCollision]` 日志 | `validation failed` / `validated` |

#### 🟠 热更新问题

| 症状 | 查什么 | 关键字 |
|------|--------|--------|
| DLL 加载失败 | `[HotReload]` 日志 | `Loading` / `Factory created` |
| 旧逻辑残留 | `[HotReload]` 日志 | `Unloading old ALC` / `Old ALC unloaded` |

**修复步骤：**
1. 确认 `GameServer.GameLogic.dll` 已重新构建
2. 触发热更新（修改 DLL 时间戳）
3. 看 `[HotReload]` 日志确认加载成功

#### 🟠 网关/连接问题

| 症状 | 查什么 | 关键字 |
|------|--------|--------|
| 连不上 | 网关日志 | `Gateway listening` / 端口号 |
| 频繁断线 | 网关日志 | `Heartbeat timeout` / `Connection closed` |
| 消息丢失 | 网关日志 | `No route for msgId` / `Send failed` |

### 2.3 全链路排查流程

遇到跨模块问题时，按此顺序追踪：

```
1. 网关层：ConnId → MsgId → 路由是否命中
2. 服务层：Handler 是否执行 → 业务逻辑日志
3. 数据层：数据库查询是否成功 → 数据是否正确
4. 配置层：Luban 表是否加载 → 配置值是否合理
```

### 2.4 快速诊断命令

```powershell
# 一键验证：构建 + 测试
npm run verify

# 只跑测试
npm run test

# 只构建服务端
npm run build:server

# 查看服务端是否在运行
Get-Process GameServer -ErrorAction SilentlyContinue

# 查看最新日志最后 30 行
Get-Content (Get-ChildItem servercsharp\logs\server-*.log | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName -Tail 30
```
