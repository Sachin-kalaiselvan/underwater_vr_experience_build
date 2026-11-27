using System;
using System.Collections.Generic;
using Abyss.Logic;
using UnityEngine;

namespace Abyss.Activities
{
    // one activity = an asset. content people edit these, nobody touches code.
    // targets are interactable ids / destination ids, not scene references, so
    // the same activity works if the scene gets rebuilt
    [CreateAssetMenu(menuName = "ABYSS/Activity Definition", fileName = "Activity_")]
    public class ActivityDefinition : ScriptableObject
    {
        [Serializable]
        public class Step
        {
            public string Id;
            public StepKind Kind;
            [TextArea(1, 3)] public string Instruction;
            [TextArea(1, 3)] public string CompletionText;
            public List<string> TargetIds = new List<string>();
            public bool RequireAllTargets = true;
        }

        public string Id;
        public string Title;
        [TextArea(2, 4)] public string Intro;
        [TextArea(2, 4)] public string Summary;
        [Tooltip("roughly how long it takes, shown on the menu")]
        public int Minutes = 3;
        public bool IsSampleContent = true;
        public List<Step> Steps = new List<Step>();

        public ActivityModel ToModel()
        {
            var m = new ActivityModel { Id = Id, Title = Title, Intro = Intro, Summary = Summary };
            foreach (var s in Steps)
            {
                m.Steps.Add(new StepModel
                {
                    Id = s.Id,
                    Kind = s.Kind,
                    Instruction = s.Instruction,
                    CompletionText = s.CompletionText,
                    TargetIds = new List<string>(s.TargetIds),
                    RequireAllTargets = s.RequireAllTargets
                });
            }
            return m;
        }

        void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(Id)) Id = name.Replace("Activity_", "").ToLowerInvariant();
            if (!ToModel().IsValid(out var problem)) Debug.LogWarning($"[ABYSS] {name}: {problem}", this);
        }
    }
}
