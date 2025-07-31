using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace NoxusBoss.Content.Particles.Metaballs;

public class BloodMetaball : MetaballType
{
    public override string MetaballAtlasTextureToUse => "NoxusBoss.DistancedMetaballCircle.png";

    public override Color EdgeColor => Color.Transparent;

    public override bool ShouldRender => ActiveParticleCount >= 1;

    public override bool DrawnManually => false;

    public override Func<Texture2D>[] LayerTextures => [() => Main.gameMenu ? InvisiblePixel : BubblyNoise];

    public override void UpdateParticle(MetaballInstance particle)
    {
        particle.Size *= 0.978f;
        particle.Velocity.X *= 0.97f;
        particle.Velocity.Y += 0.6f;
        if (particle.Velocity.Y > 16f)
            particle.Velocity.Y = Lerp(particle.Velocity.Y, 16f, 0.04f);

        particle.Velocity = Collision.TileCollision(particle.Center, particle.Velocity, 1, 1);
    }

    public override bool ShouldKillParticle(MetaballInstance particle) => particle.Size <= 2f;

    public override void PrepareShaderForTarget(int layerIndex)
    {
        // Store the shader in an easy to use local variable.
        ManagedShader metaballShader = ShaderManager.GetShader("NoxusBoss.BloodMetaballShader");

        // Fetch the layer texture. This is the texture that will be overlaid over the greyscale contents on the screen.
        Texture2D layerTexture = LayerTextures[layerIndex]();

        // Calculate the layer scroll offset. This is used to ensure that the texture contents of the given metaball have parallax, rather than being static over the screen
        // regardless of world position.
        // This may be toggled off optionally by the metaball.
        Vector2 screenSize = Main.ScreenSize.ToVector2();
        Vector2 layerScrollOffset = Main.screenPosition / screenSize + CalculateManualOffsetForLayer(layerIndex);
        if (LayerIsFixedToScreen(layerIndex))
            layerScrollOffset = Vector2.Zero;

        Vector3[] palette = new Vector3[]
        {
            new Color(189, 8, 2).ToVector3(),
            new Color(99, 7, 8).ToVector3(),
            new Color(56, 5, 19).ToVector3()
        };

        metaballShader.TrySetParameter("layerSize", layerTexture.Size());
        metaballShader.TrySetParameter("screenSize", screenSize);
        metaballShader.TrySetParameter("layerOffset", layerScrollOffset);
        metaballShader.TrySetParameter("singleFrameScreenOffset", (Main.screenLastPosition - Main.screenPosition) / screenSize);
        metaballShader.TrySetParameter("gradient", palette);
        metaballShader.TrySetParameter("gradientCount", palette.Length);
        metaballShader.TrySetParameter("dissolvePersistence", 1.65f);
        metaballShader.TrySetParameter("maxDistortionOffset", new Vector2(20f, 8f));
        metaballShader.SetTexture(PerlinNoise, 1, SamplerState.LinearWrap);
        metaballShader.SetTexture(WavyBlotchNoiseDetailed, 2, SamplerState.LinearWrap);
        metaballShader.SetTexture(DendriticNoise, 3, SamplerState.LinearWrap);

        // Apply the metaball shader.
        metaballShader.Apply();
    }

    public override void DrawInstances()
    {
        AtlasTexture texture = AtlasManager.GetTexture(MetaballAtlasTextureToUse);
        IOrderedEnumerable<MetaballInstance> darknessOrderedParticles = Particles.OrderByDescending(p => p.ExtraInfo[0]);

        foreach (MetaballInstance? particle in darknessOrderedParticles)
        {
            float darknessInterpolant = particle.ExtraInfo[0];
            float dissolveInterpolant = Pow(InverseLerp(2f, 55f, particle.Size), 1f);
            float squish = particle.Velocity.Length() * 0.035f;
            if (squish > 0.67f)
                squish = 0.67f;

            float rotation = particle.Velocity.ToRotation();
            Color color = new Color(1f, darknessInterpolant, dissolveInterpolant);
            Vector2 particleSize = new Vector2(1f + squish, 1f - squish) * particle.Size;

            Main.spriteBatch.Draw(texture, particle.Center - Main.screenPosition, null, color, rotation, null, particleSize / texture.Frame.Size(), SpriteEffects.None);
        }
    }
}
