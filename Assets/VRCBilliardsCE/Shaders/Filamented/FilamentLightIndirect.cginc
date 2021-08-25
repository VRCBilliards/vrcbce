#ifndef FILAMENT_LIGHT_INDIRECT
#define FILAMENT_LIGHT_INDIRECT

#include "FilamentCommonOcclusion.cginc"
#include "FilamentBRDF.cginc"
#include "UnityImageBasedLightingMinimal.cginc"
#include "UnityStandardUtils.cginc"
#include "UnityLightingCommon.cginc"

//------------------------------------------------------------------------------
// Image based lighting configuration
//------------------------------------------------------------------------------

// Number of spherical harmonics bands (1, 2 or 3)
#define SPHERICAL_HARMONICS_BANDS           3

// Whether to use Geometrics' deringing lightprobe sampling.
#define SPHERICAL_HARMONICS_DEFAULT         0
#define SPHERICAL_HARMONICS_GEOMETRICS      1

#define SPHERICAL_HARMONICS SPHERICAL_HARMONICS_GEOMETRICS

// IBL integration algorithm
#define IBL_INTEGRATION_PREFILTERED_CUBEMAP         0
#define IBL_INTEGRATION_IMPORTANCE_SAMPLING         1 // Not supported!

#define IBL_INTEGRATION                             IBL_INTEGRATION_PREFILTERED_CUBEMAP

#define IBL_INTEGRATION_IMPORTANCE_SAMPLING_COUNT   64


//------------------------------------------------------------------------------
// IBL prefiltered DFG term implementations
//------------------------------------------------------------------------------

float3 PrefilteredDFG_LUT(float lod, float NoV) {
    #if defined(USE_DFG_LUT)
    // coord = sqrt(linear_roughness), which is the mapping used by cmgen.
    return tex2Dlod(_DFG, float4(NoV, lod, 0, 0)).rgb;
    #else
    // Texture not available
    return float3(1.0, 0.0, 0.0);
    #endif
}

//------------------------------------------------------------------------------
// IBL environment BRDF dispatch
//------------------------------------------------------------------------------

float3 prefilteredDFG(float perceptualRoughness, float NoV) {
    #if defined(USE_DFG_LUT)
        // PrefilteredDFG_LUT() takes a LOD, which is sqrt(roughness) = perceptualRoughness
        return PrefilteredDFG_LUT(perceptualRoughness, NoV);
    #else
        #if 1
        // Karis' approximation based on Lazarov's
        const float4 c0 = float4(-1.0, -0.0275, -0.572,  0.022);
        const float4 c1 = float4( 1.0,  0.0425,  1.040, -0.040);
        float4 r = perceptualRoughness * c0 + c1;
        float a004 = min(r.x * r.x, exp2(-9.28 * NoV)) * r.x + r.y;
        return (float3(float2(-1.04, 1.04) * a004 + r.zw, 0.0));
        #else
        // Zioma's approximation based on Karis
        return float3(float2(1.0, pow(1.0 - max(perceptualRoughness, NoV), 3.0)), 0.0);
        #endif
    #endif
}

//------------------------------------------------------------------------------
// IBL irradiance implementations
//------------------------------------------------------------------------------

/* http://www.geomerics.com/wp-content/uploads/2015/08/CEDEC_Geomerics_ReconstructingDiffuseLighting1.pdf */
float shEvaluateDiffuseL1Geomerics_local(float L0, float3 L1, float3 n)
{
    // average energy
    // Add max0 to fix an issue caused by probes having a negative ambient component (???)
    // I'm not sure how normal that is but this can't handle it
    float R0 = max(L0, 0);

    // avg direction of incoming light
    float3 R1 = 0.5f * L1;

    // directional brightness
    float lenR1 = length(R1);

    // linear angle between normal and direction 0-1
    float q = dot(normalize(R1), n) * 0.5 + 0.5;
    q = saturate(q); // Thanks to ScruffyRuffles for the bug identity.

    // power for q
    // lerps from 1 (linear) to 3 (cubic) based on directionality
    float p = 1.0f + 2.0f * lenR1 / R0;

    // dynamic range constant
    // should vary between 4 (highly directional) and 0 (ambient)
    float a = (1.0f - lenR1 / R0) / (1.0f + lenR1 / R0);

    return R0 * (a + (1.0f - a) * (p + 1.0f) * pow(q, p));
}

float3 Irradiance_SphericalHarmonics(const float3 n) {
    // Uses Unity's functions for reading SH. 
    // However, this function is currently unused. 
    float3 finalSH = float3(0,0,0); 
    #if UNITY_LIGHT_PROBE_PROXY_VOLUME
    /*
        if (unity_ProbeVolumeParams.x == 1.0)
            finalSH = SHEvalLinearL0L1_SampleProbeVolume(half4(n, 1.0), shading.position);
        else
            finalSH = SHEvalLinearL0L1(half4(n, 1.0));

        finalSH += SHEvalLinearL2(half4(n, 1.0));
    */
        return max(0, finalSH);

    #else
        #if defined(SPHERICAL_HARMONICS_DEFAULT)
            finalSH = SHEvalLinearL0L1(half4(n, 1.0));
            finalSH += SHEvalLinearL2(half4(n, 1.0));
        #endif

        #if defined(SPHERICAL_HARMONICS_GEOMETRICS)
            float3 L0 = float3(unity_SHAr.w, unity_SHAg.w, unity_SHAb.w)
            + float3(unity_SHBr.z, unity_SHBg.z, unity_SHBb.z) / 3.0;
            finalSH.r = shEvaluateDiffuseL1Geomerics_local(L0.r, unity_SHAr.xyz, n);
            finalSH.g = shEvaluateDiffuseL1Geomerics_local(L0.g, unity_SHAg.xyz, n);
            finalSH.b = shEvaluateDiffuseL1Geomerics_local(L0.b, unity_SHAb.xyz, n);
            // Quadratic polynomials
            finalSH += SHEvalLinearL2 (float4(n, 1));
            finalSH = max(finalSH, 0);
        #endif
    #endif

    return finalSH;
}

/*
float3 Irradiance_RoughnessOne(const float3 n) {
    // note: lod used is always integer, hopefully the hardware skips tri-linear filtering
    return decodeDataForIBL(textureLod(light_iblSpecular, n, frameUniforms.iblRoughnessOneLevel));
}
*/

float4 UnityLightmap_ColorIntensitySeperated(float3 lightmap) {
    lightmap += 0.000001;
    return float4(lightmap.xyz / 1, 1);
}

inline float3 DecodeDirectionalLightmapSpecular(half3 color, half4 dirTex, half3 normalWorld, 
    const bool isRealtimeLightmap, fixed4 realtimeNormalTex, out Light o_light)
{
    o_light = (Light)0;
    o_light.colorIntensity = float4(color, 1.0);
    o_light.l = dirTex.xyz * 2 - 1;

    // The length of the direction vector is the light's "directionality", i.e. 1 for all light coming from this direction,
    // lower values for more spread out, ambient light.
    half directionality = max(0.001, length(o_light.l));
    o_light.l /= directionality;

    #ifdef DYNAMICLIGHTMAP_ON
    if (isRealtimeLightmap)
    {
        // Realtime directional lightmaps' intensity needs to be divided by N.L
        // to get the incoming light intensity. Baked directional lightmaps are already
        // output like that (including the max() to prevent div by zero).
        half3 realtimeNormal = realtimeNormalTex.xyz * 2 - 1;
        o_light.colorIntensity /= max(0.125, dot(realtimeNormal, o_light.l));
    }
    #endif

    // Split light into the directional and ambient parts, according to the directionality factor.
    half3 ambient = o_light.colorIntensity * (1 - directionality);
    o_light.colorIntensity = o_light.colorIntensity * directionality;
    o_light.attenuation = directionality;

    o_light.NoL = saturate(dot(normalWorld, o_light.l));

    return ambient;
}

// Return light probes or lightmap.
float3 UnityGI_Irradiance(ShadingParams shading, float3 diffuseNormal, out float occlusion, out Light derivedLight)
{
    float3 irradiance = shading.ambient;
    occlusion = 1.0;
    derivedLight = (Light)0;

    #if UNITY_SHOULD_SAMPLE_SH
        irradiance = ShadeSHPerPixel(diffuseNormal, shading.ambient, shading.position);
    #endif

    #if defined(LIGHTMAP_ON)
        // Baked lightmaps
        half4 bakedColorTex = UNITY_SAMPLE_TEX2D(unity_Lightmap, shading.lightmapUV.xy);
        half3 bakedColor = DecodeLightmap(bakedColorTex);

        #ifdef DIRLIGHTMAP_COMBINED
            fixed4 bakedDirTex = UNITY_SAMPLE_TEX2D_SAMPLER (unity_LightmapInd, unity_Lightmap, shading.lightmapUV.xy);
            irradiance += DecodeDirectionalLightmap (bakedColor, bakedDirTex, diffuseNormal);

            #if defined(LIGHTMAP_SHADOW_MIXING) && !defined(SHADOWS_SHADOWMASK) && defined(SHADOWS_SCREEN)
                irradiance = SubtractMainLightWithRealtimeAttenuationFromLightmap (irradiance, shading.attenuation, bakedColorTex, diffuseNormal);
            #endif

            #if defined(LIGHTMAP_SPECULAR) 
                irradiance = DecodeDirectionalLightmapSpecular(bakedColor, bakedDirTex, diffuseNormal, false, 0, derivedLight);
            #endif


        #else // not directional lightmap
            irradiance += bakedColor;

            #if defined(LIGHTMAP_SHADOW_MIXING) && !defined(SHADOWS_SHADOWMASK) && defined(SHADOWS_SCREEN)
                irradiance = SubtractMainLightWithRealtimeAttenuationFromLightmap(irradiance, shading.attenuation, bakedColorTex, diffuseNormal);
            #endif

        #endif
    #endif

    #ifdef DYNAMICLIGHTMAP_ON
        // Dynamic lightmaps
        fixed4 realtimeColorTex = UNITY_SAMPLE_TEX2D(unity_DynamicLightmap, shading.lightmapUV.zw);
        half3 realtimeColor = DecodeRealtimeLightmap (realtimeColorTex);

        #ifdef DIRLIGHTMAP_COMBINED
            half4 realtimeDirTex = UNITY_SAMPLE_TEX2D_SAMPLER(unity_DynamicDirectionality, unity_DynamicLightmap, shading.lightmapUV.zw);
            irradiance += DecodeDirectionalLightmap (realtimeColor, realtimeDirTex, diffuseNormal);
        #else
            irradiance += realtimeColor;
        #endif
    #endif

    occlusion *= saturate(length(irradiance) * getExposureOcclusionBias());

    return irradiance;
}

//------------------------------------------------------------------------------
// IBL irradiance dispatch
//------------------------------------------------------------------------------

float3 get_diffuseIrradiance(const float3 n) {
        return Irradiance_SphericalHarmonics(n);
}
//------------------------------------------------------------------------------
// IBL specular
//------------------------------------------------------------------------------

UnityGIInput InitialiseUnityGIInput(const ShadingParams shading, const PixelParams pixel)
{
    UnityGIInput d;
    d.worldPos = shading.position;
    d.worldViewDir = -shading.view;
    d.probeHDR[0] = unity_SpecCube0_HDR;
    d.probeHDR[1] = unity_SpecCube1_HDR;
    #if defined(UNITY_SPECCUBE_BLENDING) || defined(UNITY_SPECCUBE_BOX_PROJECTION)
      d.boxMin[0] = unity_SpecCube0_BoxMin; // .w holds lerp value for blending
    #endif
    #ifdef UNITY_SPECCUBE_BOX_PROJECTION
      d.boxMax[0] = unity_SpecCube0_BoxMax;
      d.probePosition[0] = unity_SpecCube0_ProbePosition;
      d.boxMax[1] = unity_SpecCube1_BoxMax;
      d.boxMin[1] = unity_SpecCube1_BoxMin;
      d.probePosition[1] = unity_SpecCube1_ProbePosition;
    #endif
    return d;
}

float perceptualRoughnessToLod(float perceptualRoughness) {
    const float iblRoughnessOneLevel = 1.0/UNITY_SPECCUBE_LOD_STEPS;
    // The mapping below is a quadratic fit for log2(perceptualRoughness)+iblRoughnessOneLevel when
    // iblRoughnessOneLevel is 4. We found empirically that this mapping works very well for
    // a 256 cubemap with 5 levels used. But also scales well for other iblRoughnessOneLevel values.
    //return iblRoughnessOneLevel * perceptualRoughness * (2.0 - perceptualRoughness);

    // Unity's remapping (UNITY_SPECCUBE_LOD_STEPS is 6, not 4)
    return iblRoughnessOneLevel * perceptualRoughness * (1.7 - 0.7 * perceptualRoughness);
}

float3 prefilteredRadiance(const float3 r, float perceptualRoughness) {
    float lod = perceptualRoughnessToLod(perceptualRoughness);
    return DecodeHDR(UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0, r, lod), unity_SpecCube0_HDR);
}

float3 prefilteredRadiance(const float3 r, float roughness, float offset) {
    const float iblRoughnessOneLevel = 1.0/UNITY_SPECCUBE_LOD_STEPS;
    float lod = iblRoughnessOneLevel * roughness;
    return DecodeHDR(UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0, r, lod + offset), unity_SpecCube0_HDR);
}

half3 Unity_GlossyEnvironment_local (UNITY_ARGS_TEXCUBE(tex), half4 hdr, Unity_GlossyEnvironmentData glossIn)
{
    half perceptualRoughness = glossIn.roughness /* perceptualRoughness */ ;
    // Unity derivation
    perceptualRoughness = perceptualRoughness*(1.7 - 0.7 * perceptualRoughness);
    // Filament derivation
    // perceptualRoughness = perceptualRoughness * (2.0 - perceptualRoughness);
    half mip = perceptualRoughnessToMipmapLevel(perceptualRoughness);
    half3 R = glossIn.reflUVW;
    half4 rgbm = UNITY_SAMPLE_TEXCUBE_LOD(tex, R, mip);

    return DecodeHDR(rgbm, hdr);
}

// Workaround: Construct the correct Unity variables and get the correct Unity spec values

inline half3 UnityGI_prefilteredRadiance(const UnityGIInput data, const PixelParams pixel, const float3 r)
{
    half3 specular;

    Unity_GlossyEnvironmentData glossIn = (Unity_GlossyEnvironmentData)0;
    glossIn.roughness = pixel.perceptualRoughness;
    glossIn.reflUVW = r;

    #ifdef UNITY_SPECCUBE_BOX_PROJECTION
        // we will tweak reflUVW in glossIn directly (as we pass it to Unity_GlossyEnvironment twice for probe0 and probe1), so keep original to pass into BoxProjectedCubemapDirection
        half3 originalReflUVW = glossIn.reflUVW;
        glossIn.reflUVW = BoxProjectedCubemapDirection (originalReflUVW, data.worldPos, data.probePosition[0], data.boxMin[0], data.boxMax[0]);
    #endif

    #ifdef _GLOSSYREFLECTIONS_OFF
        specular = unity_IndirectSpecColor.rgb;
    #else
        half3 env0 = Unity_GlossyEnvironment_local (UNITY_PASS_TEXCUBE(unity_SpecCube0), data.probeHDR[0], glossIn);
        #ifdef UNITY_SPECCUBE_BLENDING
            const float kBlendFactor = 0.99999;
            float blendLerp = data.boxMin[0].w;
            UNITY_BRANCH
            if (blendLerp < kBlendFactor)
            {
                #ifdef UNITY_SPECCUBE_BOX_PROJECTION
                    glossIn.reflUVW = BoxProjectedCubemapDirection (originalReflUVW, data.worldPos, data.probePosition[1], data.boxMin[1], data.boxMax[1]);
                #endif

                half3 env1 = Unity_GlossyEnvironment_local (UNITY_PASS_TEXCUBE_SAMPLER(unity_SpecCube1,unity_SpecCube0), data.probeHDR[1], glossIn);
                specular = lerp(env1, env0, blendLerp);
            }
            else
            {
                specular = env0;
            }
        #else
            specular = env0;
        #endif
    #endif

    return specular;
}

float3 getSpecularDominantDirection(const float3 n, const float3 r, float roughness) {
    return lerp(r, n, roughness * roughness);
}

float3 specularDFG(const PixelParams pixel) {
    // Disabled until DFG is implemented properly
    return pixel.f0;
#if defined(SHADING_MODEL_CLOTH)
    return pixel.f0 * pixel.dfg.z;
#else
    return lerp(pixel.dfg.xxx, pixel.dfg.yyy, pixel.f0);
#endif
}

/**
 * Returns the reflected vector at the current shading point. The reflected vector
 * return by this function might be different from shading.reflected:
 * - For anisotropic material, we bend the reflection vector to simulate
 *   anisotropic indirect lighting
 * - The reflected vector may be modified to point towards the dominant specular
 *   direction to match reference renderings when the roughness increases
 */

float3 getReflectedVector(const PixelParams pixel, const float3 v, const float3 n) {
#if defined(MATERIAL_HAS_ANISOTROPY)
    float3  anisotropyDirection = pixel.anisotropy >= 0.0 ? pixel.anisotropicB : pixel.anisotropicT;
    float3  anisotropicTangent  = cross(anisotropyDirection, v);
    float3  anisotropicNormal   = cross(anisotropicTangent, anisotropyDirection);
    float bendFactor          = abs(pixel.anisotropy) * saturate(5.0 * pixel.perceptualRoughness);
    float3  bentNormal          = normalize(lerp(n, anisotropicNormal, bendFactor));

    float3 r = reflect(-v, bentNormal);
#else
    float3 r = reflect(-v, n);
#endif
    return r;
}

float3 getReflectedVector(const ShadingParams shading, const PixelParams pixel, const float3 n) {
#if defined(MATERIAL_HAS_ANISOTROPY)
    float3 r = getReflectedVector(pixel, shading.view, n);
#else
    float3 r = shading.reflected;
#endif
    return getSpecularDominantDirection(n, r, pixel.roughness);
}

//------------------------------------------------------------------------------
// Prefiltered importance sampling
//------------------------------------------------------------------------------

#if IBL_INTEGRATION == IBL_INTEGRATION_IMPORTANCE_SAMPLING

void isEvaluateClearCoatIBL(const ShadingParams shading, const PixelParams pixel, 
    float specularAO, inout float3 Fd, inout float3 Fr) {
#if defined(MATERIAL_HAS_CLEAR_COAT)
#if defined(MATERIAL_HAS_NORMAL) || defined(MATERIAL_HAS_CLEAR_COAT_NORMAL)
    // We want to use the geometric normal for the clear coat layer
    float clearCoatNoV = clampNoV(dot(shading.clearCoatNormal, shading.view));
    float3 clearCoatNormal = shading.clearCoatNormal;
#else
    float clearCoatNoV = shading.NoV;
    float3 clearCoatNormal = shading.normal;
#endif
    // The clear coat layer assumes an IOR of 1.5 (4% reflectance)
    float Fc = F_Schlick(0.04, 1.0, clearCoatNoV) * pixel.clearCoat;
    float attenuation = 1.0 - Fc;
    Fd *= attenuation;
    Fr *= attenuation;

    PixelParams p;
    p.perceptualRoughness = pixel.clearCoatPerceptualRoughness;
    p.f0 = float3(0.04.xxx);
    p.roughness = perceptualRoughnessToRoughness(p.perceptualRoughness);
#if defined(MATERIAL_HAS_ANISOTROPY)
    p.anisotropy = 0.0;
#endif

    float3 clearCoatLobe = isEvaluateSpecularIBL(p, clearCoatNormal, shading.view, clearCoatNoV);
    Fr += clearCoatLobe * (specularAO * pixel.clearCoat);
#endif
}
#endif


//------------------------------------------------------------------------------
// IBL evaluation
//------------------------------------------------------------------------------

void evaluateClothIndirectDiffuseBRDF(const ShadingParams shading, const PixelParams pixel, 
    inout float diffuse) {
#if defined(SHADING_MODEL_CLOTH)
#if defined(MATERIAL_HAS_SUBSURFACE_COLOR)
    // Simulate subsurface scattering with a wrap diffuse term
    diffuse *= Fd_Wrap(shading.NoV, 0.5);
#endif
#endif
}

void evaluateSheenIBL(const ShadingParams shading, const PixelParams pixel, 
    float specularAO, inout float3 Fd, inout float3 Fr) {
#if !defined(SHADING_MODEL_CLOTH) && !defined(SHADING_MODEL_SUBSURFACE)
#if defined(MATERIAL_HAS_SHEEN_COLOR)
    // Albedo scaling of the base layer before we layer sheen on top
    Fd *= pixel.sheenScaling;
    Fr *= pixel.sheenScaling;

    float3 reflectance = pixel.sheenDFG * pixel.sheenColor;
    Fr += reflectance * prefilteredRadiance(shading.reflected, pixel.sheenPerceptualRoughness);
#endif
#endif
}

void evaluateClearCoatIBL(const ShadingParams shading, const PixelParams pixel, 
    float specularAO, inout float3 Fd, inout float3 Fr) {
#if IBL_INTEGRATION == IBL_INTEGRATION_IMPORTANCE_SAMPLING
    isEvaluateClearCoatIBL(pixel, specularAO, Fd, Fr);
    return;
#endif

#if defined(MATERIAL_HAS_CLEAR_COAT)
#if defined(MATERIAL_HAS_NORMAL) || defined(MATERIAL_HAS_CLEAR_COAT_NORMAL)
    // We want to use the geometric normal for the clear coat layer
    float clearCoatNoV = clampNoV(dot(shading.clearCoatNormal, shading.view));
    float3 clearCoatR = reflect(-shading.view, shading.clearCoatNormal);
#else
    float clearCoatNoV = shading.NoV;
    float3 clearCoatR = shading.reflected;
#endif
    // The clear coat layer assumes an IOR of 1.5 (4% reflectance)
    float Fc = F_Schlick(0.04, 1.0, clearCoatNoV) * pixel.clearCoat;
    float attenuation = 1.0 - Fc;
    Fd *= attenuation;
    Fr *= attenuation;
    Fr += prefilteredRadiance(clearCoatR, pixel.clearCoatPerceptualRoughness) * (specularAO * Fc);
#endif
}

void evaluateSubsurfaceIBL(const ShadingParams shading, const PixelParams pixel, 
    const float3 diffuseIrradiance, inout float3 Fd, inout float3 Fr) {
#if defined(SHADING_MODEL_SUBSURFACE)
    float3 viewIndependent = diffuseIrradiance;
    float3 viewDependent = prefilteredRadiance(-shading.view, pixel.roughness, 1.0 + pixel.thickness);
    float attenuation = (1.0 - pixel.thickness) / (2.0 * PI);
    Fd += pixel.subsurfaceColor * (viewIndependent + viewDependent) * attenuation;
#elif defined(SHADING_MODEL_CLOTH) && defined(MATERIAL_HAS_SUBSURFACE_COLOR)
    Fd *= saturate(pixel.subsurfaceColor + shading.NoV);
#endif
}

void combineDiffuseAndSpecular(const PixelParams pixel,
        const float3 n, const float3 E, const float3 Fd, const float3 Fr,
        inout float3 color) {
    const float iblLuminance = 1.0; // Unknown
#if defined(HAS_REFRACTION)
    applyRefraction(pixel, n, E, Fd, Fr, color);
#else
    color.rgb += (Fd + Fr) * iblLuminance;
#endif
}

void evaluateIBL(const ShadingParams shading, const MaterialInputs material, const PixelParams pixel, 
    inout float3 color) {
    float ssao = 1.0; // Not implemented
    float lightmapAO = 1.0; // 
    Light derivedLight = (Light)0;

    // Gather Unity GI data
    UnityGIInput unityData = InitialiseUnityGIInput(shading, pixel);
    float3 unityIrradiance = UnityGI_Irradiance(shading, shading.normal, lightmapAO, derivedLight);

    float diffuseAO = min(material.ambientOcclusion, ssao);
    float specularAO = computeSpecularAO(shading.NoV, diffuseAO*lightmapAO, pixel.roughness);

    // specular layer
    float3 Fr;
#if IBL_INTEGRATION == IBL_INTEGRATION_PREFILTERED_CUBEMAP
    float3 E = specularDFG(pixel);
    float3 r = getReflectedVector(shading, pixel, shading.normal);
    Fr = E * UnityGI_prefilteredRadiance(unityData, pixel, r);
#elif IBL_INTEGRATION == IBL_INTEGRATION_IMPORTANCE_SAMPLING
    // Not supported
    float3 E = float3(0.0); // TODO: fix for importance sampling
    Fr = isEvaluateSpecularIBL(pixel, shading.normal, shading.view, shading.NoV);
#endif
    Fr *= singleBounceAO(specularAO) * pixel.energyCompensation;

    // diffuse layer
    float diffuseBRDF = singleBounceAO(diffuseAO); // Fd_Lambert() is baked in the SH below

    evaluateClothIndirectDiffuseBRDF(shading, pixel, diffuseBRDF);

#if defined(MATERIAL_HAS_BENT_NORMAL)
    float3 diffuseNormal = shading.bentNormal;
#else
    float3 diffuseNormal = shading.normal;
#endif

#if IBL_INTEGRATION == IBL_INTEGRATION_PREFILTERED_CUBEMAP
    //float3 diffuseIrradiance = get_diffuseIrradiance(diffuseNormal);
    float3 diffuseIrradiance = unityIrradiance;
#elif IBL_INTEGRATION == IBL_INTEGRATION_IMPORTANCE_SAMPLING
    float3 diffuseIrradiance = isEvaluateDiffuseIBL(pixel, diffuseNormal, shading.view);
#endif

    float3 Fd = pixel.diffuseColor * diffuseIrradiance * (1.0 - E) * diffuseBRDF;

    // sheen layer
    evaluateSheenIBL(shading, pixel, specularAO, Fd, Fr);

    // clear coat layer
    evaluateClearCoatIBL(shading, pixel, specularAO, Fd, Fr);

    // subsurface layer
    evaluateSubsurfaceIBL(shading, pixel, diffuseIrradiance, Fd, Fr);

    // extra ambient occlusion term
    multiBounceAO(diffuseAO, pixel.diffuseColor, Fd);
    multiBounceSpecularAO(specularAO, pixel.f0, Fr);
    
    // Note: iblLuminance is already premultiplied by the exposure
    combineDiffuseAndSpecular(pixel, shading.normal, E, Fd, Fr, color);

    #if defined(LIGHTMAP_SPECULAR)
    if (derivedLight.NoL >= 0.0) color += surfaceShading(shading, pixel, derivedLight, computeMicroShadowing(derivedLight.NoL, material.ambientOcclusion * 0.8 + 0.3));
    #endif
}

#endif // FILAMENT_LIGHT_INDIRECT