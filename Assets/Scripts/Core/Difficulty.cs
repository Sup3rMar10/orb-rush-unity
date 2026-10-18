using System;

namespace OrbRush.Core
{
    /// <summary>Difficulty curve. Levels start at 1. Every value is clamped so late levels stay playable.</summary>
    public static class Difficulty
    {
        public const int OrbsPerLevel = 8;

        public static float HazardSpeed(int level)
        {
            return Math.Min(6.0f, 1.6f + 0.45f * (level - 1));
        }

        public static float HazardSpawnInterval(int level)
        {
            return Math.Max(0.55f, 2.4f - 0.2f * (level - 1));
        }

        public static int MaxHazards(int level)
        {
            return Math.Min(14, 3 + level);
        }

        /// <summary>Seconds an orb stays before it fades (a missed orb breaks the combo).</summary>
        public static float OrbLifetime(int level)
        {
            return Math.Max(3.5f, 8.0f - 0.5f * (level - 1));
        }
    }
}
