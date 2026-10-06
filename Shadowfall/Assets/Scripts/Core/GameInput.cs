using UnityEngine;
#if !ENABLE_LEGACY_INPUT_MANAGER && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Shadowfall
{
    public enum GKey { Alpha1, Alpha2, Alpha3, Alpha4, Alpha5, Q, E, I, B, C, K, L, M, H, Escape, Shift, Alt, Space, F1, W, A, S, D, Left, Right, Up, Down, Backspace, T }

    /// <summary>
    /// Thin input wrapper so the project works with either the legacy Input Manager
    /// or the new Input System package (whichever "Active Input Handling" is set to).
    /// </summary>
    public static class GameInput
    {
#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
        public static Vector2 MousePosition => Input.mousePosition;
        public static bool LeftDown => Input.GetMouseButtonDown(0);
        public static bool LeftHeld => Input.GetMouseButton(0);
        public static bool RightDown => Input.GetMouseButtonDown(1);
        public static bool RightHeld => Input.GetMouseButton(1);
        public static bool MiddleHeld => Input.GetMouseButton(2);
        public static float Scroll => Input.mouseScrollDelta.y;

        public static bool Down(GKey k)
        {
            switch (k)
            {
                case GKey.Shift: return Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift);
                case GKey.Alt: return Input.GetKeyDown(KeyCode.LeftAlt) || Input.GetKeyDown(KeyCode.RightAlt);
                default: return Input.GetKeyDown(Map(k));
            }
        }

        public static bool Held(GKey k)
        {
            switch (k)
            {
                case GKey.Shift: return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                case GKey.Alt: return Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
                default: return Input.GetKey(Map(k));
            }
        }

        static KeyCode Map(GKey k)
        {
            switch (k)
            {
                case GKey.Alpha1: return KeyCode.Alpha1;
                case GKey.Alpha2: return KeyCode.Alpha2;
                case GKey.Alpha3: return KeyCode.Alpha3;
                case GKey.Alpha4: return KeyCode.Alpha4;
                case GKey.Alpha5: return KeyCode.Alpha5;
                case GKey.Q: return KeyCode.Q;
                case GKey.E: return KeyCode.E;
                case GKey.I: return KeyCode.I;
                case GKey.B: return KeyCode.B;
                case GKey.C: return KeyCode.C;
                case GKey.K: return KeyCode.K;
                case GKey.L: return KeyCode.L;
                case GKey.M: return KeyCode.M;
                case GKey.H: return KeyCode.H;
                case GKey.Escape: return KeyCode.Escape;
                case GKey.Space: return KeyCode.Space;
                case GKey.F1: return KeyCode.F1;
                case GKey.W: return KeyCode.W;
                case GKey.A: return KeyCode.A;
                case GKey.S: return KeyCode.S;
                case GKey.D: return KeyCode.D;
                case GKey.Left: return KeyCode.LeftArrow;
                case GKey.Right: return KeyCode.RightArrow;
                case GKey.Up: return KeyCode.UpArrow;
                case GKey.Down: return KeyCode.DownArrow;
                case GKey.Backspace: return KeyCode.Backspace;
                case GKey.T: return KeyCode.T;
                default: return KeyCode.None;
            }
        }
#else
        public static Vector2 MousePosition => Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        public static bool LeftDown => Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        public static bool LeftHeld => Mouse.current != null && Mouse.current.leftButton.isPressed;
        public static bool RightDown => Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
        public static bool RightHeld => Mouse.current != null && Mouse.current.rightButton.isPressed;
        public static bool MiddleHeld => Mouse.current != null && Mouse.current.middleButton.isPressed;
        public static float Scroll
        {
            get
            {
                if (Mouse.current == null) return 0f;
                float y = Mouse.current.scroll.ReadValue().y;
                return Mathf.Approximately(y, 0f) ? 0f : Mathf.Sign(y);
            }
        }

        public static bool Down(GKey k)
        {
            var kb = Keyboard.current;
            if (kb == null) return false;
            switch (k)
            {
                case GKey.Shift: return kb.leftShiftKey.wasPressedThisFrame || kb.rightShiftKey.wasPressedThisFrame;
                case GKey.Alt: return kb.leftAltKey.wasPressedThisFrame || kb.rightAltKey.wasPressedThisFrame;
                default: return kb[Map(k)].wasPressedThisFrame;
            }
        }

        public static bool Held(GKey k)
        {
            var kb = Keyboard.current;
            if (kb == null) return false;
            switch (k)
            {
                case GKey.Shift: return kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
                case GKey.Alt: return kb.leftAltKey.isPressed || kb.rightAltKey.isPressed;
                default: return kb[Map(k)].isPressed;
            }
        }

        static Key Map(GKey k)
        {
            switch (k)
            {
                case GKey.Alpha1: return Key.Digit1;
                case GKey.Alpha2: return Key.Digit2;
                case GKey.Alpha3: return Key.Digit3;
                case GKey.Alpha4: return Key.Digit4;
                case GKey.Alpha5: return Key.Digit5;
                case GKey.Q: return Key.Q;
                case GKey.E: return Key.E;
                case GKey.I: return Key.I;
                case GKey.B: return Key.B;
                case GKey.C: return Key.C;
                case GKey.K: return Key.K;
                case GKey.L: return Key.L;
                case GKey.M: return Key.M;
                case GKey.H: return Key.H;
                case GKey.Escape: return Key.Escape;
                case GKey.Space: return Key.Space;
                case GKey.F1: return Key.F1;
                case GKey.W: return Key.W;
                case GKey.A: return Key.A;
                case GKey.S: return Key.S;
                case GKey.D: return Key.D;
                case GKey.Left: return Key.LeftArrow;
                case GKey.Right: return Key.RightArrow;
                case GKey.Up: return Key.UpArrow;
                case GKey.Down: return Key.DownArrow;
                case GKey.Backspace: return Key.Backspace;
                case GKey.T: return Key.T;
                default: return Key.None;
            }
        }
#endif
    }
}
