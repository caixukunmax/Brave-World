using Godot;

namespace ClinetCSharp
{
    public partial class DebugPanel
    {
        public string ExportConfigToJson()
        {
            var data = new Godot.Collections.Dictionary
            {
                ["timestamp"] = Time.GetDatetimeStringFromSystem(),
                ["config_version"] = CONFIG_VERSION,
            };

            foreach (var tab in _tabs)
            {
                var tabData = tab.ExportConfigData();
                if (tabData != null)
                    data[tab.TabKey] = tabData;
            }

            return Json.Stringify(data, "  ");
        }
    }
}
