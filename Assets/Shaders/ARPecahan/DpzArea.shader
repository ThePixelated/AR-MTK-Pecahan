Shader "ARPecahan/DpzArea"
{
    Properties
    {
        _BorderColor ("Border Color", Color) = (1,1,1,0.9)
        _BorderThickness ("Border Thickness (m)", Float) = 0.001
        _FillColor ("Fill Color (alpha = opacity)", Color) = (1,1,1,0.06)
        _HatchColor ("Hatch Color", Color) = (1,1,1,0.25)
        _HatchEnabled ("Hatch Enabled", Float) = 1
        _HatchAngle ("Hatch Angle (deg)", Float) = 45
        _HatchSpacing ("Hatch Spacing (m)", Float) = 0.006
        _HatchWidth ("Hatch Width (m)", Float) = 0.0006
        _HatchDashed ("Hatch Dashed", Float) = 0
        _HatchDashLength ("Hatch Dash Length (m)", Float) = 0.004
        _HatchDashGap ("Hatch Dash Gap (m)", Float) = 0.003
        _MeshUnit ("Mesh Unit (m)", Float) = 1
        _AnchorTop ("Anchor Top (1) / Bottom (0)", Float) = 1
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

            float4 _BorderColor, _FillColor, _HatchColor;
            float _BorderThickness, _HatchEnabled, _HatchAngle, _HatchSpacing, _HatchWidth;
            float _HatchDashed, _HatchDashLength, _HatchDashGap, _MeshUnit, _AnchorTop;

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float2 lp : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.lp = v.vertex.xz;   // posisi lokal: mesh berpusat di origin dengan sisi sepanjang _MeshUnit
                return o;
            }

            // komposisi "top di atas bottom", non-premultiplied
            float4 over (float4 top, float4 bottom)
            {
                float a = top.a + bottom.a * (1.0 - top.a);
                float3 rgb = (top.rgb * top.a + bottom.rgb * bottom.a * (1.0 - top.a)) / max(a, 1e-5);
                return float4(rgb, a);
            }

            float4 frag (v2f i) : SV_Target
            {
                // ukuran area (meter) dari skala objek di dunia
                float sx = length(float3(unity_ObjectToWorld[0].x, unity_ObjectToWorld[1].x, unity_ObjectToWorld[2].x)) * _MeshUnit;
                float sz = length(float3(unity_ObjectToWorld[0].z, unity_ObjectToWorld[1].z, unity_ObjectToWorld[2].z)) * _MeshUnit;

                // posisi dalam meter, diukur dari sudut anchor (kiri; atas atau bawah sesuai arah tumbuh)
                float u = i.lp.x / _MeshUnit + 0.5;
                float w = i.lp.y / _MeshUnit + 0.5;
                float2 p = float2(u * sx, (_AnchorTop > 0.5 ? 1.0 - w : w) * sz);

                // garis tepi
                float dEdge = min(min(p.x, sx - p.x), min(p.y, sz - p.y));
                float aaB = max(fwidth(dEdge), 1e-6);
                float borderMask = 1.0 - smoothstep(_BorderThickness - aaB, _BorderThickness + aaB, dEdge);

                // arsir: garis bersudut, diukur dari anchor sehingga polanya tidak bergeser saat area mengembang
                float ang = radians(_HatchAngle);
                float2 dir = float2(cos(ang), sin(ang));
                float2 nrm = float2(-dir.y, dir.x);
                float spacing = max(_HatchSpacing, 1e-4);
                float c = dot(p, nrm);
                float cm = fmod(c, spacing);
                if (cm < 0) cm += spacing;
                float dLine = min(cm, spacing - cm);
                float aaH = max(fwidth(dLine), 1e-6);
                float hatchMask = 1.0 - smoothstep(_HatchWidth * 0.5 - aaH, _HatchWidth * 0.5 + aaH, dLine);

                if (_HatchDashed > 0.5)
                {
                    float cyc = max(_HatchDashLength + _HatchDashGap, 1e-5);
                    float lineIdx = floor(c / spacing + 0.5);
                    float a = dot(p, dir) + lineIdx * cyc * 0.5;   // geser setengah siklus tiap garis (pola bata)
                    float am = fmod(a, cyc);
                    if (am < 0) am += cyc;
                    hatchMask *= step(am, _HatchDashLength);
                }

                float4 fill = _FillColor;
                float4 hatch = float4(_HatchColor.rgb, _HatchColor.a * hatchMask * _HatchEnabled);
                float4 border = float4(_BorderColor.rgb, _BorderColor.a * borderMask);
                return over(border, over(hatch, fill));
            }
            ENDCG
        }
    }
}