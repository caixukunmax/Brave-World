using System.Collections.Generic;
using Protocol;
using UnityClientSharp.Net;
using UnityEngine;

namespace UnityClientSharp.Entity
{
    /// <summary>NPC 类型（对齐 Godot NpcType.cs）。</summary>
    public enum NpcType
    {
        None = 0,
        JobMaster = 1,  // 转职大师
        Combatant = 2,  // 可战斗 NPC
    }

    /// <summary>NPC 交互选项定义（对齐 Godot NpcManager.cs NpcInteractOption）。</summary>
    public class NpcInteractOption
    {
        public string Label = "";
        public string DialogText = "";      // 对话内容（空=无对话）
        public bool ShowChangeJob;          // 是否打开转职面板
        public bool TriggerCombat;          // 是否触发战斗
    }

    /// <summary>
    /// NPC 实体（显示层）。
    /// 移植自 Godot Npc.cs 的显示部分：Profile 3 蓝样式、标签 名/转职大师或NPC、条隐藏。
    /// </summary>
    public class NpcEntity : EntityVisualBase
    {
        public ulong NpcInstanceId { get; private set; }
        public string NpcName { get; private set; } = "";
        public NpcType Type { get; private set; }

        public void SetupFromInfo(ulong instanceId, string name, int npcType, Vector2Int gridPos,
            int gridSize, int sizeX, int sizeY, int direction)
        {
            NpcInstanceId = instanceId;
            NpcName = name ?? "";
            Type = (NpcType)npcType;

            Setup(3, gridPos, gridSize, sizeX, sizeY, sortingOrder: 5);
            if (direction >= 0) Direction = direction;

            SetLabel(0, NpcName);
            SetLabel(1, Type == NpcType.JobMaster ? "转职大师" : "NPC");
        }
    }

    /// <summary>
    /// NPC 管理器（显示层 + 交互）。移植自 Godot NpcManager 的生成/交互部分：
    /// 生成、战斗血蓝同步、四邻格检测、交互菜单（配置表驱动）、挑战乐观显示。
    /// </summary>
    public class NpcManager : MonoBehaviour
    {
        public static NpcManager Instance { get; private set; }

        /// <summary>NPC 类型交互配置表 — 新增 NPC 类型只需在此表添加条目（对齐 Godot NpcInteractConfig）。</summary>
        private static readonly Dictionary<NpcType, NpcInteractOption[]> NpcInteractConfig = new()
        {
            [NpcType.JobMaster] = new[]
            {
                new NpcInteractOption { Label = "对话", DialogText = "我可以帮你改变职业，选择你想要的方向吧！" },
                new NpcInteractOption { Label = "转职", ShowChangeJob = true },
            },
            [NpcType.Combatant] = new[]
            {
                new NpcInteractOption { Label = "对话", DialogText = "哼，你有胆量挑战我吗？" },
                new NpcInteractOption { Label = "挑战", TriggerCombat = true },
            },
        };

        /// <summary>查询某 NPC 类型的交互选项（无配置返回 null，调用方走默认对话）。</summary>
        public static NpcInteractOption[] GetInteractOptions(NpcType npcType)
        {
            return NpcInteractConfig.TryGetValue(npcType, out var options) && options.Length > 0
                ? options : null;
        }

        /// <summary>没有交互选项时的默认对话（对齐 Godot GetDefaultDialog）。</summary>
        public static string GetDefaultDialog(NpcType npcType) => "你好，旅行者！";

        private readonly Dictionary<ulong, NpcEntity> _npcById = new();
        private int _gridSize = 111;

        public int GridSize { get => _gridSize; set => _gridSize = value; }

        private void Awake()
        {
            Instance = this;
            Walkability.Npcs = this;
        }

        private void Start()
        {
            if (NetworkManager.Instance == null) return;
            NetworkManager.Instance.CombatStateNotify += OnCombatState;
            NetworkManager.Instance.CombatEndNotify += OnCombatEnd;
            NetworkManager.Instance.NpcInteractNotify += OnNpcInteractNotify;
            NetworkManager.Instance.NpcCombatResponse += OnNpcCombatResponse;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (Walkability.Npcs == this) Walkability.Npcs = null;
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.CombatStateNotify -= OnCombatState;
                NetworkManager.Instance.CombatEndNotify -= OnCombatEnd;
                NetworkManager.Instance.NpcInteractNotify -= OnNpcInteractNotify;
                NetworkManager.Instance.NpcCombatResponse -= OnNpcCombatResponse;
            }
        }

        // ============ 战斗血蓝同步 ============

        /// <summary>战斗中显示血蓝条并同步（对齐 Godot NpcManager）。</summary>
        private void OnCombatState(Game.CombatStateNotify notify)
        {
            foreach (var unit in notify.Units)
            {
                if (!_npcById.TryGetValue(unit.EntityId, out var npc) || npc == null)
                    continue;
                npc.SetHpBarVisible(true);
                npc.SetMpBarVisible(true);
                if (npc.SetHpFill((float)unit.Hp / Mathf.Max(1, unit.MaxHp)))
                    npc.PlayHitEffect();
                npc.SetMpFill((float)unit.Mp / Mathf.Max(1, unit.MaxMp));
            }
        }

        private void OnCombatEnd(Game.CombatEndNotify notify)
        {
            foreach (var npc in _npcById.Values)
            {
                if (npc == null) continue;
                npc.SetHpBarVisible(false);
                npc.SetMpBarVisible(false);
            }
        }

        // ============ 邻格检测与阻挡（供 Walkability 门面 / 玩家移动挂钩）============

        /// <summary>footprint 是否包含指定格（对齐 Godot IsInFootprint）。</summary>
        public static bool IsInFootprint(Vector2Int pos, int anchorX, int anchorY, int sizeX, int sizeY)
        {
            sizeX = Mathf.Max(1, sizeX);
            sizeY = Mathf.Max(1, sizeY);
            return pos.x >= anchorX && pos.x < anchorX + sizeX &&
                   pos.y >= anchorY && pos.y < anchorY + sizeY;
        }

        /// <summary>NPC  footprint 是否包含指定格（锚点公式与 EntityVisualBase.PositionForGridPos 一致）。</summary>
        public static bool IsInFootprint(NpcEntity npc, Vector2Int pos)
        {
            int anchorX = npc.GridPos.x - (Mathf.Max(1, npc.SizeX) - 1) / 2;
            int anchorY = npc.GridPos.y - (Mathf.Max(1, npc.SizeY) - 1) / 2;
            return IsInFootprint(pos, anchorX, anchorY, npc.SizeX, npc.SizeY);
        }

        /// <summary>获取邻格的 NPC（上下左右四方向，按序首个命中；对齐 Godot GetAdjacentNpc）。</summary>
        public NpcEntity GetAdjacentNpc(Vector2Int gridPos)
        {
            var dirs = new[] { new Vector2Int(0, -1), new Vector2Int(0, 1), new Vector2Int(-1, 0), new Vector2Int(1, 0) };
            foreach (var d in dirs)
            {
                var npc = GetNpcAt(gridPos + d);
                if (npc != null) return npc;
            }
            return null;
        }

        public bool IsBlockedByNpc(Vector2Int gridPos) => GetNpcAt(gridPos) != null;

        /// <summary>footprint 覆盖指定格的第一个 NPC。</summary>
        private NpcEntity GetNpcAt(Vector2Int pos)
        {
            foreach (var npc in _npcById.Values)
            {
                if (npc != null && IsInFootprint(npc, pos)) return npc;
            }
            return null;
        }

        // ============ 交互菜单 ============

        private GameObject _interactMenu;

        /// <summary>在 NPC 旁弹出交互菜单（幂等：先关再建；对齐 Godot ShowInteractMenu）。</summary>
        public void ShowInteractMenu(NpcEntity npc, Vector2Int playerGridPos)
        {
            CloseInteractMenu();
            _interactMenu = UI.NpcInteractMenu.Show(npc, playerGridPos);
        }

        public void CloseInteractMenu()
        {
            if (_interactMenu != null)
            {
                Destroy(_interactMenu);
                _interactMenu = null;
            }
        }

        /// <summary>服务器碰撞推送（次通道）：邻接 ≤1 时服务器推 400，同样弹菜单（幂等）。</summary>
        private void OnNpcInteractNotify(Game.NpcInteractNotify notify)
        {
            NpcEntity npc = null;
            _npcById.TryGetValue(notify.NpcInstanceId, out npc);
            if (npc == null) return;

            var player = FindFirstObjectByType<PlayerEntity>();
            var playerPos = player != null ? player.GridPos : npc.GridPos;
            ShowInteractMenu(npc, playerPos);
        }

        // ============ NPC 挑战 ============

        /// <summary>触发 NPC 挑战 — 乐观亮满血蓝条，发 403（对齐 Godot TriggerNpcCombat）。</summary>
        public void TriggerNpcCombat(NpcEntity npc)
        {
            npc.SetHpBarVisible(true);
            npc.SetMpBarVisible(true);
            npc.SetHpFill(1f);
            npc.SetMpFill(1f);

            var nm = NetworkManager.Instance;
            if (nm != null && nm.IsServerConnected())
            {
                nm.SendPacket(MessageId.GameNpcCombatReq, new Game.NpcCombatRequest
                {
                    NpcInstanceId = npc.NpcInstanceId,
                });
            }
            Debug.Log($"[NpcManager] TriggerNpcCombat: npc={npc.NpcName} instanceId={npc.NpcInstanceId}");
        }

        /// <summary>挑战失败回滚：撤掉乐观亮出的血蓝条（对齐 Godot OnNpcCombatResponse）。</summary>
        private void OnNpcCombatResponse(Game.NpcCombatResponse rsp)
        {
            if (rsp.Code == Common.ErrorCode.Success) return;
            if (_npcById.TryGetValue(rsp.NpcInstanceId, out var npc) && npc != null)
            {
                npc.SetHpBarVisible(false);
                npc.SetMpBarVisible(false);
            }
            Debug.Log($"[NpcManager] NpcCombat 失败: {rsp.Code}");
        }

        // ============ 生成 ============

        public void SpawnNpcs(IEnumerable<Game.NpcInfo> npcs)
        {
            ClearNpcs();
            CloseInteractMenu();
            if (npcs == null) return;

            foreach (var info in npcs)
            {
                var go = new GameObject($"Npc_{info.NpcInstanceId}_{info.NpcName}");
                go.transform.SetParent(transform, false);
                var npc = go.AddComponent<NpcEntity>();
                npc.SetupFromInfo(info.NpcInstanceId, info.NpcName, info.NpcType,
                    new Vector2Int(info.X, info.Y), _gridSize,
                    info.SizeX > 0 ? info.SizeX : 1, info.SizeY > 0 ? info.SizeY : 1, info.Direction);
                _npcById[info.NpcInstanceId] = npc;
            }

            Debug.Log($"[NpcManager] 生成 {_npcById.Count} 个 NPC");
        }

        public IEnumerable<NpcEntity> GetNpcs() => _npcById.Values;

        public void ClearNpcs()
        {
            foreach (var npc in _npcById.Values)
                if (npc != null) Destroy(npc.gameObject);
            _npcById.Clear();
        }
    }
}
