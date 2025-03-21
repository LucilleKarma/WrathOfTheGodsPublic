sampler noiseTexture : register(s1);

float globalTime;
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
    float2 TextureCoordinates : TEXCOORD0;
};

VertexShaderOutput VertexShaderFunction(in VertexShaderInput input)
{
    VertexShaderOutput output = (VertexShaderOutput) 0;
    float4 pos = mul(input.Position, uWorldViewProjection);
    output.Position = pos;
    
    output.Color = input.Color;
    output.TextureCoordinates = input.TextureCoordinates;
    output.TextureCoordinates.y = (output.TextureCoordinates.y - 0.5) / input.TextureCoordinates.z + 0.5;

    return output;
}

float4 PixelShaderFunction(VertexShaderOutput input) : COLOR0
{
    // Store primitive data in local variables.
    float4 color = input.Color;
    float2 coords = input.TextureCoordinates;
    
    float distanceFromHorizontalCenter = distance(coords.y, 0.5);
    float distanceFromEdge = distance(distanceFromHorizontalCenter, 0.5);
    float erasureNoise = tex2D(noiseTexture, coords * 1.3 + float2(globalTime * -1.4, 0.3));
    bool erasePixel = erasureNoise + distanceFromEdge * 0.7 < 0.12;
    
    return color * (1 - erasePixel);
}

technique Technique1
{
    pass AutoloadPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
