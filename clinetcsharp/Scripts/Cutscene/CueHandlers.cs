using System;
using System.Threading.Tasks;
using Godot;
using ClinetCSharp;

namespace ClinetCSharp.Cutscene
{
    /// <summary>
    /// cue 执行器（§4.3）— 每种 type 一个执行方法，async 返回完成。
    /// 所有失败（演员找不到、坐标非法、参数缺失）统一抛 CutsceneCueException，
    /// 由 CutsceneDirector 捕获后屏幕提示并中断演出、走恢复流程。
    /// sfx/bgm 第一期仅记录日志，不接音频系统。
    /// </summary>
    public static class CueHandlers
    {
        /// <summary>分发执行单条 cue</summary>
        public static Task ExecuteAsync(CutsceneDirector director, CutsceneCue cue)
        {
            return cue.Type.ToLowerInvariant() switch
            {
                "wait" => HandleWait(director, cue),
                "story_play" => HandleStoryPlay(director, cue),
                "move" => HandleMove(director, cue),
                "face" => HandleFace(director, cue),
                "camera_focus" => HandleCameraFocus(director, cue),
                "camera_set" => HandleCameraSet(director, cue),
                "camera_reset" => HandleCameraReset(director, cue),
                "camera_shake" => HandleCameraShake(director, cue),
                "fade" => HandleFade(director, cue),
                "actor_enter" => HandleActorEnter(director, cue),
                "actor_leave" => HandleActorLeave(director, cue),
                "sfx" => HandleAudioLog(cue, "SFX"),
                "bgm" => HandleAudioLog(cue, "BGM"),
                _ => throw new CutsceneCueException(cue.Seq, $"未知 cue 类型：{cue.Type}"),
            };
        }

        private static async Task HandleWait(CutsceneDirector director, CutsceneCue cue)
        {
            await director.WaitSeconds(cue.GetFloat("sec", 1.0f));
        }

        private static async Task HandleStoryPlay(CutsceneDirector director, CutsceneCue cue)
        {
            int chapter = cue.GetInt("chapter");
            int start = cue.GetInt("dialogue_start");
            int end = cue.GetInt("dialogue_end");

            var panel = director.GetStoryPanel();
            if (panel == null)
                throw new CutsceneCueException(cue.Seq, "对白面板（StoryPanel）不可用");

            var tcs = new TaskCompletionSource();
            panel.ShowChapterRange(chapter, start, end, () => tcs.TrySetResult(), 2.0f);

            // 跳过时立即关闭对白面板并放行
            Action onSkip = () => panel.CloseCutsceneLines();
            director.Ctx.SkipRequestedSet += onSkip;
            await tcs.Task;
            director.Ctx.SkipRequestedSet -= onSkip;
        }

        private static async Task HandleMove(CutsceneDirector director, CutsceneCue cue)
        {
            if (!cue.Has("x") || !cue.Has("y"))
                throw new CutsceneCueException(cue.Seq, "move 缺少坐标 x/y");

            var entity = director.ResolveActor(cue.Actor, cue.Seq);
            var target = new Vector2I(cue.GetInt("x"), cue.GetInt("y"));

            var grid = director.GetGrid();
            if (grid == null || !grid.IsInBounds(target))
                throw new CutsceneCueException(cue.Seq, $"move 目标格非法：({target.X}, {target.Y}) 不在当前地图范围内");

            // 时长：缺省按默认速度（与玩家单格移动时长一致），按距离折算
            float duration = cue.GetFloat("sec", 0f);
            if (duration <= 0f)
            {
                float worldDist = entity.Position.DistanceTo(entity.GetWorldPositionForGridPos(target));
                duration = Mathf.Max(0.15f, 0.18f * worldDist / Mathf.Max(1, grid.GridSize));
            }

            // 暂停期间实体自身 Tween 会冻结，由 Director（Always）代为创建（见 EntityBase.TweenHostOverride）
            entity.TweenHostOverride = director;
            try
            {
                if (entity is not Monster && entity is not StoryActor)
                    entity.Direction = EntityBase.DirectionFromVector(target.X - entity.GridPos.X, target.Y - entity.GridPos.Y);

                // 纯播放原则：真实实体只动视觉位置，记录原始位置供退出演出时还原（假人销毁即可，无需还原）
                if (entity is not StoryActor && !director.Ctx.MovedEntityOriginalPositions.ContainsKey(entity))
                    director.Ctx.MovedEntityOriginalPositions[entity] = entity.Position;

                entity.MoveTo(target, duration);
                await director.WaitTween(entity.CurrentTween);
            }
            finally
            {
                entity.TweenHostOverride = null;
            }
        }

        private static Task HandleFace(CutsceneDirector director, CutsceneCue cue)
        {
            int direction = ParseDir(cue.GetString("dir", "down"), cue.Seq, "face");
            var entity = director.ResolveActor(cue.Actor, cue.Seq);
            entity.Direction = direction;
            return Task.CompletedTask;
        }

        /// <summary>把方向字符串解析为 EntityBase.Direction（0=右, 1=下, 2=左, 3=上）。非法值抛 CutsceneCueException。</summary>
        private static int ParseDir(string dir, int seq, string cueType)
        {
            return dir.ToLowerInvariant() switch
            {
                "right" => 0,
                "down" => 1,
                "left" => 2,
                "up" => 3,
                _ => throw new CutsceneCueException(seq, $"{cueType} 方向非法：{dir}（应为 up/down/left/right）"),
            };
        }

        private static async Task HandleCameraFocus(CutsceneDirector director, CutsceneCue cue)
        {
            var camera = director.GetCamera();
            if (camera == null)
                throw new CutsceneCueException(cue.Seq, "找不到相机（CameraController）");

            Vector2 targetPos;
            if (cue.Has("x") && cue.Has("y"))
            {
                var gridPos = new Vector2I(cue.GetInt("x"), cue.GetInt("y"));
                var grid = director.GetGrid();
                if (grid == null || !grid.IsInBounds(gridPos))
                    throw new CutsceneCueException(cue.Seq, $"camera_focus 坐标非法：({gridPos.X}, {gridPos.Y})");
                targetPos = grid.GridToWorld(gridPos);
            }
            else if (!string.IsNullOrWhiteSpace(cue.Actor))
            {
                var entity = director.ResolveActor(cue.Actor, cue.Seq);
                // 用当前视觉位置而非 GridPos 换算点：Npc/Player 的格子坐标不随 move cue 更新，
                // 走位后用 GridPos 会把镜头聚焦到旧格子
                targetPos = entity.GlobalPosition;
            }
            else
            {
                throw new CutsceneCueException(cue.Seq, "camera_focus 需要坐标 x/y 或 actor");
            }

            float sec = cue.GetFloat("sec", 1.0f);
            float zoom = cue.GetFloat("zoom", 0f);

            var tween = director.CreateTween();
            tween.SetParallel(true);
            tween.SetTrans(Tween.TransitionType.Quad);
            tween.SetEase(Tween.EaseType.InOut);
            tween.TweenProperty(camera, "position", targetPos, sec);
            if (zoom > 0f)
                tween.TweenProperty(camera, "zoom", new Vector2(zoom, zoom), sec);
            await director.WaitTween(tween);
        }

        /// <summary>camera_set：瞬移设置机位（无 tween，立即生效），zoom 可选（§5.5）</summary>
        private static Task HandleCameraSet(CutsceneDirector director, CutsceneCue cue)
        {
            var camera = director.GetCamera();
            if (camera == null)
                throw new CutsceneCueException(cue.Seq, "找不到相机（CameraController）");
            if (!cue.Has("x") || !cue.Has("y"))
                throw new CutsceneCueException(cue.Seq, "camera_set 缺少坐标 x/y");

            var gridPos = new Vector2I(cue.GetInt("x"), cue.GetInt("y"));
            var grid = director.GetGrid();
            if (grid == null || !grid.IsInBounds(gridPos))
                throw new CutsceneCueException(cue.Seq, $"camera_set 坐标非法：({gridPos.X}, {gridPos.Y})");

            camera.GlobalPosition = grid.GridToWorld(gridPos);
            float zoom = cue.GetFloat("zoom", 0f);
            if (zoom > 0f)
                camera.Zoom = new Vector2(zoom, zoom);
            return Task.CompletedTask;
        }

        private static async Task HandleCameraReset(CutsceneDirector director, CutsceneCue cue)
        {
            var camera = director.GetCamera();
            if (camera == null)
                throw new CutsceneCueException(cue.Seq, "找不到相机（CameraController）");

            float sec = cue.GetFloat("sec", 1.0f);

            // 回到进入演出时的快照（即玩家身边），缩放一并还原
            var tween = director.CreateTween();
            tween.SetParallel(true);
            tween.SetTrans(Tween.TransitionType.Quad);
            tween.SetEase(Tween.EaseType.InOut);
            tween.TweenProperty(camera, "position", camera.CutsceneHomePosition, sec);
            tween.TweenProperty(camera, "zoom", camera.CutsceneHomeZoom, sec);
            await director.WaitTween(tween);
        }

        private static async Task HandleCameraShake(CutsceneDirector director, CutsceneCue cue)
        {
            var camera = director.GetCamera();
            if (camera == null)
                throw new CutsceneCueException(cue.Seq, "找不到相机（CameraController）");

            float strength = cue.GetFloat("strength", 8f);
            float sec = cue.GetFloat("sec", 0.4f);
            camera.Shake(strength, sec);
            await director.WaitSeconds(sec);
        }

        private static async Task HandleFade(CutsceneDirector director, CutsceneCue cue)
        {
            string dir = cue.GetString("dir", "out").ToLowerInvariant();
            if (dir != "out" && dir != "in")
                throw new CutsceneCueException(cue.Seq, $"fade 方向非法：{dir}（应为 out/in）");

            var transition = ScreenTransition.Get();
            if (transition == null)
                throw new CutsceneCueException(cue.Seq, "转场层（ScreenTransition）不可用");

            float sec = cue.GetFloat("sec", 0.8f);
            var tcs = new TaskCompletionSource();
            if (dir == "out")
                transition.FadeToBlack(() => tcs.TrySetResult(), sec);
            else
                transition.FadeFromBlack(() => tcs.TrySetResult(), sec);

            // 跳过时立即收尾转场，不阻塞退出流程
            Action onSkip = () =>
            {
                transition.ResetInstant();
                tcs.TrySetResult();
            };
            director.Ctx.SkipRequestedSet += onSkip;
            await tcs.Task;
            director.Ctx.SkipRequestedSet -= onSkip;

            // 维护幕布状态：fade out 完整完成才算黑幕中；被跳过/ResetInstant 兜底后视为已亮场
            director.IsScreenBlack = !director.Ctx.SkipRequested && dir == "out";
        }

        private static Task HandleActorEnter(CutsceneDirector director, CutsceneCue cue)
        {
            if (!int.TryParse(cue.Actor, out int actorId))
                throw new CutsceneCueException(cue.Seq, "actor_enter 缺少或非法 actor（应为演员 id）");
            if (!cue.Has("x") || !cue.Has("y"))
                throw new CutsceneCueException(cue.Seq, "actor_enter 缺少落点坐标 x/y");

            // 登场朝向：默认 down（与 EntityBase 默认方向一致），缺省不报错以保持兼容
            int direction = ParseDir(cue.GetString("dir", "down"), cue.Seq, "actor_enter");
            director.SpawnActor(actorId, cue.GetInt("x"), cue.GetInt("y"), cue.Seq, direction);
            return Task.CompletedTask;
        }

        private static Task HandleActorLeave(CutsceneDirector director, CutsceneCue cue)
        {
            if (!int.TryParse(cue.Actor, out int actorId))
                throw new CutsceneCueException(cue.Seq, "actor_leave 缺少或非法 actor（应为演员 id）");
            director.DespawnActor(actorId, cue.Seq);
            return Task.CompletedTask;
        }

        private static Task HandleAudioLog(CutsceneCue cue, string kind)
        {
            // 第一期无音频资产与 AudioManager，仅解析记录日志；第二期接音频系统后生效
            GD.Print($"[Cutscene] {kind} cue（暂不播放）：{cue.GetString("name")}");
            return Task.CompletedTask;
        }
    }
}
