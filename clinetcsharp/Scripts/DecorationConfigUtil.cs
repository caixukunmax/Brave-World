using System.Collections.Generic;
using Godot;

namespace ClinetCSharp
{
    /// <summary>
    /// 装饰配置兼容层。
    /// 建筑已迁移到 EntityProfileManager（entityType="decoration"），
    /// 此处保留旧接口供 GridManager/MapEditor 等历史调用点平滑过渡。
    /// </summary>
    public static class DecorationConfigUtil
    {
        public static readonly Dictionary<int, DecorationConfig> Configs = new();

        /// <summary>建筑配置发生变化时触发，便于 MapEditor 等 UI 刷新列表</summary>
        public static event System.Action ProfilesChanged;

        private static bool _loaded;

        public static void Load()
        {
            if (_loaded) return;
            _loaded = true;

            RefreshFromProfileManager();
            GD.Print($"[DecorationConfigUtil] Loaded {Configs.Count} decoration configs from EntityProfileManager");
        }

        /// <summary>
        /// 从 EntityProfileManager 同步 decoration 类型的 Profile 到本地缓存。
        /// 在新增/修改建筑 Profile 后调用，保证旧接口读到最新数据。
        /// </summary>
        public static void RefreshFromProfileManager()
        {
            Configs.Clear();
            Configs[0] = new DecorationConfig
            {
                Id = 0,
                Name = "None",
                DisplayName = "无",
                BlockMovement = false,
                Color = Colors.Transparent,
                BorderColor = Colors.Transparent,
                Description = "无装饰"
            };

            var profileMgr = EntityProfileManager.Instance;
            if (profileMgr == null) return;

            foreach (var profile in profileMgr.GetProfilesByType("decoration"))
            {
                var app = profile.GetData<AppearanceData>("appearance");
                var labels = profile.GetData<LabelGroupData>("labels");
                var obstacle = profile.GetData<ObstacleData>("obstacle");

                var cfg = new DecorationConfig
                {
                    Id = profile.Id,
                    Name = profile.Name,
                    DisplayName = labels?.ContentPreview[0] ?? profile.Name,
                    BlockMovement = obstacle?.BlockMovement ?? false,
                    Description = $"{profile.Name} 装饰",
                };

                if (app != null)
                {
                    cfg.Color = new Color(app.BgColor.R, app.BgColor.G, app.BgColor.B, app.BgOpacity);
                    cfg.BorderColor = app.BorderColor;
                    cfg.SizeX = app.SizeX > 0 ? app.SizeX : 1;
                    cfg.SizeY = app.SizeY > 0 ? app.SizeY : 1;
                }

                Configs[profile.Id] = cfg;
            }

            ProfilesChanged?.Invoke();
        }

        public static DecorationConfig Get(int id)
        {
            RefreshFromProfileManager();
            return Configs.TryGetValue(id, out var c) ? c : Configs.GetValueOrDefault(0);
        }

        public static string GetDisplayName(int id)
        {
            var cfg = Get(id);
            return cfg.DisplayName;
        }

        public static bool BlocksMovement(int id)
        {
            var cfg = Get(id);
            return cfg.BlockMovement;
        }
    }

    public class DecorationConfig
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Description { get; set; } = "";
        public bool BlockMovement { get; set; } = false;
        public Color Color { get; set; } = Colors.White;
        public Color BorderColor { get; set; } = Colors.Black;

        // 未来扩展字段
        public string IconPath { get; set; } = "";      // 图标资源路径，空则使用 Color 块
        public string Category { get; set; } = "";       // 分类标签（民居 / 军事 / 装饰等）
        public string PinyinName { get; set; } = "";     // 拼音或首字母，用于搜索
        public int SizeX { get; set; } = 1;              // 占地宽度（格子数）
        public int SizeY { get; set; } = 1;              // 占地高度（格子数）
    }
}
