using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using ClinetCSharp;

namespace ClinetCSharp.Cutscene
{
    /// <summary>
    /// 导演模式编排器（§3.1）— 加载演出脚本 → 进入演出态 → 按 (seq, group) 顺序执行 cue → 恢复现场。
    ///
    /// 关键实现约束（设计文档 §3.1）：
    /// - 进入演出后整棵树被 UIInputPolicy.PauseGame() 暂停，本节点及所有演出节点必须 ProcessMode=Always；
    /// - 走位等 Tween 必须由 Always 节点创建（真实实体经 EntityBase.TweenHostOverride 托管给本节点，
    ///   StoryActor 自身即 Always），否则 Tween 随实体一并冻结。
    ///
    /// 演出互斥：一场演出播放期间拒绝再次触发（IsPlaying）。
    /// </summary>
    public partial class CutsceneDirector : Node2D
    {
        private class Waiter
        {
            public double Remaining;
            public TaskCompletionSource Tcs;
        }

        private CutsceneContext _ctx;
        private LetterboxOverlay _letterbox;
        private BubbleLayer _bubbles;
        private CutsceneTrigger _trigger;
        private StoryPanel _activeStoryPanel;

        private readonly List<Waiter> _waiters = new();
        private bool _isPlaying;
        private int _currentId;

        // 屏幕错误提示
        private CanvasLayer _errorLayer;
        private Label _errorLabel;
        private double _errorRemaining;

        /// <summary>当前是否有演出在播放（互斥锁）</summary>
        public bool IsPlaying => _isPlaying;
        /// <summary>当前播放的演出 ID（未播放时为 0）</summary>
        public int CurrentId => _currentId;
        /// <summary>当前演出上下文（仅播放期间有效）</summary>
        public CutsceneContext Ctx => _ctx;
        /// <summary>头顶气泡层（cue 执行器用）</summary>
        public BubbleLayer Bubbles => _bubbles;
        /// <summary>
        /// 幕布状态：当前是否处于 fade out 后的黑幕中（由 fade cue 维护，退出演出时复位）。
        /// 用于可视类 cue（dialogue/bubble 等）检测"在黑幕后面演"的脚本顺序错误。
        /// </summary>
        public bool IsScreenBlack { get; set; }

        public override void _Ready()
        {
            // 演出态下整棵树暂停，导演必须 Always 才能驱动 Tween/计时器/输入
            ProcessMode = ProcessModeEnum.Always;
            AddToGroup("cutscene_director");

            _letterbox = new LetterboxOverlay { Name = "Letterbox" };
            AddChild(_letterbox);

            _bubbles = new BubbleLayer { Name = "Bubbles" };
            AddChild(_bubbles);

            _trigger = new CutsceneTrigger { Name = "Trigger" };
            AddChild(_trigger);

            SetupErrorLayer();
            CutsceneConfigUtil.Load();
        }

        private void SetupErrorLayer()
        {
            // 置顶错误提示层：cue 失败时直接弹在屏幕上（高于 ScreenTransition=200）
            _errorLayer = new CanvasLayer { Layer = 300, ProcessMode = ProcessModeEnum.Always, Visible = false };
            var panel = new PanelContainer
            {
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            panel.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
            panel.Position = new Vector2(0, 40);
            panel.GrowHorizontal = Control.GrowDirection.Both;
            var style = new StyleBoxFlat
            {
                BgColor = new Color(0.35f, 0.05f, 0.05f, 0.92f),
                BorderColor = new Color(0.9f, 0.3f, 0.3f, 1f),
                BorderWidthBottom = 2,
                BorderWidthLeft = 2,
                BorderWidthRight = 2,
                BorderWidthTop = 2,
                CornerRadiusTopLeft = 6,
                CornerRadiusTopRight = 6,
                CornerRadiusBottomLeft = 6,
                CornerRadiusBottomRight = 6,
                ContentMarginLeft = 16,
                ContentMarginRight = 16,
                ContentMarginTop = 8,
                ContentMarginBottom = 8,
            };
            panel.AddThemeStyleboxOverride("panel", style);

            _errorLabel = new Label { MouseFilter = Control.MouseFilterEnum.Ignore };
            _errorLabel.AddThemeFontSizeOverride("font_size", 18);
            _errorLabel.AddThemeColorOverride("font_color", new Color(1f, 0.9f, 0.9f));
            panel.AddChild(_errorLabel);

            _errorLayer.AddChild(panel);
            AddChild(_errorLayer);
        }

        /// <summary>屏幕弹错误提示（同时打日志），约 6 秒后自动消失</summary>
        public void ShowError(string message)
        {
            GD.PrintErr($"[Cutscene] {message}");
            if (_errorLabel == null) return;
            _errorLabel.Text = message;
            _errorLayer.Visible = true;
            _errorRemaining = 6.0;
        }

        public override void _Process(double delta)
        {
            // 错误提示自动消失
            if (_errorRemaining > 0)
            {
                _errorRemaining -= delta;
                if (_errorRemaining <= 0 && _errorLayer != null)
                    _errorLayer.Visible = false;
            }

            // 推进等待中的 cue 计时器；跳过请求时立即放行
            if (_waiters.Count == 0) return;
            bool skip = _ctx != null && _ctx.SkipRequested;
            for (int i = _waiters.Count - 1; i >= 0; i--)
            {
                var waiter = _waiters[i];
                waiter.Remaining -= delta;
                if (waiter.Remaining <= 0 || skip)
                {
                    _waiters.RemoveAt(i);
                    waiter.Tcs.TrySetResult();
                }
            }
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            // ESC 请求跳过（§3.3）；不可跳过的演出忽略
            if (!_isPlaying || _ctx == null || !_ctx.Script.Skippable)
                return;
            if (@event is not InputEventKey key || !key.Pressed || key.Echo || key.Keycode != Key.Escape)
                return;
            // 文本输入框聚焦时 ESC 留给输入框自己处理
            if (UiUtils.IsGuiTextInputFocused(GetViewport()))
                return;

            GetViewport()?.SetInputAsHandled();
            RequestSkip();
        }

        /// <summary>请求跳过当前演出：当前 cue 尽快结束，不再取后续 cue，直接走退出恢复流程</summary>
        public void RequestSkip()
        {
            if (!_isPlaying || _ctx == null) return;
            GD.Print($"[Cutscene] 演出 {_currentId} 收到跳过请求");
            _ctx.RequestSkip();
        }

        // ========== 播放入口 ==========

        /// <summary>
        /// 播放指定演出。manual=true（GM 命令）时不受进度限制也不计入进度；
        /// manual=false（区域触发）时结束后写入已看进度。
        /// </summary>
        public void PlayById(int cutsceneId, bool manual)
        {
            _ = RunAsync(cutsceneId, manual);
        }

        private async Task RunAsync(int cutsceneId, bool manual)
        {
            if (_isPlaying)
            {
                ShowError($"[演出] 正在播放演出 {_currentId}，无法重复触发");
                return;
            }
            if (!CutsceneConfigUtil.TryGet(cutsceneId, out var script))
            {
                string detail = CutsceneConfigUtil.LastErrors.Count > 0
                    ? $"（{string.Join("；", CutsceneConfigUtil.LastErrors)}）"
                    : "（检查 data/cutscenes/ 下是否有对应 JSON 且格式正确）";
                ShowError($"[演出] 演出 {cutsceneId} 不存在{detail}");
                return;
            }

            var errors = CutsceneScriptIO.Validate(script, $"{cutsceneId}.json");
            if (errors.Count > 0)
            {
                ShowError($"[演出 {cutsceneId}] 脚本校验失败：{string.Join("；", errors)}");
                return;
            }

            _isPlaying = true;
            _currentId = cutsceneId;
            _ctx = new CutsceneContext(script, manual);
            GD.Print($"[Cutscene] 开始播放演出 {cutsceneId}「{script.Name}」（{(manual ? "GM手动" : "区域触发")}）");

            try
            {
                await EnterCutscene(script);

                // (seq, group) 升序逐组执行：组内 cue 并行，组间严格等待全部完成（§4.2）
                foreach (var group in script.BuildExecutionGroups())
                {
                    if (_ctx.SkipRequested) break;

                    var tasks = new List<Task>(group.Count);
                    foreach (var cue in group)
                        tasks.Add(CueHandlers.ExecuteAsync(this, cue));
                    await Task.WhenAll(tasks);
                }
            }
            catch (CutsceneCueException ex)
            {
                // cue 执行失败（演员找不到/坐标非法等）：屏幕提示并中断，走恢复流程（§6 制作侧 #2）
                ShowError($"[演出 {cutsceneId}] seq={ex.Seq} 执行失败：{ex.Message}");
            }
            catch (Exception ex)
            {
                ShowError($"[演出 {cutsceneId}] 播放异常：{ex.Message}");
            }

            await ExitCutscene();

            // 进度规则（§7）：GM 手动触发不计入进度（保持 once 触发可反复验收）；
            // 区域触发的演出跳过也算已看，避免玩家被同一演出反复拦截。
            if (!manual)
                CutsceneProgress.MarkWatched(cutsceneId);

            GD.Print($"[Cutscene] 演出 {cutsceneId} 结束{(_ctx.SkipRequested ? "（被跳过）" : "")}");
            _ctx = null;
            _isPlaying = false;
            _currentId = 0;
        }

        // ========== 进入/退出演出态（exactly once，成对执行） ==========

        private async Task EnterCutscene(CutsceneScript script)
        {
            // 1. 暂停世界与输入（计数栈，退出时必须配对 ResumeGame）
            UIInputPolicy.Instance?.PauseGame();
            // 2. 隐藏 HUD 与已打开的面板（保留实体可见，演出需要 NPC/怪物/玩家出镜）
            GameUiVisibilityManager.Instance?.HideGameUi(includeEntities: false);
            // 3. 镜头进入演出模式：快照状态、屏蔽拖拽/滚轮
            GetCamera()?.EnterCutsceneMode();
            // 4. 电影黑边
            if (script.Letterbox)
                await _letterbox.ShowAsync();
        }

        private async Task ExitCutscene()
        {
            // 1~3 步（收尾对白/转场、镜头、HUD）任何一步失败都不能阻断后面的暂停恢复
            try
            {
                // 1. 收尾对白与转场，保证无残留
                if (_activeStoryPanel != null && IsInstanceValid(_activeStoryPanel))
                    _activeStoryPanel.CloseCutsceneLines();
                _activeStoryPanel = null;
                ScreenTransition.Get()?.ResetInstant();
                IsScreenBlack = false;

                // 2. 镜头恢复快照并退出演出模式
                GetCamera()?.ExitCutsceneMode();
                // 3. 恢复 HUD
                GameUiVisibilityManager.Instance?.ShowGameUi();
            }
            catch (Exception ex)
            {
                ShowError($"[演出] 退出清理异常：{ex.Message}");
            }

            // 4. 恢复世界（与 PauseGame 配对，必须执行）
            UIInputPolicy.Instance?.ResumeGame();

            // 5. 还原被移动真实实体的视觉位置（纯播放原则：move 只是视觉位移，不留下任何状态残留）
            try
            {
                if (_ctx != null)
                {
                    foreach (var kv in _ctx.MovedEntityOriginalPositions)
                    {
                        if (kv.Key != null && IsInstanceValid(kv.Key))
                            kv.Key.Position = kv.Value;
                    }
                    _ctx.MovedEntityOriginalPositions.Clear();
                }
            }
            catch (Exception ex)
            {
                ShowError($"[演出] 实体位置还原异常：{ex.Message}");
            }

            // 6. 销毁全部假人、残留气泡与黑边
            try
            {
                if (_ctx != null)
                {
                    foreach (var actor in _ctx.Actors.Values)
                    {
                        if (actor != null && IsInstanceValid(actor))
                            actor.QueueFree();
                    }
                    _ctx.Actors.Clear();
                }
                _bubbles.ClearAll();
                await _letterbox.HideAsync(0.3f);
            }
            catch (Exception ex)
            {
                ShowError($"[演出] 退出清理异常：{ex.Message}");
            }

            // 兜底放行可能残留的等待任务，避免悬挂
            foreach (var waiter in _waiters)
                waiter.Tcs.TrySetResult();
            _waiters.Clear();
        }

        // ========== cue 执行基础设施 ==========

        public GridManager GetGrid() => GetTree()?.GetFirstNodeInGroup("grid_manager") as GridManager;
        public CameraController GetCamera() => GetTree()?.GetFirstNodeInGroup("camera") as CameraController;

        /// <summary>对白面板（cue 首次使用时创建并记录，退出时统一收尾）</summary>
        public StoryPanel GetStoryPanel()
        {
            if (_activeStoryPanel != null && IsInstanceValid(_activeStoryPanel))
                return _activeStoryPanel;
            _activeStoryPanel = PanelManager.Instance?.GetPanel<StoryPanel>();
            return _activeStoryPanel;
        }

        /// <summary>延时等待：到时或收到跳过请求时完成</summary>
        public Task WaitSeconds(double sec)
        {
            if (sec <= 0 || _ctx == null || _ctx.SkipRequested)
                return Task.CompletedTask;
            var waiter = new Waiter { Remaining = sec, Tcs = new TaskCompletionSource() };
            _waiters.Add(waiter);
            return waiter.Tcs.Task;
        }

        /// <summary>
        /// 等待 Tween 完成；收到跳过请求时快进到终值并立即完成。
        /// 逐帧轮询而非订阅 Finished 事件：Tween 被 Kill（实体回滚 RollbackTo、重新 MoveTo、节点释放）
        /// 时 Finished 不会触发，事件式等待会让演出永久悬挂、暂停栈无法恢复；轮询能兜底放行。
        /// </summary>
        public async Task WaitTween(Tween tween)
        {
            if (tween == null || !IsInstanceValid(tween) || !tween.IsRunning())
                return;

            while (IsInstanceValid(tween) && tween.IsRunning())
            {
                if (_ctx != null && _ctx.SkipRequested)
                {
                    tween.CustomStep(3600f); // 快进到终值，触发 Finished 应用最终状态
                    return;
                }
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
        }

        /// <summary>演员寻址（§3.2）：仅支持演员表里的假人，用整数 id 引用；失败抛 CutsceneCueException</summary>
        public EntityBase ResolveActor(string actorRef, int seq)
        {
            if (CutsceneScriptIO.TryParseActorId(actorRef, out int id)
                && _ctx != null && _ctx.Actors.TryGetValue(id, out var actor) && IsInstanceValid(actor))
                return actor;
            throw new CutsceneCueException(seq, $"找不到演出演员：{actorRef}（未登场或已退场）");
        }

        /// <summary>让某演员登场（actor_enter）：按 type/monsterConfigId 解析外观，在落点生成假人。
        /// direction 为登场朝向（EntityBase.Direction：0=右,1=下,2=左,3=上，默认 1=下）。</summary>
        public StoryActor SpawnActor(int actorId, int x, int y, int seq, int direction = 1)
        {
            if (_ctx == null)
                throw new CutsceneCueException(seq, "演出上下文未初始化");
            if (_ctx.Actors.ContainsKey(actorId))
                throw new CutsceneCueException(seq, $"演员 {actorId} 重复登场（actor_enter 重复）");

            var def = _ctx.Script.FindActor(actorId)
                ?? throw new CutsceneCueException(seq, $"演员 {actorId} 不在演员表中");

            var grid = GetGrid();
            if (grid == null || !grid.IsInBounds(new Vector2I(x, y)))
                throw new CutsceneCueException(seq, $"登场落点非法：({x}, {y}) 不在当前地图范围内");

            string name;
            EntityStyleConfig style;
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
            else
                throw new CutsceneCueException(seq, $"未支持的演员类型：{def.type}（仅 1=玩家 / 2=怪物）");

            var actor = new StoryActor { Name = $"StoryActor_{actorId}" };
            AddChild(actor);
            actor.Setup(actorId.ToString(), name, style, x, y, grid.GridSize);
            actor.Direction = direction; // 登场朝向
            _ctx.Actors[actorId] = actor;
            return actor;
        }

        /// <summary>让某演员退场（actor_leave）</summary>
        public void DespawnActor(int actorId, int seq)
        {
            if (_ctx == null || !_ctx.Actors.TryGetValue(actorId, out var actor))
                throw new CutsceneCueException(seq, $"退场找不到演员：{actorId}（未登场或已退场）");
            _ctx.Actors.Remove(actorId);
            if (IsInstanceValid(actor))
                actor.QueueFree();
        }
    }
}
