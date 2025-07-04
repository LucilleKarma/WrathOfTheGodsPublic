sampler baseTexture : register(s0);

float distortionStrength;
float maxLensingAngle;
float sourceRadii[5];
float2 zoom;
float2 sourcePositions[5];
float2 aspectRatioCorrectionFactor;

float InverseLerp(float from, float to, float x)
{
    return saturate((x - from) / (to - from));
}

float2 RotatedBy(float2 v, float theta)
{
    float s = sin(theta);
    float c = cos(theta);
    return float2(v.x * c - v.y * s, v.x * s + v.y * c);
}

float CalculateGravitationalLensingAngle(float sourceRadius, float2 coords, float2 sourcePosition)
{
    // Calculate how far the given pixels is from the source of the distortion. This autocorrects for the aspect ratio resulting in
    // non-square calculations.
    float2 zoomedCoords = (coords - 0.5) * aspectRatioCorrectionFactor / zoom + 0.5;
    float2 correctedSource = (sourcePosition - 0.5) * aspectRatioCorrectionFactor + 0.5;
    float distanceToSource = distance(zoomedCoords, correctedSource);
    
    // Calculate the lensing angle based on the aforementioned distance. This uses distance-based exponential decay to ensure that the effect
    // does not extend far past the source itself.
    float gravitationalLensingAngle = distortionStrength * maxLensingAngle * exp(-distanceToSource / sourceRadius * 2);
    return gravitationalLensingAngle;
}

float4 PixelShaderFunction(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    float lensingAngles[5];
    float2 distortedCoords = coords;
    for (int i = 0; i < 5; i++)
    {
        // Calculate the gravitational lensing angle and the coordinates that result from following its rotation.
        // This roughly follows the mathematics of relativistic gravitational lensing in the real world, albeit with a substitution for the impact parameter:
        // https://en.wikipedia.org/wiki/Gravitational_lensing_formalism
        // Concepts such as the speed of light, the gravitational constant, mass etc. aren't really necessary in this context since those physics definitions do not
        // exist in Terraria, and given how extreme their values are it's possible that using them would result in floating-point imprecisions.
        float2 sourcePosition = sourcePositions[i];
        lensingAngles[i] = CalculateGravitationalLensingAngle(sourceRadii[i], coords, sourcePosition);
        distortedCoords = RotatedBy(distortedCoords - 0.5, lensingAngles[i]) + 0.5;
    }
    
    return tex2D(baseTexture, distortedCoords);
}

technique Technique1
{
    pass AutoloadPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}