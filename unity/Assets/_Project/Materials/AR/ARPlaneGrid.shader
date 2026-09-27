Shader "Aayam/AR/PlaneGrid"
{
    Properties
    {
        _GridColor      ("Grid Color",      Color)         = (0, 1, 0, 0.5)
        _FillColor      ("Fill Color",      Color)         = (0, 0.5, 0, 0.1)
        _LineThickness  ("Line Thickness",  Range(0.01, 0.1)) = 0.03
        _GridSize       ("Grid Size (m)",   Float)         = 0.25
        _FadeRadius     ("Edge Fade",       Range(0, 1))   = 0.6
        _Pulse          ("Pulse Speed",     Float)         = 0.0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "ARPlaneGrid"

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
                float3 worldPos    : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _GridColor;
                float4 _FillColor;
                float  _LineThickness;
                float  _GridSize;
                float  _FadeRadius;
                float  _Pulse;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv          = IN.uv;
                OUT.worldPos    = TransformObjectToWorld(IN.positionOS.xyz);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // ── Grid lines in world space (avoids stretching on oddly shaped planes)
                float2 gridCoord = IN.worldPos.xz / _GridSize;
                float2 gridFrac  = frac(gridCoord);

                float lineX = step(gridFrac.x, _LineThickness) + step(1.0 - _LineThickness, gridFrac.x);
                float lineY = step(gridFrac.y, _LineThickness) + step(1.0 - _LineThickness, gridFrac.y);
                float isLine = saturate(lineX + lineY);

                // ── Edge fade (radial, UV-based)
                float dist      = distance(IN.uv, float2(0.5, 0.5));
                float fadeMask  = 1.0 - smoothstep(_FadeRadius * 0.4, _FadeRadius, dist);

                // ── Optional pulse animation (for door-candidate highlight)
                float pulse = 1.0;
                if (_Pulse > 0.001)
                    pulse = 0.6 + 0.4 * sin(_Time.y * _Pulse);

                // ── Blend grid lines over fill color
                half4 col = lerp(_FillColor, _GridColor, isLine);
                col.a *= fadeMask * pulse;

                return col;
            }
            ENDHLSL
        }
    }
}
