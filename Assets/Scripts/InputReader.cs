using UnityEngine;
using OrbRush.Core;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

namespace OrbRush
{
    /// <summary>
    /// One place that reads the keyboard, so the game works whichever Active Input Handling the
    /// project uses (Input Manager (Old), Input System Package (New) or Both).
    /// </summary>
    public static class InputReader
    {
        public static Vec2 Move()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return new Vec2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
#elif ENABLE_INPUT_SYSTEM
            Keyboard k = Keyboard.current;
            if (k == null) return Vec2.Zero;
            float x = 0f, y = 0f;
            if (k.aKey.isPressed || k.leftArrowKey.isPressed) x -= 1f;
            if (k.dKey.isPressed || k.rightArrowKey.isPressed) x += 1f;
            if (k.sKey.isPressed || k.downArrowKey.isPressed) y -= 1f;
            if (k.wKey.isPressed || k.upArrowKey.isPressed) y += 1f;
            return new Vec2(x, y);
#else
            return Vec2.Zero;
#endif
        }

        public static bool RestartPressed()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return);
#elif ENABLE_INPUT_SYSTEM
            Keyboard k = Keyboard.current;
            return k != null && (k.rKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame);
#else
            return false;
#endif
        }

        public static bool PausePressed()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.P) || Input.GetKeyDown(KeyCode.Escape);
#elif ENABLE_INPUT_SYSTEM
            Keyboard k = Keyboard.current;
            return k != null && (k.pKey.wasPressedThisFrame || k.escapeKey.wasPressedThisFrame);
#else
            return false;
#endif
        }
    }
}
