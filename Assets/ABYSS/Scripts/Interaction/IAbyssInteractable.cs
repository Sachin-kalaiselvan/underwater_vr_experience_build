using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Abyss.Interaction
{
    // the contract. anything the visitor can point at and pick goes through this,
    // so InteractionManager never needs to know what the concrete thing is
    public interface IAbyssInteractable
    {
        string Id { get; }
        InteractableDefinition Definition { get; }
        Transform Root { get; }
        bool IsInteractable { get; }

        void SetHighlighted(bool on);
        void OnSelected(IXRInteractor by);
        void ResetToHome();
    }
}
