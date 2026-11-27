using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Abyss.UI
{
    // text size follows the accessibility text scale setting
    [DisallowMultipleComponent]
    public class ScaledText : MonoBehaviour
    {
        public float BaseSize = UITheme.BodySize;
        TMP_Text _t;

        void Awake() => _t = GetComponent<TMP_Text>();
        void OnEnable() { UITheme.ThemeChanged += Apply; Apply(); }
        void OnDisable() => UITheme.ThemeChanged -= Apply;

        void Apply()
        {
            if (_t == null) _t = GetComponent<TMP_Text>();
            if (_t != null) _t.fontSize = BaseSize * UITheme.TextScale;
        }
    }
}
