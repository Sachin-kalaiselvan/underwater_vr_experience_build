using System.Collections;
using Abyss.Core;
using UnityEngine;

namespace Abyss.UI
{
    // base for anything that pops up in front of the visitor.
    // placement is yaw-only and a touch below eye level so nobody has to crane their neck
    public class WorldPanel : MonoBehaviour
    {
        public Canvas Canvas { get; private set; }
        public RectTransform Content { get; private set; }
        protected CanvasGroup Group;

        [SerializeField] protected float Distance = UITheme.PanelDistance;
        [SerializeField] protected float DropBelowEyes = 0.12f;
        [SerializeField] protected float FadeTime = 0.2f;

        Coroutine _fade;
        public bool IsOpen { get; private set; }

        protected void Init(string title, Vector2 sizeMm)
        {
            Canvas = UIFactory.WorldCanvas(title, transform, sizeMm);
            Group = Canvas.GetComponent<CanvasGroup>();
            var panel = UIFactory.Panel(Canvas.transform);
            Content = (RectTransform)UIFactory.Column(panel.transform).transform;
            Group.alpha = 0f;
            Canvas.gameObject.SetActive(false);
        }

        public virtual void Open(bool placeInFront = true)
        {
            if (placeInFront) PlaceInFront();
            IsOpen = true;
            Canvas.gameObject.SetActive(true);
            FadeTo(1f);
        }

        public virtual void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            FadeTo(0f, () => Canvas.gameObject.SetActive(false));
        }

        public void PlaceInFront(float? distance = null)
        {
            var cam = Camera.main;
            if (cam == null) return;
            var head = cam.transform;
            var fwd = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.ProjectOnPlane(head.up, Vector3.up); // looking straight down
            fwd.Normalize();
            transform.position = head.position + fwd * (distance ?? Distance) + Vector3.down * DropBelowEyes;
            transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
        }

        public void PlaceAt(Vector3 pos)
        {
            var cam = Camera.main;
            transform.position = pos;
            if (cam == null) return;
            var look = Vector3.ProjectOnPlane(pos - cam.transform.position, Vector3.up);
            if (look.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
        }

        void FadeTo(float a, System.Action done = null)
        {
            if (_fade != null) StopCoroutine(_fade);
            if (!isActiveAndEnabled) { Group.alpha = a; done?.Invoke(); return; }
            _fade = StartCoroutine(DoFade(a, done));
        }

        IEnumerator DoFade(float a, System.Action done)
        {
            bool reduced = Services.Get<SettingsManager>()?.Current.ReducedMotion ?? false;
            float from = Group.alpha, d = reduced ? 0.01f : FadeTime;
            Group.interactable = a > 0.5f;
            Group.blocksRaycasts = a > 0.5f;
            for (float e = 0; e < d; e += Time.unscaledDeltaTime)
            {
                Group.alpha = Mathf.Lerp(from, a, e / d);
                yield return null;
            }
            Group.alpha = a;
            _fade = null;
            done?.Invoke();
        }

        protected void ClearContent()
        {
            for (int i = Content.childCount - 1; i >= 0; i--) Destroy(Content.GetChild(i).gameObject);
        }
    }
}
