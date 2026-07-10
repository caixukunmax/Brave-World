using Godot;
using System;

namespace ClinetCSharp
{
    /// <summary>
    /// 酒馆内部面板。
    /// 全屏显示酒馆内部场景，提供退出按钮。
    /// </summary>
    public partial class TavernInteriorPanel : CanvasLayer
    {
        public static TavernInteriorPanel Instance { get; private set; }

        [Export] public float TransitionDuration { get; set; } = 0.4f;

        private PanelContainer _background;
        private bool _isOpen;

        public override void _Ready()
        {
            Instance = this;
            Layer = 105;
            Visible = false;
            ProcessMode = ProcessModeEnum.Always;

            BuildUi();
        }

        public override void _ExitTree()
        {
            if (Instance == this)
                Instance = null;
        }

        public override void _Input(InputEvent @event)
        {
            if (!_isOpen) return;

            if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Escape)
            {
                Exit();
                GetViewport()?.SetInputAsHandled();
            }
        }

        public void Enter()
        {
            if (_isOpen) return;
            _isOpen = true;

            UIInputPolicy.Instance?.PauseGame();

            ScreenTransition.Instance?.FadeToBlack(() =>
            {
                Visible = true;
                ScreenTransition.Instance?.FadeFromBlack(null, TransitionDuration);
            }, TransitionDuration);
        }

        public void Exit()
        {
            if (!_isOpen) return;

            ScreenTransition.Instance?.FadeToBlack(() =>
            {
                Visible = false;
                _isOpen = false;
                UIInputPolicy.Instance?.ResumeGame();
                ScreenTransition.Instance?.FadeFromBlack(null, TransitionDuration);
            }, TransitionDuration);
        }

        private void BuildUi()
        {
            _background = new PanelContainer();
            _background.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _background.AddThemeStyleboxOverride("panel", new StyleBoxFlat
            {
                BgColor = new Color(0.15f, 0.1f, 0.08f, 1.0f),
            });
            AddChild(_background);

            var margin = new MarginContainer();
            margin.AddThemeConstantOverride("margin_left", 40);
            margin.AddThemeConstantOverride("margin_top", 40);
            margin.AddThemeConstantOverride("margin_right", 40);
            margin.AddThemeConstantOverride("margin_bottom", 40);
            _background.AddChild(margin);

            var vbox = new VBoxContainer();
            vbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            vbox.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            margin.AddChild(vbox);

            // 顶部工具栏
            var toolbar = new HBoxContainer();
            vbox.AddChild(toolbar);

            var title = new Label
            {
                Text = "酒馆",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            title.AddThemeFontSizeOverride("font_size", 28);
            title.AddThemeColorOverride("font_color", new Color(0.95f, 0.85f, 0.6f));
            toolbar.AddChild(title);

            var exitBtn = new Button
            {
                Text = "离开",
                FocusMode = Control.FocusModeEnum.None,
            };
            exitBtn.Pressed += Exit;
            toolbar.AddChild(exitBtn);

            // 内容占位
            var content = new VBoxContainer();
            content.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            content.Alignment = BoxContainer.AlignmentMode.Center;
            vbox.AddChild(content);

            var placeholderLabel = new Label
            {
                Text = "温暖的火光映照着木质桌椅，空气中飘着麦酒香气。",
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            placeholderLabel.AddThemeFontSizeOverride("font_size", 16);
            placeholderLabel.AddThemeColorOverride("font_color", UiStyles.TextColor);
            content.AddChild(placeholderLabel);

            var buttons = new HBoxContainer();
            buttons.Alignment = BoxContainer.AlignmentMode.Center;
            content.AddChild(buttons);

            AddPlaceholderButton(buttons, "酒保");
            AddPlaceholderButton(buttons, "任务板");
            AddPlaceholderButton(buttons, "休息");
        }

        private void AddPlaceholderButton(Container parent, string text)
        {
            var btn = new Button
            {
                Text = text,
                FocusMode = Control.FocusModeEnum.None,
                CustomMinimumSize = new Vector2(100, 40),
            };
            btn.Pressed += () => GD.Print($"[TavernInteriorPanel] 点击了: {text}");
            parent.AddChild(btn);
        }
    }
}
