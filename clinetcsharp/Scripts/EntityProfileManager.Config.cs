using Godot;
using System.Collections.Generic;
using System.Linq;

namespace ClinetCSharp
{
    /// <summary>
    /// EntityProfileManager partial — 配置读写入口
    /// 实际序列化逻辑集中在 ProfileConfigIO（不依赖 Node，运行期与编辑器插件共用）。
    /// </summary>
    public partial class EntityProfileManager
    {
        public const string ConfigPath = ProfileConfigIO.ConfigPath;
        public const int ConfigVersion = ProfileConfigIO.ConfigVersion;

        public void SaveConfig()
        {
            ProfileConfigIO.SaveToFile(_profiles);
        }

        /// <summary>
        /// 将当前所有 Profile 写入 ConfigFile（用于统一保存：调试面板保存时调用，便于一次落盘多个 Profile）
        /// </summary>
        public void WriteProfileConfig(ConfigFile config)
        {
            ProfileConfigIO.Write(config, _profiles);
        }

        public void LoadConfig()
        {
            var config = new ConfigFile();
            Error err = config.Load(ConfigPath);
            if (err == Error.FileNotFound)
            {
                GD.Print("[EntityProfileManager] Config file not found, using defaults");
                ProfileConfigIO.EnsureDefaultProfiles(_profiles);
                _nextId = _profiles.Count > 0 ? _profiles.Keys.Max() + 1 : 1;
                return;
            }
            if (err != Error.Ok)
            {
                GD.PushError($"[EntityProfileManager] Failed to load config: {err}");
                return;
            }

            bool hasProfileSections = config.GetSections().Any(s => s.StartsWith("profile_"));
            if (hasProfileSections)
            {
                _nextId = ProfileConfigIO.LoadInto(config, _profiles);
            }
            else
            {
                // 旧格式（无 profile_ section，依赖 GetTree）仅运行时可迁移
                LoadLegacyFormat(config);
            }
        }
    }
}
