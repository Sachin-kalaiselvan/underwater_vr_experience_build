// hover highlight. drawn as a second copy of the mesh, pushed out a hair along the normal,
// additive fresnel so it reads as a soft glow on the silhouette without hiding the object
Shader "ABYSS/HighlightRim"
{
    Properties
    {
        _BaseColor ("Colour", Color) = (0.85, 0.80, 0.62, 1)
        _Power ("Rim power", Range(0.5, 8)) = 2.2
        _Strength ("Strength", Range(0, 3)) = 1.1
        _Push ("Push out (m)", Range(0, 0.02)) = 0.004
        _PulseSpeed ("Pulse speed", Range(0, 6)) = 2.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+10" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Rim"
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Power;
                half _Strength;
                float _Push;
                half _PulseSpeed;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 viewWS : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings vert (Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 n = TransformObjectToWorldNormal(v.normalOS);
                float3 p = TransformObjectToWorld(v.positionOS.xyz) + n * _Push;
                o.positionCS = TransformWorldToHClip(p);
                o.normalWS = n;
                o.viewWS = GetWorldSpaceViewDir(p);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half nv = saturate(dot(normalize(i.normalWS), normalize(i.viewWS)));
                half rim = pow(1.0h - nv, _Power);
                half pulse = 0.8h + 0.2h * sin(_Time.y * _PulseSpeed);
                return half4(_BaseColor.rgb * rim * _Strength * pulse, 1);
            }
            ENDHLSL
        }
    }
}
