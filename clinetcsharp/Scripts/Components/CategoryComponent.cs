using Godot;
using System;

namespace ClinetCSharp
{
    /// <summary>
    /// 建筑分类组件 — 选择建筑在地图编辑器中的分组（建筑/地形/特殊/旧版兼容）。
    /// 分类影响地图编辑器的建筑列表筛选，修改后会持久化到 Profile 配置。
    /// </summary>
    public class CategoryComponent : IEntityTabComponent
    {
        public string ComponentName => "category";
        public string DisplayName => "分类";
        public Type DataType => typeof(CategoryData);

        private Action _onChanged;
        private OptionButton _categoryOption;

        // 分类取值与 MapEditor.Ui / MapEditor.Terrain 中筛选逻辑一致，禁止随意改名。
        private static readonly (string value, string name)[] CategoryOptions = new[]
        {
            ("Building", "建筑"),
            ("Terrain", "地形"),
            ("Special", "特殊"),
            ("Legacy", "旧版兼容"),
        };

        public void BuildUI(VBoxContainer parent)
        {
            var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            row.AddChild(new Label { Text = "分类:", CustomMinimumSize = new Vector2(48, 0) });

            _categoryOption = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            foreach (var c in CategoryOptions)
                _categoryOption.AddItem(c.name);
            row.AddChild(_categoryOption);

            parent.AddChild(row);
        }

        public void SyncFromData(IComponentData data)
        {
            if (data is not CategoryData d) return;
            _categoryOption.SetBlockSignals(true);
            int index = Array.FindIndex(CategoryOptions, c => c.value == d.Category);
            // 未知分类回退到第一个（建筑），避免 OptionButton 显示空
            _categoryOption.Select(index >= 0 ? index : 0);
            _categoryOption.SetBlockSignals(false);
        }

        public IComponentData SyncToData()
        {
            int index = _categoryOption.Selected;
            string value = index >= 0 && index < CategoryOptions.Length
                ? CategoryOptions[index].value
                : "Building";
            return new CategoryData
            {
                Category = value,
            };
        }

        public void ConnectSignals(Action onChanged)
        {
            _onChanged = onChanged;
            _categoryOption.ItemSelected += OnItemSelected;
        }

        public void DisconnectSignals()
        {
            if (_categoryOption != null)
                _categoryOption.ItemSelected -= OnItemSelected;
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
