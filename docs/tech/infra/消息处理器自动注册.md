# 消息处理器自动注册设计

> 状态：已实现
> 日期：2026-04-29
> 适用范围：`servercsharp/` C# 服务端热更链路

## 1. 背景

当前 C# 服务端的消息处理器声明与注册存在双轨制：

- Handler 类使用 `HandlesMessageAttribute` 标注消息 ID。
- `GameLogicFactory.RegisterMessageHandlers(...)` 仍然手工调用 `registry.Add(...)` 完成实际注册。

这导致“声明了 handler 但未进入注册链”的问题会被静默引入。已确认的真实案例是：

- `NpcCombatHandler` 已实现并声明 `GameNpcCombatReq`
- 客户端会发送 `GameNpcCombatReq`
- 但 `GameLogicFactory` 没有手工注册该 handler
- 最终表现为 Gateway 返回 `No route for msgId=403`

这不是单点遗漏，而是“半迁移状态”带来的系统性风险。

## 2. 目标

本次改造目标：

1. 让 `HandlesMessageAttribute` 成为 C# 服务端消息处理器的唯一声明源。
2. 消除 `GameLogicFactory` 中手工维护的 `registry.Add(...)` 列表。
3. 保证新增 handler 只要实现 `IMessageHandler` 并标注 `HandlesMessageAttribute`，就会自动进入注册链。
4. 让启动和热更阶段对“无法实例化的 handler”显式失败，而不是静默跳过。
5. 顺带修复 `NpcCombatHandler` 漏注册问题。

## 3. 非目标

本次不做以下事项：

1. 不引入 Roslyn Source Generator 或其他编译期代码生成方案。
2. 不重构 Gateway 路由模型。
3. 不修改消息协议定义、消息 ID 编号或客户端发包逻辑。
4. 不顺带处理 NPC 战斗属性来源、Luban 配置补齐等其他历史债。

## 4. 当前问题与根因

### 4.1 表象

- 已存在的 handler 可能因为未手工加入 `GameLogicFactory` 而完全不生效。
- `HandlesMessageAttribute` 当前只提供“装饰性声明”，并不驱动注册。
- 新增 handler 时，开发者必须同时修改两处：handler 类本身和工厂注册列表。

### 4.2 根因

当前架构中，“消息声明”与“路由注册”分离在两个位置维护：

- 类型侧：`HandlesMessageAttribute`
- 工厂侧：`registry.Add(...)`

这违反了单一真相源原则。历史上系统已经向“attribute 驱动注册”演进了一半，但没有完成最后一步，因此出现漏注册是高概率事件，而不是偶发粗心。

## 5. 方案对比

### 方案 A：保留手工注册，补一致性校验

做法：继续保留 `registry.Add(...)`，但在测试中比对 attribute 与注册列表。

优点：

- 改动最小。
- 不需要改工厂接口。

缺点：

- 治标不治本。
- 仍然保留两份声明源。
- 运行时仍依赖人工同步。

### 方案 B：运行时自动发现并自动注册

做法：在 `GameLogicFactory` 中扫描热更程序集内所有实现 `IMessageHandler` 的类型，根据 `HandlesMessageAttribute` 自动实例化并注册。

优点：

- `HandlesMessageAttribute` 成为唯一声明源。
- 最贴合当前 ALC 热更边界。
- 不需要引入新的构建步骤。
- 能直接根治当前漏注册问题。

缺点：

- 需要为 handler 构造参数提供统一依赖解析。
- 需要调整 `IGameLogicFactory.RegisterMessageHandlers(...)` 参数，补齐 `WorldState`、`EventBus` 等依赖。

### 方案 C：编译期生成注册表

做法：引入 Source Generator 或外部生成步骤，在编译时生成强类型注册代码。

优点：

- 运行时无反射。
- 可在编译期暴露更多错误。

缺点：

- 引入成本高。
- 与当前仓库文档驱动 + 最小修复目标不匹配。

## 6. 选型

采用方案 B：运行时自动发现并自动注册。

理由：

1. 现有代码已经具备 `HandlesMessageAttribute`，说明目标方向明确。
2. 当前问题已经证明手工注册机制不可靠。
3. 运行时反射方案可以在不改变外部协议和路由模型的前提下完成根治。
4. 对 ALC 热更场景更自然，注册逻辑仍然留在热更 DLL 一侧。

## 7. 设计说明

### 7.1 设计原则

1. `HandlesMessageAttribute` 是唯一消息声明源。
2. `MessageHandlerRegistry` 只负责持有 `(msgId, handler)` 并挂到 `MessageRouter`，不负责扫描程序集。
3. `GameLogicFactory` 负责发现 handler 类型、解析依赖并完成实例化。
4. 若某个已声明 handler 无法实例化，启动或热更必须显式失败。

### 7.2 注册流程

目标流程如下：

1. 宿主启动后创建 `HotReloader`。
2. `HotReloader.Load()` 载入 `GameServer.GameLogic.dll`。
3. `GameLogicFactory.RegisterMessageHandlers(...)` 扫描当前热更程序集中的 `IMessageHandler` 实现类。
4. 对每个带 `HandlesMessageAttribute` 的 handler：
   - 读取 `MessageId`
   - 解析构造函数参数
   - 创建实例
   - 调用 `registry.Add(messageId, handler)`
5. 宿主调用 `registry.RegisterAll(router)` 完成路由挂载。

### 7.3 依赖解析规则

自动实例化只支持当前实际存在的依赖类型：

- `PlayerSessionManager`
- `INetworkSender`
- `MapDataProvider`
- `IMonsterAiService`
- `WorldState`
- `EventBus`
- `LubanTableLoader`
- `ILogger<THandler>`

规则如下：

1. 优先使用单个公开构造函数。
2. 构造函数参数必须全部可解析。
3. `ILogger<THandler>` 通过 `ILoggerFactory.CreateLogger<THandler>()` 创建。
4. 如果参数类型不在受支持列表内，直接抛出异常并终止注册。

### 7.4 接口调整

为满足 `NpcCombatHandler` 等 handler 的实例化，需要扩展 `IGameLogicFactory.RegisterMessageHandlers(...)` 及其调用链，至少增加：

- `WorldState`
- `EventBus`

这样自动注册不会依赖宿主侧对具体 handler 的了解，只传递“可用于构造 handler 的上下文依赖”。

### 7.5 错误处理

#### 情况 1：handler 未标注 `HandlesMessageAttribute`

- 该类型不参与消息注册。
- 可记录 debug 日志，说明被跳过。

#### 情况 2：已标注但无法实例化

- 直接抛出 `InvalidOperationException`。
- 错误消息必须包含 handler 类型名和缺失依赖类型。
- 启动或热更中止，避免产生“客户端可发消息但服务端无路由”的隐性故障。

#### 情况 3：多个 handler 使用同一 `MessageId`

- 视为配置错误。
- 自动注册阶段直接失败，不允许后注册覆盖前注册。

## 8. 影响范围

### 8.1 需要修改的文件

- `servercsharp/src/GameServer.Services/Core/IGameLogicFactory.cs`
- `servercsharp/src/GameServer.Services/Core/HotReloader.cs`
- `servercsharp/src/GameServer/Program.cs`
- `servercsharp/src/GameServer.GameLogic/GameLogicFactory.cs`
- `servercsharp/src/GameServer.Tests/*`

### 8.2 不应修改的文件

- `protocols/` 下的 proto 源文件
- `servercsharp/src/GameServer.Proto/generated/` 生成物
- 客户端消息发送逻辑

## 9. 测试策略

### 9.1 单元测试

新增针对自动注册链路的测试，覆盖：

1. 带 `HandlesMessageAttribute` 的 handler 会被成功注册。
2. `NpcCombatHandler` 对应 `GameNpcCombatReq` 会出现在注册表中。
3. 若存在重复 `MessageId`，注册阶段会失败。
4. 若存在无法解析依赖的 handler，注册阶段会失败并给出明确错误。

### 9.2 回归验证

1. 运行 `GameServer.Tests` 相关测试。
2. 运行仓库级验证命令确认构建通过。
3. 如涉及服务端行为声称“可运行”，需使用 `servercsharp/restart.bat` 或等效入口验证。

## 10. 风险与注意事项

1. 反射扫描范围必须限定在当前热更程序集，不能误扫宿主程序集。
2. 自动实例化依赖表必须保持收敛，避免演变成隐式 Service Locator。
3. 如果后续 handler 依赖继续增长，应考虑引入显式上下文对象或 DI 适配，而不是无限扩展参数分支。
4. `SkillPipeline.GetSkillConfigStatic(...)` 之类静态依赖仍是现存耦合点，但不属于本次处理范围。

## 11. 验收标准

满足以下条件视为本次设计落地完成：

1. `GameLogicFactory` 不再手工枚举 `registry.Add(...)`。
2. `NpcCombatHandler` 无需额外补丁即可自动注册。
3. 新增一个带 `HandlesMessageAttribute` 的 handler 后，不修改注册表代码也能被发现。
4. 测试能够在重复消息 ID 或缺失依赖时显式失败。
5. 热更路径与首次启动路径使用同一套自动注册机制。

## 12. 与旧文档关系

`docs/tech/服务注册与消息路由.md` 描述的是历史上的 Skynet/Lua 服务自注册机制，不代表当前 `servercsharp/` C# 服务端实现。

当前 C# 服务端的消息处理器注册设计，以本文为准。