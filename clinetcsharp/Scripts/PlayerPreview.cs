using Godot;

namespace ClinetCSharp
{
    /// <summary>
    /// 玩家预览专用实体 — 只负责渲染，不参与网络同步、输入处理或游戏逻辑。
    /// 用于调试面板的模板预览，确保和地图上 Player 的渲染效果完全一致。
    /// </summary>
    // [Tool]：编辑器插件「实体配置」面板也用本类渲染 player 类型 Profile 的预览。
    // _Ready 只做渲染初始化（渲染组件 + 标签节点），不触碰网络/配置 I/O；_Process/_Input 已是空覆写。
    [Tool]
    public partial class PlayerPreview : Player
    {
        public override void _Ready()
        {
            // 匹配 Player._Ready() 的渲染初始化流程，确保预览与地图效果一致
            // 不做：LoadStyleConfig（预览应展示 Profile 默认值）、网络同步、Position 设置
            // ProfileId 由外部 ApplyProfile() 在 _Ready 之前设置，不在此处覆盖
            EnsureRenderComponents();

            for (int i = 0; i < LabelCount; i++)
                _labelOffsets[i] = DefaultOffsets[i];

            // 与 Player._Ready() 一致：立即创建标签节点，异步完成布局后 ApplyProfile
            // SetupLabels() 是 private，改用 public RefreshLabels()，效果等价
            RefreshLabels();
            QueueRedraw();
        }

        public override void _Process(double delta) { }
        public override void _Input(InputEvent @event) { }
    }
}
