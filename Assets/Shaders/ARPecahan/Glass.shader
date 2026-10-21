Shader "ARPecahan/Glass"
{
    Properties
    {
        _Tint ("Tint", Color) = (0.85, 0.95, 1, 1)
        _Alpha ("Base Alpha", Range(0,1)) = 0.06
        _RimAlpha ("Rim Alpha", Range(0,1)) = 0.5
        _RimPower ("Rim Power", Range(0.5, 8)) = 3
    }
    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            fixed4 _Tint;
            float _Alpha, _RimAlpha, _RimPower;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f { float4 pos : SV_POSITION; float3 wpos : TEXCOORD0; float3 wnrm : TEXCOORD1; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.wnrm = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 view = normalize(_WorldSpaceCameraPos - i.wpos);
                float ndv = saturate(abs(dot(normalize(i.wnrm), view)));
                float a = lerp(_Alpha, _RimAlpha, pow(1.0 - ndv, _RimPower));   // tepi kaca lebih terlihat
                return fixed4(_Tint.rgb, a);
            }
            ENDCG
        }
    }
}