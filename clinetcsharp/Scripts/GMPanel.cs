using Godot;

namespace ClinetCSharp
{
    /// <summary>
    /// GM panel shell and lifecycle.
    /// Feature-specific behavior lives in partial files to keep responsibilities local.
    /// </summary>
    public partial class GMPanel : DraggablePanel
    {
        private NetworkManager _network;

        private VBoxContainer _content;
        private LineEdit _cmdEdit;
        private ScrollContainer _groupScroll;
        private VBoxContainer _groupContainer;

        private HBoxContainer _addGroupRow;
        private LineEdit _addGroupEdit;

        private HBoxContainer _addCmdRow;
        private LineEdit _addCmdLabelEdit;
        private LineEdit _addCmdEdit;
        private GmGroup _addCmdTarget;
        private GmCommand _editCmdTarget;

        private PopupMenu _cmdMenu;
        private GmCommand _menuCmd;
        private GmGroup _menuGroup;

        protected override void OnPanelInitialized()
        {
            SetToggleKey(Key.F2);
            MinHeight = 300;

            _content = GetNodeOrNull<VBoxContainer>("VBoxContainer/Content");
            if (_content == null)
                return;

            BuildContent();
            if (!LoadGmConfig())
                AddDefaultGroups();
            else
                RebuildGroupUI();

            _network = UiServices.GetNetworkManager(this);
            if (_network != null)
                _network.GmResponse += OnGmResponse;
        }

        public override void _ExitTree()
        {
            if (_network != null)
                _network.GmResponse -= OnGmResponse;

            base._ExitTree();
        }

        protected override void OnClosed()
        {
            Visible = false;
        }

        protected internal override void NotifyFocusGained()
        {
            _cmdEdit?.GrabFocus();
        }
    }
}
