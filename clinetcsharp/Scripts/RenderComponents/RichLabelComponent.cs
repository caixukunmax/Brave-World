using Godot;

namespace ClinetCSharp.RenderComponents
{
    /// <summary>
    /// 富文本标签渲染组件（RichTextLabel 节点版，Player 用）
    /// DrawOrder = 300
    /// 
    /// 注意：RichTextLabel 是 Godot 节点，自动渲染，Draw() 是空操作。
    /// 此组件存在的意义是：统一渲染组件列表，让 Player 的 _Draw 不需要特殊处理标签。
    /// 标签的位置更新在 Player.UpdateAllLabelPositions() 中处理。
    /// </summary>
    public class RichLabelComponent : IRenderComponent
    {
        private EntityBase _entity = null!;

        public int DrawOrder => 300;

        public void OnAttach(EntityBase entity) => _entity = entity;
        public void OnDetach(EntityBase entity) => _entity = null!;

        /// <summary>
        /// RichTextLabel 是 Godot 节点，自动渲染，无需手动 Draw
        /// </summary>
        public void Draw()
        {
            // 空操作 — RichTextLabel 节点由 Godot 自动渲染
        }
    }
}
