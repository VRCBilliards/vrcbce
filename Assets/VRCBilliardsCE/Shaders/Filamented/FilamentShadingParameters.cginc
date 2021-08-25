//------------------------------------------------------------------------------
// Material evaluation
//------------------------------------------------------------------------------

/**
 * Computes global shading parameters used to apply lighting, such as the view
 * vector in world space, the tangent frame at the shading point, etc.
 */

void computeShadingParams(inout ShadingParams shading, float3 vertex_worldNormal, float4 vertex_worldTangent, float3 vertex_worldPosition,
    float4 vertex_position, float3 frameUniforms_cameraPosition, bool isDoubleSided, bool gl_FrontFacing) {
#if defined(HAS_ATTRIBUTE_TANGENTS)
    float3 n = vertex_worldNormal;
#if defined(MATERIAL_NEEDS_TBN)
    float3 t = vertex_worldTangent.xyz;
    float3 b = cross(n, t) * sign(vertex_worldTangent.w);
#endif

#if defined(MATERIAL_HAS_DOUBLE_SIDED_CAPABILITY)
    if (isDoubleSided) {
        n = gl_FrontFacing ? n : -n;
#if defined(MATERIAL_NEEDS_TBN)
        t = gl_FrontFacing ? t : -t;
        b = gl_FrontFacing ? b : -b;
#endif
    }
#endif

    shading.geometricNormal = normalize(n);

#if defined(MATERIAL_NEEDS_TBN)
    // We use unnormalized post-interpolation values, assuming mikktspace tangents
    shading.tangentToWorld = float3x3(t, b, n);
#endif
#endif

    shading.position = vertex_worldPosition;
    shading.view = normalize(frameUniforms_cameraPosition - shading.position);

    // we do this so we avoid doing (matrix multiply), but we burn 4 varyings:
    //    p = clipFromWorldMatrix * shading.position;
    //    shading.normalizedViewportCoord = p.xy * 0.5 / p.w + 0.5
    shading.normalizedViewportCoord = vertex_position.xy * (0.5 / vertex_position.w) + 0.5;
}


/**
 * Computes global shading parameters that the material might need to access
 * before lighting: N dot V, the reflected vector and the shading normal (before
 * applying the normal map). These parameters can be useful to material authors
 * to compute other material properties.
 *
 * This function must be invoked by the user's material code (guaranteed by
 * the material compiler) after setting a value for MaterialInputs.normal.
 */
void prepareMaterial(inout ShadingParams shading, const MaterialInputs material) {
#if defined(HAS_ATTRIBUTE_TANGENTS)
#if defined(MATERIAL_HAS_NORMAL)
    shading.normal = normalize(mul(shading.tangentToWorld, material.normal));
#else
    shading.normal = shading.geometricNormal;
#endif // MATERIAL_HAS_NORMAL
    shading.NoV = clampNoV(dot(shading.normal, shading.view));
    shading.reflected = reflect(-shading.view, shading.normal);

#if defined(MATERIAL_HAS_BENT_NORMAL)
    shading.bentNormal = normalize(mul(shading.tangentToWorld, material.bentNormal));
#endif // MATERIAL_HAS_BENT_NORMAL

#if defined(MATERIAL_HAS_CLEAR_COAT)
#if defined(MATERIAL_HAS_CLEAR_COAT_NORMAL)
    shading.clearCoatNormal = normalize(mul(shading.tangentToWorld, material.clearCoatNormal));
#else
    shading.clearCoatNormal = shading.geometricNormal;
#endif // MATERIAL_HAS_CLEAR_COAT_NORMAL
#endif // MATERIAL_HAS_CLEAR_COAT
#endif // HAS_ATTRIBUTE_TANGENTS
}
