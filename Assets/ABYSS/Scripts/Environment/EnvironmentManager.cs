using Abyss.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Abyss.Environment
{
    // blends fog / ambient / sun from whatever zones the head is in.
    // cheap: a handful of box tests a few times a second, then a lerp every frame
    public class EnvironmentManager : MonoBehaviour
    {
        [SerializeField] UnderwaterProfile _fallback;
        [SerializeField] Light _sun;
        [SerializeField] CausticsAnimator _caustics;
        [SerializeField] float _sampleInterval = 0.2f;
        [SerializeField] float _smoothing = 1.5f; // seconds-ish to settle

        struct Look
        {
            public Color Fog, Sky, Eq, Ground, Sun;
            public float Density, SunI, Caustic, MinLuma;
        }

        Look _target, _current;
        float _nextSample;
        bool _init;

        public string CurrentAreaId { get; private set; }
        public event System.Action<string> AreaChanged;

        void Awake() => Services.Register(this);
        void OnDestroy() => Services.Unregister(this);

        void Start()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            if (_sun == null) _sun = RenderSettings.sun;
            if (_caustics == null) _caustics = FindAnyObjectByType<CausticsAnimator>();
            Sample();
            _current = _target;
            _init = true;
            Push();
        }

        void Update()
        {
            if (Time.unscaledTime >= _nextSample)
            {
                _nextSample = Time.unscaledTime + _sampleInterval;
                Sample();
            }
            if (!_init) return;
            float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime * (3f / Mathf.Max(0.05f, _smoothing)));
            _current.Fog = Color.Lerp(_current.Fog, _target.Fog, k);
            _current.Sky = Color.Lerp(_current.Sky, _target.Sky, k);
            _current.Eq = Color.Lerp(_current.Eq, _target.Eq, k);
            _current.Ground = Color.Lerp(_current.Ground, _target.Ground, k);
            _current.Sun = Color.Lerp(_current.Sun, _target.Sun, k);
            _current.Density = Mathf.Lerp(_current.Density, _target.Density, k);
            _current.SunI = Mathf.Lerp(_current.SunI, _target.SunI, k);
            _current.Caustic = Mathf.Lerp(_current.Caustic, _target.Caustic, k);
            _current.MinLuma = Mathf.Lerp(_current.MinLuma, _target.MinLuma, k);
            Push();
        }

        void Sample()
        {
            var cam = Camera.main;
            Vector3 p = cam != null ? cam.transform.position : Vector3.zero;

            float total = 0f; Look acc = default;
            AreaZone strongest = null; float strongestW = 0f;
            foreach (var z in AreaZone.All)
            {
                if (z.Profile == null) continue;
                float w = z.Weight(p) * (1f + z.Priority * 0.5f);
                if (w <= 0f) continue;
                Add(ref acc, z.Profile, w);
                total += w;
                if (w > strongestW) { strongestW = w; strongest = z; }
            }

            if (total <= 0f)
            {
                if (_fallback == null) return;
                acc = default; Add(ref acc, _fallback, 1f); total = 1f;
            }

            Scale(ref acc, 1f / total);
            _target = acc;

            var id = strongest != null ? strongest.Id : null;
            if (id != CurrentAreaId)
            {
                CurrentAreaId = id;
                AreaChanged?.Invoke(id);
            }
        }

        static void Add(ref Look a, UnderwaterProfile p, float w)
        {
            a.Fog += p.FogColor * w; a.Sky += p.AmbientSky * w; a.Eq += p.AmbientEquator * w;
            a.Ground += p.AmbientGround * w; a.Sun += p.SunColor * w;
            a.Density += p.FogDensity * w; a.SunI += p.SunIntensity * w;
            a.Caustic += p.CausticStrength * w; a.MinLuma += p.MinAmbientLuma * w;
        }

        static void Scale(ref Look a, float s)
        {
            a.Fog *= s; a.Sky *= s; a.Eq *= s; a.Ground *= s; a.Sun *= s;
            a.Density *= s; a.SunI *= s; a.Caustic *= s; a.MinLuma *= s;
        }

        void Push()
        {
            RenderSettings.fogColor = _current.Fog;
            RenderSettings.fogDensity = _current.Density;
            RenderSettings.ambientSkyColor = Floor(_current.Sky, _current.MinLuma);
            RenderSettings.ambientEquatorColor = Floor(_current.Eq, _current.MinLuma * 0.7f);
            RenderSettings.ambientGroundColor = _current.Ground;
            // build the ambient probe ourselves too, so lit shaders pick up the change straight away
            var sky = Floor(_current.Sky, _current.MinLuma); var eq = Floor(_current.Eq, _current.MinLuma * 0.7f);
            var sh = new SphericalHarmonicsL2();
            sh.AddAmbientLight(eq);
            sh.AddDirectionalLight(Vector3.up, (sky - eq) * 0.6f, 1f);
            sh.AddDirectionalLight(Vector3.down, (_current.Ground - eq) * 0.6f, 1f);
            RenderSettings.ambientProbe = sh;
            if (_sun != null) { _sun.color = _current.Sun; _sun.intensity = _current.SunI; }
            if (_caustics != null) _caustics.Strength = _current.Caustic;
            var cam = Camera.main;
            // solid clear colour = fog colour, so the far distance just melts into water. no skybox cost
            if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = _current.Fog; }
        }

        // keeps the deep area from going pitch black
        static Color Floor(Color c, float minLuma)
        {
            float l = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
            if (l >= minLuma || l <= 0.0001f) return l <= 0.0001f ? new Color(minLuma, minLuma, minLuma) : c;
            return c * (minLuma / l);
        }
    }
}
