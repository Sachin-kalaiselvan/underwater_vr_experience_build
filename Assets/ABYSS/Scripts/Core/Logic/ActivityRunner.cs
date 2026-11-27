using System;
using System.Collections.Generic;

namespace Abyss.Logic
{
    public enum RunnerState { Idle, Running, Completed }

    // drives one activity. feed it events (object selected, destination reached etc)
    // and it moves through the steps. no unity stuff in here on purpose so it can be unit tested
    public class ActivityRunner
    {
        public ActivityModel Activity { get; private set; }
        public RunnerState State { get; private set; } = RunnerState.Idle;
        public int StepIndex { get; private set; } = -1;

        readonly HashSet<string> _hitTargets = new HashSet<string>();

        public event Action<StepModel> StepStarted;
        public event Action<StepModel, string> TargetHit;     // step, target id
        public event Action<StepModel> StepCompleted;
        public event Action<ActivityModel> ActivityCompleted;
        public event Action<string> WrongTarget;              // id that doesn't belong to the current step

        public StepModel CurrentStep =>
            State == RunnerState.Running && StepIndex >= 0 && StepIndex < Activity.Steps.Count
                ? Activity.Steps[StepIndex] : null;

        public int StepCount => Activity?.Steps.Count ?? 0;

        public IReadOnlyCollection<string> HitTargets => _hitTargets;
        public bool HasHit(string id) => _hitTargets.Contains(id);

        public void Start(ActivityModel activity)
        {
            if (activity == null) throw new ArgumentNullException(nameof(activity));
            if (!activity.IsValid(out var problem)) throw new InvalidOperationException(problem);

            Activity = activity;
            State = RunnerState.Running;
            StepIndex = -1;
            Advance();
        }

        public void Stop()
        {
            State = RunnerState.Idle;
            StepIndex = -1;
            _hitTargets.Clear();
            Activity = null;
        }

        public void Restart()
        {
            var a = Activity;
            if (a == null) return;
            Stop();
            Start(a);
        }

        // returns true if the event counted for something
        public bool Report(StepKind kind, string targetId)
        {
            var step = CurrentStep;
            if (step == null || string.IsNullOrEmpty(targetId)) return false;

            // Discover steps also accept Inspect events, inspecting something implies you found it
            bool kindOk = step.Kind == kind || (step.Kind == StepKind.Discover && kind == StepKind.Inspect);
            if (!kindOk || !step.TargetIds.Contains(targetId))
            {
                // only flag as wrong when it was a deliberate selection, not a walk-through
                if (kind != StepKind.ReachDestination) WrongTarget?.Invoke(targetId);
                return false;
            }

            if (!_hitTargets.Add(targetId)) return false; // already counted

            TargetHit?.Invoke(step, targetId);

            bool done = step.RequireAllTargets ? AllHit(step) : true;
            if (done)
            {
                StepCompleted?.Invoke(step);
                Advance();
            }
            return true;
        }

        public float Progress01
        {
            get
            {
                if (Activity == null) return 0f;
                if (State == RunnerState.Completed) return 1f;
                int total = 0, hit = 0;
                for (int i = 0; i < Activity.Steps.Count; i++)
                {
                    var s = Activity.Steps[i];
                    int need = s.RequireAllTargets ? s.TargetIds.Count : 1;
                    total += need;
                    if (i < StepIndex) hit += need;
                    else if (i == StepIndex) hit += Math.Min(need, CountHit(s));
                }
                return total == 0 ? 0f : (float)hit / total;
            }
        }

        bool AllHit(StepModel s)
        {
            foreach (var t in s.TargetIds) if (!_hitTargets.Contains(t)) return false;
            return true;
        }

        int CountHit(StepModel s)
        {
            int n = 0;
            foreach (var t in s.TargetIds) if (_hitTargets.Contains(t)) n++;
            return n;
        }

        void Advance()
        {
            _hitTargets.Clear();
            StepIndex++;
            if (StepIndex >= Activity.Steps.Count)
            {
                State = RunnerState.Completed;
                ActivityCompleted?.Invoke(Activity);
                return;
            }
            StepStarted?.Invoke(Activity.Steps[StepIndex]);
        }
    }
}
