using Godot;

namespace ClinetCSharp
{
    /// <summary>
    /// 地图管理器 - 统一管理地图实体的生成（网格、宝箱、怪物等）
    /// 挂载到 Main 场景
    /// </summary>
    public partial class MapManager : Node
    {
        public override async void _Ready()
        {
            GD.Print("[MapManager] _Ready");

            // 等待一帧，确保所有子节点 _Ready 已执行
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            var nm = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
            if (nm == null)
            {
                GD.PrintErr("[MapManager] NetworkManager not found");
                return;
            }

            // 连接地图信息同步信号
            nm.MapInfoReceived += OnMapInfoReceived;
            nm.MonsterMoveReceived += OnMonsterMove;

            // 如果已经有缓存数据（热加载场景），直接生成
            if (nm.Chests.Count > 0 || nm.Monsters.Count > 0)
            {
                SpawnMapEntities();
            }
        }

        private void OnMapInfoReceived()
        {
            GD.Print("[MapManager] MapInfoReceived");
            SpawnMapEntities();
        }

        private void OnMonsterMove(uint instanceId, int fromX, int fromY, int toX, int toY, string state, int durationMs)
        {
            var monsterMgr = GetTree()?.GetFirstNodeInGroup("monster_manager") as MonsterManager;
            monsterMgr?.OnMonsterMove(instanceId, new Vector2I(fromX, fromY), new Vector2I(toX, toY), state, durationMs);
        }

        private void SpawnMapEntities()
        {
            var nm = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
            if (nm == null) return;

            var gridMgr = GetTree()?.GetFirstNodeInGroup("grid_manager") as GridManager;
            int gridSize = gridMgr?.GridSize ?? 111;

            // 生成宝箱
            var chestMgr = GetTree()?.GetFirstNodeInGroup("chest_manager") as ChestManager;
            if (chestMgr != null && nm.Chests.Count > 0)
            {
                chestMgr.SpawnChests(nm.Chests, gridSize);
                GD.Print($"[MapManager] Spawned {nm.Chests.Count} chests");
            }

            // 生成怪物
            var monsterMgr = GetTree()?.GetFirstNodeInGroup("monster_manager") as MonsterManager;
            if (monsterMgr != null && nm.Monsters.Count > 0)
            {
                monsterMgr.SpawnMonsters(nm.Monsters, gridSize);
                GD.Print($"[MapManager] Spawned {nm.Monsters.Count} monsters");
            }
        }
    }
}
