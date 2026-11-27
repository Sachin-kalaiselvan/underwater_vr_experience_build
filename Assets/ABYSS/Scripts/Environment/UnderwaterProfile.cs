using UnityEngine;

namespace Abyss.Environment
{
    // the look of one area: fog, ambient, sun. EnvironmentManager blends between these
    // as the visitor moves, so going deeper is a gradual change not a hard cut
    [CreateAssetMenu(menuName = "ABYSS/Underwater Profile", fileName = "Underwater_")]
    public class UnderwaterProfile : ScriptableObject
    {
        public Color FogColor = new Color(0.05f, 0.28f, 0.36f);
        [Range(0f, 0.2f)] public float FogDensity = 0.035f;

        [Header("Ambient (trilight)")]
        public Color AmbientSky = new Color(0.30f, 0.55f, 0.60f);
        public Color AmbientEquator = new Color(0.12f, 0.30f, 0.36f);
        public Color AmbientGround = new Color(0.06f, 0.12f, 0.14f);

        [Header("Sun from the surface")]
        public Color SunColor = new Color(0.75f, 0.92f, 0.95f);
        [Range(0f, 3f)] public float SunIntensity = 1.1f;
        [Range(0f, 1f)] public float CausticStrength = 1f;

        [Header("Safety")]
        [Tooltip("ambient never goes below this, so the deep area stays navigable")]
        [Range(0f, 1f)] public float MinAmbientLuma = 0.06f;
    }
}
