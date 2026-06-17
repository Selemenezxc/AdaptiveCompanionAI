using Terraria;
using Terraria.ID;

namespace AdaptiveCompanionAI.Common.Data
{
    public static class CompanionMath
    {
        public const double SecondsPerTick = 1d / 60d;

        public static double Clamp(double value, double min, double max)
        {
            if (value < min)
            {
                return min;
            }

            if (value > max)
            {
                return max;
            }

            return value;
        }

        public static double Clamp01(double value)
        {
            return Clamp(value, 0d, 1d);
        }

        public static float SnapToStep(double value, double min, double max, double step)
        {
            double clamped = Clamp(value, min, max);
            if (step <= 0d)
            {
                return (float)clamped;
            }

            double snapped = System.Math.Round(clamped / step, System.MidpointRounding.AwayFromZero) * step;
            snapped = Clamp(snapped, min, max);
            return (float)snapped;
        }

        public static int CountBits(int value)
        {
            int count = 0;
            while (value != 0)
            {
                value &= value - 1;
                count++;
            }

            return count;
        }

        public static double SafeRate(double numerator, double denominator)
        {
            if (denominator <= 0d)
            {
                return 0d;
            }

            return numerator / denominator;
        }

        public static long GetCoinValue(Item item)
        {
            if (item == null || item.IsAir)
            {
                return 0L;
            }

            switch (item.type)
            {
                case ItemID.CopperCoin:
                    return item.stack;
                case ItemID.SilverCoin:
                    return item.stack * 100L;
                case ItemID.GoldCoin:
                    return item.stack * 10000L;
                case ItemID.PlatinumCoin:
                    return item.stack * 1000000L;
                default:
                    return 0L;
            }
        }
    }
}
