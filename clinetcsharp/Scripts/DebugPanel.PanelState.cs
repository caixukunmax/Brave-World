using Godot;

namespace ClinetCSharp
{
    public partial class DebugPanel
    {
        private void OnPanelVisibilityChanged()
        {
            if (!Visible || _tabs == null)
                return;

            foreach (var tab in _tabs)
                tab.SyncToCurrentValues();
        }

        private void OnEntityClicked(EntityBase entity)
        {
            GD.Print($"[DebugPanel] OnEntityClicked: {entity.GetType().Name}, visible={IsVisibleInTree()}");
            if (!IsVisibleInTree())
                return;

            SelectTab(_entityTab);
        }

        public void HidePanel()
        {
            Visible = false;
        }

        public bool IsFocused()
        {
            return Visible && PanelManager.Instance?.GetFocusedPanel() == this;
        }

        public bool IsMouseOverPanel()
        {
            return IsMouseOver();
        }
    }
}
