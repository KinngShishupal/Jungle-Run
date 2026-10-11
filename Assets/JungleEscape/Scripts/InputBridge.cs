using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace JungleEscape
{
    public enum SwipeDir { None, Left, Right, Up, Down }

    /// <summary>
    /// Reads swipes (touch or mouse drag) and keyboard arrows/WASD.
    /// Works with both the new Input System and the old Input Manager.
    /// Call Read() once per frame.
    /// </summary>
    public static class InputBridge
    {
        static bool tracking;
        static Vector2 startPos;

        static float Threshold => Mathf.Max(30f, Mathf.Min(Screen.width, Screen.height) * 0.06f);

        public static SwipeDir Read()
        {
            SwipeDir key = ReadKeys();
            if (key != SwipeDir.None) return key;

            ReadPointer(out bool down, out bool held, out Vector2 pos);

            if (down)
            {
                tracking = true;
                startPos = pos;
                return SwipeDir.None;
            }

            if (tracking && held)
            {
                Vector2 delta = pos - startPos;
                if (delta.magnitude >= Threshold)
                {
                    tracking = false; // one swipe per touch, fires as soon as it's clear
                    if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
                        return delta.x > 0 ? SwipeDir.Right : SwipeDir.Left;
                    return delta.y > 0 ? SwipeDir.Up : SwipeDir.Down;
                }
            }

            if (!held) tracking = false;
            return SwipeDir.None;
        }

#if ENABLE_INPUT_SYSTEM
        static SwipeDir ReadKeys()
        {
            var kb = Keyboard.current;
            if (kb == null) return SwipeDir.None;
            if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) return SwipeDir.Left;
            if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) return SwipeDir.Right;
            if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) return SwipeDir.Up;
            if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) return SwipeDir.Down;
            return SwipeDir.None;
        }

        static void ReadPointer(out bool down, out bool held, out Vector2 pos)
        {
            var p = Pointer.current; // mouse or primary touch
            if (p == null) { down = held = false; pos = Vector2.zero; return; }
            down = p.press.wasPressedThisFrame;
            held = p.press.isPressed;
            pos = p.position.ReadValue();
        }

        public static bool BackPressed()
        {
            var kb = Keyboard.current;
            return kb != null && kb.escapeKey.wasPressedThisFrame;
        }
#else
        static SwipeDir ReadKeys()
        {
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) return SwipeDir.Left;
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) return SwipeDir.Right;
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.Space)) return SwipeDir.Up;
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) return SwipeDir.Down;
            return SwipeDir.None;
        }

        static void ReadPointer(out bool down, out bool held, out Vector2 pos)
        {
            // Touches are reported as mouse button 0 by default.
            down = Input.GetMouseButtonDown(0);
            held = Input.GetMouseButton(0);
            pos = Input.mousePosition;
        }

        // Android back button maps to Escape.
        public static bool BackPressed() => Input.GetKeyDown(KeyCode.Escape);
#endif
    }
}
