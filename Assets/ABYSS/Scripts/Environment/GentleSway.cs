using Abyss.Core;
using UnityEngine;

namespace Abyss.Environment
{
    // slow back and forth for kelp / sea fans. one component can drive many children
    // so a whole kelp bed is one Update, not hundreds
    public class GentleSway : MonoBehaviour
    {
        [SerializeField] float _angle = 6f;
        [SerializeField] float _speed = 0.35f;
        [SerializeField] Transform[] _targets;

        Quaternion[] _base;
        float[] _phase;

        void Start()
        {
            if (_targets == null || _targets.Length == 0)
            {
                _targets = new Transform[transform.childCount];
                for (int i = 0; i < transform.childCount; i++) _targets[i] = transform.GetChild(i);
            }
            _base = new Quaternion[_targets.Length];
            _phase = new float[_targets.Length];
            for (int i = 0; i < _targets.Length; i++)
            {
                _base[i] = _targets[i].localRotation;
                // phase from position so neighbours move together like a current passing through
                var p = _targets[i].position;
                _phase[i] = p.x * 0.35f + p.z * 0.2f;
            }
        }

        void Update()
        {
            bool reduced = Services.Get<SettingsManager>()?.Current.ReducedMotion ?? false;
            float amp = reduced ? _angle * 0.4f : _angle;
            float t = Time.time * _speed;
            for (int i = 0; i < _targets.Length; i++)
            {
                if (_targets[i] == null) continue;
                float a = Mathf.Sin(t + _phase[i]) * amp;
                float b = Mathf.Sin(t * 0.73f + _phase[i] * 1.3f) * amp * 0.5f;
                _targets[i].localRotation = _base[i] * Quaternion.Euler(a, 0f, b);
            }
        }
    }
}
