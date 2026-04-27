using Godot;
using Protocol;

namespace ClinetCSharp
{
    /// <summary>
    /// 转职面板 - 显示三个职业选项，当前职业高亮
    /// 转职成功后自动关闭
    /// </summary>
    public partial class ChangeJobPanel : PanelContainer
    {
        private static readonly string[] Jobs = { "战士", "法师", "牧师" };
        private static readonly Color[] JobColors = {
            new Color(0.9f, 0.3f, 0.3f),  // 战士 - 红
            new Color(0.3f, 0.5f, 0.9f),  // 法师 - 蓝
            new Color(0.3f, 0.8f, 0.3f),  // 牧师 - 绿
        };

        private NetworkManager? _network;

        public override void _Ready()
        {
            _network = GetTree().Root.GetNodeOrNull<NetworkManager>("NetworkManager");
            if (_network != null)
                _network.ChangeJobResponse += OnChangeJobResponse;

            // 面板样式
            var styleBox = new StyleBoxFlat();
            styleBox.BgColor = new Color(0.08f, 0.08f, 0.15f, 0.95f);
            styleBox.BorderColor = new Color(0.3f, 0.5f, 0.9f);
            styleBox.BorderWidthTop = 2;
            styleBox.BorderWidthBottom = 2;
            styleBox.BorderWidthLeft = 2;
            styleBox.BorderWidthRight = 2;
            styleBox.CornerRadiusTopLeft = 8;
            styleBox.CornerRadiusTopRight = 8;
            styleBox.CornerRadiusBottomLeft = 8;
            styleBox.CornerRadiusBottomRight = 8;
            AddThemeStyleboxOverride("panel", styleBox);

            CustomMinimumSize = new Vector2(200, 180);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 8);

            // 标题
            var titleLabel = new Label();
            titleLabel.Text = "转职";
            titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
            titleLabel.AddThemeColorOverride("font_color", new Color(1, 0.85f, 0.3f));
            titleLabel.AddThemeFontSizeOverride("font_size", 16);
            vbox.AddChild(titleLabel);

            // 当前职业
            var currentJob = _network?.CachedRoleInfo?.Job ?? "";

            var currentLabel = new Label();
            currentLabel.Text = $"当前: {currentJob}";
            currentLabel.HorizontalAlignment = HorizontalAlignment.Center;
            currentLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
            currentLabel.AddThemeFontSizeOverride("font_size", 12);
            vbox.AddChild(currentLabel);

            // 职业按钮
            for (int i = 0; i < Jobs.Length; i++)
            {
                var job = Jobs[i];
                var btn = new Button();
                btn.Text = job;
                btn.AddThemeFontSizeOverride("font_size", 15);
                btn.CustomMinimumSize = new Vector2(160, 36);

                if (job == currentJob)
                {
                    // 当前职业高亮
                    btn.Disabled = true;
                    btn.AddThemeColorOverride("font_disabled_color", JobColors[i]);
                    var disabledStyle = new StyleBoxFlat();
                    disabledStyle.BgColor = new Color(JobColors[i].R, JobColors[i].G, JobColors[i].B, 0.3f);
                    disabledStyle.BorderColor = JobColors[i];
                    disabledStyle.BorderWidthTop = 1;
                    disabledStyle.BorderWidthBottom = 1;
                    disabledStyle.BorderWidthLeft = 1;
                    disabledStyle.BorderWidthRight = 1;
                    btn.AddThemeStyleboxOverride("disabled", disabledStyle);
                }
                else
                {
                    btn.AddThemeColorOverride("font_color", JobColors[i]);
                    var normalStyle = new StyleBoxFlat();
                    normalStyle.BgColor = new Color(0.15f, 0.15f, 0.25f, 0.9f);
                    normalStyle.BorderColor = new Color(JobColors[i].R, JobColors[i].G, JobColors[i].B, 0.5f);
                    normalStyle.BorderWidthTop = 1;
                    normalStyle.BorderWidthBottom = 1;
                    normalStyle.BorderWidthLeft = 1;
                    normalStyle.BorderWidthRight = 1;
                    normalStyle.CornerRadiusTopLeft = 4;
                    normalStyle.CornerRadiusTopRight = 4;
                    normalStyle.CornerRadiusBottomLeft = 4;
                    normalStyle.CornerRadiusBottomRight = 4;
                    btn.AddThemeStyleboxOverride("normal", normalStyle);

                    var hoverStyle = new StyleBoxFlat();
                    hoverStyle.BgColor = new Color(JobColors[i].R, JobColors[i].G, JobColors[i].B, 0.2f);
                    hoverStyle.BorderColor = JobColors[i];
                    hoverStyle.BorderWidthTop = 1;
                    hoverStyle.BorderWidthBottom = 1;
                    hoverStyle.BorderWidthLeft = 1;
                    hoverStyle.BorderWidthRight = 1;
                    hoverStyle.CornerRadiusTopLeft = 4;
                    hoverStyle.CornerRadiusTopRight = 4;
                    hoverStyle.CornerRadiusBottomLeft = 4;
                    hoverStyle.CornerRadiusBottomRight = 4;
                    btn.AddThemeStyleboxOverride("hover", hoverStyle);

                    var pressedStyle = new StyleBoxFlat();
                    pressedStyle.BgColor = new Color(JobColors[i].R, JobColors[i].G, JobColors[i].B, 0.4f);
                    pressedStyle.BorderColor = JobColors[i];
                    pressedStyle.BorderWidthTop = 2;
                    pressedStyle.BorderWidthBottom = 2;
                    pressedStyle.BorderWidthLeft = 2;
                    pressedStyle.BorderWidthRight = 2;
                    pressedStyle.CornerRadiusTopLeft = 4;
                    pressedStyle.CornerRadiusTopRight = 4;
                    pressedStyle.CornerRadiusBottomLeft = 4;
                    pressedStyle.CornerRadiusBottomRight = 4;
                    btn.AddThemeStyleboxOverride("pressed", pressedStyle);

                    var capturedJob = job;
                    btn.Pressed += () => OnChangeJobClicked(capturedJob);
                }

                vbox.AddChild(btn);
            }

            // 关闭按钮
            var closeBtn = new Button();
            closeBtn.Text = "关闭";
            closeBtn.AddThemeFontSizeOverride("font_size", 12);
            closeBtn.Pressed += () => Visible = false;
            vbox.AddChild(closeBtn);

            AddChild(vbox);

            // 居中显示
            Position = new Vector2(
                (GetViewport().GetVisibleRect().Size.X - CustomMinimumSize.X) / 2,
                (GetViewport().GetVisibleRect().Size.Y - CustomMinimumSize.Y) / 2
            );
        }

        public override void _ExitTree()
        {
            if (_network != null)
                _network.ChangeJobResponse -= OnChangeJobResponse;
        }

        private void OnChangeJobClicked(string targetJob)
        {
            GD.Print($"[ChangeJobPanel] Requesting job change to: {targetJob}");

            if (_network == null || !_network.IsServerConnected()) return;

            var req = new Game.ChangeJobRequest { TargetJob = targetJob };
            _network.SendPacket(MessageId.GameChangeJobReq, req);
        }

        private void OnChangeJobResponse(Game.ChangeJobResponse rsp)
        {
            if (rsp.Code == Common.ErrorCode.Success)
            {
                GD.Print($"[ChangeJobPanel] Job changed to: {rsp.CurrentJob}");
                // 转职成功，自动关闭面板
                Visible = false;
            }
            else
            {
                GD.Print($"[ChangeJobPanel] Job change failed: {rsp.Message}");
            }
        }
    }
}
