Shader "harry_t/sgamefill"
{
    Properties
    {
        _Colour ("Color", Color) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderQueue"="Transparent" }
		  Cull Off
		  ZWrite Off
		  ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

				fixed4 _Colour;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = float4( v.vertex.xy*2.0, UNITY_NEAR_CLIP_VALUE, 1.0 );
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return _Colour;
            }
            ENDCG
        }
    }
}
