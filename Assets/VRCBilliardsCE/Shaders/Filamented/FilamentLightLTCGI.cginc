#ifndef FILAMENT_LIGHT_LTCGI
#define FILAMENT_LIGHT_LTCGI

#if defined(_LTCGI)
    #if defined(_SPECULARHIGHLIGHTS_OFF)
        #define LTCGI_SPECULAR_OFF
    #endif
#include "Assets/_pi_/_LTCGI/Shaders/LTCGI.cginc"
#endif

//------------------------------------------------------------------------------
// LTCGI evaluation
//------------------------------------------------------------------------------

// This is a small function to abstract the calls to the LTCGI functions.

void evaluateLTCGI(const ShadingParams shading, const PixelParams pixel, inout float3 color) {
#if defined(_LTCGI)
    float3 diffuse = 0;
    float3 specular = 0;

    LTCGI_Contribution(
        shading.position, 
        shading.normal, 
        shading.view, 
        pixel.perceptualRoughness, 
        shading.lightmapUV.xy, 
        /* out */ diffuse,
        /* out */ specular
    );
    color.rgb += specular + diffuse;
#endif
}

#endif // FILAMENT_LIGHT_LTCGI