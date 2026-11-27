using Abyss.Activities;
using Abyss.Locomotion;
using Abyss.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Abyss.Core
{
    // top level flow: intro -> menu -> explore / guided -> completion -> back.
    // menus call into here, this calls the other managers. keeps the UI dumb
    public class ExperienceManager : MonoBehaviour
    {
        [SerializeField] ApplicationBootstrap _bootstrap;

        // menu button on the left touch controller, escape for the editor simulator
        InputAction _menuAction;

        ExperienceStateManager State => Services.Get<ExperienceStateManager>();

        void Awake()
        {
            Services.Register(this);
            _menuAction = new InputAction("AbyssMenu", InputActionType.Button);
            _menuAction.AddBinding("<XRController>{LeftHand}/menuButton");
            _menuAction.AddBinding("<XRController>{LeftHand}/menu");
            _menuAction.AddBinding("<Keyboard>/escape");
            _menuAction.performed += _ => OnMenuPressed();
        }

        void OnEnable()
        {
            _menuAction.Enable();
            if (_bootstrap != null) _bootstrap.EnvironmentLoaded += OnEnvironmentLoaded;
        }

        void OnDisable()
        {
            _menuAction.Disable();
            if (_bootstrap != null) _bootstrap.EnvironmentLoaded -= OnEnvironmentLoaded;
        }

        void OnDestroy()
        {
            _menuAction?.Dispose();
            Services.Unregister(this);
        }

        void OnEnvironmentLoaded()
        {
            Services.Get<NavigationManager>()?.SnapToHub();
            var settings = Services.Get<SettingsManager>()?.Current;
            var onboarding = Services.Get<OnboardingSequence>();
            if (onboarding != null && (settings == null || !settings.SkipIntroNextTime))
                onboarding.Play(ShowMainMenu);
            else
                ShowMainMenu();
        }

        void OnMenuPressed()
        {
            var s = State;
            if (s == null) return;
            switch (s.State)
            {
                case ExperienceState.Intro:
                    Services.Get<OnboardingSequence>()?.Skip();
                    break;
                case ExperienceState.Inspecting:
                    Services.Get<Interaction.ObjectInspection>()?.EndInspection();
                    break;
                case ExperienceState.Exploring:
                case ExperienceState.Activity:
                case ExperienceState.Paused:
                    TogglePause();
                    break;
            }
        }

        public void ShowMainMenu()
        {
            Services.Get<GuidedActivityManager>()?.Abort();
            State?.SetState(ExperienceState.MainMenu);
            Services.Get<UIManager>()?.Show(UIScreen.MainMenu);
        }

        // kiosk = exploration that resets itself when the headset comes off or sits idle,
        // so staff don't have to touch anything between visitors
        public bool KioskMode { get; private set; }

        public void StartExhibitionKiosk()
        {
            KioskMode = true;
            StartExploration();
        }

        public void StopKiosk() => KioskMode = false;

        public void StartExploration()
        {
            State?.SetMode(ExperienceMode.Exhibition);
            State?.SetState(ExperienceState.Exploring);
            Services.Get<UIManager>()?.HideAll();
        }

        public void StartGuided(ActivityDefinition activity)
        {
            if (activity == null) { AbyssLog.Warn("no activity picked"); return; }
            State?.SetMode(ExperienceMode.Guided);
            Services.Get<UIManager>()?.HideAll();
            State?.SetState(ExperienceState.Activity);
            Services.Get<GuidedActivityManager>()?.Begin(activity);
        }

        public void TogglePause()
        {
            var s = State;
            if (s == null) return;
            s.TogglePause();
            var ui = Services.Get<UIManager>();
            if (s.IsPaused) ui?.Show(UIScreen.Pause);
            else ui?.HideAll();
        }

        public void Resume()
        {
            State?.Resume();
            Services.Get<UIManager>()?.HideAll();
        }

        public void ReturnToHub()
        {
            Resume();
            Services.Get<NavigationManager>()?.ReturnToHub();
        }

        public void RestartIntro()
        {
            Services.Get<UIManager>()?.HideAll();
            Services.Get<OnboardingSequence>()?.Play(ShowMainMenu);
        }

        public void ResetForNextVisitor()
        {
            Services.Get<ProgressTracker>()?.ResetForNextVisitor();
            Services.Get<Interaction.InteractionManager>()?.ResetAll();
            Services.Get<NavigationManager>()?.ReturnToHub();
            RestartIntro();
        }

        public void ExitApp()
        {
            AbyssLog.Info("exit requested");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
