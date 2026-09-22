using System;
using System.Collections.Generic;
using UnityClientSharp.Entity;
using UnityEditor;

namespace BraveWorld.Editor
{
    /// <summary>
    /// 编辑器侧组件注册表：组件名 → 显示名 / 默认数据工厂 / IMGUI 绘制器，外加各实体类型白名单。
    /// 白名单与已删除的运行时 UGUI 版 ComponentRegistry 一致。
    /// </summary>
    public static class EditorComponentRegistry
    {
        public class Entry
        {
            public string Name;
            public string DisplayName;
            public Func<IComponentData> CreateDefault;
            public Action<IComponentData> Draw;
        }

        private static readonly Dictionary<string, Entry> _entries = new()
        {
            ["appearance"] = new Entry
            {
                Name = "appearance", DisplayName = "外观",
                CreateDefault = () => new AppearanceData(),
                Draw = d => ComponentInspectors.Appearance((AppearanceData)d),
            },
            ["labels"] = new Entry
            {
                Name = "labels", DisplayName = "标签",
                CreateDefault = () => new LabelGroupData(),
                Draw = d => ComponentInspectors.LabelGroup((LabelGroupData)d),
            },
            ["nameplate"] = new Entry
            {
                Name = "nameplate", DisplayName = "铭牌背景",
                CreateDefault = () => new NameplateData(),
                Draw = d => ComponentInspectors.Nameplate((NameplateData)d),
            },
            ["healthbar"] = new Entry
            {
                Name = "healthbar", DisplayName = "血条",
                CreateDefault = BarData.CreateHealthBarDefault,
                Draw = d => ComponentInspectors.Bar((BarData)d),
            },
            ["mpbar"] = new Entry
            {
                Name = "mpbar", DisplayName = "MP条",
                CreateDefault = BarData.CreateMpBarDefault,
                Draw = d => ComponentInspectors.Bar((BarData)d),
            },
            ["castbar"] = new Entry
            {
                Name = "castbar", DisplayName = "施法条",
                CreateDefault = BarData.CreateCastBarDefault,
                Draw = d => ComponentInspectors.Bar((BarData)d),
            },
            ["actionbar"] = new Entry
            {
                Name = "actionbar", DisplayName = "动作栏",
                CreateDefault = () => new ActionBarData(),
                Draw = d => ComponentInspectors.ActionBar((ActionBarData)d),
            },
            ["levelbadge"] = new Entry
            {
                Name = "levelbadge", DisplayName = "等级徽章",
                CreateDefault = () => new LevelBadgeData(),
                Draw = d => ComponentInspectors.LevelBadge((LevelBadgeData)d),
            },
            ["monster_ai"] = new Entry
            {
                Name = "monster_ai", DisplayName = "怪物AI",
                CreateDefault = () => new MonsterAiData(),
                Draw = d => ComponentInspectors.MonsterAi((MonsterAiData)d),
            },
            ["npc_interact"] = new Entry
            {
                Name = "npc_interact", DisplayName = "交互面板",
                CreateDefault = () => new NpcInteractData(),
                Draw = d => ComponentInspectors.NpcInteract((NpcInteractData)d),
            },
            ["obstacle"] = new Entry
            {
                Name = "obstacle", DisplayName = "障碍",
                CreateDefault = () => new ObstacleData(),
                Draw = d => ComponentInspectors.Obstacle((ObstacleData)d),
            },
            ["building_type"] = new Entry
            {
                Name = "building_type", DisplayName = "建筑类型",
                CreateDefault = () => new BuildingTypeData(),
                Draw = d => ComponentInspectors.BuildingType((BuildingTypeData)d),
            },
            ["category"] = new Entry
            {
                Name = "category", DisplayName = "分类",
                CreateDefault = () => new CategoryData(),
                Draw = d => ComponentInspectors.Category((CategoryData)d),
            },
        };

        /// <summary>实体类型 → 可用组件白名单（与已删除 UGUI 版一致）。</summary>
        private static readonly Dictionary<string, string[]> _typeComponents = new()
        {
            ["player"] = new[] { "appearance", "labels", "nameplate", "healthbar", "mpbar", "castbar", "actionbar", "levelbadge" },
            ["monster"] = new[] { "appearance", "labels", "nameplate", "healthbar", "mpbar", "castbar", "actionbar", "monster_ai" },
            ["npc"] = new[] { "appearance", "labels", "nameplate", "healthbar", "mpbar", "npc_interact" },
            ["decoration"] = new[] { "appearance", "labels", "nameplate", "obstacle", "building_type", "category" },
        };

        public static Entry Get(string name)
        {
            if (_entries.TryGetValue(name, out var e)) return e;
            // 配置里存在但无检查器的组件（如未来新增）：只展示，不崩溃
            return new Entry
            {
                Name = name,
                DisplayName = name,
                CreateDefault = () => null,
                Draw = _ => EditorGUILayout.LabelField("（无编辑器检查器）", EditorStyles.miniLabel),
            };
        }

        /// <summary>该 Profile 按类型白名单尚未拥有的组件。</summary>
        public static List<Entry> GetAvailableFor(EntityProfile profile)
        {
            var list = new List<Entry>();
            if (profile == null || !_typeComponents.TryGetValue(profile.EntityType, out var names))
                return list;
            foreach (string n in names)
            {
                if (profile.HasComponent(n)) continue;
                if (_entries.TryGetValue(n, out var e)) list.Add(e);
            }
            return list;
        }
    }
}
