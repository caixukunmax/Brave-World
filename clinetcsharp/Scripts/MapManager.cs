using Godot;
using Protocol;

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
            nm.MonsterMoveNotify += OnMonsterMove;
            nm.DropSpawnNotify += OnDropSpawn;
            nm.DropPickupNotify += OnDropPickup;
            nm.DropRemoveNotify += OnDropRemove;

            // 如果已经有缓存数据（热加载场景），直接生成
            if (nm.Chests.Count > 0 || nm.Monsters.Count > 0 || nm.Npcs.Count > 0)
            {
                SpawnMapEntities();
            }
        }

        private void OnMapInfoReceived(Game.MapInfoSyncNotify notify)
        {
            GD.Print($"[MapManager] MapInfoReceived map={notify.MapName} chests={notify.Chests.Count} monsters={notify.Monsters.Count} npcs={notify.Npcs.Count} tiles={notify.Tiles.Count}");
            
            // 同步服务端地形数据到本地 GridCell
            var gridMgr = GetTree()?.GetFirstNodeInGroup("grid_manager") as GridManager;
            if (gridMgr != null && notify.Tiles.Count > 0)
            {
                foreach (var tile in notify.Tiles)
                {
                    var cell = gridMgr.GetCell((int)tile.X, (int)tile.Y);
                    if (cell != null && cell.TerrainType != tile.TerrainType)
                    {
                        cell.TerrainType = (int)tile.TerrainType;
                        cell.RefreshTerrainConfig();
                    }
                }
                GD.Print($"[MapManager] Synced {notify.Tiles.Count} terrain tiles from server");
                gridMgr.QueueRedraw();
            }
            
            SpawnMapEntities();
        }

        private void OnMonsterMove(Game.MonsterMoveNotify notify)
        {
            var monsterMgr = GetTree()?.GetFirstNodeInGroup("monster_manager") as MonsterManager;
            monsterMgr?.OnMonsterMove(notify.InstanceId, new Vector2I(notify.FromX, notify.FromY), new Vector2I(notify.ToX, notify.ToY), notify.State, notify.DurationMs);
        }

        private void OnDropSpawn(Game.DropSpawnNotify notify)
        {
            var dropMgr = GetDropManager();
            dropMgr?.OnDropSpawnNotify(notify);
        }

        private void OnDropPickup(Game.DropPickupNotify notify)
        {
            var dropMgr = GetDropManager();
            dropMgr?.OnDropPickupNotify(notify);
        }

        private void OnDropRemove(Game.DropRemoveNotify notify)
        {
            var dropMgr = GetDropManager();
            dropMgr?.OnDropRemoveNotify(notify);
        }

        private DropManager? GetDropManager()
        {
            var dropMgr = GetTree()?.GetFirstNodeInGroup("drop_manager") as DropManager;
            if (dropMgr == null)
            {
                dropMgr = new DropManager();
                dropMgr.Name = "DropManager";
                var gridMgr = GetTree()?.GetFirstNodeInGroup("grid_manager") as GridManager;
                int gridSize = gridMgr?.GridSize ?? 111;
                dropMgr.Init(gridSize);
                AddChild(dropMgr);
                dropMgr.AddToGroup("drop_manager");
            }
            return dropMgr;
        }

        private void SpawnMapEntities()
        {
            var nm = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
            if (nm == null) return;

            GD.Print($"[MapManager] SpawnMapEntities cached chests={nm.Chests.Count} monsters={nm.Monsters.Count} npcs={nm.Npcs.Count}");

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

            // 生成NPC
            var npcMgr = GetTree()?.GetFirstNodeInGroup("npc_manager") as NpcManager;
            if (npcMgr != null && nm.Npcs.Count > 0)
            {
                npcMgr.SpawnNpcs(nm.Npcs, gridSize);
                GD.Print($"[MapManager] Spawned {nm.Npcs.Count} NPCs");
            }

            // 生成掉落物
            var dropMgr = GetDropManager();
            if (dropMgr != null && nm.Drops.Count > 0)
            {
                dropMgr.OnMapInfoSyncDrops(nm.Drops);
                GD.Print($"[MapManager] Spawned {nm.Drops.Count} drops");
            }
        }

        public override void _ExitTree()
        {
            var nm = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
            if (nm != null)
            {
                nm.MapInfoReceived -= OnMapInfoReceived;
                nm.MonsterMoveNotify -= OnMonsterMove;
                nm.DropSpawnNotify -= OnDropSpawn;
                nm.DropPickupNotify -= OnDropPickup;
                nm.DropRemoveNotify -= OnDropRemove;
            }
        }
    }
}
