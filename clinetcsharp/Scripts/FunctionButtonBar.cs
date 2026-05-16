using Godot;

namespace ClinetCSharp
{
    /// <summary>
    /// 功能按钮栏 — 屏幕左上角的功能按钮条
    /// 可配置按钮位置、间距，在调试面板 UI 标签页中调整参数。
    /// </summary>
    public partial class FunctionButtonBar : CanvasLayer
    {
        [Export] public float OffsetX { get; set; } = 8;
        [Export] public float OffsetY { get; set; } = 8;
        [Export] public int ButtonSpacing { get; set; } = 3;
        [Export] public int FontSize { get; set; } = 12;

        private const string CONFIG_PATH = "user://function_bar_config.cfg";
        private const int CONFIG_VERSION = 1;

        private HBoxContainer _container;

        public override void _Ready()
        {
            AddToGroup("function_bar");
            Layer = 70;

            _container = new HBoxContainer();
            _container.AddThemeConstantOverride("separation", ButtonSpacing);
            _container.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
            AddChild(_container);

            BuildButtons();
            LoadConfig();
            ApplyLayout();
        }

        private void BuildButtons()
        {
            AddButton("调试面板", ToggleDebugPanel);
            AddButton("GM面板", ToggleGMPanel);
            AddButton("属性", ToggleCharacterPanel);
            AddButton("实体列表", ToggleEntityListPanel);
            AddButton("背包", ToggleInventory);
            AddButton("技能", ToggleSkillPanel);
            AddButton("战斗日志", ToggleIntegratedPanel);
        }

        private void AddButton(string text, System.Action onPressed)
        {
            // Per-button border wrapper
            var wrapper = new PanelContainer();
            wrapper.AddThemeStyleboxOverride("panel", new StyleBoxFlat
            {
                BgColor = new Color(0.05f, 0.05f, 0.05f, 0.65f),
                BorderColor = new Color(0.35f, 0.35f, 0.35f, 0.8f),
                BorderWidthBottom = 1,
                BorderWidthLeft = 1,
                BorderWidthRight = 1,
                BorderWidthTop = 1,
                CornerRadiusTopLeft = 3,
                CornerRadiusTopRight = 3,
                CornerRadiusBottomLeft = 3,
                CornerRadiusBottomRight = 3,
            });

            var btn = new Button
            {
                Text = text,
                FocusMode = Control.FocusModeEnum.None,
                Flat = true,
            };
            btn.AddThemeFontSizeOverride("font_size", FontSize);
            btn.AddThemeColorOverride("font_color", UiStyles.TextColor);
            btn.AddThemeColorOverride("font_hover_color", Colors.White);
            btn.AddThemeColorOverride("font_pressed_color", new Color(0.6f, 1f, 0.6f));
            btn.AddThemeStyleboxOverride("normal", new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0) });
            btn.AddThemeStyleboxOverride("hover", new StyleBoxFlat { BgColor = new Color(0.35f, 0.35f, 0.35f, 0.7f) });
            btn.AddThemeStyleboxOverride("pressed", new StyleBoxFlat { BgColor = new Color(0.2f, 0.5f, 0.2f, 0.75f) });

            btn.Pressed += () => onPressed();
            wrapper.AddChild(btn);
            _container.AddChild(wrapper);
        }

        public void ApplyLayout()
        {
            _container.AddThemeConstantOverride("separation", ButtonSpacing);
            _container.OffsetLeft = OffsetX;
            _container.OffsetTop = OffsetY;

            foreach (var wrapper in _container.GetChildren())
            {
                if (wrapper is PanelContainer pc && pc.GetChildCount() > 0 && pc.GetChild(0) is Button btn)
                    btn.AddThemeFontSizeOverride("font_size", FontSize);
            }
        }

        public void RebuildLayout()
        {
            ApplyLayout();
            SaveConfig();
        }

        #region Config
        private void SaveConfig()
        {
            var cfg = new ConfigFile();
            cfg.SetValue("layout", "offset_x", OffsetX);
            cfg.SetValue("layout", "offset_y", OffsetY);
            cfg.SetValue("layout", "spacing", ButtonSpacing);
            cfg.SetValue("meta", "version", CONFIG_VERSION);

            Error err = cfg.Save(CONFIG_PATH);
            if (err != Error.Ok)
                GD.PushError($"[FunctionButtonBar] Failed to save config: {err}");
        }

        private void LoadConfig()
        {
            var cfg = new ConfigFile();
            Error err = cfg.Load(CONFIG_PATH);
            if (err != Error.Ok) return;

            OffsetX = (float)(double)cfg.GetValue("layout", "offset_x", (double)OffsetX);
            OffsetY = (float)(double)cfg.GetValue("layout", "offset_y", (double)OffsetY);
            ButtonSpacing = (int)(double)cfg.GetValue("layout", "spacing", (double)ButtonSpacing);
        }
        #endregion

        #region Actions
        private void ToggleDebugPanel()
        {
            var dp = GetTree()?.GetFirstNodeInGroup("debug_panel") as IPanel;
            if (dp == null) return;
            if (dp.IsVisible()) dp.HidePanel();
            else dp.ShowPanel();
        }

        private void ToggleGMPanel()
        {
            PanelManager.Instance?.GetDraggablePanel<GMPanel>()?.Toggle();
        }

        private void ToggleCharacterPanel()
        {
            PanelManager.Instance?.GetDraggablePanel<CharacterPanel>()?.Toggle();
        }

        private void ToggleEntityListPanel()
        {
            PanelManager.Instance?.GetDraggablePanel<EntityListPanel>()?.Toggle();
        }

        private void ToggleInventory()
        {
            PanelManager.Instance?.GetDraggablePanel<InventoryUI>()?.Toggle();
        }

        private void ToggleSkillPanel()
        {
            PanelManager.Instance?.GetDraggablePanel<SkillPanel>()?.Toggle();
        }

        private void ToggleIntegratedPanel()
        {
            PanelManager.Instance?.GetDraggablePanel<IntegratedPanel>()?.Toggle();
        }
        #endregion
    }
}
