// particles of drifting marine snow. soft round dot, additive, faded by fog
Shader "ABYSS/MarineSnow"
{
    Properties { _BaseColor ("Colour", Color) = (0.85, 0.95, 0.95, 0.5) }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
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
            struct A { float4 p : POSITION; half4 c : COLOR; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 p : SV_POSITION; half4 c : COLOR; float2 uv : TEXCOORD0; half fog : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            V vert (A a)
            {
                V o; UNITY_SETUP_INSTANCE_ID(a); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.p = TransformObjectToHClip(a.p.xyz); o.c = a.c; o.uv = a.uv; o.fog = ComputeFogFactor(o.p.z); return o;
            }
            half4 frag (V i) : SV_Target
            {
                half d = saturate(1.0h - length(i.uv - 0.5h) * 2.0h);
                half a = d * d * _BaseColor.a * i.c.a;
                // fade with fog so far flakes disappear instead of popping
                a *= saturate(i.fog);
                return half4(_BaseColor.rgb * i.c.rgb * a, a);
            }
            ENDHLSL
        }
    }
}
