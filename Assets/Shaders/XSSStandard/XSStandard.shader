Shader "Xiexe/Standard"
{
	Properties
	{
        [Header(MAIN)]
        [Enum(Unity Default, 0, Non Linear, 1)]_LightProbeMethod("Light Probe Sampling", Int) = 0
        [Enum(UVs, 0, Triplanar World, 1, Triplanar Object, 2)]_TextureSampleMode("Texture Mode", Int) = 0
        _TriplanarFalloff("Triplanar Blend", Range(0.5,1)) = 1
        _MainTex ("Main Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        //#CUTOUT!_Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
        
        [Space(16)]
        [Header(NORMALS)]
        _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Scale", Range(-1,1)) = 1
        
        [Space(16)]
        [Header(METALLIC)]
        _MetallicGlossMap("Metallic Map", 2D) = "white" {}
        [Gamma] _Metallic("Metallic", Range(0,1)) = 0
        _Glossiness("Smoothness", Range(0,1)) = 0
        _Reflectance("Reflectance", Range(0,1)) = 0.5
        _Anisotropy("Anisotropy", Range(-1,1)) = 0
        
        [Space(16)]
        [Header(OCCLUSION)]
        _OcclusionMap("Occlusion Map", 2D) = "white" {}
        _OcclusionColor("Occlusion Color", Color) = (0,0,0,1)
        
        [Space(16)]
        [Header(SUBSURFACE)]
        [Enum(Off, 0, Estimate, 1)]_SubsurfaceMethod("Subsurface Scattering Method", Int) = 0
        _CurvatureThicknessMap("Curvature Thickness Map", 2D) = "gray" {}
        _SubsurfaceColorMap("Subsurface Color Map", 2D) = "white" {}
        _SubsurfaceScatteringColor("Subsurface Color", Color) = (1,1,1,1)
        _SubsurfaceInheritDiffuse("Subsurface Inherit Diffuse", Range(0,1)) = 0
        _TransmissionNormalDistortion("Transmission Distortion", Range(0,3)) = 1
        _TransmissionPower("Transmission Power", Range(0,3)) = 1
        _TransmissionScale("Transmission Scale", Range(0,3)) = 0.1
        
        [Space(16)]
        [Header(EMISSION)]
        _EmissionMap("Emission Map", 2D) = "white" {}
        [HDR]_EmissionColor("Emission Color", Color) = (0,0,0,1)
        
        [Space(16)]
        [Header(CLEARCOAT)]
        _ClearcoatMap("Clearcoat Map", 2D) = "white" {}
        _Clearcoat("Clearcoat", Range(0,1)) = 0
        _ClearcoatGlossiness("Clearcoat Smoothness", Range(0,1)) = 0.5
        _ClearcoatAnisotropy("Clearcoat Anisotropy", Range(-1,1)) = 0
        
        //#GEOM![Space(16)]
        //#GEOM![Header(GEOMETRY SETTINGS)]
        //#GEOM!_VertexOffset("Face Offset", float) = 0
        
        //#TESS![Space(16)]
        //#TESS![Header(GEOMETRYTESSELLATION SETTINGS)]
        //#TESS![Enum(Uniform, 0, Edge Length, 1, Distance, 2)]_TessellationMode("Tessellation Mode", Int) = 1
        //#TESS!_TessellationUniform("Tessellation Factor", Range(0,1)) = 0.05
        //#TESS!_TessClose("Tessellation Close", Float) = 10
        //#TESS!_TessFar("Tessellation Far", Float) = 50
        
        [Space(16)]
        [Header(LIGHTMAPPING HACKS)]
        _SpecularLMOcclusion("Specular Occlusion", Range(0,1)) = 0
        _SpecLMOcclusionAdjust("Spec Occlusion Sensitiviy", Range(0,1)) = 0.2
        _LMStrength("Lightmap Strength", Range(0,1)) = 1
        _RTLMStrength("Realtime Lightmap Strength", Range(0,1)) = 1
    }

	SubShader
	{
        Tags{"RenderType"="Opaque" "Queue"="Geometry"}

		Pass
		{
            Tags {"LightMode"="ForwardBase"}
			CGPROGRAM
			#pragma target 3.0
			#pragma vertex vert
			#pragma fragment frag
            #pragma multi_compile _ VERTEXLIGHT_ON
            #pragma multi_compile_fwdbase

            #ifndef UNITY_PASS_FORWARDBASE
                #define UNITY_PASS_FORWARDBASE
            #endif
			
			#include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"
            
			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                float2 uv2 : TEXCOORD2;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
			};

			struct v2f
			{
                float4 pos : SV_POSITION;
				float2 uv : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                float2 uv2 : TEXCOORD2;
                float3 btn[3] : TEXCOORD3; //TEXCOORD2, TEXCOORD3 | bitangent, tangent, worldNormal
                float3 worldPos : TEXCOORD6;
                float3 objPos : TEXCOORD7;
                float3 objNormal : TEXCOORD8;
                float4 screenPos : TEXCOORD9;
                SHADOW_COORDS(10)
			};

            #include "Defines.cginc"
            #include "LightingFunctions.cginc"
			#include "LightingBRDF.cginc"
            #include "VertFrag.cginc"
			
			ENDCG
		}

        Pass
		{
            Tags {"LightMode"="ForwardAdd"}
            Blend One One
            ZWrite Off
            
			CGPROGRAM
			#pragma target 3.0
			#pragma vertex vert
			#pragma fragment frag
            #pragma multi_compile_fwdadd_fullshadows

            #ifndef UNITY_PASS_FORWARDADD
                #define UNITY_PASS_FORWARDADD
            #endif

			#include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"
            
			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
			};

			struct v2f
			{
                float4 pos : SV_POSITION;
				float2 uv : TEXCOORD0;
                float3 btn[3] : TEXCOORD1; //TEXCOORD2, TEXCOORD3 | bitangent, tangent, worldNormal
                float3 worldPos : TEXCOORD4;
                float3 objPos : TEXCOORD5;
                float3 objNormal : TEXCOORD6;
                float4 screenPos : TEXCOORD7;
                SHADOW_COORDS(8)
			};

            #include "Defines.cginc"
            #include "LightingFunctions.cginc"
            #include "LightingBRDF.cginc"
			#include "VertFrag.cginc"
			
			ENDCG
		}

        Pass
        {
            Tags{"LightMode" = "ShadowCaster"} //Removed "DisableBatching" = "True". If issues arise re-add this.
            Cull Off
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_shadowcaster

            #ifndef UNITY_PASS_SHADOWCASTER
                #define UNITY_PASS_SHADOWCASTER
            #endif
            
            #include "UnityCG.cginc" 
            #include "Lighting.cginc"
            #include "AutoLight.cginc"
            
            struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
			};

			struct v2f
			{
                float4 pos : SV_POSITION;
				float2 uv : TEXCOORD0;
                float3 btn[3] : TEXCOORD1; //TEXCOORD2, TEXCOORD3 | bitangent, tangent, worldNormal
                float3 worldPos : TEXCOORD4;
                float3 objPos : TEXCOORD5;
                float3 objNormal : TEXCOORD6;
                float4 screenPos : TEXCOORD7;
			};

            #include "Defines.cginc"
            #include "LightingFunctions.cginc"
            #include "VertFrag.cginc"
            ENDCG
        }
	}
}
