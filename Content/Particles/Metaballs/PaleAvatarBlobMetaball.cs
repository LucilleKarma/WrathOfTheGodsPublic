using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Content.NPCs.Bosses.Avatar.Projectiles;
using Terraria;
using Terraria.ModLoader;

namespace NoxusBoss.Content.Particles.Metaballs;

public class PaleAvatarBlobMetaball : MetaballType
{
    public override string MetaballAtlasTextureToUse => "NoxusBoss.BasicMetaballCircle.png";

    public override Color EdgeColor => new(41, 201, 219);

    public override bool ShouldRender => ActiveParticleCount >= 1 || AnyProjectiles(ModContent.ProjectileType<DimensionTwistedComet>());

    public override void PrepareShaderForTarget(int layerIndex)
    {
        // Store the shader in an easy to use local variable.
        ManagedShader metaballShader = ShaderManager.GetShader("NoxusBoss.PaleAvatarBlobMetaballShader");

        // Fetch the layer texture. This is the texture that will be overlaid over the greyscale contents on the screen.
        Texture2D layerTexture = LayerTextures[layerIndex]();

        // Calculate the layer scroll offset. This is used to ensure that the texture contents of the given metaball have parallax, rather than being static over the screen
        // regardless of world position.
        // This may be toggled off optionally by the metaball.
        Vector2 screenSize = Main.ScreenSize.ToVector2();
        Vector2 layerScrollOffset = Main.screenPosition / screenSize + CalculateManualOffsetForLayer(layerIndex);
        if (LayerIsFixedToScreen(layerIndex))
            layerScrollOffset = Vector2.Zero;

        // Supply shader parameter values.
        metaballShader.TrySetParameter("layerSize", layerTexture.Size());
        metaballShader.TrySetParameter("screenSize", screenSize);
        metaballShader.TrySetParameter("layerOffset", layerScrollOffset);
        metaballShader.TrySetParameter("edgeColor", EdgeColor.ToVector4());
        metaballShader.TrySetParameter("singleFrameScreenOffset", (Main.screenLastPosition - Main.screenPosition) / screenSize);

        // Supply the metaball's layer texture to the graphics device so that the shader can read it.
        metaballShader.SetTexture(layerTexture, 1, SamplerState.LinearWrap);

        // Apply the metaball shader.
        metaballShader.Apply();
    }

    public override Func<Texture2D>[] LayerTextures => [() => WhitePixel];

    public override void UpdateParticle(MetaballInstance particle)
    {
        particle.Velocity *= 0.99f;
        particle.Velocity = Collision.TileCollision(particle.Center, particle.Velocity, 1, 1);
        if (particle.Velocity.Y == 0f)
        {
            particle.Velocity.X *= 0.5f;
            particle.Size *= 0.93f;
        }

        particle.Size *= 0.93f;
    }

    public override bool ShouldKillParticle(MetaballInstance particle) => particle.Size <= 2f;

    public override void ExtraDrawing()
    {
        int aimedPaleCometID = ModContent.ProjectileType<DimensionTwistedComet>();
        foreach (Projectile p in Main.projectile.Where(p => p.active))
        {
            if (p.type == aimedPaleCometID)
            {
                Color c = Color.White;
                p.ModProjectile.PreDraw(ref c);
            }
        }
    }
}
