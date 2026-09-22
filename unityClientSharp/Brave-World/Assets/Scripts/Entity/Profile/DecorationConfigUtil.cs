using System.Collections.Generic;
using UnityClientSharp.Entity;

namespace UnityClientSharp.Entity
{
    /// <summary>
    /// 建筑配置工具（移植自 Godot DecorationConfigUtil）。
    /// 纯数据缓存层：从 EntityProfileManager 同步 decoration Profile 到本地 Configs 字典，
    /// 变更时触发 ProfilesChanged，供需要建筑配置快照的模块订阅。
    /// 仅依赖数据类与静态 EntityProfileManager，无 UGUI，故置于逻辑层（Runtime）。
    /// </summary>
    public static class DecorationConfigUtil
    {
        /// <summary>ProfileId → 建筑配置快照（运行业务侧只读副本）。</summary>
        public static readonly Dictionary<int, EntityProfile> Configs = new();

        /// <summary>配置变更事件（AddProfile / 组件增删 / 停用切换后触发）。</summary>
        public static System.Action ProfilesChanged;

        /// <summary>从 EntityProfileManager 重新同步所有 decoration Profile 到 Configs 并广播变更。</summary>
        public static void RefreshFromProfileManager()
        {
            Configs.Clear();
            foreach (var p in EntityProfileManager.GetProfilesByType("decoration"))
                Configs[p.Id] = p;
            ProfilesChanged?.Invoke();
        }

        /// <summary>取某 ProfileId 的建筑配置（无则返回 null）。</summary>
        public static EntityProfile GetConfig(int profileId)
            => Configs.TryGetValue(profileId, out var c) ? c : null;
    }
}
