using UnityEngine;

namespace Abyss.Interaction
{
    // the content side of an interactable. swap these assets to change what the
    // panels say without touching scenes or code
    [CreateAssetMenu(menuName = "ABYSS/Interactable Definition", fileName = "Interactable_")]
    public class InteractableDefinition : ScriptableObject
    {
        [Tooltip("stable id, activities point at this. don't rename once activities use it")]
        public string Id;
        public string Title;
        [TextArea(1, 2)] public string Subtitle;
        [TextArea(3, 8)] public string Body;
        public Sprite Illustration;

        [Header("Inspection")]
        public bool Inspectable;
        [Tooltip("how big the object gets when held up for inspection, metres across")]
        public float InspectSize = 0.35f;

        [Header("Content flags")]
        [Tooltip("true = placeholder demo text, replace with client material")]
        public bool IsSampleContent = true;

        void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(Id)) Id = name.Replace("Interactable_", "").ToLowerInvariant();
        }
    }
}
