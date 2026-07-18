using System;
using UnityEngine;
using UnityClientSharp.Entity;

namespace UnityClientSharp.DebugPanel
{
    /// <summary>
    /// DebugPanel 组件控件接口（UGUI 版，移植自 Godot 的 IEntityTabComponent）。
    /// 每个 IComponentData 对应一个 IEntityTabComponent 控件，负责把 Profile 配置数据
    /// 渲染成 UGUI 并回写。
    /// 注意：本接口不含 SyncFromEntity —— UI 必须始终代表 Profile 配置数据，禁止用实体
    /// 运行时状态（血量/施法进度等）覆盖配置（参见 AGENTS 记忆 79121038）。
    /// </summary>
    public interface IEntityTabComponent
    {
        /// <summary>组件名，须与 ComponentRegistry 注册名、EntityProfile 数据键一致。</summary>
        string ComponentName { get; }

        /// <summary>UI 显示名（中文）。</summary>
        string DisplayName { get; }

        /// <summary>对应的 IComponentData 类型。</summary>
        System.Type DataType { get; }

        /// <summary>构建 UI，挂载到 parent（调用方应传入 RectTransform）。</summary>
        void BuildUI(Transform parent);

        /// <summary>用配置数据填充 UI（不触发保存，内部用 _suppress 防止回环）。</summary>
        void SyncFromData(IComponentData data);

        /// <summary>把当前 UI 状态写回数据对象并返回。</summary>
        IComponentData SyncToData();

        /// <summary>连接变更信号：UI 改动时回调 onChanged（用于触发保存/刷新）。</summary>
        void ConnectSignals(System.Action onChanged);

        /// <summary>断开变更信号。</summary>
        void DisconnectSignals();

        /// <summary>锁定某个属性（只读）；propName 为 null 表示全部。</summary>
        void SetPropertyLocked(string propName, bool locked);

        /// <summary>折叠/展开该组件行。</summary>
        void SetCollapsed(bool collapsed);

        /// <summary>释放资源（销毁临时 GameObject、断开信号等）。</summary>
        void Dispose();
    }
}
