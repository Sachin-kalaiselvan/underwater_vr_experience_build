using System;
using Abyss.Core;
using Abyss.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Abyss.Interaction
{
    // the one info panel. sits between the visitor and the object, pushed to the side
    // so it doesn't cover the thing you're reading about
    public class InformationDisplay : WorldPanel
    {
        [SerializeField] float _sideOffset = 0.42f;

        TextMeshProUGUI _title, _subtitle, _body, _sampleTag;
        Image _illustration;
        Transform _buttons;
        Action _onInspect, _onClose, _onDone;

        void Awake()
        {
            Services.Register(this);
            Init("InfoPanel", new Vector2(760, 620));

            _sampleTag = UIFactory.Text(Content, "SAMPLE CONTENT", UITheme.SmallSize, ThemeRole.TextDim, FontStyles.Bold);
            _title = UIFactory.Text(Content, "", UITheme.TitleSize, ThemeRole.Text, FontStyles.Bold);
            _subtitle = UIFactory.Text(Content, "", UITheme.BodySize, ThemeRole.Accent, FontStyles.Italic);

            var ill = new GameObject("Illustration", typeof(RectTransform));
            ill.transform.SetParent(Content, false);
            _illustration = ill.AddComponent<Image>();
            _illustration.preserveAspect = true;
            _illustration.raycastTarget = false;
            var le = ill.AddComponent<LayoutElement>(); le.preferredHeight = 180;

            _body = UIFactory.Text(Content, "", UITheme.BodySize, ThemeRole.Text);
            var bodyLe = _body.gameObject.AddComponent<LayoutElement>();
            bodyLe.flexibleHeight = 1;

            _buttons = UIFactory.Row(Content).transform;
        }

        void OnDestroy() => Services.Unregister(this);

        public void Show(IAbyssInteractable target, Action onInspect, Action onClose)
        {
            _onInspect = onInspect; _onClose = onClose; _onDone = null;
            Fill(target.Definition);
            RebuildButtons(inspecting: false);
            PlaceNear(target.Root.position);
            Open(placeInFront: false);
        }

        public void ShowInspecting(IAbyssInteractable target, Action onDone)
        {
            _onDone = onDone;
            Fill(target.Definition);
            _body.text = target.Definition.Body + "\n\n<size=80%><color=#9DB8BC>Push the thumbstick to turn it around.</color></size>";
            RebuildButtons(inspecting: true);
            // during inspection the object is front and centre, panel goes off to the right
            PlaceInFront();
            transform.position += transform.right * 0.55f;
            transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(transform.position - Camera.main.transform.position, Vector3.up), Vector3.up);
            Open(placeInFront: false);
        }

        public void Hide() => Close();

        void Fill(InteractableDefinition d)
        {
            _title.text = d.Title;
            _subtitle.text = d.Subtitle;
            _subtitle.gameObject.SetActive(!string.IsNullOrEmpty(d.Subtitle));
            _body.text = d.Body;
            _sampleTag.gameObject.SetActive(d.IsSampleContent);
            _illustration.sprite = d.Illustration;
            _illustration.gameObject.SetActive(d.Illustration != null);
        }

        void RebuildButtons(bool inspecting)
        {
            for (int i = _buttons.childCount - 1; i >= 0; i--) Destroy(_buttons.GetChild(i).gameObject);
            if (inspecting)
            {
                UIFactory.Button(_buttons, "Done", () => _onDone?.Invoke(), primary: true);
                return;
            }
            if (_onInspect != null) UIFactory.Button(_buttons, "Inspect", () => _onInspect?.Invoke(), primary: true);
            UIFactory.Button(_buttons, "Close", () => _onClose?.Invoke(), primary: _onInspect == null);
        }

        void PlaceNear(Vector3 objectPos)
        {
            var cam = Camera.main;
            if (cam == null) { PlaceAt(objectPos); return; }
            var head = cam.transform.position;
            var toObj = Vector3.ProjectOnPlane(objectPos - head, Vector3.up);
            float dist = Mathf.Clamp(toObj.magnitude * 0.6f, 1.0f, 1.6f);
            var dir = toObj.sqrMagnitude > 0.01f ? toObj.normalized : Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
            var right = Vector3.Cross(Vector3.up, dir);
            var p = head + dir * dist + right * _sideOffset;
            p.y = head.y - DropBelowEyes;
            PlaceAt(p);
        }
    }
}
