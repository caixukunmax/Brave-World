using Godot;

namespace ClinetCSharp
{
    /// <summary>
    /// NPC 实体 - 蓝色系主题，渲染在地图格子上
    /// </summary>
    public partial class Npc : EntityBase
    {
        private int _gridSize = 111;
        private ulong _instanceId;

        public override int GridSize => _gridSize;

        public ulong InstanceId => _instanceId;
        public int NpcType { get; private set; }
        public int GridX { get; private set; }
        public int GridY { get; private set; }
        public Vector2I GridPos => new Vector2I(GridX, GridY);
        public string NpcName { get; private set; } = "";

        public void Setup(ulong instanceId, string name, int npcType, int x, int y, int gridSize)
        {
            _instanceId = instanceId;
            NpcName = name;
            NpcType = npcType;
            GridX = x;
            GridY = y;
            _gridSize = gridSize;
            Position = UiUtils.GridToWorld(x, y, _gridSize);

            // 蓝色系主题
            BorderColor = new Color(0.3f, 0.5f, 0.9f);
            BgColor = new Color(0.2f, 0.4f, 0.8f);
            BgOpacity = 0.9f;
            TextColor = new Color(0.95f, 0.97f, 1.0f);
            CornerRadius = 12f;

            // NPC 默认隐藏血条/MP条（被打时显示）
            HealthBarVisible = false;
            MpBarVisible = false;

            // 默认文字
            LabelTexts[0] = name;
            LabelTexts[1] = (global::ClinetCSharp.NpcType)npcType == global::ClinetCSharp.NpcType.JobMaster ? "转职大师" : "NPC";
            LabelTexts[2] = "";
            LabelTexts[3] = "";

            QueueRedraw();
        }

        public override void _Draw()
        {
            var drawSize = VisualSize;
            if (drawSize < 10) drawSize = 10;

            EntityDrawUtils.DrawBody(this, drawSize, BgColor, BgOpacity, BorderColor, BorderWidth, CornerRadius);
            DrawBars();  // NPC 血条默认不可见，但基类方法统一处理
            DrawLabels();
        }

        public bool HitTest(Vector2 worldPos)
        {
            float half = _gridSize / 2.0f;
            var worldCenter = UiUtils.GridToWorld(GridX, GridY, _gridSize);
            return Mathf.Abs(worldPos.X - worldCenter.X) < half &&
                   Mathf.Abs(worldPos.Y - worldCenter.Y) < half;
        }

        // ========== NPC 特有 ==========

        public void SetGridSize(int size)
        {
            _gridSize = size;
            // VisualSize/BorderWidth 等都是计算属性，自动跟随 GridSize
            Position = UiUtils.GridToWorld(GridX, GridY, _gridSize);
            QueueRedraw();
        }
    }
}