using System.Collections.Generic;
using UnityEngine;

namespace Abyss.Environment
{
    // a box in the world that says "this is the reef" etc. soft edges so profiles blend
    public class AreaZone : MonoBehaviour
    {
        public static readonly List<AreaZone> All = new List<AreaZone>();

        public string Id;
        public string DisplayName;
        public UnderwaterProfile Profile;
        public Vector3 Size = new Vector3(30, 20, 30);
        [Tooltip("metres over which this zone fades in at its edges")]
        public float Blend = 6f;
        public int Priority;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        // 1 deep inside, 0 outside, smooth in between
        public float Weight(Vector3 worldPos)
        {
            var p = transform.InverseTransformPoint(worldPos);
            var h = Size * 0.5f;
            float dx = Mathf.Abs(p.x) - h.x, dy = Mathf.Abs(p.y) - h.y, dz = Mathf.Abs(p.z) - h.z;
            float outside = Mathf.Max(dx, Mathf.Max(dy, dz)); // negative = inside
            if (Blend <= 0f) return outside <= 0f ? 1f : 0f;
            return Mathf.Clamp01(-outside / Blend + 0.5f);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.3f, 0.7f, 0.8f, 0.25f);
            Gizmos.DrawWireCube(Vector3.zero, Size);
        }
    }
}
