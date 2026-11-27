using Abyss.Core;
using Abyss.Logic;
using UnityEngine;

namespace Abyss.Audio
{
    // pooled one-shots + two ambience loops. volumes come from settings.
    // there's a hard ceiling on cue volume so nothing can ever be startling
    public class AudioManager : MonoBehaviour
    {
        [SerializeField] AudioCueLibrary _library;
        [SerializeField] int _poolSize = 10;
        [Tooltip("absolute max for any single cue, keeps it gentle")]
        [SerializeField] float _cueCeiling = 0.7f;

        AudioSource[] _pool;
        int _next;
        AudioSource _ambient, _bubbles;
        float _envVol = 0.7f, _intVol = 0.8f;
        Transform _head;

        static AudioManager _instance;

        void Awake()
        {
            _instance = this;
            Services.Register(this);
            _pool = new AudioSource[_poolSize];
            for (int i = 0; i < _poolSize; i++)
            {
                var go = new GameObject("cue_" + i);
                go.transform.SetParent(transform, false);
                var s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.rolloffMode = AudioRolloffMode.Logarithmic;
                s.minDistance = 0.6f;
                s.maxDistance = 25f;
                s.dopplerLevel = 0f;
                _pool[i] = s;
            }
            _ambient = MakeLoop("ambient");
            _bubbles = MakeLoop("bubbles");
        }

        AudioSource MakeLoop(string n)
        {
            var go = new GameObject(n);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.loop = true; s.playOnAwake = false; s.spatialBlend = 0f;
            return s;
        }

        void OnEnable()
        {
            var sm = Services.Get<SettingsManager>();
            if (sm != null) { sm.Changed += Apply; Apply(sm.Current); }
        }

        void OnDisable()
        {
            var sm = Services.Get<SettingsManager>();
            if (sm != null) sm.Changed -= Apply;
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
            Services.Unregister(this);
        }

        void Start()
        {
            if (_library == null) { AbyssLog.Warn("no audio library assigned, running silent"); return; }
            StartLoop(_ambient, _library.AmbientLoop);
            StartLoop(_bubbles, _library.BubblesLoop);
            Apply(Services.Get<SettingsManager>()?.Current ?? new SettingsData());
        }

        static void StartLoop(AudioSource s, AudioClip c)
        {
            if (c == null) return;
            s.clip = c;
            s.time = Random.Range(0f, c.length * 0.9f); // so it doesn't start at the exact same spot each launch
            s.Play();
        }

        void Apply(SettingsData s)
        {
            _envVol = s.EffectiveVolume(s.EnvironmentVolume);
            _intVol = s.EffectiveVolume(s.InteractionVolume);
            if (_library != null)
            {
                _ambient.volume = _library.AmbientVolume * _envVol;
                _bubbles.volume = _library.BubblesVolume * _envVol;
            }
            AudioListener.volume = s.Muted ? 0f : 1f;
        }

        public static void PlayCue(AudioCue cue, Vector3 pos)
        {
            if (_instance != null) _instance.Play(cue, pos);
        }

        void Play(AudioCue cue, Vector3 pos)
        {
            if (_library == null || _intVol <= 0f) return;
            var e = _library.Get(cue);
            if (e == null || e.Clips == null || e.Clips.Length == 0) return;
            var clip = e.Clips[Random.Range(0, e.Clips.Length)];
            if (clip == null) return;

            var src = _pool[_next];
            _next = (_next + 1) % _pool.Length;

            // ui cues come from roughly where the panel is, but pulled close so they're never faint or far
            if (_head == null && Camera.main != null) _head = Camera.main.transform;
            if (_head != null && e.Spatial > 0f)
            {
                var d = pos - _head.position;
                if (d.magnitude > 3f) pos = _head.position + d.normalized * 3f;
            }

            src.transform.position = pos;
            src.spatialBlend = e.Spatial;
            src.pitch = Random.Range(e.PitchRange.x, e.PitchRange.y);
            src.volume = Mathf.Min(e.Volume * _intVol, _cueCeiling);
            src.clip = clip;
            src.Play();
        }
    }
}
