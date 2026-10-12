using UnityEngine;

namespace JungleEscape
{
    /// <summary>
    /// All screens (menu, HUD, pause, game over) drawn with Unity's immediate-mode
    /// GUI so no Canvas, prefabs or fonts need to be set up. Designed for a
    /// 1080x1920 portrait layout and scaled to any screen.
    /// Styling: serif display type, rounded translucent panels with gold hairlines.
    /// </summary>
    public class GameUI : MonoBehaviour
    {
        const float RefW = 1080f, RefH = 1920f;

        GUIStyle title, big, mid, small, hudLeft, hudRight, button, smallButton, warn, panel, bar, barFill, barGold;
        Texture2D scrimTop, scrimBottom, dimTex;
        bool ready;

        static readonly Color Gold = new Color(1f, 0.83f, 0.42f);
        static readonly Color Cream = new Color(1f, 0.96f, 0.86f);
        static readonly Color Leaf = new Color(0.45f, 0.78f, 0.38f);

        void OnGUI()
        {
            var gm = GameManager.I;
            if (gm == null) return;
            if (!ready) Init();

            float s = Mathf.Min(Screen.width / RefW, Screen.height / RefH);
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
            float w = Screen.width / s, h = Screen.height / s;

            switch (gm.State)
            {
                case GameState.Menu: DrawMenu(gm, w, h); break;
                case GameState.Playing: DrawHud(gm, w, h); break;
                case GameState.Paused: DrawHud(gm, w, h); DrawPause(gm, w, h); break;
                case GameState.GameOver: DrawGameOver(gm, w, h); break;
            }
            if (gm.State == GameState.Playing || gm.State == GameState.Paused) DrawAnnouncement(w, h);
        }

        // ---------------- Developer world picker ----------------

        static readonly (BiomeType world, string label)[] Worlds =
        {
            (BiomeType.Jungle, "Jungle"), (BiomeType.IceAge, "Ice Age"),
            (BiomeType.WaterWorld, "Water"), (BiomeType.Desert, "Desert")
        };

        GUIStyle worldButton, worldButtonOn;

        void DrawWorldPicker(GameManager gm, float w, float y)
        {
            if (worldButton == null)
            {
                worldButton = new GUIStyle(smallButton) { fontSize = 34 };
                worldButtonOn = new GUIStyle(worldButton);
                worldButtonOn.normal.background = button.normal.background;
                worldButtonOn.normal.textColor = Gold;
            }
            Shadow(new Rect(0, y, w, 50), "TEST WORLD  (or keys 1-4 while playing)", small, new Color(1f, 1f, 1f, 0.75f));
            const float bw = 230f, gap = 14f;
            float x = w / 2f - (Worlds.Length * bw + (Worlds.Length - 1) * gap) / 2f;
            foreach (var (world, label) in Worlds)
            {
                var style = Biomes.StartWorld == world ? worldButtonOn : worldButton;
                if (GUI.Button(new Rect(x, y + 55f, bw, 90f), label, style) && Biomes.StartWorld != world)
                    gm.PreviewWorld(world);
                x += bw + gap;
            }
        }

        // ---------------- New-world announcement ----------------

        static string announceTitle;
        static Color announceColor;
        static float announceStart = -100f;

        /// <summary>Flash the screen and show the name of the world the runner just entered.</summary>
        public static void Announce(string title, Color color)
        {
            announceTitle = title;
            announceColor = color;
            announceStart = Time.unscaledTime;
        }

        void DrawAnnouncement(float w, float h)
        {
            float t = Time.unscaledTime - announceStart;
            if (t > 3.2f || announceTitle == null) return;

            // Bright flash that fades out as the new world appears.
            float flash = Mathf.Clamp01(1f - t / 0.9f);
            if (flash > 0f)
            {
                var c = GUI.color;
                GUI.color = new Color(Mathf.Lerp(announceColor.r, 1f, 0.5f), Mathf.Lerp(announceColor.g, 1f, 0.5f), Mathf.Lerp(announceColor.b, 1f, 0.5f), flash * flash * 0.85f);
                GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
                GUI.color = c;
            }

            // Title card
            float a = Mathf.Clamp01(t / 0.35f) * Mathf.Clamp01((3.2f - t) / 0.6f);
            float rise = (1f - Mathf.Clamp01(t / 0.6f)) * 40f;
            Shadow(new Rect(0, h * 0.24f + rise, w, 70), "You entered", mid, new Color(1f, 1f, 1f, 0.9f * a));
            Shadow(new Rect(0, h * 0.24f + 70 + rise, w, 150), announceTitle, big,
                   new Color(Mathf.Lerp(announceColor.r, 1f, 0.35f), Mathf.Lerp(announceColor.g, 1f, 0.35f), Mathf.Lerp(announceColor.b, 1f, 0.35f), a));
            Ornament(w / 2, h * 0.24f + 240 + rise, 360);
            Shadow(new Rect(0, h * 0.24f + 255 + rise, w, 60), "+500 explorer bonus", small, new Color(Gold.r, Gold.g, Gold.b, a));
        }

        // ---------------- Screens ----------------

        void DrawMenu(GameManager gm, float w, float h)
        {
            Scrims(w, h, 0.75f);
            Shadow(new Rect(0, h * 0.09f, w, 300), "JUNGLE\nESCAPE", title);
            Ornament(w / 2, h * 0.09f + 320, 360);
            Shadow(new Rect(0, h * 0.09f + 345, w, 80), "Outrun the monkey troop", mid, Cream);

            var stats = new Rect(w / 2 - 330, h * 0.40f, 660, 190);
            GUI.Box(stats, GUIContent.none, panel);
            Shadow(new Rect(stats.x, stats.y + 18, stats.width, 70), $"Best   {gm.HighScore:N0}", mid, Cream);
            Shadow(new Rect(stats.x, stats.y + 98, stats.width, 70), $"Treasure   {gm.TotalBananas:N0}", mid, Gold);

            if (GUI.Button(new Rect(w / 2 - 300, h * 0.60f, 600, 170), "RUN", button)) gm.StartRun();

            // Developer world picker (editor & development builds only): choose the world a run starts in.
            if (Debug.isDebugBuild) DrawWorldPicker(gm, w, h * 0.60f + 195f);

            Shadow(new Rect(40, h - 330, w - 80, 300),
                "Swipe left / right to change lane\nSwipe up to jump logs  ·  Swipe down to roll under branches\n" +
                "Keyboard: arrows or WASD", small, new Color(1f, 1f, 1f, 0.85f));
        }

        void DrawHud(GameManager gm, float w, float h)
        {
            float top = 40f;

            // This world's money (left)
            GUI.Box(new Rect(20, top, 380, 135), GUIContent.none, panel);
            Shadow(new Rect(48, top - 6, 340, 100), gm.RunBananas.ToString("N0"), hudLeft, Gold);
            Shadow(new Rect(48, top + 76, 340, 50), Pickup.CurrencyName(Biomes.Current), small, new Color(1f, 1f, 1f, 0.85f), TextAnchor.MiddleLeft);

            // Score + multiplier (right)
            GUI.Box(new Rect(w - 400, top, 380, 175), GUIContent.none, panel);
            Shadow(new Rect(w - 400, top + 2, 352, 100), Mathf.FloorToInt(gm.Score).ToString("N0"), hudRight, Cream);
            Shadow(new Rect(w - 400, top + 85, 352, 60), $"x{gm.Multiplier} streak", small,
                   gm.Multiplier > 1 ? Gold : new Color(1f, 1f, 1f, 0.8f), TextAnchor.MiddleRight);
            if (gm.Multiplier < GameManager.MaxMultiplier)
                Bar(new Rect(w - 372, top + 147, 324, 12), gm.StreakProgress, barGold);

            // Pause
            if (gm.State == GameState.Playing &&
                GUI.Button(new Rect(w / 2 - 60, top, 120, 100), "II", smallButton)) gm.Pause();

            // Active power-ups
            float y = top + 160;
            y = PowerBar(gm, PickupType.Magnet, "Vine Magnet", y);
            y = PowerBar(gm, PickupType.Glider, "Leaf Glider", y);
            y = PowerBar(gm, PickupType.Shield, "Coconut Helmet", y);
            PowerBar(gm, PickupType.Bonanza, "Golden Idol  x2", y);

            if (gm.Monkeys.IsClose && gm.RunTime > 2.5f)
            {
                float a = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 10f);
                Shadow(new Rect(0, h * 0.30f, w, 90), "The monkeys are right behind you!", warn, new Color(1f, 0.42f, 0.28f, a));
            }

            if (gm.RunTime < 3.5f)
            {
                float a = Mathf.Clamp01((3.5f - gm.RunTime) * 1.5f);
                Shadow(new Rect(0, h * 0.70f, w, 90), $"Swipe to dodge. Grab the {Pickup.CurrencyName(Biomes.Current)}!", mid, new Color(Cream.r, Cream.g, Cream.b, a));
            }
        }

        float PowerBar(GameManager gm, PickupType t, string label, float y)
        {
            if (!gm.Player.HasPower(t)) return y;
            GUI.Box(new Rect(20, y, 380, 92), GUIContent.none, panel);
            Shadow(new Rect(46, y + 6, 340, 50), label, small, Cream, TextAnchor.MiddleLeft);
            Bar(new Rect(46, y + 60, 328, 12), gm.Player.PowerFraction(t), barFill);
            return y + 102;
        }

        void DrawPause(GameManager gm, float w, float h)
        {
            GUI.DrawTexture(new Rect(0, 0, w, h), dimTex);
            Shadow(new Rect(0, h * 0.27f, w, 150), "Paused", big);
            Ornament(w / 2, h * 0.27f + 160, 260);
            if (GUI.Button(new Rect(w / 2 - 300, h * 0.45f, 600, 150), "Resume", button)) gm.Resume();
            if (GUI.Button(new Rect(w / 2 - 300, h * 0.45f + 190, 600, 150), "Home", button)) gm.GoToMenu();
        }

        void DrawGameOver(GameManager gm, float w, float h)
        {
            GUI.DrawTexture(new Rect(0, 0, w, h), dimTex);
            Shadow(new Rect(0, h * 0.11f, w, 150), "Caught!", big);
            Shadow(new Rect(0, h * 0.11f + 150, w, 80), gm.GameOverReason, mid, Cream);

            var card = new Rect(w / 2 - 400, h * 0.285f, 800, 360);
            GUI.Box(card, GUIContent.none, panel);
            float y = card.y + 20;
            Shadow(new Rect(0, y, w, 100), $"{Mathf.FloorToInt(gm.Score):N0}", big, Cream);
            Shadow(new Rect(0, y + 105, w, 70), gm.NewHighScore ? "New best!" : $"Best  {gm.HighScore:N0}", mid,
                   gm.NewHighScore ? Gold : new Color(1f, 1f, 1f, 0.85f));
            Ornament(w / 2, y + 190, 300);
            Shadow(new Rect(0, y + 205, w, 60), $"Treasure this run  {gm.RunBananas}   ·   total {gm.TotalBananas}", small, Gold);
            Shadow(new Rect(0, y + 265, w, 60), $"Distance  {Mathf.FloorToInt(gm.Distance)} m", small, Cream);

            float by = h * 0.55f;
            bool prev = GUI.enabled;
            GUI.enabled = gm.CanRevive;
            if (GUI.Button(new Rect(w / 2 - 330, by, 660, 150), $"Revive  ·  {gm.ReviveCost} treasure", button)) gm.Revive();
            GUI.enabled = prev;
            if (GUI.Button(new Rect(w / 2 - 330, by + 180, 660, 150), "Play again", button)) gm.StartRun();
            if (GUI.Button(new Rect(w / 2 - 330, by + 360, 660, 150), "Home", button)) gm.GoToMenu();
        }

        // ---------------- Helpers ----------------

        void Scrims(float w, float h, float strength)
        {
            var c = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, strength);
            GUI.DrawTexture(new Rect(0, 0, w, h * 0.5f), scrimTop);
            GUI.DrawTexture(new Rect(0, h * 0.6f, w, h * 0.4f), scrimBottom);
            GUI.color = c;
        }

        /// <summary>Thin gold rule with a diamond in the middle.</summary>
        void Ornament(float cx, float y, float width)
        {
            var c = GUI.color;
            GUI.color = new Color(Gold.r, Gold.g, Gold.b, 0.8f);
            GUI.DrawTexture(new Rect(cx - width / 2, y, width / 2 - 18, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 18, y, width / 2 - 18, 2), Texture2D.whiteTexture);
            var m = GUI.matrix;
            GUIUtility.RotateAroundPivot(45f, new Vector2(cx, y + 1));
            GUI.DrawTexture(new Rect(cx - 7, y - 6, 14, 14), Texture2D.whiteTexture);
            GUI.matrix = m;
            GUI.color = c;
        }

        void Bar(Rect r, float fraction, GUIStyle fill)
        {
            GUI.Box(r, GUIContent.none, bar);
            if (fraction > 0.01f)
                GUI.Box(new Rect(r.x, r.y, Mathf.Max(r.height, r.width * Mathf.Clamp01(fraction)), r.height), GUIContent.none, fill);
        }

        void Shadow(Rect r, string text, GUIStyle style) => Shadow(r, text, style, style.normal.textColor);

        void Shadow(Rect r, string text, GUIStyle style, Color color, TextAnchor? align = null)
        {
            var oldAlign = style.alignment;
            var oldColor = style.normal.textColor;
            if (align.HasValue) style.alignment = align.Value;

            // Soft two-step shadow reads better on busy foliage than a single hard offset.
            style.normal.textColor = new Color(0f, 0.02f, 0f, 0.28f * color.a);
            GUI.Label(new Rect(r.x + 5, r.y + 6, r.width, r.height), text, style);
            style.normal.textColor = new Color(0f, 0.02f, 0f, 0.45f * color.a);
            GUI.Label(new Rect(r.x + 2, r.y + 3, r.width, r.height), text, style);
            style.normal.textColor = color;
            GUI.Label(r, text, style);

            style.normal.textColor = oldColor;
            style.alignment = oldAlign;
        }

        void Init()
        {
            ready = true;

            Font serif = Font.CreateDynamicFontFromOSFont(
                new[] { "Georgia", "Palatino Linotype", "Book Antiqua", "Times New Roman", "Noto Serif", "Droid Serif", "serif" }, 64);
            Font sans = Font.CreateDynamicFontFromOSFont(
                new[] { "Segoe UI", "Roboto", "Helvetica Neue", "Helvetica", "Arial", "sans-serif" }, 48);

            title = Label(150, FontStyle.Bold, Gold, TextAnchor.MiddleCenter, serif);
            big = Label(96, FontStyle.Bold, Cream, TextAnchor.MiddleCenter, serif);
            mid = Label(50, FontStyle.Normal, Color.white, TextAnchor.MiddleCenter, serif);
            small = Label(38, FontStyle.Normal, Color.white, TextAnchor.MiddleCenter, sans);
            hudLeft = Label(54, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft, serif);
            hudRight = Label(66, FontStyle.Bold, Color.white, TextAnchor.MiddleRight, serif);
            warn = Label(48, FontStyle.Bold, Color.red, TextAnchor.MiddleCenter, sans);

            panel = new GUIStyle { border = new RectOffset(26, 26, 26, 26) };
            panel.normal.background = Rounded(new Color(0.03f, 0.07f, 0.04f, 0.62f), new Color(Gold.r, Gold.g, Gold.b, 0.45f), 2f, false);

            button = new GUIStyle(GUI.skin.button)
            {
                font = serif, fontSize = 64, fontStyle = FontStyle.Bold,
                border = new RectOffset(26, 26, 26, 26), alignment = TextAnchor.MiddleCenter
            };
            button.normal.background = Rounded(new Color(0.12f, 0.32f, 0.16f, 0.94f), Gold, 3f, true);
            button.hover.background = Rounded(new Color(0.16f, 0.40f, 0.20f, 0.96f), Gold, 3f, true);
            button.active.background = Rounded(new Color(0.07f, 0.20f, 0.10f, 0.96f), Gold, 3f, true);
            button.normal.textColor = button.hover.textColor = button.active.textColor = Cream;

            smallButton = new GUIStyle(button) { fontSize = 46 };
            smallButton.normal.background = panel.normal.background;

            bar = new GUIStyle { border = new RectOffset(6, 6, 6, 6) };
            bar.normal.background = Rounded(new Color(1f, 1f, 1f, 0.18f), Color.clear, 0f, false, 6);
            barFill = new GUIStyle(bar);
            barFill.normal.background = Rounded(Leaf, Color.clear, 0f, true, 6);
            barGold = new GUIStyle(bar);
            barGold.normal.background = Rounded(Gold, Color.clear, 0f, true, 6);

            scrimTop = Gradient(new Color(0.01f, 0.04f, 0.02f, 0.85f), new Color(0.01f, 0.04f, 0.02f, 0f));
            scrimBottom = Gradient(new Color(0.01f, 0.04f, 0.02f, 0f), new Color(0.01f, 0.04f, 0.02f, 0.85f));
            dimTex = Solid(new Color(0.01f, 0.04f, 0.02f, 0.62f));
        }

        static GUIStyle Label(int size, FontStyle fs, Color c, TextAnchor a, Font font)
        {
            var st = new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = fs, alignment = a, wordWrap = true, font = font };
            st.normal.textColor = c;
            return st;
        }

        /// <summary>Anti-aliased rounded rectangle (9-sliced by the style border) with an optional outline and sheen.</summary>
        static Texture2D Rounded(Color fill, Color outline, float outlineWidth, bool sheen, int radius = 24)
        {
            int size = radius * 2 + 8;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[size * size];
            float half = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // Signed distance to a rounded square
                    float qx = Mathf.Abs(x + 0.5f - half) - (half - radius - 1f);
                    float qy = Mathf.Abs(y + 0.5f - half) - (half - radius - 1f);
                    float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                    float coverage = Mathf.Clamp01(0.5f - outside);
                    float rim = outlineWidth > 0f ? Mathf.Clamp01(outside + outlineWidth + 0.5f) * outline.a : 0f;
                    float shade = sheen ? Mathf.Lerp(0.82f, 1.18f, y / (float)size) : 1f;
                    var c = new Color(fill.r * shade, fill.g * shade, fill.b * shade);
                    c = Color.Lerp(c, new Color(outline.r, outline.g, outline.b), rim);
                    c.a = Mathf.Lerp(fill.a, 1f, rim) * coverage;
                    px[y * size + x] = c;
                }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        static Texture2D Gradient(Color top, Color bottom)
        {
            var t = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 64; y++) t.SetPixel(0, y, Color.Lerp(bottom, top, y / 63f));
            t.Apply();
            return t;
        }

        static Texture2D Solid(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }
    }
}
