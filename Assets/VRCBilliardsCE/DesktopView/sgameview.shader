Shader "harry_t/sgameview"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
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

            sampler2D _MainTex;

            v2f vert (appdata v)
            {
                v2f o;

					 float ratio = (_ScreenParams.x / _ScreenParams.y)*-0.56249999999;

                o.vertex = float4( v.vertex.x*2, v.vertex.y*2*ratio, UNITY_NEAR_CLIP_VALUE, 1.0 );
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                return col;
            }
            ENDCG
        }
    }
}
