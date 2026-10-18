// Dependency-free test runner for the engine-free game rules (Assets/Scripts/Core).
// Exit code 0 = all passed, 1 = at least one failed.
using System;
using System.Collections.Generic;
using OrbRush.Core;

namespace LogicTests
{
    public static class Program
    {
        private static int passed;
        private static int failed;

        private static void Run(string name, Action body)
        {
            try
            {
                body();
                passed++;
            }
            catch (Exception e)
            {
                failed++;
                Console.WriteLine("FAIL " + name + ": " + e.Message);
            }
        }

        private static void True(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        private static void Eq<T>(T expected, T actual, string what)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new Exception(what + ": expected " + expected + " but got " + actual);
        }

        private static void Near(float expected, float actual, float eps, string what)
        {
            if (Math.Abs(expected - actual) > eps)
                throw new Exception(what + ": expected " + expected + " but got " + actual);
        }

        private static bool Collect(GameState s)
        {
            bool lvl;
            s.CollectOrb(out lvl);
            return lvl;
        }

        public static int Main()
        {
            // ---- Rng ----
            Run("rng same seed same sequence", delegate
            {
                Rng a = new Rng(42), b = new Rng(42);
                for (int i = 0; i < 100; i++) Eq(a.NextUInt(), b.NextUInt(), "value " + i);
            });
            Run("rng different seeds differ", delegate
            {
                Rng a = new Rng(1), b = new Rng(2);
                bool differ = false;
                for (int i = 0; i < 10; i++) if (a.NextUInt() != b.NextUInt()) differ = true;
                True(differ, "sequences identical");
            });
            Run("rng seed zero is not stuck", delegate
            {
                Rng r = new Rng(0);
                uint first = r.NextUInt();
                True(first != 0u && r.NextUInt() != first, "stuck state");
            });
            Run("rng survives the one seed that mixes to a zero state", delegate
            {
                // state = seed * K + C (mod 2^32) is zero for exactly one seed; solve it with the modular inverse of K.
                uint k = 2654435761u, c = 0x9E3779B9u, inv = k;
                for (int i = 0; i < 6; i++) inv = inv * (2u - k * inv);
                uint badSeed = (0u - c) * inv;
                True(badSeed * k + c == 0u, "test setup: seed does not mix to zero");
                Rng r = new Rng((int)badSeed);
                uint a = r.NextUInt(), b = r.NextUInt();
                True(a != 0u && b != 0u && a != b, "generator is stuck at zero");
            });
            Run("rng float in [0,1)", delegate
            {
                Rng r = new Rng(7);
                for (int i = 0; i < 5000; i++)
                {
                    float f = r.NextFloat();
                    True(f >= 0f && f < 1f, "out of range " + f);
                }
            });
            Run("rng float range bounds", delegate
            {
                Rng r = new Rng(8);
                for (int i = 0; i < 5000; i++)
                {
                    float f = r.Range(-3f, 5f);
                    True(f >= -3f && f < 5f, "out of range " + f);
                }
            });
            Run("rng int range covers all values and stays in bounds", delegate
            {
                Rng r = new Rng(9);
                bool[] seen = new bool[4];
                for (int i = 0; i < 500; i++)
                {
                    int v = r.Range(0, 4);
                    True(v >= 0 && v < 4, "out of range " + v);
                    seen[v] = true;
                }
                for (int i = 0; i < 4; i++) True(seen[i], "never produced " + i);
            });
            Run("rng int range degenerate returns min", delegate
            {
                Eq(5, new Rng(1).Range(5, 5), "empty range");
                Eq(5, new Rng(1).Range(5, 2), "inverted range");
            });
            Run("rng roughly uniform", delegate
            {
                Rng r = new Rng(10);
                double sum = 0;
                int n = 20000;
                for (int i = 0; i < n; i++) sum += r.NextFloat();
                Near(0.5f, (float)(sum / n), 0.02f, "mean");
            });

            // ---- Vec2 ----
            Run("vec2 length and normalise", delegate
            {
                Near(5f, new Vec2(3f, 4f).Length, 1e-5f, "length");
                Near(1f, new Vec2(3f, 4f).Normalized().Length, 1e-5f, "normalised length");
            });
            Run("vec2 normalise zero stays zero", delegate
            {
                Vec2 z = Vec2.Zero.Normalized();
                True(z.X == 0f && z.Y == 0f, "zero vector changed");
            });
            Run("vec2 distance and arithmetic", delegate
            {
                Near(5f, Vec2.Distance(new Vec2(0f, 0f), new Vec2(3f, 4f)), 1e-5f, "distance");
                Vec2 v = new Vec2(1f, 2f) + new Vec2(3f, 4f) - new Vec2(1f, 1f);
                Near(3f, v.X, 1e-6f, "x");
                Near(5f, v.Y, 1e-6f, "y");
            });

            // ---- Difficulty ----
            Run("difficulty is monotonic and capped", delegate
            {
                for (int l = 1; l < 40; l++)
                {
                    True(Difficulty.HazardSpeed(l + 1) >= Difficulty.HazardSpeed(l), "speed fell at " + l);
                    True(Difficulty.HazardSpawnInterval(l + 1) <= Difficulty.HazardSpawnInterval(l), "interval rose at " + l);
                    True(Difficulty.MaxHazards(l + 1) >= Difficulty.MaxHazards(l), "max hazards fell at " + l);
                    True(Difficulty.OrbLifetime(l + 1) <= Difficulty.OrbLifetime(l), "lifetime rose at " + l);
                }
                Near(6f, Difficulty.HazardSpeed(999), 1e-6f, "speed cap");
                Near(0.55f, Difficulty.HazardSpawnInterval(999), 1e-6f, "interval floor");
                Eq(14, Difficulty.MaxHazards(999), "hazard cap");
                Near(3.5f, Difficulty.OrbLifetime(999), 1e-6f, "lifetime floor");
            });
            Run("difficulty level one values", delegate
            {
                Near(1.6f, Difficulty.HazardSpeed(1), 1e-6f, "speed");
                Near(2.4f, Difficulty.HazardSpawnInterval(1), 1e-6f, "interval");
                Eq(4, Difficulty.MaxHazards(1), "max hazards");
            });

            // ---- GameState ----
            Run("state starts fresh", delegate
            {
                GameState s = new GameState();
                Eq(0, s.Score, "score");
                Eq(GameState.StartLives, s.Lives, "lives");
                Eq(1, s.Level, "level");
                True(!s.IsOver && !s.IsInvulnerable, "flags");
            });
            Run("first orb scores base points and starts combo", delegate
            {
                GameState s = new GameState();
                bool lvl;
                int pts = s.CollectOrb(out lvl);
                Eq(10, pts, "points");
                Eq(1, s.Combo, "combo");
                True(!lvl, "level up too early");
            });
            Run("combo multiplier grows then caps at five", delegate
            {
                GameState s = new GameState();
                bool lvl;
                int[] expected = { 10, 20, 30, 40, 50, 50 };
                for (int i = 0; i < expected.Length; i++)
                    Eq(expected[i], s.CollectOrb(out lvl), "orb " + (i + 1));
            });
            Run("combo expires after the window", delegate
            {
                GameState s = new GameState();
                bool lvl;
                s.CollectOrb(out lvl);
                s.Tick(GameState.ComboWindow + 0.01f);
                Eq(0, s.Combo, "combo after timeout");
                Eq(10, s.CollectOrb(out lvl), "next orb starts a new chain");
            });
            Run("combo survives inside the window", delegate
            {
                GameState s = new GameState();
                bool lvl;
                s.CollectOrb(out lvl);
                s.Tick(GameState.ComboWindow - 0.1f);
                Eq(20, s.CollectOrb(out lvl), "chained orb");
            });
            Run("collecting refreshes the combo timer", delegate
            {
                GameState s = new GameState();
                bool lvl;
                s.CollectOrb(out lvl);
                s.Tick(1.5f);
                s.CollectOrb(out lvl);
                s.Tick(1.5f);
                Eq(2, s.Combo, "combo kept alive by second orb");
            });
            Run("missed orb breaks the combo", delegate
            {
                GameState s = new GameState();
                bool lvl;
                s.CollectOrb(out lvl);
                s.CollectOrb(out lvl);
                s.OrbMissed();
                Eq(0, s.Combo, "combo");
                Eq(10, s.CollectOrb(out lvl), "next orb");
            });
            Run("level up after OrbsPerLevel orbs with bonus", delegate
            {
                GameState s = new GameState();
                bool lvl = false;
                for (int i = 0; i < Difficulty.OrbsPerLevel - 1; i++)
                {
                    lvl = Collect(s);
                    True(!lvl, "early level up at orb " + (i + 1));
                }
                int before = s.Score;
                bool last;
                int pts = s.CollectOrb(out last);
                True(last, "no level up");
                Eq(2, s.Level, "level");
                Eq(0, s.OrbsThisLevel, "orb counter reset");
                Eq(before + pts + GameState.LevelBonusPerLevel * 2, s.Score, "bonus");
            });
            Run("every third level gives a life up to the cap", delegate
            {
                GameState s = new GameState();
                int livesAtStart = s.Lives;
                while (s.Level < 3) Collect(s);
                Eq(livesAtStart + 1, s.Lives, "life at level 3");
                while (s.Level < 12) Collect(s);
                Eq(GameState.MaxLives, s.Lives, "lives capped");
            });
            Run("hazard hit costs a life, resets combo and grants invulnerability", delegate
            {
                GameState s = new GameState();
                bool lvl;
                s.CollectOrb(out lvl);
                True(s.HitHazard(), "hit ignored");
                Eq(GameState.StartLives - 1, s.Lives, "lives");
                Eq(0, s.Combo, "combo");
                True(s.IsInvulnerable, "not invulnerable");
            });
            Run("no second hit while invulnerable", delegate
            {
                GameState s = new GameState();
                s.HitHazard();
                True(!s.HitHazard(), "second hit counted");
                Eq(GameState.StartLives - 1, s.Lives, "lives");
            });
            Run("invulnerability ends after its duration", delegate
            {
                GameState s = new GameState();
                s.HitHazard();
                s.Tick(GameState.InvulnerableSeconds - 0.1f);
                True(s.IsInvulnerable, "ended early");
                s.Tick(0.2f);
                True(!s.IsInvulnerable, "never ended");
                True(s.HitHazard(), "hit ignored after window");
            });
            Run("game over at zero lives and then everything is inert", delegate
            {
                GameState s = new GameState();
                for (int i = 0; i < GameState.StartLives; i++)
                {
                    s.HitHazard();
                    s.Tick(GameState.InvulnerableSeconds + 0.1f);
                }
                True(s.IsOver, "not over");
                Eq(0, s.Lives, "lives");
                int score = s.Score;
                bool lvl;
                Eq(0, s.CollectOrb(out lvl), "points after game over");
                Eq(score, s.Score, "score changed after game over");
                True(!s.HitHazard(), "hit counted after game over");
                Eq(0, s.Lives, "lives went negative");
            });
            Run("reset restores a clean state", delegate
            {
                GameState s = new GameState();
                Collect(s);
                s.HitHazard();
                s.Reset();
                Eq(0, s.Score, "score");
                Eq(GameState.StartLives, s.Lives, "lives");
                Eq(0, s.Combo, "combo");
                True(!s.IsInvulnerable && !s.IsOver, "flags");
            });
            Run("tick does not drive the timers negative", delegate
            {
                GameState s = new GameState();
                s.Tick(5f);
                Near(0f, s.ComboTimer, 1e-6f, "combo timer");
                Near(0f, s.InvulnerableTimer, 1e-6f, "invuln timer");
            });

            // ---- HighScore ----
            Run("highscore parse handles junk", delegate
            {
                Eq(0, HighScore.Parse(null), "null");
                Eq(0, HighScore.Parse(""), "empty");
                Eq(0, HighScore.Parse("abc"), "text");
                Eq(0, HighScore.Parse("-5"), "negative");
                Eq(0, HighScore.Parse("99999999999999"), "overflow");
                Eq(1234, HighScore.Parse("1234"), "valid");
            });
            Run("highscore format round trips", delegate
            {
                Eq(777, HighScore.Parse(HighScore.Format(777)), "round trip");
                Eq("0", HighScore.Format(-3), "negative formats as zero");
            });
            Run("highscore update only on strictly higher", delegate
            {
                bool isNew;
                Eq(100, HighScore.Update(100, 100, out isNew), "tie keeps best");
                True(!isNew, "tie flagged as new");
                Eq(150, HighScore.Update(100, 150, out isNew), "higher replaces");
                True(isNew, "higher not flagged");
                Eq(100, HighScore.Update(100, 40, out isNew), "lower ignored");
                True(!isNew, "lower flagged");
            });

            // ---- Motion ----
            Run("motion moves at speed", delegate
            {
                Vec2 p = PlayerMotion.Step(Vec2.Zero, new Vec2(1f, 0f), 4f, 0.5f, 10f, 10f, 0.4f);
                Near(2f, p.X, 1e-5f, "x");
                Near(0f, p.Y, 1e-5f, "y");
            });
            Run("diagonal is not faster than straight", delegate
            {
                Vec2 p = PlayerMotion.Step(Vec2.Zero, new Vec2(1f, 1f), 4f, 1f, 50f, 50f, 0.4f);
                Near(4f, p.Length, 1e-4f, "distance moved");
            });
            Run("partial stick input is not boosted", delegate
            {
                Vec2 p = PlayerMotion.Step(Vec2.Zero, new Vec2(0.5f, 0f), 4f, 1f, 50f, 50f, 0.4f);
                Near(2f, p.X, 1e-5f, "x");
            });
            Run("player stays inside the arena by its radius", delegate
            {
                Vec2 p = PlayerMotion.Step(new Vec2(8.9f, 0f), new Vec2(1f, 0f), 100f, 1f, 9f, 5f, 0.4f);
                Near(8.6f, p.X, 1e-5f, "x");
                p = PlayerMotion.Step(new Vec2(0f, -4.5f), new Vec2(0f, -1f), 100f, 1f, 9f, 5f, 0.4f);
                Near(-4.6f, p.Y, 1e-5f, "y");
            });
            Run("clamp survives an arena smaller than the player", delegate
            {
                Vec2 p = PlayerMotion.ClampToArena(new Vec2(3f, 3f), 0.2f, 0.2f, 0.5f);
                Near(0f, p.X, 1e-6f, "x");
                Near(0f, p.Y, 1e-6f, "y");
            });

            // ---- Hit ----
            Run("circles overlap at exactly touching distance", delegate
            {
                True(Hit.Overlaps(Vec2.Zero, 0.5f, new Vec2(1f, 0f), 0.5f), "touching circles");
            });
            Run("circles apart do not overlap", delegate
            {
                True(!Hit.Overlaps(Vec2.Zero, 0.5f, new Vec2(1.01f, 0f), 0.5f), "separated circles");
            });
            Run("overlap is symmetric", delegate
            {
                Vec2 a = new Vec2(0.2f, 0.3f), b = new Vec2(0.6f, 0.1f);
                Eq(Hit.Overlaps(a, 0.3f, b, 0.2f), Hit.Overlaps(b, 0.2f, a, 0.3f), "symmetry");
            });

            // ---- SpawnDirector ----
            Run("orb spawns immediately when none are active", delegate
            {
                SpawnDirector d = new SpawnDirector(1);
                True(d.ShouldSpawnOrb(0f, 0), "no orb spawned");
            });
            Run("orb spawn is capped", delegate
            {
                SpawnDirector d = new SpawnDirector(1);
                for (int i = 0; i < 100; i++) True(!d.ShouldSpawnOrb(10f, SpawnDirector.MaxOrbs), "spawned over cap");
            });
            Run("orb spawn waits for its timer", delegate
            {
                SpawnDirector d = new SpawnDirector(1);
                d.ShouldSpawnOrb(0f, 0); // arms the timer at 1.5..3.0s
                True(!d.ShouldSpawnOrb(0.1f, 1), "spawned too early");
                True(d.ShouldSpawnOrb(5f, 1), "never spawned");
            });
            Run("hazard has a grace period then spawns on interval", delegate
            {
                SpawnDirector d = new SpawnDirector(1);
                True(!d.ShouldSpawnHazard(0.5f, 1, 0), "spawned during grace period");
                True(d.ShouldSpawnHazard(0.6f, 1, 0), "no spawn after grace period");
                True(!d.ShouldSpawnHazard(0.1f, 1, 1), "spawned again immediately");
            });
            Run("hazard cap blocks spawns and frees up immediately", delegate
            {
                SpawnDirector d = new SpawnDirector(1);
                int cap = Difficulty.MaxHazards(1);
                True(!d.ShouldSpawnHazard(5f, 1, cap), "spawned over cap");
                True(d.ShouldSpawnHazard(0.01f, 1, cap - 1), "did not spawn when a slot freed");
            });
            Run("orb positions stay inside the arena and away from the player", delegate
            {
                SpawnDirector d = new SpawnDirector(3);
                Vec2 player = new Vec2(1f, 1f);
                for (int i = 0; i < 500; i++)
                {
                    Vec2 p = d.NextOrbPosition(player, 8f, 5f, 2f);
                    True(Math.Abs(p.X) <= 8f - SpawnDirector.OrbMargin + 1e-4f, "x outside arena: " + p);
                    True(Math.Abs(p.Y) <= 5f - SpawnDirector.OrbMargin + 1e-4f, "y outside arena: " + p);
                    True(Vec2.Distance(p, player) >= 2f - 1e-4f, "too close to player: " + p);
                }
            });
            Run("orb position falls back to a far corner in a tiny arena", delegate
            {
                SpawnDirector d = new SpawnDirector(3);
                Vec2 p = d.NextOrbPosition(new Vec2(0.5f, 0.5f), 1f, 1f, 50f);
                True(p.X < 0f && p.Y < 0f, "fallback not opposite the player: " + p);
            });
            Run("hazards start outside the arena", delegate
            {
                SpawnDirector d = new SpawnDirector(4);
                for (int i = 0; i < 500; i++)
                {
                    HazardSpawn h = d.NextHazard(Vec2.Zero, 8f, 5f, 1);
                    bool outside = Math.Abs(h.Position.X) > 8f || Math.Abs(h.Position.Y) > 5f;
                    True(outside, "spawned inside the arena: " + h.Position);
                }
            });
            Run("hazards head inward at roughly the level speed", delegate
            {
                SpawnDirector d = new SpawnDirector(5);
                for (int level = 1; level <= 10; level++)
                {
                    for (int i = 0; i < 100; i++)
                    {
                        HazardSpawn h = d.NextHazard(Vec2.Zero, 8f, 5f, level);
                        float speed = h.Velocity.Length;
                        float baseSpeed = Difficulty.HazardSpeed(level);
                        True(speed >= baseSpeed * 0.85f - 1e-3f && speed <= baseSpeed * 1.15f + 1e-3f,
                            "speed " + speed + " outside band at level " + level);
                        // Moving toward the origin: velocity must reduce the distance to it.
                        True(Vec2.Dot(h.Velocity, h.Position * -1f) > 0f, "moving away from the arena: " + h.Position + " " + h.Velocity);
                    }
                }
            });
            Run("hazard radius is in range", delegate
            {
                SpawnDirector d = new SpawnDirector(6);
                for (int i = 0; i < 500; i++)
                {
                    float r = d.NextHazard(Vec2.Zero, 8f, 5f, 1).Radius;
                    True(r >= 0.3f && r <= 0.55f, "radius " + r);
                }
            });
            Run("same seed gives the same spawns", delegate
            {
                SpawnDirector a = new SpawnDirector(11), b = new SpawnDirector(11);
                for (int i = 0; i < 50; i++)
                {
                    HazardSpawn ha = a.NextHazard(Vec2.Zero, 8f, 5f, 3);
                    HazardSpawn hb = b.NextHazard(Vec2.Zero, 8f, 5f, 3);
                    True(ha.Position.X == hb.Position.X && ha.Position.Y == hb.Position.Y, "position diverged at " + i);
                }
            });

            // ---- Whole-run simulation: a bot that never moves must lose, and fast enough ----
            Run("an idle player eventually loses", delegate
            {
                GameState s = new GameState();
                SpawnDirector d = new SpawnDirector(99);
                List<HazardSpawn> hazards = new List<HazardSpawn>();
                Vec2 player = Vec2.Zero;
                float t = 0f;
                float dt = 1f / 60f;
                while (!s.IsOver && t < 600f)
                {
                    s.Tick(dt);
                    if (d.ShouldSpawnHazard(dt, s.Level, hazards.Count)) hazards.Add(d.NextHazard(player, 8f, 5f, s.Level));
                    for (int i = hazards.Count - 1; i >= 0; i--)
                    {
                        HazardSpawn h = hazards[i];
                        h.Position = h.Position + h.Velocity * dt;
                        hazards[i] = h;
                        if (Hit.Overlaps(player, 0.4f, h.Position, h.Radius)) s.HitHazard();
                        if (Math.Abs(h.Position.X) > 11f || Math.Abs(h.Position.Y) > 8f) hazards.RemoveAt(i);
                    }
                    t += dt;
                }
                True(s.IsOver, "idle player survived 10 minutes: hazards never reach the middle");
            });

            Console.WriteLine(passed + " passed, " + failed + " failed");
            return failed == 0 ? 0 : 1;
        }
    }
}
