using Godot;
using System;
using System.Linq;

namespace ClinetCSharp
{
    /// <summary>
    /// 建筑类型组件 — 选择建筑分类。
    /// </summary>
    public class BuildingTypeComponent : IEntityTabComponent
    {
        public string ComponentName => "building_type";
        public string DisplayName => "建筑类型";
        public Type DataType => typeof(BuildingTypeData);

        private Action _onChanged;
        private OptionButton _typeOption;

        private static readonly (int type, string name)[] TypeOptions = BuildingType.GetAllTypes()
            .Select(type => (type, BuildingType.GetDisplayName(type)))
            .ToArray();

        public void BuildUI(VBoxContainer parent)
        {
            var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            row.AddChild(new Label { Text = "类型:", CustomMinimumSize = new Vector2(48, 0) });

            _typeOption = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            foreach (var t in TypeOptions)
                _typeOption.AddItem(t.name);
            row.AddChild(_typeOption);

            parent.AddChild(row);
        }

        public void SyncFromData(IComponentData data)
        {
            if (data is not BuildingTypeData d) return;
            _typeOption.SetBlockSignals(true);
            for (int i = 0; i < _typeOption.GetItemCount(); i++)
            {
                if (TypeOptions[i].type == d.Type)
                {
                    _typeOption.Select(i);
                    break;
                }
            }
            _typeOption.SetBlockSignals(false);
        }

        public IComponentData SyncToData()
        {
            int index = _typeOption.Selected;
            int type = index >= 0 && index < TypeOptions.Length ? TypeOptions[index].type : BuildingType.House;
            return new BuildingTypeData
            {
                Type = type,
            };
        }

        public void ConnectSignals(Action onChanged)
        {
            _onChanged = onChanged;
            _typeOption.ItemSelected += OnItemSelected;
        }

        public void DisconnectSignals()
        {
            _typeOption.ItemSelected -= OnItemSelected;
        }

        public void SyncFromEntity(EntityBase entity)
        {
            // 数据来自 Profile，不需要从实体同步
        }

        public void SetPropertyLocked(string propertyName, bool locked) { /* No lockable properties */ }
        public void SetCollapsed(bool collapsed) { /* CollapsibleContainer managed by DebugPanelEntityTab */ }
        public void Dispose() { DisconnectSignals(); }

        private void OnItemSelected(long _) => _onChanged?.Invoke();
    }
}
