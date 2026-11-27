using System.Collections;
using Abyss.Core;
using Abyss.Locomotion;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Abyss.Interaction
{
    // brings an object up in front of the visitor so they can turn it around with the thumbstick.
    // no precise hand movement needed, which matters for first timers.
    // locomotion is locked while inspecting so the stick doesn't also teleport/turn
    public class ObjectInspection : MonoBehaviour
    {
        [SerializeField] float _distance = 0.6f;
        [SerializeField] float _dropBelowEyes = 0.18f;
        [SerializeField] float _rotateSpeed = 90f; // deg/sec at full stick
        [SerializeField] float _moveTime = 0.45f;

        InputAction _rotate;
        IAbyssInteractable _target;
        Vector3 _originalScale;
        Transform _anchor;
        Rigidbody _rb;
        bool _rbWasKinematic;
        Coroutine _move;

        public bool IsInspecting => _target != null;
        public IAbyssInteractable Target => _target;

        void Awake()
        {
            Services.Register(this);
            _rotate = new InputAction("AbyssInspectRotate", InputActionType.Value);
            _rotate.AddBinding("<XRController>{RightHand}/primary2DAxis");
            _rotate.AddBinding("<XRController>{LeftHand}/primary2DAxis");
            _rotate.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            _anchor = new GameObject("InspectionAnchor").transform;
            _anchor.SetParent(transform, false);
        }

        void OnDestroy()
        {
            _rotate?.Dispose();
            Services.Unregister(this);
        }

        public void Begin(IAbyssInteractable target, IXRInteractor by)
        {
            if (target == null || target.Definition == null) return;
            if (_target != null) EndInspection();

            var cam = Camera.main;
            if (cam == null) return;

            _target = target;
            var t = target.Root;
            _originalScale = t.localScale;

            _rb = t.GetComponent<Rigidbody>();
            if (_rb != null) { _rbWasKinematic = _rb.isKinematic; _rb.isKinematic = true; }

            // anchor in front of the eyes, a bit low so it's a natural look-down not a stare
            var head = cam.transform;
            var fwd = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            _anchor.position = head.position + fwd * _distance + Vector3.down * _dropBelowEyes;
            _anchor.rotation = Quaternion.LookRotation(fwd, Vector3.up);

            float scale = FitScale(t, target.Definition.InspectSize);

            Services.Get<ExperienceStateManager>()?.SetState(ExperienceState.Inspecting);
            Services.Get<ComfortController>()?.SetLocked(true);
            _rotate.Enable();

            if (_move != null) StopCoroutine(_move);
            _move = StartCoroutine(MoveTo(t, _anchor.position, t.rotation, _originalScale * scale));

            Services.Get<InformationDisplay>()?.ShowInspecting(target, () => Finish());
            AbyssLog.Info($"inspecting {target.Id}");
        }

        void Finish()
        {
            var t = _target;
            EndInspection();
            if (t != null) Services.Get<InteractionManager>()?.NotifyInspected(t);
        }

        public void EndInspection()
        {
            if (_target == null) return;
            var t = _target;
            _target = null;
            _rotate.Disable();

            if (_move != null) StopCoroutine(_move);
            var home = (t as InteractableObject)?.HomePose ?? new Pose(t.Root.position, t.Root.rotation);
            _move = StartCoroutine(MoveTo(t.Root, home.position, home.rotation, _originalScale, () =>
            {
                if (_rb != null) _rb.isKinematic = _rbWasKinematic;
                _rb = null;
            }));

            Services.Get<ComfortController>()?.SetLocked(false);
            var s = Services.Get<ExperienceStateManager>();
            if (s != null && s.State == ExperienceState.Inspecting)
                s.SetState(s.Mode == ExperienceMode.Guided ? ExperienceState.Activity : ExperienceState.Exploring);
            Services.Get<InteractionManager>()?.Close(t);
        }

        void Update()
        {
            if (_target == null || _move != null) return;
            var v = _rotate.ReadValue<Vector2>();
            if (v.sqrMagnitude < 0.04f) return;
            var t = _target.Root;
            var cam = Camera.main;
            if (cam == null) return;
            // yaw around world up, pitch around the camera's right so it feels like turning it in your hands
            t.Rotate(Vector3.up, -v.x * _rotateSpeed * Time.deltaTime, Space.World);
            t.Rotate(cam.transform.right, v.y * _rotateSpeed * Time.deltaTime, Space.World);
        }

        static float FitScale(Transform t, float size)
        {
            var rends = t.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return 1f;
            var b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            float biggest = Mathf.Max(b.size.x, b.size.y, b.size.z);
            if (biggest < 0.0001f) return 1f;
            // only shrink big things or grow tiny things, clamp so nothing gets silly
            return Mathf.Clamp(size / biggest, 0.2f, 4f);
        }

        IEnumerator MoveTo(Transform t, Vector3 pos, Quaternion rot, Vector3 scale, System.Action done = null)
        {
            Vector3 p0 = t.position, s0 = t.localScale;
            Quaternion r0 = t.rotation;
            bool reduced = Services.Get<SettingsManager>()?.Current.ReducedMotion ?? false;
            float d = reduced ? 0.01f : _moveTime;
            for (float e = 0; e < d; e += Time.deltaTime)
            {
                if (t == null) yield break;
                float k = Mathf.SmoothStep(0, 1, e / d);
                t.SetPositionAndRotation(Vector3.Lerp(p0, pos, k), Quaternion.Slerp(r0, rot, k));
                t.localScale = Vector3.Lerp(s0, scale, k);
                yield return null;
            }
            if (t != null)
            {
                t.SetPositionAndRotation(pos, rot);
                t.localScale = scale;
            }
            _move = null;
            done?.Invoke();
        }
    }
}
