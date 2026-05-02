using Godot;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ClinetCSharp
{
    /// <summary>
    /// 怪物配置管理器 — 读写 monster_config.json
    /// 供 DebugPanel 编辑和服务端（通过共享文件）读取
    /// </summary>
    public partial class MonsterConfigManager : Node
    {
        public const string ConfigPath = "res://data/monster_config.json";

        public MonsterConfigData Config { get; private set; } = new();

        public override void _Ready()
        {
            AddToGroup("monster_config_manager");
            LoadConfig();
        }

        public void LoadConfig()
        {
            string path = ProjectSettings.GlobalizePath(ConfigPath);
            if (!File.Exists(path))
            {
                GD.PushError($"[MonsterConfigManager] Config not found: {path}");
                return;
            }

            try
            {
                string json = File.ReadAllText(path);
                Config = JsonSerializer.Deserialize<MonsterConfigData>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                }) ?? new MonsterConfigData();
                GD.Print($"[MonsterConfigManager] Loaded {Config.Monsters.Count} monsters");
            }
            catch (System.Exception ex)
            {
                GD.PushError($"[MonsterConfigManager] Failed to load config: {ex.Message}");
            }
        }

        public void SaveConfig()
        {
            string path = ProjectSettings.GlobalizePath(ConfigPath);
            try
            {
                string json = JsonSerializer.Serialize(Config, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                });
                File.WriteAllText(path, json);
                GD.Print("[MonsterConfigManager] Config saved");
            }
            catch (System.Exception ex)
            {
                GD.PushError($"[MonsterConfigManager] Failed to save config: {ex.Message}");
            }
        }

        // ---- 快捷访问 ----

        public int GetMoveSpeedMs()
            => Config.MoveDefaults?.MonsterMoveSpeedMs ?? 800;

        public void SetMoveSpeedMs(int value)
        {
            if (Config.MoveDefaults == null) Config.MoveDefaults = new MoveDefaults();
            Config.MoveDefaults.MonsterMoveSpeedMs = value;
        }

        public AiDefaults GetAiDefaults(string aiType)
        {
            if (Config.AiDefaults == null) return new AiDefaults();
            return Config.AiDefaults.GetValueOrDefault(aiType, new AiDefaults());
        }

        public void SetAiDefaults(string aiType, AiDefaults data)
        {
            if (Config.AiDefaults == null) Config.AiDefaults = new Dictionary<string, AiDefaults>();
            Config.AiDefaults[aiType] = data;
        }

        // ---- 移动系统 ----

        public MoveSystem GetMoveSystem()
            => Config.MoveSystem ?? new MoveSystem();

        public void SetMoveSystem(MoveSystem value)
        {
            Config.MoveSystem = value;
        }
    }

    // ---- 数据模型 ----

    public class MonsterConfigData
    {
        public int Version { get; set; } = 1;
        public List<MonsterDef> Monsters { get; set; } = new();
        public Dictionary<string, AiDefaults> AiDefaults { get; set; } = new();
        public MoveDefaults MoveDefaults { get; set; } = new();
        public MoveSystem MoveSystem { get; set; } = new();
    }

    public class MonsterDef
    {
        public int MonsterId { get; set; }
        public string Name { get; set; } = "";
        public string MapName { get; set; } = "";
        public int X { get; set; }
        public int Y { get; set; }
        public string AiType { get; set; } = "patrol";
        public int Hp { get; set; } = 100;
        public int MaxHp { get; set; } = 100;
        public int Level { get; set; } = 1;
        /// <summary>UI配置ID，指定使用哪个 EntityStyleConfig。必填，默认1</summary>
        public int UiConfigId { get; set; } = 1;
    }

    public class AiDefaults
    {
        public int? PatrolRange { get; set; }
        public int? AggroRange { get; set; }
        public int? MaxChaseDistance { get; set; }
        public long? MoveIntervalMs { get; set; }
        public long? ChaseIntervalMs { get; set; }
    }

    public class MoveDefaults
    {
        public int MonsterMoveSpeedMs { get; set; } = 800;
    }

    public class MoveSystem
    {
        public int CheckRatio { get; set; } = 30;
        public int DualGridStartRatio { get; set; } = 30;
        public int DualGridEndRatio { get; set; } = 70;
    }
}
