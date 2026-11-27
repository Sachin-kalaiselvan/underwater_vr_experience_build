using Abyss.Core;
using UnityEngine;

namespace Abyss.Environment
{
    // a school as one instanced draw call. no per fish gameobjects, no physics.
    // fish follow a wandering leader point with a bit of noise each, which reads as schooling
    // from a few metres away and costs almost nothing on the CPU
    public class FishSchool : MonoBehaviour
    {
        [SerializeField] Mesh _mesh;
        [SerializeField] Material _material;
        [SerializeField] int _count = 40;
        [SerializeField] Vector3 _area = new Vector3(8, 3, 8);
        [SerializeField] float _speed = 0.8f;
        [SerializeField] float _spread = 1.4f;
        [SerializeField] Vector2 _sizeRange = new Vector2(0.8f, 1.2f);
        [Tooltip("keeps fish this far from the visitor's head so nothing swims through your face")]
        [SerializeField] float _personalSpace = 1.2f;

        Vector3[] _pos, _vel, _offset;
        float[] _scale, _phase;
        Matrix4x4[] _matrices;
        RenderParams _rp;
        float _seed;
        int _active;

        public void Configure(Mesh mesh, Material mat, int count, Vector3 area, float speed)
        {
            _mesh = mesh; _material = mat; _count = count; _area = area; _speed = speed;
        }

        void Start()
        {
            if (_mesh == null || _material == null) { enabled = false; return; }
            _material.enableInstancing = true;
            _seed = Random.value * 100f;
            _pos = new Vector3[_count]; _vel = new Vector3[_count]; _offset = new Vector3[_count];
            _scale = new float[_count]; _phase = new float[_count];
            _matrices = new Matrix4x4[_count];
            for (int i = 0; i < _count; i++)
            {
                _offset[i] = Random.insideUnitSphere * _spread;
                _pos[i] = transform.position + _offset[i];
                _scale[i] = Random.Range(_sizeRange.x, _sizeRange.y);
                _phase[i] = Random.value * 10f;
            }
            _rp = new RenderParams(_material)
            {
                shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
                receiveShadows = false,
                layer = gameObject.layer,
                worldBounds = new Bounds(transform.position, _area + Vector3.one * (_spread * 4f))
            };
            _active = _count;
        }

        // PerformanceManager scales this down on the reduced profile
        public void SetDensity(float fraction) => _active = Mathf.Clamp(Mathf.RoundToInt(_count * fraction), 0, _count);

        Vector3 Leader(float t)
        {
            return transform.position + new Vector3(
                (Mathf.PerlinNoise(_seed, t * 0.05f) - 0.5f) * _area.x,
                (Mathf.PerlinNoise(_seed + 7f, t * 0.04f) - 0.5f) * _area.y,
                (Mathf.PerlinNoise(_seed + 13f, t * 0.05f) - 0.5f) * _area.z);
        }

        void Update()
        {
            if (_active == 0) return;
            bool reduced = Services.Get<SettingsManager>()?.Current.ReducedMotion ?? false;
            float sp = reduced ? _speed * 0.5f : _speed;
            float t = Time.time * sp;
            var leader = Leader(t);
            var cam = Camera.main;
            Vector3 head = cam != null ? cam.transform.position : new Vector3(9999, 9999, 9999);
            float dt = Time.deltaTime;

            for (int i = 0; i < _active; i++)
            {
                var wobble = new Vector3(Mathf.Sin(t * 1.3f + _phase[i]), Mathf.Sin(t * 0.9f + _phase[i] * 2f) * 0.4f, Mathf.Cos(t * 1.1f + _phase[i]));
                var goal = leader + _offset[i] + wobble * 0.35f;
                var away = _pos[i] - head;
                float d = away.magnitude;
                if (d < _personalSpace && d > 0.001f) goal += away / d * (_personalSpace - d) * 3f;

                var desired = (goal - _pos[i]) * 1.2f;
                _vel[i] = Vector3.Lerp(_vel[i], desired, dt * 1.5f);
                float max = sp * 1.6f;
                if (_vel[i].sqrMagnitude > max * max) _vel[i] = _vel[i].normalized * max;
                _pos[i] += _vel[i] * dt;

                var dir = _vel[i].sqrMagnitude > 0.0001f ? _vel[i] : transform.forward;
                _matrices[i] = Matrix4x4.TRS(_pos[i], Quaternion.LookRotation(dir, Vector3.up), Vector3.one * _scale[i]);
            }

            _rp.worldBounds = new Bounds(leader, _area + Vector3.one * (_spread * 4f));
            Graphics.RenderMeshInstanced(_rp, _mesh, 0, _matrices, _active);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.5f, 0.3f);
            Gizmos.DrawWireCube(transform.position, _area);
        }
    }
}
