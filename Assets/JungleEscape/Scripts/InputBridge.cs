using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace JungleEscape
{
    public enum SwipeDir { None, Left, Right, Up, Down }

    /// <summary>
    /// Reads touch / mouse drags and keyboard arrows/WASD.
    /// Horizontal input is continuous steering: dragging sideways moves the runner sideways by a matching
    /// distance (a quarter-screen drag is about one lane), so it can go anywhere across the trail.
    /// Vertical swipes jump and roll. Keyboard: tap left/right for a lane-sized step, hold to keep moving.
    /// Works with both the new Input System and the old Input Manager. Call Read() once per frame.
    /// </summary>
    public static class InputBridge
    {
        /// <summary>Metres of sideways movement per full screen-width drag.</summary>
        const float SteerGain = 10f;

        static bool tracking, steering;
        static Vector2 startPos, lastPos;

        /// <summary>Sideways movement requested by dragging this frame, in metres (+ = right).</summary>
        public static float SteerDelta { get; private set; }

        /// <summary>Keyboard left/right held: -1, 0 or +1.</summary>
        public static float SteerHold { get; private set; }

        static float Threshold => Mathf.Max(30f, Mathf.Min(Screen.width, Screen.height) * 0.06f);
        static float SteerThreshold => Mathf.Max(10f, Mathf.Min(Screen.width, Screen.height) * 0.02f);

        public static SwipeDir Read()
        {
            SteerDelta = 0f;
            SteerHold = ReadHold();
            SwipeDir key = ReadKeys();
            if (key != SwipeDir.None) return key;

            ReadPointer(out bool down, out bool held, out Vector2 pos);

            if (down)
            {
                tracking = true;
                steering = false;
                startPos = lastPos = pos;
                return SwipeDir.None;
            }

            if (tracking && held)
            {
                Vector2 delta = pos - startPos;
                if (steering)
                {
                    // Follow the finger: sideways movement in proportion to the drag.
                    SteerDelta = (pos.x - lastPos.x) / Mathf.Max(1f, Screen.width) * SteerGain;
                    lastPos = pos;
                    // A clear upward/downward flick mid-drag still jumps or rolls.
                    Vector2 recent = pos - startPos;
                    if (Mathf.Abs(recent.y) >= Threshold * 1.4f && Mathf.Abs(recent.y) > Mathf.Abs(recent.x) * 1.5f)
                    {
                        tracking = false;
                        return recent.y > 0 ? SwipeDir.Up : SwipeDir.Down;
                    }
                    return SwipeDir.None;
                }
                if (Mathf.Abs(delta.x) >= SteerThreshold && Mathf.Abs(delta.x) > Mathf.Abs(delta.y) * 1.2f)
                {
                    // Sideways drag: start steering, applying the distance already dragged.
                    steering = true;
                    SteerDelta = delta.x / Mathf.Max(1f, Screen.width) * SteerGain;
                    startPos = lastPos = pos;
                    return SwipeDir.None;
                }
                if (Mathf.Abs(delta.y) >= Threshold && Mathf.Abs(delta.y) > Mathf.Abs(delta.x))
                {
                    tracking = false; // one vertical swipe per touch, fires as soon as it's clear
                    return delta.y > 0 ? SwipeDir.Up : SwipeDir.Down;
                }
            }

            if (!held) { tracking = false; steering = false; }
            return SwipeDir.None;
        }

#if ENABLE_INPUT_SYSTEM
        static float ReadHold()
        {
            var kb = Keyboard.current;
            if (kb == null) return 0f;
            float h = 0f;
            if (kb.leftArrowKey.isPressed || kb.aKey.isPressed) h -= 1f;
            if (kb.rightArrowKey.isPressed || kb.dKey.isPressed) h += 1f;
            return h;
        }

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
        static float ReadHold()
        {
            float h = 0f;
            if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) h -= 1f;
            if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) h += 1f;
            return h;
        }

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
