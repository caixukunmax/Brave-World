using Godot;

namespace ClinetCSharp
{
    /// <summary>
    /// DebugPanel partial — panel-level event handlers.
    /// Tab-specific handlers now live in their respective tab classes.
    /// </summary>
    public partial class DebugPanel
    {
        #region Save / Discard
        private void OnMapSavePressed()
        {
            SaveConfig();
        }

        private void OnDiscardChangesPressed()
        {
            GD.Print("[DebugPanel] Discarding changes, reloading config");
            LoadConfig();
            ShowDiscardNotification();
        }

        private void ShowDiscardNotification()
        {
            GD.Print("[DebugPanel] Restored to last saved state");
        }
        #endregion

        #region UpdateControlStates
        /// <summary>
        /// Update editable/enabled states of controls across all tabs.
        /// Delegates to each tab for tab-specific control states.
        /// </summary>
        internal void UpdateControlStates()
        {
            // Delegate map-specific control states
            _mapTab?.UpdateControlStates();
        }
        #endregion
    }
}