using System;
using Abyss.Core;
using Abyss.Logic;
using UnityEngine;

namespace Abyss.UI
{
    // everything is a big button that cycles or steps a value. no sliders,
    // they need steady aim which first timers (and plenty of others) don't have
    public class SettingsScreen : MenuPanel
    {
        public override UIScreen Screen => UIScreen.Settings;
        protected override string Title => "Settings";
        protected override Vector2 Size => new Vector2(980, 1240);

        SettingsManager SM => Services.Get<SettingsManager>();

        protected override void Build(Transform root)
        {
            var sm = SM;
            if (sm == null) { Body(root, "Settings unavailable."); return; }
            var s = sm.Current;

            Section(root, "Comfort");
            Toggle(root, "Teleport", s.TeleportEnabled, v => sm.Modify(x => x.TeleportEnabled = v));
            Toggle(root, "Smooth movement", s.SmoothMoveEnabled, v => sm.Modify(x => x.SmoothMoveEnabled = v));
            Cycle(root, "Turning", s.Turn == TurnMode.Snap ? $"Snap {s.SnapAngle:0}°" : "Smooth", () => sm.Modify(x =>
            {
                // snap 30 -> 45 -> 60 -> smooth -> snap 30
                if (x.Turn == TurnMode.Smooth) { x.Turn = TurnMode.Snap; x.SnapAngle = 30; }
                else if (x.SnapAngle < 44) x.SnapAngle = 45;
                else if (x.SnapAngle < 59) x.SnapAngle = 60;
                else x.Turn = TurnMode.Smooth;
            }));
            Toggle(root, "Comfort vignette", s.Vignette, v => sm.Modify(x => x.Vignette = v));
            Toggle(root, "Stay in place (look only)", s.Stationary, v => sm.Modify(x => x.Stationary = v));
            Toggle(root, "Reduced motion", s.ReducedMotion, v => sm.Modify(x => x.ReducedMotion = v));

            Section(root, "Reading");
            Stepper(root, "Text size", $"{s.TextScale * 100:0}%", () => sm.Modify(x => x.TextScale -= 0.1f), () => sm.Modify(x => x.TextScale += 0.1f));
            Toggle(root, "High contrast panels", s.HighContrast, v => sm.Modify(x => x.HighContrast = v));

            Section(root, "Sound");
            Stepper(root, "Master", Pct(s.MasterVolume), () => sm.Modify(x => x.MasterVolume -= 0.1f), () => sm.Modify(x => x.MasterVolume += 0.1f));
            Stepper(root, "Environment", Pct(s.EnvironmentVolume), () => sm.Modify(x => x.EnvironmentVolume -= 0.1f), () => sm.Modify(x => x.EnvironmentVolume += 0.1f));
            Stepper(root, "Interaction", Pct(s.InteractionVolume), () => sm.Modify(x => x.InteractionVolume -= 0.1f), () => sm.Modify(x => x.InteractionVolume += 0.1f));
            Toggle(root, "Mute", s.Muted, v => sm.Modify(x => x.Muted = v));

            Section(root, "Visitor");
            Toggle(root, "Skip intro next time", s.SkipIntroNextTime, v => sm.Modify(x => x.SkipIntroNextTime = v));

            var row = UIFactory.Row(root).transform;
            UIFactory.Button(row, "Reset to defaults", () => { sm.ResetToDefaults(); Rebuild(); });
            UIFactory.Button(row, "Back", () => UI?.Back(), primary: true);
        }

        static string Pct(float v) => $"{Mathf.RoundToInt(v * 100)}%";

        void Section(Transform root, string label)
        {
            UIFactory.Spacer(root, 6);
            UIFactory.Text(root, label.ToUpperInvariant(), UITheme.SmallSize, ThemeRole.Accent, TMPro.FontStyles.Bold);
        }

        void Toggle(Transform root, string label, bool value, Action<bool> set)
        {
            var row = UIFactory.Row(root).transform;
            UIFactory.Text(row, label, UITheme.BodySize, ThemeRole.Text, TMPro.FontStyles.Normal, TMPro.TextAlignmentOptions.Left);
            // On/Off spelled out, not just a colour change
            UIFactory.Button(row, value ? "On" : "Off", () => { set(!value); Rebuild(); }, primary: value, height: 80);
        }

        void Cycle(Transform root, string label, string value, Action next)
        {
            var row = UIFactory.Row(root).transform;
            UIFactory.Text(row, label, UITheme.BodySize, ThemeRole.Text, TMPro.FontStyles.Normal, TMPro.TextAlignmentOptions.Left);
            UIFactory.Button(row, value, () => { next(); Rebuild(); }, height: 80);
        }

        void Stepper(Transform root, string label, string value, Action down, Action up)
        {
            var row = UIFactory.Row(root).transform;
            UIFactory.Text(row, label, UITheme.BodySize, ThemeRole.Text, TMPro.FontStyles.Normal, TMPro.TextAlignmentOptions.Left);
            UIFactory.Button(row, "−", () => { down(); Rebuild(); }, height: 80);
            UIFactory.Text(row, value, UITheme.BodySize, ThemeRole.Text, TMPro.FontStyles.Bold, TMPro.TextAlignmentOptions.Center);
            UIFactory.Button(row, "+", () => { up(); Rebuild(); }, height: 80);
        }
    }
}
