using Godot;

namespace ClinetCSharp
{
    /// <summary>
    /// 玩家预览专用实体 — 只负责渲染，不参与网络同步、输入处理或游戏逻辑。
    /// 用于调试面板的模板预览，确保和地图上 Player 的渲染效果完全一致。
    /// </summary>
    public partial class PlayerPreview : Player
    {
        public override void _Ready()
        {
            // 完全替换 Player._Ready：只做渲染初始化，不做网络同步
            EnsureRenderComponents();
            // 注意：不设置 Position，由 PreviewMap.SetEntity 统一放置到地图中心

            for (int i = 0; i < LabelCount; i++)
                _labelOffsets[i] = DefaultOffsets[i];

            // 标签创建交给 RefreshLabels() 统一处理，避免和外部 ApplyProfile 的调用顺序冲突
            QueueRedraw();
        }

        public override void _Process(double delta) { }
        public override void _Input(InputEvent @event) { }
    }
}
