// Unity built-in shader source. Copyright (c) 2016 Unity Technologies. MIT license (see license.txt)

#ifndef UNITY_STANDARD_INPUT_INCLUDED
#define UNITY_STANDARD_INPUT_INCLUDED

#include "UnityCG.cginc"
#include "UnityStandardConfig.cginc"
#include "UnityStandardUtils.cginc"

//------------------------------------------------------------------------------
// Input configuration
//------------------------------------------------------------------------------

// Parallax settings
#define PARALLAX_NONE                 0
#define PARALLAX_ONESTEP              1
#define PARALLAX_RAYMARCH             2

#define PARALLAX_OPERATOR             PARALLAX_RAYMARCH

//---------------------------------------
// Directional lightmaps & Parallax require tangent space too
#if (_NORMALMAP || DIRLIGHTMAP_COMBINED || _PARALLAXMAP || MATERIAL_NEEDS_TBN)
    #define _TANGENT_TO_WORLD 1
#endif

#if (_DETAIL_MULX2 || _DETAIL_MUL || _DETAIL_ADD || _DETAIL_LERP)
    #define _DETAIL 1
#endif

//---------------------------------------
half4       _Color;
half        _Cutoff;

sampler2D   _MainTex;
float4      _MainTex_ST;

sampler2D   _DetailAlbedoMap;
float4      _DetailAlbedoMap_ST;

sampler2D   _BumpMap;
half        _BumpScale;

sampler2D   _DetailMask;
sampler2D   _DetailNormalMap;
half        _DetailNormalMapScale;

sampler2D   _SpecGlossMap;
sampler2D   _MetallicGlossMap;
half        _Metallic;
float       _Glossiness;
float       _GlossMapScale;

sampler2D   _OcclusionMap;
half        _OcclusionStrength;

sampler2D   _ParallaxMap;
half        _Parallax;
half        _UVSec;

half4       _EmissionColor;
sampler2D   _EmissionMap;

// New settings
half       _ExposureOcclusion;

//-------------------------------------------------------------------------------------
// Input functions

struct VertexInput
{
    float4 vertex   : POSITION;
    half3 normal    : NORMAL;
    float2 uv0      : TEXCOORD0;
    float2 uv1      : TEXCOORD1;
#if defined(DYNAMICLIGHTMAP_ON) || defined(UNITY_PASS_META)
    float2 uv2      : TEXCOORD2;
#endif
#ifdef _TANGENT_TO_WORLD
    half4 tangent   : TANGENT;
#endif
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

float4 TexCoords(VertexInput v)
{
    float4 texcoord;
    texcoord.xy = TRANSFORM_TEX(v.uv0, _MainTex); // Always source from uv0
    texcoord.zw = TRANSFORM_TEX(((_UVSec == 0) ? v.uv0 : v.uv1), _DetailAlbedoMap);
    return texcoord;
}

half DetailMask(float2 uv)
{
    return tex2D (_DetailMask, uv).a;
}

half3 Albedo(float4 texcoords)
{
    half3 albedo = _Color.rgb * tex2D (_MainTex, texcoords.xy).rgb;
#if _DETAIL
    #if (SHADER_TARGET < 30)
        // SM20: instruction count limitation
        // SM20: no detail mask
        half mask = 1;
    #else
        half mask = DetailMask(texcoords.xy);
    #endif
    half3 detailAlbedo = tex2D (_DetailAlbedoMap, texcoords.zw).rgb;
    #if _DETAIL_MULX2
        albedo *= LerpWhiteTo (detailAlbedo * unity_ColorSpaceDouble.rgb, mask);
    #elif _DETAIL_MUL
        albedo *= LerpWhiteTo (detailAlbedo, mask);
    #elif _DETAIL_ADD
        albedo += detailAlbedo * mask;
    #elif _DETAIL_LERP
        albedo = lerp (albedo, detailAlbedo, mask);
    #endif
#endif
    return albedo;
}

half Alpha(float2 uv)
{
#if defined(_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A)
    return _Color.a;
#else
    return tex2D(_MainTex, uv).a * _Color.a;
#endif
}

half Occlusion(float2 uv)
{
#if (SHADER_TARGET < 30)
    // SM20: instruction count limitation
    // SM20: simpler occlusion
    return tex2D(_OcclusionMap, uv).g;
#else
    half occ = tex2D(_OcclusionMap, uv).g;
    return LerpOneTo (occ, _OcclusionStrength);
#endif
}

half4 SpecularGloss(float2 uv)
{
    half4 sg;
#ifdef _SPECGLOSSMAP
    #if defined(_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A)
        sg.rgb = tex2D(_SpecGlossMap, uv).rgb;
        sg.a = tex2D(_MainTex, uv).a;
    #else
        sg = tex2D(_SpecGlossMap, uv);
    #endif
    sg.a *= _GlossMapScale;
#else
    sg.rgb = _SpecColor.rgb;
    #ifdef _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
        sg.a = tex2D(_MainTex, uv).a * _GlossMapScale;
    #else
        sg.a = _Glossiness;
    #endif
#endif
    return sg;
}

half2 MetallicGloss(float2 uv)
{
    half2 mg;

#ifdef _METALLICGLOSSMAP
    #ifdef _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
        mg.r = tex2D(_MetallicGlossMap, uv).r;
        mg.g = tex2D(_MainTex, uv).a;
    #else
        mg = tex2D(_MetallicGlossMap, uv).ra;
    #endif
    mg.g *= _GlossMapScale;
#else
    mg.r = _Metallic;
    #ifdef _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
        mg.g = tex2D(_MainTex, uv).a * _GlossMapScale;
    #else
        mg.g = _Glossiness;
    #endif
#endif
    return mg;
}

half2 MetallicRough(float2 uv)
{
    half2 mg;
#ifdef _METALLICGLOSSMAP
    mg.r = tex2D(_MetallicGlossMap, uv).r;
#else
    mg.r = _Metallic;
#endif

#ifdef _SPECGLOSSMAP
    mg.g = 1.0f - tex2D(_SpecGlossMap, uv).r;
#else
    mg.g = 1.0f - _Glossiness;
#endif
    return mg;
}

half3 Emission(float2 uv)
{
#ifndef _EMISSION
    return 0;
#else
    return tex2D(_EmissionMap, uv).rgb * _EmissionColor.rgb;
#endif
}

#ifdef _NORMALMAP
half3 NormalInTangentSpace(float4 texcoords)
{
    half3 normalTangent = UnpackScaleNormal(tex2D (_BumpMap, texcoords.xy), _BumpScale);

#if _DETAIL && defined(UNITY_ENABLE_DETAIL_NORMALMAP)
    half mask = DetailMask(texcoords.xy);
    half3 detailNormalTangent = UnpackScaleNormal(tex2D (_DetailNormalMap, texcoords.zw), _DetailNormalMapScale);
    #if _DETAIL_LERP
        normalTangent = lerp(
            normalTangent,
            detailNormalTangent,
            mask);
    #else
        normalTangent = lerp(
            normalTangent,
            BlendNormals(normalTangent, detailNormalTangent),
            mask);
    #endif
#endif

    return normalTangent;
}
#endif

struct PerPixelHeightDisplacementParam
{
    float2 uv;
    float2 dX;
    float2 dY;
};

PerPixelHeightDisplacementParam InitPerPixelHeightDisplacementParam(float2 uv)
{
    PerPixelHeightDisplacementParam ppd;
 
    ppd.uv = uv;
    ppd.dX = ddx(uv);
    ppd.dY = ddy(uv);

    return ppd;
}

float ComputePerPixelHeightDisplacement(float2 offset, float lod, PerPixelHeightDisplacementParam ppdParam)
{
    float height = 1;
    float strength = _Parallax;
    // Probably can use LOD to skip reading if too far
    height = 
        tex2Dgrad(_ParallaxMap, ppdParam.uv + offset, ppdParam.dX, ppdParam.dY).g;

    height = clamp(height, 0, 0.9999);

    return height;
}

#include "SharedParallaxLib.hlsl"

float4 Parallax (float4 texcoords, half3 viewDir)
{
#if !defined(_PARALLAXMAP) || (SHADER_TARGET < 30) || (PARALLAX_OPERATOR == PARALLAX_NONE)
    // Disable parallax on pre-SM3.0 shader target models
    return texcoords;
#else
#if (PARALLAX_OPERATOR == PARALLAX_ONESTEP)
    half h = tex2D (_ParallaxMap, texcoords.xy).g;
    float2 offset = ParallaxOffset1Step (h, _Parallax, viewDir);
    return float4(texcoords.xy + offset, texcoords.zw + offset);
#endif
#if (PARALLAX_OPERATOR == PARALLAX_RAYMARCH)
    PerPixelHeightDisplacementParam ppd = InitPerPixelHeightDisplacementParam(texcoords.xy);
    float height = 1.0;
    viewDir = normalize(viewDir);
    viewDir.xy /= (viewDir.z + 0.42); 
    float2 offset = ParallaxRaymarching(viewDir, ppd, _Parallax, /* out */ height);
    return float4(texcoords.xy + offset, texcoords.zw + offset);
#endif
#endif
    return texcoords;
}

#if (defined(_NORMALMAP) && defined(NORMALMAP_SHADOW))
struct NormalMapShadowsParam
{
    float4 uv;
    float2 dX;
    float2 dY;
};

NormalMapShadowsParam InitNormalMapShadowsParam(float4 uv)
{
    NormalMapShadowsParam nms;
 
    nms.uv = uv;
    nms.dX = ddx(uv.xy);
    nms.dY = ddy(uv.xy);

    return nms;
}

float3 SampleNormalMap (NormalMapShadowsParam nmsParam, float2 offset) {
    return NormalInTangentSpace(float4(nmsParam.uv.xy + offset, nmsParam.uv.zw + offset));
}

#include "SharedNormalShadowLib.hlsl"

float NormalTangentShadow(float4 texcoords, half3 lightDirTS, float noise)
{
    float _HeightScale = 0.2;
    float _ShadowHardness = 50.0;
    NormalMapShadowsParam nms = InitNormalMapShadowsParam(texcoords);
    nms.uv = texcoords;
    return NormalMapShadows (lightDirTS, nms, noise, _HeightScale, _ShadowHardness);
}

#endif

half getMaskThreshold()
{
    return _Cutoff;
}

half getExposureOcclusionBias()
{
    return 1.0/(_ExposureOcclusion);
}

#endif // UNITY_STANDARD_INPUT_INCLUDED
