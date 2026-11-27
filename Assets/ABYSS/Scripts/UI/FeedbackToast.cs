using System.Collections;
using Abyss.Core;
using TMPro;
using UnityEngine;

namespace Abyss.UI
{
    // short floating label above an object: "New discovery", "Done" etc.
    // the visual half of every feedback event so audio is never the only signal
    public class FeedbackToast : MonoBehaviour
    {
        public enum Kind { Positive, Neutral }

        Canvas _canvas;
        CanvasGroup _group;
        TextMeshProUGUI _label;
        ThemedGraphic _labelTheme;
        Coroutine _run;

        public static FeedbackToast Create(Transform parent)
        {
            var go = new GameObject("FeedbackToast");
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<FeedbackToast>();
            t._canvas = UIFactory.WorldCanvas("ToastCanvas", go.transform, new Vector2(460, 100));
            t._canvas.GetComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>().enabled = false;
            t._group = t._canvas.GetComponent<CanvasGroup>();
            t._group.blocksRaycasts = false;
            UIFactory.Panel(t._canvas.transform);
            t._label = UIFactory.Text(t._canvas.transform, "", UITheme.HeadingSize, ThemeRole.Positive, FontStyles.Bold, TextAlignmentOptions.Center);
            UIFactory.Stretch(t._label.rectTransform);
            t._labelTheme = t._label.GetComponent<ThemedGraphic>();
            t._canvas.gameObject.SetActive(false);
            return t;
        }

        public void Show(Vector3 objectPos, string text, Kind kind)
        {
            var cam = Camera.main;
            if (cam == null) return;
            // a bit above the object and pulled towards the viewer so it's never inside geometry
            var toCam = (cam.transform.position - objectPos);
            var pos = objectPos + Vector3.up * 0.28f + toCam.normalized * Mathf.Min(0.4f, toCam.magnitude * 0.3f);
            transform.position = pos;
            var look = Vector3.ProjectOnPlane(pos - cam.transform.position, Vector3.up);
            if (look.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(look, Vector3.up);

            // scale with distance so it reads the same size whatever range it's at
            float d = Vector3.Distance(cam.transform.position, pos);
            _canvas.transform.localScale = Vector3.one * UITheme.CanvasScale * Mathf.Clamp(d / 1.3f, 0.6f, 2.5f);

            _label.text = (kind == Kind.Positive ? "✓  " : "") + text;
            _labelTheme.Role = kind == Kind.Positive ? ThemeRole.Positive : ThemeRole.Neutral;
            _labelTheme.Apply();

            if (_run != null) StopCoroutine(_run);
            _run = StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            _canvas.gameObject.SetActive(true);
            bool reduced = Services.Get<SettingsManager>()?.Current.ReducedMotion ?? false;
            var start = transform.position;
            for (float e = 0; e < 1.8f; e += Time.deltaTime)
            {
                _group.alpha = e < 0.15f ? e / 0.15f : (e > 1.4f ? 1f - (e - 1.4f) / 0.4f : 1f);
                if (!reduced) transform.position = start + Vector3.up * (e * 0.03f);
                yield return null;
            }
            _canvas.gameObject.SetActive(false);
            _run = null;
        }
    }
}
