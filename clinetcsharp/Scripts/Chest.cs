using Godot;
using System.Collections.Generic;
using Protocol;

namespace ClinetCSharp
{
    /// <summary>
    /// 宝箱实体 - 渲染在地图格子上，支持开箱交互
    /// </summary>
    public partial class Chest : Node2D
    {
        private int _gridSize = 111;
        private bool _opened = false;
        private uint _chestId;
        private float _boxSize;

        public uint ChestId => _chestId;
        public int GridX { get; private set; }
        public int GridY { get; private set; }
        public bool Opened => _opened;

        public void Setup(uint chestId, int x, int y, bool opened, int gridSize)
        {
            _chestId = chestId;
            GridX = x;
            GridY = y;
            _opened = opened;
            _gridSize = gridSize;
            _boxSize = gridSize * 0.6f;
            Position = UiUtils.GridToWorld(x, y, _gridSize);
            QueueRedraw();
        }

        public override void _Draw()
        {
            float half = _boxSize / 2.0f;

            if (_opened)
            {
                // 已开：虚线框
                var rect = new Rect2(-half, -half, _boxSize, _boxSize);
                DrawRect(rect, new Color(1, 1, 1, 0.15f));
                DrawRect(rect, new Color(1, 1, 1, 0.3f), false, 1.0f);
                // "空" 标记
                DrawString(ThemeDB.FallbackFont, new Vector2(-8, 4),
                    "空", HorizontalAlignment.Center, -1, 12, new Color(1, 1, 1, 0.4f));
            }
            else
            {
                // 未开：宝箱图标（黑白风格）
                var bodyRect = new Rect2(-half, -half * 0.4f, _boxSize, _boxSize * 0.7f);
                var lidRect = new Rect2(-half, -half, _boxSize, _boxSize * 0.5f);

                // 箱体
                DrawRect(bodyRect, new Color(0.2f, 0.2f, 0.2f));
                DrawRect(bodyRect, Colors.White, false, 2.0f);

                // 箱盖
                DrawRect(lidRect, new Color(0.3f, 0.3f, 0.3f));
                DrawRect(lidRect, Colors.White, false, 2.0f);

                // 锁扣（中心小方块）
                float lockSize = _boxSize * 0.15f;
                var lockRect = new Rect2(-lockSize / 2, -half * 0.1f, lockSize, lockSize);
                DrawRect(lockRect, Colors.White);

                // 金属横条
                float bandY = -half * 0.4f;
                DrawLine(new Vector2(-half, bandY), new Vector2(half, bandY), Colors.White, 1.5f);
            }
        }

        public void MarkOpened()
        {
            _opened = true;
            QueueFree();
        }

        /// <summary>
        /// 检查点击是否命中宝箱区域
        /// </summary>
        public bool HitTest(Vector2 worldPos)
        {
            float half = _gridSize / 2.0f;
            var worldCenter = UiUtils.GridToWorld(GridX, GridY, _gridSize);
            return Mathf.Abs(worldPos.X - worldCenter.X) < half &&
                   Mathf.Abs(worldPos.Y - worldCenter.Y) < half;
        }

    }
}
