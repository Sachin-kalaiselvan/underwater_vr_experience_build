using Abyss.Activities;
using Abyss.Core;
using UnityEngine;

namespace Abyss.UI
{
    public class ActivityPickerScreen : MenuPanel
    {
        public override UIScreen Screen => UIScreen.ActivityPicker;
        protected override string Title => "Guided activities";

        protected override void Build(Transform root)
        {
            var gm = Services.Get<GuidedActivityManager>();
            var progress = Services.Get<ProgressTracker>();
            if (gm == null || gm.Catalog == null || gm.Catalog.Activities.Count == 0)
            {
                Body(root, "No activities are set up yet.");
            }
            else
            {
                Body(root, "Pick one. You can stop any time from the menu button.");
                foreach (var a in gm.Catalog.Activities)
                {
                    if (a == null) continue;
                    bool done = progress != null && progress.Store.IsCompleted(a.Id);
                    var label = $"{a.Title}  <size=75%>· {a.Minutes} min{(done ? "  ✓" : "")}</size>";
                    var def = a;
                    UIFactory.Button(root, label, () => Experience?.StartGuided(def));
                }
            }
            UIFactory.Spacer(root, 8);
            UIFactory.Button(root, "Back", () => UI?.Back());
        }
    }
}
