using TMPro;
using UnityEngine;

namespace Abyss.UI
{
    public class MainMenuScreen : MenuPanel
    {
        public override UIScreen Screen => UIScreen.MainMenu;
        protected override string Title => "ABYSS";
        protected override Vector2 Size => new Vector2(760, 980);

        protected override void Build(Transform root)
        {
            UIFactory.Text(root, "An underwater exhibition", UITheme.BodySize, ThemeRole.Accent, FontStyles.Italic);
            UIFactory.Spacer(root, 12);
            UIFactory.Button(root, "Start exploring", () => Experience?.StartExploration(), primary: true);
            UIFactory.Button(root, "Guided activities", () => UI?.Show(UIScreen.ActivityPicker));
            UIFactory.Button(root, "Exhibition mode (kiosk)", () => Experience?.StartExhibitionKiosk());
            UIFactory.Button(root, "Settings", () => UI?.Show(UIScreen.Settings));
            UIFactory.Button(root, "Help", () => UI?.Show(UIScreen.Help));
            UIFactory.Button(root, "Exit", () => Experience?.ExitApp());
        }
    }
}
