using System;

namespace ClinetCSharp
{
    public static class DebugPanelSliderValueFormatter
    {
        public static string Format(double value, double step)
        {
            int decimals = GetDecimalPlaces(step);
            return decimals <= 0
                ? ((int)Math.Round(value)).ToString()
                : value.ToString($"F{decimals}");
        }

        private static int GetDecimalPlaces(double step)
        {
            if (step <= 0 || step >= 1)
                return 0;

            for (int decimals = 1; decimals <= 4; decimals++)
            {
                double scaled = step * Math.Pow(10, decimals);
                if (Math.Abs(scaled - Math.Round(scaled)) < 0.000001)
                    return decimals;
            }

            return 4;
        }
    }
}
