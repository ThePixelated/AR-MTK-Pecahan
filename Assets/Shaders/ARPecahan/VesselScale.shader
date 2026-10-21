Shader "ARPecahan/VesselScale"
{
    Properties
    {
        _Color ("Line Color", Color) = (0.1, 0.1, 0.1, 0.85)
        _FloorY ("Floor Y (local)", Float) = 0
        _TopY ("Full Y (local)", Float) = 1
        _Divisions ("Divisions", Float) = 4
        _Step ("Line Step", Float) = 1
        _LineWidth ("Line Width (fraction of depth)", Float) = 0.006
        _TopLine ("Top Line (0/1)", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
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
            float _FloorY, _TopY, _Divisions, _Step, _LineWidth, _TopLine;

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float ly : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.ly = v.vertex.y;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float depth = max(_TopY - _FloorY, 1e-6);
                float k = (i.ly - _FloorY) / depth * _Divisions;       // posisi dalam satuan "bagian"
                float kr = floor(k + 0.5);
                float halfW = _LineWidth * 0.5 * _Divisions;           // setengah tebal garis, dalam satuan bagian
                float aa = max(fwidth(k), 1e-5);
                float on = 1.0 - smoothstep(halfW - aa, halfW + aa, abs(k - kr));

                float isLine = (kr >= 1.0 && kr <= _Divisions - 1.0 && fmod(kr, max(_Step, 1.0)) < 0.5) ? 1.0 : 0.0;
                if (_TopLine > 0.5 && abs(kr - _Divisions) < 0.5) isLine = 1.0;

                float a = on * isLine * _Color.a;
                clip(a - 0.003);
                return fixed4(_Color.rgb, a);
            }
            ENDCG
        }
    }
}