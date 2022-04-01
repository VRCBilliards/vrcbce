#ifndef FILAMENT_LIGHT_INDIRECT
#define FILAMENT_LIGHT_INDIRECT

#include "FilamentCommonOcclusion.cginc"
#include "FilamentCommonGraphics.cginc"
#include "FilamentBRDF.cginc"
#include "UnityImageBasedLightingMinimal.cginc"
#include "UnityStandardUtils.cginc"
#include "UnityLightingCommon.cginc"
#include "SharedFilteringLib.hlsl"
#include "FilamentLightLTCGI.cginc"

//------------------------------------------------------------------------------
// Image based lighting configuration
//------------------------------------------------------------------------------

// Whether to use Geometrics' deringing lightprobe sampling.
#define SPHERICAL_HARMONICS_DEFAULT         0
#define SPHERICAL_HARMONICS_GEOMETRICS      1

#define SPHERICAL_HARMONICS SPHERICAL_HARMONICS_GEOMETRICS

// IBL integration algorithm
#define IBL_INTEGRATION_PREFILTERED_CUBEMAP         0
#define IBL_INTEGRATION_IMPORTANCE_SAMPLING         1 // Not supported!

#define IBL_INTEGRATION                             IBL_INTEGRATION_PREFILTERED_CUBEMAP

#define IBL_INTEGRATION_IMPORTANCE_SAMPLING_COUNT   64

// Refraction defines
#define REFRACTION_TYPE_SOLID 0
#define REFRACTION_TYPE_THIN 1

#ifndef REFRACTION_TYPE
#define REFRACTION_TYPE REFRACTION_TYPE_SOLID
#endif

#define REFRACTION_MODE_CUBEMAP 0
#define REFRACTION_MODE_SCREEN 1

#ifndef REFRACTION_MODE
#define REFRACTION_MODE REFRACTION_MODE_CUBEMAP
#endif

//------------------------------------------------------------------------------
// IBL prefiltered DFG term implementations
//------------------------------------------------------------------------------

float3 PrefilteredDFG_LUT(float lod, float NoV) {
    #if defined(USE_DFG_LUT)
    // coord = sqrt(linear_roughness), which is the mapping used by cmgen.
    return UNITY_SAMPLE_TEX2D(_DFG, float2(NoV, lod));
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
        #if 0
        // Karis' approximation based on Lazarov's
        const float4 c0 = float4(-1.0, -0.0275, -0.572,  0.022);
        const float4 c1 = float4( 1.0,  0.0425,  1.040, -0.040);
        float4 r = perceptualRoughness * c0 + c1;
        float a004 = min(r.x * r.x, exp2(-9.28 * NoV)) * r.x + r.y;
        return (float3(float2(-1.04, 1.04) * a004 + r.zw, 0.0));
        #endif
        #if 0
        // Zioma's approximation based on Karis
        return float3(float2(1.0, pow(1.0 - max(perceptualRoughness, NoV), 3.0)), 0.0);
        #endif
        return float3(0, 1, 1);
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

float3 Irradiance_SphericalHarmonics(const float3 n, const bool useL2) {
    // Uses Unity's functions for reading SH. 
    // However, this function is currently unused. 
    float3 finalSH = float3(0,0,0); 
        #if (SPHERICAL_HARMONICS == SPHERICAL_HARMONICS_DEFAULT)
            finalSH = SHEvalLinearL0L1(half4(n, 1.0));
            if (useL2) finalSH += SHEvalLinearL2(half4(n, 1.0));
        #endif

        #if (SPHERICAL_HARMONICS == SPHERICAL_HARMONICS_GEOMETRICS)
            float3 L0 = float3(unity_SHAr.w, unity_SHAg.w, unity_SHAb.w)
            + float3(unity_SHBr.z, unity_SHBg.z, unity_SHBb.z) / 3.0;
            finalSH.r = shEvaluateDiffuseL1Geomerics_local(L0.r, unity_SHAr.xyz, n);
            finalSH.g = shEvaluateDiffuseL1Geomerics_local(L0.g, unity_SHAg.xyz, n);
            finalSH.b = shEvaluateDiffuseL1Geomerics_local(L0.b, unity_SHAb.xyz, n);
            // Quadratic polynomials
            if (useL2) finalSH += SHEvalLinearL2 (float4(n, 1));
        #endif

    return finalSH;
}

float3 Irradiance_SphericalHarmonics(const float3 n) {
    // Assume L2 is wanted
    return Irradiance_SphericalHarmonics(n, true);
}

#if UNITY_LIGHT_PROBE_PROXY_VOLUME
// normal should be normalized, w=1.0
half3 Irradiance_SampleProbeVolume (half4 normal, float3 worldPos)
{
    const float transformToLocal = unity_ProbeVolumeParams.y;
    const float texelSizeX = unity_ProbeVolumeParams.z;

    //The SH coefficients textures and probe occlusion are packed into 1 atlas.
    //-------------------------
    //| ShR | ShG | ShB | Occ |
    //-------------------------

    float3 position = (transformToLocal == 1.0f) ? mul(unity_ProbeVolumeWorldToObject, float4(worldPos, 1.0)).xyz : worldPos;
    float3 texCoord = (position - unity_ProbeVolumeMin.xyz) * unity_ProbeVolumeSizeInv.xyz;
    texCoord.x = texCoord.x * 0.25f;

    // We need to compute proper X coordinate to sample.
    // Clamp the coordinate otherwize we'll have leaking between RGB coefficients
    float texCoordX = clamp(texCoord.x, 0.5f * texelSizeX, 0.25f - 0.5f * texelSizeX);

    // sampler state comes from SHr (all SH textures share the same sampler)
    texCoord.x = texCoordX;
    half4 SHAr = UNITY_SAMPLE_TEX3D_SAMPLER(unity_ProbeVolumeSH, unity_ProbeVolumeSH, texCoord);

    texCoord.x = texCoordX + 0.25f;
    half4 SHAg = UNITY_SAMPLE_TEX3D_SAMPLER(unity_ProbeVolumeSH, unity_ProbeVolumeSH, texCoord);

    texCoord.x = texCoordX + 0.5f;
    half4 SHAb = UNITY_SAMPLE_TEX3D_SAMPLER(unity_ProbeVolumeSH, unity_ProbeVolumeSH, texCoord);

    // Linear + constant polynomial terms
    half3 x1;

    #if (SPHERICAL_HARMONICS == SPHERICAL_HARMONICS_DEFAULT)
        x1.r = dot(SHAr, normal);
        x1.g = dot(SHAg, normal);
        x1.b = dot(SHAb, normal);
    #endif

    #if (SPHERICAL_HARMONICS == SPHERICAL_HARMONICS_GEOMETRICS)
        x1.r = shEvaluateDiffuseL1Geomerics_local(SHAr.w, SHAr.rgb, normal);
        x1.g = shEvaluateDiffuseL1Geomerics_local(SHAg.w, SHAg.rgb, normal);
        x1.b = shEvaluateDiffuseL1Geomerics_local(SHAb.w, SHAb.rgb, normal);
    #endif

    return x1;
}
#endif

half3 Irradiance_SphericalHarmonicsUnity (half3 normal, half3 ambient, float3 worldPos)
{
    half3 ambient_contrib = 0.0;

    #if UNITY_SAMPLE_FULL_SH_PER_PIXEL
        // Completely per-pixel
        #if UNITY_LIGHT_PROBE_PROXY_VOLUME
            if (unity_ProbeVolumeParams.x == 1.0)
                ambient_contrib = Irradiance_SampleProbeVolume(half4(normal, 1.0), worldPos);
            else
                ambient_contrib = Irradiance_SphericalHarmonics(normal, true);
        #else
            ambient_contrib = Irradiance_SphericalHarmonics(normal, true);
        #endif

            ambient += max(half3(0, 0, 0), ambient_contrib);

        #ifdef UNITY_COLORSPACE_GAMMA
            ambient = LinearToGammaSpace(ambient);
        #endif
    #elif (SHADER_TARGET < 30) || UNITY_STANDARD_SIMPLE
        // Completely per-vertex
        // nothing to do here. Gamma conversion on ambient from SH takes place in the vertex shader, see ShadeSHPerVertex.
    #else
        // L2 per-vertex, L0..L1 & gamma-correction per-pixel
        // Ambient in this case is expected to be always Linear, see ShadeSHPerVertex()
        #if UNITY_LIGHT_PROBE_PROXY_VOLUME
            if (unity_ProbeVolumeParams.x == 1.0)
                ambient_contrib = Irradiance_SampleProbeVolume (half4(normal, 1.0), worldPos);
            else
                ambient_contrib = Irradiance_SphericalHarmonics(normal, false);
        #else
            ambient_contrib = Irradiance_SphericalHarmonics(normal, false);
        #endif

        ambient = max(half3(0, 0, 0), ambient+ambient_contrib);     // include L2 contribution in vertex shader before clamp.
        #ifdef UNITY_COLORSPACE_GAMMA
            ambient = LinearToGammaSpace (ambient);
        #endif
    #endif

    return ambient;
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

float4 SampleLightmapBicubic(float2 uv)
{
    #ifdef SHADER_API_D3D11
        float width, height;
        unity_Lightmap.GetDimensions(width, height);

        float4 unity_Lightmap_TexelSize = float4(width, height, 1.0/width, 1.0/height);

        return SampleTexture2DBicubicFilter(TEXTURE2D_PARAM(unity_Lightmap, samplerunity_Lightmap),
            uv, unity_Lightmap_TexelSize);
    #else
        return SAMPLE_TEXTURE2D(unity_Lightmap, samplerunity_Lightmap, uv);
    #endif
}

float4 SampleLightmapDirBicubic(float2 uv)
{
    #ifdef SHADER_API_D3D11
        float width, height;
        unity_LightmapInd.GetDimensions(width, height);

        float4 unity_LightmapInd_TexelSize = float4(width, height, 1.0/width, 1.0/height);

        return SampleTexture2DBicubicFilter(TEXTURE2D_PARAM(unity_LightmapInd, samplerunity_Lightmap),
            uv, unity_LightmapInd_TexelSize);
    #else
        return SAMPLE_TEXTURE2D(unity_LightmapInd, samplerunity_Lightmap, uv);
    #endif
}

float4 SampleDynamicLightmapBicubic(float2 uv)
{
    #ifdef SHADER_API_D3D11
        float width, height;
        unity_DynamicLightmap.GetDimensions(width, height);

        float4 unity_DynamicLightmap_TexelSize = float4(width, height, 1.0/width, 1.0/height);

        return SampleTexture2DBicubicFilter(TEXTURE2D_PARAM(unity_DynamicLightmap, samplerunity_DynamicLightmap),
            uv, unity_DynamicLightmap_TexelSize);
    #else
        return SAMPLE_TEXTURE2D(unity_DynamicLightmap, samplerunity_DynamicLightmap, uv);
    #endif
}

float4 SampleDynamicLightmapDirBicubic(float2 uv)
{
    #ifdef SHADER_API_D3D11
        float width, height;
        unity_DynamicDirectionality.GetDimensions(width, height);

        float4 unity_DynamicDirectionality_TexelSize = float4(width, height, 1.0/width, 1.0/height);

        return SampleTexture2DBicubicFilter(TEXTURE2D_PARAM(unity_DynamicDirectionality, samplerunity_DynamicLightmap),
            uv, unity_DynamicDirectionality_TexelSize);
    #else
        return SAMPLE_TEXTURE2D(unity_DynamicDirectionality, samplerunity_DynamicLightmap, uv);
    #endif
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

#if defined(USING_BAKERY) && defined(LIGHTMAP_ON)
// needs specular variant?
float3 DecodeRNMLightmap(half3 color, half2 lightmapUV, half3 normalTangent, float3x3 tangentToWorld, out Light o_light)
{
    const float rnmBasis0 = float3(0.816496580927726f, 0, 0.5773502691896258f);
    const float rnmBasis1 = float3(-0.4082482904638631f, 0.7071067811865475f, 0.5773502691896258f);
    const float rnmBasis2 = float3(-0.4082482904638631f, -0.7071067811865475f, 0.5773502691896258f);

    float3 irradiance;
    o_light = (Light)0;

    #ifdef SHADER_API_D3D11
        float width, height;
        _RNM0.GetDimensions(width, height);

        float4 rnm_TexelSize = float4(width, height, 1.0/width, 1.0/height);
        
        float3 rnm0 = DecodeLightmap(SampleTexture2DBicubicFilter(TEXTURE2D_PARAM(_RNM0, sampler_RNM0), lightmapUV, rnm_TexelSize));
        float3 rnm1 = DecodeLightmap(SampleTexture2DBicubicFilter(TEXTURE2D_PARAM(_RNM1, sampler_RNM0), lightmapUV, rnm_TexelSize));
        float3 rnm2 = DecodeLightmap(SampleTexture2DBicubicFilter(TEXTURE2D_PARAM(_RNM2, sampler_RNM0), lightmapUV, rnm_TexelSize));
    #else
        float3 rnm0 = DecodeLightmap(SAMPLE_TEXTURE2D(_RNM0, sampler_RNM0, lightmapUV));
        float3 rnm1 = DecodeLightmap(SAMPLE_TEXTURE2D(_RNM1, sampler_RNM0, lightmapUV));
        float3 rnm2 = DecodeLightmap(SAMPLE_TEXTURE2D(_RNM2, sampler_RNM0, lightmapUV));
    #endif

    normalTangent.g *= -1;

    irradiance =  saturate(dot(rnmBasis0, normalTangent)) * rnm0
                + saturate(dot(rnmBasis1, normalTangent)) * rnm1
                + saturate(dot(rnmBasis2, normalTangent)) * rnm2;

    #if defined(LIGHTMAP_SPECULAR)
    float3 dominantDirT = rnmBasis0 * luminance(rnm0) +
                          rnmBasis1 * luminance(rnm1) +
                          rnmBasis2 * luminance(rnm2);

    float3 dominantDirTN = normalize(dominantDirT);
    float3 specColor = saturate(dot(rnmBasis0, dominantDirTN)) * rnm0 +
                       saturate(dot(rnmBasis1, dominantDirTN)) * rnm1 +
                       saturate(dot(rnmBasis2, dominantDirTN)) * rnm2;                        

    o_light.l = normalize(mul(tangentToWorld, dominantDirT));
    half directionality = max(0.001, length(o_light.l));
    o_light.l /= directionality;

    // Split light into the directional and ambient parts, according to the directionality factor.
    o_light.colorIntensity = float4(specColor * directionality, 1.0);
    o_light.attenuation = directionality;
    o_light.NoL = saturate(dot(normalTangent, dominantDirTN));
    #endif

    return irradiance;
}

float3 DecodeSHLightmap(half3 L0, half2 lightmapUV, half3 normalWorld, out Light o_light)
{
    float3 irradiance;
    o_light = (Light)0;

    #ifdef SHADER_API_D3D11
        float width, height;
        _RNM0.GetDimensions(width, height);

        float4 rnm_TexelSize = float4(width, height, 1.0/width, 1.0/height);
        
        float3 nL1x = SampleTexture2DBicubicFilter(TEXTURE2D_PARAM(_RNM0, sampler_RNM0), lightmapUV, rnm_TexelSize);
        float3 nL1y = SampleTexture2DBicubicFilter(TEXTURE2D_PARAM(_RNM1, sampler_RNM0), lightmapUV, rnm_TexelSize);
        float3 nL1z = SampleTexture2DBicubicFilter(TEXTURE2D_PARAM(_RNM2, sampler_RNM0), lightmapUV, rnm_TexelSize);
    #else
        float3 nL1x = SAMPLE_TEXTURE2D(_RNM0, sampler_RNM0, lightmapUV);
        float3 nL1y = SAMPLE_TEXTURE2D(_RNM1, sampler_RNM0, lightmapUV);
        float3 nL1z = SAMPLE_TEXTURE2D(_RNM2, sampler_RNM0, lightmapUV);
    #endif

    nL1x = nL1x * 2 - 1;
    nL1y = nL1y * 2 - 1;
    nL1z = nL1z * 2 - 1;
    float3 L1x = nL1x * L0 * 2;
    float3 L1y = nL1y * L0 * 2;
    float3 L1z = nL1z * L0 * 2;

    #ifdef BAKERY_SHNONLINEAR
        float lumaL0 = dot(L0, float(1));
        float lumaL1x = dot(L1x, float(1));
        float lumaL1y = dot(L1y, float(1));
        float lumaL1z = dot(L1z, float(1));
        float lumaSH = shEvaluateDiffuseL1Geomerics_local(lumaL0, float3(lumaL1x, lumaL1y, lumaL1z), normalWorld);

        irradiance = L0 + normalWorld.x * L1x + normalWorld.y * L1y + normalWorld.z * L1z;
        float regularLumaSH = dot(irradiance, 1);
        irradiance *= lerp(1, lumaSH / regularLumaSH, saturate(regularLumaSH*16));
    #else
        irradiance = L0 + normalWorld.x * L1x + normalWorld.y * L1y + normalWorld.z * L1z;
    #endif

    #if defined(LIGHTMAP_SPECULAR)
    float3 dominantDir = float3(luminance(nL1x), luminance(nL1y), luminance(nL1z));

    o_light.l = dominantDir;
    half directionality = max(0.001, length(o_light.l));
    o_light.l /= directionality;

    // Split light into the directional and ambient parts, according to the directionality factor.
    o_light.colorIntensity = float4(irradiance * directionality, 1.0);
    o_light.attenuation = directionality;
    o_light.NoL = saturate(dot(normalWorld, o_light.l));
    #endif

    return irradiance;
}
#endif

float IrradianceToExposureOcclusion(float3 irradiance)
{
    return saturate(length(irradiance + FLT_EPS) * getExposureOcclusionBias());
}

// Return light probes or lightmap.
float3 UnityGI_Irradiance(ShadingParams shading, float3 tangentNormal, out float occlusion, out Light derivedLight)
{
    float3 irradiance = shading.ambient;
    float3 irradianceForAO;
    occlusion = 1.0;
    derivedLight = (Light)0;

    #if UNITY_SHOULD_SAMPLE_SH
        irradiance = Irradiance_SphericalHarmonicsUnity(shading.normal, shading.ambient, shading.position);
        occlusion = saturate(length(irradiance) * getExposureOcclusionBias());
    #endif

    irradianceForAO = irradiance;

    #if defined(LIGHTMAP_ON)
        // Baked lightmaps
        
        half4 bakedColorTex = SampleLightmapBicubic(shading.lightmapUV.xy);
        half3 bakedColor = DecodeLightmap(bakedColorTex);

        #ifdef DIRLIGHTMAP_COMBINED
            fixed4 bakedDirTex = SampleLightmapDirBicubic (shading.lightmapUV.xy);
            irradiance += DecodeDirectionalLightmap (bakedColor, bakedDirTex, shading.normal);

            irradianceForAO = irradiance;

            #if defined(LIGHTMAP_SHADOW_MIXING) && !defined(SHADOWS_SHADOWMASK) && defined(SHADOWS_SCREEN)
                irradiance = SubtractMainLightWithRealtimeAttenuationFromLightmap (irradiance, shading.attenuation, bakedColorTex, shading.normal);
            #endif

            #if defined(LIGHTMAP_SPECULAR) 
                irradiance = DecodeDirectionalLightmapSpecular(bakedColor, bakedDirTex, shading.normal, false, 0, derivedLight);
            #endif

        #else // not directional lightmap

            #if defined(USING_BAKERY)
                #if defined(_BAKERY_RNM)
                // bakery rnm mode
                irradiance = DecodeRNMLightmap(bakedColor, shading.lightmapUV.xy, tangentNormal, shading.tangentToWorld, derivedLight);
                #endif

                #if defined(_BAKERY_SH)
                // bakery sh mode
                irradiance = DecodeSHLightmap(bakedColor, shading.lightmapUV.xy, shading.normal, derivedLight);
                #endif

                irradianceForAO = irradiance;

                #if defined(LIGHTMAP_SHADOW_MIXING) && !defined(SHADOWS_SHADOWMASK) && defined(SHADOWS_SCREEN)
                    irradiance = SubtractMainLightWithRealtimeAttenuationFromLightmap(irradiance, shading.attenuation, bakedColorTex, shading.normal);
                #endif

            #else

                irradiance += bakedColor;

                irradianceForAO = irradiance;

                #if defined(LIGHTMAP_SHADOW_MIXING) && !defined(SHADOWS_SHADOWMASK) && defined(SHADOWS_SCREEN)
                    irradiance = SubtractMainLightWithRealtimeAttenuationFromLightmap(irradiance, shading.attenuation, bakedColorTex, shading.normal);
                #endif
            #endif

        #endif
    #endif

    #if defined(DYNAMICLIGHTMAP_ON)
        // Dynamic lightmaps
        fixed4 realtimeColorTex = SampleDynamicLightmapBicubic(shading.lightmapUV.zw);
        half3 realtimeColor = DecodeRealtimeLightmap (realtimeColorTex);

        irradianceForAO += realtimeColor;

        #ifdef DIRLIGHTMAP_COMBINED
            half4 realtimeDirTex = SampleDynamicLightmapDirBicubic(shading.lightmapUV.zw);
            irradiance += DecodeDirectionalLightmap (realtimeColor, realtimeDirTex, shading.normal);
        #else
            irradiance += realtimeColor;
        #endif
    #endif
    
    occlusion = IrradianceToExposureOcclusion(irradianceForAO);

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

    // Workaround for issue where objects are blurrier than they should be 
    // due to specular AA.
    #if !defined(TARGET_MOBILE) && defined(GEOMETRIC_SPECULAR_AA)
    float roughnessAdjustment = 1-perceptualRoughness;
    roughnessAdjustment = MIN_PERCEPTUAL_ROUGHNESS * roughnessAdjustment * roughnessAdjustment;
    perceptualRoughness = perceptualRoughness - roughnessAdjustment;
    #endif

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

inline half3 UnityGI_prefilteredRadiance(const UnityGIInput data, const float perceptualRoughness, const float3 r)
{
    half3 specular;

    Unity_GlossyEnvironmentData glossIn = (Unity_GlossyEnvironmentData)0;
    glossIn.roughness = perceptualRoughness;
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
    p.f0 = 0.04;
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
    float diffuseAO, inout float3 Fd, inout float3 Fr) {
#if !defined(SHADING_MODEL_CLOTH) && !defined(SHADING_MODEL_SUBSURFACE)
#if defined(MATERIAL_HAS_SHEEN_COLOR)
    // Albedo scaling of the base layer before we layer sheen on top
    Fd *= pixel.sheenScaling;
    Fr *= pixel.sheenScaling;

    float3 reflectance = pixel.sheenDFG * pixel.sheenColor;
    reflectance *= computeSpecularAO(shading_NoV, diffuseAO, pixel.sheenRoughness);

    Fr += reflectance * prefilteredRadiance(shading.reflected, pixel.sheenPerceptualRoughness);
#endif
#endif
}

void evaluateClearCoatIBL(const ShadingParams shading, const PixelParams pixel, 
    float diffuseAO, inout float3 Fd, inout float3 Fr) {
#if IBL_INTEGRATION == IBL_INTEGRATION_IMPORTANCE_SAMPLING
    float specularAO = computeSpecularAO(shading_NoV, diffuseAO, pixel.clearCoatRoughness);
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

    // TODO: Should we apply specularAO to the attenuation as well?
    float specularAO = computeSpecularAO(clearCoatNoV, diffuseAO, pixel.clearCoatRoughness);
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

#if defined(HAS_REFRACTION)

struct Refraction {
    float3 position;
    float3 direction;
    float d;
};

void refractionSolidSphere(const ShadingParams shading, const PixelParams pixel,
    const float3 n, float3 r, out Refraction ray) {
    r = refract(r, n, pixel.etaIR);
    float NoR = dot(n, r);
    float d = pixel.thickness * -NoR;
    ray.position = float3(shading.position + r * d);
    ray.d = d;
    float3 n1 = normalize(NoR * r - n * 0.5);
    ray.direction = refract(r, n1,  pixel.etaRI);
}

void refractionSolidBox(const ShadingParams shading, const PixelParams pixel,
    const float3 n, float3 r, out Refraction ray) {
    float3 rr = refract(r, n, pixel.etaIR);
    float NoR = dot(n, rr);
    float d = pixel.thickness / max(-NoR, 0.001);
    ray.position = float3(shading.position + rr * d);
    ray.direction = r;
    ray.d = d;
#if REFRACTION_MODE == REFRACTION_MODE_CUBEMAP
    // fudge direction vector, so we see the offset due to the thickness of the object
    float envDistance = 10.0; // this should come from a ubo
    ray.direction = normalize((ray.position - shading.position) + ray.direction * envDistance);
#endif
}

void refractionThinSphere(const ShadingParams shading, const PixelParams pixel,
    const float3 n, float3 r, out Refraction ray) {
    float d = 0.0;
#if defined(MATERIAL_HAS_MICRO_THICKNESS)
    // note: we need the refracted ray to calculate the distance traveled
    // we could use shading.NoV, but we would lose the dependency on ior.
    float3 rr = refract(r, n, pixel.etaIR);
    float NoR = dot(n, rr);
    d = pixel.uThickness / max(-NoR, 0.001);
    ray.position = float3(shading.position + rr * d);
#else
    ray.position = float3(shading.position);
#endif
    ray.direction = r;
    ray.d = d;
}

void applyRefraction(
    const ShadingParams shading, 
    const PixelParams pixel,
    float3 E, float3 Fd, float3 Fr,
    inout float3 color) {

    Refraction ray;
    float iblLuminance = 1.0; // unused
    float refractionLodOffset = 0.0; // unused

#if REFRACTION_TYPE == REFRACTION_TYPE_SOLID
    refractionSolidSphere(shading, pixel, shading.normal, -shading.view, ray);
#elif REFRACTION_TYPE == REFRACTION_TYPE_THIN
    refractionThinSphere(shading, pixel, shading.normal, -shading.view, ray);
#else
#error invalid REFRACTION_TYPE
#endif

    // compute transmission T
#if defined(MATERIAL_HAS_ABSORPTION)
#if defined(MATERIAL_HAS_THICKNESS) || defined(MATERIAL_HAS_MICRO_THICKNESS)
    float3 T = min(1.0, exp(-pixel.absorption * ray.d));
#else
    float3 T = 1.0 - pixel.absorption;
#endif
#endif

    // Roughness remapping so that an IOR of 1.0 means no microfacet refraction and an IOR
    // of 1.5 has full microfacet refraction
    float perceptualRoughness = lerp(pixel.perceptualRoughnessUnclamped, 0.0,
            saturate(pixel.etaIR * 3.0 - 2.0));
#if REFRACTION_TYPE == REFRACTION_TYPE_THIN
    // For thin surfaces, the light will bounce off at the second interface in the direction of
    // the reflection, effectively adding to the specular, but this process will repeat itself.
    // Each time the ray exits the surface on the front side after the first bounce,
    // it's multiplied by E^2, and we get: E + E(1-E)^2 + E^3(1-E)^2 + ...
    // This infinite series converges and is easy to simplify.
    // Note: we calculate these bounces only on a single component,
    // since it's a fairly subtle effect.
    E *= 1.0 + pixel.transmission * (1.0 - E.g) / (1.0 + E.g);
#endif

    /* sample the cubemap or screen-space */
#if REFRACTION_MODE == REFRACTION_MODE_CUBEMAP
    // when reading from the cubemap, we are not pre-exposed so we apply iblLuminance
    // which is not the case when we'll read from the screen-space buffer

    // Gather Unity GI data
    UnityGIInput unityData = InitialiseUnityGIInput(shading, pixel);
    float3 Ft = UnityGI_prefilteredRadiance(unityData, perceptualRoughness, ray.direction) * iblLuminance;
#else
    // compute the point where the ray exits the medium, if needed
    //float4 p = float4(frameUniforms.clipFromWorldMatrix * float4(ray.position, 1.0));
    //p.xy = uvToRenderTargetUV(p.xy * (0.5 / p.w) + 0.5);
    float4 p = UnityWorldToClipPos(float4(ray.position, 1.0));
    p.w =  (0.5 / p.w);
    p.xy = ComputeGrabScreenPos(p);

    // perceptualRoughness to LOD
    // Empirical factor to compensate for the gaussian approximation of Dggx, chosen so
    // cubemap and screen-space modes match at perceptualRoughness 0.125
    // TODO: Remove this factor temporarily until we find a better solution
    //       This overblurs many scenes and needs a more principled approach
    // float tweakedPerceptualRoughness = perceptualRoughness * 1.74;
    float tweakedPerceptualRoughness = perceptualRoughness;
    float lod = max(0.0, 2.0 * log2(tweakedPerceptualRoughness) + refractionLodOffset);

    float3 Ft = UNITY_SAMPLE_TEX2D_LOD(REFRACTION_SOURCE, p.xy, lod).rgb * REFRACTION_MULTIPLIER;
#endif

    // base color changes the amount of light passing through the boundary
    Ft *= pixel.diffuseColor;

    // fresnel from the first interface
    Ft *= 1.0 - E;

    // apply absorption
#if defined(MATERIAL_HAS_ABSORPTION)
    Ft *= T;
#endif

    Fr *= iblLuminance;
    Fd *= iblLuminance;
    color.rgb += Fr + lerp(Fd, Ft, pixel.transmission);
}
#endif

void combineDiffuseAndSpecular(const ShadingParams shading, const PixelParams pixel,
        const float3 E, const float3 Fd, const float3 Fr,
        inout float3 color) {
    const float iblLuminance = 1.0; // Unknown
#if defined(HAS_REFRACTION)
    applyRefraction(shading, pixel, E, Fd, Fr, color);
#else
    color.rgb += (Fd + Fr) * iblLuminance;
#endif
}

void evaluateIBL(const ShadingParams shading, const MaterialInputs material, const PixelParams pixel, 
    inout float3 color) {
    float ssao = 1.0; // Not implemented
    float lightmapAO = 1.0; // 
    float3 tangentNormal = float3(0, 0, 1);
    Light derivedLight = (Light)0;

    // Gather Unity GI data
    UnityGIInput unityData = InitialiseUnityGIInput(shading, pixel);
#if defined(MATERIAL_HAS_NORMAL)
    tangentNormal = material.normal;
#endif
    float3 unityIrradiance = UnityGI_Irradiance(shading, tangentNormal, lightmapAO, derivedLight);

    float diffuseAO = min(material.ambientOcclusion, ssao);
    float specularAO = computeSpecularAO(shading.NoV, diffuseAO*lightmapAO, pixel.roughness);

    // specular layer
    float3 Fr;
#if IBL_INTEGRATION == IBL_INTEGRATION_PREFILTERED_CUBEMAP
    float3 E = specularDFG(pixel);
    float3 r = getReflectedVector(shading, pixel, shading.normal);
    Fr = E * UnityGI_prefilteredRadiance(unityData, pixel.perceptualRoughness, r);
#elif IBL_INTEGRATION == IBL_INTEGRATION_IMPORTANCE_SAMPLING
    // Not supported
    float3 E = float3(0.0); // TODO: fix for importance sampling
    Fr = isEvaluateSpecularIBL(pixel, shading.normal, shading.view, shading.NoV);
#endif

    // Gather LTCGI data, if present.
#if defined(_LTCGI)
    float3 ltcDiffuse = 0;
    float3 ltcSpecular = 0;
    float ltcSpecularIntensity = 0;

    LTCGI_Contribution(
        shading.position, 
        shading.normal, 
        shading.view, 
        pixel.perceptualRoughness, 
        (shading.lightmapUV.xy - unity_LightmapST.zw) / unity_LightmapST.xy,
        /* out */ ltcDiffuse,
        /* out */ ltcSpecular,
        /* out */ ltcSpecularIntensity
    );

#endif

#if defined(_LTCGI)
    Fr = lerp(Fr, E * ltcSpecular, saturate(ltcSpecularIntensity));
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

#if defined(_LTCGI)
    diffuseIrradiance += ltcDiffuse;
#endif

    float3 Fd = pixel.diffuseColor * diffuseIrradiance * (1.0 - E) * diffuseBRDF;

    // subsurface layer
    evaluateSubsurfaceIBL(shading, pixel, diffuseIrradiance, Fd, Fr);

    // extra ambient occlusion term for the base and subsurface layers
    multiBounceAO(diffuseAO, pixel.diffuseColor, Fd);
    multiBounceSpecularAO(specularAO, pixel.f0, Fr);

    // sheen layer
    evaluateSheenIBL(shading, pixel, diffuseAO, Fd, Fr);

    // clear coat layer
    evaluateClearCoatIBL(shading, pixel, diffuseAO, Fd, Fr);
    
    // Note: iblLuminance is already premultiplied by the exposure
    combineDiffuseAndSpecular(shading, pixel, E, Fd, Fr, color);

    #if defined(LIGHTMAP_SPECULAR)
    PixelParams pixelForBakedSpecular = pixel;

    // remap roughness to clamp at max roughness without a hard clamp
    pixelForBakedSpecular.roughness = remap_almostIdentity(pixelForBakedSpecular.roughness,
        1-getLightmapSpecularMaxSmoothness(), 1-getLightmapSpecularMaxSmoothness()+MIN_ROUGHNESS);
    
    if (derivedLight.NoL >= 0.0) color += surfaceShading(shading, pixelForBakedSpecular, derivedLight, 
        computeMicroShadowing(derivedLight.NoL, material.ambientOcclusion * 0.8 + 0.3));
    #endif
}

#endif // FILAMENT_LIGHT_INDIRECT