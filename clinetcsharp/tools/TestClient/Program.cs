using System.Net.Sockets;
using System.Linq;
using Google.Protobuf;
using PCommon = global::Common;
using PProtocol = global::Protocol;
using PLogin = global::Login;
using PGame = global::Game;
using PGateway = global::Gateway;

namespace TestClient;

/// <summary>
/// Game automation test client — sends protobuf messages directly to the server.
/// No Godot UI required. Can be used for integration testing and bot simulation.
/// </summary>
class Program
{
    static string Host = "127.0.0.1";
    static int Port = 8889;
    static uint SessionCounter = 1;

    static TcpClient? Tcp;
    static NetworkStream? Stream;
    static byte[] ReadBuffer = new byte[0];
    static int ExpectedLength = -1;

    static string AccountToken = "";
    static string GatewayToken = "";
    static uint AccountId = 0;
    static ulong RoleId = 0;
    static string CurrentMap = "xinshoucun";
    static int GridX = 25;
    static int GridY = 25;

    static readonly System.Collections.Generic.List<PCommon.Packet> PushQueue = new();

    static async Task Main(string[] args)
    {
        if (args.Length == 0 || args[0] == "--help" || args[0] == "-h")
        {
            PrintHelp();
            return;
        }

        var cmd = args[0].ToLowerInvariant();

        // Parse global flags
        for (int i = 1; i < args.Length; i++)
        {
            if (args[i] == "--host" && i + 1 < args.Length) { Host = args[++i]; }
            else if (args[i] == "--port" && i + 1 < args.Length) { Port = int.Parse(args[++i]); }
        }

        try
        {
            switch (cmd)
            {
                case "ping":
                    await RunPing();
                    break;
                case "login":
                    {
                        var username = GetArg(args, "--username", "test");
                        var password = GetArg(args, "--password", "test");
                        await RunLogin(username, password);
                        break;
                    }
                case "enter":
                    {
                        var username = GetArg(args, "--username", "test");
                        var password = GetArg(args, "--password", "test");
                        var roleName = GetArg(args, "--role-name", "");
                        await RunEnterGame(username, password, roleName);
                        break;
                    }
                case "move":
                    {
                        var username = GetArg(args, "--username", "test");
                        var password = GetArg(args, "--password", "test");
                        var x = int.Parse(GetArg(args, "--x", "25"));
                        var y = int.Parse(GetArg(args, "--y", "25"));
                        await RunMove(username, password, x, y);
                        break;
                    }
                case "gm":
                    {
                        var username = GetArg(args, "--username", "test");
                        var password = GetArg(args, "--password", "test");
                        var gmCmd = GetArg(args, "--cmd", "help");
                        await RunGm(username, password, gmCmd);
                        break;
                    }
                case "combat":
                    {
                        var username = GetArg(args, "--username", "test");
                        var password = GetArg(args, "--password", "test");
                        var tx = int.Parse(GetArg(args, "--tx", "20"));
                        var ty = int.Parse(GetArg(args, "--ty", "20"));
                        await RunCombatTest(username, password, tx, ty);
                        break;
                    }
                case "flee":
                    {
                        var username = GetArg(args, "--username", "test");
                        var password = GetArg(args, "--password", "test");
                        var tx = int.Parse(GetArg(args, "--tx", "20"));
                        var ty = int.Parse(GetArg(args, "--ty", "20"));
                        var fleeX = int.Parse(GetArg(args, "--flee-x", "40"));
                        var fleeY = int.Parse(GetArg(args, "--flee-y", "40"));
                        await RunFleeTest(username, password, tx, ty, fleeX, fleeY);
                        break;
                    }
                case "bot":
                    {
                        var username = GetArg(args, "--username", "bot");
                        var password = GetArg(args, "--password", "bot");
                        var durationSec = int.Parse(GetArg(args, "--duration", "60"));
                        await RunBot(username, password, durationSec);
                        break;
                    }
                default:
                    Console.WriteLine($"Unknown command: {cmd}");
                    PrintHelp();
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] {ex.Message}");
            Environment.Exit(1);
        }
        finally
        {
            Disconnect();
        }
    }

    // ─────────────────────────────────────────────────────────
    // Command implementations
    // ─────────────────────────────────────────────────────────

    static async Task RunPing()
    {
        await ConnectAsync();
        Console.WriteLine("[OK] Server is reachable");
    }

    static async Task RunLogin(string username, string password)
    {
        await ConnectAsync();
        await SendGatewayConnect();

        var rsp = await DoLogin(username, password);
        if (rsp == null || rsp.Code != PCommon.ErrorCode.Success)
        {
            Console.WriteLine($"[FAIL] Login failed: code={rsp?.Code}, msg={rsp?.Message}");
            return;
        }
        Console.WriteLine($"[OK] Login success: accountId={AccountId}, token={AccountToken[..Math.Min(16, AccountToken.Length)]}...");
        Console.WriteLine($"[INFO] Servers: {rsp.Servers.Count}");
    }

    static async Task RunEnterGame(string username, string password, string roleName)
    {
        await ConnectAsync();
        await SendGatewayConnect();

        // Step 1: Login
        var loginRsp = await DoLogin(username, password);
        if (loginRsp == null || loginRsp.Code != PCommon.ErrorCode.Success)
        {
            Console.WriteLine($"[FAIL] Login failed");
            return;
        }

        // Step 2: Select server (serverId = 1)
        var selectRsp = await DoSelectServer(1);
        if (selectRsp == null || selectRsp.Code != PCommon.ErrorCode.Success)
        {
            Console.WriteLine($"[FAIL] SelectServer failed");
            return;
        }

        // Step 3: Enter game or create role
        if (selectRsp.Roles.Count > 0)
        {
            RoleId = selectRsp.Roles[0].RoleId;
            var enterRsp = await DoEnterGame(RoleId);
            if (enterRsp != null && enterRsp.Code == PCommon.ErrorCode.Success)
            {
                CurrentMap = enterRsp.RoleInfo.CurrentMap;
                GridX = enterRsp.RoleInfo.GridX;
                GridY = enterRsp.RoleInfo.GridY;
                Console.WriteLine($"[OK] EnterGame: {enterRsp.RoleInfo.RoleName} @ {CurrentMap} ({GridX},{GridY})");
            }
            else
            {
                Console.WriteLine($"[FAIL] EnterGame failed: {enterRsp?.Code}");
            }
        }
        else
        {
            // Create role
            var name = string.IsNullOrEmpty(roleName) ? $"Bot_{Guid.NewGuid().ToString()[..6]}" : roleName;
            var createRsp = await DoCreateRole(name);
            if (createRsp != null && createRsp.Code == PCommon.ErrorCode.Success)
            {
                CurrentMap = createRsp.RoleInfo.CurrentMap;
                GridX = createRsp.RoleInfo.GridX;
                GridY = createRsp.RoleInfo.GridY;
                Console.WriteLine($"[OK] CreateRole: {createRsp.RoleInfo.RoleName} @ {CurrentMap} ({GridX},{GridY})");
            }
            else
            {
                Console.WriteLine($"[FAIL] CreateRole failed: {createRsp?.Code}");
            }
        }
    }

    static async Task RunMove(string username, string password, int x, int y)
    {
        await ConnectAsync();
        await SendGatewayConnect();

        // Auto login + enter game
        var loginRsp = await DoLogin(username, password);
        if (loginRsp?.Code != PCommon.ErrorCode.Success) { Console.WriteLine("[FAIL] Login"); return; }

        var selectRsp = await DoSelectServer(1);
        if (selectRsp?.Code != PCommon.ErrorCode.Success) { Console.WriteLine("[FAIL] SelectServer"); return; }

        if (selectRsp.Roles.Count == 0) { Console.WriteLine("[FAIL] No role"); return; }
        RoleId = selectRsp.Roles[0].RoleId;

        var enterRsp = await DoEnterGame(RoleId);
        if (enterRsp?.Code != PCommon.ErrorCode.Success) { Console.WriteLine("[FAIL] EnterGame"); return; }

        // Move
        var moveRsp = await DoMove(x, y);
        if (moveRsp?.Code == PCommon.ErrorCode.Success)
        {
            Console.WriteLine($"[OK] Moved to ({moveRsp.X}, {moveRsp.Y})");
        }
        else
        {
            Console.WriteLine($"[FAIL] Move failed: {moveRsp?.Code}");
        }
    }

    static async Task RunGm(string username, string password, string gmCmd)
    {
        await ConnectAsync();
        await SendGatewayConnect();

        var loginRsp = await DoLogin(username, password);
        if (loginRsp?.Code != PCommon.ErrorCode.Success) { Console.WriteLine("[FAIL] Login"); return; }

        var selectRsp = await DoSelectServer(1);
        if (selectRsp?.Code != PCommon.ErrorCode.Success) { Console.WriteLine("[FAIL] SelectServer"); return; }

        if (selectRsp.Roles.Count == 0) { Console.WriteLine("[FAIL] No role"); return; }
        RoleId = selectRsp.Roles[0].RoleId;

        var enterRsp = await DoEnterGame(RoleId);
        if (enterRsp?.Code != PCommon.ErrorCode.Success) { Console.WriteLine("[FAIL] EnterGame"); return; }

        var gmRsp = await DoGm(gmCmd);
        Console.WriteLine($"[GM] code={gmRsp?.Code}, msg={gmRsp?.Message}");
    }

    static async Task RunFleeTest(string username, string password, int targetX, int targetY, int fleeX, int fleeY)
    {
        await ConnectAsync();
        await SendGatewayConnect();

        var loginRsp = await DoLogin(username, password);
        if (loginRsp?.Code != PCommon.ErrorCode.Success) { Console.WriteLine("[FAIL] Login"); return; }

        var selectRsp = await DoSelectServer(1);
        if (selectRsp?.Code != PCommon.ErrorCode.Success) { Console.WriteLine("[FAIL] SelectServer"); return; }

        if (selectRsp.Roles.Count == 0)
        {
            var name = $"Test_{Guid.NewGuid().ToString()[..6]}";
            var createRsp = await DoCreateRole(name);
            if (createRsp?.Code != PCommon.ErrorCode.Success) { Console.WriteLine("[FAIL] CreateRole"); return; }
            RoleId = createRsp.RoleInfo.RoleId;
        }
        else
        {
            RoleId = selectRsp.Roles[0].RoleId;
        }

        var enterRsp = await DoEnterGame(RoleId);
        if (enterRsp?.Code != PCommon.ErrorCode.Success) { Console.WriteLine("[FAIL] EnterGame"); return; }
        CurrentMap = enterRsp.RoleInfo.CurrentMap;
        GridX = enterRsp.RoleInfo.GridX;
        GridY = enterRsp.RoleInfo.GridY;

        Console.WriteLine($"[FLEE] In game at ({GridX},{GridY}) on {CurrentMap}");

        // Step 1: Teleport near monster to trigger combat
        var gmRsp = await DoGm($"teleport,{targetX},{targetY}");
        Console.WriteLine($"[GM] teleport to monster: {gmRsp?.Message}");
        if (gmRsp?.Code == PCommon.ErrorCode.Success)
        {
            var parts = gmRsp.Message?.Split(':');
            if (parts != null && parts.Length >= 3)
            {
                if (int.TryParse(parts[1], out var nx)) GridX = nx;
                if (int.TryParse(parts[2], out var ny)) GridY = ny;
            }
        }
        DrainPushQueue();

        // Move one step to trigger collision/combat
        int dx = targetX > GridX ? 1 : (targetX < GridX ? -1 : 0);
        int dy = targetY > GridY ? 1 : (targetY < GridY ? -1 : 0);
        if (dx == 0 && dy == 0) dx = 1;
        var moveRsp = await DoMove(GridX + dx, GridY + dy);
        Console.WriteLine($"[MOVE] trigger move: code={moveRsp?.Code}, pos=({moveRsp?.X},{moveRsp?.Y})");

        // Wait 3s for combat to establish
        Console.WriteLine("[FLEE] Waiting 3s for combat to establish...");
        await WatchPushMessages(3000);

        // Step 2: Teleport far away to force chase timeout
        Console.WriteLine($"[FLEE] Teleporting far away to ({fleeX},{fleeY})...");
        var fleeRsp = await DoGm($"teleport,{fleeX},{fleeY}");
        Console.WriteLine($"[GM] flee teleport: {fleeRsp?.Message}");
        if (fleeRsp?.Code == PCommon.ErrorCode.Success)
        {
            var parts = fleeRsp.Message?.Split(':');
            if (parts != null && parts.Length >= 3)
            {
                if (int.TryParse(parts[1], out var nx)) GridX = nx;
                if (int.TryParse(parts[2], out var ny)) GridY = ny;
            }
        }
        DrainPushQueue();

        // Step 3: Watch for 20s to observe chase -> timeout -> return -> re-aggro?
        Console.WriteLine("[FLEE] Watching monster behavior for 20s...");
        await WatchPushMessages(20000);
        Console.WriteLine("[FLEE] Test complete.");
    }

    static async Task RunCombatTest(string username, string password, int targetX, int targetY)
    {
        await ConnectAsync();
        await SendGatewayConnect();

        // Login flow
        var loginRsp = await DoLogin(username, password);
        if (loginRsp?.Code != PCommon.ErrorCode.Success) { Console.WriteLine("[FAIL] Login"); return; }

        var selectRsp = await DoSelectServer(1);
        if (selectRsp?.Code != PCommon.ErrorCode.Success) { Console.WriteLine("[FAIL] SelectServer"); return; }

        if (selectRsp.Roles.Count == 0)
        {
            var name = $"Test_{Guid.NewGuid().ToString()[..6]}";
            var createRsp = await DoCreateRole(name);
            if (createRsp?.Code != PCommon.ErrorCode.Success) { Console.WriteLine("[FAIL] CreateRole"); return; }
            RoleId = createRsp.RoleInfo.RoleId;
        }
        else
        {
            RoleId = selectRsp.Roles[0].RoleId;
        }

        var enterRsp = await DoEnterGame(RoleId);
        if (enterRsp?.Code != PCommon.ErrorCode.Success) { Console.WriteLine("[FAIL] EnterGame"); return; }
        CurrentMap = enterRsp.RoleInfo.CurrentMap;
        GridX = enterRsp.RoleInfo.GridX;
        GridY = enterRsp.RoleInfo.GridY;

        Console.WriteLine($"[COMBAT] In game at ({GridX},{GridY}) on {CurrentMap}");

        // Step 1: Teleport near target
        var gmRsp = await DoGm($"teleport,{targetX},{targetY}");
        Console.WriteLine($"[GM] teleport result: code={gmRsp?.Code}, msg={gmRsp?.Message}");
        if (gmRsp?.Code == PCommon.ErrorCode.Success)
        {
            // Parse teleport result to update coords
            var parts = gmRsp.Message?.Split(':');
            if (parts != null && parts.Length >= 3)
            {
                if (int.TryParse(parts[1], out var nx)) GridX = nx;
                if (int.TryParse(parts[2], out var ny)) GridY = ny;
            }
            Console.WriteLine($"[COMBAT] Teleported to ({GridX},{GridY})");
        }
        else
        {
            Console.WriteLine("[FAIL] Teleport failed");
            return;
        }

        // Drain any push messages that arrived during setup
        DrainPushQueue();

        // Step 2: Move one step toward the target to trigger collision
        int dx = targetX > GridX ? 1 : (targetX < GridX ? -1 : 0);
        int dy = targetY > GridY ? 1 : (targetY < GridY ? -1 : 0);
        if (dx == 0 && dy == 0) dx = 1; // already at target, move anyway

        int moveTx = GridX + dx;
        int moveTy = GridY + dy;
        Console.WriteLine($"[COMBAT] Moving from ({GridX},{GridY}) to ({moveTx},{moveTy})...");

        var moveRsp = await DoMove(moveTx, moveTy);
        Console.WriteLine($"[MOVE] code={moveRsp?.Code}, msg={moveRsp?.Message}, pos=({moveRsp?.X},{moveRsp?.Y}), duration={moveRsp?.DurationMs}ms");

        // Step 3: Watch push messages for 10 seconds
        Console.WriteLine("[COMBAT] Watching push messages for 10s...");
        await WatchPushMessages(10000);
        Console.WriteLine("[COMBAT] Test complete.");
    }

    static async Task RunBot(string username, string password, int durationSec)
    {
        await ConnectAsync();
        await SendGatewayConnect();

        Console.WriteLine($"[BOT] Starting bot '{username}' for {durationSec}s...");

        // Login flow
        var loginRsp = await DoLogin(username, password);
        if (loginRsp?.Code != PCommon.ErrorCode.Success) { Console.WriteLine("[FAIL] Login"); return; }

        var selectRsp = await DoSelectServer(1);
        if (selectRsp?.Code != PCommon.ErrorCode.Success) { Console.WriteLine("[FAIL] SelectServer"); return; }

        if (selectRsp.Roles.Count == 0)
        {
            var name = $"Bot_{Guid.NewGuid().ToString()[..6]}";
            var createRsp = await DoCreateRole(name);
            if (createRsp?.Code != PCommon.ErrorCode.Success) { Console.WriteLine("[FAIL] CreateRole"); return; }
            RoleId = createRsp.RoleInfo.RoleId;
            CurrentMap = createRsp.RoleInfo.CurrentMap;
            GridX = createRsp.RoleInfo.GridX;
            GridY = createRsp.RoleInfo.GridY;
        }
        else
        {
            RoleId = selectRsp.Roles[0].RoleId;
            var enterRsp = await DoEnterGame(RoleId);
            if (enterRsp?.Code != PCommon.ErrorCode.Success) { Console.WriteLine("[FAIL] EnterGame"); return; }
            CurrentMap = enterRsp.RoleInfo.CurrentMap;
            GridX = enterRsp.RoleInfo.GridX;
            GridY = enterRsp.RoleInfo.GridY;
        }

        Console.WriteLine($"[BOT] In game at ({GridX},{GridY}) on {CurrentMap}");

        // Bot loop: random moves
        var rand = new Random();
        var start = DateTime.UtcNow;
        int moveCount = 0;

        while ((DateTime.UtcNow - start).TotalSeconds < durationSec)
        {
            var dx = rand.Next(-1, 2);
            var dy = rand.Next(-1, 2);
            if (dx == 0 && dy == 0) continue;

            var tx = GridX + dx;
            var ty = GridY + dy;

            try
            {
                var moveRsp = await DoMove(tx, ty);
                if (moveRsp?.Code == PCommon.ErrorCode.Success)
                {
                    GridX = moveRsp.X;
                    GridY = moveRsp.Y;
                    moveCount++;
                    if (moveCount % 10 == 0)
                    {
                        Console.WriteLine($"[BOT] Moved {moveCount} times, now at ({GridX},{GridY})");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BOT] Move error: {ex.Message}");
            }

            await Task.Delay(rand.Next(500, 1500));
        }

        Console.WriteLine($"[BOT] Finished. Total moves: {moveCount}");
    }

    // ─────────────────────────────────────────────────────────
    // Low-level message helpers
    // ─────────────────────────────────────────────────────────

    static async Task<PLogin.AccountLoginResponse?> DoLogin(string username, string password)
    {
        var req = new PLogin.AccountLoginRequest
        {
            Username = username,
            Password = password,
            Platform = "pc",
            DeviceId = "test_device",
            ClientVersion = "1.0.0",
        };
        var data = await SendAndWait((int)PProtocol.MessageId.LoginAccountLoginReq, req.ToByteArray());
        if (data == null) return null;
        var rsp = PLogin.AccountLoginResponse.Parser.ParseFrom(data);
        if (rsp.Code == PCommon.ErrorCode.Success)
        {
            AccountToken = rsp.AccountToken;
            AccountId = rsp.AccountId;
        }
        return rsp;
    }

    static async Task<PLogin.SelectServerResponse?> DoSelectServer(uint serverId)
    {
        var req = new PLogin.SelectServerRequest
        {
            AccountToken = AccountToken,
            ServerId = serverId,
        };
        var data = await SendAndWait((int)PProtocol.MessageId.LoginSelectServerReq, req.ToByteArray());
        if (data == null) return null;
        var rsp = PLogin.SelectServerResponse.Parser.ParseFrom(data);
        if (rsp.Code == PCommon.ErrorCode.Success)
        {
            GatewayToken = rsp.GatewayToken;
        }
        return rsp;
    }

    static async Task<PGame.EnterGameResponse?> DoEnterGame(ulong roleId)
    {
        var req = new PGame.EnterGameRequest { RoleId = roleId };
        var data = await SendAndWait((int)PProtocol.MessageId.GameEnterGameReq, req.ToByteArray());
        if (data == null) return null;
        return PGame.EnterGameResponse.Parser.ParseFrom(data);
    }

    static async Task<PGame.CreateRoleResponse?> DoCreateRole(string roleName)
    {
        var req = new PGame.CreateRoleRequest { RoleName = roleName };
        var data = await SendAndWait((int)PProtocol.MessageId.GameCreateRoleReq, req.ToByteArray());
        if (data == null) return null;
        return PGame.CreateRoleResponse.Parser.ParseFrom(data);
    }

    static async Task<PGame.MoveResponse?> DoMove(int x, int y)
    {
        var req = new PGame.MoveRequest
        {
            FromX = GridX,
            FromY = GridY,
            ToX = x,
            ToY = y,
            MapName = CurrentMap,
        };
        var data = await SendAndWait((int)PProtocol.MessageId.GameMoveReq, req.ToByteArray());
        if (data == null) return null;
        return PGame.MoveResponse.Parser.ParseFrom(data);
    }

    static async Task<PGame.GmCommandResponse?> DoGm(string cmd)
    {
        var req = new PGame.GmCommandRequest { Command = cmd };
        var data = await SendAndWait((int)PProtocol.MessageId.GameGmReq, req.ToByteArray());
        if (data == null) return null;
        return PGame.GmCommandResponse.Parser.ParseFrom(data);
    }

    static async Task SendGatewayConnect()
    {
        var req = new PGateway.ConnectRequest
        {
            ClientInfo = new PGateway.ClientInfo { Ip = "127.0.0.1", Platform = "pc", Version = "1.0.0" }
        };
        var data = await SendAndWait((int)PProtocol.MessageId.GatewayConnectReq, req.ToByteArray());
        if (data != null)
        {
            var rsp = PGateway.ConnectResponse.Parser.ParseFrom(data);
            Console.WriteLine($"[CONN] Gateway connected, connId={rsp.ConnId}, serverTime={rsp.ServerTime}");
        }
    }

    // ─────────────────────────────────────────────────────────
    // Network layer
    // ─────────────────────────────────────────────────────────

    static async Task ConnectAsync()
    {
        Tcp = new TcpClient();
        await Tcp.ConnectAsync(Host, Port);
        Stream = Tcp.GetStream();
        Console.WriteLine($"[NET] Connected to {Host}:{Port}");
    }

    static void Disconnect()
    {
        Stream?.Close();
        Tcp?.Close();
    }

    static async Task<byte[]?> SendAndWait(int msgId, byte[] body)
    {
        if (Stream == null) throw new InvalidOperationException("Not connected");

        var session = SessionCounter++;
        var packet = new PCommon.Packet
        {
            MsgId = (uint)msgId,
            Session = session,
            Data = ByteString.CopyFrom(body),
            Timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        };

        // Encode: [4-byte LE length][protobuf packet]
        var packetBytes = packet.ToByteArray();
        var header = BitConverter.GetBytes((uint)packetBytes.Length);
        var combined = new byte[header.Length + packetBytes.Length];
        Buffer.BlockCopy(header, 0, combined, 0, 4);
        Buffer.BlockCopy(packetBytes, 0, combined, 4, packetBytes.Length);

        await Stream.WriteAsync(combined);

        // Wait for response with matching session
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            if (Stream.DataAvailable || Tcp!.Available > 0)
            {
                var rsp = await TryReadResponse(session);
                if (rsp != null) return rsp;
            }
            await Task.Delay(50);
        }
        throw new TimeoutException($"No response for msgId={msgId} session={session}");
    }

    static async Task<byte[]?> TryReadResponse(uint expectedSession)
    {
        // Check push queue first
        lock (PushQueue)
        {
            for (int i = 0; i < PushQueue.Count; i++)
            {
                if (PushQueue[i].Session == expectedSession)
                {
                    var data = PushQueue[i].Data.ToByteArray();
                    PushQueue.RemoveAt(i);
                    return data;
                }
            }
        }

        if (Stream == null || Tcp == null) return null;

        // Read available bytes
        var buf = new byte[Tcp.Available];
        var read = await Stream.ReadAsync(buf);
        if (read == 0) return null;

        // Append to buffer
        var newBuf = new byte[ReadBuffer.Length + read];
        Buffer.BlockCopy(ReadBuffer, 0, newBuf, 0, ReadBuffer.Length);
        Buffer.BlockCopy(buf, 0, newBuf, ReadBuffer.Length, read);
        ReadBuffer = newBuf;

        // Parse packets
        while (true)
        {
            if (ExpectedLength < 0)
            {
                if (ReadBuffer.Length < 4) break;
                ExpectedLength = BitConverter.ToInt32(ReadBuffer, 0);
                ReadBuffer = ReadBuffer[4..];
            }

            if (ReadBuffer.Length < ExpectedLength) break;

            var packetBytes = new byte[ExpectedLength];
            Buffer.BlockCopy(ReadBuffer, 0, packetBytes, 0, ExpectedLength);
            ReadBuffer = ReadBuffer[ExpectedLength..];
            ExpectedLength = -1;

            var packet = PCommon.Packet.Parser.ParseFrom(packetBytes);
            if (packet.Session == expectedSession)
            {
                return packet.Data.ToByteArray();
            }
            else
            {
                lock (PushQueue) { PushQueue.Add(packet); }
            }
        }
        return null;
    }

    static void DrainPushQueue()
    {
        lock (PushQueue)
        {
            foreach (var packet in PushQueue)
            {
                PrintPushMessage(packet);
            }
            PushQueue.Clear();
        }
    }

    static void PrintPushMessage(PCommon.Packet packet)
    {
        try
        {
            var msgId = (PProtocol.MessageId)packet.MsgId;
            switch (msgId)
            {
                case PProtocol.MessageId.GameCombatStartNotify:
                    {
                        var n = PGame.CombatStartNotify.Parser.ParseFrom(packet.Data);
                        Console.WriteLine($"[PUSH] CombatStart: entities=[{string.Join(",", n.EntityIds)}]");
                        break;
                    }
                case PProtocol.MessageId.GameCombatEndNotify:
                    {
                        var n = PGame.CombatEndNotify.Parser.ParseFrom(packet.Data);
                        Console.WriteLine($"[PUSH] CombatEnd: reason={n.Reason}, entities=[{string.Join(",", n.EntityIds)}]");
                        break;
                    }
                case PProtocol.MessageId.GameCombatStateNotify:
                    {
                        var n = PGame.CombatStateNotify.Parser.ParseFrom(packet.Data);
                        var units = string.Join(", ", n.Units.Select(u => $"{u.EntityName}(id={u.EntityId},hp={u.Hp}/{u.MaxHp},mp={u.Mp}/{u.MaxMp})"));
                        Console.WriteLine($"[PUSH] CombatState: [{units}]");
                        break;
                    }
                case PProtocol.MessageId.GameMonsterMoveNotify:
                    {
                        var n = PGame.MonsterMoveNotify.Parser.ParseFrom(packet.Data);
                        Console.WriteLine($"[PUSH] MonsterMove: id={n.InstanceId} from=({n.FromX},{n.FromY}) to=({n.ToX},{n.ToY})");
                        break;
                    }
                case PProtocol.MessageId.GameMoveCancelNotify:
                    {
                        var n = PGame.MoveCancelNotify.Parser.ParseFrom(packet.Data);
                        Console.WriteLine($"[PUSH] MoveCancel: entity={n.EntityId} rollback=({n.RollbackX},{n.RollbackY})");
                        break;
                    }
                case PProtocol.MessageId.GameBuffUpdateNotify:
                    {
                        var n = PGame.BuffUpdateNotify.Parser.ParseFrom(packet.Data);
                        var buffs = string.Join(", ", n.Buffs.Select(b => $"{b.BuffName}(id={b.BuffId},stacks={b.Stacks})"));
                        Console.WriteLine($"[PUSH] BuffUpdate: entity={n.EntityId} buffs=[{buffs}]");
                        break;
                    }
                default:
                    Console.WriteLine($"[PUSH] msgId={packet.MsgId} session={packet.Session} len={packet.Data.Length}");
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PUSH] msgId={packet.MsgId} session={packet.Session} parse_error={ex.Message}");
        }
    }

    static async Task WatchPushMessages(int durationMs)
    {
        if (Stream == null || Tcp == null) return;
        var deadline = DateTime.UtcNow.AddMilliseconds(durationMs);
        while (DateTime.UtcNow < deadline)
        {
            lock (PushQueue)
            {
                while (PushQueue.Count > 0)
                {
                    var packet = PushQueue[0];
                    PushQueue.RemoveAt(0);
                    PrintPushMessage(packet);
                }
            }

            if (Stream.DataAvailable || Tcp.Available > 0)
            {
                var buf = new byte[Tcp.Available];
                var read = await Stream.ReadAsync(buf);
                if (read > 0)
                {
                    var newBuf = new byte[ReadBuffer.Length + read];
                    Buffer.BlockCopy(ReadBuffer, 0, newBuf, 0, ReadBuffer.Length);
                    Buffer.BlockCopy(buf, 0, newBuf, ReadBuffer.Length, read);
                    ReadBuffer = newBuf;

                    while (true)
                    {
                        if (ExpectedLength < 0)
                        {
                            if (ReadBuffer.Length < 4) break;
                            ExpectedLength = BitConverter.ToInt32(ReadBuffer, 0);
                            ReadBuffer = ReadBuffer[4..];
                        }
                        if (ReadBuffer.Length < ExpectedLength) break;

                        var packetBytes = new byte[ExpectedLength];
                        Buffer.BlockCopy(ReadBuffer, 0, packetBytes, 0, ExpectedLength);
                        ReadBuffer = ReadBuffer[ExpectedLength..];
                        ExpectedLength = -1;

                        var packet = PCommon.Packet.Parser.ParseFrom(packetBytes);
                        PrintPushMessage(packet);
                    }
                }
            }

            await Task.Delay(100);
        }
    }

    // ─────────────────────────────────────────────────────────
    // Utilities
    // ─────────────────────────────────────────────────────────

    static string GetArg(string[] args, string flag, string defaultValue)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == flag) return args[i + 1];
        }
        return defaultValue;
    }

    static void PrintHelp()
    {
        Console.WriteLine("Game Automation Test Client");
        Console.WriteLine("");
        Console.WriteLine("Usage: dotnet run -- [command] [options]");
        Console.WriteLine("");
        Console.WriteLine("Commands:");
        Console.WriteLine("  ping                           Test server connectivity");
        Console.WriteLine("  login --username U --password P  Login only");
        Console.WriteLine("  enter --username U --password P [--role-name N]  Full login+enter/create");
        Console.WriteLine("  move --username U --password P --x X --y Y       Login and move to (X,Y)");
        Console.WriteLine("  gm --username U --password P --cmd CMD           Execute GM command");
        Console.WriteLine("  combat --username U --password P --tx X --ty Y   Teleport to (X,Y) and move 1 step to trigger combat");
        Console.WriteLine("  bot --username U --password P [--duration N]     Run a random-move bot for N seconds");
        Console.WriteLine("");
        Console.WriteLine("Global flags:");
        Console.WriteLine("  --host HOST    Server host (default: 127.0.0.1)");
        Console.WriteLine("  --port PORT    Server port (default: 8889)");
        Console.WriteLine("");
        Console.WriteLine("Examples:");
        Console.WriteLine("  dotnet run -- ping");
        Console.WriteLine("  dotnet run -- login --username test --password test");
        Console.WriteLine("  dotnet run -- enter --username test --password test --role-name MyHero");
        Console.WriteLine("  dotnet run -- move --username test --password test --x 30 --y 30");
        Console.WriteLine("  dotnet run -- gm --username test --password test --cmd \"additem 1001 10\"");
        Console.WriteLine("  dotnet run -- bot --username bot1 --password bot1 --duration 120");
    }
}
