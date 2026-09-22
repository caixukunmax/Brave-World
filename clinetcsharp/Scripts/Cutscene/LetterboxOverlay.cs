using System.Threading.Tasks;
using Godot;

namespace ClinetCSharp.Cutscene
{
    /// <summary>
    /// 电影黑边（letterbox）— 上下两条黑边，支持动画展开/收起。
    /// 挂在 CutsceneDirector 下，ProcessMode=Always，演出暂停期间照常工作。
    /// </summary>
    public partial class LetterboxOverlay : CanvasLayer
    {
        /// <summary>单边黑边高度占屏幕高度的比例</summary>
        private const float BarRatio = 0.12f;

        private ColorRect _top;
        private ColorRect _bottom;
        private Tween _tween;

        public override void _Ready()
        {
            // 位于世界之上、常规 HUD/面板之下（ScreenTransition=200、UICanvas=100、FunctionButtonBar=70）
            Layer = 80;
            ProcessMode = ProcessModeEnum.Always;

            _top = CreateBar();
            _bottom = CreateBar();
            AddChild(_top);
            AddChild(_bottom);

            GetViewport().SizeChanged += OnViewportSizeChanged;
            OnViewportSizeChanged();
            SetBarsHeight(0f);
        }

        public override void _ExitTree()
        {
            GetViewport().SizeChanged -= OnViewportSizeChanged;
        }

        private static ColorRect CreateBar()
        {
            return new ColorRect
            {
                Color = Colors.Black,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
        }

        private void OnViewportSizeChanged()
        {
            var size = GetViewport().GetVisibleRect().Size;
            _top.SetAnchorsPreset(Control.LayoutPreset.TopWide);
            _bottom.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
            _top.CustomMinimumSize = new Vector2(size.X, 0);
            _bottom.CustomMinimumSize = new Vector2(size.X, 0);
            // 尺寸变化时按当前进度重排，避免黑边错位
            SetBarsHeight(_top.Size.Y);
        }

        private float TargetHeight => GetViewport().GetVisibleRect().Size.Y * BarRatio;

        private void SetBarsHeight(float height)
        {
            var size = GetViewport().GetVisibleRect().Size;
            _top.Position = Vector2.Zero;
            _top.Size = new Vector2(size.X, height);
            _bottom.Position = new Vector2(0, size.Y - height);
            _bottom.Size = new Vector2(size.X, height);
        }

        private Task AnimateTo(float targetHeight, float duration)
        {
            _tween?.Kill();
            if (duration <= 0f)
            {
                SetBarsHeight(targetHeight);
                return Task.CompletedTask;
            }

            var tcs = new TaskCompletionSource();
            _tween = CreateTween();
            _tween.SetTrans(Tween.TransitionType.Quad);
            _tween.SetEase(Tween.EaseType.InOut);
            _tween.TweenMethod(Callable.From<float>(SetBarsHeight), _top.Size.Y, targetHeight, duration);
            _tween.Finished += () => tcs.TrySetResult();
            return tcs.Task;
        }

        /// <summary>展开黑边（进入演出）</summary>
        public Task ShowAsync(float duration = 0.6f) => AnimateTo(TargetHeight, duration);

        /// <summary>收起黑边（退出演出）</summary>
        public Task HideAsync(float duration = 0.4f) => AnimateTo(0f, duration);

        /// <summary>立即收起（中断/跳过兜底，保证无残留）</summary>
        public void HideInstant()
        {
            _tween?.Kill();
            _tween = null;
            SetBarsHeight(0f);
        }
    }
}
