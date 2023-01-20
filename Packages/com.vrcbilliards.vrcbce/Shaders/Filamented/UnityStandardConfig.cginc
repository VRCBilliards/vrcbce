// Unity built-in shader source. Copyright (c) 2016 Unity Technologies. MIT license (see license.txt)

#ifndef UNITY_STANDARD_CONFIG_INCLUDED
#define UNITY_STANDARD_CONFIG_INCLUDED

// Filamented configuration

#define FILAMENT_QUALITY_LOW    0
#define FILAMENT_QUALITY_NORMAL 1
#define FILAMENT_QUALITY_HIGH   2

// Sets a different quality level for mobile and other platforms.
#if !(defined(SHADER_API_MOBILE))
#define FILAMENT_QUALITY FILAMENT_QUALITY_HIGH
#else
#define FILAMENT_QUALITY FILAMENT_QUALITY_LOW
#define TARGET_MOBILE
#endif

#if !defined(SPECULAR_AMBIENT_OCCLUSION)
#define SPECULAR_AMBIENT_OCCLUSION SPECULAR_AO_BENT_NORMALS
#endif

#define MULTI_BOUNCE_AMBIENT_OCCLUSION 1

// Whether to use specular AA by default 
// in shaders that don't specify USE_GEOMETRIC_SPECULAR_AA.
#if !defined(USE_GEOMETRIC_SPECULAR_AA)
#define USE_GEOMETRIC_SPECULAR_AA_DEFAULT
#endif


// Whether to read the _DFG texture for DFG instead of the approximate one. 
// The shader must have a _DFG texture property, so don't enable this here.
// For example, you can use this ShaderLab semantic to specify a DFG texture.
// [NonModifiableTextureData][HideInInspector] _DFG("DFG", 2D) = "white" {}
// Then, set the default texture on the .shader in Unity
// and it will propagate to all materials.
// #define USE_DFG_LUT

// Filament cross-compatibility defines

#if (DIRECTIONAL || DIRECTIONAL_COOKIE)
#define HAS_DIRECTIONAL_LIGHTING 
#endif
#if (POINT || SPOT || POINT_COOKIE)
#define HAS_DYNAMIC_LIGHTING 
#endif
#if (SHADOWS_SCREEN || SHADOWS_SHADOWMASK || LIGHTMAP_SHADOW || DIRECTIONAL_COOKIE)
#define HAS_SHADOWING 
#endif

#if _EMISSION
#define MATERIAL_HAS_EMISSIVE 
#endif
#if defined(MATERIAL_HAS_NORMAL)
#define _NORMALMAP 1
#endif
#if _NORMALMAP
#define MATERIAL_HAS_NORMAL
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

#ifndef NEEDS_ALPHA_CHANNEL
#define NEEDS_ALPHA_CHANNEL 0
#endif

// By default, Standard assumes meshes have normals
#define HAS_ATTRIBUTE_TANGENTS

//#define MATERIAL_HAS_AMBIENT_OCCLUSION 

// If USE_GEOMETRIC_SPECULAR_AA is set, don't use the default values
#if defined(USE_GEOMETRIC_SPECULAR_AA)
#define GEOMETRIC_SPECULAR_AA
#else
#if defined(USE_GEOMETRIC_SPECULAR_AA_DEFAULT)
#define GEOMETRIC_SPECULAR_AA
#define _specularAntiAliasingVariance 0.15
#define _specularAntiAliasingThreshold 0.25
#endif
#endif

#if defined(_LIGHTMAPSPECULAR)
#define LIGHTMAP_SPECULAR
#endif

#if defined(_NORMALMAP_SHADOW) && defined(MATERIAL_HAS_NORMAL) && defined(HAS_SHADOWING)
#define NORMALMAP_SHADOW
#endif

#if defined(USE_DFG_LUT)
UNITY_DECLARE_TEX2D_FLOAT(_DFG);
#endif

#if defined(_BAKERY_RNM) || defined(_BAKERY_SH)
#define USING_BAKERY
UNITY_DECLARE_TEX2D_HALF(_RNM0);
UNITY_DECLARE_TEX2D_HALF(_RNM1);
UNITY_DECLARE_TEX2D_HALF(_RNM2);
#endif

// For MonoSH, the extra textures aren't used. 
#if defined(_BAKERY_MONOSH)
#endif

// Refraction source texture
#if REFRACTION_MODE == REFRACTION_MODE_SCREEN
    #ifndef REFRACTION_SOURCE
    #define REFRACTION_SOURCE _GrabPass
    #define REFRACTION_MULTIPLIER 1.0
    #endif
UNITY_DECLARE_TEX2D_FLOAT(REFRACTION_SOURCE);
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
