Shader "harry_t/scorecard"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
		_Info("Info", Vector) = (0,0,0,0)

		[HDR] _Colour0("Colour 0", Color) = (1,1,1,1)
		[HDR] _Colour1("Colour 1", Color) = (1,1,1,1)
	}
	SubShader
	{
		Tags { "Queue" = "Transparent" }
		
		ZWrite Off
		Cull Off

		Pass
		{
			Blend One One

			CGPROGRAM

			#pragma vertex vert
			#pragma fragment frag
			// make fog work
			#pragma multi_compile_fog

			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				UNITY_FOG_COORDS(1)
				float4 vertex : SV_POSITION;
			};

			sampler2D _MainTex;
			float4 _Info;

			float4 _Colour0;
			float4 _Colour1;

			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				UNITY_TRANSFER_FOG(o,o.vertex);
				return o;
			}

			fixed4 frag (v2f i) : SV_Target
			{
				fixed4 infmap = tex2D(_MainTex, i.uv);
				fixed4 wterm = fixed4( infmap.r, infmap.r, infmap.r, 1.0 );
				fixed4 colourterm = (step(i.uv.x - _Info.x, 0.03125) * _Colour0 + step(1.0-i.uv.x - _Info.y, 0.03125) * _Colour1) * infmap.g;

				// apply fog
				//UNITY_APPLY_FOG(i.fogCoord, col);

				return wterm + colourterm;
			}

			ENDCG
		}
	}
}
