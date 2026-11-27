using UnityEngine;

namespace Abyss.Environment
{
    // keeps the marine snow particle volume around the visitor. position only, never rotation,
    // and the particles simulate in world space so they don't stick to your face
    public class FollowHead : MonoBehaviour
    {
        [SerializeField] Vector3 _offset;
        Transform _head;

        void LateUpdate()
        {
            if (_head == null)
            {
                var cam = Camera.main;
                if (cam == null) return;
                _head = cam.transform;
            }
            transform.position = _head.position + _offset;
        }
    }
}
