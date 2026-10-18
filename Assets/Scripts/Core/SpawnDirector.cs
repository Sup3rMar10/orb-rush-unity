using System;

namespace OrbRush.Core
{
    public struct HazardSpawn
    {
        public Vec2 Position;
        public Vec2 Velocity;
        public float Radius;
    }

    /// <summary>Decides when and where orbs and hazards appear. Deterministic for a given seed.</summary>
    public sealed class SpawnDirector
    {
        public const int MaxOrbs = 3;
        public const float OrbMargin = 0.6f;

        private readonly Rng rng;
        private float hazardTimer;
        private float orbTimer;

        public SpawnDirector(int seed)
        {
            rng = new Rng(seed);
            hazardTimer = 1.0f; // short grace period at the start
            orbTimer = 0f;
        }

        /// <summary>True when a new orb should appear: always when none is on the field, otherwise on a timer up to MaxOrbs.</summary>
        public bool ShouldSpawnOrb(float dt, int activeOrbs)
        {
            if (activeOrbs <= 0)
            {
                orbTimer = rng.Range(1.5f, 3.0f);
                return true;
            }
            if (activeOrbs >= MaxOrbs) return false;
            orbTimer -= dt;
            if (orbTimer > 0f) return false;
            orbTimer = rng.Range(1.5f, 3.0f);
            return true;
        }

        public bool ShouldSpawnHazard(float dt, int level, int activeHazards)
        {
            hazardTimer -= dt;
            if (hazardTimer > 0f) return false;
            if (activeHazards >= Difficulty.MaxHazards(level))
            {
                hazardTimer = 0f; // spawn as soon as a slot frees up
                return false;
            }
            hazardTimer = Difficulty.HazardSpawnInterval(level) * rng.Range(0.8f, 1.2f);
            return true;
        }

        /// <summary>A point inside the arena (with a margin) that is at least minDistance from the player.</summary>
        public Vec2 NextOrbPosition(Vec2 player, float halfW, float halfH, float minDistance)
        {
            float maxX = Math.Max(0f, halfW - OrbMargin);
            float maxY = Math.Max(0f, halfH - OrbMargin);
            for (int i = 0; i < 30; i++)
            {
                Vec2 p = new Vec2(rng.Range(-maxX, maxX), rng.Range(-maxY, maxY));
                if (Vec2.Distance(p, player) >= minDistance) return p;
            }
            // Arena too small for the requested distance: take the corner furthest from the player.
            float cx = player.X >= 0f ? -maxX : maxX;
            float cy = player.Y >= 0f ? -maxY : maxY;
            return new Vec2(cx, cy);
        }

        /// <summary>A hazard that starts just outside a random edge and drifts roughly toward the player.</summary>
        public HazardSpawn NextHazard(Vec2 player, float halfW, float halfH, int level)
        {
            float radius = rng.Range(0.3f, 0.55f);
            float margin = radius + 0.1f;
            Vec2 pos;
            switch (rng.Range(0, 4))
            {
                case 0: pos = new Vec2(rng.Range(-halfW, halfW), halfH + margin); break;   // top
                case 1: pos = new Vec2(rng.Range(-halfW, halfW), -halfH - margin); break;  // bottom
                case 2: pos = new Vec2(-halfW - margin, rng.Range(-halfH, halfH)); break;  // left
                default: pos = new Vec2(halfW + margin, rng.Range(-halfH, halfH)); break;  // right
            }

            Vec2 aim = new Vec2(player.X + rng.Range(-2f, 2f), player.Y + rng.Range(-2f, 2f));
            Vec2 dir = (aim - pos).Normalized();
            float speed = Difficulty.HazardSpeed(level) * rng.Range(0.85f, 1.15f);

            HazardSpawn spawn;
            spawn.Position = pos;
            spawn.Velocity = dir * speed;
            spawn.Radius = radius;
            return spawn;
        }
    }
}
