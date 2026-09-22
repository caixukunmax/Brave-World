using System.Collections.Generic;
using System.Linq;
using UnityClientSharp.Map.Core;
using UnityEngine;

namespace UnityClientSharp.Entity
{
    /// <summary>
    /// 实体配置档案管理器（静态版）。
    /// 移植自 clinetcsharp/Scripts/EntityProfileManager.cs 的 EnsureDefaultProfiles 装饰系注册；
    /// 裁剪：debug_panel_config.cfg 样式覆盖存读（随 DebugPanel 阶段处理）、玩家/怪物/NPC 默认 Profile（在线实体阶段接入）。
    /// 首次访问时自动初始化，无 Godot 单例时序问题。
    /// </summary>
    public static partial class EntityProfileManager
    {
        private static readonly Dictionary<int, EntityProfile> _profiles = new();
        private static bool _initialized;

        /// <summary>下一个可分配的 Profile ID（LoadConfig 后根据已有最大 ID 推进）。</summary>
        private static int _nextId = 1;

        /// <summary>配置文件版本（JSON 新基线从 1 起，不兼容旧 Godot cfg 的定点格式）。</summary>
        private const int ConfigVersion = 1;

        /// <summary>配置落盘路径（Editor 与运行时同一路径，双模式共享同一份配置）。</summary>
        private static string ConfigPath => System.IO.Path.Combine(Application.persistentDataPath, "entity_profiles.json");

        public static void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;
            EnsureDefaultProfiles();
            LoadConfig(); // 有存档则覆盖默认；无存档则保持默认（LoadConfig 内部处理）
        }

        public static EntityProfile GetProfile(int id)
        {
            EnsureInitialized();
            return _profiles.TryGetValue(id, out var p) ? p : null;
        }

        public static IEnumerable<EntityProfile> GetProfilesByType(string entityType)
        {
            EnsureInitialized();
            return _profiles.Values.Where(p => p.EntityType == entityType);
        }

        /// <summary>
        /// 默认建筑 Profiles（数值与 Godot 端一致，勿改：房舍 2x2 与服务器 buildings.json 对齐）。
        /// 10001=房舍(2x2, Building), 20000=商店, 30000=水井, 40000=农田(2x1), 50000=酒馆(2x2),
        /// 60000=出生点, 70000=共享传送门, 80000=水域, 90000=岩石, 100000=树, 110000=草地
        /// 旧兼容: 10000=树旧ID, 10002=岩石旧ID, 10003=草地旧ID（仅用于未重新生成的旧地图）
        /// </summary>
        private static void EnsureDefaultProfiles()
        {
            // 玩家(1)/怪物(2)/NPC(3)
            if (!_profiles.ContainsKey(1))
                _profiles[1] = EntityProfile.CreatePlayerDefault(1);
            if (!_profiles.ContainsKey(2))
                _profiles[2] = EntityProfile.CreateMonsterDefault(2);
            if (!_profiles.ContainsKey(3))
                _profiles[3] = EntityProfile.CreateNpcDefault(3);

            void EnsureDeco(int id, string name, string displayName, int buildingType, Color bgColor, Color borderColor,
                bool blockMovement, int sizeX, int sizeY, string category)
            {
                // Godot 端此处还会给已存在的 profile 补 category 组件（用户 cfg 覆盖场景）；
                // 本静态版在全新初始化时注册，无此场景。
                if (_profiles.ContainsKey(id)) return;
                _profiles[id] = EntityProfile.CreateDecorationDefault(id, name, displayName, buildingType,
                    bgColor, borderColor, blockMovement, sizeX, sizeY, category);
            }

            EnsureDeco(BuildingType.GetConfigBaseId(BuildingType.House), "TreeLegacy", "树(旧)", BuildingType.House,
                new Color(0.2f, 0.5f, 0.25f, 0.9f), new Color(0.1f, 0.35f, 0.15f), false, 1, 1, "Legacy");
            EnsureDeco(BuildingType.GetConfigBaseId(BuildingType.House) + 1, "House", "房舍", BuildingType.House,
                new Color(0.545f, 0.353f, 0.169f, 0.9f), new Color(0.4f, 0.2f, 0.1f), true, 2, 2, "Building");
            // 10002 岩石：历史地图生成器使用的旧 ID（House 区间），保留兼容
            EnsureDeco(BuildingType.GetConfigBaseId(BuildingType.House) + 2, "RockLegacy", "岩石(旧)", BuildingType.House,
                new Color(0.53f, 0.53f, 0.53f, 0.9f), new Color(0.35f, 0.35f, 0.35f), true, 1, 1, "Legacy");
            // 10003 草地：旧 ID，保留兼容
            EnsureDeco(BuildingType.GetConfigBaseId(BuildingType.House) + 3, "GrassLegacy", "草地(旧)", BuildingType.House,
                new Color(0.35f, 0.65f, 0.35f, 0.9f), new Color(0.2f, 0.45f, 0.2f), false, 1, 1, "Legacy");
            EnsureDeco(BuildingType.GetConfigBaseId(BuildingType.Shop), "Shop", "商店", BuildingType.Shop,
                new Color(0.2f, 0.4f, 0.6f, 0.9f), new Color(0.1f, 0.3f, 0.5f), true, 1, 1, "Building");
            EnsureDeco(BuildingType.GetConfigBaseId(BuildingType.Well), "Well", "水井", BuildingType.Well,
                new Color(0.5f, 0.5f, 0.55f, 0.9f), new Color(0.3f, 0.3f, 0.35f), true, 1, 1, "Building");
            EnsureDeco(BuildingType.GetConfigBaseId(BuildingType.Farm), "Farm", "农田", BuildingType.Farm,
                new Color(0.8f, 0.7f, 0.3f, 0.9f), new Color(0.5f, 0.4f, 0.1f), true, 2, 1, "Building");
            EnsureDeco(BuildingType.GetConfigBaseId(BuildingType.Tavern), "Tavern", "酒馆", BuildingType.Tavern,
                new Color(0.6f, 0.3f, 0.2f, 0.9f), new Color(0.4f, 0.15f, 0.1f), true, 2, 2, "Building");
            EnsureDeco(BuildingType.GetConfigBaseId(BuildingType.SpawnPoint), "SpawnPoint", "出生点", BuildingType.SpawnPoint,
                new Color(0.2f, 0.8f, 0.9f, 0.9f), new Color(0.1f, 0.5f, 0.6f), false, 1, 1, "Special");
            EnsureDeco(BuildingType.GetConfigBaseId(BuildingType.Portal), "Portal", "共享传送门", BuildingType.Portal,
                new Color(0.6f, 0.2f, 0.9f, 0.9f), new Color(0.4f, 0.1f, 0.7f), false, 1, 1, "Special");
            // 水域装饰：把底层水地形转换为建筑装饰，与房舍同为 decoration 级别
            EnsureDeco(BuildingType.GetConfigBaseId(BuildingType.Water), "Water", "水", BuildingType.Water,
                new Color(0.29f, 0.56f, 0.85f, 0.9f), new Color(0.15f, 0.35f, 0.6f), true, 1, 1, "Terrain");
            // 岩石装饰：独立建筑类型，取代旧 10002 成为岩石的标准 decoration ID
            EnsureDeco(BuildingType.GetConfigBaseId(BuildingType.Rock), "Rock", "岩石", BuildingType.Rock,
                new Color(0.53f, 0.53f, 0.53f, 0.9f), new Color(0.35f, 0.35f, 0.35f), true, 1, 1, "Terrain");
            EnsureDeco(BuildingType.GetConfigBaseId(BuildingType.Tree), "Tree", "树", BuildingType.Tree,
                new Color(0.2f, 0.5f, 0.25f, 0.9f), new Color(0.1f, 0.35f, 0.15f), false, 1, 1, "Terrain");
            EnsureDeco(BuildingType.GetConfigBaseId(BuildingType.Grass), "Grass", "草地", BuildingType.Grass,
                new Color(0.35f, 0.65f, 0.35f, 0.9f), new Color(0.2f, 0.45f, 0.2f), false, 1, 1, "Terrain");
        }
    }
}
