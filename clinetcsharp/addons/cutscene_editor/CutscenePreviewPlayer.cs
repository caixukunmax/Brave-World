using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ClinetCSharp.Cutscene;

namespace ClinetCSharp
{
    /// <summary>
    /// 演出编辑器模拟预览播放器（§5.4）：在地图视口内按真实时序把整场演出"演"出来。
    ///
    /// 本身是一个叠在 SubViewportContainer（地图视图）上的全屏透明 Control（MouseFilter=Ignore，
    /// 不挡地图平移/缩放），负责：
    ///   - 执行引擎：按 BuildExecutionGroups 逐组执行、组内并行（Task.WhenAll），
    ///     时长/等待语义对照运行时 CueHandlers；
    ///   - 镜头即视图：camera_* cue 直接 Tween 地图视口的 Camera2D；
    ///   - 演员令牌：spawn/move/face/despawn 用色块令牌表现（挂在 SubViewport 世界内）；
    ///   - 幕布与黑边：fade 叠黑色渐变 ColorRect，letterbox 叠上下黑条；
    ///   - 对白：视口底部文字条，点击/空格继续（预览中唯一需要交互的 cue）。
    ///
    /// 预览不改脚本数据；npc:/monster:/player 演员的 cue 跳过并黄字提示；sfx/bgm 只记日志。
    /// 停止时还原进入预览时的视图机位、清掉全部令牌/气泡/幕布/黑边。
    /// </summary>
    [Tool]
    public partial class CutscenePreviewPlayer : Control
    {
        public enum PreviewState
        {
            Stopped,
            Playing,
            Paused,
        }

        /// <summary>预览状态（Stopped/Playing/Paused）</summary>
        public PreviewState State { get; private set; } = PreviewState.Stopped;
        /// <summary>预览进行中（播放或暂停都算）</summary>
        public bool IsActive => State != PreviewState.Stopped;
        /// <summary>是否有对白正在等待用户继续（冒烟/自动化用）</summary>
        public bool IsDialogueWaiting => _dialogueTcs != null;
        /// <summary>状态每次切换时触发（主面板刷新按钮可用性）</summary>
        public event Action StateChanged;

        // ============ 外部依赖（Initialize 注入） ============
        private SubViewport _sub;
        private Camera2D _camera;
        private CutsceneMapRenderer _renderer;
        private Func<CutsceneScript> _getScript;
        private CutsceneScript _previewScript;   // BeginPreview 时缓存，供登场解析演员表定义
        private Action<string> _loadViewMap;
        private Action<string, Color> _setStatus;
        private Action<int> _stepChanged;       // 当前执行组下标（0 基），-1 = 无
        private Action<bool> _activeChanged;    // 预览开始/结束（主面板切只读）

        // ============ 引擎状态 ============
        private List<List<CutsceneCue>> _groups;
        private int _groupIndex;
        private bool _stopRequested;
        private bool _pauseAfterGroup;          // 单步：跑完当前组后回到暂停
        private TaskCompletionSource _cancelTcs;

        // 相机快照与"家"（camera_reset 目标）
        private Vector2 _savedCamPos;
        private Vector2 _savedCamZoom;
        private Vector2 _homePos;

        // ============ 覆盖层 ============
        private ColorRect _fadeRect;
        private ColorRect _letterboxTop;
        private ColorRect _letterboxBottom;
        private PanelContainer _dialogueBar;
        private Label _dialogueLabel;
        private Control _dialogueClickCatcher;
        private TaskCompletionSource _dialogueTcs;

        // ============ 演员令牌 ============
        private Node2D _tokenLayer;             // 挂在 SubViewport 内（世界坐标，随镜头动）
        private readonly Dictionary<string, CutscenePreviewActorToken> _tokens = new();
        private readonly List<Tween> _activeTweens = new();

        /// <summary>注入主面板持有的视图组件与回调。调用方需先把本节点加为地图视图的子节点。</summary>
        public void Initialize(
            SubViewport sub, Camera2D camera, CutsceneMapRenderer renderer,
            Func<CutsceneScript> getScript, Action<string> loadViewMap,
            Action<string, Color> setStatus, Action<int> stepChanged, Action<bool> activeChanged)
        {
            _sub = sub;
            _camera = camera;
            _renderer = renderer;
            _getScript = getScript;
            _loadViewMap = loadViewMap;
            _setStatus = setStatus;
            _stepChanged = stepChanged;
            _activeChanged = activeChanged;

            MouseFilter = MouseFilterEnum.Ignore;
            SetAnchorsPreset(LayoutPreset.FullRect);
        }

        // ============ 播放控制（供主面板按钮/冒烟调用） ============

        /// <summary>播放：停止态则从头开始，暂停态则继续</summary>
        public void Play()
        {
            if (State == PreviewState.Playing) return;
            if (State == PreviewState.Stopped && !BeginPreview())
                return;
            _pauseAfterGroup = false;
            SetState(PreviewState.Playing);
        }

        /// <summary>暂停（冻结等待计时与所有 Tween）</summary>
        public void PausePreview()
        {
            if (State != PreviewState.Playing) return;
            SetState(PreviewState.Paused);
            foreach (var t in _activeTweens)
                if (t != null && t.IsValid() && t.IsRunning())
                    t.Pause();
            _setStatus?.Invoke("预览已暂停", Colors.Yellow);
        }

        /// <summary>单步：前进一个执行组后停在暂停态</summary>
        public void StepOnce()
        {
            if (State == PreviewState.Playing)
            {
                _setStatus?.Invoke("播放中：请先暂停再单步", Colors.Yellow);
                return;
            }
            if (State == PreviewState.Stopped && !BeginPreview())
                return;
            _pauseAfterGroup = true;
            SetState(PreviewState.Playing);
        }

        /// <summary>停止：清理全部预览视觉并还原视图机位</summary>
        public void StopPreview()
        {
            if (State == PreviewState.Stopped) return;
            _stopRequested = true;
            _cancelTcs?.TrySetResult();
            FinishPreview(natural: false);
        }

        /// <summary>对白翻页（点击对白条 / 空格 / 回车 / 冒烟自动翻页都会走到这里）。
        /// 与运行时一致：把当前行标记结束，等待循环立即进入下一行（自动播放时等于"加速"）。</summary>
        public void ContinueDialogue()
        {
            _dialogueTcs = null;
        }

        private void SetState(PreviewState s)
        {
            if (State == s) return;
            bool wasPaused = State == PreviewState.Paused;
            State = s;
            if (wasPaused && s == PreviewState.Playing)
            {
                foreach (var t in _activeTweens)
                    if (t != null && t.IsValid() && t.IsRunning())
                        t.Play();
            }
            StateChanged?.Invoke();
        }

        // ============ 引擎主循环 ============

        private bool BeginPreview()
        {
            var script = _getScript?.Invoke();
            if (script == null)
            {
                _setStatus?.Invoke("没有正在编辑的演出，无法预览", Colors.Yellow);
                return false;
            }
            _previewScript = script;
            if (_renderer.GridData == null)
            {
                _setStatus?.Invoke("视图地图未加载，无法预览", Colors.Yellow);
                return false;
            }

            // 预览目标地图 = 当前视图；trigger.map 配了且不同则先切过去
            var trigger = script.Trigger;
            if (trigger != null && !string.IsNullOrEmpty(trigger.Map) && trigger.Map != _renderer.CurrentMapName)
            {
                _loadViewMap(trigger.Map);
                if (_renderer.GridData == null)
                {
                    _setStatus?.Invoke($"trigger.map「{trigger.Map}」加载失败，无法预览", Colors.Red);
                    return false;
                }
                _setStatus?.Invoke($"已切到触发地图：{trigger.Map}", Colors.White);
            }

            _groups = script.BuildExecutionGroups();
            if (_groups.Count == 0)
            {
                _setStatus?.Invoke("演出没有任何 cue，无法预览", Colors.Yellow);
                return false;
            }
            _groupIndex = 0;
            _stopRequested = false;
            _pauseAfterGroup = false;
            _cancelTcs = new TaskCompletionSource();

            // 记住视图初始机位（停止时还原）；镜头"家" = trigger.rect 中心 / 地图中心
            _savedCamPos = _camera.GlobalPosition;
            _savedCamZoom = _camera.Zoom;
            _homePos = ComputeHome(script);

            if (script.Letterbox)
                ShowLetterbox(true);

            State = PreviewState.Paused; // Play/StepOnce 会立即置 Playing
            _activeChanged?.Invoke(true);
            GD.Print($"[CutscenePreview] 开始预览「{script.Name}」，共 {_groups.Count} 个执行组");
            RunLoopAsync();
            return true;
        }

        /// <summary>camera_reset 的"家"：trigger.rect 中心（需配在当前视图地图上），否则地图中心</summary>
        private Vector2 ComputeHome(CutsceneScript script)
        {
            var t = script.Trigger;
            if (t?.Rect != null && t.Rect.Length == 4 && t.Map == _renderer.CurrentMapName)
            {
                return new Vector2(
                    (t.Rect[0] + t.Rect[2] / 2f) * CutsceneMapRenderer.GridSize,
                    (t.Rect[1] + t.Rect[3] / 2f) * CutsceneMapRenderer.GridSize);
            }
            var b = _renderer.MapBounds;
            return _renderer.GridToWorld(new Vector2I(
                b.Position.X + b.Size.X / 2, b.Position.Y + b.Size.Y / 2));
        }

        private async void RunLoopAsync()
        {
            try
            {
                while (_groupIndex < _groups.Count)
                {
                    if (_stopRequested) return;
                    if (State != PreviewState.Playing)
                    {
                        await WaitWhilePaused();
                        if (_stopRequested) return;
                        continue;
                    }

                    var group = _groups[_groupIndex];
                    _stepChanged?.Invoke(_groupIndex);
                    GD.Print($"[CutscenePreview] 执行组 {_groupIndex + 1}/{_groups.Count}（并行 {group.Count} 条）");

                    var tasks = new List<Task>();
                    foreach (var cue in group)
                        tasks.Add(ExecuteCueAsync(cue));
                    try
                    {
                        await Task.WhenAll(tasks);
                    }
                    catch (Exception ex)
                    {
                        GD.PushError($"[CutscenePreview] 执行组 {_groupIndex + 1} 失败: {ex}");
                        _setStatus?.Invoke($"预览失败：{ex.Message}", Colors.Red);
                        break;
                    }

                    if (_stopRequested) return;
                    _groupIndex++;
                    if (_pauseAfterGroup)
                    {
                        _pauseAfterGroup = false;
                        SetState(PreviewState.Paused);
                        _setStatus?.Invoke($"单步完成：已执行 {_groupIndex}/{_groups.Count} 组", Colors.Yellow);
                    }
                }

                if (!_stopRequested)
                    FinishPreview(natural: true);
            }
            catch (Exception ex)
            {
                GD.PushError($"[CutscenePreview] 预览循环异常: {ex}");
                FinishPreview(natural: false);
            }
        }

        /// <summary>结束清理（幂等）：杀 Tween、清令牌/气泡/幕布/黑边/对白、还原机位</summary>
        private void FinishPreview(bool natural)
        {
            if (State == PreviewState.Stopped) return;
            _stopRequested = true;
            _cancelTcs?.TrySetResult();

            foreach (var t in _activeTweens)
                if (t != null && t.IsValid())
                    t.Kill();
            _activeTweens.Clear();

            ClearTokens();
            HideDialogue();
            ShowLetterbox(false);
            if (_fadeRect != null)
            {
                _fadeRect.QueueFree();
                _fadeRect = null;
            }

            if (GodotObject.IsInstanceValid(_camera))
            {
                _camera.Offset = Vector2.Zero;
                _camera.GlobalPosition = _savedCamPos;
                _camera.Zoom = _savedCamZoom;
            }

            State = PreviewState.Stopped;
            _stepChanged?.Invoke(-1);
            _activeChanged?.Invoke(false);
            StateChanged?.Invoke();
            GD.Print($"[CutscenePreview] 预览结束（{(natural ? "播放完毕" : "手动停止")}）");
            _setStatus?.Invoke(natural ? "预览播放完毕，视图已还原" : "预览已停止，视图已还原", Colors.White);
        }

        private async Task WaitWhilePaused()
        {
            while (State == PreviewState.Paused && !_stopRequested)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        // ============ 等待原语（暂停时冻结计时，停止时可取消） ============

        private async Task WaitSeconds(float sec)
        {
            double remaining = sec;
            while (remaining > 0 && !_stopRequested)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                if (State != PreviewState.Playing) continue;
                remaining -= GetProcessDeltaTime();
            }
        }

        /// <summary>一条对白行的等待：自动推进（sec 秒后自动翻页，与运行时 StoryPanel 一致）+ 点击/回车可立即翻页。
        /// 暂停时冻结计时，停止时取消。_dialogueTcs 仅用作"当前行正在等待"的标记，置 null 即代表用户触发翻页。</summary>
        private async Task WaitDialogueLine(float sec)
        {
            _dialogueTcs = new TaskCompletionSource();
            double remaining = sec;
            while (!_stopRequested && _dialogueTcs != null)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                if (_stopRequested || _dialogueTcs == null) break;
                if (State != PreviewState.Playing) continue;
                remaining -= GetProcessDeltaTime();
                if (remaining <= 0) break;
            }
            _dialogueTcs = null;
        }

        private async Task AwaitCancellable(Task task)
        {
            await Task.WhenAny(task, _cancelTcs.Task);
        }

        private Tween CreateTrackedTween()
        {
            var t = CreateTween();
            _activeTweens.Add(t);
            return t;
        }

        private async Task AwaitTween(Tween tween)
        {
            var finished = new TaskCompletionSource();
            tween.Finished += () => finished.TrySetResult();
            await AwaitCancellable(finished.Task);
        }

        // ============ cue 执行 ============

        private async Task ExecuteCueAsync(CutsceneCue cue)
        {
            string type = cue.Type.ToLowerInvariant();
            GD.Print($"[CutscenePreview]   cue seq={cue.Seq} type={type} actor=\"{cue.Actor}\"");

            switch (type)
            {
                case "wait": await PreviewWait(cue); break;
                case "story_play": await PreviewStoryPlay(cue); break;
                case "move": await PreviewMove(cue); break;
                case "face": PreviewFace(cue); break;
                case "camera_focus": await PreviewCameraFocus(cue); break;
                case "camera_set": PreviewCameraSet(cue); break;
                case "camera_reset": await PreviewCameraReset(cue); break;
                case "camera_shake": await PreviewCameraShake(cue); break;
                case "fade": await PreviewFade(cue); break;
                case "actor_enter": PreviewActorEnter(cue); break;
                case "actor_leave": PreviewActorLeave(cue); break;
                case "sfx": PreviewAudioLog(cue, "SFX"); break;
                case "bgm": PreviewAudioLog(cue, "BGM"); break;
                default:
                    _setStatus?.Invoke($"预览跳过：未知 cue 类型「{cue.Type}」", Colors.Yellow);
                    break;
            }
        }

        private Task PreviewWait(CutsceneCue cue) => WaitSeconds(cue.GetFloat("sec", 1f));

        private async Task PreviewStoryPlay(CutsceneCue cue)
        {
            int chapter = cue.GetInt("chapter", 1);
            int start = cue.GetInt("dialogue_start", 1);
            int end = cue.GetInt("dialogue_end", 1);

            // 预览要真正读取剧情表，逐条展示真实对白，而不是只弹一个占位条
            StoryConfigUtil.Load(); // 编辑器预览场景下剧情表可能尚未加载
            var all = StoryConfigUtil.GetDialogues(chapter);
            var rows = all.Where(d => d.id >= start && d.id <= end).ToList();

            if (rows.Count == 0)
            {
                ShowDialogue("", $"（章节 {chapter} · 对话id {start}~{end}：范围内没有对白，预览直接跳过）");
                await WaitDialogueLine(1.5f);
                HideDialogue();
                return;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                if (_stopRequested) break;
                var row = rows[i];
                // 每条对白停留时长与运行时 StoryPanel 一致：max(2.0f, 内容长度*0.045+1.0)
                ShowDialogue(row.speaker ?? "", row.content ?? "");
                float sec = Mathf.Max(2.0f, (row.content ?? string.Empty).Length * 0.045f + 1.0f);
                await WaitDialogueLine(sec);
            }
            HideDialogue();
        }

        private async Task PreviewMove(CutsceneCue cue)
        {
            var token = ResolveToken(cue, "move");
            if (token == null) return;
            if (!cue.Has("x") || !cue.Has("y"))
            {
                _setStatus?.Invoke($"预览跳过 move：缺少坐标 x/y（seq={cue.Seq}）", Colors.Yellow);
                return;
            }

            var target = new Vector2I(cue.GetInt("x"), cue.GetInt("y"));
            var targetWorld = _renderer.GridToWorld(target);

            // 时长缺省按默认速度折算（与运行时 CueHandlers.HandleMove 同一公式）
            float duration = cue.GetFloat("sec", 0f);
            if (duration <= 0f)
            {
                float dist = token.GlobalPosition.DistanceTo(targetWorld);
                duration = Mathf.Max(0.15f, 0.18f * dist / CutsceneMapRenderer.GridSize);
            }

            // 走位附带转向（对照运行时 move 先设置 Direction）
            token.SetDirection(DirectionFromDelta(targetWorld - token.GlobalPosition));

            var tween = CreateTrackedTween();
            tween.TweenProperty(token, "global_position", targetWorld, duration);
            await AwaitTween(tween);
        }

        private void PreviewFace(CutsceneCue cue)
        {
            var token = ResolveToken(cue, "face");
            if (token == null) return;

            string dir = cue.GetString("dir").ToLowerInvariant();
            int direction = dir switch
            {
                "right" => 0,
                "down" => 1,
                "left" => 2,
                "up" => 3,
                _ => -1,
            };
            if (direction < 0)
            {
                _setStatus?.Invoke($"预览跳过 face：方向非法「{dir}」（应为 up/down/left/right）", Colors.Yellow);
                return;
            }
            token.SetDirection(direction);
        }

        private async Task PreviewCameraFocus(CutsceneCue cue)
        {
            Vector2 targetPos;
            if (cue.Has("x") && cue.Has("y"))
            {
                targetPos = _renderer.GridToWorld(new Vector2I(cue.GetInt("x"), cue.GetInt("y")));
            }
            else if (!string.IsNullOrWhiteSpace(cue.Actor))
            {
                var token = ResolveToken(cue, "camera_focus");
                if (token == null) return;
                // 用令牌当前视觉位置：走位中聚焦会跟着移动目标走（对照运行时取 GlobalPosition）
                targetPos = token.GlobalPosition;
            }
            else
            {
                _setStatus?.Invoke($"预览跳过 camera_focus：需要坐标 x/y 或 actor（seq={cue.Seq}）", Colors.Yellow);
                return;
            }

            float sec = cue.GetFloat("sec", 1f);
            float zoom = cue.GetFloat("zoom", 0f);

            var tween = CreateTrackedTween();
            tween.SetParallel(true);
            tween.SetTrans(Tween.TransitionType.Quad);
            tween.SetEase(Tween.EaseType.InOut);
            tween.TweenProperty(_camera, "global_position", targetPos, sec);
            if (zoom > 0f)
                tween.TweenProperty(_camera, "zoom", new Vector2(zoom, zoom), sec);
            await AwaitTween(tween);
        }

        /// <summary>camera_set：瞬移机位（无 tween，立即生效），对照运行时 CueHandlers.HandleCameraSet</summary>
        private void PreviewCameraSet(CutsceneCue cue)
        {
            if (!cue.Has("x") || !cue.Has("y"))
            {
                _setStatus?.Invoke($"预览跳过 camera_set：缺少坐标 x/y（seq={cue.Seq}）", Colors.Yellow);
                return;
            }

            _camera.GlobalPosition = _renderer.GridToWorld(new Vector2I(cue.GetInt("x"), cue.GetInt("y")));
            float zoom = cue.GetFloat("zoom", 0f);
            if (zoom > 0f)
                _camera.Zoom = new Vector2(zoom, zoom);
        }

        private async Task PreviewCameraReset(CutsceneCue cue)
        {
            float sec = cue.GetFloat("sec", 1f);
            var tween = CreateTrackedTween();
            tween.SetParallel(true);
            tween.SetTrans(Tween.TransitionType.Quad);
            tween.SetEase(Tween.EaseType.InOut);
            tween.TweenProperty(_camera, "global_position", _homePos, sec);
            tween.TweenProperty(_camera, "zoom", _savedCamZoom, sec);
            await AwaitTween(tween);
        }

        private async Task PreviewCameraShake(CutsceneCue cue)
        {
            float strength = cue.GetFloat("strength", 8f);
            float sec = cue.GetFloat("sec", 0.4f);

            var rng = new RandomNumberGenerator();
            rng.Randomize();
            const float step = 1f / 30f;
            float elapsed = 0f;
            while (elapsed < sec && !_stopRequested)
            {
                _camera.Offset = new Vector2(
                    rng.RandfRange(-strength, strength),
                    rng.RandfRange(-strength, strength));
                await WaitSeconds(step);
                elapsed += step;
            }
            if (GodotObject.IsInstanceValid(_camera))
                _camera.Offset = Vector2.Zero;
        }

        private async Task PreviewFade(CutsceneCue cue)
        {
            string dir = cue.GetString("dir", "out").ToLowerInvariant();
            if (dir != "out" && dir != "in")
            {
                _setStatus?.Invoke($"预览跳过 fade：方向非法「{dir}」（应为 out/in）", Colors.Yellow);
                return;
            }

            EnsureFadeRect();
            float sec = cue.GetFloat("sec", 0.8f);
            float target = dir == "out" ? 1f : 0f;
            var tween = CreateTrackedTween();
            tween.TweenProperty(_fadeRect, "modulate:a", target, sec);
            await AwaitTween(tween);
        }

        private void PreviewActorEnter(CutsceneCue cue)
        {
            if (string.IsNullOrWhiteSpace(cue.Actor))
            {
                _setStatus?.Invoke($"预览跳过 actor_enter：缺少 actor（seq={cue.Seq}）", Colors.Yellow);
                return;
            }
            if (!cue.Has("x") || !cue.Has("y"))
            {
                _setStatus?.Invoke($"预览跳过 actor_enter：缺少坐标 x/y（seq={cue.Seq}）", Colors.Yellow);
                return;
            }
            if (_tokens.ContainsKey(cue.Actor))
            {
                _setStatus?.Invoke($"预览跳过 actor_enter：演员「{cue.Actor}」已存在（seq={cue.Seq}）", Colors.Yellow);
                return;
            }

            // 按演员表定义解析名字/外观（§5.5），与运行时 SpawnActor 同一来源 EntityStyleConfig
            string name = "角色";
            EntityStyleConfig style = CutsceneActorCatalog.ResolveMonsterStyle(0); // 兜底默认（红）
            if (int.TryParse(cue.Actor, out int actorId))
            {
                var def = _previewScript?.FindActor(actorId);
                if (def != null)
                {
                    if (def.type == 1)
                    {
                        name = "玩家";
                        style = CutsceneActorCatalog.ResolvePlayerStyle();
                    }
                    else if (def.type == 2)
                    {
                        name = CutsceneActorCatalog.ResolveMonsterName(def.monsterConfigId);
                        style = CutsceneActorCatalog.ResolveMonsterStyle(def.monsterConfigId);
                    }
                }
            }

            var token = new CutscenePreviewActorToken();
            token.Setup(name, style, CutsceneMapRenderer.GridSize);
            token.GlobalPosition = _renderer.GridToWorld(new Vector2I(cue.GetInt("x"), cue.GetInt("y")));
            // 登场朝向：默认 down；解析失败（非法 dir）黄字提示并退回默认
            string dir = cue.GetString("dir", "down").ToLowerInvariant();
            int direction = dir switch
            {
                "right" => 0,
                "down" => 1,
                "left" => 2,
                "up" => 3,
                _ => -1,
            };
            if (direction < 0)
                _setStatus?.Invoke($"预览 actor_enter：方向非法「{dir}」，已用默认朝向（seq={cue.Seq}）", Colors.Yellow);
            else
                token.SetDirection(direction);
            EnsureTokenLayer().AddChild(token);
            _tokens[cue.Actor] = token;
        }

        private void PreviewActorLeave(CutsceneCue cue)
        {
            if (string.IsNullOrWhiteSpace(cue.Actor) || !_tokens.Remove(cue.Actor, out var token))
            {
                _setStatus?.Invoke($"预览跳过 actor_leave：演员「{cue.Actor}」不存在（seq={cue.Seq}）", Colors.Yellow);
                return;
            }
            if (GodotObject.IsInstanceValid(token))
                token.QueueFree();
        }

        private void PreviewAudioLog(CutsceneCue cue, string kind)
        {
            // 第一期无音频资产，预览与运行时一致只记日志（§4.3）
            GD.Print($"[CutscenePreview] {kind} cue（预览不播放）：{cue.GetString("name")}");
        }

        /// <summary>取 actor id 对应的令牌；取不到则黄字提示并返回 null</summary>
        private CutscenePreviewActorToken ResolveToken(CutsceneCue cue, string what)
        {
            if (string.IsNullOrWhiteSpace(cue.Actor) || !_tokens.TryGetValue(cue.Actor, out var token) || !GodotObject.IsInstanceValid(token))
            {
                _setStatus?.Invoke($"预览跳过 {what}：演员「{cue.Actor}」不存在（未登场或已退场，seq={cue.Seq}）", Colors.Yellow);
                return null;
            }
            return token;
        }

        private static int DirectionFromDelta(Vector2 delta)
        {
            if (Mathf.Abs(delta.X) >= Mathf.Abs(delta.Y))
                return delta.X >= 0 ? 0 : 2;    // 右 / 左
            return delta.Y >= 0 ? 1 : 3;        // 下 / 上
        }

        // ============ 覆盖层：幕布 / 黑边 / 对白 ============

        private void EnsureFadeRect()
        {
            if (_fadeRect != null) return;
            _fadeRect = new ColorRect
            {
                Color = Colors.Black,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            _fadeRect.SetAnchorsPreset(LayoutPreset.FullRect);
            _fadeRect.Modulate = new Color(1f, 1f, 1f, 0f);
            AddChild(_fadeRect);
        }

        /// <summary>letterbox 上下黑条（比例参考 2.35:1，高度约为视口 12%）</summary>
        private void ShowLetterbox(bool on)
        {
            if (on)
            {
                if (_letterboxTop != null) return;
                _letterboxTop = MakeLetterboxBar();
                _letterboxTop.SetAnchorsPreset(LayoutPreset.TopWide);
                _letterboxTop.AnchorBottom = 0.12f;
                _letterboxTop.OffsetBottom = 0;
                _letterboxBottom = MakeLetterboxBar();
                _letterboxBottom.SetAnchorsPreset(LayoutPreset.BottomWide);
                _letterboxBottom.AnchorTop = 0.88f;
                _letterboxBottom.OffsetTop = 0;
                AddChild(_letterboxTop);
                AddChild(_letterboxBottom);
            }
            else
            {
                if (_letterboxTop != null) { _letterboxTop.QueueFree(); _letterboxTop = null; }
                if (_letterboxBottom != null) { _letterboxBottom.QueueFree(); _letterboxBottom = null; }
            }
        }

        private static ColorRect MakeLetterboxBar() => new()
        {
            Color = Colors.Black,
            MouseFilter = MouseFilterEnum.Ignore,
        };

        private void ShowDialogue(string speaker, string content)
        {
            if (_dialogueBar == null)
            {
                _dialogueBar = new PanelContainer();
                _dialogueBar.AddThemeStyleboxOverride("panel", new StyleBoxFlat
                {
                    BgColor = new Color(0.06f, 0.06f, 0.1f, 0.95f),
                    BorderColor = new Color(0.45f, 0.55f, 0.95f, 1f),
                    BorderWidthBottom = 2,
                    BorderWidthLeft = 2,
                    BorderWidthRight = 2,
                    BorderWidthTop = 2,
                    CornerRadiusTopLeft = 10,
                    CornerRadiusTopRight = 10,
                    CornerRadiusBottomLeft = 10,
                    CornerRadiusBottomRight = 10,
                });
                _dialogueBar.SetAnchorsPreset(LayoutPreset.Center);
                _dialogueBar.CustomMinimumSize = new Vector2(560, 150);
                _dialogueBar.MouseFilter = MouseFilterEnum.Ignore; // 点击穿透到全屏 catcher

                _dialogueLabel = new Label
                {
                    AutowrapMode = TextServer.AutowrapMode.WordSmart,
                    SizeFlagsHorizontal = SizeFlags.ExpandFill,
                    SizeFlagsVertical = SizeFlags.ExpandFill,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                _dialogueLabel.AddThemeFontSizeOverride("font_size", 18);
                _dialogueBar.AddChild(_dialogueLabel);

                // 全屏透明点击层：预览对白"点击任意处继续"
                _dialogueClickCatcher = new Control { MouseFilter = MouseFilterEnum.Stop };
                _dialogueClickCatcher.SetAnchorsPreset(LayoutPreset.FullRect);
                _dialogueClickCatcher.GuiInput += OnDialogueGuiInput;
                AddChild(_dialogueClickCatcher);
                AddChild(_dialogueBar);
            }

            _dialogueLabel.Text = (string.IsNullOrWhiteSpace(speaker)
                ? content
                : $"【{speaker}】{content}") + "　（自动播放中，点击 / 回车加速）";
            _dialogueBar.Visible = true;
            _dialogueClickCatcher.Visible = true;
        }

        private void HideDialogue()
        {
            if (_dialogueBar != null)
                _dialogueBar.Visible = false;
            if (_dialogueClickCatcher != null)
                _dialogueClickCatcher.Visible = false;
            _dialogueTcs = null;
        }

        private void OnDialogueGuiInput(InputEvent @event)
        {
            if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left && mb.Pressed)
            {
                ContinueDialogue();
                AcceptEvent();
            }
            else if (@event is InputEventKey key && key.Pressed && !key.Echo
                     && (key.Keycode == Key.Space || key.Keycode == Key.Enter))
            {
                ContinueDialogue();
                AcceptEvent();
            }
        }

        public override void _UnhandledKeyInput(InputEvent @event)
        {
            if (_dialogueTcs != null
                && @event is InputEventKey key && key.Pressed && !key.Echo
                && (key.Keycode == Key.Space || key.Keycode == Key.Enter))
            {
                ContinueDialogue();
                GetViewport()?.SetInputAsHandled();
            }
        }

        // ============ 令牌层 ============

        private Node2D EnsureTokenLayer()
        {
            if (_tokenLayer == null || !GodotObject.IsInstanceValid(_tokenLayer))
            {
                _tokenLayer = new Node2D { Name = "PreviewTokens" };
                _sub.AddChild(_tokenLayer);
            }
            return _tokenLayer;
        }

        private void ClearTokens()
        {
            _tokens.Clear();
            if (_tokenLayer != null && GodotObject.IsInstanceValid(_tokenLayer))
                _tokenLayer.QueueFree();
            _tokenLayer = null;
        }
    }

    /// <summary>
    /// 预览演员令牌：色块 + 名字标签 + 方向箭头（对应运行时的本地假人 StoryActor）。
    /// 挂在 SubViewport 世界内，格子坐标→像素换算与 CutsceneMapRenderer.GridToWorld 一致。
    /// </summary>
    [Tool]
    public partial class CutscenePreviewActorToken : Node2D
    {
        /// <summary>显示名（dialogue speaker 兜底用）</summary>
        public string DisplayName { get; private set; } = "";

        private EntityStyleConfig _style = new();
        private float _grid = CutsceneMapRenderer.GridSize;
        private int _direction = 1; // 0=右 1=下 2=左 3=上（同 EntityBase.Direction）
        private Label _nameLabel;
        private Label _bubbleLabel;

        /// <summary>按与游戏一致的 EntityStyleConfig 表现（圆角色块+边框+铭牌+方向箭头），
        /// 但不实例化 EntityBase/StoryActor，保持对真实游戏零影响。</summary>
        public void Setup(string displayName, EntityStyleConfig style, float gridSize)
        {
            DisplayName = displayName;
            _style = style ?? new EntityStyleConfig();
            _grid = gridSize;

            _nameLabel = new Label
            {
                Text = displayName,
                Position = new Vector2(-_grid, -_grid * 0.92f),
                Size = new Vector2(_grid * 2, _grid * 0.5f),
                HorizontalAlignment = HorizontalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            _nameLabel.AddThemeFontSizeOverride("font_size", 14);
            _nameLabel.AddThemeColorOverride("font_color", _style.TextColor);
            _nameLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
            _nameLabel.AddThemeConstantOverride("outline_size", 4);
            AddChild(_nameLabel);
            QueueRedraw();
        }

        /// <summary>0=右 1=下 2=左 3=上</summary>
        public void SetDirection(int direction)
        {
            _direction = direction;
            QueueRedraw();
        }

        /// <summary>头顶气泡：≤2 字符放大显示（模拟运行时表情符号规则），到时由播放器调 HideBubble</summary>
        public void ShowBubble(string text)
        {
            HideBubble();
            bool big = text.Length <= 2;
            _bubbleLabel = new Label
            {
                Text = text,
                Position = new Vector2(-_grid, -_grid * 1.7f),
                Size = new Vector2(_grid * 2, _grid * 1.1f),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            _bubbleLabel.AddThemeFontSizeOverride("font_size", big ? 34 : 16);
            _bubbleLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
            _bubbleLabel.AddThemeConstantOverride("outline_size", 5);
            AddChild(_bubbleLabel);
        }

        public void HideBubble()
        {
            if (_bubbleLabel != null)
            {
                _bubbleLabel.QueueFree();
                _bubbleLabel = null;
            }
        }

        public override void _Draw()
        {
            int grid = (int)_grid;
            int outer = _style.ComputeVisualOuterSize(grid);
            int border = _style.ComputeBorderWidth(grid);
            int inner = _style.ComputeVisualSize(grid);

            // 与游戏 EntityBase.AppearanceComponent 同一套绘制（圆角+边框+背景透明度）
            EntityDrawUtils.DrawBody(this, outer, outer, inner, inner,
                _style.BgColor, _style.BgOpacity, _style.BorderColor, border, _style.CornerRadius);

            // 方向箭头：从色块外缘向外指的白三角（与游戏 EntityDrawUtils.DrawDirectionArrow 一致）
            Vector2 dirVec = _direction switch
            {
                0 => Vector2.Right,
                2 => Vector2.Left,
                3 => Vector2.Up,
                _ => Vector2.Down,
            };
            float reach = outer * 0.5f + _grid * 0.08f;
            Vector2 tip = dirVec * reach;
            Vector2 baseC = dirVec * (outer * 0.5f);
            Vector2 perp = new Vector2(-dirVec.Y, dirVec.X) * (_grid * 0.12f);
            DrawColoredPolygon(new[] { tip, baseC + perp, baseC - perp }, Colors.White);
        }
    }
}
