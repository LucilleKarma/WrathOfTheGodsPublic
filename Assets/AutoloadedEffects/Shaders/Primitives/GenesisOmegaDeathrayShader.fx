sampler noiseScrollTexture : register(s1);
sampler lightningScrollTexture : register(s2);

float globalTime;
float edgeGlowIntensity;
float gradientCount;
float3 gradient[8];
float3 edgeColorSubtraction;
matrix uWorldViewProjection;

struct VertexShaderInput
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float3 TextureCoordinates : TEXCOORD0;
};

struct VertexShaderOutput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float3 TextureCoordinates : TEXCOORD0;
};

VertexShaderOutput VertexShaderFunction(in VertexShaderInput input)
{
    VertexShaderOutput output = (VertexShaderOutput) 0;
    float4 pos = mul(input.Position, uWorldViewProjection);
    output.Position = pos;
    
    output.Color = input.Color;
    output.TextureCoordinates = input.TextureCoordinates;

    return output;
}

float4 PaletteLerp(float interpolant)
{
    int startIndex = clamp(frac(interpolant) * gradientCount, 0, gradientCount - 1);
    int endIndex = (startIndex + 1) % gradientCount;
    return float4(lerp(gradient[startIndex], gradient[endIndex], frac(interpolant * gradientCount)), 1);
}

float4 PixelShaderFunction(VertexShaderOutput input) : COLOR0
{
    float2 coords = input.TextureCoordinates;
        
    // Account for texture distortion artifacts in accordance with the primitive distortion fixes.
    coords.y = (coords.y - 0.5) / input.TextureCoordinates.z + 0.5;
    
    float noise = tex2D(noiseScrollTexture, coords * float2(0.8, 1.75) + float2(globalTime * -2, 0));
    float4 color = input.Color * PaletteLerp(frac(noise * 1.5 + globalTime * 8));
    
    // Calculate the edge glow, creating a strong, bright center coloration.
    float distanceFromCenter = distance(coords.y, 0.5);
    float edgeGlow = edgeGlowIntensity / pow(distanceFromCenter, 0.9);
    color = saturate(color * edgeGlow);
    
    // Apply subtractive blending that gets stronger near the edges of the beam, to help with saturating the colors a bit.
    color.rgb -= distanceFromCenter * edgeColorSubtraction;
    
    // Apply additive blending in accordance with a lightning scroll texture.
    color += tex2D(lightningScrollTexture, coords * float2(0.9, 2) + float2(globalTime * -1.5, 0)).r * color.a * (color.g + 0.35);
    
    // Fade at the edges.
    color *= smoothstep(0.5, 0.3, distanceFromCenter);
    
    // Brighten the center tremendously.
    color += color.a / smoothstep(0, 0.1, distanceFromCenter) * 0.15;
    
    // Fade out at the bottom.
    color = saturate(color) * smoothstep(0.01, 0.03, coords.x - noise * 0.02);
    
    // Fade at the laser's end.
    float endOfLaserFade = smoothstep(0.98, 0.9 + noise * 0.06, coords.x);
    color *= endOfLaserFade;
    
    // Apply some fast, scrolling noise to the overall result.
    return color * (noise + 1 + step(0.5, noise + (0.5 - distanceFromCenter)));
}

technique Technique1
{
    pass AutoloadPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
