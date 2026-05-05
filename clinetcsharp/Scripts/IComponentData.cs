using Godot;

namespace ClinetCSharp
{
    /// <summary>
    /// 组件数据标记接口 — 所有组件数据类必须实现
    /// </summary>
    public interface IComponentData
    {
        IComponentData Clone();
    }
}
