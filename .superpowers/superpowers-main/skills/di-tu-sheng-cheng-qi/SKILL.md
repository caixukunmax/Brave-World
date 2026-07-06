---
name: di-tu-sheng-cheng-qi
description: Use when the user asks to generate a new random map or adjust an existing map for the Godot map editor in this project
---

# 地图生成器

## Overview

This skill coordinates the creation of random/AI-driven maps for the project's Godot map editor. It turns natural-language descriptions into concrete map parameters, presents an approval form, and invokes `clinetcsharp/tools/generate-map.js` to produce the final JSON.

## When to Use

- User says "生成一张地图 ..."
- User says "调整一下 XXX 地图 ..."
- User wants multiple variants of a map (`name_1`, `name_2`, ...)

## When NOT to Use

- Manual edits inside the Godot editor (use the editor UI directly).
- Migrating or fixing existing map formats (use the existing migration scripts).

## Inputs to Collect

| Field | Required | Default |
|-------|----------|---------|
| `name` | yes | - |
| `description` | no | - |
| `count` | no | 1 |
| `width` / `height` | no | inferred from description |
| `seed` | no | random |
| `style` | no | inferred or `mixed` |
| `water` | no | inferred |
| `obstacle` | no | inferred |
| `decoration` | no | inferred or `medium` |
| `force` | no | false |

## Size Inference

If user does not specify size, infer from description keywords:

- small: 房间、小村庄、密室、小岛 → 15–25
- medium: 小镇、森林、山谷、港口 → 30–45
- large: 大陆、广袤、王国、平原 → 46–50

Never exceed 50 unless the user explicitly requests a larger size.

## AI Parsing Rules

1. Parse explicit parameters first.
2. Infer missing parameters from `description`.
3. Extract spatial layout hints (e.g., "中央有湖", "北边是雪地") into a `map-blueprint.json`.
4. Detect naming conflicts and choose next available `name_x`.
5. Generate the Markdown approval form.
6. STOP and wait for user confirmation before running the tool.

## Approval Form

Always present this table before executing:

| 项目 | 值 |
|------|-----|
| 请求地图名 | ... |
| 最终保存名 | ... |
| 模式 | 生成新地图 / 调整已有地图 |
| 尺寸 | ... |
| 种子 | ... |
| 用户描述 | ... |
| AI 解析参数 | ... |
| AI 解析布局 | ... |
| 是否覆盖 | ... |
| 输出路径 | ... |

Wait for explicit "确认" or "执行". Do not proceed on vague responses.

## Execution Command

After confirmation, run:

```bash
node clinetcsharp/tools/generate-map.js --name <finalName> --description "<desc>" --width <w> --height <h> --seed <seed> --style <style> --water <water> --obstacle <obstacle> --decoration <decoration>
```

For adjustment mode, add `--adjust`.

## Post-Execution

1. Verify `clinetcsharp/maps/<name>/map.json` exists.
2. Verify `clinetcsharp/maps/<name>/map-gen-form.md` exists.
3. If `servercsharp/data/maps/` exists, verify the copy.
4. Report the final map name and paths.

## Common Mistakes

- Generating without a form when the user did not confirm.
- Overwriting an existing map without explicit `force=true` and confirmation.
- Using sizes above 50 without user approval.
- Forgetting to save the approval form with the map.
