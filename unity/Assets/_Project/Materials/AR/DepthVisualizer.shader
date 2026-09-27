Shader "Aayam/AR/Debug/DepthVisualizer"
{
    Properties
    {
        _MainTex  ("Depth Texture", 2D) = "white" {}
        _MinDepth ("Min Depth (m)",  Float) = 0.1
        _MaxDepth ("Max Depth (m)",  Float) = 5.0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Overlay" "RenderPipeline"="UniversalPipeline" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always       // Always on top of everything (debug overlay)
        Cull Off

        Pass
        {
            Name "DepthVis"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float _MinDepth;
                float _MaxDepth;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv          = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float depth = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).r;

                // Normalise depth into 0-1 range
                float t = saturate((depth - _MinDepth) / (_MaxDepth - _MinDepth));

                // Heatmap: Red → Green → Blue
                half3 color;
                if (t < 0.5)
                    color = lerp(half3(1, 0, 0), half3(0, 1, 0), t * 2.0);
                else
                    color = lerp(half3(0, 1, 0), half3(0, 0, 1), (t - 0.5) * 2.0);

                // Invalid / zero depth → black
                if (depth < 0.01) color = half3(0, 0, 0);

                return half4(color, 0.7);    // Semi-transparent overlay
            }
            ENDHLSL
        }
    }
}
