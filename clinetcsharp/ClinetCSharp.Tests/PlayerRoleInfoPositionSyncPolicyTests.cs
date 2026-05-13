using Xunit;

namespace ClinetCSharp.Tests;

public class PlayerRoleInfoPositionSyncPolicyTests
{
    [Fact]
    public void ShouldApplyServerGrid_WhenPlayerIsStable_ReturnsTrue()
    {
        Assert.True(PlayerRoleInfoPositionSyncPolicy.ShouldApplyServerGrid(
            isMoving: false,
            isMovePending: false,
            isBouncingBack: false));
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void ShouldApplyServerGrid_WhenLocalMovementStateActive_ReturnsFalse(
        bool isMoving,
        bool isMovePending,
        bool isBouncingBack)
    {
        Assert.False(PlayerRoleInfoPositionSyncPolicy.ShouldApplyServerGrid(
            isMoving: isMoving,
            isMovePending: isMovePending,
            isBouncingBack: isBouncingBack));
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void ShouldDeferServerGrid_WhenLocalMovementStateActive_ReturnsTrue(
        bool isMoving,
        bool isMovePending,
        bool isBouncingBack)
    {
        Assert.True(PlayerRoleInfoPositionSyncPolicy.ShouldDeferServerGrid(
            isMoving: isMoving,
            isMovePending: isMovePending,
            isBouncingBack: isBouncingBack));
    }

    [Fact]
    public void ResolveServerGridCorrectionDuration_WhenDistanceIsTiny_ReturnsZero()
    {
        Assert.Equal(0f, PlayerRoleInfoPositionSyncPolicy.ResolveServerGridCorrectionDuration(
            distance: 0.4f,
            gridSize: 111f));
    }

    [Fact]
    public void ResolveServerGridCorrectionDuration_WhenDistanceIsOneGrid_ReturnsClampedDuration()
    {
        Assert.Equal(0.08f, PlayerRoleInfoPositionSyncPolicy.ResolveServerGridCorrectionDuration(
            distance: 111f,
            gridSize: 111f), 3);
    }

    [Fact]
    public void ResolveServerGridCorrectionDuration_WhenDistanceIsLarge_ClampsToUpperBound()
    {
        Assert.Equal(0.12f, PlayerRoleInfoPositionSyncPolicy.ResolveServerGridCorrectionDuration(
            distance: 800f,
            gridSize: 111f), 3);
    }
}