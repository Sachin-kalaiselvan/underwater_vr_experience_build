using System.Collections.Generic;
using Abyss.Core;
using UnityEngine;

namespace Abyss.Activities
{
    // a spot on the sea floor the visitor needs to get to. checks horizontal distance
    // to the head each frame (cheap, no physics triggers needed).
    // hidden unless an activity asks for it
    public class DestinationMarker : MonoBehaviour
    {
        public static readonly List<DestinationMarker> All = new List<DestinationMarker>();

        [SerializeField] string _id;
        [SerializeField] string _label;
        [SerializeField] float _radius = 1.4f;
        [SerializeField] GameObject _visual;
        [SerializeField] Transform _pulse;

        public string Id => _id;
        public string Label => _label;
        public bool Active { get; private set; }
        public event System.Action<DestinationMarker> Reached;

        bool _inside;
        Vector3 _pulseBase;

        public void Configure(string id, string label, float radius, GameObject visual, Transform pulse)
        {
            _id = id; _label = label; _radius = radius; _visual = visual; _pulse = pulse;
        }

        void Awake()
        {
            if (_pulse != null) _pulseBase = _pulse.localScale;
            SetActive(false);
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public static DestinationMarker Find(string id)
        {
            foreach (var m in All) if (m._id == id) return m;
            return null;
        }

        public void SetActive(bool on)
        {
            Active = on;
            _inside = false;
            if (_visual != null) _visual.SetActive(on);
        }

        void Update()
        {
            if (!Active) return;
            var cam = Camera.main;
            if (cam == null) return;

            if (_pulse != null && !(Services.Get<SettingsManager>()?.Current.ReducedMotion ?? false))
            {
                float k = 1f + 0.08f * Mathf.Sin(Time.time * 2.2f);
                _pulse.localScale = new Vector3(_pulseBase.x * k, _pulseBase.y, _pulseBase.z * k);
            }

            var d = cam.transform.position - transform.position;
            d.y = 0;
            bool inside = d.sqrMagnitude < _radius * _radius;
            if (inside && !_inside) Reached?.Invoke(this);
            _inside = inside;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.5f, 0.85f, 0.8f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, _radius);
        }
    }
}
