using System.Text.Json;
using GameServer.Services.Monster;
using GameServer.Tables;
using Xunit;

namespace GameServer.Tests;

public class MonsterCurrentPhaseConfigTests
{
    [Fact]
    public void XinshoucunMonsterAiConfigs_ShouldHaveValidBehaviorType()
    {
        var aiRows = LoadTable<AiRow>("common_tbai.json");
        var validBehaviorTypes = new HashSet<CombatBehaviorType> { CombatBehaviorType.Melee, CombatBehaviorType.Ranged, CombatBehaviorType.Caster };

        foreach (var aiId in new[] { 2, 4, 5 })
        {
            var row = aiRows.SingleOrDefault(x => x.Id == aiId);
            Assert.NotNull(row);
            Assert.Contains(row!.Param1, validBehaviorTypes);
        }
    }

    [Fact]
    public void XinshoucunMonsterSkills_ShouldAllReferenceExistingSkills()
    {
        var monsters = LoadTable<MonsterRow>("common_tbmonster.json");
        var skills = LoadTable<SkillConfigRow>("common_tbskill.json").ToDictionary(x => x.Id);

        foreach (var monsterId in new[] { 1, 2, 3 })
        {
            var monster = monsters.SingleOrDefault(x => x.Id == monsterId);
            Assert.NotNull(monster);

            foreach (var skillId in monster!.Skills)
            {
                Assert.True(skills.ContainsKey(skillId), $"monster={monsterId} references skill={skillId} which does not exist in skill config");
            }
        }
    }

    private static List<T> LoadTable<T>(string fileName)
    {
        var repoRoot = FindRepoRoot();
        var path = Path.Combine(repoRoot, "servercsharp", "data", "tables", fileName);
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<List<T>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        }) ?? new List<T>();
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "servercsharp", "data", "tables");
            if (Directory.Exists(candidate))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("repo root not found");
    }
}
