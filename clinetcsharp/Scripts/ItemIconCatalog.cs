using Godot;
using System.Collections.Generic;

namespace ClinetCSharp
{
    /// <summary>
    /// 道具图标目录 —— 统一管理道具 id → 图标资源的映射与加载。
    /// </summary>
    public static class ItemIconCatalog
    {
        private static readonly Dictionary<uint, Texture2D> _cache = new();

        private static readonly Color CommonColor = new(1f, 1f, 1f);       // 白
        private static readonly Color UncommonColor = new(0.1f, 1f, 0.1f); // 绿
        private static readonly Color RareColor = new(0.1f, 0.5f, 1f);     // 蓝
        private static readonly Color EpicColor = new(0.6f, 0.2f, 1f);     // 紫
        private static readonly Color LegendaryColor = new(1f, 0.5f, 0f);  // 金/橙

        private const string DefaultIconPath = "res://assets/items/food_banana_128.png";
        private const string AssetRoot = "res://assets/items/";

        /// <summary>获取道具图标；缺失时返回默认占位图。</summary>
        public static Texture2D GetIcon(uint itemId)
        {
            if (_cache.TryGetValue(itemId, out var cached))
                return cached;

            var texture = LoadIcon(itemId);
            _cache[itemId] = texture;
            return texture;
        }

        /// <summary>按品质获取颜色。</summary>
        public static Color GetQualityColor(int quality)
        {
            return quality switch
            {
                0 => CommonColor,
                1 => UncommonColor,
                2 => RareColor,
                3 => EpicColor,
                4 => LegendaryColor,
                _ => CommonColor,
            };
        }

        /// <summary>清除缓存（配置热更后调用）。</summary>
        public static void ClearCache()
        {
            _cache.Clear();
        }

        private static Texture2D LoadIcon(uint itemId)
        {
            string iconName = TryGetConfigIcon(itemId);
            if (!string.IsNullOrEmpty(iconName))
            {
                string path = AssetRoot + iconName;
                var texture = GD.Load<Texture2D>(path);
                if (texture != null)
                    return texture;

                GD.PrintErr($"[ItemIconCatalog] Failed to load icon: {path}, fallback to default.");
            }

            var fallback = GD.Load<Texture2D>(DefaultIconPath);
            return fallback ?? new PlaceholderTexture2D();
        }

        private static string TryGetConfigIcon(uint itemId)
        {
            var tree = Engine.GetMainLoop() as SceneTree;
            if (tree == null) return string.Empty;

            var node = tree.GetFirstNodeInGroup("inventory_manager");
            if (node is not InventoryManager mgr) return string.Empty;

            return mgr.GetItemIcon(itemId);
        }
    }
}
