// Unity built-in shader source. Copyright (c) 2016 Unity Technologies. MIT license (see license.txt)

#ifndef UNITY_STANDARD_META_INCLUDED
#define UNITY_STANDARD_META_INCLUDED

// Functionality for Standard shader "meta" pass
// (extracts albedo/emission for lightmapper etc.)

#include "UnityCG.cginc"
#include "UnityStandardInput.cginc"
#include "UnityMetaPass.cginc"
#include "UnityStandardCore.cginc"
#include "FilamentShadingLit.cginc"

struct v2f_meta
{
    float4 pos      : SV_POSITION;
    float4 uv       : TEXCOORD0;
#ifdef EDITOR_VISUALIZATION
    float2 vizUV        : TEXCOORD1;
    float4 lightCoord   : TEXCOORD2;
#endif
#ifdef HAS_ATTRIBUTE_COLOR
    float4 color        : COLOR_Centroid;
#endif
};

v2f_meta vert_meta (VertexInput v)
{
    v2f_meta o;
    o.pos = UnityMetaVertexPosition(v.vertex, v.uv1.xy, v.uv2.xy, unity_LightmapST, unity_DynamicLightmapST);
    o.uv = TexCoords(v);
#ifdef EDITOR_VISUALIZATION
    o.vizUV = 0;
    o.lightCoord = 0;
    if (unity_VisualizationMode == EDITORVIZ_TEXTURE)
        o.vizUV = UnityMetaVizUV(unity_EditorViz_UVIndex, v.uv0.xy, v.uv1.xy, v.uv2.xy, unity_EditorViz_Texture_ST);
    else if (unity_VisualizationMode == EDITORVIZ_SHOWLIGHTMASK)
    {
        o.vizUV = v.uv1.xy * unity_LightmapST.xy + unity_LightmapST.zw;
        o.lightCoord = mul(unity_EditorViz_WorldToLight, mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1)));
    }
#endif
#ifdef HAS_ATTRIBUTE_COLOR
    o.color = v.color;
#endif
    return o;
}

// Albedo for lightmapping should basically be diffuse color.
// But rough metals (black diffuse) still scatter quite a lot of light around, so
// we want to take some of that into account too.
half3 UnityLightmappingAlbedo (half3 diffuse, half3 specular, half smoothness)
{
    half roughness = SmoothnessToRoughness(smoothness);
    half3 res = diffuse;
    res += specular * roughness * 0.5;
    return res;
}

float4 frag_meta (v2f_meta i) : SV_Target
{
    // we're interested in diffuse & specular colors,
    // and surface roughness to produce final albedo.
    MaterialInputs material = SETUP_BRDF_INPUT (i.uv);
    
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

#endif // UNITY_STANDARD_META_INCLUDED
