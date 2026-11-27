using UnityEngine;

namespace Abyss.UI
{
    // one place for the look. restrained on purpose: deep navy glass, warm off-white text,
    // muted sea-glass accent. no neon. high contrast swaps to black/white/yellow
    public static class UITheme
    {
        public struct Palette
        {
            public Color Panel, PanelEdge, Text, TextDim, Accent, AccentText, Button, ButtonHover, ButtonPress, Positive, Neutral, Track;
        }

        public static readonly Palette Normal = new Palette
        {
            Panel = new Color32(9, 27, 40, 230),
            PanelEdge = new Color32(140, 190, 196, 70),
            Text = new Color32(236, 242, 240, 255),
            TextDim = new Color32(170, 192, 196, 255),
            Accent = new Color32(132, 190, 182, 255),
            AccentText = new Color32(8, 24, 32, 255),
            Button = new Color32(22, 52, 66, 255),
            ButtonHover = new Color32(38, 82, 96, 255),
            ButtonPress = new Color32(132, 190, 182, 255),
            Positive = new Color32(222, 204, 156, 255), // warm sand, reads well on blue
            Neutral = new Color32(190, 200, 205, 255),
            Track = new Color32(255, 255, 255, 40),
        };

        public static readonly Palette HighContrast = new Palette
        {
            Panel = new Color32(0, 0, 0, 250),
            PanelEdge = new Color32(255, 255, 255, 255),
            Text = Color.white,
            TextDim = new Color32(235, 235, 235, 255),
            Accent = new Color32(255, 214, 0, 255),
            AccentText = Color.black,
            Button = new Color32(30, 30, 30, 255),
            ButtonHover = new Color32(70, 70, 70, 255),
            ButtonPress = new Color32(255, 214, 0, 255),
            Positive = new Color32(255, 214, 0, 255),
            Neutral = Color.white,
            Track = new Color32(255, 255, 255, 90),
        };

        public static bool UseHighContrast;
        public static float TextScale = 1f;
        public static Palette Current => UseHighContrast ? HighContrast : Normal;

        // sizes in canvas units. canvases are 1 unit = 1 mm, so 40 = 4 cm tall caps at ~1.3 m away
        public const float TitleSize = 52f;
        public const float HeadingSize = 40f;
        public const float BodySize = 32f;
        public const float SmallSize = 26f;
        public const float ButtonHeight = 96f;
        public const float Corner = 24f;

        public const float PanelDistance = 1.35f;   // metres from the eyes
        public const float CanvasScale = 0.001f;    // 1 canvas unit = 1 mm

        public static event System.Action ThemeChanged;
        public static void NotifyChanged() => ThemeChanged?.Invoke();
    }
}
