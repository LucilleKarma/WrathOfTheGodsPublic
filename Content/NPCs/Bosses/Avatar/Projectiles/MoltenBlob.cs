using Luminance.Assets;
using Luminance.Common.DataStructures;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm;
using NoxusBoss.Content.Particles.Metaballs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.Avatar.Projectiles;

public class MoltenBlob : ModProjectile, IPixelatedPrimitiveRenderer, IProjOwnedByBoss<AvatarOfEmptiness>
{
    /// <summary>
    /// How long the blob should exist for.
    /// </summary>
    public int Lifetime => SecondsToFrames(Lerp(2f, 3f, Projectile.identity / 15f % 1f));

    /// <summary>
    /// The amount of time that's passed since the blob spawned, in frames.
    /// </summary>
    public ref float Time => ref Projectile.ai[0];

    public override string Texture => MiscTexturesRegistry.InvisiblePixelPath;

    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailingMode[Type] = 2;
        ProjectileID.Sets.TrailCacheLength[Type] = 18;
    }

    public override void SetDefaults()
    {
        Projectile.width = 26;
        Projectile.height = 26;
        Projectile.hostile = true;
        Projectile.ignoreWater = true;
        Projectile.tileCollide = false;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 3600;
        CooldownSlot = ImmunityCooldownID.Bosses;
    }

    public override void AI()
    {
        // Ensure the Avatar is present. If he isn't, die immediately.
        if (AvatarOfEmptiness.Myself is null)
        {
            Projectile.Kill();
            return;
        }

        // Adhere to gravity.
        float maxFallSpeed = Lerp(18f, 22.5f, Projectile.identity / 11f % 1f);

        if (Projectile.velocity.Length() < 25f)
        {
            Projectile.velocity.X *= 0.993f;
            Projectile.velocity.Y = Clamp(Projectile.velocity.Y + 0.03f, -20f, maxFallSpeed);
        }
        else
            Projectile.velocity *= 0.87f;

        if (Collision.SolidCollision(Projectile.TopLeft, Projectile.width, Projectile.height))
            Projectile.velocity = Vector2.Zero;

        // Create lava particles.
        MoltenLavaMetaball metaball = ModContent.GetInstance<MoltenLavaMetaball>();
        for (int i = 0; i < 2; i++)
            metaball.CreateParticle(Projectile.Center, Main.rand.NextVector2Circular(3f, 3f) + Projectile.velocity * 0.35f, Projectile.width, Main.rand.NextFloat(0.09f));

        // Increment time.
        Time++;

        // Die once the life time has been reached.
        if (Time >= Lifetime)
            Projectile.Kill();
    }

    public float FlameTrailWidthFunction(float completionRatio)
    {
        return SmoothStep(15f, 1f, Pow(completionRatio, 0.7f)) * Projectile.Opacity;
    }

    public Color FlameTrailColorFunction(float completionRatio)
    {
        // Make the trail fade out at the end and fade in sharply at the start, to prevent the trail having a definitive, flat "start".
        float trailOpacity = InverseLerpBump(0f, 0.067f, 0.27f, 0.75f, completionRatio) * 0.9f;

        // Interpolate between a bunch of colors based on the completion ratio.
        Color color = Color.Lerp(Color.Orange, Color.Brown * 0.5f, completionRatio) * trailOpacity;

        return color * Projectile.Opacity * 1.6f;
    }

    public override bool PreDraw(ref Color lightColor)
    {
        Vector2 drawPosition = Projectile.Center - Main.screenPosition;
        Main.spriteBatch.Draw(BloomCircleSmall, drawPosition, null, Projectile.GetAlpha(Color.Wheat) with { A = 0 }, 0f, BloomCircleSmall.Size() * 0.5f, 0.4f, 0, 0f);
        Main.spriteBatch.Draw(BloomCircleSmall, drawPosition, null, Projectile.GetAlpha(Color.White) with { A = 0 }, 0f, BloomCircleSmall.Size() * 0.5f, 0.23f, 0, 0f);

        return false;
    }

    public void RenderPixelatedPrimitives(SpriteBatch spriteBatch)
    {
        ManagedShader shader = ShaderManager.GetShader("NoxusBoss.MoltenFlameTrail");
        shader.SetTexture(MilkyNoise, 1, SamplerState.LinearWrap);

        PrimitiveSettings settings = new PrimitiveSettings(FlameTrailWidthFunction, FlameTrailColorFunction, _ => Projectile.Size * 0.5f, Pixelate: true, Shader: shader);
        PrimitiveRenderer.RenderTrail(Projectile.oldPos, settings, 8);
    }
}
