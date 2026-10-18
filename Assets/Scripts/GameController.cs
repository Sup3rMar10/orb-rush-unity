using System;
using System.Collections.Generic;
using UnityEngine;
using OrbRush.Core;

namespace OrbRush
{
    /// <summary>
    /// Unity side of Orb Rush: builds the camera, arena and sprites, reads input, moves things and
    /// draws the HUD. All rules (scoring, combo, lives, spawning, collision maths) live in
    /// OrbRush.Core, which has no engine dependency and is covered by Tests/LogicTests.
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        private const float PlayerSpeed = 6f;
        private const float PlayerRadius = 0.4f;
        private const float OrbRadius = 0.3f;
        private const float MaxFrameTime = 0.05f; // clamp hitches so nothing tunnels through the player
        private const string BestKey = "orbrush.best";

        private sealed class Orb
        {
            public GameObject Go;
            public SpriteRenderer Renderer;
            public Vec2 Pos;
            public float Life;
        }

        private sealed class Hazard
        {
            public GameObject Go;
            public Vec2 Pos;
            public Vec2 Vel;
            public float Radius;
        }

        private readonly List<Orb> orbs = new List<Orb>();
        private readonly List<Hazard> hazards = new List<Hazard>();
        private readonly Transform[] borders = new Transform[4];

        private GameState state;
        private SpawnDirector director;
        private Sfx sfx;
        private Camera cam;
        private float halfW, halfH;
        private Transform playerTransform;
        private SpriteRenderer playerRenderer;
        private Vec2 playerPos;
        private int best;
        private bool newBest;
        private bool paused;
        private float runTime;
        private GUIStyle bigStyle, midStyle, smallStyle;

        private void Awake()
        {
            state = new GameState();
            sfx = new Sfx(gameObject);
            SetupCamera();
            SetupArena();
            SetupPlayer();
            StartGame();
        }

        private void SetupCamera()
        {
            cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                cam = go.AddComponent<Camera>();
            }
            if (FindFirstObjectByType<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.06f, 0.12f);
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.transform.rotation = Quaternion.identity;
            UpdateBounds();
        }

        private void SetupArena()
        {
            for (int i = 0; i < borders.Length; i++)
            {
                var sr = MakeSprite("Border" + i, SpriteFactory.Square(), new Color(0.25f, 0.3f, 0.5f), -10);
                borders[i] = sr.transform;
            }
            UpdateBounds();
        }

        private void SetupPlayer()
        {
            playerRenderer = MakeSprite("Player", SpriteFactory.Circle(), new Color(0.25f, 0.9f, 1f), 10);
            playerTransform = playerRenderer.transform;
            playerTransform.localScale = Vector3.one * (PlayerRadius * 2f);

            // Soft halo as a child; the child inherits the parent's scale, so 1.8 means 1.8x the player.
            var glow = MakeSprite("Glow", SpriteFactory.Circle(), new Color(0.25f, 0.9f, 1f, 0.2f), 9);
            glow.transform.SetParent(playerTransform, false);
            glow.transform.localScale = Vector3.one * 1.8f;
        }

        private SpriteRenderer MakeSprite(string objectName, Sprite sprite, Color color, int order)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        private void StartGame()
        {
            foreach (var o in orbs) Destroy(o.Go);
            foreach (var h in hazards) Destroy(h.Go);
            orbs.Clear();
            hazards.Clear();

            state.Reset();
            director = new SpawnDirector(Environment.TickCount);
            playerPos = Vec2.Zero;
            newBest = false;
            paused = false;
            runTime = 0f;
            best = HighScore.Parse(PlayerPrefs.GetString(BestKey, ""));
            SyncPlayer();
        }

        private void UpdateBounds()
        {
            halfH = cam.orthographicSize;
            halfW = halfH * cam.aspect;
            const float t = 0.1f;
            if (borders[0] == null) return;
            Place(borders[0], 0f, halfH - t / 2f, halfW * 2f, t);
            Place(borders[1], 0f, -halfH + t / 2f, halfW * 2f, t);
            Place(borders[2], -halfW + t / 2f, 0f, t, halfH * 2f);
            Place(borders[3], halfW - t / 2f, 0f, t, halfH * 2f);
        }

        private static void Place(Transform tr, float x, float y, float w, float h)
        {
            tr.position = new Vector3(x, y, 0f);
            tr.localScale = new Vector3(w, h, 1f);
        }

        private void Update()
        {
            UpdateBounds();

            if (!state.IsOver && InputReader.PausePressed()) paused = !paused;
            if (state.IsOver && InputReader.RestartPressed())
            {
                StartGame();
                return;
            }
            if (paused || state.IsOver) return;

            float dt = Mathf.Min(Time.deltaTime, MaxFrameTime);
            runTime += dt;

            playerPos = PlayerMotion.Step(playerPos, InputReader.Move(), PlayerSpeed, dt, halfW, halfH, PlayerRadius);
            state.Tick(dt);

            if (director.ShouldSpawnOrb(dt, orbs.Count)) SpawnOrb();
            if (director.ShouldSpawnHazard(dt, state.Level, hazards.Count)) SpawnHazard();

            UpdateOrbs(dt);
            UpdateHazards(dt);
            SyncPlayer();
        }

        private void SpawnOrb()
        {
            var sr = MakeSprite("Orb", SpriteFactory.Circle(), new Color(1f, 0.85f, 0.2f), 5);
            Vec2 pos = director.NextOrbPosition(playerPos, halfW, halfH, 2f);
            sr.transform.position = new Vector3(pos.X, pos.Y, 0f);
            orbs.Add(new Orb { Go = sr.gameObject, Renderer = sr, Pos = pos, Life = Difficulty.OrbLifetime(state.Level) });
        }

        private void SpawnHazard()
        {
            HazardSpawn s = director.NextHazard(playerPos, halfW, halfH, state.Level);
            var sr = MakeSprite("Hazard", SpriteFactory.Circle(), new Color(1f, 0.25f, 0.25f), 6);
            sr.transform.localScale = Vector3.one * (s.Radius * 2f);
            sr.transform.position = new Vector3(s.Position.X, s.Position.Y, 0f);
            hazards.Add(new Hazard { Go = sr.gameObject, Pos = s.Position, Vel = s.Velocity, Radius = s.Radius });
        }

        private void UpdateOrbs(float dt)
        {
            for (int i = orbs.Count - 1; i >= 0; i--)
            {
                Orb o = orbs[i];
                o.Life -= dt;

                if (Hit.Overlaps(playerPos, PlayerRadius, o.Pos, OrbRadius))
                {
                    bool leveledUp;
                    state.CollectOrb(out leveledUp);
                    sfx.Collect();
                    if (leveledUp) sfx.LevelUp();
                    Destroy(o.Go);
                    orbs.RemoveAt(i);
                    continue;
                }
                if (o.Life <= 0f)
                {
                    state.OrbMissed();
                    Destroy(o.Go);
                    orbs.RemoveAt(i);
                    continue;
                }

                // Pulse, and blink during the last two seconds as a warning.
                float pulse = 1f + 0.12f * Mathf.Sin(Time.time * 6f);
                o.Go.transform.localScale = Vector3.one * (OrbRadius * 2f * pulse);
                Color c = o.Renderer.color;
                c.a = o.Life < 2f && ((int)(Time.time * 8f) % 2 == 0) ? 0.35f : 1f;
                o.Renderer.color = c;
            }
        }

        private void UpdateHazards(float dt)
        {
            for (int i = hazards.Count - 1; i >= 0; i--)
            {
                Hazard h = hazards[i];
                h.Pos = h.Pos + h.Vel * dt;
                h.Go.transform.position = new Vector3(h.Pos.X, h.Pos.Y, 0f);

                if (Hit.Overlaps(playerPos, PlayerRadius, h.Pos, h.Radius) && state.HitHazard())
                {
                    sfx.Hit();
                    if (state.IsOver)
                    {
                        sfx.GameOver();
                        SaveBest();
                    }
                }

                if (Mathf.Abs(h.Pos.X) > halfW + 2f || Mathf.Abs(h.Pos.Y) > halfH + 2f)
                {
                    Destroy(h.Go);
                    hazards.RemoveAt(i);
                }
            }
        }

        private void SaveBest()
        {
            bool isNew;
            best = HighScore.Update(best, state.Score, out isNew);
            newBest = isNew;
            PlayerPrefs.SetString(BestKey, HighScore.Format(best));
            PlayerPrefs.Save();
        }

        private void SyncPlayer()
        {
            playerTransform.position = new Vector3(playerPos.X, playerPos.Y, 0f);
            // Blink while invulnerable after a hit.
            playerRenderer.enabled = !(state.IsInvulnerable && ((int)(Time.time * 10f) % 2 == 0));
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && state != null && !state.IsOver) paused = true;
        }

        // ---- HUD (IMGUI: no UI package or canvas needed) ----

        private void EnsureStyles()
        {
            int h = Screen.height;
            if (bigStyle == null)
            {
                bigStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                midStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold };
                smallStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
            }
            bigStyle.fontSize = Mathf.Max(24, h / 10);
            midStyle.fontSize = Mathf.Max(14, h / 28);
            smallStyle.fontSize = Mathf.Max(12, h / 36);
        }

        private static void Shadowed(Rect r, string text, GUIStyle style, Color color)
        {
            Color old = style.normal.textColor;
            style.normal.textColor = new Color(0f, 0f, 0f, 0.7f);
            GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), text, style);
            style.normal.textColor = color;
            GUI.Label(r, text, style);
            style.normal.textColor = old;
        }

        private void OnGUI()
        {
            if (state == null) return;
            EnsureStyles();

            float w = Screen.width, h = Screen.height;
            int shownBest = Mathf.Max(best, state.Score);
            Shadowed(new Rect(16f, 8f, w, h * 0.06f),
                "Score " + state.Score + "    Level " + state.Level + "    Best " + shownBest, midStyle, Color.white);
            Shadowed(new Rect(16f, 8f + h * 0.055f, w, h * 0.06f),
                "Lives " + state.Lives, midStyle, new Color(1f, 0.5f, 0.5f));
            if (state.Combo >= 2)
                Shadowed(new Rect(16f, 8f + h * 0.11f, w, h * 0.06f),
                    "Combo x" + state.Multiplier + "  (" + state.Combo + " in a row)", midStyle, new Color(1f, 0.85f, 0.2f));

            if (!state.IsOver && !paused && runTime < 8f)
                Shadowed(new Rect(0f, h * 0.9f, w, h * 0.06f),
                    "WASD / arrow keys to move.  Grab yellow orbs, dodge red.  P to pause.", smallStyle, new Color(1f, 1f, 1f, 0.85f));

            if (paused)
            {
                Shadowed(new Rect(0f, h * 0.35f, w, h * 0.2f), "PAUSED", bigStyle, Color.white);
                Shadowed(new Rect(0f, h * 0.55f, w, h * 0.06f), "Press P to resume", smallStyle, Color.white);
            }

            if (state.IsOver)
            {
                Shadowed(new Rect(0f, h * 0.28f, w, h * 0.2f), "GAME OVER", bigStyle, new Color(1f, 0.35f, 0.35f));
                Shadowed(new Rect(0f, h * 0.48f, w, h * 0.07f),
                    "Score " + state.Score + (newBest ? "   NEW BEST!" : "   Best " + best), smallStyle, Color.white);
                Shadowed(new Rect(0f, h * 0.56f, w, h * 0.06f), "Press R or Space to play again", smallStyle, Color.white);
            }
        }
    }
}
