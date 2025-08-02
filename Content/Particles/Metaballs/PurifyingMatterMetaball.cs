using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace NoxusBoss.Content.Particles.Metaballs;

public class PurifyingMatterMetaball : MetaballType
{
    public override string MetaballAtlasTextureToUse => "NoxusBoss.BasicMetaballCircle.png";

    public override bool ShouldRender => ActiveParticleCount >= 1;

    public override Func<Texture2D>[] LayerTextures => new Func<Texture2D>[]
    {
        () => ModContent.Request<Texture2D>("NoxusBoss/Assets/Textures/Content/Particles/PitchBlackLayer").Value
    };

    public override Color EdgeColor => new(120, 22, 181);

    public override void PrepareShaderForTarget(int layerIndex)
    {
        // Store the in an easy to use local variables.
        ManagedShader metaballShader = ShaderManager.GetShader("NoxusBoss.PurifyingMatterMetaballShader");

        // Fetch the layer texture. This is the texture that will be overlaid over the greyscale contents on the screen.
        Texture2D layerTexture = LayerTextures[layerIndex]();

        // Calculate the layer scroll offset. This is used to ensure that the texture contents of the given metaball have parallax, rather than being static over the screen
        // regardless of world position.
        // This may be toggled off optionally by the metaball.
        Vector2 screenSize = Main.ScreenSize.ToVector2();
        Vector2 layerScrollOffset = Main.screenPosition / screenSize + CalculateManualOffsetForLayer(layerIndex);
        if (LayerIsFixedToScreen(layerIndex))
            layerScrollOffset = Vector2.Zero;

        float[] blurWeights = new float[9];
        for (int i = 0; i < blurWeights.Length; i++)
            blurWeights[i] = GaussianDistribution(i - blurWeights.Length * 0.5f, 1.8f) / 9f;

        // Supply shader parameter values.
        metaballShader.TrySetParameter("layerSize", layerTexture.Size());
        metaballShader.TrySetParameter("screenSize", screenSize);
        metaballShader.TrySetParameter("layerOffset", layerScrollOffset);
        metaballShader.TrySetParameter("edgeColor", EdgeColor.ToVector4());
        metaballShader.TrySetParameter("blurWeights", blurWeights);
        metaballShader.TrySetParameter("blurOffset", Vector2.One * 1.5f / layerTexture.Size());
        metaballShader.TrySetParameter("singleFrameScreenOffset", (Main.screenLastPosition - Main.screenPosition) / screenSize);

        // Supply the metaball's layer texture to the graphics device so that the shader can read it.
        metaballShader.SetTexture(layerTexture, 1, SamplerState.LinearWrap);

        // Apply the metaball shader.
        metaballShader.Apply();
    }

    public override bool ShouldKillParticle(MetaballInstance particle) => particle.Size <= 2f;

    public override void UpdateParticle(MetaballInstance particle)
    {
        particle.Size *= 0.96f;
        particle.Size -= 1.05f;
        particle.Velocity *= 0.97f;
    }
}
