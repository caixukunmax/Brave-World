using System;
using Godot;

namespace ClinetCSharp.Cutscene
{
    /// <summary>
    /// 镜头机位复合编码：把 (格子x, 格子y, 缩放) 压成一个正整数，便于在
    /// 编辑器里"预览右上角复制 → 直接粘贴进 cue 镜头值"的整数字段，免去分别填 x/y/缩放。
    ///
    /// 比特布局（从低位到高位）：
    ///   [0..8]   zoom  ×100 的整数（0..511 → zoom 0..5.11，覆盖编辑器的 0.2..4.0）
    ///   [9..19]  x + 1024 偏移（0..2047 → 格子 x ∈ [-1024, 1023]）
    ///   [20..30] y + 1024 偏移（0..2047 → 格子 y ∈ [-1024, 1023]）
    /// 编码结果恒为非负 int（最大约 2.147e9，不超 int32），可直接作为整数字符串复制粘贴。
    /// </summary>
    public static class CutsceneCameraCode
    {
        private const int ZOOM_BITS = 9;
        private const int COORD_BITS = 11;
        private const int COORD_OFFSET = 1024;
        private const int ZOOM_SCALE = 100;

        private const int ZOOM_MASK = (1 << ZOOM_BITS) - 1;
        private const int COORD_MASK = (1 << COORD_BITS) - 1;

        /// <summary>编码 (x, y, zoom) → 复合正整数。越界坐标/缩放会被夹紧。</summary>
        public static int Encode(int x, int y, float zoom)
        {
            int zf = Math.Clamp((int)Math.Round(zoom * ZOOM_SCALE), 0, ZOOM_MASK);
            int xf = Math.Clamp(x + COORD_OFFSET, 0, COORD_MASK);
            int yf = Math.Clamp(y + COORD_OFFSET, 0, COORD_MASK);
            return (yf << (COORD_BITS + ZOOM_BITS)) | (xf << ZOOM_BITS) | zf;
        }

        /// <summary>解码复合正整数 → (x, y, zoom)。zoom 保留 0.01 精度。</summary>
        public static void Decode(int value, out int x, out int y, out float zoom)
        {
            int zf = value & ZOOM_MASK;
            int xf = (value >> ZOOM_BITS) & COORD_MASK;
            int yf = (value >> (COORD_BITS + ZOOM_BITS)) & COORD_MASK;
            x = xf - COORD_OFFSET;
            y = yf - COORD_OFFSET;
            zoom = zf / (float)ZOOM_SCALE;
        }

        /// <summary>从 Godot Camera2D 当前机位编码（格子坐标 + 缩放）。</summary>
        public static int EncodeFromCamera(Camera2D cam, float gridSize)
        {
            var cell = new Vector2I(
                Mathf.FloorToInt(cam.GlobalPosition.X / gridSize),
                Mathf.FloorToInt(cam.GlobalPosition.Y / gridSize));
            return Encode(cell.X, cell.Y, cam.Zoom.X);
        }
    }
}
