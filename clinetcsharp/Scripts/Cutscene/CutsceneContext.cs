using System;
using System.Collections.Generic;

namespace ClinetCSharp.Cutscene
{
    /// <summary>
    /// 演出上下文 — 一场演出播放期间共享的状态：
    /// 当前脚本、跳过请求、spawn_actor 创建的本地假人注册表。
    /// 镜头快照由 CameraController.EnterCutsceneMode/ExitCutsceneMode 自行保管。
    /// </summary>
    public class CutsceneContext
    {
        /// <summary>当前播放的脚本</summary>
        public CutsceneScript Script { get; }

        /// <summary>是否由 GM 手动触发（手动触发不受进度限制，也不计入"已看"进度）</summary>
        public bool Manual { get; }

        /// <summary>是否请求跳过（ESC）。一旦置位，当前 cue 尽快结束，后续 cue 不再执行。</summary>
        public bool SkipRequested { get; private set; }

        /// <summary>跳过请求事件：各 cue 执行器订阅以立即收尾（如关闭对白面板）</summary>
        public event Action SkipRequestedSet;

        /// <summary>actor_enter 登场的假人（演员 id -> 实体），退出演出时统一销毁</summary>
        public readonly Dictionary<int, StoryActor> Actors = new();

        /// <summary>
        /// 被 move cue 移动过的真实实体 -> 演出前原始视觉位置。
        /// 纯播放原则（2026-07-22 确认）：演出不修改任何真实状态，move 只做视觉位移，
        /// 退出演出时按此表还原视觉位置（假人直接销毁，不在此表）。
        /// </summary>
        public readonly Dictionary<EntityBase, Godot.Vector2> MovedEntityOriginalPositions = new();

        public CutsceneContext(CutsceneScript script, bool manual)
        {
            Script = script;
            Manual = manual;
        }

        public void RequestSkip()
        {
            if (SkipRequested) return;
            SkipRequested = true;
            SkipRequestedSet?.Invoke();
        }
    }
}
