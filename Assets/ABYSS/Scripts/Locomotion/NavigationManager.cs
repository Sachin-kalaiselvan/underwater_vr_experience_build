using System.Collections;
using System.Collections.Generic;
using Abyss.Core;
using Abyss.XR;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace Abyss.Locomotion
{
    // named spots in the world (the hub + one per area) and safe ways of getting there.
    // every jump is fade out -> teleport -> fade in so it's never a sudden cut
    public class NavigationManager : MonoBehaviour
    {
        [System.Serializable]
        public class Anchor
        {
            public string Id;
            public string Label;
            public Transform Point;
        }

        [SerializeField] string _hubId = "hub";
        [SerializeField] List<Anchor> _anchors = new List<Anchor>();
        [SerializeField] float _fadeTime = 0.25f;

        TeleportationProvider _teleport;
        bool _busy;

        public IReadOnlyList<Anchor> Anchors => _anchors;

        void Awake()
        {
            Services.Register(this);
            _teleport = FindAnyObjectByType<TeleportationProvider>();
        }

        void OnDestroy() => Services.Unregister(this);

        public void SetAnchors(string hubId, List<Anchor> anchors)
        {
            _hubId = hubId;
            _anchors = anchors;
        }

        // environment scene registers its anchors after it loads
        public void RegisterAnchor(string id, string label, Transform point)
        {
            _anchors.RemoveAll(a => a.Id == id);
            _anchors.Add(new Anchor { Id = id, Label = label, Point = point });
        }

        public void ReturnToHub() => GoTo(_hubId);

        // instant, no fade. only for placing the visitor while the screen is already covered (startup)
        public void SnapToHub()
        {
            var a = _anchors.Find(x => x.Id == _hubId);
            if (a == null || a.Point == null) { AbyssLog.Warn("no hub anchor yet"); return; }
            Services.Get<XRConfigurationManager>()?.RecenterTo(a.Point.position, a.Point.forward);
        }

        public void GoTo(string id)
        {
            var a = _anchors.Find(x => x.Id == id);
            if (a == null || a.Point == null) { AbyssLog.Warn($"no nav anchor '{id}'"); return; }
            if (_busy) return;
            StartCoroutine(Jump(a.Point.position, a.Point.forward));
        }

        IEnumerator Jump(Vector3 pos, Vector3 forward)
        {
            _busy = true;
            var fader = Services.Get<ScreenFader>();
            bool reduced = Services.Get<SettingsManager>()?.Current.ReducedMotion ?? false;
            float t = reduced ? _fadeTime * 1.5f : _fadeTime; // slower fade is gentler, not faster

            if (fader != null) yield return fader.FadeTo(1f, t);

            if (_teleport == null) _teleport = FindAnyObjectByType<TeleportationProvider>();
            bool queued = false;
            if (_teleport != null)
            {
                queued = _teleport.QueueTeleportRequest(new TeleportRequest
                {
                    destinationPosition = pos,
                    destinationRotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(forward, Vector3.up).normalized, Vector3.up),
                    matchOrientation = MatchOrientation.TargetUpAndForward
                });
            }
            if (!queued)
            {
                // fallback for setups without a teleport provider
                Services.Get<XRConfigurationManager>()?.RecenterTo(pos, forward);
            }

            // provider applies on its next update
            yield return null;
            yield return null;

            if (fader != null) yield return fader.FadeTo(0f, t);
            _busy = false;
        }
    }
}
