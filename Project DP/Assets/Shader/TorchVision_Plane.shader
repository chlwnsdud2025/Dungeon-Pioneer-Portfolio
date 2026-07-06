Shader "Custom/TorchVision_Plane"
{
    Properties
    {
        _Color ("Darkness Color", Color) = (0, 0, 0, 1) // 던전 밖 완전한 어둠 색상
        _MaskTex ("Render Texture Mask", 2D) = "black" {} // 렌더 텍스처 삽입구
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+100" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; };

            TEXTURE2D(_MaskTex); SAMPLER(sampler_MaskTex);
            half4 _Color;

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv; 
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                // 서브 카메라가 찍은 횃불 시야(흑백)를 가져옴
                half maskVal = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, i.uv).r;
                // 횃불 빛이 닿은 하얀색(1) 부분은 투명해지고, 안 닿은 검은 부분(0)은 어두워짐
                return half4(_Color.rgb, _Color.a * (1.0 - maskVal));
            }
            ENDHLSL
        }
    }
}