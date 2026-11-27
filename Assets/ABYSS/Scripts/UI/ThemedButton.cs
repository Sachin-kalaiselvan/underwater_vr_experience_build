using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Abyss.UI
{
    [DisallowMultipleComponent]
    public class ThemedButton : MonoBehaviour
    {
        public bool Primary;
        Button _b;

        void Awake() => _b = GetComponent<Button>();
        void OnEnable() { UITheme.ThemeChanged += Apply; Apply(); }
        void OnDisable() => UITheme.ThemeChanged -= Apply;

        void Apply()
        {
            if (_b == null) _b = GetComponent<Button>();
            if (_b == null) return;
            var p = UITheme.Current;
            var c = _b.colors;
            c.normalColor = Primary ? p.Accent : p.Button;
            c.highlightedColor = Primary ? Color.Lerp(p.Accent, Color.white, 0.25f) : p.ButtonHover;
            c.selectedColor = c.normalColor;
            c.pressedColor = p.ButtonPress;
            c.disabledColor = new Color(p.Button.r, p.Button.g, p.Button.b, 0.4f);
            c.colorMultiplier = 1f;
            c.fadeDuration = 0.08f;
            _b.colors = c;
            if (_b.targetGraphic != null) _b.targetGraphic.color = Color.white;
            var label = GetComponentInChildren<ThemedGraphic>();
            if (label != null) { label.Role = Primary ? ThemeRole.AccentText : ThemeRole.Text; label.Apply(); }
        }
    }
}
