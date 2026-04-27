using Godot;
using System.Collections.Generic;

namespace ClinetCSharp
{
    /// <summary>
    /// DebugPanel tab base class — each tab independently manages its controls, events, and config.
    /// Subclasses: DebugPanelMapTab, DebugPanelPlayerTab, DebugPanelMonsterTab, DebugPanelSystemTab, DebugPanelUITab
    /// </summary>
    public abstract class DebugPanelTab
    {
        protected DebugPanel Owner { get; }

        protected DebugPanelTab(DebugPanel owner)
        {
            Owner = owner;
        }

        // Convenience accessors — avoid writing Owner._player in every handler
        protected Player Player => Owner._player;
        protected MonsterManager MonsterManager => Owner._monsterManager;
        protected NpcManager NpcManager => Owner._npcManager;
        protected GridManager GridManager => Owner._gridManager;
        protected CameraController Camera => Owner._camera;

        /// <summary>Unique key for this tab, used in config/undo dictionaries</summary>
        public abstract string TabKey { get; }

        /// <summary>Create all controls for this tab and add to tabContainer</summary>
        public abstract void BuildUI(VBoxContainer tabContainer);

        /// <summary>Connect all event subscriptions</summary>
        public abstract void ConnectSignals();

        /// <summary>Disconnect all event subscriptions</summary>
        public abstract void DisconnectSignals();

        /// <summary>Write current state to ConfigFile</summary>
        public abstract void SaveConfig(ConfigFile cfg);

        /// <summary>Restore state from ConfigFile</summary>
        public abstract void LoadConfig(ConfigFile cfg, bool configLoaded);

        /// <summary>When panel opens, sync sliders to current values</summary>
        public abstract void SyncToCurrentValues();

        /// <summary>Capture current undo state into a new dictionary</summary>
        public abstract Godot.Collections.Dictionary CaptureUndoState();

        /// <summary>Apply undo state from dictionary</summary>
        public abstract void ApplyUndoState(Godot.Collections.Dictionary state);

        /// <summary>Export config data for JSON output (optional, returns null by default)</summary>
        public virtual Godot.Collections.Dictionary ExportConfigData() => null;

        #region Shared Helpers — slider row creation
        protected (HSlider slider, Label valueLabel) CreateSliderRow(
            Container parent, string label, float min, float max, float def, float step = -1f)
        {
            float actualStep = step > 0 ? step : (max <= 1.0f ? 0.05f : 1f);
            var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            row.AddChild(new Label { Text = label + ":", CustomMinimumSize = new Vector2(80, 0) });

            var slider = new HSlider
            {
                MinValue = min, MaxValue = max, Value = def,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 20), Step = actualStep
            };
            row.AddChild(slider);

            string initialText = actualStep < 1.0f ? def.ToString("F1") : ((int)def).ToString();
            var valLbl = new Label { Text = initialText, CustomMinimumSize = new Vector2(36, 0) };
            row.AddChild(valLbl);

            slider.ValueChanged += (v) => valLbl.Text = (actualStep < 1.0f ? v.ToString("F1") : ((int)v).ToString());
            parent.AddChild(row);
            return (slider, valLbl);
        }

        protected (HSlider slider, Label valueLabel) CreateMonsterSliderRow(
            Container parent, string label, float min, float max, float def, float? step = null)
        {
            var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            row.AddChild(new Label { Text = label + ":", CustomMinimumSize = new Vector2(80, 0) });

            var slider = new HSlider
            {
                MinValue = min, MaxValue = max, Value = def,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 20),
                Step = step ?? (max <= 1 ? 0.05f : 1f)
            };
            row.AddChild(slider);

            var valLbl = new Label { Text = def.ToString("F1"), CustomMinimumSize = new Vector2(36, 0) };
            row.AddChild(valLbl);

            slider.ValueChanged += (v) => valLbl.Text = (max <= 1 ? v.ToString("F2") : ((int)v).ToString());
            parent.AddChild(row);
            return (slider, valLbl);
        }

        protected SpinBox CreateSpinBox(double minV, double maxV, double step, double value, int width)
        {
            var spin = new SpinBox
            {
                MinValue = minV, MaxValue = maxV, Step = step, Value = value,
                CustomMinimumSize = new Vector2(width, 0)
            };
            return spin;
        }
        #endregion
    }
}