using System;

namespace OrbRush.Core
{
    public static class PlayerMotion
    {
        /// <summary>
        /// Move the player. Input is normalised when longer than 1, so diagonals are not faster.
        /// The result is clamped so the whole circle stays inside the arena.
        /// </summary>
        public static Vec2 Step(Vec2 position, Vec2 input, float speed, float dt, float halfW, float halfH, float radius)
        {
            Vec2 dir = input;
            if (dir.SqrLength > 1f) dir = dir.Normalized();
            Vec2 next = position + dir * (speed * dt);
            return ClampToArena(next, halfW, halfH, radius);
        }

        public static Vec2 ClampToArena(Vec2 p, float halfW, float halfH, float radius)
        {
            float maxX = Math.Max(0f, halfW - radius);
            float maxY = Math.Max(0f, halfH - radius);
            return new Vec2(Math.Max(-maxX, Math.Min(maxX, p.X)), Math.Max(-maxY, Math.Min(maxY, p.Y)));
        }
    }

    public static class Hit
    {
        /// <summary>Circles touch when the centre distance is at most the sum of the radii.</summary>
        public static bool Overlaps(Vec2 a, float ra, Vec2 b, float rb)
        {
            float r = ra + rb;
            return (a - b).SqrLength <= r * r;
        }
    }
}
