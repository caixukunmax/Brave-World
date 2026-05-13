using Godot;

namespace ClinetCSharp
{
    /// <summary>
    /// Character attribute panel shell and lifecycle.
    /// Attribute definitions, row building, and actions live in partial files.
    /// </summary>
    public partial class CharacterPanel : DraggablePanel
    {
        private NetworkManager _network;
        private VBoxContainer _content;

        protected override void OnPanelInitialized()
        {
            _content = GetNodeOrNull<VBoxContainer>("VBoxContainer/Content");
            if (_content == null)
                return;

            BuildAttributeRows();
            BuildBottomActions();

            _network = UiServices.GetNetworkManager(this);
            if (_network != null)
                _network.RoleAttrUpdated += OnRoleAttrUpdated;

            Visible = false;
        }

        public override void _ExitTree()
        {
            if (_network != null)
                _network.RoleAttrUpdated -= OnRoleAttrUpdated;

            base._ExitTree();
        }

        protected internal override void NotifyFocusGained()
        {
            RefreshFromPlayer();
        }
    }
}
