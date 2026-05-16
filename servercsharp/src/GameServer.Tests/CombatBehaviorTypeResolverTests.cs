using GameServer.Services.Monster;
using GameServer.Services.Monster.AI;
using GameServer.Tables;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GameServer.Tests;

public class CombatBehaviorTypeResolverTests
{
    [Fact]
    public void Resolve_WhenParamOverrides_UsesExplicitCombatType()
    {
        var tables = new LubanTableLoader(new NullLogger<LubanTableLoader>());
        tables.Skills[10] = new SkillConfigRow { Id = 10, CastRange = 4, CastTime = 0 };
        var monster = new MonsterRow { Id = 1, Skills = new List<int> { 10 } };
        var aiRow = new AiRow { Param1 = CombatBehaviorType.Melee };

        var (type, range) = CombatBehaviorTypeResolver.Resolve(aiRow, monster, tables);

        Assert.Equal(CombatBehaviorType.Melee, type);
        Assert.Equal(1, range);
    }

    [Fact]
    public void Resolve_WhenAutoAndHasCastSkill_UsesCaster()
    {
        var tables = new LubanTableLoader(new NullLogger<LubanTableLoader>());
        tables.Skills[11] = new SkillConfigRow { Id = 11, CastRange = 5, CastTime = 1.5 };
        var monster = new MonsterRow { Id = 2, Skills = new List<int> { 11 } };
        var aiRow = new AiRow { Param1 = CombatBehaviorType.Auto };

        var (type, range) = CombatBehaviorTypeResolver.Resolve(aiRow, monster, tables);

        Assert.Equal(CombatBehaviorType.Caster, type);
        Assert.Equal(5, range);
    }
}
