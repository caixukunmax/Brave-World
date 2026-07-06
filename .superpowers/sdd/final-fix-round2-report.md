# 地图生成器 Skill — 第二轮最终修复报告

## 修复内容

### 1. 无条件回滚会删除已存在地图目录
- **文件**: `clinetcsharp/tools/generate-map.js`
- **改动**: 在 `generateSingleMap` 中记录 `mapDir` 是否在本运行之前已存在 (`mapDirExisted`)。
  - 若目录是本运行新建的，失败时仍递归删除整个目录。
  - 若目录原本就存在，失败时仅删除本运行写入的文件：`map.json`、`map-gen-form.md`、`map-blueprint.json`，保留原目录及其他文件。
- **新增**: `rollbackMapDir(mapDir, existedBefore)` 辅助函数。

### 2. 调整模式拒绝字符串类型的 bounds
- **文件**: `clinetcsharp/tools/generate-map.js`
- **改动**: 加载基础地图后，将 `baseMapData.bounds.w/h` 强制 `Number()` 转换；默认宽高取值也使用 `Number()`。
- **结果**: `width !== baseMapData.bounds.w` 不再因字符串/数字类型不同而误报。

### 3. 强制重新生成现有地图时未更新服务端注册表
- **文件**: `clinetcsharp/tools/lib/sync.js`
- **改动**: `updateRegistry` 不再在 `map_name` 已存在时直接跳过，而是替换现有条目的 `width`、`height`、`spawn_x`、`spawn_y`、`display_name`。
- **结果**: 重复同步同一地图会更新注册表数据，而不是保留旧值。

### 4. 审批表单未填充 AI 尺寸理由 / 布局摘要
- **文件**: `clinetcsharp/tools/generate-map.js`
- **改动**:
  - 新增 `buildSizeReason()`：显式传 `--width/--height` 时输出 `User specified WxH`；否则从 `description` 关键词推断 small/medium/large；否则输出 `Default size WxH`。
  - 新增 `summarizeBlueprint()`：将 blueprint 的 regions 汇总为 `center water (medium), north snow (small)` 等简短描述，无蓝图时返回 `-`。
  - `formOptions` 中传入 `sizeReason` 与 `layoutSummary`。
- **文件**: `clinetcsharp/tools/lib/form.js` 无需修改，已支持这两个字段。

### 5. Skill 执行示例缺少 `--allow-oversize`
- **文件**: `.superpowers/superpowers-main/skills/di-tu-sheng-cheng-qi/SKILL.md`
- **改动**: 在 Execution Command 段落后新增一个显式的 oversized map 示例，包含 `--width 100 --height 100 --allow-oversize`。

### 6. `water`/`obstacle` 比例未做范围校验
- **文件**: `clinetcsharp/tools/generate-map.js`
- **改动**: 新增 `parseRatio(value, label)`，要求输入为 [0, 1] 范围内的有限数字，否则抛出清晰错误。

## 测试

- 更新 `clinetcsharp/tools/tests/cli.test.js`:
  - 调整回滚测试：验证预存在目录只删除写入文件，保留其他文件。
  - 新增字符串 bounds 调整模式测试。
  - 新增 water/obstacle 范围校验测试。
  - 新增表单 `sizeReason`/`layoutSummary` 填充测试。
  - 新增未指定尺寸时从描述推断 size reason 的测试。
- 更新 `clinetcsharp/tools/tests/sync.test.js`:
  - 新增重复同步更新注册表条目的测试。

### 测试结果

```
▶ tests 47
▶ suites 14
▶ pass 47
▶ fail 0
▶ cancelled 0
▶ skipped 0
▶ todo 0
▶ duration_ms 1212.7973
```

完整命令：`cd clinetcsharp/tools && node --test`

## 变更文件

- `clinetcsharp/tools/generate-map.js`
- `clinetcsharp/tools/lib/sync.js`
- `clinetcsharp/tools/tests/cli.test.js`
- `clinetcsharp/tools/tests/sync.test.js`
- `.superpowers/superpowers-main/skills/di-tu-sheng-cheng-qi/SKILL.md`
- `.superpowers/sdd/final-fix-round2-report.md`（本报告）

## 注意事项

- 工作树中还存在对 `.superpowers/sdd/.gitignore` 和 `.superpowers/sdd/progress.md` 的预存在修改，未包含在本次提交中，因为用户指定的修改文件列表未包含它们。
- 回滚逻辑对 `map-blueprint.json` 的处理是“若存在则删除”。当前 `generate-map.js` 并不写入 `map-blueprint.json`（仅作为 `--blueprint` 输入），因此实际无影响；保留是为了与 issue 描述一致，并兼容未来可能写入该文件的情况。
- 尺寸推断关键词同时支持中文和英文，覆盖 SKILL.md 中列出的主要关键词。
