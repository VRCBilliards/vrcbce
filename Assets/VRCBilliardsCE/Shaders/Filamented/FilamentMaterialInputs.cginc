#ifndef FILAMENT_MATERIAL_INPUTS
#define FILAMENT_MATERIAL_INPUTS

// Decide if we can skip lighting when dot(n, l) <= 0.0
#if defined(SHADING_MODEL_CLOTH)
#if !defined(MATERIAL_HAS_SUBSURFACE_COLOR)
    #define MATERIAL_CAN_SKIP_LIGHTING
#endif
#elif defined(SHADING_MODEL_SUBSURFACE)
    // Cannot skip lighting
#else
    #define MATERIAL_CAN_SKIP_LIGHTING
#endif

struct MaterialInputs {
    float4  baseColor;
#if !defined(SHADING_MODEL_UNLIT)
#if !defined(SHADING_MODEL_SPECULAR_GLOSSINESS)
    float roughness;
#endif
#if !defined(SHADING_MODEL_CLOTH) && !defined(SHADING_MODEL_SPECULAR_GLOSSINESS)
    float metallic;
    float reflectance;
#endif
    float ambientOcclusion;
#endif
    float4  emissive;

#if !defined(SHADING_MODEL_CLOTH) && !defined(SHADING_MODEL_SUBSURFACE) && !defined(SHADING_MODEL_UNLIT)
    float3 sheenColor;
    float sheenRoughness;
#endif

    float clearCoat;
    float clearCoatRoughness;

    float anisotropy;
    float3  anisotropyDirection;

#if defined(SHADING_MODEL_SUBSURFACE) || defined(HAS_REFRACTION)
    float thickness;
#endif
#if defined(SHADING_MODEL_SUBSURFACE)
    float subsurfacePower;
    float3  subsurfaceColor;
#endif

#if defined(SHADING_MODEL_CLOTH)
    float3  sheenColor;
#if defined(MATERIAL_HAS_SUBSURFACE_COLOR)
    float3  subsurfaceColor;
#endif
#endif

#if defined(SHADING_MODEL_SPECULAR_GLOSSINESS)
    float3  specularColor;
    float glossiness;
#endif

#if defined(MATERIAL_HAS_NORMAL)
    float3  normal;
#endif
#if defined(MATERIAL_HAS_BENT_NORMAL)
    float3  bentNormal;
#endif
#if defined(MATERIAL_HAS_CLEAR_COAT) && defined(MATERIAL_HAS_CLEAR_COAT_NORMAL)
    float3  clearCoatNormal;
#endif

#if defined(MATERIAL_HAS_POST_LIGHTING_COLOR)
    float4  postLightingColor;
#endif

#if defined(HAS_REFRACTION)
#if defined(MATERIAL_HAS_ABSORPTION)
    float3 absorption;
#endif
#if defined(MATERIAL_HAS_TRANSMISSION)
    float transmission;
#endif
#if defined(MATERIAL_HAS_IOR)
    float ior;
#endif
#if defined(MATERIAL_HAS_MICRO_THICKNESS) && (REFRACTION_TYPE == REFRACTION_TYPE_THIN)
    float microThickness;
#endif
#endif
};

void initMaterial(out MaterialInputs material) {
    material = (MaterialInputs)0;

    material.baseColor = 1.0;
#if !defined(SHADING_MODEL_UNLIT)
#if !defined(SHADING_MODEL_SPECULAR_GLOSSINESS)
    material.roughness = 1.0;
#endif
#if !defined(SHADING_MODEL_CLOTH) && !defined(SHADING_MODEL_SPECULAR_GLOSSINESS)
    material.metallic = 0.0;
    material.reflectance = 0.5;
#endif
    material.ambientOcclusion = 1.0;
#endif
    material.emissive = float4(float3(0.0.xxx), 1.0);

#if !defined(SHADING_MODEL_CLOTH) && !defined(SHADING_MODEL_SUBSURFACE) && !defined(SHADING_MODEL_UNLIT)
#if defined(MATERIAL_HAS_SHEEN_COLOR)
    material.sheenColor = float3(0.0.xxx);
    material.sheenRoughness = 0.0;
#endif
#endif

#if defined(MATERIAL_HAS_CLEAR_COAT)
    material.clearCoat = 1.0;
    material.clearCoatRoughness = 0.0;
#endif

#if defined(MATERIAL_HAS_ANISOTROPY)
    material.anisotropy = 0.0;
    material.anisotropyDirection = float3(1.0, 0.0, 0.0);
#endif

#if defined(SHADING_MODEL_SUBSURFACE) || defined(HAS_REFRACTION)
    material.thickness = 0.5;
#endif
#if defined(SHADING_MODEL_SUBSURFACE)
    material.subsurfacePower = 12.234;
    material.subsurfaceColor = float3(1.0.xxx);
#endif

#if defined(SHADING_MODEL_CLOTH)
    material.sheenColor = sqrt(material.baseColor.rgb);
#if defined(MATERIAL_HAS_SUBSURFACE_COLOR)
    material.subsurfaceColor = float3(0.0.xxx);
#endif
#endif

#if defined(SHADING_MODEL_SPECULAR_GLOSSINESS)
    material.glossiness = 0.0;
    material.specularColor = float3(0.0.xxx);
#endif

#if defined(MATERIAL_HAS_NORMAL)
    material.normal = float3(0.0, 0.0, 1.0);
#endif
#if defined(MATERIAL_HAS_BENT_NORMAL)
    material.bentNormal = float3(0.0, 0.0, 1.0);
#endif
#if defined(MATERIAL_HAS_CLEAR_COAT) && defined(MATERIAL_HAS_CLEAR_COAT_NORMAL)
    material.clearCoatNormal = float3(0.0, 0.0, 1.0);
#endif

#if defined(MATERIAL_HAS_POST_LIGHTING_COLOR)
    material.postLightingColor = float4(0.0.xxx);
#endif

#if defined(HAS_REFRACTION)
#if defined(MATERIAL_HAS_ABSORPTION)
    material.absorption = float3(0.0.xxx);
#endif
#if defined(MATERIAL_HAS_TRANSMISSION)
    material.transmission = 1.0;
#endif
#if defined(MATERIAL_HAS_IOR)
    material.ior = 1.5;
#endif
#if defined(MATERIAL_HAS_MICRO_THICKNESS) && (REFRACTION_TYPE == REFRACTION_TYPE_THIN)
    material.microThickness = 0.0;
#endif
#endif
}

#endif // FILAMENT_MATERIAL_INPUTS