sampler noiseTexture : register(s1);
sampler highlightNoiseTexture : register(s2);

bool drawAdditively;
float globalTime;
float darknessNoiseScrollSpeed;
float brightnessNoiseScrollSpeed;
float2 darknessScrollOffset;
float2 brightnessScrollOffset;
float2 uCorrectionOffset;
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
    output.Position = pos + float4(uCorrectionOffset.x, uCorrectionOffset.y, 0, 0);
    output.Position.z = 0;
    
    output.Color = input.Color;
    output.TextureCoordinates = input.TextureCoordinates;

    return output;
}

float QuadraticBump(float x)
{
    return x * (4 - x * 4);
}

float4 PixelShaderFunction(VertexShaderOutput input) : COLOR0
{
    float4 color = input.Color;
    float2 baseCoords = input.TextureCoordinates;
    
    // Account for texture distortion artifacts in accordance with the primitive distortion fixes.
    baseCoords.y = (baseCoords.y - 0.5) / input.TextureCoordinates.z + 0.5;
    
    float2 coords = frac(baseCoords * float2(2, 1));
    float3 finalColor = color.rgb;
    
    // Calculate color-affecting interpolants.
    float darknessNoise = tex2D(noiseTexture, coords * float2(2, 1) + darknessScrollOffset + float2(globalTime * -darknessNoiseScrollSpeed, 0));
    float fadeToWhite = smoothstep(0.24 - darknessNoise * 0.15, 0, baseCoords.x) * 0.85;
    float brightnessNoise = tex2D(noiseTexture, coords * float2(2, 1) + brightnessScrollOffset + float2(globalTime * -brightnessNoiseScrollSpeed, 0));
    
    // Apply darkness effects.
    finalColor += float3(-0.75, -0.75, -0.45) * darknessNoise;
    
    // Apply brightness effects.
    finalColor *= lerp(1, 6, pow(brightnessNoise, 2));
    finalColor.rg -= brightnessNoise * 0.4;
    
    // Increase the contrast of the color a good bit, based on the brightness interpolant.
    // This "splits" the color bands to their extremes the higher the power is.
    float contrastPower = brightnessNoise * 0.75 + 1.95;
    finalColor = finalColor * contrastPower + (1 - contrastPower) * 0.5;
    
    // Calculate the highlight. This allows dark parts to have a bit of interesting texturing.
    float3 highlight = (1 - tex2D(highlightNoiseTexture, coords * float2(1.5, 1) + float2(0, globalTime * -0.06))) * lerp(0.5, 5, pow(darknessNoise, 3));
    
    // Calculate the edge-fade opacity.
    float opacity = smoothstep(0, 0.2, coords.y) * smoothstep(1, 0.8, coords.y) * 0.7;
    
    // Calculate the final color, applying additive blending if parameters request it.
    fadeToWhite += pow(QuadraticBump(baseCoords.y), 5 + darknessNoise * 50);
    float4 result = lerp(float4(finalColor + pow(highlight, 3.6), 1), 2, fadeToWhite) * opacity;
    result.a *= 1 - drawAdditively;
    
    // Ensure that the laser doesn't have a flat, unnatural start and end.
    float startFade = smoothstep(0.01, 0.05, baseCoords.x);
    float endFade = smoothstep(0.97, 0.85, baseCoords.x + darknessNoise * 0.07);
    
    return saturate(result) * startFade * endFade;
}

technique Technique1
{
    pass AutoloadPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
