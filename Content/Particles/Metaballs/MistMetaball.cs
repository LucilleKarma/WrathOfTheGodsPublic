using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using Terraria;

namespace NoxusBoss.Content.Particles.Metaballs;

public class MistMetaball : MetaballType
{
    public override string MetaballAtlasTextureToUse => "NoxusBoss.LargeMistParticle.png";

    public override Color EdgeColor => Color.Transparent;

    public override bool ShouldRender => ActiveParticleCount >= 1;

    public override bool DrawnManually => true;

    public override Func<Texture2D>[] LayerTextures => [() => GennedAssets.Textures.Particles.PitchBlackLayer];

    public override void UpdateParticle(MetaballInstance particle)
    {
        particle.Velocity *= 0.98f;
        particle.Size *= 0.9f;
    }

    public override bool PerformCustomSpritebatchBegin(SpriteBatch spriteBatch)
    {
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp, DepthStencilState.Default, RasterizerState.CullNone, null, Matrix.Identity);
        return true;
    }

    public override void DrawInstances()
    {
        AtlasTexture texture = AtlasManager.GetTexture(MetaballAtlasTextureToUse);
        foreach (MetaballInstance? particle in Particles)
        {
            Rectangle frame = texture.Frame.Subdivide(1, 4, 0, (int)particle.ExtraInfo[0]);
            Main.spriteBatch.Draw(texture, particle.Center - Main.screenPosition, frame, Color.White, 0f, null, new Vector2(particle.Size) / frame.Size(), SpriteEffects.None);
        }
    }

    public override bool ShouldKillParticle(MetaballInstance particle) => particle.Size <= 2f;
}
