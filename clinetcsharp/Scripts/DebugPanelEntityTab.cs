using Godot;
using System;
using System.Collections.Generic;

namespace ClinetCSharp
{
    /// <summary>
    /// Debug panel entity tab. UI building, profile flows, component flows,
    /// and config persistence live in partial files.
    /// </summary>
    public partial class DebugPanelEntityTab : DebugPanelTab
    {
        private OptionButton _profileOption;
        private Button _addProfileBtn;
        private Button _deleteProfileBtn;
        private LineEdit _profileNameEdit;
        private int _currentProfileId = -1;

        private Button _manageComponentsBtn;
        private AcceptDialog _manageComponentsDialog;
        private Action _manageComponentsHandler;

        private VBoxContainer _componentContainer;
        private readonly Dictionary<string, IEntityTabComponent> _activeComponents = new();
        private readonly Dictionary<string, CollapsibleContainer> _componentContainers = new();

        private AcceptDialog _newProfileDialog;
        private LineEdit _newProfileNameEdit;
        private OptionButton _newProfileTemplateOption;
        private ConfirmationDialog _deleteProfileDialog;
        private ConfirmationDialog _deleteComponentDialog;
        private string _pendingDeleteComponentName;

        private Button _previewBtn;
        private EntityProfilePreviewPanel _previewPanel;
        private EntityBase _previewEntity;

        private bool _isRefreshing;
        private bool _showAllComponents;

        private int _undoProfileId = -1;
        private Dictionary<string, IComponentData> _undoComponentSnapshots = new();

        public DebugPanelEntityTab(DebugPanel owner) : base(owner) { }

        public override string TabKey => "entity";

        public override void ConnectSignals()
        {
            EntityBase.EntityClicked += OnEntityClicked;
        }

        public override void DisconnectSignals()
        {
            EntityBase.EntityClicked -= OnEntityClicked;
            if (_profileOption != null) _profileOption.ItemSelected -= OnProfileOptionSelected;
            if (_profileNameEdit != null) _profileNameEdit.TextChanged -= OnProfileNameChanged;
            if (_addProfileBtn != null) _addProfileBtn.Pressed -= OnAddProfilePressed;
            if (_deleteProfileBtn != null) _deleteProfileBtn.Pressed -= OnDeleteProfilePressed;
            if (_manageComponentsBtn != null) _manageComponentsBtn.Pressed -= OnManageComponentsPressed;
            if (_newProfileDialog != null) _newProfileDialog.Confirmed -= OnNewProfileConfirmed;
            if (_deleteProfileDialog != null) _deleteProfileDialog.Confirmed -= OnDeleteProfileConfirmed;
            if (_deleteComponentDialog != null)
            {
                _deleteComponentDialog.Confirmed -= OnDeleteComponentConfirmed;
                _deleteComponentDialog.Canceled -= OnDeleteComponentCanceled;
            }
            if (_previewBtn != null) _previewBtn.Pressed -= OnPreviewPressed;
            foreach (var component in _activeComponents.Values)
                component.DisconnectSignals();

            // 清理预览实体
            if (_previewEntity != null && GodotObject.IsInstanceValid(_previewEntity))
                _previewEntity.QueueFree();
            _previewEntity = null;
            _previewPanel = null;
        }

        private void OnDeleteComponentCanceled()
        {
            _pendingDeleteComponentName = null;
        }
    }
}
