Shader "VRCBilliards/cue"
{
	Properties
	{
		_MainTex("MainTex", 2D) = "white" {}
		_EmissionMap("EmissionMap", 2D) = "white" {}
		[HDR] _EmissionColor("Main Color", Color) = (1,1,1,1)
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" }
		LOD 200

		CGPROGRAM

		#pragma surface surf Lambert noforwardadd
		sampler2D _MainTex;
		sampler2D _EmissionMap;
		
		struct Input
		{
			float2 uv_MainTex;
		};

		fixed4 _EmissionColor;

		void surf (Input IN, inout SurfaceOutput o)
		{
			o.Albedo = tex2D(_MainTex, IN.uv_MainTex);
			o.Emission = _EmissionColor * tex2D(_EmissionMap, IN.uv_MainTex);;
		}

		ENDCG
	}
}