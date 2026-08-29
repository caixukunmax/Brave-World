# 地图配置管理 Godot 插件 设计文档

- 日期：2026-08-28
- 状态：待确认（确认后进入实现）
- 目标读者：客户端（Godot）开发者

## 1. 背景与目标

现状：地图的可视化编辑（画格子、摆东西）由**运行时** `MapEditor`（游戏内按 E 进入）完成。地图的**配置/元数据**（有哪些地图、尺寸、出生点、显示名）目前只能靠手改 JSON 或跑构建脚本维护，缺一个顺手的管理入口。

本插件目标：**做一个 Godot 编辑器插件，只负责“管理地图相关配置”**——地图清单 + 每张图的元数据，并在改完后一键同步到各端。

## 2. 明确的范围边界（非目标）

- **不做**可视化逐格编辑（那是运行时 `MapEditor` 的职责，插件不重做）。
- **不管理**逐格 terrain/height 内容（除“改尺寸时自动补齐/裁剪默认格”这一必要联动，见 §5.2）。
- **不管理**地形定义表（terrain_config，由 Luban 生成）。
- **不管理**装饰物/怪物/NPC/宝箱等摆放实体配置。
- 插件只碰：地图清单 + `map.json` 的元数据字段 + 同步。

## 3. 现状（已核对）

- 数据源（唯一真相）：`tables/datas/maps/<地图名>/map.json`。
- `map.json` 结构：
  ```json
  {
    "bounds": { "x": 0, "y": 0, "w": 50, "h": 50 },
    "cells":  { "x_y": { "terrain": 0, "height": 0, "custom": "" }, ... },
    "display_name": "新手村",
    "spawn": { "x": 25, "y": 25 },
    "version": 3
  }
  ```
- 同步：`npm run sync:maps`（`tables/scripts/sync-maps.ts`）扫描源目录，部署到 `servercsharp/data/maps/`、`clinetcsharp/maps/`、服务器 bin 运行目录，并生成 `servercsharp/data/map_registry.json`（字段 `map_name/display_name/width/height/spawn_x/spawn_y`）。
- 校验：`npm run verify:maps`（`verify-maps.ts`）。
- 路径统一由 `paths.json` 的 `maps` 段定义（source_dir / client_output_dir / cs_output_dir / cs_registry_path）。
- ⚠ 已知坑：`sync-maps.ts` 会“优先用地图中的出生点建筑（类型 60000）推导 spawn”。若某图存在 60000 建筑，插件里手改的 `spawn` 可能被同步覆盖。

## 4. 插件形态

- Godot `EditorPlugin`，注册为一个 **Dock 面板**（底部或右侧，可停靠），命名如“地图配置管理”。
- 面板内容：
  - 左侧：地图列表（扫描 `tables/datas/maps/` 得到），每项显示 `地图名 (display_name)`。
  - 右侧：选中地图的元数据表单 + 操作按钮。
- 技术选型见 §7 决策点 D1（GDScript vs C#）。

## 5. 功能清单

### 5.1 查看/编辑元数据
选中一张图，读取其 `map.json`，展示并可编辑：
- `display_name`（文本）
- `width` / `height`（整数，对应 `bounds.w/h`）
- `spawn.x` / `spawn.y`（整数）
- `version`（只读展示，不随意改）

“保存”→ 写回**源** `map.json`（UTF-8，保持其余字段与 cells 不变，仅改上述字段）。

### 5.2 改尺寸的联动（关键）
`bounds` 与 `cells` 必须自洽。改 w/h 时插件自动 resize `cells`：
- 变大：新增区域补默认格 `{terrain:0,height:0,custom:""}`。
- 变小：删除越界 cell。
- 已有格的 terrain/height/custom **保持不变**（只动新增/越界部分）。
UI 上明确提示“调整尺寸会补齐/裁剪边缘格（默认值），不会改动已有格子内容”。

### 5.3 新建地图
输入地图名 + 宽 + 高 → 在 `tables/datas/maps/<名>/` 生成脚手架 `map.json`：`bounds`、`spawn` 默认取中心、`display_name` 默认=地图名、`version` 取当前常见值（如 3）、`cells` 生成 w×h 全默认格。地图名需做合法性校验（非空、无路径非法字符、不与现有重名）。

### 5.4 删除地图
把 `tables/datas/maps/<名>/` **移入回收站**（遵循文件保护策略，绝不硬删）。删除前二次确认，并提示需再点“同步”才会从 client/server 与注册表移除。

### 5.5 一键同步
按钮执行 `npm run sync:maps`（工作目录=仓库根），面板内显示其 stdout/stderr 与退出码；成功后刷新地图列表。可选：同步前自动跑 `verify:maps`（见 D3）。

## 6. 写盘与一致性策略

- 插件**只写数据源** `tables/datas/maps/`，绝不直接改 client/server 副本（避免分叉）。
- 各端一致性一律通过 §5.5 同步达成（复用既有脚本，不另造同步逻辑）。
- 所有路径从 `paths.json` 解析，不硬编码目录。

## 7. 待确认决策点

- **D1 插件语言：GDScript vs C#？**
  - 推荐 **GDScript**：EditorPlugin 用 GDScript 最轻，不编译进游戏程序集、不依赖 dotnet 构建、改脚本即生效；与现有 `addons/openclaw`（GDScript 插件）一致。
  - C# 插件也可，但会把编辑器工具代码混进客户端工程、增加构建耦合。
- **D2 改尺寸自动 resize cells（§5.2）是否接受？** 还是“尺寸只读、要改尺寸请去运行时编辑器”？（推荐接受自动补齐/裁剪）
- **D3 同步按钮是否顺带跑 verify？** 还是同步、校验各一个按钮？（推荐各一个按钮，校验可选）
- **D4 出生点建筑覆盖 spawn 的坑（§3）：** 插件里遇到该图含 60000 建筑时，是“灰掉 spawn 输入并提示以建筑为准”，还是“照改但同步后可能被覆盖”？（推荐前者：检测到 60000 建筑则提示）

## 8. 改动清单（预估）

- 新增 `clinetcsharp/addons/map-config-manager/`：`plugin.cfg`、插件主脚本、面板场景/脚本。
- `project.godot`：在 `[editor_plugins]/enabled` 增加该插件。
- 不改动运行时 `MapEditor`、不改同步脚本、不改 proto/服务器。

## 9. 风险与注意

- 在插件里跑 `npm`：Windows 下用 `OS.execute` 调 `cmd /c npm`（或 `where npm` 定位），工作目录设为仓库根；node 不在 PATH 时给出清晰报错。
- 中文地图名/路径：JSON 读写统一 UTF-8；Godot `FileAccess` 默认 UTF-8，注意不要引入 BOM 破坏既有文件。
- 与运行时编辑器并存：插件改的是磁盘源文件；若游戏正在运行，需重载地图才生效（面板提示）。
- 删除走回收站，避免误删不可逆。

## 10. 范围扩展（2026-08-28 追加）：单元编辑器（独立窗口）

与用户确认后扩展：配置管理不够，需要一个**在 Godot 里编辑地图单元（格子内容）**的能力。经多轮澄清定案如下。

### 10.1 关键决策

- **不做数据模型迁移**（用户一度想”把水/草并入建筑系统”，经说明后放弃）。保留现有两字段模型：`cell.terrain`（地面：水/草/沙/岩…，带 walkable/速度/regen 等 gameplay 属性）与 `cell.decoration`（建筑：房舍/商店/井/田/酒馆/出生点/传送门，带 footprint）。二者语义不同，合并会打穿服务器移动/战斗、渲染、运行时编辑器，风险高收益零。
- **编辑器 UI 用”统一调色板”**：把地面单元 + 建筑单元都列成可选”单元”（含子类型），用户只管点选，插件背后写对字段（选水→terrain=1；选房舍→decoration=10001 且占 2×2）。满足”都是单元、有子类型”的心智模型，零迁移。
- 界面形态：**独立大窗口**（从配置面板按钮打开），不占任何 dock。

### 10.2 已核实的数据事实（实现依据）

- 多格建筑**只存锚点格**：`map.json` 中 anchor 格带 `decoration=config_id`，被 footprint 覆盖的格不带 decoration。`MapDecorationManager.SpawnDecorations` 按 Y→X 排序、先放的锚点先占格、被覆盖格跳过。
- footprint 尺寸来源：`debug_panel_config.cfg` 的 `profile_<id>.appearance` 的 `size_x/size_y`（与服务器 `servercsharp/data/buildings.json` 的 sizeX/sizeY 保持一致）。
- 建筑中文名：`profile_<id>.labels` 的 `label_0_content`；`profile_<id>` 段有 `entity_type=”decoration”` 才算建筑。
- 地形：`clinetcsharp/data/terrain_config.json`，每条 `id/name/color_r/g/b/a/walkable`。
- 出生点建筑 `decoration=60000`（BuildingType.SpawnPoint×10000）。
- 编辑器保存：只改 cells，保留 version/display_name/bounds/spawn；写源 `tables/datas/maps/<名>/map.json`，再点同步传播。

### 10.3 编辑器功能（v1）

网格画布（按 terrain 上色、建筑按 footprint 绘制+名称、出生点标记、缩放/平移）；工具：刷地面 / 放建筑（footprint 预览 + 越界/重叠校验）/ 擦建筑 / 擦地面；保存源 + 同步。与配置 Dock 分工：Dock 管清单/元数据/新建/删除/同步，窗口编辑器管格子内容。

### 10.4 验证边界

GDScript 解析/插件加载可经 headless 验证；窗口弹出、绘制、鼠标交互、放置校验等运行时行为需在编辑器内实测迭代。
