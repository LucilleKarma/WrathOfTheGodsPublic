sampler baseTexture : register(s1);

float globalTime;
float pinchExponent;
float spherePatternZoom;
float sphereSpinTime;
float2 pixelationFactor;

float4 PixelShaderFunction(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    // Pixelate things.
    coords = round(coords / pixelationFactor) * pixelationFactor;
    
    // Calculate the distance to the center of the star. This is magnified a bit for intensity purposes in later equations.
    float2 coordsNormalizedToCenter = (coords - 0.5) * 2;
    float distanceFromCenterSqr = dot(coordsNormalizedToCenter, coordsNormalizedToCenter) * 2;
    
    // Calculate coordinates relative to the sphere.
    // This pinch factor effectively ensures that the UVs are relative to a circle, rather than a rectangle.
    // This helps SIGNIFICANTLY for making the texturing look realistic, as it will appear to be traveling on a
    // sphere rather than on a sheet that happens to overlay a circle.
    float spherePinchFactor = (1 - sqrt(abs(1 - distanceFromCenterSqr))) / distanceFromCenterSqr + 0.001;
    
    // Exaggerate the pinch slightly.
    spherePinchFactor = pow(spherePinchFactor, pinchExponent);
    
    float2 sphereCoords = frac((coords - 0.5) * spherePinchFactor + 0.5 + float2(sphereSpinTime, 0));
    
    // Calculate the base color.
    float lattice = clamp(tex2D(baseTexture, (sphereCoords - 0.5) * spherePatternZoom + 0.5), 0.001, 1);
    float4 color = sampleColor / lattice * 0.67;
    
    // Calculate an edge glow value, to give the forcefield a strong outline.
    float distanceFromCenter = distance(coords, 0.5);
    float distanceFromEdge = distance(distanceFromCenter, 0.4);
    float edgeGlow = sampleColor.a * 0.04 / pow(distanceFromEdge, 1.5);
    
    // Combine things together.
    float edgeFade = smoothstep(1.1, 0.9, distanceFromCenterSqr);
    return saturate(color + edgeGlow) * edgeFade;
}

technique Technique1
{
    pass AutoloadPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}