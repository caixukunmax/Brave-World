using Godot;
using System.Collections.Generic;

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

        private Sprite2D _sprite;

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

            SetupIcon();
        }

        private void SetupIcon()
        {
            _sprite = new Sprite2D();
            _sprite.Texture = ItemIconCatalog.GetIcon((uint)ItemId);
            // 让图标大小与原来的品质方块大致匹配
            float textureSize = _sprite.Texture.GetSize().X;
            float scale = _boxSize / textureSize;
            _sprite.Scale = new Vector2(scale, scale);
            AddChild(_sprite);
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

            // 品质边框
            var quality = GetNodeInventoryQuality();
            var color = ItemIconCatalog.GetQualityColor(quality);
            float half = _boxSize / 2f;
            var rect = new Rect2(center.X - half, center.Y - half, _boxSize, _boxSize);
            DrawRect(rect, color, false, 1.5f);

            // 同步子节点 Sprite2D 的浮动偏移
            if (_sprite != null)
                _sprite.Position = center;

            // 数量文本（>1 时显示）
            if (Count > 1)
            {
                DrawString(ThemeDB.FallbackFont, center + new Vector2(-6, 4),
                    $"x{Count}", HorizontalAlignment.Center, -1, 11, Colors.White);
            }
        }

        private int GetNodeInventoryQuality()
        {
            var tree = Engine.GetMainLoop() as SceneTree;
            var node = tree?.GetFirstNodeInGroup("inventory_manager");
            if (node is InventoryManager mgr)
                return mgr.GetItemQuality((uint)ItemId);
            return 0;
        }
    }
}
