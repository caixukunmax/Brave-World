using GameServer.Database.Models;
using GameServer.Database.Repositories;
using GameServer.Services.Core;
using GameServer.Services.Map;
using Microsoft.Extensions.Logging;

namespace GameServer.Services.Player;

/// <summary>
/// 玩家会话管理 — 替代 PlayerManager + PlayerPool 分片
/// 单进程无需分片，共享在线玩家字典
/// </summary>
public class PlayerSessionManager
{
    private readonly RoleRepository _roles;
    private readonly InventoryRepository _inventory;
    private readonly ChestRepository _chests;
    private readonly MapService _mapService;
    private readonly ILogger _logger;

    private readonly Dictionary<long, Role> _onlinePlayers = new();

    public PlayerSessionManager(
        RoleRepository roles,
        InventoryRepository inventory,
        ChestRepository chests,
        MapService mapService,
        ILogger<PlayerSessionManager> logger)
    {
        _roles = roles;
        _inventory = inventory;
        _chests = chests;
        _mapService = mapService;
        _logger = logger;
    }

    // ---- 在线玩家管理 ----

    public bool TryGetPlayer(long accountId, out Role player) => _onlinePlayers.TryGetValue(accountId, out player!);
    public void SetOnline(long accountId, Role role) => _onlinePlayers[accountId] = role;
    public Dictionary<long, Role> OnlinePlayers => _onlinePlayers;

    // ---- 依赖访问（供 Handlers 使用）----

    public RoleRepository Roles => _roles;
    public InventoryRepository Inventory => _inventory;
    public ChestRepository Chests => _chests;
    public MapService MapService => _mapService;
    public ILogger Logger => _logger;
    public ICombatService? CombatService { get; set; }
}
