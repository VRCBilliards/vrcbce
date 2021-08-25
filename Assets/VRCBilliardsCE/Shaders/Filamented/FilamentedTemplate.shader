/*
Filamented example template.
This is a template of how to use Filamented to make a simple shader
that passes its own properties through to Filament, in a manner
similar to a Unity surface shader - but with less jank. 

Instead of reading multiple seperate maps, it just asks for
an albedo map, a normal map, and a MOES map. 
*/ 
Shader "Silent/Filamented Template"
{
    Properties
    {
        _MainTex("Albedo", 2D) = "white" {}
        [Normal] _BumpMap("Normal", 2D) = "bump" {}
        _BumpScale("Normal Scale", Float) = 1
        [Space]
        _MOESMap("MOES Map", 2D) = "white" {}
        _MetallicScale("Metallic", Range( 0 , 1)) = 0
        _OcclusionScale("Occlusion", Range( 0 , 1)) = 0
        _SmoothnessScale("Smoothness", Range( 0 , 1)) = 0
        [Space]
        _Emission("Emission Power", Float) = 0
        _EmissionColor("Emission Color", Color) = (1,1,1,1)
        [Space]
        [Enum(UnityEngine.Rendering.CullMode)]_CullMode("Cull Mode", Int) = 2
    }

    CGINCLUDE
    	// First, setup what Filamented does. 
    	// Filamented's behaviour is decided by the shading model and what material properties are defined.
    	// These are listed in FilamentMaterialInputs.
    	// You can set up and use anything in the initMaterials function.

		// SHADING_MODEL_CLOTH
		// SHADING_MODEL_SUBSURFACE
    	// These are *not* currently supported.

    	// SHADING_MODEL_SPECULAR_GLOSSINESS
    	// If this is not defined, the material will default to metallic/roughness workflow.

    	#define MATERIAL_HAS_NORMAL
    	// If this is not defined, normal maps won't be enabled.

    	#define MATERIAL_HAS_AMBIENT_OCCLUSION
    	// If this is not defined, occlusion won't be taken into account

    	#define MATERIAL_HAS_EMISSIVE
    	// If this is not defined, emission won't be taken into account

    	// MATERIAL_HAS_ANISOTROPY
    	// If this is set, the material will support anisotropy.

    	// MATERIAL_HAS_CLEAR_COAT 
    	// If this is set, the material will support clear coat.
    ENDCG

    CGINCLUDE
    #ifndef UNITY_PASS_SHADOWCASTER

    // Include common files. These will include the other files as needed.
	#include "UnityLightingCommon.cginc"
	#include "UnityStandardInput.cginc"
	#include "UnityStandardConfig.cginc"
	#include "UnityStandardCore.cginc"
	// Note: Unfortunately, Input is still needed due to some interdependancies with other Unity files.
	// This means that some properties will always be defined, even if they aren't used. 
	// In practise, this won't affect the final compilation, but it means you'll need to watch out for the names
	// of some common parameters. In this case, only MOESMap and some other properties are defined here because
	// they are already defined in Input. 

    // uniform sampler2D _MainTex;
    // uniform sampler2D _BumpMap;
    uniform sampler2D _MOESMap;
	// uniform half _BumpScale;
	uniform half _MetallicScale;
	uniform half _OcclusionScale;
	uniform half _SmoothnessScale;
	uniform half _Emission;
	// uniform half3 _EmissionColor;

	// Vertex functions are called from UnityStandardCore.
	// You can alter values here, or copy the function in and modify it.
	VertexOutputForwardBase vertBase (VertexInput v) { return vertForwardBase(v); }
	VertexOutputForwardAdd vertAdd (VertexInput v) { return vertForwardAdd(v); }

	// The material function itself!  You can alter the code below to add extra properties. 
inline MaterialInputs MyMaterialSetup (inout float4 i_tex, float3 i_eyeVec, half3 i_viewDirForParallax, float4 tangentToWorld[3], float3 i_posWorld)
{   
    half4 baseColor = tex2D (_MainTex, i_tex.xy);
    half4 packedMap = tex2D (_MOESMap, i_tex.xy);
    half3 normalTangent = UnpackScaleNormal(tex2D (_BumpMap, i_tex.xy), _BumpScale);

    half metallic = packedMap.x * _MetallicScale;
    half occlusion = lerp(1, packedMap.y, _OcclusionScale);
    half emissionMask = packedMap.z;
    half smoothness = packedMap.w * _SmoothnessScale; 

    MaterialInputs material = (MaterialInputs)0;
    initMaterial(material);
    material.baseColor = baseColor;
    material.metallic = metallic;
    material.roughness = computeRoughnessFromGlossiness(smoothness);
    material.normal = normalTangent;
    material.emissive.rgb = baseColor.rgb * emissionMask * _Emission * _EmissionColor;
    material.emissive.a = 1.0;
    material.ambientOcclusion = occlusion;
    return material;
}

half4 fragForwardBaseTemplate (VertexOutputForwardBase i)
{
    UNITY_APPLY_DITHER_CROSSFADE(i.pos.xy);

    UNITY_SETUP_INSTANCE_ID(i);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

    ShadingParams shading = (ShadingParams)0;
    // Initialize shading with expected parameters
    computeShadingParamsForwardBase(shading, i);

    UNITY_LIGHT_ATTENUATION(atten, i, shading.position);

    #if defined(LIGHTMAP_ON) || defined(DYNAMICLIGHTMAP_ON)
    GetBakedAttenuation(atten, i.ambientOrLightmapUV.xy, shading.position);
    #endif

    // Your material setup goes here.
    MaterialInputs material =
    MyMaterialSetup(i.tex, i.eyeVec.xyz, IN_VIEWDIR4PARALLAX(i), i.tangentToWorldAndPackedData, IN_WORLDPOS(i));

    prepareMaterial(shading, material);

#if (defined(_NORMALMAP) && defined(NORMALMAP_SHADOW))
    float noise = noiseR2(i.pos.xy);
    float nmShade = NormalTangentShadow (i.tex, i.lightDirTS, noise);
    shading.attenuation = min(shading.attenuation, max(1-nmShade, 0));
#endif

    float4 c = evaluateMaterial (shading, material);

    UNITY_EXTRACT_FOG_FROM_EYE_VEC(i);
    UNITY_APPLY_FOG(_unity_fogCoord, c.rgb);
    return c;
}

half4 fragForwardAddTemplate (VertexOutputForwardAdd i)
{
    UNITY_APPLY_DITHER_CROSSFADE(i.pos.xy);

    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

    ShadingParams shading = (ShadingParams)0;
    // Initialize shading with expected parameters
    computeShadingParamsForwardAdd(shading, i);

    UNITY_LIGHT_ATTENUATION(atten, i, shading.position);

    // Your material setup goes here.
    MaterialInputs material =
    MyMaterialSetup(i.tex, i.eyeVec.xyz, IN_VIEWDIR4PARALLAX_FWDADD(i), i.tangentToWorldAndLightDir, IN_WORLDPOS_FWDADD(i));

    prepareMaterial(shading, material);

#if (defined(_NORMALMAP) && defined(NORMALMAP_SHADOW))
    float noise = noiseR2(i.pos.xy);
    float nmShade = NormalTangentShadow (i.tex, i.lightDirTS, noise);
    shading.attenuation = min(shading.attenuation, max(1-nmShade, 0));
#endif

    float4 c = evaluateMaterial (shading, material);

    UNITY_EXTRACT_FOG_FROM_EYE_VEC(i);
    UNITY_APPLY_FOG_COLOR(_unity_fogCoord, c.rgb, half4(0,0,0,0)); // fog towards black in additive pass
    return c;
}

half4 fragBase (VertexOutputForwardBase i) : SV_Target { return fragForwardBaseTemplate(i); }
half4 fragAdd (VertexOutputForwardAdd i) : SV_Target { return fragForwardAddTemplate(i); }
    #endif 

    ENDCG

    SubShader
    {
        Tags { "RenderType"="Opaque" "PerformanceChecks"="False" }
        LOD 300

        // ------------------------------------------------------------------
        //  Base forward pass (directional light, emission, lightmaps, ...)
        Pass
        {
            Name "FORWARD"
            Tags { "LightMode" = "ForwardBase" }

            Cull [_CullMode]

            CGPROGRAM
            #pragma target 4.0

            // -------------------------------------

            #pragma shader_feature _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
            #pragma shader_feature _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature _GLOSSYREFLECTIONS_OFF

            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            // Uncomment the following line to enable dithering LOD crossfade. Note: there are more in the file to uncomment for other passes.
            //#pragma multi_compile _ LOD_FADE_CROSSFADE

            #pragma vertex vertBase
            #pragma fragment fragBase

            ENDCG
        }
        // ------------------------------------------------------------------
        //  Additive forward pass (one light per pass)
        Pass
        {
            Name "FORWARD_DELTA"
            Tags { "LightMode" = "ForwardAdd" }
            Blend One One
            Fog { Color (0,0,0,0) } // in additive pass fog should be black
            ZWrite Off
            ZTest Equal
            Cull [_CullMode]

            CGPROGRAM
            #pragma target 3.0

            // -------------------------------------


            #pragma shader_feature _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
            #pragma shader_feature _SPECULARHIGHLIGHTS_OFF

            #pragma multi_compile_fwdadd_fullshadows
            #pragma multi_compile_fog
            // Uncomment the following line to enable dithering LOD crossfade. Note: there are more in the file to uncomment for other passes.
            //#pragma multi_compile _ LOD_FADE_CROSSFADE

            #pragma vertex vertAdd
            #pragma fragment fragAdd

            ENDCG
        }
        
        // ------------------------------------------------------------------
        //  Shadow rendering pass
        Pass {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On ZTest LEqual
            Cull [_CullMode]

            CGPROGRAM
            #pragma target 3.0

            // -------------------------------------

            #ifndef UNITY_PASS_SHADOWCASTER
            #define UNITY_PASS_SHADOWCASTER
            #endif  

            #pragma shader_feature _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
            #pragma multi_compile_shadowcaster
            #pragma multi_compile_instancing
            // Uncomment the following line to enable dithering LOD crossfade. Note: there are more in the file to uncomment for other passes.
            //#pragma multi_compile _ LOD_FADE_CROSSFADE

            #pragma vertex vertShadowCaster
            #pragma fragment fragShadowCaster

            #include "UnityStandardShadow.cginc"

            ENDCG
        }
        

        // Deferred not implemented
        UsePass "Standard/DEFERRED"

        // Meta not implemented
        UsePass "Standard/META"

    }

    FallBack "VertexLit"
}
