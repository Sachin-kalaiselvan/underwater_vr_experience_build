using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Abyss.Environment
{
    // slides the sun's light cookie around so caustics drift over the sand.
    // one cookie on the main light is about the cheapest caustic you can get in URP
    [RequireComponent(typeof(Light))]
    public class CausticsAnimator : MonoBehaviour
    {
        [SerializeField] Vector2 _speed = new Vector2(0.11f, 0.07f);
        [SerializeField] float _wobble = 0.35f;
        [SerializeField] Vector2 _cookieSize = new Vector2(6f, 6f);

        Light _light;
        UniversalAdditionalLightData _urp;
        Texture _cookie;
        float _baseIntensity = -1f;

        public float Strength { get; set; } = 1f;

        void Awake()
        {
            _light = GetComponent<Light>();
            _urp = GetComponent<UniversalAdditionalLightData>();
            _cookie = _light.cookie;
            if (_urp != null) _urp.lightCookieSize = _cookieSize;
        }

        void Update()
        {
            bool reduced = Abyss.Core.Services.Get<Abyss.Core.SettingsManager>()?.Current.ReducedMotion ?? false;
            float t = Time.time * (reduced ? 0.35f : 1f);
            if (_urp != null)
                _urp.lightCookieOffset = new Vector2(t * _speed.x + Mathf.Sin(t * 0.7f) * _wobble, t * _speed.y + Mathf.Cos(t * 0.5f) * _wobble);

            // fade the cookie out in the deep, where there'd be no caustics anyway
            _light.cookie = Strength > 0.05f ? _cookie : null;
        }
    }
}
