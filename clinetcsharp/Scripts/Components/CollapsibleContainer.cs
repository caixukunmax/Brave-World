using Godot;
using System;

namespace ClinetCSharp
{
    /// <summary>
    /// 可折叠容器 — VBoxContainer + 标题按钮 + 内容区
    /// 点击标题切换内容区可见性
    /// </summary>
    public partial class CollapsibleContainer : VBoxContainer
    {
        private readonly Button _titleBtn;
        private readonly HBoxContainer _headerRow;
        private readonly VBoxContainer _content;
        private bool _collapsed;

        public string Title
        {
            get => _titleBtn.Text;
            set => _titleBtn.Text = value;
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
                _titleBtn.Text = (_collapsed ? "▶ " : "▼ ") + _titleBtn.Text.Substring(2);
            }
        }

        public CollapsibleContainer(string title, bool collapsed = false)
        {
            _collapsed = collapsed;

            _headerRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            AddChild(_headerRow);

            _titleBtn = new Button
            {
                Text = (collapsed ? "▶ " : "▼ ") + title,
                SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
                Flat = true,
                FocusMode = FocusModeEnum.Click,
                CustomMinimumSize = new Vector2(0, 24),
            };
            _titleBtn.Pressed += OnTitlePressed;
            _headerRow.AddChild(_titleBtn);

            _content = new VBoxContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                Visible = !collapsed,
            };
            AddChild(_content);
        }

        private void OnTitlePressed()
        {
            Collapsed = !Collapsed;
        }

        /// <summary>设置折叠状态（不触发信号）</summary>
        public void SetCollapsedSilent(bool collapsed)
        {
            _collapsed = collapsed;
            _content.Visible = !collapsed;
            // 更新箭头符号
            string text = _titleBtn.Text;
            if (text.StartsWith("▶ ") || text.StartsWith("▼ "))
                text = text.Substring(2);
            _titleBtn.Text = (collapsed ? "▶ " : "▼ ") + text;
        }
    }
}
