# orb-rush-unity

![ci](https://github.com/Sup3rMar10/orb-rush-unity/actions/workflows/ci.yml/badge.svg)

A small top-down arcade game for **Unity 6**: steer a glowing dot, grab yellow orbs, dodge red hazards. Chain orbs for a score multiplier, level up for harder hazards, survive on three lives.

> **What is verified, and what is not.** I wrote this on a machine with **no Unity editor installed**, so **the game has never been opened or played in Unity**. What *was* checked:
> - The game rules (scoring, combo, lives, levels, spawning, collision maths) live in plain C# under `Assets/Scripts/Core` with no Unity types, and **52 tests pass** on them. I broke the rules on purpose 16 ways (combo cap, invulnerability, off-by-one caps, hazards moving the wrong way, ...) and the tests failed every time; one break first survived, which is why the RNG zero-seed test exists.
> - The Unity-facing scripts compile against **hand-written stubs** of the Unity API, with each input-handling configuration. That catches typos and type mistakes in my code. It does **not** check that I used the real Unity API correctly, and it says nothing about how the game looks or feels.
> - Not checked at all: `Assets/Editor/BuildScript.cs`, the `build-unity` workflow (needs a Unity license, never run), the pinned editor version `6000.0.23f1`, and the `com.unity.inputsystem` 1.11.2 package version. If Unity reports a problem on first open, those are the first places to look.

## Play it

1. Install **Unity 6** (any 6000.0.x; Hub will offer to upgrade the pinned version if yours differs).
2. Unity Hub → **Add project from disk** → pick this folder. The first open imports everything and generates the `.meta` files (commit them afterwards; Unity expects them in version control).
3. Open any scene (or just the default empty one) and press **Play**. There is no scene to wire up: `Bootstrap.cs` builds the camera, arena, player and HUD at runtime.

| Key | Action |
|---|---|
| WASD / arrow keys | Move |
| P / Esc | Pause (also pauses when the window loses focus) |
| R / Space / Enter | Restart after game over |

**Rules.** An orb is worth 10 points times the combo multiplier (1 to 5, one step per orb collected within two seconds of the previous). A missed orb breaks the combo. Every 8 orbs is a level: bonus points, faster and more numerous hazards, and every third level gives a life back (max 5). A hazard hit costs a life, resets the combo and gives 1.5 s of invulnerability. Best score is stored in `PlayerPrefs`.

## Layout

```
Assets/Scripts/Core/      engine-free rules: GameState, Difficulty, SpawnDirector, Motion (move + hit test), HighScore, Rng, Vec2
Assets/Scripts/           Unity layer: Bootstrap, GameController, InputReader, SpriteFactory, Sfx
Assets/Editor/            BuildScript (menu "Orb Rush" + batch-mode entry points)
Tests/LogicTests/         console test runner that compiles Core without Unity (dotnet run)
.github/workflows/        ci.yml (runs the logic tests), build-unity.yml (reference, needs a Unity license)
```

Design choices worth knowing:
- **Rules separate from engine.** The Unity layer only moves sprites and asks `Core` what happened. That is what makes the part that matters testable without an editor.
- **No assets.** Sprites are generated white shapes tinted at runtime; sounds are synthesised beeps. Nothing to import, nothing binary in git.
- **No hand-written scene.** Scene files are YAML that is easy to get subtly wrong without the editor, so the scene is empty and the game builds itself at start-up. `Orb Rush → Create Scene` makes the empty `Main.unity` for builds.
- **Collision by distance checks**, not the physics engine: circles only, deterministic, and the same function the tests exercise.
- **Own RNG** (xorshift) so spawn sequences are identical on every platform and the spawn tests are stable.
- **Either input system.** `InputReader` compiles for the old Input Manager, the new Input System, or both.

## Run the tests

```
dotnet run --project Tests/LogicTests -c Release     # needs the .NET 8 SDK
```

It prints `52 passed, 0 failed` and exits non-zero on any failure. CI runs the same command. (Locally, with no .NET SDK, I compiled the same sources with the old .NET Framework C# 5 compiler, which is why `Core` and the tests avoid newer C# syntax.)

## Build and deploy

- **In the editor:** menu **Orb Rush → Build WebGL** (output `Builds/WebGL`, compression disabled so any static host works) or **Build Windows**.
- **Batch mode:** `Unity -batchmode -quit -projectPath . -executeMethod OrbRush.Editor.BuildScript.BuildWebGL`.
- **GitHub:** this repo is the source. `build-unity.yml` is a manual (`workflow_dispatch`) game-ci pipeline that needs `UNITY_LICENSE`, `UNITY_EMAIL` and `UNITY_PASSWORD` secrets ([how](https://game.ci/docs/github/activation)). I have not run it. Hosting a playable WebGL build on GitHub Pages is a follow-up: build, then publish the `Builds/WebGL` folder.

## Steps taken

1. **Checked the machine.** No Unity Hub or editor, no .NET SDK, 280 GB free; the only compiler available is the old .NET Framework `csc` (C# 5). That decided the approach: keep the rules engine-free so they can be compiled and tested anyway.
2. **Chose the game and the split.** A small arcade game with a score/combo/level loop gives enough rules to test. Rules go in `Core`; the Unity layer stays thin.
3. **Wrote `Core` in C# 5 syntax** (`Vec2`, `Rng`, `Difficulty`, `GameState`, `HighScore`, `PlayerMotion`/`Hit`, `SpawnDirector`).
4. **Wrote the test runner** (dependency-free, since NUnit and the Unity test framework need packages I could not restore here) and ran it with `csc`: 51 passed.
5. **Mutation-checked the tests**: 16 deliberate breaks, one script that reports "pattern missed" instead of silently passing. One survived (the RNG's all-zero-state guard, reachable by exactly one seed). I solved for that seed with a modular inverse, added a test, and all 16 were then caught. Final count: 52.
6. **Wrote the Unity layer**: runtime bootstrap, generated sprites and sounds, distance-based collisions, IMGUI HUD, pause/restart, best score, and the dual input path.
7. **Compiled the Unity layer against stubs** with four combinations of `ENABLE_LEGACY_INPUT_MANAGER` / `ENABLE_INPUT_SYSTEM`. All compiled. (Caveat above: this proves my code is self-consistent, not that the Unity API usage is right.)
8. **Wrote the editor build script and workflows**, and checked that the action tags used (`checkout@v7`, `setup-dotnet@v6`, `cache@v6`, `upload-artifact@v7`, `game-ci/unity-builder@v6`) exist.
9. **Hit and fixed along the way:** Git Bash rewrote `csc`'s `/out:` flags as paths (switched to PowerShell and `-flag` style); `.gitignore`'s usual `*.csproj` rule would have excluded the test project (anchored to the project root); game-ci passes its own output folder, so the build script now honours `-customBuildPath`.
10. **Pushed to GitHub** and checked CI: the logic-test job (`dotnet run` on .NET 8, warnings as errors) passed on the first run. That run covers only `Core` and its tests, not the Unity scripts.

## Known gaps

- Never run in Unity: look, feel, difficulty tuning (speeds, spawn rates, the 6 u/s player speed) are guesses.
- No `.meta` files yet, no committed scene, no ProjectSettings beyond the version file; Unity fills these in on first open.
- Keyboard only: no touch or gamepad input.
- Hazards are aimed at roughly where the player is when they spawn and then fly straight, so a moving player can probably sidestep them and difficulty comes mostly from count and speed. I have not played it, so I do not know whether that is too easy. The only simulation is that a player who never moves does lose.

## License

MIT
