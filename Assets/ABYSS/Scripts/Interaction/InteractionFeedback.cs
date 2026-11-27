using System.Collections;
using Abyss.Audio;
using Abyss.Core;
using Abyss.UI;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Abyss.Interaction
{
    // every result gets a visual (always), a sound (if audio is on) and a buzz (if the controller can).
    // visual never relies on colour alone, there's a short text label too
    public class InteractionFeedback : MonoBehaviour
    {
        [SerializeField] float _hoverHaptic = 0.12f;
        [SerializeField] float _successHaptic = 0.35f;
        [SerializeField] float _failHaptic = 0.25f;

        FeedbackToast _toast;
        Coroutine _punch;

        void Awake() => Services.Register(this);
        void OnDestroy() => Services.Unregister(this);

        void Start() => _toast = FeedbackToast.Create(transform);

        bool ReducedMotion => Services.Get<SettingsManager>()?.Current.ReducedMotion ?? false;

        public void Hover(IAbyssInteractable i, IXRInteractor by)
        {
            Buzz(by, _hoverHaptic, 0.03f);
            AudioManager.PlayCue(AudioCue.Hover, i.Root.position);
        }

        public void Success(IAbyssInteractable i, IXRInteractor by)
        {
            Buzz(by, _successHaptic, 0.08f);
            AudioManager.PlayCue(AudioCue.Select, i.Root.position);
            Punch(i.Root, 1.06f);
        }

        public void Fail(IAbyssInteractable i, IXRInteractor by)
        {
            // two short pulses reads as "no" without being harsh
            StartCoroutine(DoubleBuzz(by));
            AudioManager.PlayCue(AudioCue.Fail, i.Root.position);
            _toast?.Show(i.Root.position, "Not part of this task", FeedbackToast.Kind.Neutral);
        }

        public void Discovered(IAbyssInteractable i)
        {
            AudioManager.PlayCue(AudioCue.Discover, i.Root.position);
            _toast?.Show(i.Root.position, "New discovery", FeedbackToast.Kind.Positive);
        }

        public void Complete(IAbyssInteractable i)
        {
            AudioManager.PlayCue(AudioCue.StepComplete, i.Root.position);
            _toast?.Show(i.Root.position, "Done", FeedbackToast.Kind.Positive);
        }

        public void Toast(Vector3 where, string text, bool positive = true) =>
            _toast?.Show(where, text, positive ? FeedbackToast.Kind.Positive : FeedbackToast.Kind.Neutral);

        public static void Buzz(IXRInteractor by, float amp, float dur)
        {
            if (by == null) return;
            var player = by.transform.GetComponentInParent<HapticImpulsePlayer>();
            if (player != null) player.SendHapticImpulse(amp, dur);
        }

        IEnumerator DoubleBuzz(IXRInteractor by)
        {
            Buzz(by, _failHaptic, 0.05f);
            yield return new WaitForSeconds(0.11f);
            Buzz(by, _failHaptic, 0.05f);
        }

        void Punch(Transform t, float amount)
        {
            if (ReducedMotion || t == null) return;
            if (_punch != null) StopCoroutine(_punch);
            _punch = StartCoroutine(DoPunch(t, amount));
        }

        IEnumerator DoPunch(Transform t, float amount)
        {
            var baseScale = t.localScale;
            float d = 0.22f;
            for (float e = 0; e < d; e += Time.deltaTime)
            {
                if (t == null) yield break;
                float k = Mathf.Sin(e / d * Mathf.PI);
                t.localScale = baseScale * Mathf.Lerp(1f, amount, k);
                yield return null;
            }
            if (t != null) t.localScale = baseScale;
            _punch = null;
        }
    }
}
