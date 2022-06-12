// Custom shader made by Juice...

Shader "Custom/StandardScrollingEmissive" {
        Properties {
		[Header(Texture (UV1))]
		_Color("Color", Color) = (1,1,1,1)
		_MainTex("Texture", 2D) = "white" {}
		
		[Header(Emission (UV2))]
		[HDR]_EmissionColor("Color", Color) = (1,1,1,1)
		_EmissionTex("Texture", 2D) = "white" {}
		_EmissionBoost("Emission Boost", Range(0,5)) = 0.0
		_ScrollX ("Scroll X", Range(-5,5)) = 0.0
		_ScrollY ("Scroll Y", Range(-5,5)) = 0.0
		}
        SubShader
        {
             Tags {
				 "RenderType"="Opaque"
				 "Queue"="Geometry"
			 }
            LOD 200
			Cull Back
 
   
            CGPROGRAM
 
            #pragma surface surf Standard
        	#pragma shader_feature _EMISSION
            #pragma target 3.0
        	#pragma shader_feature_local _SPECULARHIGHLIGHTS_OFF

            sampler2D _MainTex;
            sampler2D _EmissionTex;
	
			struct Input {
				float2 uv_MainTex: TEXCOORD0;
				float2 uv2_EmissionTex: TEXCOORD1;
			};

            float4 _Color;
            float4 _EmissionColor;
            float _EmissionBoost;
			float _ScrollX;
			float _ScrollY;
 
            void surf (Input IN, inout SurfaceOutputStandard o)
            {
				float2 scrolledUV = IN.uv2_EmissionTex;
				float xScrollValue = frac(_ScrollX * (_Time));
				float yScrollValue = frac(_ScrollY * (_Time));
				scrolledUV += float2(xScrollValue, yScrollValue);
				o.Emission = tex2D(_EmissionTex, scrolledUV) * _EmissionColor * (_EmissionBoost + 1);
				o.Albedo = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            }
            ENDCG
        }
        FallBack "Standard"
    }