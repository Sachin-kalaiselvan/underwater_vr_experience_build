using System;
using UnityEngine;

namespace Abyss.Core
{
    public enum ExperienceState
    {
        Boot,
        Intro,        // identity + onboarding
        MainMenu,
        Exploring,    // free roam, exhibition mode
        Activity,     // guided activity running
        Inspecting,   // holding an object up close
        Paused,
        Completion
    }

    public enum ExperienceMode { Exhibition, Guided }

    // just the state, nothing else. other systems react to the events
    [DefaultExecutionOrder(-300)]
    public class ExperienceStateManager : MonoBehaviour
    {
        [SerializeField] ExperienceState _state = ExperienceState.Boot;
        [SerializeField] ExperienceMode _mode = ExperienceMode.Exhibition;

        ExperienceState _beforePause = ExperienceState.Exploring;

        public ExperienceState State => _state;
        public ExperienceMode Mode => _mode;
        public bool IsPaused => _state == ExperienceState.Paused;

        // locomotion is allowed while roaming or doing an activity
        public bool CanMove => _state == ExperienceState.Exploring || _state == ExperienceState.Activity;

        public event Action<ExperienceState, ExperienceState> StateChanged; // from, to
        public event Action<ExperienceMode> ModeChanged;

        void Awake() => Services.Register(this);
        void OnDestroy() => Services.Unregister(this);

        public void SetMode(ExperienceMode mode)
        {
            if (_mode == mode) return;
            _mode = mode;
            ModeChanged?.Invoke(mode);
        }

        public void SetState(ExperienceState next)
        {
            if (_state == next) return;
            var prev = _state;
            if (next == ExperienceState.Paused) _beforePause = prev;
            _state = next;
            AbyssLog.Info($"state {prev} -> {next}");
            StateChanged?.Invoke(prev, next);
        }

        public void TogglePause()
        {
            if (_state == ExperienceState.Paused) SetState(_beforePause);
            else if (_state != ExperienceState.Boot && _state != ExperienceState.Intro) SetState(ExperienceState.Paused);
        }

        public void Resume()
        {
            if (_state == ExperienceState.Paused) SetState(_beforePause);
        }
    }
}
