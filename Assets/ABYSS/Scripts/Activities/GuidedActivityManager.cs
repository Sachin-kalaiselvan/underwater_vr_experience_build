using System;
using Abyss.Audio;
using Abyss.Core;
using Abyss.Interaction;
using Abyss.Logic;
using Abyss.UI;
using UnityEngine;

namespace Abyss.Activities
{
    // glue between the ActivityRunner (pure logic) and the world:
    // listens to interaction + destination events, lights up the right markers,
    // pushes instructions to the HUD
    public class GuidedActivityManager : MonoBehaviour
    {
        [SerializeField] ActivityCatalog _catalog;

        readonly ActivityRunner _runner = new ActivityRunner();
        ActivityDefinition _current;
        Func<IAbyssInteractable, bool> _relevance;

        public ActivityCatalog Catalog => _catalog;
        public ActivityRunner Runner => _runner;
        public ActivityDefinition Current => _current;
        public bool IsRunning => _runner.State == RunnerState.Running;

        public event Action<ActivityDefinition> Started;
        public event Action<ActivityDefinition> Finished;

        void Awake()
        {
            Services.Register(this);
            _relevance = IsRelevantTarget;
            _runner.StepStarted += OnStepStarted;
            _runner.TargetHit += OnTargetHit;
            _runner.StepCompleted += OnStepCompleted;
            _runner.ActivityCompleted += OnActivityCompleted;
        }

        void OnEnable()
        {
            var im = Services.Get<InteractionManager>();
            if (im != null) Hook(im);
        }

        void Start()
        {
            // in case InteractionManager woke up after us
            var im = Services.Get<InteractionManager>();
            if (im != null) { Unhook(im); Hook(im); }
        }

        void OnDisable()
        {
            var im = Services.Get<InteractionManager>();
            if (im != null) Unhook(im);
            UnhookMarkers();
        }

        void OnDestroy() => Services.Unregister(this);

        void Hook(InteractionManager im)
        {
            im.Selected += OnSelected;
            im.Inspected += OnInspected;
        }

        void Unhook(InteractionManager im)
        {
            im.Selected -= OnSelected;
            im.Inspected -= OnInspected;
            if (im.IsRelevant == _relevance) im.IsRelevant = null;
        }

        public void Begin(ActivityDefinition def)
        {
            Abort();
            var model = def.ToModel();
            if (!model.IsValid(out var problem))
            {
                AbyssLog.Error($"activity '{def.name}' is broken: {problem}");
                Services.Get<ExperienceManager>()?.ShowMainMenu();
                return;
            }
            _current = def;
            var im = Services.Get<InteractionManager>();
            if (im != null) im.IsRelevant = _relevance;
            foreach (var m in DestinationMarker.All) m.Reached += OnMarkerReached;

            Services.Get<ActivityHud>()?.Show(def);
            Started?.Invoke(def);
            _runner.Start(model);
        }

        public void Restart()
        {
            if (_current == null) return;
            var def = _current;
            Begin(def);
            Services.Get<ExperienceStateManager>()?.SetState(ExperienceState.Activity);
        }

        public void Abort()
        {
            if (_current == null) return;
            ClearMarkers();
            UnhookMarkers();
            var im = Services.Get<InteractionManager>();
            if (im != null && im.IsRelevant == _relevance) im.IsRelevant = null;
            _runner.Stop();
            _current = null;
            Services.Get<ActivityHud>()?.Hide();
        }

        bool IsRelevantTarget(IAbyssInteractable i)
        {
            var step = _runner.CurrentStep;
            return step == null || step.TargetIds.Contains(i.Id);
        }

        void OnSelected(IAbyssInteractable i)
        {
            if (!IsRunning) return;
            var step = _runner.CurrentStep;
            // inspect steps only count when the inspection is finished, not on first click
            if (step != null && step.Kind == StepKind.Inspect) return;
            _runner.Report(step != null && step.Kind == StepKind.Interact ? StepKind.Interact : StepKind.Discover, i.Id);
        }

        void OnInspected(IAbyssInteractable i)
        {
            if (IsRunning) _runner.Report(StepKind.Inspect, i.Id);
        }

        void OnMarkerReached(DestinationMarker m)
        {
            if (IsRunning && m.Active) _runner.Report(StepKind.ReachDestination, m.Id);
        }

        void OnStepStarted(StepModel step)
        {
            ClearMarkers();
            if (step.Kind == StepKind.ReachDestination)
                foreach (var id in step.TargetIds) DestinationMarker.Find(id)?.SetActive(true);
            Services.Get<ActivityHud>()?.SetStep(step, _runner.StepIndex, _runner.StepCount, _runner.Progress01);
        }

        void OnTargetHit(StepModel step, string id)
        {
            if (step.Kind == StepKind.ReachDestination) DestinationMarker.Find(id)?.SetActive(false);
            Services.Get<ActivityHud>()?.SetProgress(_runner.Progress01, step, _runner.HitTargets.Count);
        }

        void OnStepCompleted(StepModel step)
        {
            AudioManager.PlayCue(AudioCue.StepComplete, Camera.main != null ? Camera.main.transform.position : Vector3.zero);
            if (!string.IsNullOrEmpty(step.CompletionText)) Services.Get<ActivityHud>()?.Flash(step.CompletionText);
        }

        void OnActivityCompleted(ActivityModel a)
        {
            ClearMarkers();
            UnhookMarkers();
            Services.Get<ProgressTracker>()?.MarkActivityCompleted(a.Id);
            AudioManager.PlayCue(AudioCue.ActivityComplete, Camera.main != null ? Camera.main.transform.position : Vector3.zero);
            Services.Get<ActivityHud>()?.Hide();
            Services.Get<ExperienceStateManager>()?.SetState(ExperienceState.Completion);
            Services.Get<UIManager>()?.ShowCompletion(_current);
            Finished?.Invoke(_current);
        }

        void ClearMarkers()
        {
            foreach (var m in DestinationMarker.All) m.SetActive(false);
        }

        void UnhookMarkers()
        {
            foreach (var m in DestinationMarker.All) m.Reached -= OnMarkerReached;
        }

        // where the visitor should be heading right now, for the HUD arrow
        public bool TryGetGuideTarget(out Vector3 pos)
        {
            pos = default;
            var step = _runner.CurrentStep;
            if (step == null) return false;
            var cam = Camera.main;
            float best = float.MaxValue; bool found = false;
            foreach (var id in step.TargetIds)
            {
                if (_runner.HasHit(id)) continue;
                Vector3 p;
                var m = DestinationMarker.Find(id);
                if (m != null) p = m.transform.position;
                else
                {
                    var i = Services.Get<InteractionManager>()?.Find(id);
                    if (i == null) continue;
                    p = i.Root.position;
                }
                float d = cam != null ? (p - cam.transform.position).sqrMagnitude : 0f;
                if (d < best) { best = d; pos = p; found = true; }
            }
            return found;
        }
    }
}
