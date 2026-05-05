using Godot;

namespace ClinetCSharp.RenderComponents
{
    /// <summary>
    /// 客户端渲染组件接口 — EntityBase._Draw 按 DrawOrder 顺序调用
    /// DrawOrder 约定：
    ///   0   = 外观（AppearanceComponent / DrawBody）
    ///   100 = 血条/MP条（HealthBarComponent, MpBarComponent）
    ///   200 = 施法条/动作栏（CastBarComponent, ActionBarComponent）
    ///   300 = 标签（LabelComponent, RichLabelComponent）
    ///   400 = 等级徽章（LevelBadgeComponent）
    /// </summary>
    public interface IRenderComponent
    {
        /// <summary>绘制顺序，小值先画（底层），大值后画（顶层）</summary>
        int DrawOrder { get; }

        /// <summary>组件附加到实体时调用</summary>
        void OnAttach(EntityBase entity);

        /// <summary>组件从实体移除时调用</summary>
        void OnDetach(EntityBase entity);

        /// <summary>在 EntityBase._Draw 中按 DrawOrder 顺序调用</summary>
        void Draw();
    }
}
