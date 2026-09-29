// 던전 밖 미니맵을 찍을 때 모든 물체에 잠깐 씌우는 셰이더. (OutdoorMinimap이 머티리얼을 바꿔 끼움)
// 조명·그림자·안개 없이 재질의 원래 색(_BaseMap × _BaseColor)만 그려서, 낮밤과 상관없이 늘 같은 그림이 나옵니다.
Shader "Hidden/Minimap/CaptureColor"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _Cutoff ("Alpha Cutoff (negative = no clipping)", Float) = -1
        _WorldTiling ("World XZ Tiling (0 = mesh UV)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "MinimapCaptureColor"
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
                float _WorldTiling;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 worldXZ : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.worldXZ = positionWS.xz;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 트라이플래너처럼 월드 좌표로 텍스처를 붙이는 재질은 위에서 본 면(XZ)으로 찍습니다.
                float2 uv = _WorldTiling > 0 ? input.worldXZ * _WorldTiling : input.uv;
                half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor;
                clip(color.a - _Cutoff);
                return half4(color.rgb, 1);
            }
            ENDHLSL
        }
    }
}
