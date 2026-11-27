using Abyss.Core;
using TMPro;
using UnityEngine;

namespace Abyss.UI
{
    // a screen that rebuilds its contents every time it opens, so values (settings, progress)
    // are always current without wiring up a binding system
    public abstract class MenuPanel : WorldPanel
    {
        protected virtual Vector2 Size => new Vector2(820, 900);
        protected abstract string Title { get; }
        public abstract UIScreen Screen { get; }

        protected ExperienceManager Experience => Services.Get<ExperienceManager>();
        protected UIManager UI => Services.Get<UIManager>();

        void Awake() => Init(GetType().Name, Size);

        public override void Open(bool placeInFront = true)
        {
            Rebuild();
            base.Open(placeInFront);
        }

        public void Rebuild()
        {
            ClearContent();
            UIFactory.Text(Content, Title, UITheme.TitleSize, ThemeRole.Text, FontStyles.Bold);
            Build(Content);
        }

        protected abstract void Build(Transform root);

        protected TextMeshProUGUI Body(Transform root, string text) => UIFactory.Text(root, text, UITheme.BodySize, ThemeRole.TextDim);
    }
}
