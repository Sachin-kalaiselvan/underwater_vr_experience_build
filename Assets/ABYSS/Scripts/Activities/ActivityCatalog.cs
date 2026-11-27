using System.Collections.Generic;
using UnityEngine;

namespace Abyss.Activities
{
    // the list the guided menu shows. reorder or swap entries here to change the curriculum
    [CreateAssetMenu(menuName = "ABYSS/Activity Catalog", fileName = "ActivityCatalog")]
    public class ActivityCatalog : ScriptableObject
    {
        public List<ActivityDefinition> Activities = new List<ActivityDefinition>();

        public ActivityDefinition Find(string id)
        {
            foreach (var a in Activities) if (a != null && a.Id == id) return a;
            return null;
        }
    }
}
