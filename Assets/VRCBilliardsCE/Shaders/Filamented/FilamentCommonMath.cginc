#ifndef FILAMENT_COMMON_MATH
#define FILAMENT_COMMON_MATH
//------------------------------------------------------------------------------
// Common math
//------------------------------------------------------------------------------

/** @public-api */
#define PI                 3.14159265359
/** @public-api */
#define HALF_PI            1.570796327

#define MEDIUMP_FLT_MAX    65504.0
#define MEDIUMP_FLT_MIN    0.00006103515625

#ifdef TARGET_MOBILE
#define FLT_EPS            MEDIUMP_FLT_MIN
#define saturateMediump(x) min(x, MEDIUMP_FLT_MAX)
#else
#define FLT_EPS            1e-5
#define saturateMediump(x) x
#endif

#define saturate(x)        clamp(x, 0.0, 1.0)
#define atan2(x, y)        atan(y, x)

//------------------------------------------------------------------------------
// Scalar operations
//------------------------------------------------------------------------------

/**
 * Computes x^5 using only multiply operations.
 *
 * @public-api
 */
float pow5(float x) {
    float x2 = x * x;
    return x2 * x2 * x;
}

/**
 * Computes x^2 as a single multiplication.
 *
 * @public-api
 */
float sq(float x) {
    return x * x;
}

//------------------------------------------------------------------------------
// Vector operations
//------------------------------------------------------------------------------

/**
 * Returns the maximum component of the specified vector.
 *
 * @public-api
 */
float max3(const float3 v) {
    return max(v.x, max(v.y, v.z));
}

/**
 * Returns the minimum component of the specified vector.
 *
 * @public-api
 */
float min3(const float3 v) {
    return min(v.x, min(v.y, v.z));
}

//------------------------------------------------------------------------------
// Trigonometry
//------------------------------------------------------------------------------

/**
 * Approximates acos(x) with a max absolute error of 9.0x10^-3.
 * Valid in the range -1..1.
 */
float acosFast(float x) {
    // Lagarde 2014, "Inverse trigonometric functions GPU optimization for AMD GCN architecture"
    // This is the approximation of degree 1, with a max absolute error of 9.0x10^-3
    float y = abs(x);
    float p = -0.1565827 * y + 1.570796;
    p *= sqrt(1.0 - y);
    return x >= 0.0 ? p : PI - p;
}

/**
 * Approximates acos(x) with a max absolute error of 9.0x10^-3.
 * Valid only in the range 0..1.
 */
float acosFastPositive(float x) {
    float p = -0.1565827 * x + 1.570796;
    return p * sqrt(1.0 - x);
}
#endif // FILAMENT_COMMON_MATH