using System.Collections.Generic;
using Godot;

namespace ClinetCSharp.Cutscene
{
    /// <summary>
    /// 头顶气泡层 — 在实体头顶显示文字/表情符号气泡。
    /// 气泡跟随实体（每帧按 CanvasTransform 换算屏幕坐标），到时自动消失。
    /// 表情符号（! ? … 等约定符号，≤2 字符视为符号）放大显示，零美术资产。
    /// </summary>
    public partial class BubbleLayer : CanvasLayer
    {
        private const float FollowOffsetY = 96f; // 气泡底部相对实体中心的上抬像素

        private class Bubble
        {
            public PanelContainer Root;
            public Node2D Target;
            public float Remaining;
        }

        private readonly List<Bubble> _bubbles = new();

        public override void _Ready()
        {
            // 略高于黑边，低于 UICanvas(100) 与 ScreenTransition(200)
            Layer = 90;
            ProcessMode = ProcessModeEnum.Always;
        }

        /// <summary>在目标实体头顶弹出一个气泡；同一目标只保留一个（新气泡替换旧的）</summary>
        public void ShowBubble(Node2D target, string text, float duration)
        {
            if (target == null || !IsInstanceValid(target) || string.IsNullOrEmpty(text))
                return;

            RemoveBubbleOf(target);

            bool isSymbol = text.Trim().Length <= 2;
            var label = new Label
            {
                Text = text,
                HorizontalAlignment = HorizontalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            label.AddThemeFontSizeOverride("font_size", isSymbol ? 44 : 20);
            if (!isSymbol)
                label.AddThemeColorOverride("font_color", new Color(0.95f, 0.95f, 0.95f));

            var panel = new PanelContainer
            {
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            var style = new StyleBoxFlat
            {
                BgColor = isSymbol ? new Color(0, 0, 0, 0) : new Color(0.08f, 0.08f, 0.08f, 0.85f),
                BorderColor = new Color(0.6f, 0.6f, 0.6f, isSymbol ? 0f : 0.8f),
                BorderWidthBottom = isSymbol ? 0 : 2,
                BorderWidthLeft = isSymbol ? 0 : 2,
                BorderWidthRight = isSymbol ? 0 : 2,
                BorderWidthTop = isSymbol ? 0 : 2,
                CornerRadiusTopLeft = 8,
                CornerRadiusTopRight = 8,
                CornerRadiusBottomLeft = 8,
                CornerRadiusBottomRight = 8,
                ContentMarginLeft = 10,
                ContentMarginRight = 10,
                ContentMarginTop = 4,
                ContentMarginBottom = 4,
            };
            panel.AddThemeStyleboxOverride("panel", style);
            panel.AddChild(label);
            AddChild(panel);

            _bubbles.Add(new Bubble
            {
                Root = panel,
                Target = target,
                Remaining = duration > 0f ? duration : 2f,
            });

            UpdateBubblePosition(_bubbles[_bubbles.Count - 1]);
        }

        /// <summary>清空全部气泡（演出退出/跳过兜底，保证无残留）</summary>
        public void ClearAll()
        {
            foreach (var bubble in _bubbles)
            {
                if (bubble.Root != null && IsInstanceValid(bubble.Root))
                    bubble.Root.QueueFree();
            }
            _bubbles.Clear();
        }

        public override void _Process(double delta)
        {
            for (int i = _bubbles.Count - 1; i >= 0; i--)
            {
                var bubble = _bubbles[i];
                bool invalid = bubble.Target == null || !IsInstanceValid(bubble.Target)
                    || bubble.Root == null || !IsInstanceValid(bubble.Root);

                bubble.Remaining -= (float)delta;
                if (invalid || bubble.Remaining <= 0f)
                {
                    if (!invalid)
                        bubble.Root.QueueFree();
                    _bubbles.RemoveAt(i);
                    continue;
                }

                UpdateBubblePosition(bubble);
            }
        }

        private void UpdateBubblePosition(Bubble bubble)
        {
            // 世界坐标 → 屏幕坐标，锚定实体头顶
            var screenPos = GetViewport().CanvasTransform * bubble.Target.GlobalPosition;
            var panel = bubble.Root;
            var size = panel.Size;
            panel.Position = new Vector2(
                screenPos.X - size.X / 2f,
                screenPos.Y - FollowOffsetY - size.Y);
        }

        private void RemoveBubbleOf(Node2D target)
        {
            for (int i = _bubbles.Count - 1; i >= 0; i--)
            {
                if (_bubbles[i].Target != target) continue;
                if (_bubbles[i].Root != null && IsInstanceValid(_bubbles[i].Root))
                    _bubbles[i].Root.QueueFree();
                _bubbles.RemoveAt(i);
            }
        }
    }
}
