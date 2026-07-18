using System.Collections.Generic;
using System.Linq;

namespace UnityClientSharp.Entity
{
    /// <summary>
    /// EntityProfileManager partial — Profile CRUD（供 DebugPanel EntityTab / DecorationTab 调用）。
    /// 纯数据层，不依赖运行时世界对象；结构性变更由调用方负责 SaveConfig（仓库记忆 28653953）。
    /// </summary>
    public static partial class EntityProfileManager
    {
        /// <summary>返回所有 Profile 的快照枚举。</summary>
        public static IEnumerable<EntityProfile> GetAllProfiles()
        {
            EnsureInitialized();
            return _profiles.Values.ToList();
        }

        /// <summary>分配下一个可用 Profile ID（基于当前最大 ID +1）。</summary>
        public static int AllocateNextId()
        {
            EnsureInitialized();
            return _nextId++;
        }

        /// <summary>新增/覆盖一个 Profile（按 Id）。</summary>
        public static void AddProfile(EntityProfile profile)
        {
            EnsureInitialized();
            if (profile == null) return;
            _profiles[profile.Id] = profile;
            if (profile.Id >= _nextId) _nextId = profile.Id + 1;
        }

        /// <summary>删除指定 ID 的 Profile，成功返回 true。</summary>
        public static bool RemoveProfile(int id)
        {
            EnsureInitialized();
            return _profiles.Remove(id);
        }

        /// <summary>深拷贝一个 Profile（用于「复制」功能）。</summary>
        public static EntityProfile CloneProfile(EntityProfile src, int newId)
        {
            if (src == null) return null;
            var dst = new EntityProfile
            {
                Id = newId,
                Name = src.Name + " 副本",
                EntityType = src.EntityType,
            };
            foreach (var name in src.ComponentNames)
            {
                var data = src.GetData(name);
                if (data != null) dst.SetData(name, data.Clone());
                if (src.IsComponentDisabled(name)) dst.SetComponentDisabled(name, true);
            }
            return dst;
        }
    }
}
