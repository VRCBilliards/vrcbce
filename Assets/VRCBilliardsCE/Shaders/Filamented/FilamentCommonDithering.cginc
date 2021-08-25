//------------------------------------------------------------------------------
// Dithering configuration
//------------------------------------------------------------------------------

// Dithering operators
#define DITHERING_NONE                 0
#define DITHERING_INTERLEAVED_NOISE    1
#define DITHERING_VLACHOS              2
#define DITHERING_TRIANGLE_NOISE       3
#define DITHERING_TRIANGLE_NOISE_RGB   4

#define DITHERING_OPERATOR      DITHERING_TRIANGLE_NOISE

//------------------------------------------------------------------------------
// Noise
//------------------------------------------------------------------------------

// n must be normalized in [0..1] (e.g. texture coordinates)
float triangleNoise(float2 n) {
    // triangle noise, in [-1.0..1.0[ range
    n  = fract(n * float2(5.3987, 5.4421));
    n += dot(n.yx, n.xy + float2(21.5351, 14.3137));

    float xy = n.x * n.y;
    // compute in [0..2[ and remap to [-1.0..1.0[
    return fract(xy * 95.4307) + fract(xy * 75.04961) - 1.0;
}

// n must not be normalize (e.g. window coordinates)
float interleavedGradientNoise(float2 n) {
    return fract(52.982919 * fract(dot(float2(0.06711, 0.00584), n)));
}

//------------------------------------------------------------------------------
// Dithering
//------------------------------------------------------------------------------

float4 Dither_InterleavedGradientNoise(float4 rgba, const float temporalNoise01) {
    // Jimenez 2014, "Next Generation Post-Processing in Call of Duty"
    float2 uv = gl_FragCoord.xy + temporalNoise01;

    // The noise variable must be to workaround Adreno bug #1096.
    float noise = interleavedGradientNoise(uv);

    // remap from [0..1[ to [-0.5..0.5[
    noise -= 0.5;

    return rgba + float4(noise / 255.0);
}

float4 Dither_TriangleNoise(float4 rgba, const float temporalNoise01) {
    // Gjøl 2016, "Banding in Games: A Noisy Rant"
    float2 uv = gl_FragCoord.xy * frameUniforms.resolution.zw;
    uv += float2(0.07 * temporalNoise01);

    // The noise variable must be to workaround Adreno bug #1096.
    float noise = triangleNoise(uv);

    // noise is in [-1..1[

    return rgba + float4(noise / 255.0);
}

float4 Dither_Vlachos(float4 rgba, const float temporalNoise01) {
    // Vlachos 2016, "Advanced VR Rendering"
    float noise = dot(float2(171.0, 231.0), gl_FragCoord.xy + temporalNoise01);
    float3 noiseRGB = fract(float3(noise) / float3(103.0, 71.0, 97.0));

    // remap from [0..1[ to [-0.5..0.5[
    noiseRGB -= 0.5;

    return float4(rgba.rgb + (noiseRGB / 255.0), rgba.a);
}

float4 Dither_TriangleNoiseRGB(float4 rgba, const float temporalNoise01) {
    // Gjøl 2016, "Banding in Games: A Noisy Rant"
    float2 uv = gl_FragCoord.xy * frameUniforms.resolution.zw;
    uv += float2(0.07 * temporalNoise01);

    float3 noiseRGB = float3(
            triangleNoise(uv),
            triangleNoise(uv + 0.1337),
            triangleNoise(uv + 0.3141));

    // noise is in [-1..1[

    return rgba + noiseRGB.xyzx / 255.0;
}

//------------------------------------------------------------------------------
// Dithering dispatch
//------------------------------------------------------------------------------

/**
 * Dithers the specified RGBA color based on the current time and fragment
 * coordinates the input must be in the final color space (including OECF).
 * This dithering function assumes we are dithering to an 8-bit target.
 * This function dithers the alpha channel assuming premultiplied output
 */
float4 dither(float4 rgba, const float temporalNoise01) {
#if DITHERING_OPERATOR == DITHERING_NONE
    return rgba;
#elif DITHERING_OPERATOR == DITHERING_INTERLEAVED_NOISE
    return Dither_InterleavedGradientNoise(rgba, temporalNoise01);
#elif DITHERING_OPERATOR == DITHERING_VLACHOS
    return Dither_Vlachos(rgba, temporalNoise01);
#elif DITHERING_OPERATOR  == DITHERING_TRIANGLE_NOISE
    return Dither_TriangleNoise(rgba, temporalNoise01);
#elif DITHERING_OPERATOR  == DITHERING_TRIANGLE_NOISE_RGB
    return Dither_TriangleNoiseRGB(rgba, temporalNoise01);
#endif
}
