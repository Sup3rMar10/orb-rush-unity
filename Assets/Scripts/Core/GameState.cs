using System;

namespace OrbRush.Core
{
    /// <summary>Score, lives, combo and level. No engine types; driven by the Unity layer.</summary>
    public sealed class GameState
    {
        public const int StartLives = 3;
        public const int MaxLives = 5;
        public const float ComboWindow = 2.0f;
        public const float InvulnerableSeconds = 1.5f;
        public const int MaxMultiplier = 5;
        public const int BasePoints = 10;
        public const int LevelBonusPerLevel = 50;

        public int Score { get; private set; }
        public int Lives { get; private set; }
        public int Level { get; private set; }
        public int Combo { get; private set; }
        public int OrbsThisLevel { get; private set; }
        public float ComboTimer { get; private set; }
        public float InvulnerableTimer { get; private set; }
        public bool IsOver { get; private set; }

        public GameState()
        {
            Reset();
        }

        public void Reset()
        {
            Score = 0;
            Lives = StartLives;
            Level = 1;
            Combo = 0;
            OrbsThisLevel = 0;
            ComboTimer = 0f;
            InvulnerableTimer = 0f;
            IsOver = false;
        }

        public bool IsInvulnerable { get { return InvulnerableTimer > 0f; } }

        /// <summary>Score multiplier from the current combo, 1..MaxMultiplier.</summary>
        public int Multiplier { get { return Math.Max(1, Math.Min(Combo, MaxMultiplier)); } }

        /// <summary>Collect an orb. Returns the points awarded; leveledUp is set when this orb completed a level.</summary>
        public int CollectOrb(out bool leveledUp)
        {
            leveledUp = false;
            if (IsOver) return 0;

            // A new chain starts if the previous one timed out.
            if (ComboTimer <= 0f) Combo = 0;
            Combo++;
            ComboTimer = ComboWindow;

            int points = BasePoints * Multiplier;
            Score += points;
            OrbsThisLevel++;

            if (OrbsThisLevel >= Difficulty.OrbsPerLevel)
            {
                OrbsThisLevel = 0;
                Level++;
                leveledUp = true;
                Score += LevelBonusPerLevel * Level;
                // Every third level restores a life, up to the cap.
                if (Level % 3 == 0 && Lives < MaxLives) Lives++;
            }
            return points;
        }

        /// <summary>An orb expired uncollected: the chain is broken.</summary>
        public void OrbMissed()
        {
            Combo = 0;
            ComboTimer = 0f;
        }

        /// <summary>The player touched a hazard. Returns true if it cost a life (false while invulnerable or over).</summary>
        public bool HitHazard()
        {
            if (IsOver || IsInvulnerable) return false;
            Lives--;
            Combo = 0;
            ComboTimer = 0f;
            InvulnerableTimer = InvulnerableSeconds;
            if (Lives <= 0)
            {
                Lives = 0;
                IsOver = true;
            }
            return true;
        }

        public void Tick(float dt)
        {
            if (IsOver) return;
            if (ComboTimer > 0f)
            {
                ComboTimer -= dt;
                if (ComboTimer <= 0f)
                {
                    ComboTimer = 0f;
                    Combo = 0;
                }
            }
            if (InvulnerableTimer > 0f)
            {
                InvulnerableTimer -= dt;
                if (InvulnerableTimer < 0f) InvulnerableTimer = 0f;
            }
        }
    }
}
