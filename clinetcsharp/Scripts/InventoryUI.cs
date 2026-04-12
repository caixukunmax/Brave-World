using Godot;
using System.Collections.Generic;

namespace ClinetCSharp
{
    /// <summary>
    /// 背包 UI - 网格布局显示物品，支持使用/丢弃操作
    /// </summary>
    public partial class InventoryUI : CanvasLayer
    {
        private Panel _panel;
        private GridContainer _grid;
        private Label _titleLabel;
        private bool _isVisible = false;

        // 物品颜色按品质
        private static readonly Color[] QualityColors = new Color[]
        {
            Colors.White,    // 0 白
            new Color(0.3f, 1, 0.3f),  // 1 绿
            new Color(0.3f, 0.5f, 1),   // 2 蓝
            new Color(0.7f, 0.3f, 1),   // 3 紫
            new Color(1, 0.8f, 0.2f),    // 4 金
        };

        public override void _Ready()
        {
            GD.Print("[InventoryUI] _Ready() called");
            BuildUI();

            var inv = GetNodeOrNull<InventoryManager>("/root/Main/InventoryManager");
            if (inv != null)
            {
                inv.InventoryChanged += OnInventoryChanged;
                GD.Print("[InventoryUI] Connected to InventoryManager");
            }
            else
            {
                GD.PrintErr("[InventoryUI] InventoryManager not found!");
            }
        }

        public override void _Input(InputEvent @event)
        {
            if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.I)
            {
                GD.Print("[InventoryUI] I key pressed, toggling");
                Toggle();
                GetViewport().SetInputAsHandled();
            }
        }

        private void BuildUI()
        {
            var viewportSize = GetViewport().GetVisibleRect().Size;
            var panelWidth = 420f;
            var panelHeight = 520f;

            _panel = new Panel();
            _panel.Position = new Vector2((viewportSize.X - panelWidth) / 2, (viewportSize.Y - panelHeight) / 2);
            _panel.Size = new Vector2(panelWidth, panelHeight);

            var vbox = new VBoxContainer();
            vbox.SetAnchorsPreset((int)Control.LayoutPreset.FullRect);
            vbox.OffsetLeft = 8;
            vbox.OffsetTop = 8;
            vbox.OffsetRight = -8;
            vbox.OffsetBottom = -8;

            // 标题
            _titleLabel = new Label
            {
                Text = "背包",
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            _titleLabel.AddThemeFontSizeOverride("font_size", 18);
            vbox.AddChild(_titleLabel);

            // 分隔线
            var sep = new HSeparator();
            vbox.AddChild(sep);

            // 网格容器
            _grid = new GridContainer
            {
                Columns = 5,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            };
            vbox.AddChild(_grid);

            // 关闭按钮
            var closeBtn = new Button
            {
                Text = "关闭",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            closeBtn.Pressed += () => Toggle();
            vbox.AddChild(closeBtn);

            _panel.AddChild(vbox);
            AddChild(_panel);

            _panel.Visible = false;
        }

        private void Toggle()
        {
            _isVisible = !_isVisible;
            _panel.Visible = _isVisible;
            if (_isVisible)
                RefreshGrid();
        }

        private void OnInventoryChanged()
        {
            if (_isVisible)
                RefreshGrid();
        }

        private void RefreshGrid()
        {
            // 清空格子
            foreach (var child in _grid.GetChildren())
                child.QueueFree();

            var inv = GetNodeOrNull<InventoryManager>("/root/Main/InventoryManager");
            if (inv == null)
            {
                GD.PrintErr("[InventoryUI] RefreshGrid: InventoryManager not found");
                // 填充空格子
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

            GD.Print($"[InventoryUI] RefreshGrid: {inv.Items.Count} items");

            foreach (var slot in inv.Items)
            {
                var btn = new Button
                {
                    CustomMinimumSize = new Vector2(70, 70),
                    Text = $"{slot.Name}\n×{slot.Count}",
                    ClipText = true,
                };

                // 品质颜色
                btn.AddThemeColorOverride("font_color", Colors.White);
                btn.AddThemeColorOverride("font_hover_color", Colors.Yellow);

                var itemId = slot.ItemId;
                var count = slot.Count;
                btn.Pressed += () => OnItemClicked(itemId, count);

                _grid.AddChild(btn);
            }

            // 填充空格子（最多 20 格）
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
            // 简单操作菜单：使用/丢弃各 1 个
            var popup = new PopupMenu();
            popup.AddItem("使用 ×1", 0);
            popup.AddItem("丢弃 ×1", 1);

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
