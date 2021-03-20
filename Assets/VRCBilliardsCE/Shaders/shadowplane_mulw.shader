Shader "harry_t/shadowplane_mulw"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
		_ShadowOffset("Shadow Offset", float) = 0
	}
	SubShader
	{
		Tags { "Queue" = "Transparent" "DisableBatching"="true" }

		ZWrite Off
		Cull Off

		Pass
		{
			Blend SrcAlpha OneMinusSrcAlpha

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
				float3 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
			};

			sampler2D _MainTex;
			float _ShadowOffset;

			v2f vert (appdata v)
			{
				v2f o;
				float offset = _ShadowOffset - 0.0025;
				float3 base = mul( unity_ObjectToWorld, float4(0.0,0.0,0.0,1.0) );
				float scale = 1-saturate(abs(base.y - offset) * 15);

				base.y = offset;
				v.vertex.xz *= scale;
				float4 pos_world = float4(v.vertex.xyz + base, 1);

				o.vertex = UnityWorldToClipPos(pos_world);
				o.uv = float3( v.uv, scale );
				return o;
			}

			fixed4 frag (v2f i) : SV_Target
			{
				return tex2D(_MainTex, i.uv.xy) * float4( 1.0, 1.0, 1.0, i.uv.z );
			}
			ENDCG
		}
	}
}
