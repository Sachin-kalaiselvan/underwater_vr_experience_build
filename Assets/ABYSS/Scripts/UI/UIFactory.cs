using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace Abyss.UI
{
    // builds world-space UI from code so every panel has the same structure and look,
    // and nobody has to hand-wire 40 prefabs. themed bits get a ThemedGraphic tag so they recolour live
    public static class UIFactory
    {
        static Sprite _rounded;

        // 9-sliced rounded rect made at runtime, no texture asset needed
        public static Sprite RoundedSprite
        {
            get
            {
                if (_rounded != null) return _rounded;
                const int s = 64; const float r = 24f;
                var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "abyss_rounded" };
                var px = new Color32[s * s];
                for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, r, s - r), cy = Mathf.Clamp(y + 0.5f, r, s - r);
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                    byte a = (byte)(Mathf.Clamp01(r - d + 0.5f) * 255);
                    px[y * s + x] = new Color32(255, 255, 255, a);
                }
                tex.SetPixels32(px);
                tex.Apply(false, true);
                _rounded = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
                return _rounded;
            }
        }

        public static Canvas WorldCanvas(string name, Transform parent, Vector2 sizeMm)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 10;
            go.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 3f; // keeps text crisp at 1mm units
            go.AddComponent<TrackedDeviceGraphicRaycaster>();
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = sizeMm;
            rt.localScale = Vector3.one * UITheme.CanvasScale;
            go.AddComponent<CanvasGroup>();
            return canvas;
        }

        public static Image Panel(Transform parent, string name = "Panel", ThemeRole role = ThemeRole.Panel)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform);
            var img = go.AddComponent<Image>();
            img.sprite = RoundedSprite;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1.2f;
            go.AddComponent<ThemedGraphic>().Role = role;

            // thin edge so the panel separates from busy backgrounds
            var edge = new GameObject("Edge", typeof(RectTransform));
            edge.transform.SetParent(go.transform, false);
            var ert = (RectTransform)edge.transform;
            Stretch(ert);
            ert.offsetMin = new Vector2(-3, -3); ert.offsetMax = new Vector2(3, 3);
            edge.transform.SetAsFirstSibling();
            var ei = edge.AddComponent<Image>();
            ei.sprite = RoundedSprite; ei.type = Image.Type.Sliced; ei.raycastTarget = false;
            edge.AddComponent<ThemedGraphic>().Role = ThemeRole.Edge;
            return img;
        }

        public static VerticalLayoutGroup Column(Transform parent, int pad = 48, float spacing = 20)
        {
            var go = new GameObject("Column", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform);
            var v = go.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(pad, pad, pad, pad);
            v.spacing = spacing;
            v.childControlHeight = true; v.childControlWidth = true;
            v.childForceExpandHeight = false; v.childForceExpandWidth = true;
            return v;
        }

        public static HorizontalLayoutGroup Row(Transform parent, float spacing = 16)
        {
            var go = new GameObject("Row", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.childControlHeight = true; h.childControlWidth = true;
            h.childForceExpandHeight = false; h.childForceExpandWidth = true;
            return h;
        }

        public static TextMeshProUGUI Text(Transform parent, string text, float size, ThemeRole role = ThemeRole.Text, FontStyles style = FontStyles.Normal, TextAlignmentOptions align = TextAlignmentOptions.TopLeft)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontStyle = style;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.raycastTarget = false;
            t.lineSpacing = 8f;
            var st = go.AddComponent<ScaledText>();
            st.BaseSize = size;
            go.AddComponent<ThemedGraphic>().Role = role;
            return t;
        }

        public static Button Button(Transform parent, string label, Action onClick, bool primary = false, float height = UITheme.ButtonHeight)
        {
            var go = new GameObject("Button_" + label, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = RoundedSprite; img.type = Image.Type.Sliced;
            var b = go.AddComponent<Button>();
            b.targetGraphic = img;
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = height; le.preferredHeight = height;
            go.AddComponent<ThemedButton>().Primary = primary;

            var t = Text(go.transform, label, UITheme.BodySize, primary ? ThemeRole.AccentText : ThemeRole.Text, FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch((RectTransform)t.transform);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;

            if (onClick != null) b.onClick.AddListener(() =>
            {
                Audio.AudioManager.PlayCue(Audio.AudioCue.UiClick, go.transform.position);
                onClick();
            });
            return b;
        }

        public static Image ProgressBar(Transform parent, out Image fill, float height = 14f)
        {
            var go = new GameObject("Progress", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var track = go.AddComponent<Image>();
            track.sprite = RoundedSprite; track.type = Image.Type.Sliced; track.raycastTarget = false;
            go.AddComponent<ThemedGraphic>().Role = ThemeRole.Track;
            var le = go.AddComponent<LayoutElement>(); le.minHeight = height; le.preferredHeight = height;

            var f = new GameObject("Fill", typeof(RectTransform));
            f.transform.SetParent(go.transform, false);
            var frt = (RectTransform)f.transform;
            frt.anchorMin = Vector2.zero; frt.anchorMax = new Vector2(0, 1);
            frt.offsetMin = frt.offsetMax = Vector2.zero;
            fill = f.AddComponent<Image>();
            fill.sprite = RoundedSprite; fill.type = Image.Type.Sliced; fill.raycastTarget = false;
            f.AddComponent<ThemedGraphic>().Role = ThemeRole.Positive;
            return track;
        }

        public static void SetProgress(Image fill, float t)
        {
            var rt = fill.rectTransform;
            rt.anchorMax = new Vector2(Mathf.Clamp01(t), 1);
        }

        public static LayoutElement Spacer(Transform parent, float h)
        {
            var go = new GameObject("Spacer", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = h; le.preferredHeight = h;
            return le;
        }

        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
