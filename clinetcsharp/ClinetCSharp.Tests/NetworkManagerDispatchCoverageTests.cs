using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace ClinetCSharp.Tests;

public class NetworkManagerDispatchCoverageTests
{
    private static readonly string[] ExpectedHandledMessageIds =
    {
        "GatewayHeartbeatRsp",
        "GatewayKickNotify",
        "GatewayDisconnectNotify",
        "LoginAccountLoginRsp",
        "LoginSelectServerRsp",
        "GameEnterGameRsp",
        "GameCreateRoleRsp",
        "GameMapInfoSyncNotify",
        "GameChangeMapRsp",
        "GameChestUpdateNotify",
        "GameDropSpawnNotify",
        "GameDropPickupNotify",
        "GameDropRemoveNotify",
        "GameNpcInteractNotify",
        "GameNpcCombatRsp",
        "GameMoveRsp",
        "GameMoveCancelNotify",
        "GameMonsterMoveNotify",
        "GameMonsterMoveCancelNotify",
        "GameCombatLogNotify",
        "GameCombatStateNotify",
        "GameBuffUpdateNotify",
        "GameCombatStartNotify",
        "GameCombatEndNotify",
        "GamePlayerDeathNotify",
        "GameLevelUpNotify",
        "GameRoleAttrNotify",
        "GameOpenChestRsp",
        "GameUseItemRsp",
        "GameDropItemRsp",
        "GameGmRsp",
        "GameEquipSkillRsp",
        "GameUnequipSkillRsp",
        "GameSetPreferredSkillRsp",
        "GameChangeJobRsp",
    };

    [Fact]
    public void DispatchTable_ContainsExactlyExpectedHandledMessageIds()
    {
        var dispatchFile = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Scripts", "NetworkManager.Dispatch.cs"));
        var content = File.ReadAllText(dispatchFile);

        var actual = Regex.Matches(content, @"case\s+MessageId\.(\w+):")
            .Select(match => match.Groups[1].Value)
            .ToHashSet();

        var expected = ExpectedHandledMessageIds.ToHashSet();
        var missing = expected.Except(actual).OrderBy(x => x).ToArray();
        var unexpected = actual.Except(expected).OrderBy(x => x).ToArray();

        Assert.True(missing.Length == 0, $"Missing dispatch cases: {string.Join(", ", missing)}");
        Assert.True(unexpected.Length == 0, $"Unexpected dispatch cases: {string.Join(", ", unexpected)}");
    }

    [Fact]
    public void DispatchTable_DoesNotContainDuplicateMessageIds()
    {
        var dispatchFile = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Scripts", "NetworkManager.Dispatch.cs"));
        var content = File.ReadAllText(dispatchFile);

        var duplicates = Regex.Matches(content, @"case\s+MessageId\.(\w+):")
            .Select(match => match.Groups[1].Value)
            .GroupBy(name => name)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(name => name)
            .ToArray();

        Assert.True(duplicates.Length == 0, $"Duplicate dispatch cases: {string.Join(", ", duplicates)}");
    }
}
