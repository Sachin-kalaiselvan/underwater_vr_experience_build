using System.Collections.Generic;
using Abyss.Core;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR;

namespace Abyss.XR
{
    // runtime XR setup that isn't covered by project settings:
    // floor tracking, foveation level, reading the refresh rate, recentering.
    // never touches the camera transform, only the rig root
    [DefaultExecutionOrder(-250)]
    public class XRConfigurationManager : MonoBehaviour
    {
        [SerializeField] XROrigin _origin;
        [Range(0f, 1f)]
        [SerializeField] float _foveation = 0.5f; // medium, raised by the reduced profile

        static readonly List<XRDisplaySubsystem> _displays = new List<XRDisplaySubsystem>();
        static readonly List<XRInputSubsystem> _inputs = new List<XRInputSubsystem>();

        public XROrigin Origin => _origin;
        public float RefreshRate { get; private set; }
        public bool HeadsetActive => XRSettings.isDeviceActive;

        void Awake()
        {
            Services.Register(this);
            if (_origin == null) _origin = FindAnyObjectByType<XROrigin>();
        }

        void OnDestroy() => Services.Unregister(this);

        void Start()
        {
            if (_origin != null) _origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
            SetFoveation(_foveation);

            var d = Display;
            if (d != null && d.TryGetDisplayRefreshRate(out float hz))
            {
                RefreshRate = hz;
                AbyssLog.Info($"display refresh {hz:0} Hz");
            }
            else
            {
                AbyssLog.Info("no XR display running (editor without headset or simulator?)");
            }
        }

        public static XRDisplaySubsystem Display
        {
            get
            {
                SubsystemManager.GetSubsystems(_displays);
                foreach (var d in _displays) if (d.running) return d;
                return null;
            }
        }

        public void SetFoveation(float level)
        {
            _foveation = Mathf.Clamp01(level);
            var d = Display;
            if (d == null) return;
            d.foveatedRenderingLevel = _foveation;
            // gaze based foveation only kicks in on headsets with eye tracking, harmless elsewhere
            d.foveatedRenderingFlags = XRDisplaySubsystem.FoveatedRenderingFlags.GazeAllowed;
        }

        // turns the rig so the visitor faces 'forward' at 'position' without moving the camera locally.
        // used by the recenter option in the pause menu
        public void RecenterTo(Vector3 position, Vector3 forward)
        {
            // try the runtime first, Quest usually handles this through the system menu anyway
            SubsystemManager.GetSubsystems(_inputs);
            foreach (var i in _inputs) if (i.running) i.TryRecenter();

            if (_origin == null) return;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            _origin.MatchOriginUpCameraForward(Vector3.up, forward.normalized);
            var head = _origin.Camera.transform.position;
            // keep the visitor's real head height, just move them over the target spot
            _origin.MoveCameraToWorldLocation(new Vector3(position.x, head.y - _origin.Origin.transform.position.y + position.y, position.z));
        }
    }
}
