using Abyss.Core;
using UnityEngine;

namespace Abyss.Locomotion
{
    // marks a spot the menu can send you to. lives in the environment scene,
    // registers with NavigationManager (which lives in bootstrap) once both exist
    public class NavAnchor : MonoBehaviour
    {
        public string Id = "hub";
        public string Label = "Start point";

        void Start() => Services.Get<NavigationManager>()?.RegisterAnchor(Id, Label, transform);

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.9f, 0.8f, 0.5f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, 0.3f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward);
        }
    }
}
