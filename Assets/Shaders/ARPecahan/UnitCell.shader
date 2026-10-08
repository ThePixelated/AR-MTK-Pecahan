Shader "ARPecahan/UnitCell"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _Highlight ("Highlight", Range(0,1)) = 0
        _HighlightBrighten ("Highlight Brighten", Range(0,1)) = 0.4
        _LightDir ("Fake Light Direction", Vector) = (-0.4, 1, -0.3, 0)
        _Ambient ("Ambient", Range(0,1)) = 0.55
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Cull Back
            ZWrite On

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Highlight, _HighlightBrighten, _Ambient;
            float4 _LightDir;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f { float4 pos : SV_POSITION; float3 nrm : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.nrm = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 n = normalize(i.nrm);
                float ndl = dot(n, normalize(_LightDir.xyz)) * 0.5 + 0.5;   // half-lambert, tidak ikut cahaya scene
                float shade = lerp(_Ambient, 1.0, ndl);
                float3 col = _Color.rgb * shade;
                col = lerp(col, float3(1, 1, 1), _Highlight * _HighlightBrighten);
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
}