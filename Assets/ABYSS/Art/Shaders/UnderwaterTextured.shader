// same lighting as UnderwaterVertexLit but for the photoscans: albedo + normal map,
// vertex colour on top as a weathering tint (algae, silt, waterlogged wood)
Shader "ABYSS/UnderwaterTextured"
{
    Properties
    {
        _BaseMap ("Albedo", 2D) = "white" {}
        [Normal] _BumpMap ("Normal (OpenGL)", 2D) = "bump" {}
        _BumpScale ("Normal strength", Range(0,2)) = 1
        _BaseColor ("Tint", Color) = (1,1,1,1)
        _Wrap ("Wrap lighting", Range(0,1)) = 0.3
        _Spec ("Wet sheen", Range(0,1)) = 0.12
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _LIGHT_COOKIES
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _BumpScale, _Wrap, _Spec;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; float2 uv : TEXCOORD0; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half4 tangentWS : TEXCOORD3;
                half4 color : COLOR;
                half fog : TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                VertexNormalInputs n = GetVertexNormalInputs(v.normalOS, v.tangentOS);
                o.positionCS = p.positionCS; o.positionWS = p.positionWS;
                o.normalWS = n.normalWS;
                o.tangentWS = half4(n.tangentWS, v.tangentOS.w * GetOddNegativeScale());
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.color = v.color;
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb * _BaseColor.rgb * i.color.rgb;
                half3 nTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uv), _BumpScale);
                half3 bitan = cross(i.normalWS, i.tangentWS.xyz) * i.tangentWS.w;
                half3 n = normalize(TransformTangentToWorld(nTS, half3x3(i.tangentWS.xyz, bitan, i.normalWS)));

                Light sun = GetMainLight();
                #if defined(_LIGHT_COOKIES)
                    sun.color *= SampleMainLightCookie(i.positionWS);
                #endif
                half ndl = saturate((dot(n, sun.direction) + _Wrap) / (1.0h + _Wrap));
                half3 v = normalize(GetWorldSpaceViewDir(i.positionWS));
                half spec = pow(saturate(dot(n, normalize(sun.direction + v))), 48.0h) * _Spec;
                half3 col = albedo * (sun.color * ndl + SampleSH(n)) + sun.color * spec * ndl;
                col = MixFog(col, i.fog);
                return half4(col, 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex v
            #pragma fragment f
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST; half4 _BaseColor; half _BumpScale, _Wrap, _Spec;
            CBUFFER_END
            struct A { float4 p : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 p : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };
            V v (A a) { V o; UNITY_SETUP_INSTANCE_ID(a); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.p = TransformObjectToHClip(a.p.xyz); return o; }
            half4 f (V i) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
