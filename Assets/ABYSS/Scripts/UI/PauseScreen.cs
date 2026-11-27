using Abyss.Activities;
using Abyss.Core;
using Abyss.XR;
using UnityEngine;

namespace Abyss.UI
{
    public class PauseScreen : MenuPanel
    {
        public override UIScreen Screen => UIScreen.Pause;
        protected override string Title => "Paused";

        protected override void Build(Transform root)
        {
            var gm = Services.Get<GuidedActivityManager>();
            UIFactory.Button(root, "Resume", () => Experience?.Resume(), primary: true);
            UIFactory.Button(root, "Return to the start point", () => Experience?.ReturnToHub());
            UIFactory.Button(root, "Face forward (recenter)", Recenter);
            if (gm != null && gm.IsRunning)
            {
                UIFactory.Button(root, "Restart this activity", () => { Experience?.Resume(); gm.Restart(); });
                UIFactory.Button(root, "Stop activity", () => Experience?.ShowMainMenu());
            }
            UIFactory.Button(root, "Settings", () => UI?.Show(UIScreen.Settings));
            UIFactory.Button(root, "Help", () => UI?.Show(UIScreen.Help));
            if (Experience != null && Experience.KioskMode)
            {
                UIFactory.Button(root, "Reset for next visitor", () => Experience.ResetForNextVisitor());
                UIFactory.Button(root, "Leave kiosk mode", () => { Experience.StopKiosk(); Experience.ShowMainMenu(); });
            }
            else UIFactory.Button(root, "Main menu", () => Experience?.ShowMainMenu());
        }

        void Recenter()
        {
            var cam = Camera.main;
            var xr = Services.Get<XRConfigurationManager>();
            if (cam != null && xr != null && xr.Origin != null)
            {
                // turn the world so wherever you're facing now becomes the panel's forward. position stays
                var here = xr.Origin.transform.position;
                var fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
                var headFlat = cam.transform.position; headFlat.y = here.y;
                xr.RecenterTo(headFlat, fwd);
            }
            Experience?.Resume();
        }
    }
}
