﻿sampler baseTexture : register(s0);
sampler uvOffsetNoiseTexture : register(s1);

float globalTime;
float distortionStrength;
float maxLensingAngle;
float sourceRadius;
float2 sourcePosition;
float2 aspectRatioCorrectionFactor;
float3 uColor;
float3 uSecondaryColor;
float3 accretionDiskFadeColor;

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
    float distanceToSource = max(distance((coords - 0.5) * aspectRatioCorrectionFactor + 0.5, sourcePosition), 0);
    
    // Calculate the lensing angle based on the aforementioned distance. This uses distance-based exponential decay to ensure that the effect
    // does not extend far past the source itself.
    float gravitationalLensingAngle = distortionStrength * maxLensingAngle * exp(-distanceToSource / sourceRadius * 2);
    return gravitationalLensingAngle;
}

float4 ApplyColorEffects(float sourceRadius, float4 color, float gravitationalLensingAngle, float2 coords, float2 distortedCoords, float2 sourcePosition)
{
    // Calculate offset values based on noise. Points sampled from this always give back a unit vector's components in the Red and Green channels.
    float2 uvOffset1 = tex2D(uvOffsetNoiseTexture, distortedCoords + float2(0, globalTime * 0.8));
    float2 uvOffset2 = tex2D(uvOffsetNoiseTexture, distortedCoords * 0.4 + float2(0, globalTime * 0.7));
    
    // Calculate color interpolants. These are used below.
    // The black hole uses a little bit of the UV offset noise for calculating the edge boundaries. This helps make the effect feel a bit less
    // mathematically perfect and more aesthetically interesting.
    float offsetDistanceToSource = max(distance((coords - 0.5) * aspectRatioCorrectionFactor + 0.5, sourcePosition + uvOffset1 * 0.008), 0);
    float blackInterpolant = InverseLerp(sourceRadius, sourceRadius * 0.85, offsetDistanceToSource);
    float brightInterpolant = pow(InverseLerp(sourceRadius * (1.01 + uvOffset2.x * 0.1), sourceRadius * 0.97, offsetDistanceToSource), 1.6) * 0.6 + gravitationalLensingAngle * 7.777 / maxLensingAngle;
    float accretionDiskInterpolant = InverseLerp(sourceRadius * 1.93, sourceRadius * 1.3, offsetDistanceToSource) * (1 - brightInterpolant);
    
    // Calculate the inner bright color. This is the color used right at the edge of the black hole itself, where everything is burning due to extraordinary amounts of particle friction.
    float4 brightColor = float4(lerp(uColor, uSecondaryColor, uvOffset1.y), 1) * 2;
    
    // Interpolate towards the bright color first.
    color = lerp(color, brightColor, saturate(brightInterpolant) * distortionStrength);
    
    // Interpolate towards the accretion disk's color next. This is what is drawn as a bit beyond the burning bright edge. It is still heated, but not as much, and as such is closer to an orange
    // glow than a blazing yellowish white.
    color = lerp(color, float4(accretionDiskFadeColor, 1), accretionDiskInterpolant * distortionStrength);
    
    // Lastly, place the black hole in the center above everything.
    color = lerp(color, float4(0, 0, 0, 1), blackInterpolant * distortionStrength);
    
    return color;
}


float4 PixelShaderFunction(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    float4 color = tex2D(baseTexture, coords);
    float gravitationalLensingAngle = CalculateGravitationalLensingAngle(sourceRadius, coords, sourcePosition);
    float2 distortedCoords = RotatedBy(coords - 0.5, gravitationalLensingAngle) + 0.5;
    
    // Calculate the colors based on the above information.
    color = ApplyColorEffects(sourceRadius, color, gravitationalLensingAngle, coords, distortedCoords, sourcePosition);
    
    return color;
}

technique Technique1
{
    pass AutoloadPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}