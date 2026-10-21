Shader "ARPecahan/LiquidBody"
{
    Properties
    {
        _Color ("Liquid Color", Color) = (1, 0.82, 0.15, 1)
        _SurfaceBrighten ("Surface Brighten", Range(0,1)) = 0.2
        _LightDir ("Fake Light Direction", Vector) = (-0.4, 1, -0.3, 0)
        _Ambient ("Ambient", Range(0,1)) = 0.7
        _PlanePoint ("Plane Point (world)", Vector) = (0,0,0,0)
        _PlaneNormal ("Plane Normal (world)", Vector) = (0,1,0,0)
    }
    SubShader
    {
        Tags { "Queue"="Geometry+1" "RenderType"="Opaque" }
        Pass
        {
            Cull Off
            ZWrite On

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _SurfaceBrighten, _Ambient;
            float4 _LightDir, _PlanePoint, _PlaneNormal;

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

            fixed4 frag (v2f i, float facing : VFACE) : SV_Target
            {
                // buang bagian di atas permukaan cairan: bidang selalu horizontal di dunia walau gelas miring
                clip(dot(_PlaneNormal.xyz, _PlanePoint.xyz - i.wpos));

                float3 col = _Color.rgb;
                if (facing > 0)
                {
                    float3 n = normalize(i.wnrm);
                    float ndl = dot(n, normalize(_LightDir.xyz)) * 0.5 + 0.5;
                    col *= lerp(_Ambient, 1.0, ndl);
                }
                else
                {
                    col = lerp(col, float3(1, 1, 1), _SurfaceBrighten);   // sisi dalam = permukaan cairan: warna rata, sedikit lebih terang
                }
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
}