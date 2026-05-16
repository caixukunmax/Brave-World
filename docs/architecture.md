# tslua2 项目架构总览

> 最后更新：2026-04-28

## 一句话概括

tslua2 是一个 MMORPG 项目，C# 全栈：Godot 4 客户端 + .NET 8 服务端，Protobuf 通信，Luban 配置表。

> 项目名是历史遗留，实际没有 TypeScript / Lua。

---

## 仓库结构

```
tslua2/
├── protocols/          # Protobuf 协议定义 + 生成脚本
│   ├── protos/         #   .proto 源文件
│   ├── scripts/        #   build_proto.ts 生成脚本
│   └── gen/            #   [生成物] TS / Lua / C# 协议代码
├── tables/             # Luban 配置表定义 + 生成脚本
│   └── scripts/        #   build_tables.ts 生成脚本
├── servercsharp/       # .NET 8 服务端
│   ├── src/
│   │   ├── GameServer/           # 主程序入口（Host + TCP Server）
│   │   ├── GameServer.Common/    # 公共工具（安全、网络、配置、事件）
│   │   ├── GameServer.Database/  # MongoDB 数据访问
│   │   ├── GameServer.GameLogic/ # 游戏逻辑（热更新 DLL，ALC 加载）
│   │   ├── GameServer.Proto/     # Protobuf 协议生成代码引用
│   │   ├── GameServer.Services/  # 业务服务层
│   │   ├── GameServer.Tables/    # Luban 配置表加载
│   │   └── GameServer.Tests/     # 单元测试
│   ├── data/           # 运行时数据（tables JSON 等）
│   └── logs/           # 运行时日志
├── clinetcsharp/       # Godot 4 C# 客户端（目录名是历史遗留拼写偏差，文档中统一称"客户端"）
│   ├── Scripts/        #   C# 脚本（含 protos 生成代码）
│   ├── scenes/         #   Godot 场景
│   ├── assets/         #   美术资源
│   └── tools/          #   开发工具
├── docs/               # 项目文档
│   ├── architecture.md #   架构总览（本文档）
│   ├── design/         #   系统设计文档
│   │   ├── debug-panel/ #   调试面板设计子目录
│   │   ├── plans/      #   实施计划归档
│   │   └── _archive/   #   已过时设计文档
│   ├── tech/           #   技术文档与规范
│   ├── daily/          #   开发日报（按月分组）
│   ├── reports/        #   分析报告
│   └── reference/      #   外部资料与参考
├── scripts/            # 仓库级构建脚本
│   └── build.ps1       #   统一构建入口
├── .editorconfig       # 代码风格统一
├── Directory.Build.props    # C# 构建属性统一
└── Directory.Packages.props # NuGet 版本集中管理（CPM）
```

---

## 四条核心链路

### 链路 1：配置表 → 生成 → 服务端加载

```
tables/ 定义 (Excel/JSON)
    ↓ npm run build:tables (Luban)
servercsharp/data/tables/*.json    ← 服务端运行时读取
servercsharp/src/GameServer.Tables/ ← C# 强类型加载
```

**常见改动：** 修改数值/道具/技能/奖励/地图/活动配置
**Checklist：**
1. 修改 tables/ 下的定义文件
2. 运行 `npm run build:tables` 或 `pwsh scripts/build.ps1 tables`
3. 重启服务端验证加载

### 链路 2：Proto → 生成 → 双端通信

```
protocols/protos/*.proto
    ↓ npm run build:proto (protoc + ts-proto)
servercsharp/src/GameServer.Proto/generated/*.cs  ← 服务端引用
clinetcsharp/Scripts/protos/*.cs                   ← 客户端引用
protocols/gen/ts/*.ts                               ← TS 引用
protocols/gen/lua/*.lua                             ← Lua 引用
```

**常见改动：** 新增/修改协议消息
**Checklist：**
1. 修改 `protocols/protos/` 下的 .proto 文件
2. 运行 `npm run build:proto` 或 `pwsh scripts/build.ps1 proto`
3. 服务端和客户端代码同步更新（**必须双端同步**）
4. ⚠️ 消息字段编号不能随便改，只能追加

### 链路 3：客户端预测 → 服务端权威

```
客户端：预测操作 → 发送请求 → 播放动画
服务端：验证请求 → 计算结果 → 广播结果
客户端：收到结果 → 修正状态
```

**铁律：** 客户端预测不能突破服务端权威。服务端是唯一真相源。

### 链路 4：本地调试 → 重启验证

```
pwsh scripts/build.ps1 dev
    → kill 旧进程 → build tables → build proto → build server → 启动
```

**快速命令：**
| 命令 | 作用 |
|------|------|
| `pwsh scripts/build.ps1 tables` | 只构建配置表 |
| `pwsh scripts/build.ps1 proto` | 只构建协议 |
| `pwsh scripts/build.ps1 server` | 只构建服务端 |
| `pwsh scripts/build.ps1 all` | 构建全部 |
| `pwsh scripts/build.ps1 test` | 运行测试 |
| `pwsh scripts/build.ps1 verify` | 构建 + 测试 |
| `pwsh scripts/build.ps1 dev` | 构建 + 启动服务端 |
| `npm run verify` | 同 verify（npm 入口） |

---

## 目录命名约定

| 目录 | 含义 | 备注 |
|------|------|------|
| `clinetcsharp/` | Godot 4 C# 客户端 | 历史遗留拼写偏差，文档中统一称"客户端" |
| `servercsharp/` | .NET 8 服务端 | |
| `protocols/` | Protobuf 协议定义与生成 | |
| `tables/` | Luban 配置表定义与生成 | |
| `scripts/` | 仓库级构建脚本 | |
| `docs/` | 项目文档 | |

> ⚠️ `clinetcsharp` 重命名波及面大（Godot .csproj、脚本引用、文档链接），
> 需作为专项处理，不在日常改动中随意重命名。

---

## 高风险约束

1. **协议字段编号** — 只能追加，不能修改或删除已有编号
2. **配置表变更** — 必须同步验证客户端和服务端
3. **客户端预测** — 不能突破服务端权威
4. **生成物** — 禁止手工修改 `generated/`、`data/tables/`、`Scripts/protos/` 下的文件
5. **GameLogic 热更新** — 通过 ALC 加载，不与主项目直接引用，需单独构建

---

## 服务端模块依赖关系

```
GameServer (入口)
├── GameServer.Common
│   ├── GameServer.Proto
│   └── GameServer.Tables
├── GameServer.Database
│   └── GameServer.Common
├── GameServer.Services
│   ├── GameServer.Common
│   └── GameServer.Database
├── GameServer.Tables
├── GameServer.Proto
└── [GameServer.GameLogic] (ALC 热加载，不直接引用)
    ├── GameServer.Services
    ├── GameServer.Common
    ├── GameServer.Database
    └── GameServer.Tables
```

---

## 技术栈版本

| 组件 | 版本 |
|------|------|
| .NET | 8.0 |
| Godot | 4.x |
| Google.Protobuf | 3.28.3 |
| MongoDB.Driver | 3.2.1 |
| Serilog | 8.0 |
| Node.js | (protocols/tables 构建用) |

---

## 相关文档

### 技术规范
- [日志规范与排障手册](tech/logging-and-troubleshooting.md)
- [登录流程协议](tech/登录流程协议.md)
- [服务注册与消息路由](tech/服务注册与消息路由.md)
- [项目优化建议](tech/项目优化建议-2026-04-28.md)

### 设计文档
- [战斗系统设计](design/combat/战斗系统设计.md)
- [移动系统设计](design/movement/移动系统设计.md)
- [实体系统组件化重构](design/entity/实体系统组件化重构.md)
- [未开发功能清单](design/pending-features.md)

### 文档索引
- [设计文档目录](design/)
- [技术文档目录](tech/)
- [开发日报](daily/)
- [分析报告](reports/)
- [参考资料](reference/)
