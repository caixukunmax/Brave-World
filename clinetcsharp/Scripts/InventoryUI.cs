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

        // 物品颜色按品质
        private static readonly Color[] QualityColors = new Color[]
        {
            Colors.White,
            new Color(0.3f, 1, 0.3f),
            new Color(0.3f, 0.5f, 1),
            new Color(0.7f, 0.3f, 1),
            new Color(1, 0.8f, 0.2f),
        };

        protected override void OnPanelReady()
        {
            _content = GetNodeOrNull<VBoxContainer>("VBoxContainer/Content");
            if (_content == null) return;

            // 面板样式
            AddThemeStyleboxOverride("panel", new StyleBoxFlat
            {
                BgColor = new Color(0, 0, 0, 0.85f),
                BorderColor = new Color(0.2f, 0.2f, 0.2f),
                BorderWidthBottom = 1,
                BorderWidthLeft = 1,
                BorderWidthRight = 1,
                BorderWidthTop = 1,
            });

            var titleBar = GetNodeOrNull<PanelContainer>("VBoxContainer/TitleBar");
            if (titleBar != null)
                titleBar.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0.1f, 0.1f, 0.1f, 0.9f) });

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
                for (int i = 0; i < 20; i++)
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

            int emptySlots = 20 - (inv.Items?.Count ?? 0);
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
            popup.Position = (Vector2I)GetViewport().GetMousePosition();
            popup.Popup();
        }
    }
}
