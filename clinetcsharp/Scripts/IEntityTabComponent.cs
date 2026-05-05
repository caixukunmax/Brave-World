using Godot;
using System;

namespace ClinetCSharp
{
    /// <summary>
    /// 实体 Tab 组件接口 — 每个可折叠的配置区域实现此接口
    /// 组件负责：创建 UI、同步数据、连接信号、锁定属性
    /// </summary>
    public interface IEntityTabComponent : IDisposable
    {
        string ComponentName { get; }       // "appearance", "labels", "healthbar"
        string DisplayName { get; }         // "外观", "标签", "血条"
        Type DataType { get; }              // typeof(AppearanceData)

        void BuildUI(VBoxContainer parent);
        void SyncFromData(IComponentData data);   // 数据 → UI
        IComponentData SyncToData();              // UI → 数据
        void ConnectSignals(Action onChanged);     // 通知 Tab 数据变了
        void DisconnectSignals();
        void SyncFromEntity(EntityBase entity);    // 实体真值 → UI（服务端推送时调用）
        void SetPropertyLocked(string propertyName, bool locked);  // 锁定/解锁属性
        void SetCollapsed(bool collapsed);
    }
}
