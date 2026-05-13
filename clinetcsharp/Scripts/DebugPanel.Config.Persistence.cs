using Godot;

namespace ClinetCSharp
{
    public partial class DebugPanel
    {
        protected override void SavePosition()
        {
            SavePanelGeometry();
        }

        private void SavePanelGeometry()
        {
            ConfigFile config = new ConfigFile();
            Error err = config.Load(CONFIG_PATH);
            if (err != Error.Ok && err != Error.FileNotFound)
            {
                GD.PushError($"[DebugPanel] Failed to load config before saving panel geometry: {err}");
                return;
            }

            config.SetValue("meta", "config_version", CONFIG_VERSION);
            WritePanelGeometry(config);

            err = config.Save(CONFIG_PATH);
            if (err != Error.Ok)
                GD.PushError($"[DebugPanel] Failed to save panel geometry: {err}");
        }

        private void WritePanelGeometry(ConfigFile config)
        {
            float persistedHeight = IsMinimized ? NormalHeight : Size.Y;

            config.SetValue("panel_geo", "offset_left", Position.X);
            config.SetValue("panel_geo", "offset_top", Position.Y);
            config.SetValue("panel_geo", "offset_right", Position.X + Size.X);
            config.SetValue("panel_geo", "offset_bottom", Position.Y + persistedHeight);
        }

        internal void SaveConfigFromTab() => SaveConfig();

        private ConfigFile BuildCurrentConfigSnapshot()
        {
            ConfigFile config = new ConfigFile();
            config.SetValue("meta", "config_version", CONFIG_VERSION);
            config.SetValue("meta", "last_save_time", Time.GetDatetimeStringFromSystem());

            foreach (var tab in _tabs)
                tab.SaveConfig(config);

            WritePanelGeometry(config);
            return config;
        }

        private void SaveConfig()
        {
            ConfigFile config = BuildCurrentConfigSnapshot();

            Error err = config.Save(CONFIG_PATH);
            if (err == Error.Ok)
            {
                GD.Print("[DebugPanel] Config saved");
            }
            else
            {
                GD.PushError($"Failed to save config: {err}");
            }
        }

        private void LoadConfig()
        {
            if (_player == null)
            {
                _player = GetTree().GetFirstNodeInGroup("player") as Player;
                if (_player == null)
                    return;
            }

            ConfigFile config = new ConfigFile();
            Error err = config.Load(CONFIG_PATH);
            bool configLoaded = err == Error.Ok;

            if (!configLoaded)
            {
                if (err == Error.FileNotFound)
                {
                    GD.Print("[DebugPanel] Config file doesn't exist, using default settings");
                }
                else
                {
                    GD.PushError($"[DebugPanel] Failed to load config: {err}, using defaults");
                }
            }
            else
            {
                int savedVersion = (int)config.GetValue("meta", "config_version", 0);
                if (savedVersion < CONFIG_VERSION)
                {
                    GD.Print($"[DebugPanel] Config version upgraded from {savedVersion} to {CONFIG_VERSION}");
                    MigrateConfig(config, savedVersion);
                }

                if (config.HasSection("panel_geo"))
                {
                    float left = (float)(double)config.GetValue("panel_geo", "offset_left", (double)Position.X);
                    float top = (float)(double)config.GetValue("panel_geo", "offset_top", (double)Position.Y);
                    float right = (float)(double)config.GetValue("panel_geo", "offset_right", (double)(Position.X + Size.X));
                    float bottom = (float)(double)config.GetValue("panel_geo", "offset_bottom", (double)(Position.Y + Size.Y));

                    RestorePosition(left, top, Mathf.Max(250.0f, right - left), Mathf.Max(200.0f, bottom - top));
                    GD.Print("[DebugPanel] Restored panel geometry");
                }
            }

            foreach (var tab in _tabs)
                tab.LoadConfig(config, configLoaded);

            UpdateControlStates();
        }

        private void ResetConfigAndReload()
        {
            GD.Print("[DebugPanel] Resetting config to defaults");
            ConfigFile config = new ConfigFile();
            config.SetValue("meta", "config_version", CONFIG_VERSION);
            config.SetValue("meta", "last_save_time", Time.GetDatetimeStringFromSystem());
            Error err = config.Save(CONFIG_PATH);
            if (err != Error.Ok)
            {
                GD.PushError($"[DebugPanel] Failed to reset config: {err}");
            }

            LoadConfig();
        }
    }
}
