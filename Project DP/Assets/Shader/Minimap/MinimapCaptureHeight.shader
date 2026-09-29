// 던전 밖 미니맵을 찍을 때 쓰는 높이 셰이더. 픽셀마다 월드 높이를 적어 두면, 이웃과 높이 차이가 큰 곳에
// 외곽선을 그어 나무·집·언덕을 바닥과 구분합니다. (OutdoorMinimap이 머티리얼을 바꿔 끼움)
// 높이는 _MinimapHeightRange(x = 최저 높이, y = 1 / 높이 폭) 기준 0~1을 16비트로 나눠 R(상위)·G(하위)에 적습니다.
Shader "Hidden/Minimap/CaptureHeight"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _Cutoff ("Alpha Cutoff (negative = no clipping)", Float) = -1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "MinimapCaptureHeight"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Cutoff;
            CBUFFER_END

            float4 _MinimapHeightRange;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float heightWS : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.heightWS = positionWS.y;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a;
                clip(alpha - _Cutoff);

                float h = saturate((input.heightWS - _MinimapHeightRange.x) * _MinimapHeightRange.y);
                float value = floor(h * 65535.0 + 0.5);
                float high = floor(value / 256.0);
                float low = value - high * 256.0;
                return float4(high / 255.0, low / 255.0, 0, 1);
            }
            ENDHLSL
        }
    }
}
