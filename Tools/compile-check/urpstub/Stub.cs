// only the URP members ABYSS touches, signatures copied from URP 17.3.0 source
namespace UnityEngine.Rendering.Universal
{
    public class UniversalRenderPipelineAsset : UnityEngine.Rendering.RenderPipelineAsset
    {
        public float renderScale { get; set; }
        public bool supportsSoftShadows { get; set; }
        public UpscalingFilterSelection upscalingFilter { get; set; }
        public static UniversalRenderPipelineAsset Create(ScriptableRendererData rendererData = null) => null;
        protected override UnityEngine.Rendering.RenderPipeline CreatePipeline() => null;
    }
    public class UniversalAdditionalLightData : UnityEngine.MonoBehaviour
    {
        public UnityEngine.Vector2 lightCookieSize { get; set; }
        public UnityEngine.Vector2 lightCookieOffset { get; set; }
    }
}
namespace UnityEngine.Rendering.Universal
{
    public abstract class ScriptableRendererData : UnityEngine.ScriptableObject { }
    public class UniversalRendererData : ScriptableRendererData { }
    public enum AntialiasingMode { None, FastApproximateAntialiasing, SubpixelMorphologicalAntiAliasing, TemporalAntiAliasing }
    public class UniversalAdditionalCameraData : UnityEngine.MonoBehaviour
    {
        public bool renderPostProcessing { get; set; }
        public AntialiasingMode antialiasing { get; set; }
    }
    public partial class UniversalRenderPipelineAssetExt { }
}
namespace UnityEngine.Rendering.Universal
{
    public enum UpscalingFilterSelection { Auto, Linear, Point, FSR, STP }
}
namespace UnityEngine.Rendering.Universal
{
    public class RenderGraphSettings : UnityEngine.Rendering.IRenderPipelineGraphicsSettings
    {
        public bool enableRenderCompatibilityMode { get; set; }
        public int version => 0;
        public bool isAvailableInPlayerBuild => true;
    }
}
