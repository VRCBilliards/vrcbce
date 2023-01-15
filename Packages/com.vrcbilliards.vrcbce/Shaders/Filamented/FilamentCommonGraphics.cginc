#ifndef FILAMENT_COMMON_GRAPHICS
#define FILAMENT_COMMON_GRAPHICS
//------------------------------------------------------------------------------
// Common color operations
//------------------------------------------------------------------------------

/**
 * Computes the luminance of the specified linear RGB color using the
 * luminance coefficients from Rec. 709.
 *
 * @public-api
 */
float luminance(const float3 linearCol) {
    return dot(linearCol, float3(0.2126, 0.7152, 0.0722));
}

/**
 * Computes the pre-exposed intensity using the specified intensity and exposure.
 * This function exists to force high precision on the two parameters
 * ...Which isn't applicable yet.
 */
float computePreExposedIntensity(const float intensity, const float exposure) {
    return intensity * exposure;
}

void unpremultiply(inout float4 color) {
    color.rgb /= max(color.a, FLT_EPS);
}

/**
 * Applies a full range YCbCr to sRGB conversion and returns an RGB color.
 *
 * @public-api
 */
float3 ycbcrToRgb(float luminance, float2 cbcr) {
    // Taken from https://developer.apple.com/documentation/arkit/arframe/2867984-capturedimage
    const float4x4 ycbcrToRgbTransform = {
         1.0000,  1.0000,  1.0000,  0.0000,
         0.0000, -0.3441,  1.7720,  0.0000,
         1.4020, -0.7141,  0.0000,  0.0000,
        -0.7010,  0.5291, -0.8860,  1.0000
    };
    return mul(ycbcrToRgbTransform, float4(luminance, cbcr, 1.0)).rgb;
}

//------------------------------------------------------------------------------
// Tone mapping operations
//------------------------------------------------------------------------------

/*
 * The input must be in the [0, 1] range.
 */
float3 Inverse_Tonemap_Unreal(const float3 x) {
    return (x * -0.155) / (x - 1.019);
}

/**
 * Applies the inverse of the tone mapping operator to the specified HDR or LDR
 * sRGB (non-linear) color and returns a linear sRGB color. The inverse tone mapping
 * operator may be an approximation of the real inverse operation.
 *
 * @public-api
 */
float3 inverseTonemapSRGB(float3 color) {
    // sRGB input
    color = clamp(color, 0.0, 1.0);
    return Inverse_Tonemap_Unreal(color);
}

/**
 * Applies the inverse of the tone mapping operator to the specified HDR or LDR
 * linear RGB color and returns a linear RGB color. The inverse tone mapping operator
 * may be an approximation of the real inverse operation.
 *
 * @public-api
 */
float3 inverseTonemap(float3 linearCol) {
    // Linear input
    linearCol = clamp(linearCol, 0.0, 1.0);
    return Inverse_Tonemap_Unreal(pow(linearCol, 1.0 / 2.2));
}

//------------------------------------------------------------------------------
// Common texture operations
//------------------------------------------------------------------------------

/**
 * Decodes the specified RGBM value to linear HDR RGB.
 */
float3 decodeRGBM(float4 c) {
    c.rgb *= (c.a * 16.0);
    return c.rgb * c.rgb;
}

//------------------------------------------------------------------------------
// Common debug
//------------------------------------------------------------------------------

float3 heatmap(float v) {
    float3 r = v * 2.1 - float3(1.8, 1.14, 0.3);
    return 1.0 - r * r;
}

float3 uintToColorDebug(uint v) {
    if (v == 0u) {
        return float3(0.0, 1.0, 0.0);     // green
    } else if (v == 1u) {
        return float3(0.0, 0.0, 1.0);     // blue
    } else if (v == 2u) {
        return float3(1.0, 1.0, 0.0);     // yellow
    } else if (v == 3u) {
        return float3(1.0, 0.0, 0.0);     // red
    } else if (v == 4u) {
        return float3(1.0, 0.0, 1.0);     // purple
    } else if (v == 5u) {
        return float3(0.0, 1.0, 1.0);     // cyan
    }
}
#endif // FILAMENT_COMMON_GRAPHICS