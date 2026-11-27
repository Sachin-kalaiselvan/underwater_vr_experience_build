// soft vertical beam over a destination. vertex alpha fades it out going up
Shader "ABYSS/MarkerBeam"
{
    Properties { _BaseColor ("Colour", Color) = (0.9, 0.84, 0.66, 0.35) }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END
            struct A { float4 p : POSITION; half4 c : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 p : SV_POSITION; half4 c : COLOR; half fog : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            V vert (A a)
            {
                V o; UNITY_SETUP_INSTANCE_ID(a); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.p = TransformObjectToHClip(a.p.xyz); o.c = a.c; o.fog = ComputeFogFactor(o.p.z); return o;
            }
            half4 frag (V i) : SV_Target
            {
                half a = _BaseColor.a * i.c.a * (0.8h + 0.2h * sin(_Time.y * 2.0h));
                return half4(MixFog(_BaseColor.rgb, i.fog) * a, a);
            }
            ENDHLSL
        }
    }
}
