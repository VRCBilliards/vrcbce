// Unity built-in shader source. Copyright (c) 2016 Unity Technologies. MIT license (see license.txt)

#ifndef UNITY_STANDARD_CORE_INCLUDED
#define UNITY_STANDARD_CORE_INCLUDED

#include "UnityCG.cginc"
#include "UnityShaderVariables.cginc"
#include "UnityLightingCommon.cginc"

// Workaround for some failsafes
#define UNITY_BRDF_PBS 

#include "FilamentMaterialInputs.cginc"
#include "FilamentCommonMath.cginc"
#include "FilamentCommonLighting.cginc"
#include "FilamentCommonMaterial.cginc"
#include "FilamentCommonShading.cginc"
#include "FilamentShadingParameters.cginc"

#include "UnityStandardConfig.cginc"
#include "UnityStandardInput.cginc"
//#include "UnityPBSLighting.cginc"
#include "UnityStandardUtils.cginc"
#include "UnityImageBasedLightingMinimal.cginc"
#include "UnityGBuffer.cginc"
#include "UnityGlobalIllumination.cginc"

#include "FilamentBRDF.cginc"
#include "FilamentShadingStandard.cginc"
#include "FilamentLightIndirect.cginc"
#include "FilamentLightDirectional.cginc"
#include "FilamentLightPunctual.cginc"
#include "FilamentShadingLit.cginc"

#include "AutoLight.cginc"
//-------------------------------------------------------------------------------------
// counterpart for NormalizePerPixelNormal
// skips normalization per-vertex and expects normalization to happen per-pixel
half3 NormalizePerVertexNormal (float3 n) // takes float to avoid overflow
{
    #if (SHADER_TARGET < 30) || UNITY_STANDARD_SIMPLE
        return normalize(n);
    #else
        return n; // will normalize per-pixel instead
    #endif
}

float3 NormalizePerPixelNormal (float3 n)
{
    #if (SHADER_TARGET < 30) || UNITY_STANDARD_SIMPLE
        return n;
    #else
        return normalize((float3)n); // takes float to avoid overflow
    #endif
}

//-------------------------------------------------------------------------------------
void GetBakedAttenuation(inout float atten, float2 lightmapUV, float3 worldPos)
{
    // Base pass with Lightmap support is responsible for handling ShadowMask / blending here for performance reason
    #if defined(HANDLE_SHADOWS_BLENDING_IN_GI)
        half bakedAtten = UnitySampleBakedOcclusion(lightmapUV.xy, worldPos);
        float zDist = dot(_WorldSpaceCameraPos - worldPos, UNITY_MATRIX_V[2].xyz);
        float fadeDist = UnityComputeShadowFadeDistance(worldPos, zDist);
        atten = UnityMixRealtimeAndBakedShadows(atten, bakedAtten, UnityComputeShadowFade(fadeDist));
    #endif
}

//-------------------------------------------------------------------------------------
// Common fragment setup

#ifdef _PARALLAXMAP
    #define IN_VIEWDIR4PARALLAX(i) NormalizePerPixelNormal(half3(i.tangentToWorldAndPackedData[0].w,i.tangentToWorldAndPackedData[1].w,i.tangentToWorldAndPackedData[2].w))
    #define IN_VIEWDIR4PARALLAX_FWDADD(i) NormalizePerPixelNormal(i.viewDirForParallax.xyz)
#else
    #define IN_VIEWDIR4PARALLAX(i) half3(0,0,0)
    #define IN_VIEWDIR4PARALLAX_FWDADD(i) half3(0,0,0)
#endif

#if UNITY_REQUIRE_FRAG_WORLDPOS
    #if UNITY_PACK_WORLDPOS_WITH_TANGENT
        #define IN_WORLDPOS(i) half3(i.tangentToWorldAndPackedData[0].w,i.tangentToWorldAndPackedData[1].w,i.tangentToWorldAndPackedData[2].w)
    #else
        #define IN_WORLDPOS(i) i.posWorld
    #endif
    #define IN_WORLDPOS_FWDADD(i) i.posWorld
#else
    #define IN_WORLDPOS(i) half3(0,0,0)
    #define IN_WORLDPOS_FWDADD(i) half3(0,0,0)
#endif

#define IN_LIGHTDIR_FWDADD(i) half3(i.tangentToWorldAndLightDir[0].w, i.tangentToWorldAndLightDir[1].w, i.tangentToWorldAndLightDir[2].w)

#define MATERIAL_SETUP(x) MaterialInputs x = \
    MaterialSetup(i.tex, i.eyeVec.xyz, IN_VIEWDIR4PARALLAX(i), i.tangentToWorldAndPackedData, IN_WORLDPOS(i));

#define MATERIAL_SETUP_FWDADD(x) MaterialInputs x = \
    MaterialSetup(i.tex, i.eyeVec.xyz, IN_VIEWDIR4PARALLAX_FWDADD(i), i.tangentToWorldAndLightDir, IN_WORLDPOS_FWDADD(i));


// Filament's preferred model, but not Unity's default
#if defined(SHADING_MODEL_METALLIC_ROUGHNESS)
    #define SETUP_BRDF_INPUT RoughnessMaterialSetup
inline MaterialInputs RoughnessMaterialSetup (float4 i_tex)
{   
    half4 baseColor = half4(Albedo(i_tex), Alpha(i_tex));
    half2 metallicGloss = MetallicRough(i_tex.xy);
    half metallic = metallicGloss.x;
    half smoothness = metallicGloss.y; // this is 1 minus the square root of real roughness m.

    MaterialInputs material = (MaterialInputs)0;
    initMaterial(material);
    material.baseColor = baseColor;
    material.metallic = metallic;
    material.roughness = computeRoughnessFromGlossiness(smoothness);
    return material;
}
#else

#if defined(SHADING_MODEL_SPECULAR_GLOSSINESS)
    #define SETUP_BRDF_INPUT SpecularMaterialSetup
inline MaterialInputs SpecularMaterialSetup (float4 i_tex)
{   
    half4 baseColor = half4(Albedo(i_tex), Alpha(i_tex));
    half4 specGloss = SpecularGloss(i_tex.xy);
    half3 specColor = specGloss.rgb;
    half smoothness = specGloss.a;

    MaterialInputs material = (MaterialInputs)0;
    initMaterial(material);
    material.baseColor = baseColor;
    material.specularColor = specColor;
    material.glossiness = smoothness;
    return material;
}
#endif

#if (!defined(SHADING_MODEL_SPECULAR_GLOSSINESS))
    #define SETUP_BRDF_INPUT MetallicMaterialSetup
inline MaterialInputs MetallicMaterialSetup (float4 i_tex)
{   
    half4 baseColor = half4(Albedo(i_tex), Alpha(i_tex));
    half2 metallicGloss = MetallicGloss(i_tex.xy); 
    half metallic = metallicGloss.x;
    half smoothness = metallicGloss.y; // this is 1 minus the square root of real roughness m.

    MaterialInputs material = (MaterialInputs)0;
    initMaterial(material);
    material.baseColor = baseColor;
    material.metallic = metallic;
    material.roughness = computeRoughnessFromGlossiness(smoothness);
    return material;
}
#endif
#endif

#ifndef SETUP_BRDF_INPUT 
    #define SETUP_BRDF_INPUT NoneMaterialSetup
#endif

inline MaterialInputs NoneMaterialSetup (float4 i_tex)
{   
    MaterialInputs material = (MaterialInputs)0;
    initMaterial(material);
    return material;
}

// parallax transformed texcoord is used to sample occlusion
inline MaterialInputs MaterialSetup (inout float4 i_tex, float3 i_eyeVec, half3 i_viewDirForParallax, float4 tangentToWorld[3], float3 i_posWorld)
{
    i_tex = Parallax(i_tex, i_viewDirForParallax);

    MaterialInputs material = SETUP_BRDF_INPUT (i_tex);

    // Added tangent output
    #if _NORMALMAP
    material.normal = NormalInTangentSpace(i_tex);
    #endif
    #if _EMISSION
    material.emissive.rgb = Emission (i_tex);
    material.emissive.a = 1.0;
    #endif
    material.ambientOcclusion = Occlusion(i_tex);
    return material;
}


//-------------------------------------------------------------------------------------
inline half4 VertexGIForward(VertexInput v, float3 posWorld, half3 normalWorld)
{
    half4 ambientOrLightmapUV = 0;
    // Static lightmaps
    #ifdef LIGHTMAP_ON
        ambientOrLightmapUV.xy = v.uv1.xy * unity_LightmapST.xy + unity_LightmapST.zw;
        ambientOrLightmapUV.zw = 0;
    // Sample light probe for Dynamic objects only (no static or dynamic lightmaps)
    #elif UNITY_SHOULD_SAMPLE_SH
        #ifdef VERTEXLIGHT_ON
            // Approximated illumination from non-important point lights
            ambientOrLightmapUV.rgb = Shade4PointLights (
                unity_4LightPosX0, unity_4LightPosY0, unity_4LightPosZ0,
                unity_LightColor[0].rgb, unity_LightColor[1].rgb, unity_LightColor[2].rgb, unity_LightColor[3].rgb,
                unity_4LightAtten0, posWorld, normalWorld);
        #endif

        ambientOrLightmapUV.rgb = ShadeSHPerVertex (normalWorld, ambientOrLightmapUV.rgb);
    #endif

    #ifdef DYNAMICLIGHTMAP_ON
        ambientOrLightmapUV.zw = v.uv2.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
    #endif

    return ambientOrLightmapUV;
}

// ------------------------------------------------------------------
//  Base forward pass (directional light, emission, lightmaps, ...)

struct VertexOutputForwardBase
{
    UNITY_POSITION(pos);
    float4 tex                            : TEXCOORD0;
    float4 eyeVec                         : TEXCOORD1;    // eyeVec.xyz | fogCoord
    float4 tangentToWorldAndPackedData[3] : TEXCOORD2;    // [3x3:tangentToWorld | 1x3:viewDirForParallax or worldPos]
    half4 ambientOrLightmapUV             : TEXCOORD5;    // SH or Lightmap UV
    UNITY_LIGHTING_COORDS(6,7)

    // next ones would not fit into SM2.0 limits, but they are always for SM3.0+
#if UNITY_REQUIRE_FRAG_WORLDPOS && !UNITY_PACK_WORLDPOS_WITH_TANGENT
    float3 posWorld                     : TEXCOORD8;
#endif

#if defined(NORMALMAP_SHADOW)
    float3 lightDirTS                   : TEXCOORD9;
#endif

    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

VertexOutputForwardBase vertForwardBase (VertexInput v)
{
    UNITY_SETUP_INSTANCE_ID(v);
    VertexOutputForwardBase o;
    UNITY_INITIALIZE_OUTPUT(VertexOutputForwardBase, o);
    UNITY_TRANSFER_INSTANCE_ID(v, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

    float4 posWorld = mul(unity_ObjectToWorld, v.vertex);
    #if UNITY_REQUIRE_FRAG_WORLDPOS
        #if UNITY_PACK_WORLDPOS_WITH_TANGENT
            o.tangentToWorldAndPackedData[0].w = posWorld.x;
            o.tangentToWorldAndPackedData[1].w = posWorld.y;
            o.tangentToWorldAndPackedData[2].w = posWorld.z;
        #else
            o.posWorld = posWorld.xyz;
        #endif
    #endif
    o.pos = UnityObjectToClipPos(v.vertex);

    o.tex = TexCoords(v);
    o.eyeVec.xyz = NormalizePerVertexNormal(posWorld.xyz - _WorldSpaceCameraPos);
    float3 normalWorld = UnityObjectToWorldNormal(v.normal);
    #ifdef _TANGENT_TO_WORLD
        float4 tangentWorld = float4(UnityObjectToWorldDir(v.tangent.xyz), v.tangent.w);

        float3x3 tangentToWorld = CreateTangentToWorldPerVertex(normalWorld, tangentWorld.xyz, tangentWorld.w);
        o.tangentToWorldAndPackedData[0].xyz = tangentToWorld[0];
        o.tangentToWorldAndPackedData[1].xyz = tangentToWorld[1];
        o.tangentToWorldAndPackedData[2].xyz = tangentToWorld[2];
    #else
        o.tangentToWorldAndPackedData[0].xyz = 0;
        o.tangentToWorldAndPackedData[1].xyz = 0;
        o.tangentToWorldAndPackedData[2].xyz = normalWorld;
    #endif

    //We need this for shadow receving
    UNITY_TRANSFER_LIGHTING(o, v.uv1);

    o.ambientOrLightmapUV = VertexGIForward(v, posWorld, normalWorld);

    #ifdef _PARALLAXMAP
        TANGENT_SPACE_ROTATION;
        half3 viewDirForParallax = mul (rotation, ObjSpaceViewDir(v.vertex));
        o.tangentToWorldAndPackedData[0].w = viewDirForParallax.x;
        o.tangentToWorldAndPackedData[1].w = viewDirForParallax.y;
        o.tangentToWorldAndPackedData[2].w = viewDirForParallax.z;
    #endif

    #ifdef _TANGENT_TO_WORLD
    #if defined(NORMALMAP_SHADOW)
    float3 lightDirWS = normalize(_WorldSpaceLightPos0.xyz - posWorld.xyz * _WorldSpaceLightPos0.w);
    o.lightDirTS = TransformToTangentSpace(tangentToWorld[0],tangentToWorld[1],tangentToWorld[2],lightDirWS);
    #endif
    #endif

    UNITY_TRANSFER_FOG_COMBINED_WITH_EYE_VEC(o,o.pos);
    return o;
}

void computeShadingParamsForwardBase(inout ShadingParams shading, VertexOutputForwardBase i)
{
    float3x3 tangentToWorld;
    tangentToWorld[0] = i.tangentToWorldAndPackedData[0].xyz;
    tangentToWorld[1] = i.tangentToWorldAndPackedData[1].xyz;
    tangentToWorld[2] = i.tangentToWorldAndPackedData[2].xyz;
    shading.tangentToWorld = transpose(tangentToWorld);
    shading.geometricNormal = normalize(i.tangentToWorldAndPackedData[2].xyz);

    shading.normalizedViewportCoord = i.pos.xy * (0.5 / i.pos.w) + 0.5;

    shading.normal = (shading.geometricNormal);
    shading.position = IN_WORLDPOS(i);
    shading.view = -NormalizePerPixelNormal(i.eyeVec);

    UNITY_LIGHT_ATTENUATION(atten, i, shading.position)

    #if (defined(LIGHTMAP_ON) || defined(DYNAMICLIGHTMAP_ON))
    GetBakedAttenuation(atten, i.ambientOrLightmapUV.xy, shading.position);
    #endif

    shading.attenuation = atten;

    #if (defined(LIGHTMAP_ON) || defined(DYNAMICLIGHTMAP_ON))
        shading.ambient = 0;
        shading.lightmapUV = i.ambientOrLightmapUV;
    #else
        shading.ambient = i.ambientOrLightmapUV.rgb;
        shading.lightmapUV = 0;
    #endif
}

half4 fragForwardBaseInternal (VertexOutputForwardBase i)
{
    UNITY_APPLY_DITHER_CROSSFADE(i.pos.xy);

    MATERIAL_SETUP(material)

    UNITY_SETUP_INSTANCE_ID(i);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

    ShadingParams shading = (ShadingParams)0;
    // Initialize shading with expected parameters
    computeShadingParamsForwardBase(shading, i);

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

half4 fragForwardBase (VertexOutputForwardBase i) : SV_Target   // backward compatibility (this used to be the fragment entry function)
{
    return fragForwardBaseInternal(i);
}

// ------------------------------------------------------------------
//  Additive forward pass (one light per pass)

struct VertexOutputForwardAdd
{
    UNITY_POSITION(pos);
    float4 tex                          : TEXCOORD0;
    float4 eyeVec                       : TEXCOORD1;    // eyeVec.xyz | fogCoord
    float4 tangentToWorldAndLightDir[3] : TEXCOORD2;    // [3x3:tangentToWorld | 1x3:lightDir]
    float3 posWorld                     : TEXCOORD5;
    UNITY_LIGHTING_COORDS(6, 7)

    // next ones would not fit into SM2.0 limits, but they are always for SM3.0+
#if defined(_PARALLAXMAP)
    half3 viewDirForParallax            : TEXCOORD8;
#endif

#if (defined(_NORMALMAP) && defined(NORMALMAP_SHADOW))
    float3 lightDirTS                   : TEXCOORD9;
#endif

    UNITY_VERTEX_OUTPUT_STEREO
};

VertexOutputForwardAdd vertForwardAdd (VertexInput v)
{
    UNITY_SETUP_INSTANCE_ID(v);
    VertexOutputForwardAdd o;
    UNITY_INITIALIZE_OUTPUT(VertexOutputForwardAdd, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

    float4 posWorld = mul(unity_ObjectToWorld, v.vertex);
    o.pos = UnityObjectToClipPos(v.vertex);

    o.tex = TexCoords(v);
    o.eyeVec.xyz = NormalizePerVertexNormal(posWorld.xyz - _WorldSpaceCameraPos);
    o.posWorld = posWorld.xyz;
    float3 normalWorld = UnityObjectToWorldNormal(v.normal);
    #ifdef _TANGENT_TO_WORLD
        float4 tangentWorld = float4(UnityObjectToWorldDir(v.tangent.xyz), v.tangent.w);

        float3x3 tangentToWorld = CreateTangentToWorldPerVertex(normalWorld, tangentWorld.xyz, tangentWorld.w);
        o.tangentToWorldAndLightDir[0].xyz = tangentToWorld[0];
        o.tangentToWorldAndLightDir[1].xyz = tangentToWorld[1];
        o.tangentToWorldAndLightDir[2].xyz = tangentToWorld[2];
    #else
        o.tangentToWorldAndLightDir[0].xyz = 0;
        o.tangentToWorldAndLightDir[1].xyz = 0;
        o.tangentToWorldAndLightDir[2].xyz = normalWorld;
    #endif
    //We need this for shadow receiving and lighting
    UNITY_TRANSFER_LIGHTING(o, v.uv1);

    float3 lightDir = _WorldSpaceLightPos0.xyz - posWorld.xyz * _WorldSpaceLightPos0.w;
    #ifndef USING_DIRECTIONAL_LIGHT
        lightDir = NormalizePerVertexNormal(lightDir);
    #endif
    o.tangentToWorldAndLightDir[0].w = lightDir.x;
    o.tangentToWorldAndLightDir[1].w = lightDir.y;
    o.tangentToWorldAndLightDir[2].w = lightDir.z;

    #ifdef _PARALLAXMAP
        TANGENT_SPACE_ROTATION;
        o.viewDirForParallax = mul (rotation, ObjSpaceViewDir(v.vertex));
    #endif

    #ifdef _TANGENT_TO_WORLD
    #if (defined(_NORMALMAP) && defined(NORMALMAP_SHADOW))
    float3 lightDirWS = normalize(_WorldSpaceLightPos0.xyz - posWorld.xyz * _WorldSpaceLightPos0.w);
    o.lightDirTS = TransformToTangentSpace(tangentToWorld[0],tangentToWorld[1],tangentToWorld[2],lightDirWS);
    #endif
    #endif

    UNITY_TRANSFER_FOG_COMBINED_WITH_EYE_VEC(o, o.pos);
    return o;
}

void computeShadingParamsForwardAdd(inout ShadingParams shading, VertexOutputForwardAdd i)
{
    float3x3 tangentToWorld;
    tangentToWorld[0] = i.tangentToWorldAndLightDir[0].xyz;
    tangentToWorld[1] = i.tangentToWorldAndLightDir[1].xyz;
    tangentToWorld[2] = i.tangentToWorldAndLightDir[2].xyz;
    shading.tangentToWorld = transpose(tangentToWorld);
    shading.geometricNormal = normalize(i.tangentToWorldAndLightDir[2].xyz);

    shading.normalizedViewportCoord = i.pos.xy * (0.5 / i.pos.w) + 0.5;
    shading.normal = normalize(shading.geometricNormal);
    shading.position = IN_WORLDPOS_FWDADD(i);
    shading.view = -NormalizePerPixelNormal(i.eyeVec);

    UNITY_LIGHT_ATTENUATION(atten, i, shading.position)
    shading.attenuation = atten;
}

half4 fragForwardAddInternal (VertexOutputForwardAdd i)
{
    UNITY_APPLY_DITHER_CROSSFADE(i.pos.xy);

    MATERIAL_SETUP_FWDADD(material)

    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

    ShadingParams shading = (ShadingParams)0;
    // Initialize shading with expected parameters
    computeShadingParamsForwardAdd(shading, i);

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

half4 fragForwardAdd (VertexOutputForwardAdd i) : SV_Target     // backward compatibility (this used to be the fragment entry function)
{
    return fragForwardAddInternal(i);
}

#endif // UNITY_STANDARD_CORE_INCLUDED
