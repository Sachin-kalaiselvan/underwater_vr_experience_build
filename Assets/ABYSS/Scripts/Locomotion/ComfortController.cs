using Abyss.Core;
using Abyss.Logic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Comfort;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;

namespace Abyss.Locomotion
{
    // maps comfort settings onto the XRI locomotion providers that come with the starter rig.
    // also locks movement during menus/inspection so the sticks don't do two things at once
    public class ComfortController : MonoBehaviour
    {
        [SerializeField] Transform _rig;

        TeleportationProvider _teleport;
        ContinuousMoveProvider _move;
        SnapTurnProvider _snap;
        ContinuousTurnProvider _smoothTurn;
        TunnelingVignetteController _vignette;
        bool _locked;

        void Awake()
        {
            Services.Register(this);
            if (_rig == null)
            {
                var origin = FindAnyObjectByType<Unity.XR.CoreUtils.XROrigin>();
                if (origin != null) _rig = origin.transform;
            }
            if (_rig != null)
            {
                _teleport = _rig.GetComponentInChildren<TeleportationProvider>(true);
                _move = _rig.GetComponentInChildren<ContinuousMoveProvider>(true);
                _snap = _rig.GetComponentInChildren<SnapTurnProvider>(true);
                _smoothTurn = _rig.GetComponentInChildren<ContinuousTurnProvider>(true);
                _vignette = _rig.GetComponentInChildren<TunnelingVignetteController>(true);
            }
            if (_teleport == null) AbyssLog.Warn("no TeleportationProvider on the rig, teleport won't work");
        }

        void OnEnable()
        {
            var s = Services.Get<SettingsManager>();
            if (s != null) { s.Changed += Apply; Apply(s.Current); }
            var st = Services.Get<ExperienceStateManager>();
            if (st != null) st.StateChanged += OnStateChanged;
        }

        void OnDisable()
        {
            var s = Services.Get<SettingsManager>();
            if (s != null) s.Changed -= Apply;
            var st = Services.Get<ExperienceStateManager>();
            if (st != null) st.StateChanged -= OnStateChanged;
        }

        void OnDestroy() => Services.Unregister(this);

        void OnStateChanged(ExperienceState from, ExperienceState to)
        {
            var s = Services.Get<SettingsManager>();
            if (s != null) Apply(s.Current);
        }

        public void SetLocked(bool locked)
        {
            _locked = locked;
            var s = Services.Get<SettingsManager>();
            Apply(s != null ? s.Current : new SettingsData());
        }

        void Apply(SettingsData s)
        {
            var st = Services.Get<ExperienceStateManager>();
            bool canMove = !_locked && (st == null || st.CanMove) && !s.Stationary;
            // turning is fine in menus etc, only blocked while the stick is busy inspecting
            bool canTurn = !_locked && !s.Stationary;

            // teleport stays on for the provider itself (ReturnToHub uses it), we just gate the input
            if (_teleport != null) _teleport.enabled = true;
            SetTeleportInput(canMove && s.TeleportEnabled);

            if (_move != null)
            {
                _move.enabled = canMove && s.SmoothMoveEnabled;
                _move.moveSpeed = s.SmoothMoveSpeed;
            }

            if (_snap != null)
            {
                _snap.enabled = canTurn && s.Turn == TurnMode.Snap;
                _snap.turnAmount = s.SnapAngle;
            }
            if (_smoothTurn != null)
            {
                _smoothTurn.enabled = canTurn && s.Turn == TurnMode.Smooth;
                _smoothTurn.turnSpeed = s.SmoothTurnSpeed;
            }

            if (_vignette != null) _vignette.gameObject.SetActive(s.Vignette);
        }

        // the starter rig's ControllerInputActionManager decides teleport vs smooth move on the
        // left stick from its smoothMotionEnabled flag, and toggles the teleport ray's gameobject itself.
        // so: set its flags, and gate the teleport ray by disabling components not the gameobject.
        // done by name/reflection so we don't hard-depend on the sample assembly
        void SetTeleportInput(bool on)
        {
            if (_rig == null) return;
            var s = Services.Get<SettingsManager>()?.Current ?? new SettingsData();
            foreach (var mb in _rig.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || mb.GetType().Name != "ControllerInputActionManager") continue;
                SetBool(mb, "smoothMotionEnabled", s.SmoothMoveEnabled);
                SetBool(mb, "smoothTurnEnabled", s.Turn == TurnMode.Smooth);
            }
            foreach (var t in _rig.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor>(true))
            {
                if (t.gameObject.name.IndexOf("Teleport", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                t.enabled = on;
                var line = t.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals.XRInteractorLineVisual>();
                if (line != null) line.enabled = on;
                var lr = t.GetComponent<LineRenderer>();
                if (lr != null) lr.enabled = on;
            }
        }

        static void SetBool(object target, string prop, bool value)
        {
            var p = target.GetType().GetProperty(prop);
            if (p != null && p.CanWrite && p.PropertyType == typeof(bool)) p.SetValue(target, value);
        }
    }
}
