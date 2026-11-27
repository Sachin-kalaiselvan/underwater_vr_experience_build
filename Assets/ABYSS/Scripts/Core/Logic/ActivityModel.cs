using System;
using System.Collections.Generic;

namespace Abyss.Logic
{
    // what the visitor has to do to clear a step
    public enum StepKind
    {
        Discover,          // select one or more target objects
        Inspect,           // open an object in inspection mode and finish it
        ReachDestination,  // walk/teleport to a marker
        Interact           // generic "use this thing" step
    }

    // plain data copy of a step so the runner doesn't care about ScriptableObjects
    [Serializable]
    public class StepModel
    {
        public string Id;
        public StepKind Kind;
        public string Instruction;
        public string CompletionText;
        public List<string> TargetIds = new List<string>();
        // true = all targets needed (any order), false = any one target clears it
        public bool RequireAllTargets = true;
    }

    [Serializable]
    public class ActivityModel
    {
        public string Id;
        public string Title;
        public string Intro;
        public string Summary;
        public List<StepModel> Steps = new List<StepModel>();

        public bool IsValid(out string problem)
        {
            if (string.IsNullOrWhiteSpace(Id)) { problem = "activity has no id"; return false; }
            if (Steps == null || Steps.Count == 0) { problem = $"activity '{Id}' has no steps"; return false; }
            var seen = new HashSet<string>();
            foreach (var s in Steps)
            {
                if (s == null) { problem = $"activity '{Id}' has an empty step slot"; return false; }
                if (string.IsNullOrWhiteSpace(s.Id) || !seen.Add(s.Id)) { problem = $"activity '{Id}' has a missing or duplicate step id '{s.Id}'"; return false; }
                if (s.TargetIds == null || s.TargetIds.Count == 0) { problem = $"step '{s.Id}' has no targets"; return false; }
            }
            problem = null;
            return true;
        }
    }
}
