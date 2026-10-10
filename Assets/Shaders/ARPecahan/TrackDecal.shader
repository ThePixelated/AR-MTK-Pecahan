Shader "ARPecahan/TrackDecal"
{
    Properties
    {
        _Denominator ("Denominator", Float) = 4
        _Filled ("Filled Units", Float) = 0
        _TrackLength ("Track Length (m)", Float) = 0.1
        _TrackWidth ("Track Width (m)", Float) = 0.015
        _LineColor ("Line Color", Color) = (1,1,1,1)
        _LineWidth ("Line Width (m)", Float) = 0.0006
        _Style ("Style (0 Solid, 1 Dashed, 2 Texture)", Float) = 0
        _DashLength ("Dash Length (m)", Float) = 0.004
        _DashGap ("Dash Gap (m)", Float) = 0.003
        _TexTile ("Texture Tile (m)", Float) = 0.01
        _Phase ("Animation Phase (m)", Float) = 0
        _EmptyAlpha ("Empty Cell Alpha", Range(0,1)) = 0.08
        _LineTex ("Line Texture", 2D) = "white" {}
        _FilledAlpha ("Filled Cell Alpha (preview)", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            Offset -1, -1

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            float _Denominator, _Filled, _TrackLength, _TrackWidth, _LineWidth, _Style;
            float _DashLength, _DashGap, _TexTile, _Phase, _EmptyAlpha, _FilledAlpha;
            // float _DashLength, _DashGap, _TexTile, _Phase, _EmptyAlpha;
            float4 _LineColor;
            sampler2D _LineTex;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float L = _TrackLength;
                float Wd = _TrackWidth;
                float2 p = float2(i.uv.x * L, i.uv.y * Wd);        // posisi dalam meter, asal di sudut kiri-depan track

                // jarak ke garis tepi
                float dEdgeX = min(p.x, L - p.x);
                float dEdgeZ = min(p.y, Wd - p.y);
                float dEdge = min(dEdgeX, dEdgeZ);

                // jarak ke garis pembagi k/d (k = 1 .. d-1)
                float s = p.x / L * _Denominator;
                float k = floor(s + 0.5);
                float dSection = abs(s - k) * (L / _Denominator);
                float hasSection = (k >= 1.0 && k <= _Denominator - 1.0) ? 1.0 : 0.0;

                float halfW = _LineWidth * 0.5;
                float aaE = max(fwidth(dEdge), 1e-6);
                float aaS = max(fwidth(dSection), 1e-6);
                float onEdge = 1.0 - smoothstep(_LineWidth - aaE, _LineWidth + aaE, dEdge);
                float onSection = hasSection * (1.0 - smoothstep(halfW - aaS, halfW + aaS, dSection));
                float onLine = max(onEdge, onSection);

                // koordinat sepanjang garis dan lintang garis (untuk dashed dan texture)
                bool edgeDominant = onEdge >= onSection;
                float along = edgeDominant ? ((dEdgeZ <= dEdgeX) ? p.x : p.y) : p.y;
                float across = edgeDominant
                    ? saturate(dEdge / _LineWidth)
                    : saturate((s - k) * (L / _Denominator) / _LineWidth + 0.5);
                float t = along + _Phase;

                float cyc = max(_DashLength + _DashGap, 1e-5);
                float m = fmod(t, cyc);
                if (m < 0) m += cyc;
                float dash = step(m, _DashLength);

                float4 tx = tex2D(_LineTex, float2(t / _TexTile, across));

                float3 col = _LineColor.rgb;
                float pattern = 1.0;
                if (_Style > 0.5 && _Style < 1.5) pattern = dash;
                if (_Style > 1.5) { col = tx.rgb * _LineColor.rgb; pattern = tx.a; }

                float lineAlpha = onLine * pattern * _LineColor.a;
                float cellAlpha = (s >= _Filled) ? _EmptyAlpha : _FilledAlpha;   // sel kosong samar; sel terisi hanya tampil di preview
                cellAlpha *= (1.0 - onLine);
                return float4(col, max(lineAlpha, cellAlpha));
            }
            ENDCG
        }
    }
}