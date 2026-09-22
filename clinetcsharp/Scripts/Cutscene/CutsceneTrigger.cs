using System;
using Godot;

namespace ClinetCSharp.Cutscene
{
    /// <summary>
    /// 区域触发器（§7）— 玩家落格后检查当前地图已配置 trigger 的演出：
    /// 命中 trigger.map + trigger.rect 且（once 时）未看过则自动播放。
    /// 纯客户端本地检测，不动服务器；演出播放期间停止检查，结束后自动恢复（互斥）。
    /// </summary>
    public partial class CutsceneTrigger : Node
    {
        private CutsceneDirector _director;
        private Vector2I? _lastCheckedPos;
        private string _lastMapName;

        public override void _Ready()
        {
            ProcessMode = ProcessModeEnum.Always;
            _director = GetParent() as CutsceneDirector;
        }

        public override void _Process(double delta)
        {
            if (_director == null || _director.IsPlaying)
                return;

            var grid = GetTree()?.GetFirstNodeInGroup("grid_manager") as GridManager;
            var player = GetTree()?.GetFirstNodeInGroup("player") as Player;
            if (grid == null || player == null)
                return;

            // 玩家还在移动途中时等落格稳定再检查（"落格后"语义）
            if (player.IsMoving)
                return;

            var pos = player.GridPos;
            string map = grid.CurrentMapName ?? "";
            if (_lastCheckedPos == pos && _lastMapName == map)
                return;
            _lastCheckedPos = pos;
            _lastMapName = map;

            CheckTriggers(grid, pos, map);
        }

        private void CheckTriggers(GridManager grid, Vector2I pos, string map)
        {
            CutsceneConfigUtil.Load();

            foreach (var pair in CutsceneConfigUtil.Scripts)
            {
                var script = pair.Value;
                var trigger = script.Trigger;
                if (trigger == null || trigger.Rect == null || trigger.Rect.Length != 4)
                    continue;

                // 地图名匹配（不区分大小写；与 NetworkManager.CurrentMapName 同源，如 luoyexiang）
                if (!string.Equals(trigger.Map, map, StringComparison.OrdinalIgnoreCase))
                    continue;

                // rect = [x, y, w, h]，半开区间
                if (pos.X < trigger.Rect[0] || pos.X >= trigger.Rect[0] + trigger.Rect[2] ||
                    pos.Y < trigger.Rect[1] || pos.Y >= trigger.Rect[1] + trigger.Rect[3])
                    continue;

                if (trigger.Once && CutsceneProgress.IsWatched(script.Id))
                    continue;

                GD.Print($"[CutsceneTrigger] 玩家落格 {pos} 命中演出 {script.Id}「{script.Name}」触发区");
                _director.PlayById(script.Id, manual: false);
                return;
            }
        }
    }
}
