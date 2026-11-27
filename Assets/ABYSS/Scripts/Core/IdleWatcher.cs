using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace Abyss.Core
{
    // kiosk helper: when the headset comes off (proximity sensor) or nobody touches anything
    // for a while, reset so the next visitor starts at the intro
    public class IdleWatcher : MonoBehaviour
    {
        [SerializeField] float _idleSeconds = 120f;
        [SerializeField] float _removedSeconds = 8f;

        static readonly List<InputDevice> _devices = new List<InputDevice>();
        float _lastActivity, _removedSince = -1f;
        Vector3 _lastHeadPos;

        void Start() => _lastActivity = Time.unscaledTime;

        void Update()
        {
            var em = Services.Get<ExperienceManager>();
            if (em == null || !em.KioskMode) { _lastActivity = Time.unscaledTime; _removedSince = -1f; return; }

            var cam = Camera.main;
            if (cam != null)
            {
                // small head movement counts as someone being there
                if ((cam.transform.position - _lastHeadPos).sqrMagnitude > 0.0004f) _lastActivity = Time.unscaledTime;
                _lastHeadPos = cam.transform.position;
            }

            if (HeadsetPresent(out bool present) && !present)
            {
                if (_removedSince < 0f) _removedSince = Time.unscaledTime;
                if (Time.unscaledTime - _removedSince > _removedSeconds) Reset(em, "headset removed");
            }
            else _removedSince = -1f;

            if (Time.unscaledTime - _lastActivity > _idleSeconds) Reset(em, "idle");
        }

        void Reset(ExperienceManager em, string why)
        {
            AbyssLog.Info($"kiosk reset ({why})");
            _lastActivity = Time.unscaledTime;
            _removedSince = -1f;
            em.ResetForNextVisitor();
        }

        static bool HeadsetPresent(out bool present)
        {
            present = true;
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.HeadMounted, _devices);
            foreach (var d in _devices)
                if (d.TryGetFeatureValue(CommonUsages.userPresence, out present)) return true;
            return false;
        }
    }
}
