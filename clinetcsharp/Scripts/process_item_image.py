#!/usr/bin/env python3
"""
道具图标标准化处理脚本。

用法:
    python clinetcsharp/scripts/process_item_image.py <input> <item_id> <english_name>

示例:
    python clinetcsharp/scripts/process_item_image.py xianbei_raw.png 1001 xianbei

输出:
    clinetcsharp/assets/items/item_1001_xianbei.png
"""

import argparse
import sys
from pathlib import Path

from PIL import Image

TARGET_SIZE = 128
ASSETS_DIR = Path(__file__).resolve().parent.parent / "assets" / "items"


def parse_args():
    parser = argparse.ArgumentParser(description="Process AI-generated item icon for Godot.")
    parser.add_argument("input", help="Path to input PNG image")
    parser.add_argument("item_id", help="Item ID from config table")
    parser.add_argument("english_name", help="English item name for filename")
    parser.add_argument("--output-dir", default=str(ASSETS_DIR), help="Output directory")
    parser.add_argument("--size", type=int, default=TARGET_SIZE, help="Target size")
    return parser.parse_args()


def ensure_rgba(img: Image.Image) -> Image.Image:
    """确保图片为 RGBA 模式。"""
    if img.mode != "RGBA":
        return img.convert("RGBA")
    return img


def is_solid_color_background(pixel, bg_color, tolerance: int = 15) -> bool:
    """判断像素是否接近给定的纯色背景。"""
    return sum(abs(pixel[i] - bg_color[i]) for i in range(3)) <= tolerance


def remove_solid_background(img: Image.Image, tolerance: int = 15) -> Image.Image:
    """
    若图片背景为接近纯色的纯色背景，则将其变为透明。
    仅对四角颜色一致且接近的情况生效。
    """
    width, height = img.size
    corners = [
        img.getpixel((0, 0)),
        img.getpixel((width - 1, 0)),
        img.getpixel((0, height - 1)),
        img.getpixel((width - 1, height - 1)),
    ]

    base = corners[0]
    if all(is_solid_color_background(c, base, tolerance) for c in corners):
        datas = list(img.getdata())
        new_data = []
        for item in datas:
            if is_solid_color_background(item, base, tolerance):
                new_data.append((0, 0, 0, 0))
            else:
                new_data.append(item)
        img.putdata(new_data)
    return img


def is_checkerboard_background(pixel) -> bool:
    """检测常见的 AI 生成图假棋盘格背景：接近纯白或浅灰。"""
    r, g, b, a = pixel
    if a < 10:
        return True
    # 接近纯白
    if r > 245 and g > 245 and b > 245:
        return True
    # 浅灰棋盘格（常见值 220~235）
    if abs(r - g) < 15 and abs(g - b) < 15 and 210 <= r <= 245:
        return True
    return False


def remove_checkerboard_background(img: Image.Image) -> Image.Image:
    """将棋盘格/灰白格假透明背景替换为真正透明。"""
    datas = list(img.getdata())
    new_data = []
    for pixel in datas:
        if is_checkerboard_background(pixel):
            new_data.append((0, 0, 0, 0))
        else:
            new_data.append(pixel)
    img.putdata(new_data)
    return img


def remove_background(img: Image.Image) -> Image.Image:
    """统一入口：先尝试去除纯色背景，再去除棋盘格背景。"""
    img = remove_solid_background(img)
    # 若去除后仍有大量不透明像素接近四角颜色，尝试棋盘格去除
    alpha = img.split()[-1]
    opaque_count = sum(1 for p in alpha.getdata() if p > 10)
    total = img.width * img.height
    if opaque_count > total * 0.9:
        img = remove_checkerboard_background(img)
    return img


def crop_to_content(img: Image.Image, padding_ratio: float = 0.05) -> Image.Image:
    """按内容包围盒裁剪并居中为正方形。"""
    alpha = img.split()[-1]
    bbox = alpha.getbbox()
    if not bbox:
        return img

    left, top, right, bottom = bbox
    width = right - left
    height = bottom - top
    max_dim = max(width, height)
    padding = int(max_dim * padding_ratio)

    center_x = (left + right) // 2
    center_y = (top + bottom) // 2
    half = max_dim // 2 + padding

    new_left = max(0, center_x - half)
    new_top = max(0, center_y - half)
    new_right = min(img.width, center_x + half)
    new_bottom = min(img.height, center_y + half)

    # 确保正方形
    size = min(new_right - new_left, new_bottom - new_top)
    new_right = new_left + size
    new_bottom = new_top + size

    return img.crop((new_left, new_top, new_right, new_bottom))


def resize_centered(img: Image.Image, size: int) -> Image.Image:
    """等比缩放图片并居中放置到目标尺寸的透明画布上。"""
    img = img.copy()
    img.thumbnail((size, size), Image.Resampling.NEAREST)

    canvas = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    x = (size - img.width) // 2
    y = (size - img.height) // 2
    canvas.paste(img, (x, y), img)
    return canvas


def check_watermark(img: Image.Image) -> bool:
    """简单水印检测：检查右下角区域是否有不透明像素聚集。"""
    width, height = img.size
    # 取右下角 32x32 区域
    right_bottom = img.crop((width - 32, height - 32, width, height))
    alpha = right_bottom.split()[-1]
    opaque_ratio = sum(1 for p in alpha.getdata() if p > 30) / (32 * 32)
    # 若右下角不透明像素占比高，可能含水印
    return opaque_ratio > 0.6


def process_image(img: Image.Image, size: int) -> Image.Image:
    """完整处理流程：去背景 → 裁剪内容 → 缩放居中。"""
    img = ensure_rgba(img)
    img = remove_background(img)
    img = crop_to_content(img)
    img = resize_centered(img, size)
    return img


def main():
    args = parse_args()
    input_path = Path(args.input)
    output_dir = Path(args.output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)

    if not input_path.exists():
        print(f"[ERROR] Input file not found: {input_path}", file=sys.stderr)
        sys.exit(1)

    if input_path.suffix.lower() != ".png":
        print(f"[WARN] Input is not PNG ({input_path.suffix}); converting may lose quality.", file=sys.stderr)

    item_id = args.item_id
    english_name = args.english_name.lower().replace(" ", "_")
    output_path = output_dir / f"item_{item_id}_{english_name}.png"

    img = Image.open(input_path)
    img = process_image(img, args.size)

    has_watermark = check_watermark(img)
    if has_watermark:
        print("[WARN] Possible watermark detected in bottom-right corner. Please regenerate without watermark.", file=sys.stderr)

    img.save(output_path, "PNG")

    print(f"[OK] Saved: {output_path}")
    print(f"    Size: {img.size}")
    print(f"    Mode: {img.mode}")
    print(f"    Watermark warning: {'yes' if has_watermark else 'no'}")
    print(f"    Next steps:")
    print(f"      1. Fill 'icon' field in tables/datas/item/#Item-道具系统表.xlsx with: item_{item_id}_{english_name}.png")
    print(f"      2. Re-export Luban tables.")
    print(f"      3. Open Godot editor or run 'godot --headless --import --path clinetcsharp' to generate .import files.")


if __name__ == "__main__":
    main()
