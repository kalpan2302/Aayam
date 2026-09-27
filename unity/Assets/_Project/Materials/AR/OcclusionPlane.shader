Shader "Aayam/AR/OcclusionPlane"
{
    Properties
    {
        // No visual properties — this shader writes to Z-buffer only
    }

    SubShader
    {
        // Render before standard geometry so it writes depth first
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry-1" }
        LOD 100

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="UniversalForward" }

            ZWrite On
            ZTest LEqual
            ColorMask 0       // Invisible — writes Z only
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return half4(0, 0, 0, 0);   // Color doesn't matter — ColorMask 0
            }
            ENDHLSL
        }
    }
}
