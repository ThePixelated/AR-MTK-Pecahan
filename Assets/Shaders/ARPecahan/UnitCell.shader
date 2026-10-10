Shader "ARPecahan/UnitCellGhost"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _Alpha ("Alpha", Range(0,1)) = 0.5
        _LightDir ("Fake Light Direction", Vector) = (-0.4, 1, -0.3, 0)
        _Ambient ("Ambient", Range(0,1)) = 0.55
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
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

            fixed4 _Color;
            float _Alpha, _Ambient;
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
                float ndl = dot(n, normalize(_LightDir.xyz)) * 0.5 + 0.5;
                float shade = lerp(_Ambient, 1.0, ndl);
                return fixed4(_Color.rgb * shade, _Alpha);
            }
            ENDCG
        }
    }
}