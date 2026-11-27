// the one shader most of the world uses. colour comes from vertex colours baked in blender,
// lit by the main light (which carries the caustic cookie) + ambient SH, then fog.
// no textures, no normal maps, one pass, srp batcher friendly. cheap enough for quest
Shader "ABYSS/UnderwaterVertexLit"
{
    Properties
    {
        _BaseColor ("Tint", Color) = (1,1,1,1)
        _Wrap ("Wrap lighting", Range(0,1)) = 0.35
        _Glow ("Glow from bright vertex colours", Range(0,3)) = 0
        _GlowColor ("Glow colour", Color) = (0.44, 0.88, 0.82, 1)
        _GlowPulse ("Glow pulse speed", Range(0,3)) = 0.6
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull (Off for kelp, grass, fans)", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _LIGHT_COOKIES
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Wrap;
                half _Glow;
                half4 _GlowColor;
                half _GlowPulse;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 color : COLOR;
                half fog : TEXCOORD2;
                half3 sh : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.color = v.color;
                o.fog = ComputeFogFactor(p.positionCS.z);
                o.sh = SampleSH(o.normalWS);
                return o;
            }

            half4 frag (Varyings i, bool front : SV_IsFrontFace) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half3 n = normalize(i.normalWS) * (front ? 1.0h : -1.0h);
                Light sun = GetMainLight();
                #if defined(_LIGHT_COOKIES)
                    sun.color *= SampleMainLightCookie(i.positionWS);
                #endif
                half ndl = saturate((dot(n, sun.direction) + _Wrap) / (1.0h + _Wrap));
                half3 albedo = i.color.rgb * _BaseColor.rgb;
                half3 col = albedo * (sun.color * ndl + i.sh);
                // bioluminescence: brightest parts of the vertex colour glow a little, slowly breathing
                half lum = dot(i.color.rgb, half3(0.3h, 0.59h, 0.11h));
                half pulse = 0.75h + 0.25h * sin(_Time.y * _GlowPulse + i.positionWS.x * 0.7h);
                col += _GlowColor.rgb * _Glow * saturate(lum * 2.0h - 0.6h) * pulse;
                col = MixFog(col, i.fog);
                return half4(col, 1);
            }
            ENDHLSL
        }
        // depth only, so the srp batcher and depth prepass (if ever enabled) behave
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R
            Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex v
            #pragma fragment f
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor; half _Wrap; half _Glow; half4 _GlowColor; half _GlowPulse;
            CBUFFER_END
            struct A { float4 p : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 p : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };
            V v (A a) { V o; UNITY_SETUP_INSTANCE_ID(a); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.p = TransformObjectToHClip(a.p.xyz); return o; }
            half4 f (V i) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
