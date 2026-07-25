using Xunit;

namespace ClinetCSharp.Tests;

public class EntityPreviewPolicyTests
{
    [Theory]
    [InlineData("player", EntityPreviewPolicy.PreviewKind.Player)]
    [InlineData("monster", EntityPreviewPolicy.PreviewKind.Monster)]
    [InlineData("npc", EntityPreviewPolicy.PreviewKind.Npc)]
    [InlineData("decoration", EntityPreviewPolicy.PreviewKind.Decoration)]
    public void ResolveKind_WhenKnownEntityType_ReturnsMatchingKind(
        string entityType, EntityPreviewPolicy.PreviewKind expected)
    {
        Assert.Equal(expected, EntityPreviewPolicy.ResolveKind(entityType));
    }

    [Theory]
    [InlineData("")]
    [InlineData("boss")]
    [InlineData("PLAYER")]
    public void ResolveKind_WhenUnknownOrEmptyEntityType_FallsBackToDecoration(string entityType)
    {
        // 未知类型必须兜底到最通用的 MapDecoration 渲染路径，
        // 且运行时与编辑器共用此规则，防止两边各自实现再次漂移
        Assert.Equal(EntityPreviewPolicy.PreviewKind.Decoration, EntityPreviewPolicy.ResolveKind(entityType));
    }
}
