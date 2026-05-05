using Godot;
using System.Collections.Generic;
using Protocol;

namespace ClinetCSharp
{
    /// <summary>
    /// 掉落物实体 — 渲染在地图上，支持自动拾取
    /// </summary>
    public partial class DropItem : Node2D
    {
        private int _gridSize = 111;
        private float _boxSize;
        private float _time; // 动画时间

        public long DropId { get; private set; }
        public int ItemId { get; private set; }
        public int Count { get; private set; }
        public int GridX { get; private set; }
        public int GridY { get; private set; }

        // 品质颜色
        private static readonly Color CommonColor = new(1f, 1f, 1f);       // 白
        private static readonly Color UncommonColor = new(0.1f, 1f, 0.1f); // 绿
        private static readonly Color RareColor = new(0.1f, 0.5f, 1f);     // 蓝
        private static readonly Color EpicColor = new(0.6f, 0.2f, 1f);     // 紫
        private static readonly Color LegendaryColor = new(1f, 0.5f, 0f);  // 橙

        public void Setup(long dropId, int itemId, int count, int x, int y, int gridSize)
        {
            DropId = dropId;
            ItemId = itemId;
            Count = count;
            GridX = x;
            GridY = y;
            _gridSize = gridSize;
            _boxSize = gridSize * 0.4f;
            Position = UiUtils.GridToWorld(x, y, _gridSize);
        }

        public override void _Process(double delta)
        {
            _time += (float)delta;
            QueueRedraw();
        }

        public override void _Draw()
        {
            // 浮动动画
            float floatOffset = Mathf.Sin(_time * 3f) * 3f;
            var center = new Vector2(0, floatOffset);

            // 底部光晕
            DrawCircle(center, _boxSize * 0.6f, new Color(1, 1, 1, 0.15f));

            // 品质颜色方块
            var color = GetQualityColor(ItemId);
            float half = _boxSize / 2f;
            var rect = new Rect2(center.X - half, center.Y - half, _boxSize, _boxSize);
            DrawRect(rect, color);
            DrawRect(rect, Colors.White, false, 1.5f);

            // 数量文本（>1 时显示）
            if (Count > 1)
            {
                DrawString(ThemeDB.FallbackFont, center + new Vector2(-6, 4),
                    $"x{Count}", HorizontalAlignment.Center, -1, 11, Colors.White);
            }
        }

        private static Color GetQualityColor(int itemId)
        {
            // 简单品质判断：按 ID 范围
            if (itemId >= 2000) return LegendaryColor;  // 传说（武器）
            if (itemId >= 1002) return UncommonColor;    // 优秀（附魔石）
            return CommonColor;                          // 普通（药水）
        }
    }
}
