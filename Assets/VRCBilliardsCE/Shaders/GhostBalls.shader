Shader "VRCBCE/Ghost Balls"
{
	Properties
	{
		[HDR] _Color("Main Color", Color) = (1,1,1,1)
		_FresnelBias ("Fresnel Bias", Float) = 0
		_FresnelScale ("Fresnel Scale", Float) = 1
		_FresnelPower ("Fresnel Power", Float) = 1
	}

	SubShader
	{
		Tags { "Queue" = "Transparent" }

		ZWrite Off

		Pass
		{
			Blend One One

			CGPROGRAM

			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_instancing
			#pragma instancing_options nolightprobe nolightmap
			#include "UnityCG.cginc"

			struct appdata_t
			{
				float4 pos : POSITION;
				half3 normal : NORMAL;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct v2f
			{
				float4 pos : SV_POSITION;
				float fresnel : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			UNITY_INSTANCING_BUFFER_START(Props)
			UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
			UNITY_INSTANCING_BUFFER_END(Props)
			fixed _FresnelBias;
			fixed _FresnelScale;
			fixed _FresnelPower;

			v2f vert(appdata_t v)
			{
				v2f o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				o.pos = UnityObjectToClipPos(v.pos);

				float3 i = normalize(ObjSpaceViewDir(v.pos));
				o.fresnel = _FresnelBias + _FresnelScale * pow(1 + dot(i, v.normal), _FresnelPower);
				return o;
			}

			fixed4 frag(v2f i) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(i);
            	return lerp(fixed4(0.0,0.0,0.0,0.0), UNITY_ACCESS_INSTANCED_PROP(Props, _Color), saturate(1 - i.fresnel));
			}

			ENDCG
		}
	}
}