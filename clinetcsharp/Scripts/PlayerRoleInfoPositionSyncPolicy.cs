using System;

namespace ClinetCSharp
{
    public static class PlayerRoleInfoPositionSyncPolicy
    {
        public static bool ShouldApplyServerGrid(
            bool isMoving,
            bool isMovePending,
            bool isBouncingBack)
        {
            return !isMoving && !isMovePending && !isBouncingBack;
        }

        public static bool ShouldDeferServerGrid(
            bool isMoving,
            bool isMovePending,
            bool isBouncingBack)
        {
            return !ShouldApplyServerGrid(isMoving, isMovePending, isBouncingBack);
        }

        public static float ResolveServerGridCorrectionDuration(float distance, float gridSize)
        {
            if (distance <= 0.5f)
                return 0f;

            float normalizedDistance = distance / Math.Max(gridSize, 1f);
            return Math.Clamp(0.04f + normalizedDistance * 0.04f, 0.04f, 0.12f);
        }
    }
}