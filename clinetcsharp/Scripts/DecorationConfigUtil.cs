using System.Collections.Generic;
using Godot;

namespace ClinetCSharp
{
    /// <summary>
    /// 装饰配置静态数据。
    /// 一期内嵌默认配置，后续类型增多后可改为从 Luban JSON 加载。
    /// </summary>
    public static class DecorationConfigUtil
    {
        public static readonly Dictionary<int, DecorationConfig> Configs = new();

        private static bool _loaded;

        public static void Load()
        {
            if (_loaded) return;
            _loaded = true;

            // 一期内嵌：0=无，1=房舍
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

            Configs[1] = new DecorationConfig
            {
                Id = 1,
                Name = "House",
                DisplayName = "房舍",
                BlockMovement = true,
                Color = new Color(0.545f, 0.353f, 0.169f, 0.9f), // #8B5A2B
                BorderColor = new Color(0.4f, 0.2f, 0.1f),
                Description = "普通的房舍，阻塞移动"
            };

            GD.Print($"[DecorationConfigUtil] Loaded {Configs.Count} decoration configs (embedded)");
        }

        public static DecorationConfig Get(int id)
        {
            return Configs.TryGetValue(id, out var c) ? c : Configs.GetValueOrDefault(0);
        }

        public static string GetDisplayName(int id)
        {
            return Configs.TryGetValue(id, out var c) ? c.DisplayName : "未知装饰";
        }

        public static bool BlocksMovement(int id)
        {
            return Configs.TryGetValue(id, out var c) && c.BlockMovement;
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
    }
}
