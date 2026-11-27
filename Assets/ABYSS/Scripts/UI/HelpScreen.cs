using UnityEngine;

namespace Abyss.UI
{
    public class HelpScreen : MenuPanel
    {
        public override UIScreen Screen => UIScreen.Help;
        protected override string Title => "How it works";
        protected override Vector2 Size => new Vector2(900, 1000);

        protected override void Build(Transform root)
        {
            Body(root,
                "<b>Point and select</b>\nAim either controller at something. If it glows, pull the trigger to select it.\n\n" +
                "<b>Move</b>\nPush the left thumbstick forward, aim the arc at the sea floor, let go to jump there.\n\n" +
                "<b>Turn</b>\nFlick the right thumbstick left or right. Or just turn your body.\n\n" +
                "<b>Menu</b>\nPress the menu button on the left controller (the one with three lines) any time.\n\n" +
                "<b>Lost?</b>\nOpen the menu and pick \"Return to the start point\".");
            UIFactory.Spacer(root, 8);
            var row = UIFactory.Row(root).transform;
            UIFactory.Button(row, "Replay intro", () => Experience?.RestartIntro());
            UIFactory.Button(row, "Back", () => UI?.Back(), primary: true);
        }
    }
}
