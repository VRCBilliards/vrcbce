// Unity built-in shader source. Copyright (c) 2016 Unity Technologies. MIT license (see license.txt)

#ifndef UNITY_STANDARD_CONFIG_INCLUDED
#define UNITY_STANDARD_CONFIG_INCLUDED

// Filamented configuration

// Whether to read the _DFG texture for DFG instead of the approximate one. 
//#define USE_DFG_LUT

#define SPECULAR_AMBIENT_OCCLUSION SPECULAR_AO_SIMPLE
#define MULTI_BOUNCE_AMBIENT_OCCLUSION 1
#define GEOMETRIC_SPECULAR_AA
#define _specularAntiAliasingVariance 0.15
#define _specularAntiAliasingThreshold 0.25

// Filament cross-compatibility defines

#if DIRECTIONAL
#define HAS_DIRECTIONAL_LIGHTING 
#endif
#if (POINT || SPOT)
#define HAS_DYNAMIC_LIGHTING 
#endif
#if _EMISSION
#define MATERIAL_HAS_EMISSIVE 
#endif
#if (SHADOWS_SCREEN || SHADOWS_SHADOWMASK || LIGHTMAP_SHADOW)
#define HAS_SHADOWING 
#endif

#if defined(MATERIAL_HAS_NORMAL)
#define _NORMALMAP 1
#endif

#if (MATERIAL_NEEDS_TBN)
#define _TANGENT_TO_WORLD
#endif

#if _ALPHAPREMULTIPLY_ON
#define BLEND_MODE_TRANSPARENT 
#endif
#if _ALPHABLEND_ON
#define BLEND_MODE_FADE 
#endif
#if _ALPHATEST_ON
#define BLEND_MODE_MASKED 
#endif

// By default, Standard assumes meshes have normals
#define HAS_ATTRIBUTE_TANGENTS

#if _NORMALMAP
#define MATERIAL_HAS_NORMAL
//#define NORMALMAP_SHADOW
#endif

//#define MATERIAL_HAS_AMBIENT_OCCLUSION 

#if 1
#define LIGHTMAP_SPECULAR
#endif

#if defined(USE_DFG_LUT)
uniform sampler2D _DFG;
#endif

// Define Specular cubemap constants
#ifndef UNITY_SPECCUBE_LOD_EXPONENT
#define UNITY_SPECCUBE_LOD_EXPONENT (1.5)
#endif
#ifndef UNITY_SPECCUBE_LOD_STEPS
#define UNITY_SPECCUBE_LOD_STEPS (6)
#endif

// Energy conservation for Specular workflow is Monochrome. For instance: Red metal will make diffuse Black not Cyan
#ifndef UNITY_CONSERVE_ENERGY
#define UNITY_CONSERVE_ENERGY 1
#endif
#ifndef UNITY_CONSERVE_ENERGY_MONOCHROME
#define UNITY_CONSERVE_ENERGY_MONOCHROME 1
#endif

// "platform caps" defines: they are controlled from TierSettings (Editor will determine values and pass them to compiler)
// UNITY_SPECCUBE_BOX_PROJECTION:                   TierSettings.reflectionProbeBoxProjection
// UNITY_SPECCUBE_BLENDING:                         TierSettings.reflectionProbeBlending
// UNITY_ENABLE_DETAIL_NORMALMAP:                   TierSettings.detailNormalMap
// UNITY_USE_DITHER_MASK_FOR_ALPHABLENDED_SHADOWS:  TierSettings.semitransparentShadows

// disregarding what is set in TierSettings, some features have hardware restrictions
// so we still add safety net, otherwise we might end up with shaders failing to compile

#if defined(SHADER_TARGET_SURFACE_ANALYSIS)
    // For surface shader code analysis pass, disable some features that don't affect inputs/outputs
    #undef UNITY_SPECCUBE_BOX_PROJECTION
    #undef UNITY_SPECCUBE_BLENDING
    #undef UNITY_USE_DITHER_MASK_FOR_ALPHABLENDED_SHADOWS
#elif SHADER_TARGET < 30
    #undef UNITY_SPECCUBE_BOX_PROJECTION
    #undef UNITY_SPECCUBE_BLENDING
    #undef UNITY_ENABLE_DETAIL_NORMALMAP
    #ifdef _PARALLAXMAP
        #undef _PARALLAXMAP
    #endif
#endif
#if (SHADER_TARGET < 30) || defined(SHADER_API_GLES)
    #undef UNITY_USE_DITHER_MASK_FOR_ALPHABLENDED_SHADOWS
#endif

#ifndef UNITY_SAMPLE_FULL_SH_PER_PIXEL
    // Lightmap UVs and ambient color from SHL2 are shared in the vertex to pixel interpolators. Do full SH evaluation in the pixel shader when static lightmap and LIGHTPROBE_SH is enabled.
    #define UNITY_SAMPLE_FULL_SH_PER_PIXEL (LIGHTMAP_ON && LIGHTPROBE_SH)

    // Shaders might fail to compile due to shader instruction count limit. Leave only baked lightmaps on SM20 hardware.
    #if UNITY_SAMPLE_FULL_SH_PER_PIXEL && (SHADER_TARGET < 25)
        #undef UNITY_SAMPLE_FULL_SH_PER_PIXEL
        #undef LIGHTPROBE_SH
    #endif
#endif

#ifndef UNITY_BRDF_GGX
#define UNITY_BRDF_GGX 1
#endif

// Orthnormalize Tangent Space basis per-pixel
// Necessary to support high-quality normal-maps. Compatible with Maya and Marmoset.
// However xNormal expects oldschool non-orthnormalized basis - essentially preventing good looking normal-maps :(
// Due to the fact that xNormal is probably _the most used tool to bake out normal-maps today_ we have to stick to old ways for now.
//
// Disabled by default, until xNormal has an option to bake proper normal-maps.
#ifndef UNITY_TANGENT_ORTHONORMALIZE
#define UNITY_TANGENT_ORTHONORMALIZE 1
#endif


// Some extra optimizations

// Simplified Standard Shader is off by default and should not be used for Legacy Shaders
#ifndef UNITY_STANDARD_SIMPLE
    #define UNITY_STANDARD_SIMPLE 0
#endif

// Setup a new define with meaningful name to know if we require world pos in fragment shader
#if UNITY_STANDARD_SIMPLE
    #define UNITY_REQUIRE_FRAG_WORLDPOS 0
#else
    #define UNITY_REQUIRE_FRAG_WORLDPOS 1
#endif

// Should we pack worldPos along tangent (saving an interpolator)
// We want to skip this on mobile platforms, because worldpos gets packed into mediump
#if UNITY_REQUIRE_FRAG_WORLDPOS && !defined(_PARALLAXMAP) && !defined(SHADER_API_MOBILE)
    #define UNITY_PACK_WORLDPOS_WITH_TANGENT 1
#else
    #define UNITY_PACK_WORLDPOS_WITH_TANGENT 0
#endif

#endif // UNITY_STANDARD_CONFIG_INCLUDED
