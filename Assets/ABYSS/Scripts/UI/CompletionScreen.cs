using Abyss.Activities;
using Abyss.Core;
using TMPro;
using UnityEngine;

namespace Abyss.UI
{
    public class CompletionScreen : MenuPanel
    {
        public override UIScreen Screen => UIScreen.Completion;
        protected override string Title => "Activity complete";

        protected override void Build(Transform root)
        {
            var def = UI != null ? UI.LastCompleted : null;
            if (def != null)
            {
                UIFactory.Text(root, "✓  " + def.Title, UITheme.HeadingSize, ThemeRole.Positive, FontStyles.Bold);
                if (!string.IsNullOrEmpty(def.Summary)) Body(root, def.Summary);
            }
            var progress = Services.Get<ProgressTracker>();
            var gm = Services.Get<GuidedActivityManager>();
            if (progress != null && gm != null && gm.Catalog != null)
                Body(root, $"{progress.Store.CompletedCount} of {gm.Catalog.Activities.Count} activities done.");

            UIFactory.Spacer(root, 8);
            UIFactory.Button(root, "Keep exploring", () => Experience?.StartExploration(), primary: true);
            if (def != null) UIFactory.Button(root, "Do it again", () => Experience?.StartGuided(def));
            UIFactory.Button(root, "Another activity", () => UI?.Show(UIScreen.ActivityPicker));
            UIFactory.Button(root, "Main menu", () => Experience?.ShowMainMenu());
        }
    }
}
