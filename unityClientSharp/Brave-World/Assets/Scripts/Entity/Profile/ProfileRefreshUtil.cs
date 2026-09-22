using UnityEngine;

namespace UnityClientSharp.Entity
{
    /// <summary>
    /// Profile 配置运行时刷新工具（通用 API，非调试专用）：
    /// 把 EntityProfileManager 当前内存中的配置重新应用到场景内已生成的实体。
    /// 典型用法：编辑器调试面板「应用并刷新场景实体」按钮（仅 Play 模式）。
    /// 注意：装饰的占地/阻挡变更对阻挡格的登记需重新进图才完全生效。
    /// </summary>
    public static class ProfileRefreshUtil
    {
        /// <summary>刷新场景内全部在线实体（EntityVisualBase 系）与地图装饰，并同步 decoration 配置缓存。</summary>
        public static void RefreshAll()
        {
            foreach (var v in Object.FindObjectsByType<EntityVisualBase>(FindObjectsSortMode.None))
                v.RefreshFromProfile();
            foreach (var d in Object.FindObjectsByType<MapDecoration>(FindObjectsSortMode.None))
                d.RefreshFromProfile();
            DecorationConfigUtil.RefreshFromProfileManager();
        }
    }
}
