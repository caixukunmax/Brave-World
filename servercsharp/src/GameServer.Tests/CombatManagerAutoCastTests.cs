using System.Reflection;
using GameServer.Services.Core;
using GameServer.Services.Map.Combat;
using GameServer.Services.Map.Combat.Actions;
using GameServer.Tables;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PGame = global::Game;
using Xunit;

namespace GameServer.Tests;

public class CombatManagerAutoCastTests
{
    private const long PlayerId = 1L;
    private const long MonsterId = 1_000_001L;
    private const int SkillId = 9001;
    private const string MapName = "test_map";

    [Fact]
    public void RequestCast_ClearsPreferredSkill_OnMiss()
    {
        var (manager, pipeline, maps) = CreateManagerWithCombat();
        var ctx = manager.GetContext(PlayerId)!;
        Assert.Equal(SkillId, ctx.PreferredSkillId);

        var requestCast = typeof(CombatManager).GetMethod(
            "RequestCast",
            BindingFlags.NonPublic | BindingFlags.Instance)!;

        pipeline.NextResult = "MISS";
        requestCast.Invoke(manager, new object[] { PlayerId, SkillId, maps });

        Assert.Single(pipeline.CastCalls);
        Assert.Equal((SkillId, PlayerId), pipeline.CastCalls[0]);
        Assert.Equal(0, ctx.PreferredSkillId);
        Assert.Equal(0, maps[MapName].Players[PlayerId].PreferredSkillId);
    }

    [Fact]
    public void SelectSkill_KeepsPreferred_WhenPreferredOnCooldown()
    {
        var (manager, pipeline, maps) = CreateManagerWithCombat();
        var ctx = manager.GetContext(PlayerId)!;
        var player = maps[MapName].Players[PlayerId];

        player.PreferredSkillId = SkillId;
        ctx.PreferredSkillId = SkillId;
        ctx.SubState = "NONE";
        ctx.SkillCooldowns[SkillId] = Environment.TickCount64 + 600_000;
        pipeline.NextResult = "SUCCESS";

        var tickMonsterSkills = typeof(CombatManager).GetMethod(
            "TickMonsterSkills",
            BindingFlags.NonPublic | BindingFlags.Instance)!;

        tickMonsterSkills.Invoke(manager, new object[] { 0.033d, maps });

        Assert.Empty(pipeline.CastCalls);
        Assert.Equal(SkillId, ctx.PreferredSkillId);
        Assert.Equal(SkillId, player.PreferredSkillId);
    }

    [Fact]
    public void TickMonsterSkills_SkipsAutoCast_DuringPostCast()
    {
        var (manager, pipeline, maps) = CreateManagerWithCombat();
        var ctx = manager.GetContext(PlayerId)!;
        var player = maps[MapName].Players[PlayerId];

        // 手动进入后摇期，并设置一个足够远的结束时间
        ctx.SubState = "POST_CAST";
        long postCastEnd = Environment.TickCount64 + 60_000;
        ctx.PostCastEndTime = postCastEnd;
        player.PreferredSkillId = SkillId;
        ctx.PreferredSkillId = SkillId;

        var tickMonsterSkills = typeof(CombatManager).GetMethod(
            "TickMonsterSkills",
            BindingFlags.NonPublic | BindingFlags.Instance)!;

        pipeline.NextResult = "SUCCESS";
        tickMonsterSkills.Invoke(manager, new object[] { 0.033d, maps });

        // 后摇期间不应发起自动施法
        Assert.Empty(pipeline.CastCalls);
        Assert.Equal("POST_CAST", ctx.SubState);
        Assert.Equal(postCastEnd, ctx.PostCastEndTime);
        Assert.Equal(SkillId, ctx.PreferredSkillId);
        Assert.Equal(SkillId, player.PreferredSkillId);
    }

    [Fact]
    public void HandleCastRequest_WithoutInterrupt_ReturnsAlreadyCasting()
    {
        var (manager, pipeline, maps) = CreateManagerWithCombat();
        var ctx = manager.GetContext(PlayerId)!;

        ctx.SubState = "CASTING";
        ctx.CastSkillId = 9999;
        ctx.CastEndTime = Environment.TickCount64 + 60_000;

        PGame.CastResponse response = manager.HandleCastRequest(PlayerId, SkillId, interrupt: false, targetId: null, maps);

        Assert.False(response.Success);
        Assert.Equal("already_casting", response.Error);
        Assert.Empty(pipeline.CastCalls);
    }

    [Fact]
    public void HandleCastRequest_WithoutInterrupt_ReturnsPostCast()
    {
        var (manager, pipeline, maps) = CreateManagerWithCombat();
        var ctx = manager.GetContext(PlayerId)!;

        ctx.SubState = "POST_CAST";
        ctx.PostCastEndTime = Environment.TickCount64 + 60_000;

        PGame.CastResponse response = manager.HandleCastRequest(PlayerId, SkillId, interrupt: false, targetId: null, maps);

        Assert.False(response.Success);
        Assert.Equal("post_cast", response.Error);
        Assert.Empty(pipeline.CastCalls);
    }

    [Fact]
    public void HandleCastRequest_WithInterrupt_ResetsCastStateAndCasts()
    {
        var (manager, pipeline, maps) = CreateManagerWithCombat();
        var ctx = manager.GetContext(PlayerId)!;
        var player = maps[MapName].Players[PlayerId];

        ctx.SubState = "CASTING";
        ctx.CastSkillId = 9999;
        ctx.CastEndTime = Environment.TickCount64 + 60_000;
        pipeline.NextResult = "PENDING";

        PGame.CastResponse response = manager.HandleCastRequest(PlayerId, SkillId, interrupt: true, targetId: null, maps);

        Assert.True(response.Success);
        Assert.Single(pipeline.CastCalls);
        Assert.Equal((SkillId, PlayerId), pipeline.CastCalls[0]);
        // Fake pipeline does not simulate state transitions; verify InterruptCast cleared the previous state.
        Assert.Equal("NONE", ctx.SubState);
        Assert.Null(ctx.CastSkillId);
        Assert.Equal(0, ctx.PreferredSkillId);
        Assert.Equal(0, player.PreferredSkillId);
    }

    [Fact]
    public void HandleCastRequest_WithInterrupt_ClearsPreferredSkill_AfterMiss()
    {
        var (manager, pipeline, maps) = CreateManagerWithCombat();
        var ctx = manager.GetContext(PlayerId)!;
        var player = maps[MapName].Players[PlayerId];

        ctx.SubState = "CASTING";
        ctx.CastSkillId = 9999;
        ctx.CastEndTime = Environment.TickCount64 + 60_000;
        pipeline.NextResult = "MISS";

        PGame.CastResponse response = manager.HandleCastRequest(PlayerId, SkillId, interrupt: true, targetId: null, maps);

        Assert.True(response.Success);
        Assert.Single(pipeline.CastCalls);
        Assert.Equal((SkillId, PlayerId), pipeline.CastCalls[0]);
        Assert.Equal("NONE", ctx.SubState);
        Assert.Null(ctx.CastSkillId);
        Assert.Equal(0, ctx.PreferredSkillId);
        Assert.Equal(0, player.PreferredSkillId);
    }

    [Fact]
    public void SkillPipeline_InterruptCast_ResetsCastState()
    {
        var (manager, pipeline, maps) = CreateManagerWithCombat();
        var ctx = manager.GetContext(PlayerId)!;

        ctx.SubState = "CASTING";
        ctx.CastSkillId = 9999;
        long castEnd = Environment.TickCount64 + 60_000;
        ctx.CastEndTime = castEnd;
        ctx.PostCastEndTime = Environment.TickCount64 + 120_000;

        pipeline.InterruptCast(PlayerId);

        Assert.Equal("NONE", ctx.SubState);
        Assert.Null(ctx.CastSkillId);
        Assert.Null(ctx.CastEndTime);
        Assert.Null(ctx.PostCastEndTime);
    }

    [Fact]
    public void HandleCastRequest_WithoutInterrupt_AllowsCast_WhenPostCastExpired()
    {
        var (manager, pipeline, maps) = CreateManagerWithCombat();
        var ctx = manager.GetContext(PlayerId)!;

        ctx.SubState = "POST_CAST";
        ctx.PostCastEndTime = Environment.TickCount64 - 1;
        pipeline.NextResult = "SUCCESS";

        PGame.CastResponse response = manager.HandleCastRequest(PlayerId, SkillId, interrupt: false, targetId: null, maps);

        Assert.True(response.Success);
        Assert.Single(pipeline.CastCalls);
        Assert.Equal((SkillId, PlayerId), pipeline.CastCalls[0]);
        Assert.Equal("NONE", ctx.SubState);
        Assert.Null(ctx.PostCastEndTime);
    }

    [Fact]
    public void TickMonsterSkills_PlayerAutoCast_CastsEquippedSkill()
    {
        var (manager, pipeline, maps) = CreateManagerWithCombat();
        var ctx = manager.GetContext(PlayerId)!;
        var player = maps[MapName].Players[PlayerId];

        player.PreferredSkillId = 0;
        ctx.PreferredSkillId = 0;
        ctx.SubState = "NONE";
        pipeline.NextResult = "SUCCESS";

        var tickMonsterSkills = typeof(CombatManager).GetMethod(
            "TickMonsterSkills",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        tickMonsterSkills.Invoke(manager, new object[] { 0.033d, maps });

        Assert.Contains((SkillId, PlayerId), pipeline.CastCalls);
    }

    [Fact]
    public void TickMonsterSkills_PlayerAutoCast_ClearsPreferredSkill()
    {
        var (manager, pipeline, maps) = CreateManagerWithCombat();
        var ctx = manager.GetContext(PlayerId)!;
        var player = maps[MapName].Players[PlayerId];

        player.PreferredSkillId = SkillId;
        ctx.PreferredSkillId = SkillId;
        ctx.SubState = "NONE";
        pipeline.NextResult = "SUCCESS";

        var tickMonsterSkills = typeof(CombatManager).GetMethod(
            "TickMonsterSkills",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        tickMonsterSkills.Invoke(manager, new object[] { 0.033d, maps });

        Assert.Contains((SkillId, PlayerId), pipeline.CastCalls);
        Assert.Equal(0, ctx.PreferredSkillId);
        Assert.Equal(0, player.PreferredSkillId);
    }

    [Fact]
    public void OnDeath_ClearsPreferredSkill()
    {
        var (manager, pipeline, maps) = CreateManagerWithCombat();
        var ctx = manager.GetContext(PlayerId)!;
        var player = maps[MapName].Players[PlayerId];

        player.PreferredSkillId = SkillId;
        ctx.PreferredSkillId = SkillId;

        manager.OnDeath(PlayerId, maps);

        Assert.Equal(0, ctx.PreferredSkillId);
        Assert.Equal(0, player.PreferredSkillId);
    }

    [Fact]
    public void Disengage_ClearsPreferredSkill()
    {
        var (manager, pipeline, maps) = CreateManagerWithCombat();
        var ctx = manager.GetContext(PlayerId)!;
        var player = maps[MapName].Players[PlayerId];

        player.PreferredSkillId = SkillId;
        ctx.PreferredSkillId = SkillId;

        manager.RequestDisengage(PlayerId, MonsterId);
        manager.Tick(0.033d, maps, new FakeMonsterRegistry());

        Assert.Equal(0, ctx.PreferredSkillId);
        Assert.Equal(0, player.PreferredSkillId);
    }

    [Fact]
    public void HandleCastRequest_InvalidSkill_DoesNotInterruptCasting()
    {
        var (manager, pipeline, maps) = CreateManagerWithCombat();
        var ctx = manager.GetContext(PlayerId)!;

        ctx.SubState = "CASTING";
        ctx.CastSkillId = 9999;
        ctx.CastEndTime = Environment.TickCount64 + 60_000;

        PGame.CastResponse response = manager.HandleCastRequest(PlayerId, skillId: 12345, interrupt: true, targetId: null, maps);

        Assert.False(response.Success);
        Assert.Equal("invalid_skill", response.Error);
        Assert.Empty(pipeline.CastCalls);
        Assert.Equal("CASTING", ctx.SubState);
        Assert.Equal(9999, ctx.CastSkillId);
    }

    private static (CombatManager manager, FakeSkillPipeline pipeline, Dictionary<string, MapState> maps)
        CreateManagerWithCombat()
    {
        var tables = new LubanTableLoader(NullLogger<LubanTableLoader>.Instance);
        tables.Skills[SkillId] = new SkillConfigRow
        {
            Id = SkillId,
            Name = "Test Skill",
            CastRange = 1,
            CastTime = 0,
            TargetType = ESkillTargetType.SingleEnemy,
        };

        var pipeline = new FakeSkillPipeline(tables);
        var manager = new CombatManager(
            NullLogger<CombatManager>.Instance,
            NullLoggerFactory.Instance,
            pipeline,
            new ActionRegistry(),
            new FakeNetworkSender(),
            tables);

        var player = new MapPlayerState
        {
            AccountId = PlayerId,
            RoleId = PlayerId,
            RoleName = "Player",
            GridX = 0,
            GridY = 0,
            Job = "warrior",
            EquippedSkills = new List<int> { SkillId },
            PreferredSkillId = SkillId,
            Hp = 100,
            MaxHp = 100,
            Mp = 100,
            MaxMp = 100,
        };

        var monster = new MapMonsterState
        {
            InstanceId = MonsterId,
            MonsterId = 1,
            Name = "Monster",
            X = 1,
            Y = 0,
            Hp = 100,
            MaxHp = 100,
        };

        var map = new MapState { MapId = 1 };
        map.Players[PlayerId] = player;
        map.Monsters[MonsterId] = monster;
        var maps = new Dictionary<string, MapState> { [MapName] = map };

        manager.OnCollision(PlayerId, MonsterId, maps);

        return (manager, pipeline, maps);
    }

    private sealed class FakeSkillPipeline : SkillPipeline
    {
        public List<(int SkillId, long CasterId)> CastCalls { get; } = new();
        public string NextResult { get; set; } = "MISS";

        public FakeSkillPipeline(LubanTableLoader? tables = null)
            : base(NullLogger<SkillPipeline>.Instance, new ActionRegistry(), tables)
        {
        }

        public override string Cast(int skillId, long casterId, Dictionary<string, MapState>? maps)
        {
            CastCalls.Add((skillId, casterId));
            return NextResult;
        }
    }

    private sealed class FakeNetworkSender : INetworkSender
    {
        public void SendToAccount(long accountId, int serverId, int msgId, byte[] data) { }
        public void SendToClient(long connId, int msgId, uint session, byte[] data) { }
    }

    private sealed class FakeMonsterRegistry : IMonsterRegistry
    {
        public void OnDamage(long instanceId, long attackerId, int damage) { }
        public void OnRegen(long instanceId, int regen) { }
        public bool IsOccupied(string mapName, int x, int y) => false;
        public void OnDisengage(long instanceId) { }
    }
}
