namespace ClinetCSharp
{
    public static class RoleControlCenterXResolver
    {
        public static float ResolveX(float manualX, bool centerX)
        {
            return centerX ? 0f : manualX;
        }

        public static bool IsManualXEditable(bool centerX)
        {
            return !centerX;
        }

        public static bool ResolveInitialCenterX(bool? persistedCenterX, float offsetX)
        {
            if (persistedCenterX.HasValue)
            {
                return persistedCenterX.Value;
            }

            return offsetX == 0f;
        }
    }
}