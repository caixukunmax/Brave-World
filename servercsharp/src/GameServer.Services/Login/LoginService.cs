using GameServer.Common.Events;
using GameServer.Common.Net;
using GameServer.Common.Security;
using GameServer.Database.Models;
using GameServer.Database.Repositories;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using PCommon = global::Common;
using PLogin = global::Login;
using PProtocol = global::Protocol;
using PServer = global::Server;

namespace GameServer.Services.Login;

/// <summary>
/// 登录服务 — 安全升级版
/// </summary>
public class LoginService
{
    private readonly ILogger<LoginService> _logger;
    private readonly AccountRepository _accounts;
    private readonly RoleRepository _roles;
    private readonly ServerRepository _servers;
    private readonly TokenGenerator _tokenGen;
    private readonly EventBus _eventBus;
    private readonly bool _allowAutoRegister;

    public LoginService(
        ILogger<LoginService> logger,
        AccountRepository accounts,
        RoleRepository roles,
        ServerRepository servers,
        TokenGenerator tokenGen,
        EventBus eventBus,
        IConfiguration config)
    {
        _logger = logger;
        _accounts = accounts;
        _roles = roles;
        _servers = servers;
        _tokenGen = tokenGen;
        _eventBus = eventBus;
        _allowAutoRegister = config.GetValue<bool>("Game:AllowAutoRegister", false);
    }

    public void RegisterRoutes(MessageRouter router, Gateway.GatewayService gateway)
    {
        router.Register((int)PProtocol.MessageId.LoginAccountLoginReq,
            (ctx, data) => HandleAccountLogin(data));
        router.Register((int)PProtocol.MessageId.LoginSelectServerReq,
            (ctx, data) => HandleSelectServer(ctx, data, gateway));
    }

    // ---- AccountLogin (210) ----

    private async Task<byte[]?> HandleAccountLogin(byte[] data)
    {
        var req = PLogin.AccountLoginRequest.Parser.ParseFrom(data);

        if (string.IsNullOrEmpty(req.Username) || req.Username.Length < 2)
            return MakeError(PCommon.ErrorCode.InvalidAccountFormat);

        if (string.IsNullOrEmpty(req.Password))
            return MakeError(PCommon.ErrorCode.InvalidPasswordFormat);

        var account = await _accounts.FindByUsername(req.Username);

        if (account == null)
        {
            if (!_allowAutoRegister)
            {
                _logger.LogWarning("Login failed: {Username} — account not found (auto-register disabled)", req.Username);
                return MakeError(PCommon.ErrorCode.AccountNotFound);
            }

            var hashed = PasswordHasher.Hash(req.Password);
            account = await _accounts.Create(req.Username, hashed);
            _logger.LogInformation("Auto-registered: {Username} id={AccountId}", req.Username, account.AccountId);
        }
        else
        {
            bool shouldMigrate;
            if (!PasswordHasher.Verify(req.Password, account.Password, out shouldMigrate))
                return MakeError(PCommon.ErrorCode.PasswordError);

            // 旧 MD5 密码自动迁移到 bcrypt
            if (shouldMigrate)
            {
                var newHash = PasswordHasher.Hash(req.Password);
                await _accounts.UpdatePassword(account.AccountId, newHash);
                _logger.LogInformation("Password migrated to bcrypt: {Username} id={AccountId}", req.Username, account.AccountId);
            }

            if (account.Status == 1)
                return MakeError(PCommon.ErrorCode.AccountBanned);
        }

        var accountId = account.AccountId;

        var servers = await _servers.GetAll();
        var allRoles = await _roles.FindAllByAccount(accountId);

        var rolesByServer = new Dictionary<int, List<Role>>();
        foreach (var r in allRoles)
        {
            var sid = r.ServerId;
            if (!rolesByServer.TryGetValue(sid, out var list))
            {
                list = new List<Role>();
                rolesByServer[sid] = list;
            }
            list.Add(r);
        }

        var accountToken = _tokenGen.GenerateAccountToken(accountId, req.Username);

        var rsp = new PLogin.AccountLoginResponse
        {
            Code = PCommon.ErrorCode.Success,
            Message = "",
            AccountToken = accountToken,
            AccountId = (uint)accountId,
            LastServerId = (uint)account.LastServerId,
            LastRoleName = account.LastRoleName ?? "",
        };

        foreach (var s in servers)
        {
            var sid = s.ServerId;
            var roles = rolesByServer.GetValueOrDefault(sid) ?? new List<Role>();
            rsp.Servers.Add(new PServer.ServerInfo
            {
                ServerId = (uint)sid,
                ServerName = s.ServerName ?? "",
                Host = s.Host ?? "",
                Port = (uint)s.Port,
                Status = (PCommon.ServerStatus)s.Status,
                OnlineCount = (uint)s.OnlineCount,
                IsNew = s.IsNew,
                IsRecommend = s.IsRecommend,
                HasRole = roles.Count > 0,
                RoleCount = (uint)roles.Count,
            });
        }

        _logger.LogInformation("Login success: {Username} accountId={AccountId}", req.Username, accountId);
        _eventBus.Emit(EventTypes.PlayerLogin, new { account_id = accountId, username = req.Username });

        return rsp.ToByteArray();
    }

    // ---- SelectServer (212) ----

    private async Task<byte[]?> HandleSelectServer(MessageContext ctx, byte[] data, Gateway.GatewayService gateway)
    {
        var req = PLogin.SelectServerRequest.Parser.ParseFrom(data);

        var claims = _tokenGen.ValidateAccountToken(req.AccountToken);
        if (claims == null)
            return MakeError(PCommon.ErrorCode.Unauthorized);

        var server = await _servers.FindById((int)req.ServerId);
        if (server == null)
            return MakeError(PCommon.ErrorCode.ServerNotFound);

        if (server.Status == 0)
            return MakeError(PCommon.ErrorCode.ServerMaintenance);

        var roles = await _roles.FindByAccountAndServer(claims.AccountId, (int)req.ServerId);
        string lastRoleName = "";
        long lastLoginTime = 0;

        var rsp = new PLogin.SelectServerResponse
        {
            Code = PCommon.ErrorCode.Success,
            Message = "",
            MaxRoleCount = 3,
            ServerTime = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        };

        foreach (var r in roles)
        {
            var brief = new PLogin.RoleBrief
            {
                RoleId = (ulong)r.RoleId,
                RoleName = r.RoleName ?? "",
                Level = (uint)r.Level,
                AvatarId = (uint)r.AvatarId,
                LastLogin = (ulong)r.LastLoginTime,
                TotalPower = (uint)r.TotalPower,
            };
            rsp.Roles.Add(brief);

            if ((long)brief.LastLogin > lastLoginTime)
            {
                lastLoginTime = (long)brief.LastLogin;
                lastRoleName = brief.RoleName;
            }
        }

        var gatewayToken = _tokenGen.GenerateGatewayToken(claims.AccountId, (int)req.ServerId);
        rsp.GatewayToken = gatewayToken;

        await _accounts.UpdateLastServer(claims.AccountId, (int)req.ServerId, lastRoleName);

        // Bind token to connection for subsequent authenticated requests
        gateway.BindToken(ctx.ConnId, gatewayToken, claims.AccountId, (int)req.ServerId);

        _logger.LogInformation("SelectServer: accountId={AccountId} serverId={ServerId}", claims.AccountId, req.ServerId);

        return rsp.ToByteArray();
    }

    private static byte[] MakeError(PCommon.ErrorCode code)
    {
        return new PCommon.Response { Code = code, Message = "" }.ToByteArray();
    }

}
