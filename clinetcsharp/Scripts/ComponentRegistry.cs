using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ClinetCSharp
{
    /// <summary>
    /// 组件注册表 — 管理所有 IEntityTabComponent 的工厂方法
    /// 静态构造中注册所有已知组件，外部通过 Create() 创建实例
    /// </summary>
    public static class ComponentRegistry
    {
        private static readonly Dictionary<string, Func<IEntityTabComponent>> _factories = new();
        private static readonly Dictionary<string, ComponentMeta> _meta = new();

        /// <summary>实体类型 → 默认组件名列表</summary>
        private static readonly Dictionary<string, string[]> _typeComponents = new()
        {
            ["player"] = new[] { "appearance", "labels", "healthbar", "mpbar", "castbar", "actionbar", "levelbadge" },
            ["monster"] = new[] { "appearance", "labels", "healthbar", "mpbar", "castbar", "actionbar", "monster_ai" },
            ["npc"] = new[] { "appearance", "labels", "healthbar", "mpbar", "npc_interact" },
            ["decoration"] = new[] { "appearance", "labels", "obstacle", "building_type" },
        };

        public static void Register(string name, Func<IEntityTabComponent> factory, string displayName = null, string category = null, string icon = null, Color? accentColor = null)
        {
            _factories[name] = factory;
            _meta[name] = new ComponentMeta
            {
                Name = name,
                DisplayName = displayName ?? name,
                Category = category ?? "其他",
                Icon = icon ?? "◆",
                AccentColor = accentColor ?? new Color(0.5f, 0.5f, 0.5f),
            };
        }

        public static IEntityTabComponent Create(string name)
        {
            if (_factories.TryGetValue(name, out var factory))
                return factory();
            return null;
        }

        public static IEnumerable<(string name, string displayName)> GetAllComponents()
        {
            return _meta.Keys.Select(k => (k, _meta[k].DisplayName));
        }

        public static IEnumerable<(string name, string displayName)> GetComponentsForType(string entityType)
        {
            if (_typeComponents.TryGetValue(entityType, out var names))
            {
                foreach (var n in names)
                    if (_meta.TryGetValue(n, out var m))
                        yield return (n, m.DisplayName);
            }
            else
            {
                // 未知类型 → 返回所有组件
                foreach (var kv in _meta)
                    yield return (kv.Key, kv.Value.DisplayName);
            }
        }

        public static ComponentMeta GetComponentMeta(string name)
        {
            return _meta.TryGetValue(name, out var m) ? m : new ComponentMeta { Name = name, DisplayName = name, Category = "其他", Icon = "◆" };
        }

        static ComponentRegistry()
        {
            Register("appearance", () => new AppearanceComponent(), "外观", "外观", "🎨", new Color(0.95f, 0.55f, 0.25f));
            Register("labels", () => new LabelGroupComponent(), "标签", "外观", "🏷️", new Color(0.90f, 0.70f, 0.30f));
            Register("healthbar", () => new BarGroupComponent("healthbar", "血条"), "血条", "状态", "❤️", new Color(0.90f, 0.25f, 0.25f));
            Register("mpbar", () => new BarGroupComponent("mpbar", "MP条"), "MP条", "状态", "💙", new Color(0.25f, 0.55f, 0.95f));
            Register("castbar", () => new CastBarComponent(), "施法条", "状态", "✨", new Color(0.70f, 0.40f, 0.95f));
            Register("actionbar", () => new ActionBarComponent(), "动作栏", "交互", "⚡", new Color(0.25f, 0.80f, 0.80f));
            Register("levelbadge", () => new LevelBadgeComponent(), "等级徽章", "状态", "⭐", new Color(0.95f, 0.85f, 0.25f));
            Register("monster_ai", () => new MonsterAiComponent(), "怪物AI", "行为", "🧠", new Color(0.55f, 0.55f, 0.55f));
            Register("npc_interact", () => new NpcInteractComponent(), "交互面板", "交互", "💬", new Color(0.35f, 0.75f, 0.65f));
            Register("obstacle", () => new ObstacleComponent(), "障碍", "行为", "🧱", new Color(0.65f, 0.55f, 0.45f));
            Register("building_type", () => new BuildingTypeComponent(), "建筑类型", "行为", "🏠", new Color(0.75f, 0.65f, 0.35f));
        }
    }

    public class ComponentMeta
    {
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string Category { get; set; }
        public string Icon { get; set; }
        public Color AccentColor { get; set; }
    }
}
