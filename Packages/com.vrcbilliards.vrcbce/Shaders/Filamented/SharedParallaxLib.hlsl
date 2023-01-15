#ifndef SERVICE_PARALLAX_INCLUDED
#define SERVICE_PARALLAX_INCLUDED

// Please define a PerPixelHeightDisplacementParam parameter
// and ComputePerPixelHeightDisplacement function to sample the heightmap.

float2 ParallaxRaymarching(float2 viewDir, PerPixelHeightDisplacementParam ppdParam, 
    float strength, out float outHeight)
{
    const float raymarch_steps = 10;

    float2 uvOffset = 0;
    const float stepSize = 1.0 / raymarch_steps;
    float2 uvDelta = viewDir * (stepSize * strength);
    
    float stepHeight = 1;
    float surfaceHeight = ComputePerPixelHeightDisplacement(0, 0, ppdParam);
    
    float2 prevUVOffset = uvOffset;
    float prevStepHeight = stepHeight;
    float prevSurfaceHeight = surfaceHeight;
    
    for (int i = 1; i < raymarch_steps && stepHeight > surfaceHeight; i++)
    {
        prevUVOffset = uvOffset;
        prevStepHeight = stepHeight;
        prevSurfaceHeight = surfaceHeight;
        
        uvOffset -= uvDelta;
        stepHeight -= stepSize;
        surfaceHeight = ComputePerPixelHeightDisplacement(uvOffset, 0, ppdParam);
    }
    
    float prevDifference = prevStepHeight - prevSurfaceHeight;
    float difference = surfaceHeight - stepHeight;
    float t = prevDifference / (prevDifference + difference);
    uvOffset = prevUVOffset -uvDelta * t;
    
    outHeight = surfaceHeight;
    return uvOffset;
}

#endif // SERVICE_PARALLAX_INCLUDED