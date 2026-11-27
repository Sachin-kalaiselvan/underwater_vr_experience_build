using Abyss.Activities;
using Abyss.Core;
using Abyss.Logic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Abyss.UI
{
    // small instruction card that lazily follows the visitor's view during an activity.
    // it only re-centres when it drifts well out of view, so it never feels glued to your face.
    // there's an arrow on it pointing at the next target
    public class ActivityHud : WorldPanel
    {
        [SerializeField] float _maxAngle = 32f;
        [SerializeField] float _followSpeed = 2.5f;

        TextMeshProUGUI _title, _instruction, _count, _flash;
        Image _fill;
        RectTransform _arrow;
        float _flashUntil;
        Vector3 _goalPos;
        Quaternion _goalRot;

        void Awake()
        {
            Services.Register(this);
            Distance = 1.5f;
            DropBelowEyes = 0.32f; // low in the view, a glance down not a head tilt
            Init("ActivityHud", new Vector2(620, 300));
            Canvas.GetComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>().enabled = false;

            var head = UIFactory.Row(Content).transform;
            _title = UIFactory.Text(head, "", UITheme.SmallSize, ThemeRole.Accent, FontStyles.Bold);
            _count = UIFactory.Text(head, "", UITheme.SmallSize, ThemeRole.TextDim, FontStyles.Bold, TextAlignmentOptions.TopRight);
            _instruction = UIFactory.Text(Content, "", UITheme.HeadingSize, ThemeRole.Text, FontStyles.Bold);
            UIFactory.ProgressBar(Content, out _fill);
            _flash = UIFactory.Text(Content, "", UITheme.BodySize, ThemeRole.Positive, FontStyles.Italic);

            // arrow: a rotated square peeking out of the card edge
            var a = new GameObject("Arrow", typeof(RectTransform));
            a.transform.SetParent(Canvas.transform, false);
            _arrow = (RectTransform)a.transform;
            _arrow.sizeDelta = new Vector2(40, 40);
            var img = a.AddComponent<Image>();
            img.raycastTarget = false;
            a.AddComponent<ThemedGraphic>().Role = ThemeRole.Accent;
        }

        void OnDestroy() => Services.Unregister(this);

        public void Show(ActivityDefinition def)
        {
            _title.text = def.Title.ToUpperInvariant() + (def.IsSampleContent ? "  · SAMPLE" : "");
            _flash.text = "";
            UIFactory.SetProgress(_fill, 0);
            Open();
            _goalPos = transform.position; _goalRot = transform.rotation;
        }

        public void Hide() => Close();

        public void SetStep(StepModel step, int index, int count, float progress)
        {
            _instruction.text = step.Instruction;
            UpdateCount(step, 0);
            UIFactory.SetProgress(_fill, progress);
            // new step: bring it into view straight away
            PlaceInFront();
            _goalPos = transform.position; _goalRot = transform.rotation;
        }

        public void SetProgress(float progress, StepModel step, int hits)
        {
            UIFactory.SetProgress(_fill, progress);
            UpdateCount(step, hits);
        }

        void UpdateCount(StepModel step, int hits)
        {
            int need = step.RequireAllTargets ? step.TargetIds.Count : 1;
            _count.text = need > 1 ? $"{hits} / {need}" : "";
        }

        public void Flash(string text)
        {
            _flash.text = "✓  " + text;
            _flashUntil = Time.time + 4f;
        }

        void LateUpdate()
        {
            if (!IsOpen) return;
            if (_flash.text.Length > 0 && Time.time > _flashUntil) _flash.text = "";

            var cam = Camera.main;
            if (cam == null) return;
            var head = cam.transform;

            // lazy follow
            var fwd = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            var toHud = Vector3.ProjectOnPlane(_goalPos - head.position, Vector3.up);
            if (fwd.sqrMagnitude > 0.01f && toHud.sqrMagnitude > 0.01f && Vector3.Angle(fwd, toHud) > _maxAngle)
            {
                fwd.Normalize();
                _goalPos = head.position + fwd * Distance + Vector3.down * DropBelowEyes;
                _goalRot = Quaternion.LookRotation(fwd, Vector3.up);
            }
            bool reduced = Services.Get<SettingsManager>()?.Current.ReducedMotion ?? false;
            float k = reduced ? 1f : 1f - Mathf.Exp(-Time.deltaTime * _followSpeed);
            transform.position = Vector3.Lerp(transform.position, _goalPos, k);
            transform.rotation = Quaternion.Slerp(transform.rotation, _goalRot, k);

            // arrow toward target, in the card's plane
            var gm = Services.Get<GuidedActivityManager>();
            if (gm != null && gm.TryGetGuideTarget(out var target))
            {
                var local = transform.InverseTransformPoint(target);
                var dir2 = new Vector2(local.x, local.z > 0 ? local.y : -Mathf.Abs(local.y) - 1f);
                bool ahead = local.z > 0 && Mathf.Abs(local.x) < local.z * 0.4f;
                _arrow.gameObject.SetActive(!ahead);
                if (!ahead)
                {
                    dir2 = new Vector2(local.x, 0).normalized; // left/right is what matters for turning
                    if (dir2.sqrMagnitude < 0.01f) dir2 = Vector2.right;
                    _arrow.anchoredPosition = new Vector2(dir2.x * 340, 0);
                    _arrow.localRotation = Quaternion.Euler(0, 0, 45);
                }
            }
            else _arrow.gameObject.SetActive(false);
        }
    }
}
