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
        private int _gridX;
        private int _gridY;

        protected override int GetGridSize() => _gridSize;
        protected override void SetGridSizeValue(int value) => _gridSize = value;

        public ulong InstanceId => _instanceId;
        public int NpcType { get; private set; }
        protected override Vector2I GetGridPos() => new Vector2I(_gridX, _gridY);
        public int GridX => _gridX;
        public int GridY => _gridY;
        public string NpcName { get; private set; } = "";

        public void Setup(ulong instanceId, string name, int npcType, int x, int y, int gridSize)
        {
            _instanceId = instanceId;
            NpcName = name;
            NpcType = npcType;
            _gridX = x;
            _gridY = y;
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
    }
}
