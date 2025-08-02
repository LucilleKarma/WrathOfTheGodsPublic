using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace NoxusBoss.Content.Particles.Metaballs;

public class TileDistortionMetaball : MetaballType
{
    public override string MetaballAtlasTextureToUse => "NoxusBoss.StrongBloom.png";

    public override Color EdgeColor => Color.Transparent;

    public override bool ShouldRender => ActiveParticleCount >= 1;

    public override bool DrawnManually => true;

    public override Func<Texture2D>[] LayerTextures => [() => WhitePixel];

    public override bool LayerIsFixedToScreen(int layerIndex) => true;

    public override bool PerformCustomSpritebatchBegin(SpriteBatch spriteBatch)
    {
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearWrap, DepthStencilState.Default, RasterizerState.CullNone, null, Matrix.Identity);
        return true;
    }

    public override void UpdateParticle(MetaballInstance particle)
    {
        particle.Size *= 1.11f;
        particle.ExtraInfo[0] += 0.015f;
    }

    public override void DrawInstances()
    {
        AtlasTexture texture = AtlasManager.GetTexture(MetaballAtlasTextureToUse);
        foreach (MetaballInstance? particle in Particles)
        {
            float fadeOut = 1f - particle.ExtraInfo[0];
            Main.spriteBatch.Draw(texture, particle.Center - Main.screenPosition, null, Color.White * fadeOut, 0f, null, new Vector2(particle.Size) * new Vector2(1.5f, 0.56f) / texture.Frame.Size(), SpriteEffects.None);
        }
    }

    public override bool ShouldKillParticle(MetaballInstance particle) => particle.ExtraInfo[0] >= 1f;
}
