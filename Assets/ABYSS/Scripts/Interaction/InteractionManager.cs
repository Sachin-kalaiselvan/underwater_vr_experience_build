using System;
using System.Collections.Generic;
using Abyss.Core;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Abyss.Interaction
{
    // knows about every interactable, decides what happens when one gets picked.
    // activities listen to the events here instead of hooking objects directly
    [DefaultExecutionOrder(-250)]
    public class InteractionManager : MonoBehaviour
    {
        readonly Dictionary<string, IAbyssInteractable> _byId = new Dictionary<string, IAbyssInteractable>();

        public IAbyssInteractable Current { get; private set; }

        public event Action<IAbyssInteractable> Hovered;
        public event Action<IAbyssInteractable> Selected;
        public event Action<IAbyssInteractable> Discovered;   // first time ever this session
        public event Action<IAbyssInteractable> Inspected;    // finished an inspection
        public event Action<IAbyssInteractable> Closed;       // info panel dismissed

        // a guided activity can set this to show "not this one" feedback for off-task objects
        public Func<IAbyssInteractable, bool> IsRelevant;

        void Awake() => Services.Register(this);
        void OnDestroy() => Services.Unregister(this);

        public void Register(IAbyssInteractable i)
        {
            if (i == null) return;
            if (_byId.TryGetValue(i.Id, out var other) && !ReferenceEquals(other, i))
                AbyssLog.Warn($"two interactables share id '{i.Id}', the second one wins");
            _byId[i.Id] = i;
        }

        public void Unregister(IAbyssInteractable i)
        {
            if (i != null && _byId.TryGetValue(i.Id, out var cur) && ReferenceEquals(cur, i)) _byId.Remove(i.Id);
        }

        public IAbyssInteractable Find(string id) => id != null && _byId.TryGetValue(id, out var i) ? i : null;

        public IEnumerable<IAbyssInteractable> All => _byId.Values;

        bool InputAllowed
        {
            get
            {
                var s = Services.Get<ExperienceStateManager>();
                return s == null || s.State == ExperienceState.Exploring || s.State == ExperienceState.Activity;
            }
        }

        public void NotifyHover(IAbyssInteractable i, bool entered, IXRInteractor by)
        {
            if (entered && !InputAllowed) return;
            i.SetHighlighted(entered);
            if (entered)
            {
                Services.Get<InteractionFeedback>()?.Hover(i, by);
                Hovered?.Invoke(i);
            }
        }

        public void NotifySelect(IAbyssInteractable i, IXRInteractor by)
        {
            if (!InputAllowed || i == null) return;

            var fb = Services.Get<InteractionFeedback>();
            if (IsRelevant != null && !IsRelevant(i))
            {
                fb?.Fail(i, by);
                // still show info, exploring off-task is fine, it just doesn't count
            }
            else
            {
                fb?.Success(i, by);
            }

            Current = i;
            i.OnSelected(by);
            Selected?.Invoke(i);

            var progress = Services.Get<ProgressTracker>();
            if (progress != null && progress.MarkDiscovered(i.Id))
            {
                fb?.Discovered(i);
                Discovered?.Invoke(i);
            }

            var info = Services.Get<InformationDisplay>();
            if (info != null && i.Definition != null)
            {
                info.Show(i, i.Definition.Inspectable
                    ? (Action)(() => Services.Get<ObjectInspection>()?.Begin(i, by))
                    : null,
                    () => Close(i));
            }
        }

        public void NotifyInspected(IAbyssInteractable i)
        {
            Inspected?.Invoke(i);
            Services.Get<InteractionFeedback>()?.Complete(i);
        }

        public void Close(IAbyssInteractable i)
        {
            if (i != null) i.SetHighlighted(false);
            if (ReferenceEquals(Current, i)) Current = null;
            Services.Get<InformationDisplay>()?.Hide();
            Closed?.Invoke(i);
        }

        // back to how things looked at launch, used between visitors
        public void ResetAll()
        {
            Services.Get<ObjectInspection>()?.EndInspection();
            foreach (var i in _byId.Values) i.ResetToHome();
            Close(Current);
        }
    }
}
