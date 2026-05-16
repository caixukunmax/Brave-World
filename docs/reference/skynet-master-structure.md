# Skynet 框架目录结构详解

## 概述

Skynet 是一个基于 Actor 模型的多用户 Lua 框架，主要用于游戏服务器开发。它采用 C 语言编写核心引擎，Lua 编写业务逻辑的分层架构。

```
skynet-master/
├── .github/          # GitHub 配置（Issue 模板、CI 工作流）
├── 3rd/              # 第三方依赖库
├── examples/         # 示例程序和配置文件
├── lualib/           # Lua 标准库（业务层使用）
├── lualib-src/       # Lua C 扩展库源码
├── service/          # 内置服务（Lua 层）
├── service-src/      # 内置服务（C 层源码）
├── skynet-src/       # Skynet 核心引擎源码
└── test/             # 测试用例
```

---

## 1. skynet-src/ - 核心引擎层

这是 Skynet 的心脏，用 C 语言实现，提供底层基础设施。

### 核心模块

| 文件 | 作用 |
|------|------|
| `skynet_main.c` | 程序入口，初始化整个框架 |
| `skynet_server.c` | 服务器主循环，管理所有服务实例 |
| `skynet_module.c` | 动态模块加载系统（加载 C 服务） |
| `skynet_mq.c` | 消息队列实现（Actor 间通信核心） |
| `skynet_handle.c` | 服务句柄管理（地址分配、查找） |
| `skynet_socket.c` | 网络层封装（epoll/kqueue 抽象） |
| `socket_server.c` | 底层网络事件处理 |
| `skynet_timer.c` | 定时器实现（时间轮算法） |
| `skynet_harbor.c` | 跨节点通信（集群间消息路由） |
| `skynet_monitor.c` | 服务监控（死循环检测、性能统计） |
| `malloc_hook.c` | 内存分配钩子（统计、jemalloc 集成） |

### 关键概念

- **服务（Service）**: 独立的 Lua VM 实例，Actor 模型的执行单元
- **消息队列（MQ）**: 每个服务有自己的队列，Skynet 调度器轮询分发
- **句柄（Handle）**: 服务的唯一标识，32 位整数

---

## 2. service-src/ - C 层服务源码

C 语言实现的底层服务，编译成动态库（.so）被 Skynet 加载。

| 文件 | 作用 |
|------|------|
| `service_snlua.c` | **最关键** - Lua 服务沙盒，每个 Lua 服务都运行在此之上 |
| `service_gate.c` | 网关服务，处理 TCP 连接、数据包分割 |
| `service_harbor.c` | 集群节点间的网络连接服务 |
| `service_logger.c` | 日志服务，输出到文件或控制台 |
| `databuffer.h` | 网关的数据缓冲区管理 |
| `hashid.h` | 连接 ID 哈希管理 |

> 💡 **service_snlua.c** 是 Lua 世界的入口，它创建 Lua VM，加载脚本，处理消息分发。

---

## 3. service/ - Lua 层内置服务

用 Lua 编写的系统服务，放在 `service/` 目录会被自动加入路径。

| 文件 | 作用 |
|------|------|
| `bootstrap.lua` | **启动服务** - 第一个运行的服务，负责初始化其他系统服务 |
| `launcher.lua` | 服务管理器，处理服务的创建、销毁、查询 |
| `gate.lua` | Lua 层网关逻辑，配合 C 层 gate 服务处理连接 |
| `debug_console.lua` | 调试控制台，提供运行时管理接口 |
| `clusterd.lua` | 集群管理守护进程 |
| `clusteragent.lua` | 集群代理，处理节点间消息 |
| `clustersender.lua` | 集群消息发送器 |
| `datacenterd.lua` | 数据中心，全局共享数据存储 |
| `sharedatad.lua` | 共享数据服务（只读表共享） |
| `multicastd.lua` | 组播服务，高效广播消息 |
| `snaxd.lua` | Snax 服务框架支持 |
| `service_mgr.lua` | 服务提供者管理 |
| `console.lua` | 本地控制台 |
| `cmaster/cslave.lua` | 主从模式集群管理（旧版，已被 cluster 替代） |

### 启动流程
```
skynet_main → bootstrap.lua → launcher → 其他系统服务
```

---

## 4. lualib/ - Lua 标准库

业务开发主要使用的 Lua 模块。

### 4.1 lualib/skynet/ - 核心 API 库

| 文件 | 作用 |
|------|------|
| `skynet.lua` | **核心库** - 服务启动、消息收发、协程管理 |
| `socket.lua` | 网络套接字封装（协程风格异步 IO） |
| `socketchannel.lua` | 连接池管理（数据库连接等） |
| `cluster.lua` | 集群 API（跨节点调用） |
| `sharedata.lua` | 共享数据读取接口 |
| `sharetable.lua` | 共享表（可热更新） |
| `datacenter.lua` | 数据中心接口 |
| `multicast.lua` | 组播 API |
| `manager.lua` | 服务管理接口 |
| `queue.lua` | 消息队列包装（顺序处理） |
| `snax.lua` | Snax 框架（简化服务编写） |
| `db/` | 数据库驱动（mongo.lua, mysql.lua, redis.lua） |
| `datasheet/` | 数据表工具（配置表转换） |
| `debug.lua` | 调试工具 |

### 4.2 lualib/http/ - HTTP 协议栈

| 文件 | 作用 |
|------|------|
| `httpc.lua` | HTTP 客户端 |
| `httpd.lua` | HTTP 服务端 |
| `websocket.lua` | WebSocket 支持 |
| `url.lua` | URL 解析 |
| `internal.lua` | 协议解析内部逻辑 |

### 4.3 lualib/snax/ - Snax 框架

简化服务开发的封装层，隐藏消息细节。

| 文件 | 作用 |
|------|------|
| `gateserver.lua` | 网关服务器封装 |
| `loginserver.lua` | 登录服务器封装 |
| `msgserver.lua` | 消息服务器封装 |
| `hotfix.lua` | 热更新支持 |

### 4.4 其他重要文件

| 文件 | 作用 |
|------|------|
| `sproto.lua` | Sproto 协议库（类似 Protocol Buffers） |
| `sprotoloader.lua` | Sproto 协议加载 |
| `sprotoparser.lua` | Sproto 协议解析器 |
| `md5.lua` | MD5 工具 |
| `loader.lua` | 服务加载器 |

---

## 5. lualib-src/ - C 扩展库源码

为 Lua 提供高性能 C 扩展。

| 文件 | 作用 |
|------|------|
| `lua-skynet.c` | Lua 与 Skynet 核心交互的 C API |
| `lua-socket.c` | 网络操作 C API |
| `lua-seri.c` | 消息序列化（零拷贝优化） |
| `lua-netpack.c` | 网络包解析 |
| `lua-crypt.c` | 加密算法（AES, DES, RSA, DH, HMAC, BASE64） |
| `lua-bson.c` | MongoDB BSON 格式支持 |
| `lua-mongo.c` | MongoDB 驱动底层 |
| `lua-memory.c` | 内存统计接口 |
| `lua-debugchannel.c` | 调试通道 |
| `lua-stm.c` | 共享内存事务 |
| `lsha1.c` | SHA1 算法 |
| `ltls.c` | TLS/SSL 支持 |
| `sproto/` | Sproto 协议的 C 实现 |

---

## 6. 3rd/ - 第三方库

| 目录 | 作用 |
|------|------|
| `lua/` | **Lua 5.5 源码** - Skynet 使用修改版 Lua，支持多 State |
| `lpeg/` | LPeg - Lua 模式匹配库（用于解析协议） |
| `jemalloc/` | 内存分配器（替代系统 malloc，更好性能/统计） |
| `lua-md5/` | MD5 算法实现 |
| `compat-mingw/` | Windows MinGW 兼容性层 |

---

## 7. examples/ - 示例代码

学习 Skynet 的最佳入口。

### 配置文件

| 文件 | 作用 |
|------|------|
| `config` | 默认配置，单机模式启动 |
| `config.login` | 登录服集群配置示例 |
| `config.c1/c2` | 集群节点配置示例 |
| `config.mongodb/mysql` | 数据库配置示例 |

### 示例程序

| 文件 | 作用 |
|------|------|
| `main.lua` | 示例主服务 |
| `agent.lua` | 连接代理示例 |
| `watchdog.lua` | 看门狗服务（管理 agent） |
| `client.lua` | 测试客户端 |
| `login/` | 登录流程完整示例 |
| `simpledb.lua` | 简单数据库服务示例 |
| `share.lua` | 共享数据示例 |

---

## 8. test/ - 测试用例

各种功能的测试脚本。

| 文件 | 作用 |
|------|------|
| `testsocket.lua` | 网络测试 |
| `testmongo.lua` | MongoDB 测试 |
| `testmysql.lua` | MySQL 测试 |
| `testredis.lua` | Redis 测试 |
| `testcluster.lua` | 集群测试 |
| `testmulticast.lua` | 组播测试 |
| `testtimer.lua` | 定时器测试 |
| `testcoroutine.lua` | 协程测试 |
| `testservice/` | 测试专用服务 |

---

## 架构总结

```
┌─────────────────────────────────────────────────────┐
│  应用层 (你的游戏逻辑)                                 │
│  - login, player, game, db, gateway...              │
├─────────────────────────────────────────────────────┤
│  Lua 标准库 (lualib/)                                │
│  - skynet, socket, cluster, db drivers...           │
├─────────────────────────────────────────────────────┤
│  Lua 内置服务 (service/)                             │
│  - bootstrap, launcher, gate, debug_console...      │
├─────────────────────────────────────────────────────┤
│  C 扩展库 (lualib-src/)                              │
│  - lua-skynet, lua-socket, lua-crypt, sproto...     │
├─────────────────────────────────────────────────────┤
│  C 系统服务 (service-src/)                           │
│  - service_snlua (Lua VM), service_gate...          │
├─────────────────────────────────────────────────────┤
│  核心引擎 (skynet-src/)                              │
│  - server, module, mq, timer, socket, harbor...     │
├─────────────────────────────────────────────────────┤
│  第三方库 (3rd/)                                     │
│  - lua, lpeg, jemalloc                              │
└─────────────────────────────────────────────────────┘
```

## 开发建议

1. **入门**: 从 `examples/` 开始，特别是 `main.lua` + `config`
2. **业务开发**: 主要使用 `lualib/skynet/` 下的 API
3. **网络协议**: 推荐 Sproto (`lualib/sproto.lua`)
4. **数据库**: 使用 `lualib/skynet/db/` 下的驱动
5. **集群**: 参考 `examples/` 中的 cluster 配置

---

> 📚 官方文档: https://github.com/cloudwu/skynet/wiki
