# Editor Remote — Godot 编辑器远程控制插件

在 Godot 编辑器内启动 HTTP 服务器，允许外部 AI 工具（Claude Code 等）通过 REST API 远程操控编辑器。

## 功能

- **场景操作**：获取节点树、打开/保存场景、增删改节点
- **属性操作**：读取/设置节点属性，支持 Vector2/3、Color 等 Godot 类型
- **运行控制**：Play / Stop / 运行指定场景 / 截图
- **文件系统**：列出目录、读写文件
- **资源操作**：加载/保存 `.tres` 资源

## 启用插件

1. 打开 Godot 编辑器 → 项目 → 项目设置 → 插件
2. 找到 "Editor Remote (AI HTTP API)"，点击启用
3. 编辑器底部会出现 "Editor Remote" 面板，显示服务器状态

默认端口：`7788`

## API 概览

访问 `http://localhost:7788/api/editor/info` 查看完整 API 列表。

### 快速测试

```bash
curl http://localhost:7788/api/editor/info
curl http://localhost:7788/api/scene/current
curl -X POST http://localhost:7788/api/play/start
```

### 使用 CLI

```bash
node scripts/editor-remote/editor-remote.mjs info
node scripts/editor-remote/editor-remote.mjs scene tree
node scripts/editor-remote/editor-remote.mjs play screenshot out.png
```

## 安全说明

⚠️ **此插件仅用于本地开发，不要暴露到公网。**
- 默认只监听 `localhost`，外部无法访问
- 无认证机制（本地使用场景下不需要）
- 文件写入 API 可以覆盖项目内任意文件，谨慎使用
