using System;
using System.Collections.Generic;
using System.Linq;

namespace ClinetCSharp
{
    /// <summary>
    /// 组件注册表 — 管理所有 IEntityTabComponent 的工厂方法
    /// 静态构造中注册所有已知组件，外部通过 Create() 创建实例
    /// </summary>
    public static class ComponentRegistry
    {
        private static readonly Dictionary<string, Func<IEntityTabComponent>> _factories = new();
        private static readonly Dictionary<string, string> _displayNames = new();

        /// <summary>实体类型 → 默认组件名列表</summary>
        private static readonly Dictionary<string, string[]> _typeComponents = new()
        {
            ["player"] = new[] { "appearance", "labels", "healthbar", "mpbar", "castbar", "actionbar", "levelbadge" },
            ["monster"] = new[] { "appearance", "labels", "healthbar", "mpbar", "monster_ai" },
            ["npc"] = new[] { "appearance", "labels", "healthbar", "mpbar", "npc_interact" },
        };

        public static void Register(string name, Func<IEntityTabComponent> factory, string displayName = null)
        {
            _factories[name] = factory;
            _displayNames[name] = displayName ?? name;
        }

        public static IEntityTabComponent Create(string name)
        {
            if (_factories.TryGetValue(name, out var factory))
                return factory();
            return null;
        }

        public static IEnumerable<(string name, string displayName)> GetAllComponents()
        {
            return _factories.Keys.Select(k => (k, _displayNames.GetValueOrDefault(k, k)));
        }

        public static IEnumerable<(string name, string displayName)> GetComponentsForType(string entityType)
        {
            if (_typeComponents.TryGetValue(entityType, out var names))
            {
                foreach (var n in names)
                    if (_displayNames.TryGetValue(n, out var dn))
                        yield return (n, dn);
            }
            else
            {
                // 未知类型 → 返回所有组件
                foreach (var kv in _displayNames)
                    yield return (kv.Key, kv.Value);
            }
        }

        static ComponentRegistry()
        {
            Register("appearance", () => new AppearanceComponent(), "外观");
            Register("labels", () => new LabelGroupComponent(), "标签");
            Register("healthbar", () => new BarGroupComponent("healthbar", "血条"), "血条");
            Register("mpbar", () => new BarGroupComponent("mpbar", "MP条"), "MP条");
            Register("castbar", () => new CastBarComponent(), "施法条");
            Register("actionbar", () => new ActionBarComponent(), "动作栏");
            Register("levelbadge", () => new LevelBadgeComponent(), "等级徽章");
            Register("monster_ai", () => new MonsterAiComponent(), "怪物AI");
            Register("npc_interact", () => new NpcInteractComponent(), "交互面板");
        }
    }
}
