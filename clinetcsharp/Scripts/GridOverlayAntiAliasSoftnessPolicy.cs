namespace ClinetCSharp;

public static class GridOverlayAntiAliasSoftnessPolicy
{
    public const float Min = 0.5f;
    public const float Max = 3.0f;
    public const float Default = 1.0f;

    public static float Clamp(float value)
    {
        if (value < Min)
            return Min;
        if (value > Max)
            return Max;
        return value;
    }
}