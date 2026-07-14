using Godot;
using System;

namespace ClinetCSharp
{
    /// <summary>
    /// 全屏淡入淡出过渡层。
    /// 用于进门、切换场景等需要黑屏过渡的效果。
    /// </summary>
    public partial class ScreenTransition : CanvasLayer, IPanel
    {
        /// <summary>获取全屏过渡层实例。优先从 PanelManager 查询，确保生命周期受统一管理。</summary>
        public static ScreenTransition Get() => PanelManager.Instance?.GetPanel<ScreenTransition>();

        [Export] public float DefaultDuration { get; set; } = 0.4f;

        private ColorRect _overlay;
        private Tween _activeTween;
        private bool _isFading;

        public bool IsFading => _isFading;

        public override void _Ready()
        {
            Layer = 200;
            ProcessMode = ProcessModeEnum.Always;

            _overlay = new ColorRect();
            _overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _overlay.Color = new Color(0, 0, 0, 0);
            _overlay.MouseFilter = Control.MouseFilterEnum.Ignore;
            AddChild(_overlay);

            CallDeferred(MethodName.RegisterWithPanelManager);
        }

        public override void _ExitTree()
        {
            PanelManager.Instance?.UnregisterPanel(this);
        }

        private void RegisterWithPanelManager()
        {
            PanelManager.Instance?.RegisterPanel(this);
        }

        /// <summary>淡入到黑屏，黑屏后调用 onBlack。</summary>
        public void FadeToBlack(Action onBlack = null, float? duration = null)
        {
            FadeTo(new Color(0, 0, 0, 1), onBlack, duration);
        }

        /// <summary>从黑屏淡出，结束后调用 onFinished。</summary>
        public void FadeFromBlack(Action onFinished = null, float? duration = null)
        {
            FadeTo(new Color(0, 0, 0, 0), onFinished, duration);
        }

        /// <summary>从当前颜色渐变到目标颜色，到达目标后调用回调。</summary>
        private void FadeTo(Color targetColor, Action onFinished, float? duration)
        {
            if (_overlay == null) return;

            _activeTween?.Kill();
            _activeTween = CreateTween();
            _activeTween.SetTrans(Tween.TransitionType.Quad);
            _activeTween.SetEase(Tween.EaseType.InOut);

            float dur = duration ?? DefaultDuration;
            _isFading = true;

            _activeTween.TweenProperty(_overlay, "color", targetColor, dur);
            _activeTween.Finished += () =>
            {
                _isFading = false;
                onFinished?.Invoke();
            };
        }

        #region IPanel Implementation
        bool IPanel.IsVisible() => Visible;
        void IPanel.ShowPanel() => Visible = true;
        void IPanel.HidePanel() => Visible = false;
        string IPanel.PanelName => "ScreenTransition";
        #endregion
    }
}
