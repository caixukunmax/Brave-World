using Godot;
using System;

namespace ClinetCSharp
{
    /// <summary>
    /// 障碍组件 — 控制建筑是否阻塞移动
    /// </summary>
    public class ObstacleComponent : IEntityTabComponent
    {
        public string ComponentName => "obstacle";
        public string DisplayName => "障碍";
        public Type DataType => typeof(ObstacleData);

        private Action _onChanged;
        private CheckButton _blockMovementCheck;

        public void BuildUI(VBoxContainer parent)
        {
            _blockMovementCheck = new CheckButton
            {
                Text = "阻塞移动",
                ButtonPressed = true,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            parent.AddChild(_blockMovementCheck);
        }

        public void SyncFromData(IComponentData data)
        {
            if (data is not ObstacleData d) return;
            _blockMovementCheck.SetBlockSignals(true);
            _blockMovementCheck.ButtonPressed = d.BlockMovement;
            _blockMovementCheck.SetBlockSignals(false);
        }

        public IComponentData SyncToData()
        {
            return new ObstacleData
            {
                BlockMovement = _blockMovementCheck.ButtonPressed,
            };
        }

        public void ConnectSignals(Action onChanged)
        {
            _onChanged = onChanged;
            _blockMovementCheck.Toggled += OnToggled;
        }

        public void DisconnectSignals()
        {
            _blockMovementCheck.Toggled -= OnToggled;
        }

        public void SyncFromEntity(EntityBase entity)
        {
            if (entity is not MapDecoration dec) return;
            _blockMovementCheck.SetBlockSignals(true);
            _blockMovementCheck.ButtonPressed = dec.BlockMovement;
            _blockMovementCheck.SetBlockSignals(false);
        }

        public void SetPropertyLocked(string propertyName, bool locked) { /* No lockable properties */ }
        public void SetCollapsed(bool collapsed) { /* CollapsibleContainer managed by DebugPanelEntityTab */ }
        public void Dispose() { DisconnectSignals(); }

        private void OnToggled(bool _) => _onChanged?.Invoke();
    }
}
