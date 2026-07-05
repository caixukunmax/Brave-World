using Godot;
using System;

namespace ClinetCSharp
{
    /// <summary>
    /// 可折叠卡片容器 — PanelContainer 卡片 + 标题栏 + 内容区
    /// 每个组件独立成卡，带颜色标题栏与左边框，便于在调试面板中快速识别。
    /// </summary>
    public partial class CollapsibleContainer : VBoxContainer
    {
        private readonly PanelContainer _cardPanel;
        private readonly PanelContainer _headerPanel;
        private readonly HBoxContainer _headerRow;
        private readonly Label _iconLabel;
        private readonly Button _titleBtn;
        private readonly VBoxContainer _content;
        private bool _collapsed;

        private Color _accentColor = new Color(0.35f, 0.35f, 0.35f);
        private string _titleText;

        public string Title
        {
            get => _titleText;
            set
            {
                _titleText = value;
                UpdateTitleText();
            }
        }

        public VBoxContainer Content => _content;

        /// <summary>标题行 HBoxContainer，可在此添加右侧按钮</summary>
        public HBoxContainer HeaderRow => _headerRow;

        public bool Collapsed
        {
            get => _collapsed;
            set
            {
                _collapsed = value;
                _content.Visible = !_collapsed;
                UpdateTitleText();
            }
        }

        public CollapsibleContainer(string title, bool collapsed = false)
        {
            _titleText = title;
            _collapsed = collapsed;
            SizeFlagsHorizontal = SizeFlags.ExpandFill;

            // 外层卡片
            _cardPanel = new PanelContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            AddChild(_cardPanel);

            var body = new VBoxContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            _cardPanel.AddChild(body);

            // 标题栏
            _headerPanel = new PanelContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 28),
            };
            body.AddChild(_headerPanel);

            _headerRow = new HBoxContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ShrinkCenter,
            };
            _headerPanel.AddChild(_headerRow);

            _iconLabel = new Label
            {
                Text = "",
                CustomMinimumSize = new Vector2(20, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Modulate = new Color(1.2f, 1.2f, 1.2f),
            };
            _headerRow.AddChild(_iconLabel);

            _titleBtn = new Button
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ShrinkCenter,
                Flat = true,
                FocusMode = FocusModeEnum.Click,
                Alignment = HorizontalAlignment.Left,
            };
            _titleBtn.Pressed += OnTitlePressed;
            _headerRow.AddChild(_titleBtn);

            // 内容区
            _content = new VBoxContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                Visible = !_collapsed,
            };
            _content.AddThemeConstantOverride("separation", 6);
            body.AddChild(_content);

            ApplyDefaultCardStyle();
            ApplyHeaderStyle(_accentColor);
            UpdateTitleText();
        }

        private void OnTitlePressed()
        {
            Collapsed = !Collapsed;
        }

        /// <summary>设置折叠状态（不触发信号）</summary>
        public void SetCollapsedSilent(bool collapsed)
        {
            _collapsed = collapsed;
            _content.Visible = !_collapsed;
            UpdateTitleText();
        }

        /// <summary>设置标题栏强调色与左边框色</summary>
        public void SetAccentColor(Color color)
        {
            _accentColor = color;
            ApplyHeaderStyle(color);
            ApplyCardBorder(color);
        }

        /// <summary>设置标题栏左侧图标（emoji 或单个字符）</summary>
        public void SetHeaderIcon(string icon)
        {
            _iconLabel.Text = icon ?? "";
        }

        private void UpdateTitleText()
        {
            _titleBtn.Text = (_collapsed ? "▶ " : "▼ ") + _titleText;
        }

        private void ApplyDefaultCardStyle()
        {
            var cardStyle = new StyleBoxFlat
            {
                BgColor = new Color(0.10f, 0.10f, 0.10f, 0.75f),
                BorderColor = new Color(0.25f, 0.25f, 0.25f, 0.90f),
                BorderWidthLeft = 1,
                BorderWidthTop = 1,
                BorderWidthRight = 1,
                BorderWidthBottom = 1,
                CornerRadiusTopLeft = 6,
                CornerRadiusTopRight = 6,
                CornerRadiusBottomRight = 6,
                CornerRadiusBottomLeft = 6,
                ContentMarginLeft = 8,
                ContentMarginTop = 6,
                ContentMarginRight = 8,
                ContentMarginBottom = 8,
            };
            _cardPanel.AddThemeStyleboxOverride("panel", cardStyle);
        }

        private void ApplyHeaderStyle(Color color)
        {
            var headerStyle = new StyleBoxFlat
            {
                BgColor = new Color(color.R * 0.35f, color.G * 0.35f, color.B * 0.35f, 0.55f),
                BorderColor = color,
                BorderWidthBottom = 2,
                CornerRadiusTopLeft = 5,
                CornerRadiusTopRight = 5,
                ContentMarginLeft = 4,
                ContentMarginTop = 2,
                ContentMarginRight = 4,
                ContentMarginBottom = 2,
            };
            _headerPanel.AddThemeStyleboxOverride("panel", headerStyle);
        }

        private void ApplyCardBorder(Color color)
        {
            if (_cardPanel.GetThemeStylebox("panel") is not StyleBoxFlat cardStyle)
                return;

            cardStyle.BorderColor = new Color(color.R, color.G, color.B, 0.55f);
            cardStyle.BorderWidthLeft = 3;
        }
    }
}
