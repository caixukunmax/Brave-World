# Editor Remote Plugin — AI 远程控制 Godot 编辑器

## 目标

让 AI（Claude Code 等）可以通过 HTTP API 全自动操作 Godot 编辑器，实现：
- 自动创建/修改场景和节点
- 自动调整属性
- 自动运行游戏并截图验证
- 自动读写资源和脚本文件

## 架构

```
AI (Claude Code)
    │
    │ HTTP 请求
    ▼
Godot Editor (EditorRemotePlugin)
    │
    ├── EditorHttpServer (Node + HttpListener)
    │   └── 后台线程监听 → ConcurrentQueue → _Process 主线程调度
    │
    ├── SceneHandler       — 场景/节点操作
    ├── PropertyHandler    — 属性读写
    ├── PlaybackHandler    — 运行控制/截图
    └── ResourceHandler    — 资源/文件操作
```

### 线程模型

- **HTTP 监听**：后台线程（`HttpListener`），不阻塞编辑器
- **请求解析**：后台线程（读取 body、解析 JSON）
- **Godot API 调用**：主线程（通过 `ConcurrentQueue` + `_Process` 调度）
- **响应写入**：后台线程

## API 列表

### 场景操作
| Method | Path | 说明 |
|--------|------|------|
| GET | `/api/scene/tree` | 当前场景节点树（深度5） |
| GET | `/api/scene/current` | 当前场景信息 |
| GET | `/api/scene/open?path=...` | 打开场景 |
| POST | `/api/scene/save` | 保存当前场景 |
| POST | `/api/scene/new` | 新建空场景 |
| POST | `/api/scene/node/add` | 添加节点 `{parent_path, type, name}` |
| DELETE | `/api/scene/node/remove?path=...` | 删除节点 |
| POST | `/api/scene/node/reparent` | 重新挂接 `{path, new_parent_path}` |
| POST | `/api/scene/node/rename` | 重命名 `{path, name}` |

### 属性操作
| Method | Path | 说明 |
|--------|------|------|
| GET | `/api/node/properties?path=...&names=a,b` | 读取节点属性 |
| PUT | `/api/node/property` | 设置属性 `{path, name, value}` |
| GET | `/api/node/property_list?path=...` | 节点全部属性定义 |

### 运行控制
| Method | Path | 说明 |
|--------|------|------|
| POST | `/api/play/start` | 运行当前场景 |
| POST | `/api/play/stop` | 停止运行 |
| POST | `/api/play/start_custom` | 运行指定场景 `{scene}` |
| GET | `/api/play/status` | 运行状态 |
| GET | `/api/play/screenshot` | 截图（base64 PNG） |

### 控制台
| Method | Path | 说明 |
|--------|------|------|
| GET | `/api/console?limit=100` | 控制台输出 |
| DELETE | `/api/console` | 清空控制台 |

### 资源/文件
| Method | Path | 说明 |
|--------|------|------|
| GET | `/api/resource/load?path=...` | 加载资源信息 |
| POST | `/api/resource/save` | 保存资源 `{path}` |
| GET | `/api/filesystem/list?path=...` | 列出目录 |
| GET | `/api/filesystem/read?path=...` | 读文本文件 |
| POST | `/api/filesystem/write` | 写文本文件 `{path, content}` |

### 元数据
| Method | Path | 说明 |
|--------|------|------|
| GET | `/api/editor/info` | 编辑器信息 + 完整 API 列表 |

## 安装步骤

1. 插件文件位于 `addons/editor_remote/`
2. 打开 Godot → 项目 → 项目设置 → 插件
3. 找到 "Editor Remote (AI HTTP API)"，启用
4. 编辑器底部面板会出现 "Editor Remote" 标签页
5. 默认端口：`7788`

## 使用方式

### 方式一：curl
```bash
curl http://localhost:7788/api/editor/info
curl http://localhost:7788/api/scene/current
curl -X POST http://localhost:7788/api/play/start
```

### 方式二：CLI 工具
```bash
node scripts/editor-remote/editor-remote.mjs info
node scripts/editor-remote/editor-remote.mjs scene tree
node scripts/editor-remote/editor-remote.mjs play screenshot out.png
```

### 方式三：AI 直接调用
AI 可以通过 `Bash` 工具运行 `curl` 或 node CLI 来操作编辑器。

## 安全注意事项

- 默认只监听 `localhost`，外部网络无法访问
- 无认证机制（本地开发不需要）
- 文件写入 API 可以覆盖项目内任意文件，谨慎使用
- **永远不要把这个插件暴露到公网**

## 后续规划

- [ ] MCP Server 包装，让 Claude Code 原生支持工具调用
- [ ] 封装为 Claude Code Skill，提供自然语言操作
- [ ] 支持批量操作（一次请求多个动作）
- [ ] 支持 Undo/Redo 集成（使用 EditorUndoRedoManager）
- [ ] 支持场景 diff / 变更预览
- [ ] 支持 GDScript 代码生成和注入
