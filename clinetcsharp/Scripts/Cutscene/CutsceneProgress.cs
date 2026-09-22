using System.Collections.Generic;
using Godot;

namespace ClinetCSharp.Cutscene
{
    /// <summary>
    /// 演出进度本地记录 — 已看演出 ID 列表，存于 user://cutscene_progress.cfg。
    /// 用途：trigger.once=true 的演出只自动播放一次；演出被跳过也计为已看（§7）。
    /// 第二期再上服务器持久化。
    /// </summary>
    public static class CutsceneProgress
    {
        private const string SavePath = "user://cutscene_progress.cfg";
        private const string Section = "progress";
        private const string KeyWatched = "watched";

        private static readonly HashSet<int> _watched = new();
        private static bool _loaded;

        private static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;

            var cfg = new ConfigFile();
            if (cfg.Load(SavePath) != Error.Ok)
                return;

            foreach (var id in cfg.GetValue(Section, KeyWatched, new int[0]).AsInt32Array())
                _watched.Add(id);
        }

        public static bool IsWatched(int cutsceneId)
        {
            EnsureLoaded();
            return _watched.Contains(cutsceneId);
        }

        /// <summary>标记演出为已看并立即落盘（幂等）</summary>
        public static void MarkWatched(int cutsceneId)
        {
            EnsureLoaded();
            if (!_watched.Add(cutsceneId))
                return;

            var cfg = new ConfigFile();
            cfg.Load(SavePath); // 保留文件里可能存在的其他配置
            var ids = new List<int>(_watched);
            ids.Sort();
            cfg.SetValue(Section, KeyWatched, ids.ToArray());
            var err = cfg.Save(SavePath);
            if (err != Error.Ok)
                GD.PrintErr($"[CutsceneProgress] 保存进度失败：{err}");
        }
    }
}
