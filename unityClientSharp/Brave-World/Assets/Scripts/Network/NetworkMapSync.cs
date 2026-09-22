using UnityClientSharp.Entity;
using UnityClientSharp.Map.Rendering;
using UnityEngine;

namespace UnityClientSharp.Net
{
    /// <summary>
    /// 地图网络同步桥 — 移植自 Godot MapManager 对 MapInfoSyncNotify 的处理：
    /// 地图名不同先整图加载本地 JSON，然后服务器 tiles 以差异方式写回 GridCell（不重建 GridData），
    /// 最后刷新地形遮罩并重建装饰实体。
    /// TODO(在线实体阶段): 宝箱/怪物/NPC/掉落生成（缓存已在 NetworkManager 就位）。
    /// </summary>
    [RequireComponent(typeof(GridManager))]
    public class NetworkMapSync : MonoBehaviour
    {
        private GridManager _gm;
        private MapDecorationManager _decoMgr;

        private void Awake()
        {
            _gm = GetComponent<GridManager>();
            _decoMgr = GetComponent<MapDecorationManager>();
        }

        private void Start()
        {
            if (NetworkManager.Instance == null) return;
            NetworkManager.Instance.MapInfoReceived += OnMapInfoReceived;
            // 服务器可能先发 MapInfoSync 再回 EnterGameRsp（已实证）：
            // 玩家生成不能只听 MapInfoReceived，角色数据就绪时也要尝试
            NetworkManager.Instance.EnterGameResponse += OnRoleInfoReady;
            NetworkManager.Instance.CreateRoleResponse += OnRoleInfoReady;
        }

        private void OnDestroy()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.MapInfoReceived -= OnMapInfoReceived;
                NetworkManager.Instance.EnterGameResponse -= OnRoleInfoReady;
                NetworkManager.Instance.CreateRoleResponse -= OnRoleInfoReady;
            }
        }

        private void OnRoleInfoReady(Game.EnterGameResponse rsp) => SpawnPlayerIfNeeded(NetworkManager.Instance);
        private void OnRoleInfoReady(Game.CreateRoleResponse rsp) => SpawnPlayerIfNeeded(NetworkManager.Instance);

        private void OnMapInfoReceived(Game.MapInfoSyncNotify notify)
        {
            // 1) 地图不同：先加载本地地图 JSON（装饰也会随之重建）
            bool mapSwitched = _gm.CurrentMapName != notify.MapName;
            if (mapSwitched)
            {
                _gm.LoadMap(notify.MapName);
            }

            // 2) 服务器 tiles 差异写回本地 GridCell（对齐 Godot：不重建 GridData）
            bool tilesChanged = false;
            foreach (var tile in notify.Tiles)
            {
                var cell = _gm.GetCell(new Vector2Int(tile.X, tile.Y));
                if (cell == null) continue;

                int terrain = (int)tile.TerrainType;
                if (cell.TerrainType != terrain)
                {
                    cell.TerrainType = terrain;
                    cell.RefreshTerrainConfig();
                    tilesChanged = true;
                }
                if (cell.DecorationType != tile.DecorationType)
                {
                    cell.DecorationType = tile.DecorationType;
                    tilesChanged = true;
                }
            }

            // 3) 刷新渲染与装饰
            if (tilesChanged || mapSwitched)
                _gm.NotifyTerrainChanged();

            // 4) 在线实体生成（顺序对齐 Godot MapManager.SpawnMapEntities：宝箱→怪物→NPC→掉落→装饰）
            var nm = NetworkManager.Instance;
            if (nm != null)
            {
                if (ChestManager.Instance != null) ChestManager.Instance.SpawnChests(nm.Chests);
                if (MonsterManager.Instance != null) MonsterManager.Instance.SpawnMonsters(nm.Monsters);
                if (NpcManager.Instance != null) NpcManager.Instance.SpawnNpcs(nm.Npcs);
                if (DropManager.Instance != null) DropManager.Instance.SpawnDrops(nm.Drops);
            }
            if (_decoMgr != null)
                _decoMgr.SpawnDecorations(_gm.GridData);

            // 5) 玩家生成（角色数据就绪且未生成时）
            SpawnPlayerIfNeeded(nm);

            Debug.Log($"[NetworkMapSync] 已同步地图 {notify.MapName} tiles={notify.Tiles.Count} changed={tilesChanged}");
        }

        /// <summary>用 NetworkManager 缓存重放地图同步（登录路径：MapInfo 可能先于本组件创建到达）。
        /// 缓存为空（尚未收到过 360）时不动——冒烟/常规在线路径的首次同步仍由事件驱动。</summary>
        public void SyncFromCache()
        {
            var nm = NetworkManager.Instance;
            if (nm == null) return;
            bool hasData = nm.Tiles.Count > 0 || nm.Chests.Count > 0 || nm.Monsters.Count > 0
                           || nm.Npcs.Count > 0 || nm.Drops.Count > 0;
            if (!hasData) return;
            var notify = new Game.MapInfoSyncNotify { MapName = nm.CurrentMapName };
            notify.Tiles.AddRange(nm.Tiles);
            OnMapInfoReceived(notify);
        }

        private void SpawnPlayerIfNeeded(NetworkManager nm)
        {
            if (nm == null || nm.CachedRoleInfo == null || PlayerEntity.Instance != null)
                return;

            var go = new GameObject("Player");
            go.transform.SetParent(transform, false);
            var player = go.AddComponent<PlayerEntity>();
            player.SetupFromRoleInfo(nm.CachedRoleInfo, _gm.GridSize);

            // 相机跟随
            if (MapCameraController.Instance != null)
                MapCameraController.Instance.FollowTarget = player.transform;

            Debug.Log($"[NetworkMapSync] 玩家生成: {nm.CachedRoleInfo.RoleName} at ({nm.CachedRoleInfo.GridX},{nm.CachedRoleInfo.GridY})");
        }
    }
}
