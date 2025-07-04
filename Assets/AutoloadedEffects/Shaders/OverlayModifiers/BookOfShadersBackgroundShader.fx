sampler baseTexture : register(s0);
sampler rainbowTexture : register(s1);

float globalTime;

float4 PixelShaderFunction(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0, float4 position : SV_Position) : COLOR0
{
    float4 color = tex2D(baseTexture, coords) * sampleColor;
    float greyscale = dot(color.rgb, float3(0.3, 0.6, 0.1));
    float time = globalTime * 0.3;
    float2 uv = position.xy / 3450;
    
    float2 warp = 
        tex2D(rainbowTexture, uv * 3.3 + time * float2(0, 0.2)).rg * 0.04 -
        tex2D(rainbowTexture, uv * 2.1 + time * float2(0.2, 0)).rg * 0.02 + uv.y + time * float2(-0.05, 0);
    float4 rainbow = tex2D(rainbowTexture, uv + warp) * (1 + greyscale * 8);
    
    rainbow *= tex2D(baseTexture, coords).a;
    return rainbow;
}

technique Technique1
{
    pass AutoloadPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}