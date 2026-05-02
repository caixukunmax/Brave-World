using Godot;
using System.Collections.Generic;
using System.Linq;

namespace ClinetCSharp
{
    public class DebugPanelNpcTab : DebugPanelEntityStyleTabBase
    {
        #region Fields - Interact Menu Offset
        private HSlider _interactMenuOffsetAXSlider;
        private Label _interactMenuOffsetAXValue;
        private HSlider _interactMenuOffsetAYSlider;
        private Label _interactMenuOffsetAYValue;
        private HSlider _interactMenuOffsetBXSlider;
        private Label _interactMenuOffsetBXValue;
        private HSlider _interactMenuOffsetBYSlider;
        private Label _interactMenuOffsetBYValue;
        #endregion

        public DebugPanelNpcTab(DebugPanel owner) : base(owner) { }
        public override string TabKey => "npc";

        #region Abstract implementations
        protected override string ConfigSectionPrefix => "npc";
        protected override EntityStyleConfig GetStyleConfig(int id) => NpcManager.GetStyleConfig(id);
        protected override EntityStyleConfig GetOrCreateStyleConfig(int id) => NpcManager.GetOrCreateStyleConfig(id);
        protected override Dictionary<int, EntityStyleConfig> GetAllStyleConfigs() => NpcManager.StyleConfigs;
        protected override void ApplyStyleToAll() => NpcManager.Instance?.ApplyStyleToAll();
        #endregion

        #region BuildUI
        public override void BuildUI(VBoxContainer tabContainer)
        {
            BuildEntityStyleUI(tabContainer, "NPC 全局样式");
            _borderColorPicker.Color = new Color(0.3f, 0.5f, 0.9f);
            _bgColorPicker.Color = new Color(0.2f, 0.4f, 0.8f);
            _textColorPicker.Color = new Color(0.95f, 0.97f, 1.0f);

            // 交互面板偏移
            tabContainer.AddChild(new HSeparator());
            tabContainer.AddChild(new Label { Text = "交互面板偏移:" });
            tabContainer.AddChild(new Label { Text = "  A位置(玩家在左,面板在右):" });
            (_interactMenuOffsetAXSlider, _interactMenuOffsetAXValue) = CreateMonsterSliderRow(tabContainer, "  A-X", -200, 200, 60);
            (_interactMenuOffsetAYSlider, _interactMenuOffsetAYValue) = CreateMonsterSliderRow(tabContainer, "  A-Y", -200, 200, -20);
            tabContainer.AddChild(new Label { Text = "  B位置(玩家在右,面板在左):" });
            (_interactMenuOffsetBXSlider, _interactMenuOffsetBXValue) = CreateMonsterSliderRow(tabContainer, "  B-X", -200, 200, -60);
            (_interactMenuOffsetBYSlider, _interactMenuOffsetBYValue) = CreateMonsterSliderRow(tabContainer, "  B-Y", -200, 200, -20);

            ConnectStyleSignals();
            _interactMenuOffsetAXSlider.ValueChanged += (_) => ApplyStyleChanges();
            _interactMenuOffsetAYSlider.ValueChanged += (_) => ApplyStyleChanges();
            _interactMenuOffsetBXSlider.ValueChanged += (_) => ApplyStyleChanges();
            _interactMenuOffsetBYSlider.ValueChanged += (_) => ApplyStyleChanges();
        }

        protected override void BuildSubclassUI(VBoxContainer tabContainer) { }
        #endregion

        #region ApplySubclassChanges
        protected override void ApplySubclassChanges(EntityStyleConfig cfg)
        {
            cfg.InteractMenuOffsetAX = (float)_interactMenuOffsetAXSlider.Value;
            cfg.InteractMenuOffsetAY = (float)_interactMenuOffsetAYSlider.Value;
            cfg.InteractMenuOffsetBX = (float)_interactMenuOffsetBXSlider.Value;
            cfg.InteractMenuOffsetBY = (float)_interactMenuOffsetBYSlider.Value;
            NpcManager.Instance?.RefreshInteractMenuPosition();
        }
        #endregion

        #region SyncAfterStyleUI
        protected override void SyncAfterStyleUI()
        {
            var cfg = NpcManager.GetStyleConfig(_selectedConfigId);
            _interactMenuOffsetAXSlider.SetBlockSignals(true);
            _interactMenuOffsetAYSlider.SetBlockSignals(true);
            _interactMenuOffsetBXSlider.SetBlockSignals(true);
            _interactMenuOffsetBYSlider.SetBlockSignals(true);
            _interactMenuOffsetAXSlider.Value = cfg.InteractMenuOffsetAX;
            _interactMenuOffsetAYSlider.Value = cfg.InteractMenuOffsetAY;
            _interactMenuOffsetBXSlider.Value = cfg.InteractMenuOffsetBX;
            _interactMenuOffsetBYSlider.Value = cfg.InteractMenuOffsetBY;
            _interactMenuOffsetAXSlider.SetBlockSignals(false);
            _interactMenuOffsetAYSlider.SetBlockSignals(false);
            _interactMenuOffsetBXSlider.SetBlockSignals(false);
            _interactMenuOffsetBYSlider.SetBlockSignals(false);
        }
        #endregion

        #region SyncToCurrentValues
        public override void SyncToCurrentValues()
        {
            RefreshConfigIdList();
            SyncStyleUI();
            SyncAfterStyleUI();
        }
        #endregion

        #region SaveConfig
        public override void SaveConfig(ConfigFile cfg)
        {
            foreach (var kv in NpcManager.StyleConfigs)
            {
                string sec = $"npc_{kv.Key}";
                var c = kv.Value;
                SaveCommonStyleConfig(cfg, sec, c);
                SaveBarConfig(cfg, sec, "hp", c);
                SaveBarConfig(cfg, sec, "mp", c);
                cfg.SetValue(sec, "interact_menu_offset_ax", (double)c.InteractMenuOffsetAX);
                cfg.SetValue(sec, "interact_menu_offset_ay", (double)c.InteractMenuOffsetAY);
                cfg.SetValue(sec, "interact_menu_offset_bx", (double)c.InteractMenuOffsetBX);
                cfg.SetValue(sec, "interact_menu_offset_by", (double)c.InteractMenuOffsetBY);
            }
        }
        #endregion

        #region LoadConfig
        public override void LoadConfig(ConfigFile cfg, bool configLoaded)
        {
            RefreshConfigIdList();
            NpcManager.Instance?.ApplyStyleToAll();
            SyncStyleUI();
            SyncAfterStyleUI();
        }
        #endregion

        #region ExportConfigData
        public override Godot.Collections.Dictionary ExportConfigData()
        {
            var data = new Godot.Collections.Dictionary();
            foreach (var kv in NpcManager.StyleConfigs)
            {
                var c = kv.Value;
                var dict = new Godot.Collections.Dictionary();
                ExportCommonConfigData(dict, c);
                dict["interact_menu_offset_ax"] = c.InteractMenuOffsetAX;
                dict["interact_menu_offset_ay"] = c.InteractMenuOffsetAY;
                dict["interact_menu_offset_bx"] = c.InteractMenuOffsetBX;
                dict["interact_menu_offset_by"] = c.InteractMenuOffsetBY;
                data[$"config_{kv.Key}"] = dict;
            }
            return data;
        }
        #endregion

        #region SaveSubclassConfig / ExportSubclassConfigData
        protected override void SaveSubclassConfig(ConfigFile cfg, string section, EntityStyleConfig c) { }
        protected override void ExportSubclassConfigData(Godot.Collections.Dictionary dict, EntityStyleConfig c) { }
        #endregion
    }
}