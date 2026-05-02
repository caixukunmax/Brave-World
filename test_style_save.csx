// 验证 MonsterStyle 保存/加载逻辑的测试
// 不依赖 Godot 运行时，只验证数据流

using System;
using System.Collections.Generic;

// 简化版 EntityStyleConfig，只包含关键字段
class EntityStyleConfig
{
    public float HpBarLengthScale = 102.0f / 111.0f; // ≈ 0.919
    public float HpBarHeightScale = 6.0f / 111.0f;   // ≈ 0.054
    public float MpBarLengthScale = 102.0f / 111.0f;
    public float MpBarHeightScale = 6.0f / 111.0f;
    public bool HpBarVisible = true;
    public bool MpBarVisible = false;
    public float HpBarFillPercent = 1.0f;
    public float MpBarFillPercent = 1.0f;
    public float HpBarOffsetX = 0;
    public float HpBarOffsetY = 0;
    public float MpBarOffsetX = 0;
    public float MpBarOffsetY = 0;
    public bool HpBarCenterX = true;
    public bool MpBarCenterX = true;
}

class Program
{
    static void Main()
    {
        // 模拟场景：用户修改了配置 ID 1 的血条长度比例
        var styleConfigs = new Dictionary<int, EntityStyleConfig>();
        styleConfigs[1] = new EntityStyleConfig();
        styleConfigs[2] = new EntityStyleConfig();
        
        // 默认值
        Console.WriteLine($"初始值: config[1].HpBarLengthScale = {styleConfigs[1].HpBarLengthScale:F6}");
        Console.WriteLine($"初始值: config[2].HpBarLengthScale = {styleConfigs[2].HpBarLengthScale:F6}");
        
        // 用户修改配置 ID 1 的血条长度比例
        styleConfigs[1].HpBarLengthScale = 1.183f;
        Console.WriteLine($"\n修改后: config[1].HpBarLengthScale = {styleConfigs[1].HpBarLengthScale:F6}");
        
        // 模拟保存到配置文件（key = "monster_{id}"）
        var savedData = new Dictionary<string, Dictionary<string, object>>();
        foreach (var kv in styleConfigs)
        {
            string sec = $"monster_{kv.Key}";
            var c = kv.Value;
            savedData[sec] = new Dictionary<string, object>
            {
                ["hp_bar_length_scale"] = (double)c.HpBarLengthScale,
                ["hp_bar_height_scale"] = (double)c.HpBarHeightScale,
                ["hp_bar_visible"] = c.HpBarVisible,
                ["mp_bar_length_scale"] = (double)c.MpBarLengthScale,
                ["mp_bar_height_scale"] = (double)c.MpBarHeightScale,
                ["mp_bar_visible"] = c.MpBarVisible,
            };
        }
        
        Console.WriteLine($"\n保存到配置文件:");
        foreach (var sec in savedData)
        {
            Console.WriteLine($"  [{sec.Key}]");
            foreach (var kv in sec.Value)
                Console.WriteLine($"    {kv.Key}={kv.Value}");
        }
        
        // 模拟重启后加载
        var loadedConfigs = new Dictionary<int, EntityStyleConfig>();
        foreach (var sec in savedData)
        {
            string idStr = sec.Key.Substring("monster_".Length);
            int id = int.Parse(idStr);
            var cfg = new EntityStyleConfig(); // 默认值
            // LoadStyleConfigFromSection
            cfg.HpBarLengthScale = (float)(double)sec.Value["hp_bar_length_scale"];
            cfg.HpBarHeightScale = (float)(double)sec.Value["hp_bar_height_scale"];
            cfg.HpBarVisible = (bool)sec.Value["hp_bar_visible"];
            cfg.MpBarLengthScale = (float)(double)sec.Value["mp_bar_length_scale"];
            cfg.MpBarHeightScale = (float)(double)sec.Value["mp_bar_height_scale"];
            cfg.MpBarVisible = (bool)sec.Value["mp_bar_visible"];
            loadedConfigs[id] = cfg;
        }
        
        Console.WriteLine($"\n重启后加载:");
        foreach (var kv in loadedConfigs)
            Console.WriteLine($"  config[{kv.Key}].HpBarLengthScale = {kv.Value.HpBarLengthScale:F6}");
        
        // 模拟怪物查找配置
        int[] monsterIds = { 1, 2, 3 };
        Console.WriteLine($"\n怪物查找配置:");
        foreach (int mid in monsterIds)
        {
            if (loadedConfigs.TryGetValue(mid, out var cfg))
                Console.WriteLine($"  MonsterId={mid} → HpBarLengthScale = {cfg.HpBarLengthScale:F6}");
            else
            {
                var fallback = loadedConfigs.Values.First();
                Console.WriteLine($"  MonsterId={mid} → 未找到，回退到 HpBarLengthScale = {fallback.HpBarLengthScale:F6}");
            }
        }
        
        // 验证
        if (Math.Abs(loadedConfigs[1].HpBarLengthScale - 1.183f) < 0.001f)
            Console.WriteLine($"\n✅ 配置 ID 1 的血条长度比例保存/加载正确");
        else
            Console.WriteLine($"\n❌ 配置 ID 1 的血条长度比例保存/加载失败: {loadedConfigs[1].HpBarLengthScale:F6}");
    }
}
