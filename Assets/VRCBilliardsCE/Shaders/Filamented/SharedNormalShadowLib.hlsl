#ifndef SERVICE_NORMALSHADOW_INCLUDED
#define SERVICE_NORMALSHADOW_INCLUDED

// Ref: Normal Mapping Shadows by Boris Vorontsov

// Please define a NormalMapShadowsParam parameter
// and SampleNormalMap function to sample the normal map.

float NormalMapShadows (float3 lightDirTangent, NormalMapShadowsParam nmsParam, float noise,
	float heightScale, float shadowHardness)
{
	const float screenShadowSamples = 20;
	const float hardness = heightScale * shadowHardness;
	const float sampleStep = 1.0 / screenShadowSamples;

	float2 dir = lightDirTangent.xy * heightScale;

	// Redundancy can't be helped
	float3 normal = SampleNormalMap(nmsParam, 0);

	lightDirTangent = normalize(lightDirTangent);
    float tangentNdotL = saturate(dot(lightDirTangent, normal));

	float currentSample = sampleStep - sampleStep * noise;

    // Skip on backfaces
	currentSample += (tangentNdotL <= 0.0);

	/*
	From the PDF:
	Trace from hit point to light direction and compute sum of dot products
	between normal map and light direction.
	If slope is bigger than 0, pixel is shadowed. If slope is also bigger
	than previous maximal value, increase hardness of shadow.
	*/

	float result = 0;
	float slope = -tangentNdotL;
	float maxslope = 0.0;
	while (currentSample <= 1.0)
	{
		normal = SampleNormalMap(nmsParam, dir * currentSample);
		tangentNdotL = dot(lightDirTangent, normal);
		slope = slope - tangentNdotL;
		if (slope > maxslope)
		{
			result += hardness * (1.0-currentSample);
		}
		maxslope = max(maxslope, slope);
		currentSample += sampleStep;
	}

	return result * sampleStep;
}

#endif // SERVICE_NORMALSHADOW_INCLUDED