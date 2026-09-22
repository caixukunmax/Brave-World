using System;
using System.Collections.Generic;
using Protocol;
using UnityEngine;

namespace UnityClientSharp.Net
{
    /// <summary>
    /// 网络入口：TCP、封包收发、运行时缓存、类型化事件。
    /// 移植自 clinetcsharp/Scripts/NetworkManager*.cs（10 个 partial，共 1045 行），
    /// 拆为 NetworkManager.cs / NetworkManager.Core.cs / NetworkManager.Dispatch.cs 三个 partial。
    /// 线程模型与 Godot 一致：完全单线程（Update 轮询），事件回调即在主线程。
    /// </summary>
    public partial class NetworkManager : MonoBehaviour
    {
        public static NetworkManager Instance { get; private set; }

        // Core events
        public event Action Connected;
        public event Action<string> ConnectionError;
        public event Action Disconnected;
        public event Action<string> Kicked;
        public event Action<Game.ChangeMapResponse> ChangeMapResponse;

        // Typed message events
        public event Action<Login.AccountLoginResponse> LoginResponse;
        public event Action<Login.SelectServerResponse> SelectServerResponse;
        public event Action<Game.EnterGameResponse> EnterGameResponse;
        public event Action<Game.CreateRoleResponse> CreateRoleResponse;
        public event Action<Game.MoveResponse> MoveResponse;
        public event Action<Game.MoveCancelNotify> MoveCancelNotify;
        public event Action<Game.MonsterMoveNotify> MonsterMoveNotify;
        public event Action<Game.MonsterMoveCancelNotify> MonsterMoveCancelNotify;
        public event Action<Game.MonsterDeathNotify> MonsterDeathNotify;
        public event Action<Game.MonsterRespawnNotify> MonsterRespawnNotify;
        public event Action<Game.DirectionNotify> DirectionNotify;
        public event Action<Game.MapInfoSyncNotify> MapInfoReceived;
        public event Action<Game.ChestUpdateNotify> ChestUpdateNotify;
        public event Action<Game.DropSpawnNotify> DropSpawnNotify;
        public event Action<Game.DropPickupNotify> DropPickupNotify;
        public event Action<Game.DropRemoveNotify> DropRemoveNotify;
        public event Action<Game.OpenChestResponse> OpenChestResponse;
        public event Action<Game.CombatLogNotify> CombatLogNotify;
        public event Action<Game.CombatStateNotify> CombatStateNotify;
        public event Action<Game.CastResponse> CastResponse;
        public event Action<Game.BuffUpdateNotify> BuffUpdateNotify;
        public event Action<Game.CombatStartNotify> CombatStartNotify;
        public event Action<Game.CombatEndNotify> CombatEndNotify;
        public event Action<Game.CastStartNotify> CastStartNotify;
        public event Action<Game.CastResultNotify> CastResultNotify;
        public event Action<Game.CombatEventNotify> CombatEventNotify;
        public event Action<Game.ProjectileSpawnNotify> ProjectileSpawnNotify;
        public event Action<Game.ProjectileHitNotify> ProjectileHitNotify;
        public event Action<Game.DisengageNotify> DisengageNotify;
        public event Action<Game.FullRoleInfo> RoleAttrUpdated;
        public event Action<Game.GmCommandResponse> GmResponse;
        public event Action<Game.UseItemResponse> UseItemResponse;
        public event Action<Game.DropItemResponse> DropItemResponse;
        public event Action<Game.InventoryReorderResponse> InventoryReorderResponse;
        public event Action<Game.PlayerDeathNotify> PlayerDeathNotify;
        public event Action<Game.EquipSkillResponse> EquipSkillResponse;
        public event Action<Game.UnequipSkillResponse> UnequipSkillResponse;
        public event Action<Game.SetPreferredSkillResponse> SetPreferredSkillResponse;
        public event Action<Game.NpcInteractNotify> NpcInteractNotify;
        public event Action<Game.NpcCombatResponse> NpcCombatResponse;
        public event Action<Game.ChangeJobResponse> ChangeJobResponse;
        public event Action<Game.LevelUpNotify> LevelUpNotify;

        public const string ServerHost = "127.0.0.1";
        public const int ServerPort = 8889;

        private TcpTransport _tcp;
        private bool _connected;
        private readonly PacketFramer _framer = new();
        private uint _sessionCounter = 1;

        private float _heartbeatTimer;
        private const float HeartbeatInterval = 30.0f;

        private ulong _cachedTimestamp;
        private bool _timestampDirty = true;

        // Login and gateway cache
        public string AccountToken { get; set; } = "";
        public uint AccountId { get; set; }
        public uint LastServerId { get; set; }
        public string LastRoleName { get; set; } = "";
        public string GatewayToken { get; set; } = "";
        public uint MaxRoleCount { get; set; } = 3;
        public uint ServerTime { get; set; }

        // Role and map cache
        public Game.FullRoleInfo CachedRoleInfo { get; set; } = null;
        public string CurrentMapName { get; set; } = "xinshoucun";
        public int SpawnGridX { get; set; } = 25;
        public int SpawnGridY { get; set; } = 25;

        // Runtime entity cache
        public List<Server.ServerInfo> Servers { get; set; } = new();
        public List<Login.RoleBrief> Roles { get; set; } = new();
        public List<Game.ChestInfo> Chests { get; set; } = new();
        public List<Game.MonsterInfo> Monsters { get; set; } = new();
        public List<Game.NpcInfo> Npcs { get; set; } = new();
        public List<Game.DropItemInfo> Drops { get; set; } = new();
        public List<Game.TileInfo> Tiles { get; set; } = new();
        public List<Game.ItemInfo> CachedItems { get; set; } = new();
        public List<uint> CachedLearnedSkills { get; set; } = new();
        public List<uint> CachedEquippedSkills { get; set; } = new();

        public uint? PendingOpenChestId { get; set; }

        // 按 session 关联的一次性响应回调（当前用于请求服务器日志路径）
        private readonly Dictionary<uint, Action<string>> _pendingLogPathCallbacks = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
