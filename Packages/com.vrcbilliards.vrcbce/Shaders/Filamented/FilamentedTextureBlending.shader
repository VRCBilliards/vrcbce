/*
Filamented texture blending sample.
This is designed to be used as-is in existing projects, but
experienced users may wish to modify how the shader functions.
*/ 
Shader "Silent/Filamented Extras/Filamented Texture Blending"
{
    Properties
    {
        [Header(Base Color)]
        _MainTexA ("Albedo (RGB)", 2D) = "white" {}
        [NoScaleOffset][Normal]_BumpMapA ("Normal Map (XYZ)", 2D) = "bump" {}
        [NoScaleOffset]_MaskMapA ("Mask (MOES)", 2D) = "green" {}
        _PropertiesA("MOES Scale", Vector) = (0.0, 0.0, 0.0, 1.0)

        [Header(Vertex Color R)]
        _MainTexR ("Albedo (RGB)", 2D) = "white" {}
        [NoScaleOffset][Normal]_BumpMapR ("Normal Map (XYZ)", 2D) = "bump" {}
        [NoScaleOffset]_MaskMapR ("Mask (MOES)", 2D) = "green" {}
        _PropertiesR("MOES Scale", Vector) = (0.0, 0.0, 0.0, 1.0)

        [Header(Vertex Color G)]
        _MainTexG ("Albedo (RGB)", 2D) = "white" {}
        [NoScaleOffset][Normal]_BumpMapG ("Normal Map (XYZ)", 2D) = "bump" {}
        [NoScaleOffset]_MaskMapG ("Mask (MOES)", 2D) = "green" {}
        _PropertiesG("MOES Scale", Vector) = (0.0, 0.0, 0.0, 1.0)

        [Header(Vertex Color B)]
        _MainTexB ("Albedo (RGB)", 2D) = "white" {}
        [NoScaleOffset][Normal]_BumpMapB ("Normal Map (XYZ)", 2D) = "bump" {}
        [NoScaleOffset]_MaskMapB ("Mask (MOES)", 2D) = "green" {}
        _PropertiesB("MOES Scale", Vector) = (0.0, 0.0, 0.0, 1.0)

        [Header(Texture Blending Settings)]
        _BlendMask ("Texture Blending Offset (R)", 2D) = "grey" {}
        _MaskStr("Blending Mask Power (per-channel)", Vector) = (1.0, 1.0, 1.0, 1.0)
        [Toggle(_DEBUG_VIEWWEIGHTS)]_DebugViewBlendingWeights("Debug View for Blend Weights", Float ) = 0.0

        [Space]
        [Toggle(_TRIPLANAR)]_UseTriplanar("Use Triplanar Sampling", Float) = 0.0
        _UVTransform0("Triplanar UV Transform X", Vector) = (1, 0, 0, 0)
        _UVTransform1("Triplanar UV Transform Y", Vector) = (0, 1, 0, 0)
        _UVTransform2("Triplanar UV Transform Z", Vector) = (0, 0, 1, 0)

        [Header(Blend Source Settings)]
        [Toggle(_SPLATMAP)]_UseSplatMap("Use Splat Map", Float) = 0
        _MainTex("Splat Map (Optional) and Texture Scale", 2D) = "black" {}

        [Header(Filamented Settings)]
        [Toggle(_LIGHTMAPSPECULAR)]_LightmapSpecular("Lightmap Specular", Range(0, 1)) = 1
        _LightmapSpecularMaxSmoothness("Lightmap Specular Max Smoothness", Range(0, 1)) = 1
        _ExposureOcclusion("Lightmap Occlusion Sensitivity", Range(0, 1)) = 0.2

        [Space]
        [KeywordEnum(None, SH, RNM, MonoSH)] _Bakery ("Bakery Mode", Int) = 0
        [HideInInspector]_RNM0("RNM0", 2D) = "black" {}
        [HideInInspector]_RNM1("RNM1", 2D) = "black" {}
        [HideInInspector]_RNM2("RNM2", 2D) = "black" {}
        [Toggle(_LTCGI)] _LTCGI ("LTCGI", Int) = 0
        [Space]
        [Enum(UnityEngine.Rendering.CullMode)]_CullMode("Cull Mode", Int) = 2

        [NonModifiableTextureData][HideInInspector] _DFG("DFG", 2D) = "white" {}
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

        #define HAS_ATTRIBUTE_COLOR
        // If this is not defined, vertex colour will not be available.

        #define USE_DFG_LUT
        // Whether to use the lookup texture for specular reflection calculation.
        // Requires a shader property _DFG to be present and filled.
    ENDCG

    CGINCLUDE
    #ifndef UNITY_PASS_SHADOWCASTER

    // Include common files. These will include the other files as needed.
	#include "UnityLightingCommon.cginc"
	#include "UnityStandardInput.cginc"
	#include "UnityStandardConfig.cginc"
	#include "UnityStandardCore.cginc"
    #include "SharedSamplingLib.hlsl"
	// Note: Unfortunately, Input is still needed due to some interdependancies with other Unity files.
	// This means that some properties will always be defined, even if they aren't used. 
	// In practise, this won't affect the final compilation, but it means you'll need to watch out for the names
	// of some common parameters. 

    TEXTURE2D(_MainTexR); TEXTURE2D(_MainTexG); TEXTURE2D(_MainTexB); TEXTURE2D(_MainTexA);
    TEXTURE2D(_BumpMapR); TEXTURE2D(_BumpMapG); TEXTURE2D(_BumpMapB); TEXTURE2D(_BumpMapA);
    TEXTURE2D(_MaskMapR); TEXTURE2D(_MaskMapG); TEXTURE2D(_MaskMapB); TEXTURE2D(_MaskMapA);

    SAMPLER(sampler_MainTexR);
    SAMPLER(sampler_BumpMapR);
    SAMPLER(sampler_MaskMapR);

    float4 _MainTexR_ST; float4 _MainTexG_ST; float4 _MainTexB_ST; float4 _MainTexA_ST;

    float4 _PropertiesR; float4 _PropertiesG; float4 _PropertiesB; float4 _PropertiesA;

    uniform half4 _UVTransform0;
    uniform half4 _UVTransform1;
    uniform half4 _UVTransform2;

    TEXTURE2D(_BlendMask); SAMPLER(sampler_BlendMask);
    float4 _BlendMask_ST;
    float4 _MaskStr;

	// Vertex functions are called from UnityStandardCore.
	// You can alter values here, or copy the function in and modify it.
	VertexOutputForwardBase vertBase (VertexInput v) { return vertForwardBase(v); }
	VertexOutputForwardAdd vertAdd (VertexInput v) { return vertForwardAdd(v); }

// Typical triplanar mapping -- has less visual artifacts than biplanar.
// Artifacts stand out because this is a material shader for things like
// terrain which cover a large portion of the screen.
float4 boxmap( TEXTURE2D_PARAM(tex, smp), float3 p, float3 n, float k )
{
    // grab coord derivatives for texturing
    float3 dpdx = ddx(p);
    float3 dpdy = ddy(p);

    float3 m = pow( abs(n), k );

    // project+fetch
    float4 x = 0.0;
    if (m.x > 0) x = SAMPLE_TEXTURE2D_GRAD( tex, smp, p.zy, dpdx.zy, dpdy.zy );
    float4 y = 0.0;
    if (m.y > 0) y = SAMPLE_TEXTURE2D_GRAD( tex, smp, p.zx, dpdx.zx, dpdy.zx );
    float4 z = 0.0;
    if (m.z > 0) z = SAMPLE_TEXTURE2D_GRAD( tex, smp, p.xy, dpdx.xy, dpdy.xy );
    
    // and blend
    return (x*m.x + y*m.y + z*m.z) / (m.x + m.y + m.z);
}

float3 RNMBlendUnpacked(float3 n1, float3 n2)
{
    n1 += float3( 0,  0, 1);
    n2 *= float3(-1, -1, 1);
    return n1*dot(n1, n2)/n1.z - n2;
}

void addLayer(float weight, float2 uv, float4 uv_ST,
    TEXTURE2D_PARAM(tex_A, smp_A), inout float4 albedoAlpha, 
    TEXTURE2D_PARAM(tex_N, smp_N), inout float3 normal, 
    TEXTURE2D_PARAM(tex_M, smp_M), inout float4 props,
    inout float4 maskProps)
{
    if (weight > 0)  
    {
        uv = uv * uv_ST.xy + uv_ST.zw;
        float4 thisBasemap = SAMPLE_TEXTURE2D(tex_A, smp_A, uv);
        float3 thisNormal = UnpackScaleNormal(SAMPLE_TEXTURE2D(tex_N, smp_N, uv), weight);
        float4 thisProps = SAMPLE_TEXTURE2D(tex_M, smp_M, uv);
        thisProps.rba *= maskProps.rba; // metallic, emission, smoothness
        thisProps.g = lerp(1, thisProps.g, maskProps.g); // occlusion

        #if 0
        albedoAlpha = albedoAlpha + thisBasemap * weight;
        normal = RNMBlendUnpacked(thisNormal, normal);
        props = props + thisProps * weight;
        #else
        albedoAlpha = lerp(albedoAlpha, thisBasemap, weight);
        normal = RNMBlendUnpacked(thisNormal, normal);
        props = lerp(props, thisProps, weight);
        #endif
    }
}

void addLayerTriplanar(float weight, float3 p, float3 n, float4 uv_ST,
    TEXTURE2D_PARAM(tex_A, smp_A), inout float4 albedoAlpha, 
    TEXTURE2D_PARAM(tex_N, smp_N), inout float3 normal, 
    TEXTURE2D_PARAM(tex_M, smp_M), inout float4 props,
    inout float4 maskProps)
{
    const float tightness = 6.0;
    if (weight > 0)
    {
        p = p * uv_ST.xyx + uv_ST.zwz;
        float4 thisBasemap = boxmap(TEXTURE2D_ARGS(tex_A, smp_A), p, n, tightness);
        float3 thisNormal = UnpackScaleNormal(boxmap(TEXTURE2D_ARGS(tex_N, smp_N), p, n, tightness), weight);
        float4 thisProps = boxmap(TEXTURE2D_ARGS(tex_M, smp_M), p, n, tightness);
        thisProps.rba *= maskProps.rba; // metallic, emission, smoothness
        thisProps.g = lerp(1, thisProps.g, maskProps.g); // occlusion

        #if 0
        albedoAlpha = albedoAlpha + thisBasemap * weight;
        normal = RNMBlendUnpacked(thisNormal, normal);
        props = props + thisProps * weight;
        #else
        albedoAlpha = lerp(albedoAlpha, thisBasemap, weight);
        normal = RNMBlendUnpacked(thisNormal, normal);
        props = lerp(props, thisProps, weight);
        #endif
    }
}

	// The material function itself!  You can alter the code below to add extra properties. 
inline MaterialInputs BlendedMaterialSetup (inout float4 i_tex, float3 i_eyeVec, 
    half3 i_viewDirForParallax, float4 tangentToWorld[3], float3 i_posWorld, float4 i_color)
{   
    // Blend weights in vertex colours
    fixed4 weights = i_color;
    #if defined(_SPLATMAP)
    // Blend weights in splat map
    weights = tex2D (_MainTex, i_tex.xy);
    #endif

    float3x3 tangentToWorldOnly = float3x3(tangentToWorld[0].xyz, tangentToWorld[1].xyz, tangentToWorld[2].xyz);
    float3 worldNormalT = mul ( float3( 0, 0, 1 ), tangentToWorldOnly );
    float3 worldPosT = float3(
        dot(float4(i_posWorld.xyz, 1), _UVTransform0),
        dot(float4(i_posWorld.xyz, 1), _UVTransform1),
        dot(float4(i_posWorld.xyz, 1), _UVTransform2)
        );

#if defined(_TRIPLANAR)
    float4 blendMod = boxmap(TEXTURE2D_ARGS(_BlendMask, sampler_BlendMask),
        worldPosT * _BlendMask_ST.xyx + _BlendMask_ST.zwz, worldNormalT, 1.0).r;
#else
    float4 blendMod = SAMPLE_TEXTURE2D(_BlendMask, sampler_BlendMask,
        i_tex.xy * _BlendMask_ST.xy + _BlendMask_ST.zw).r;
#endif

    weights = saturate( pow(((blendMod*weights)*4) + (weights*2), _MaskStr) );

#if defined(_DEBUG_VIEWWEIGHTS)
    MaterialInputs debugView = (MaterialInputs)0;
    initMaterial(debugView);
    debugView.baseColor = 0.0;
    debugView.emissive = weights;
    debugView.emissive.a = 1.0;
    return debugView;
#endif

    float4 c = 0;
    float3 n = float3(0.0, 0.0, 1.0);
    float4 m = 0;

#if defined(_TRIPLANAR)
    addLayerTriplanar(1.0, worldPosT, worldNormalT, _MainTexA_ST, 
        TEXTURE2D_ARGS(_MainTexA, sampler_MainTexR), c, 
        TEXTURE2D_ARGS(_BumpMapA, sampler_BumpMapR), n, 
        TEXTURE2D_ARGS(_MaskMapA, sampler_MaskMapR), m, 
        _PropertiesA); 
    addLayerTriplanar(weights.r, worldPosT, worldNormalT, _MainTexR_ST, 
        TEXTURE2D_ARGS(_MainTexR, sampler_MainTexR), c, 
        TEXTURE2D_ARGS(_BumpMapR, sampler_BumpMapR), n, 
        TEXTURE2D_ARGS(_MaskMapR, sampler_MaskMapR), m, 
        _PropertiesR);
    addLayerTriplanar(weights.g, worldPosT, worldNormalT, _MainTexG_ST, 
        TEXTURE2D_ARGS(_MainTexG, sampler_MainTexR), c, 
        TEXTURE2D_ARGS(_BumpMapG, sampler_BumpMapR), n, 
        TEXTURE2D_ARGS(_MaskMapG, sampler_MaskMapR), m, 
        _PropertiesG);
    addLayerTriplanar(weights.b, worldPosT, worldNormalT, _MainTexB_ST, 
        TEXTURE2D_ARGS(_MainTexB, sampler_MainTexR), c, 
        TEXTURE2D_ARGS(_BumpMapB, sampler_BumpMapR), n, 
        TEXTURE2D_ARGS(_MaskMapB, sampler_MaskMapR), m, 
        _PropertiesB);
#else
    addLayer(1.0, i_tex.xy, _MainTexA_ST, 
        TEXTURE2D_ARGS(_MainTexA, sampler_MainTexR), c, 
        TEXTURE2D_ARGS(_BumpMapA, sampler_BumpMapR), n, 
        TEXTURE2D_ARGS(_MaskMapA, sampler_MaskMapR), m, 
        _PropertiesA);
    addLayer(weights.r, i_tex.xy, _MainTexR_ST, 
        TEXTURE2D_ARGS(_MainTexR, sampler_MainTexR), c, 
        TEXTURE2D_ARGS(_BumpMapR, sampler_BumpMapR), n, 
        TEXTURE2D_ARGS(_MaskMapR, sampler_MaskMapR), m, 
        _PropertiesR);
    addLayer(weights.g, i_tex.xy, _MainTexG_ST, 
        TEXTURE2D_ARGS(_MainTexG, sampler_MainTexR), c, 
        TEXTURE2D_ARGS(_BumpMapG, sampler_BumpMapR), n, 
        TEXTURE2D_ARGS(_MaskMapG, sampler_MaskMapR), m, 
        _PropertiesG);
    addLayer(weights.b, i_tex.xy, _MainTexB_ST, 
        TEXTURE2D_ARGS(_MainTexB, sampler_MainTexR), c, 
        TEXTURE2D_ARGS(_BumpMapB, sampler_BumpMapR), n, 
        TEXTURE2D_ARGS(_MaskMapB, sampler_MaskMapR), m, 
        _PropertiesB);
#endif

    half metallic = m.x;
    half occlusion =  m.y;
    half emissionMask = m.z;
    half smoothness = m.w; 

    MaterialInputs material = (MaterialInputs)0;
    initMaterial(material);
    material.baseColor = c;
    material.metallic = metallic;
    material.roughness = computeRoughnessFromGlossiness(smoothness);
    material.normal = n;
    material.emissive.rgb = c.rgb * emissionMask;
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
    BlendedMaterialSetup(i.tex, i.eyeVec.xyz, IN_VIEWDIR4PARALLAX(i), i.tangentToWorldAndPackedData, 
        IN_WORLDPOS(i), i.color);

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
    BlendedMaterialSetup(i.tex, i.eyeVec.xyz, IN_VIEWDIR4PARALLAX_FWDADD(i), i.tangentToWorldAndLightDir, 
        IN_WORLDPOS_FWDADD(i), i.color);

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
        Tags { "RenderType"="Opaque" "PerformanceChecks"="False" "LTCGI" = "_LTCGI" }
        LOD 300

        // ------------------------------------------------------------------
        //  Base forward pass (directional light, emission, lightmaps, ...)
        Pass
        {
            Name "FORWARD"
            Tags { "LightMode" = "ForwardBase" }

            Cull [_CullMode]

            CGPROGRAM
            #pragma target 5.0

            // -------------------------------------

            #pragma shader_feature_local _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
            #pragma shader_feature_local _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature_local _GLOSSYREFLECTIONS_OFF
            
            #pragma shader_feature_local _ _BAKERY_RNM _BAKERY_SH _BAKERY_MONOSH
            #pragma shader_feature_local _LTCGI
            #pragma shader_feature_local _SPLATMAP
            #pragma shader_feature_local _TRIPLANAR
            #pragma shader_feature_local _DEBUG_VIEWWEIGHTS

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
            #pragma target 5.0

            // -------------------------------------


            #pragma shader_feature_local _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
            #pragma shader_feature_local _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature_local _SPLATMAP
            #pragma shader_feature_local _TRIPLANAR

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
            #pragma target 5.0

            // -------------------------------------

            #ifndef UNITY_PASS_SHADOWCASTER
            #define UNITY_PASS_SHADOWCASTER
            #endif  

            #pragma shader_feature_local _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
            #pragma multi_compile_shadowcaster
            #pragma multi_compile_instancing
            // Uncomment the following line to enable dithering LOD crossfade. Note: there are more in the file to uncomment for other passes.
            //#pragma multi_compile _ LOD_FADE_CROSSFADE

            #pragma vertex vertShadowCaster
            #pragma fragment fragShadowCaster

            #include "UnityStandardShadow.cginc"

            ENDCG
        }

        Pass
        {
            Name "META"
            Tags {"LightMode"="Meta"}
            Cull Off
            CGPROGRAM

            #include "UnityStandardMeta.cginc"

            #define META_PASS

            float4 frag_meta2 (v2f_meta i): SV_Target
            {
                MaterialInputs material = SETUP_BRDF_INPUT (i.uv);
                float4 dummy[3]; dummy[0] = 0; dummy[1] = 0; dummy[2] = 0;
                material = BlendedMaterialSetup(i.uv, 0, 0, dummy, 0, i.color);
                
                PixelParams pixel = (PixelParams)0;
                getCommonPixelParams(material, pixel);

                UnityMetaInput o;
                UNITY_INITIALIZE_OUTPUT(UnityMetaInput, o);

            #ifdef EDITOR_VISUALIZATION
                o.Albedo = pixel.diffuseColor;
                o.VizUV = i.vizUV;
                o.LightCoord = i.lightCoord;
            #else
                o.Albedo = UnityLightmappingAlbedo (pixel.diffuseColor, pixel.f0, 1-pixel.perceptualRoughness);
            #endif
                o.SpecularColor = pixel.f0;
                o.Emission = material.emissive;

                return UnityMetaFragment(o);
            }

            #pragma vertex vert_meta
            #pragma fragment frag_meta2
            #pragma shader_feature _EMISSION
            #pragma shader_feature _METALLICGLOSSMAP
            #pragma shader_feature ___ _DETAIL_MULX2
            ENDCG
        }

    }

    FallBack "VertexLit"
}
