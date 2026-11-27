using System.IO;
using System.Text;
using Abyss.Core;
using Abyss.Environment;
using Abyss.Logic;
using Abyss.XR;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Abyss.Performance
{
    // two quality profiles + a stats logger for real device numbers.
    // nothing in the docs gets a number unless it came out of this logger on a headset.
    // staff combo: click both thumbsticks together to toggle logging (and the stats card in dev builds)
    public class PerformanceManager : MonoBehaviour
    {
        [System.Serializable]
        public class Profile
        {
            public float RenderScale = 1f;
            [Range(0, 1)] public float Foveation = 0.5f;
            [Range(0, 1)] public float FishDensity = 1f;
            [Range(0, 1)] public float ParticleDensity = 1f;
            public bool Caustics = true;
        }

        [SerializeField] Profile _standard = new Profile();
        [SerializeField] Profile _reduced = new Profile { RenderScale = 0.85f, Foveation = 1f, FishDensity = 0.5f, ParticleDensity = 0.5f, Caustics = false };
        [SerializeField] ParticleSystem[] _particles;
        [SerializeField] float _logInterval = 1f;

        InputAction _combo;
        bool _logging;
        StreamWriter _log;
        float _nextLog;
        int _frames;
        double _cpuSum, _gpuSum, _cpuMax, _gpuMax;
        readonly FrameTiming[] _timings = new FrameTiming[1];
        ProfilerRecorder _drawCalls, _batches, _setPass, _tris;
        int[] _particleMax;

        public bool IsLogging => _logging;
        public string LastSummary { get; private set; } = "";

        void Awake()
        {
            Services.Register(this);
            _combo = new InputAction("AbyssPerfCombo", InputActionType.Button);
            _combo.AddCompositeBinding("ButtonWithOneModifier")
                .With("Modifier", "<XRController>{LeftHand}/primary2DAxisClick")
                .With("Button", "<XRController>{RightHand}/primary2DAxisClick");
            _combo.AddBinding("<Keyboard>/f9");
            _combo.performed += _ => ToggleLogging();

            if (_particles != null)
            {
                _particleMax = new int[_particles.Length];
                for (int i = 0; i < _particles.Length; i++) if (_particles[i] != null) _particleMax[i] = _particles[i].main.maxParticles;
            }
        }

        void OnEnable()
        {
            _combo.Enable();
            // these counters only have data in development builds / editor
            _drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            _batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            _setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            _tris = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            var sm = Services.Get<SettingsManager>();
            if (sm != null) { sm.Changed += OnSettings; OnSettings(sm.Current); }
        }

        void OnDisable()
        {
            _combo.Disable();
            _drawCalls.Dispose(); _batches.Dispose(); _setPass.Dispose(); _tris.Dispose();
            var sm = Services.Get<SettingsManager>();
            if (sm != null) sm.Changed -= OnSettings;
            StopLogging();
        }

        void OnDestroy()
        {
            _combo?.Dispose();
            Services.Unregister(this);
        }

        void OnSettings(SettingsData s) => Apply(s.Quality == QualityProfile.Reduced ? _reduced : _standard);

        public void Apply(Profile p)
        {
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp) urp.renderScale = p.RenderScale;
            Services.Get<XRConfigurationManager>()?.SetFoveation(p.Foveation);
            foreach (var f in FindObjectsByType<FishSchool>(FindObjectsSortMode.None)) f.SetDensity(p.FishDensity);
            // particles live in the environment scene, pick them up lazily
            if (_particles == null || _particles.Length == 0)
            {
                _particles = FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None);
                _particleMax = new int[_particles.Length];
                for (int i = 0; i < _particles.Length; i++) _particleMax[i] = _particles[i].main.maxParticles;
            }
            if (_particles != null)
                for (int i = 0; i < _particles.Length; i++)
                {
                    if (_particles[i] == null) continue;
                    var m = _particles[i].main;
                    m.maxParticles = Mathf.Max(1, Mathf.RoundToInt(_particleMax[i] * p.ParticleDensity));
                }
            foreach (var c in FindObjectsByType<CausticsAnimator>(FindObjectsSortMode.None)) c.enabled = p.Caustics;
        }

        public void ToggleLogging()
        {
            if (_logging) StopLogging(); else StartLogging();
        }

        void StartLogging()
        {
            var dir = Path.Combine(Application.persistentDataPath, "perf");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"perf_{System.DateTime.Now:yyyyMMdd_HHmmss}.csv");
            _log = new StreamWriter(path, false, Encoding.UTF8);
            _log.WriteLine("# device," + SystemInfo.deviceModel + ",gpu," + SystemInfo.graphicsDeviceName + ",api," + SystemInfo.graphicsDeviceType);
            var d = XRConfigurationManager.Display;
            if (d != null)
                _log.WriteLine($"# eyeTexture,{UnityEngine.XR.XRSettings.eyeTextureWidth}x{UnityEngine.XR.XRSettings.eyeTextureHeight},renderScale,{(GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset)?.renderScale},refresh,{Services.Get<XRConfigurationManager>()?.RefreshRate}");
            _log.WriteLine("time_s,area,fps,cpu_avg_ms,cpu_max_ms,gpu_avg_ms,gpu_max_ms,draw_calls,batches,setpass,tris,mem_alloc_mb,mem_reserved_mb,mono_mb");
            _logging = true;
            ResetWindow();
            AbyssLog.Warn($"perf logging to {path}"); // warn so it shows in release logcat too
        }

        void StopLogging()
        {
            if (!_logging) return;
            _logging = false;
            _log?.Flush(); _log?.Dispose(); _log = null;
            AbyssLog.Warn("perf logging stopped");
        }

        void ResetWindow()
        {
            _frames = 0; _cpuSum = _gpuSum = _cpuMax = _gpuMax = 0;
            _nextLog = Time.unscaledTime + _logInterval;
        }

        void Update()
        {
            if (!_logging) return;
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, _timings) > 0)
            {
                double cpu = _timings[0].cpuFrameTime, gpu = _timings[0].gpuFrameTime;
                _cpuSum += cpu; _gpuSum += gpu;
                if (cpu > _cpuMax) _cpuMax = cpu;
                if (gpu > _gpuMax) _gpuMax = gpu;
            }
            _frames++;

            if (Time.unscaledTime < _nextLog) return;
            float window = _logInterval + (Time.unscaledTime - _nextLog);
            string area = Services.Get<EnvironmentManager>()?.CurrentAreaId ?? "-";
            double mb = 1024.0 * 1024.0;
            var line = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "{0:0.0},{1},{2:0.0},{3:0.00},{4:0.00},{5:0.00},{6:0.00},{7},{8},{9},{10},{11:0.0},{12:0.0},{13:0.0}",
                Time.realtimeSinceStartup, area, _frames / window,
                _frames > 0 ? _cpuSum / _frames : 0, _cpuMax, _frames > 0 ? _gpuSum / _frames : 0, _gpuMax,
                _drawCalls.LastValue, _batches.LastValue, _setPass.LastValue, _tris.LastValue,
                UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / mb,
                UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong() / mb,
                UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() / mb);
            _log.WriteLine(line);
            LastSummary = line;
            ResetWindow();
        }
    }
}
