using Godot;
using System.Collections.Generic;

namespace ClinetCSharp
{
    /// <summary>
    /// 背包面板 — 继承 DraggablePanel，按 I 键开关。
    /// 网格布局显示物品，支持使用/丢弃操作。
    /// </summary>
    public partial class InventoryUI : DraggablePanel
    {
        private VBoxContainer _content;
        private GridContainer _grid;
        private Label _titleLabel;

        private const int DEFAULT_MAX_SLOTS = 20;

        protected override void OnPanelInitialized()
        {
            _content = GetNodeOrNull<VBoxContainer>("VBoxContainer/Content");
            if (_content == null) return;

            // 居中定位
            var vpSize = GetViewport().GetVisibleRect().Size;
            Position = new Vector2((vpSize.X - Size.X) / 2, (vpSize.Y - Size.Y) / 2);

            BuildContent();

            var inv = GetNodeOrNull<InventoryManager>("/root/Main/InventoryManager");
            if (inv != null)
                inv.InventoryChanged += OnInventoryChanged;

            SetToggleKey(Key.I);
        }

        public override void _ExitTree()
        {
            var inv = GetNodeOrNull<InventoryManager>("/root/Main/InventoryManager");
            if (inv != null)
                inv.InventoryChanged -= OnInventoryChanged;
            base._ExitTree();
        }

        private void BuildContent()
        {
            // 分隔线
            _content.AddChild(new HSeparator());

            // 网格容器
            _grid = new GridContainer
            {
                Columns = 5,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            };
            _content.AddChild(_grid);
        }

        protected internal override void NotifyFocusGained()
        {
            RefreshGrid();
        }

        private void OnInventoryChanged()
        {
            if (Visible)
                RefreshGrid();
        }

        private void RefreshGrid()
        {
            if (_grid == null) return;

            foreach (var child in _grid.GetChildren())
                child.QueueFree();

            var inv = GetNodeOrNull<InventoryManager>("/root/Main/InventoryManager");
            if (inv == null)
            {
                for (int i = 0; i < DEFAULT_MAX_SLOTS; i++)
                {
                    var empty = new Button
                    {
                        CustomMinimumSize = new Vector2(70, 70),
                        Disabled = true,
                    };
                    _grid.AddChild(empty);
                }
                return;
            }

            foreach (var slot in inv.Items)
            {
                var btn = new Button
                {
                    CustomMinimumSize = new Vector2(70, 70),
                    Text = $"{slot.Name}\n×{slot.Count}",
                    ClipText = true,
                };

                btn.AddThemeColorOverride("font_color", Colors.White);
                btn.AddThemeColorOverride("font_hover_color", Colors.Yellow);

                var itemId = slot.ItemId;
                var count = slot.Count;
                btn.Pressed += () => OnItemClicked(itemId, count);

                _grid.AddChild(btn);
            }

            int emptySlots = DEFAULT_MAX_SLOTS - (inv.Items?.Count ?? 0);
            for (int i = 0; i < emptySlots; i++)
            {
                var empty = new Button
                {
                    CustomMinimumSize = new Vector2(70, 70),
                    Disabled = true,
                };
                empty.AddThemeColorOverride("font_disabled_color", new Color(0.5f, 0.5f, 0.5f, 0.3f));
                _grid.AddChild(empty);
            }
        }

        private void OnItemClicked(uint itemId, uint count)
        {
            var popup = new PopupMenu();
            popup.AddItem("使用 x1", 0);
            popup.AddItem("丢弃 x1", 1);

            popup.IdPressed += (id) =>
            {
                var inv = GetNodeOrNull<InventoryManager>("/root/Main/InventoryManager");
                if (inv == null) return;

                if (id == 0)
                    inv.SendUseItem(itemId, 1);
                else if (id == 1)
                    inv.SendDropItem(itemId, 1);

                popup.QueueFree();
            };

            popup.PopupHide += () => popup.QueueFree();

            AddChild(popup);
            popup.Position = new Vector2I(
                Mathf.RoundToInt(GetViewport().GetMousePosition().X),
                Mathf.RoundToInt(GetViewport().GetMousePosition().Y));
            popup.Popup();
        }
    }
}
