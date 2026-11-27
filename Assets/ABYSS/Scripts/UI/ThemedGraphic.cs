using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Abyss.UI
{
    public enum ThemeRole { Panel, Edge, Text, TextDim, Accent, AccentText, Positive, Neutral, Track }

    // recolours itself when high contrast gets toggled
    [DisallowMultipleComponent]
    public class ThemedGraphic : MonoBehaviour
    {
        public ThemeRole Role;
        Graphic _g;

        void Awake() => _g = GetComponent<Graphic>();
        void OnEnable() { UITheme.ThemeChanged += Apply; Apply(); }
        void OnDisable() => UITheme.ThemeChanged -= Apply;

        public void Apply()
        {
            if (_g == null) _g = GetComponent<Graphic>();
            if (_g == null) return;
            var p = UITheme.Current;
            _g.color = Role switch
            {
                ThemeRole.Panel => p.Panel,
                ThemeRole.Edge => p.PanelEdge,
                ThemeRole.Text => p.Text,
                ThemeRole.TextDim => p.TextDim,
                ThemeRole.Accent => p.Accent,
                ThemeRole.AccentText => p.AccentText,
                ThemeRole.Positive => p.Positive,
                ThemeRole.Neutral => p.Neutral,
                ThemeRole.Track => p.Track,
                _ => p.Text
            };
        }
    }
}
