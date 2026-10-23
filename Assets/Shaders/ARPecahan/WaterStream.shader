Shader "ARPecahan/WaterStream"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.82, 0.15, 1)
        _Alpha ("Alpha", Range(0,1)) = 0.9
        _Speed ("Scroll Speed", Float) = 3
        _Freq ("Stripe Frequency", Float) = 8
    }
    SubShader
    {
        Tags { "Queue"="Transparent+5" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Alpha, _Speed, _Freq;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float stripe = 0.5 + 0.5 * sin((i.uv.y * _Freq - _Time.y * _Speed) * 6.2831853);   // garis mengalir sepanjang aliran
                float3 col = lerp(_Color.rgb, float3(1, 1, 1), stripe * 0.25);
                return fixed4(col, _Alpha);
            }
            ENDCG
        }
    }
}