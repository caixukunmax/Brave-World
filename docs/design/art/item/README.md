# 道具图标资源制作规范

> 本文档面向策划/美术，说明如何制作和交付道具图标，确保 AI 生成的图片能直接交给程序接入游戏。

## 交付标准

| 项目 | 要求 |
|---|---|
| 格式 | PNG |
| 尺寸 | 128×128 像素（若不一致程序会自动缩放） |
| 背景 | **透明**（alpha 通道） |
| 水印 | 无 |
| 文字 | 无 |
| 风格 | 灰阶像素风，参考 `food_banana_128.png` |
| 构图 | 道具主体居中，约占画面 70%~80% |

## AI 生成提示词模板

```
一个[道具中文名]的俯视角游戏道具图标，灰阶像素艺术风格，
简单轮廓，透明背景，无水印，主体居中，128x128 像素，单色
```

示例（仙贝）：
```
一个圆形仙贝米饼的俯视角游戏道具图标，灰阶像素艺术风格，
简单轮廓，表面有细小孔洞，透明背景，无水印，主体居中，
128x128 像素，单色，酥脆质感
```

> 若使用 GPT Image、Midjourney 等海外工具，也可使用英文提示词，关键词：
> `grayscale pixel art, 2D top-down RPG item icon, transparent background, no watermark, 128x128 sprite style, monochrome`。

## 命名规范

程序会自动按以下规则命名和存放：

```
item_<道具ID>_<english_name>.png
例：item_1001_xianbei.png
```

存放目录：`clinetcsharp/assets/items/`

## 工作流

1. 策划在 `tables/datas/item/#Item-道具系统表.xlsx` 中确定道具 ID 和英文名。
2. 美术按上述提示词模板生成图片，确保透明背景、无水印。
3. 将图片发给程序。
4. 程序运行校验脚本，缩放/居中后输出到 `clinetcsharp/assets/items/`。
5. 程序在道具表 `icon` 字段填入文件名。
6. 重新导表，客户端自动读取并显示。

## 常见错误

- **灰色/白色背景**：游戏内会显示为一块色块，必须透明。
- **带水印**：会破坏图标统一性，必须去掉。
- **主体太小或贴边**：在 128×128 的小尺寸下会看不清。
- **彩色图片**：与项目整体灰阶像素风格不一致。

## 相关文件

- 提示词表：`item_image_prompts.xlsx`
- 校验脚本：`clinetcsharp/scripts/process_item_image.py`
- 图标目录：`clinetcsharp/assets/items/`
