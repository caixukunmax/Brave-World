using System;
using System.Collections.Generic;
using Godot;
using Protocol;

namespace ClinetCSharp
{
    /// <summary>
    /// Network entrypoint for TCP, packet IO, runtime cache, and typed events.
    /// Behavior is intentionally unchanged; implementation is split across partial files.
    /// </summary>
    public partial class NetworkManager : Node
    {
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
        public event Action<Game.NpcInteractNotify> NpcInteractNotify;
        public event Action<Game.NpcCombatResponse> NpcCombatResponse;
        public event Action<Game.ChangeJobResponse> ChangeJobResponse;
        public event Action<Game.LevelUpNotify> LevelUpNotify;

        public const string ServerHost = "127.0.0.1";
        public const int ServerPort = 8889;

        private StreamPeerTcp _tcp;
        private bool _connected;
        private byte[] _readBuffer = new byte[8192];
        private int _bufferOffset;
        private int _bufferCount;
        private int _expectedLength = -1;
        private uint _sessionCounter = 1;

        private double _heartbeatInterval = 30.0;
        private double _heartbeatTimer;

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
    }
}
