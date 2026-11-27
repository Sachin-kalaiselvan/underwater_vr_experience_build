using System.Collections;
using Abyss.Core;
using UnityEngine;

namespace Abyss.Locomotion
{
    // fades to the water colour (not black, black feels like a crash in a headset).
    // a quad parented to the camera, never moves the camera itself
    public class ScreenFader : MonoBehaviour
    {
        [SerializeField] Color _color = new Color(0.02f, 0.09f, 0.13f, 1f);
        [SerializeField] Material _material;

        MeshRenderer _quad;
        static readonly int ColorId = Shader.PropertyToID("_BaseColor");
        MaterialPropertyBlock _mpb;
        float _alpha;

        public float Alpha => _alpha;

        void Awake()
        {
            Services.Register(this);
            _mpb = new MaterialPropertyBlock();
        }

        void Start()
        {
            var cam = Camera.main;
            if (cam == null || _material == null) { AbyssLog.Warn("fader has no camera or material"); return; }
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad); // just a fullscreen card, not scene art
            Destroy(go.GetComponent<Collider>());
            go.name = "FadeQuad";
            go.transform.SetParent(cam.transform, false);
            go.transform.localPosition = new Vector3(0, 0, cam.nearClipPlane + 0.02f);
            go.transform.localScale = new Vector3(2f, 2f, 1f);
            _quad = go.GetComponent<MeshRenderer>();
            _quad.sharedMaterial = _material;
            _quad.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Set(0f);
        }

        void OnDestroy() => Services.Unregister(this);

        public void Set(float a)
        {
            _alpha = Mathf.Clamp01(a);
            if (_quad == null) return;
            _quad.enabled = _alpha > 0.001f;
            var c = _color; c.a = _alpha;
            _mpb.SetColor(ColorId, c);
            _quad.SetPropertyBlock(_mpb);
        }

        public IEnumerator FadeTo(float target, float seconds)
        {
            float from = _alpha;
            if (seconds <= 0f) { Set(target); yield break; }
            for (float e = 0; e < seconds; e += Time.unscaledDeltaTime)
            {
                Set(Mathf.Lerp(from, target, e / seconds));
                yield return null;
            }
            Set(target);
        }
    }
}
