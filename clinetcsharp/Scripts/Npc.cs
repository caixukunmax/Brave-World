using Godot;
using ClinetCSharp.RenderComponents;

namespace ClinetCSharp
{
    /// <summary>
    /// NPC 实体 - 蓝色系主题，渲染在地图格子上
    /// </summary>
    // [Tool]：编辑器插件「实体配置」面板用真实 Npc 渲染 npc 类型 Profile 的预览。
    // 本类没有覆写 _Ready/_Process，编辑器下不会跑任何逻辑，只有 _Draw 会执行；
    // 输入已被预览侧 SetProcessInput(false) 关闭。
    [Tool]
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
        /// <summary>UI配置ID，指定使用哪个 EntityStyleConfig</summary>
        public int UiConfigId { get; private set; }
        protected override Vector2I GetGridPos() => new Vector2I(_gridX, _gridY);
        public int GridX => _gridX;
        public int GridY => _gridY;
        public string NpcName { get; private set; } = "";

        public void Setup(ulong instanceId, string name, int npcType, int x, int y, int gridSize, int uiConfigId = 1, int sizeX = 1, int sizeY = 1)
        {
            AddToGroup("npc");
            _instanceId = instanceId;
            NpcName = name;
            NpcType = npcType;
            UiConfigId = uiConfigId;
            _gridX = x;
            _gridY = y;
            _gridSize = gridSize;
            GridSizeX = sizeX > 0 ? sizeX : 1;
            GridSizeY = sizeY > 0 ? sizeY : 1;
            Position = GetWorldPositionForGridPos(new Vector2I(x, y));

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

            // 添加渲染组件（只添加一次）
            EnsureRenderComponents();

            // 使用 RichTextLabel 控件替代 DrawString
            SetupRichLabels();

            QueueRedraw();
        }

        public override void _Input(InputEvent @event)
        {
            CheckEntityClick(@event);
        }

        public override void _Draw()
        {
            // 渲染组件模式：按 DrawOrder 顺序遍历
            foreach (var comp in _renderComponents)
                comp.Draw();
            EntityDrawUtils.DrawDirectionArrow(this);
        }

        /// <summary>确保渲染组件已添加（按类型幂等补齐；禁止 Count>0 早退，原因见 AGENTS.md）</summary>
        private void EnsureRenderComponents()
        {
            if (GetRenderComponent<RenderComponents.AppearanceComponent>() == null)
                AddRenderComponent(new RenderComponents.AppearanceComponent());
            if (GetRenderComponent<RenderComponents.HealthBarComponent>() == null)
                AddRenderComponent(new RenderComponents.HealthBarComponent());
            if (GetRenderComponent<RenderComponents.MpBarComponent>() == null)
                AddRenderComponent(new RenderComponents.MpBarComponent());
            if (GetRenderComponent<RenderComponents.DebugOverlayComponent>() == null)
                AddRenderComponent(new RenderComponents.DebugOverlayComponent());
            // 不再使用 LabelComponent — 改用 RichTextLabel 控件
        }
    }
}
