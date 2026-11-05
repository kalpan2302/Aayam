// Phase 6 Step 4: shadow-only receiver for a real-world surface (the
// door plane) in AR. No URP/HDRP installed in this project (confirmed) -
// this targets the Built-in Render Pipeline directly via CGPROGRAM, not
// Shader Graph. Renders fully transparent everywhere except where a
// real-time shadow falls, so the AR camera feed shows through underneath.
Shader "Aayam/ARShadowReceiver"
{
    Properties
    {
        _ShadowColor ("Shadow Color", Color) = (0, 0, 0, 0.5)
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }

        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase

            #include "UnityCG.cginc"
            #include "AutoLight.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                SHADOW_COORDS(0)
            };

            fixed4 _ShadowColor;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                TRANSFER_SHADOW(o);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed shadowAttenuation = SHADOW_ATTENUATION(i);
                fixed alpha = (1 - shadowAttenuation) * _ShadowColor.a;
                return fixed4(_ShadowColor.rgb, alpha);
            }
            ENDCG
        }
    }
}
