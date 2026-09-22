using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace UnityClientSharp.Entity
{
    /// <summary>
    /// 物品图标与品质色目录（静态版）。
    /// 移植自 clinetcsharp/Scripts/ItemIconCatalog.cs + InventoryManager.GetItemName：
    /// 品质描边色表、item_id → 名称/图标纹理（StreamingAssets/Data/items，运行时读取并缓存）。
    /// </summary>
    public static class ItemIconCatalog
    {
        /// <summary>品质描边色（对齐 Godot ItemIconCatalog）：0 白 / 1 绿 / 2 蓝 / 3 紫 / 4 金。</summary>
        public static Color GetQualityColor(int quality) => quality switch
        {
            1 => new Color(0.1f, 1f, 0.1f),
            2 => new Color(0.1f, 0.5f, 1f),
            3 => new Color(0.6f, 0.2f, 1f),
            4 => new Color(1f, 0.5f, 0f),
            _ => Color.white,
        };

        private class ItemEntry
        {
            public string Name = "";
            public int Quality;
            public string Icon = "";
        }

        private static readonly Dictionary<int, ItemEntry> _items = new();
        private static readonly Dictionary<string, Texture2D> _iconCache = new();
        private static bool _loaded;

        private static void Load()
        {
            if (_loaded) return;
            _loaded = true;

            try
            {
                string path = Path.Combine(Application.streamingAssetsPath, "Data", "item_config.json");
                if (!File.Exists(path))
                {
                    Debug.LogWarning("[ItemIconCatalog] item_config.json not found: " + path);
                    return;
                }

                foreach (JObject row in JArray.Parse(File.ReadAllText(path)))
                {
                    int id = (int?)row["id"] ?? 0;
                    _items[id] = new ItemEntry
                    {
                        Name = (string)row["name"] ?? "",
                        Quality = (int?)row["quality"] ?? 0,
                        Icon = (string)row["icon"] ?? "",
                    };
                }
                Debug.Log($"[ItemIconCatalog] Loaded {_items.Count} items");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ItemIconCatalog] Failed to load: {ex.Message}");
            }
        }

        /// <summary>物品名（缺省 "物品{id}"，对齐 Godot InventoryManager.GetItemName）。</summary>
        public static string GetItemName(int itemId)
        {
            Load();
            return _items.TryGetValue(itemId, out var e) && !string.IsNullOrEmpty(e.Name)
                ? e.Name
                : $"物品{itemId}";
        }

        public static int GetQuality(int itemId)
        {
            Load();
            return _items.TryGetValue(itemId, out var e) ? e.Quality : 0;
        }

        /// <summary>物品图标纹理（icon 字段为空或文件缺失时回退 banana 128，对齐 Godot 缺省逻辑）。</summary>
        public static Texture2D GetIcon(int itemId)
        {
            Load();
            string file = _items.TryGetValue(itemId, out var e) ? e.Icon : "";
            if (string.IsNullOrEmpty(file)) file = "food_banana_128.png";
            return LoadIcon(file);
        }

        private static Texture2D LoadIcon(string filename)
        {
            if (_iconCache.TryGetValue(filename, out var cached) && cached != null)
                return cached;

            string path = Path.Combine(Application.streamingAssetsPath, "Data", "items", filename);
            Texture2D tex = null;
            if (File.Exists(path))
            {
                tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
                tex.LoadImage(File.ReadAllBytes(path));
                tex.name = filename;
            }
            else if (filename != "food_banana_128.png")
            {
                return LoadIcon("food_banana_128.png");
            }

            if (tex != null) _iconCache[filename] = tex;
            return tex;
        }
    }
}
