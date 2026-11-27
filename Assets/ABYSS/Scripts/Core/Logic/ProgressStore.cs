using System;
using System.Collections.Generic;

namespace Abyss.Logic
{
    [Serializable]
    public class ActivityRecord
    {
        public string ActivityId;
        public int TimesCompleted;
        public long LastCompletedUnix;
    }

    [Serializable]
    public class ProgressData
    {
        public List<ActivityRecord> Activities = new List<ActivityRecord>();
        public List<string> Discovered = new List<string>(); // interactable ids found in free exploration
    }

    // keeps track of what's been done. exhibition staff can wipe it between visitors
    public class ProgressStore
    {
        public ProgressData Data { get; private set; } = new ProgressData();
        public event Action Changed;

        public void Load(ProgressData data)
        {
            Data = data ?? new ProgressData();
            if (Data.Activities == null) Data.Activities = new List<ActivityRecord>();
            if (Data.Discovered == null) Data.Discovered = new List<string>();
            Changed?.Invoke();
        }

        public bool IsCompleted(string activityId) => Find(activityId)?.TimesCompleted > 0;

        public void MarkCompleted(string activityId, long unixTime)
        {
            if (string.IsNullOrEmpty(activityId)) return;
            var r = Find(activityId);
            if (r == null) { r = new ActivityRecord { ActivityId = activityId }; Data.Activities.Add(r); }
            r.TimesCompleted++;
            r.LastCompletedUnix = unixTime;
            Changed?.Invoke();
        }

        // returns true the first time something gets found
        public bool MarkDiscovered(string interactableId)
        {
            if (string.IsNullOrEmpty(interactableId) || Data.Discovered.Contains(interactableId)) return false;
            Data.Discovered.Add(interactableId);
            Changed?.Invoke();
            return true;
        }

        public bool IsDiscovered(string id) => Data.Discovered.Contains(id);

        public int CompletedCount
        {
            get { int n = 0; foreach (var a in Data.Activities) if (a.TimesCompleted > 0) n++; return n; }
        }

        public void ResetAll()
        {
            Data = new ProgressData();
            Changed?.Invoke();
        }

        ActivityRecord Find(string id)
        {
            foreach (var a in Data.Activities) if (a.ActivityId == id) return a;
            return null;
        }
    }
}
