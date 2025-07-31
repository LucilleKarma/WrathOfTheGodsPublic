using Luminance.Assets;
using Luminance.Common.DataStructures;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity.SpecificEffectManagers;
using NoxusBoss.Content.Particles;
using NoxusBoss.Core.Graphics.SpecificEffectManagers;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.NamelessDeity.Projectiles;

public class LightWave : ModProjectile, IProjOwnedByBoss<NamelessDeityBoss>
{
    public static readonly int Lifetime = SecondsToFrames(0.6f);

    public ref float Radius => ref Projectile.ai[0];

    public static Color DetermineExplosionColor()
    {
        Color c = Color.Lerp(Color.IndianRed, Color.Wheat, 0.24f);
        c = Color.Lerp(c, Color.Cyan, NamelessDeitySky.DifferentStarsInterpolant * 0.85f);
        return c with { A = 80 };
    }

    public override string Texture => MiscTexturesRegistry.InvisiblePixelPath;

    public override void SetDefaults()
    {
        Projectile.width = 72;
        Projectile.height = 72;
        Projectile.penetrate = -1;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.timeLeft = Lifetime;
        Projectile.scale = 0.001f;
    }

    public override void AI()
    {
        // Make screen shove effects happen.
        if (Projectile.localAI[0] == 0f)
        {
            RadialScreenShoveSystem.Start(Projectile.Center, 20);
            ScreenShakeSystem.StartShakeAtPoint(Projectile.Center, 6f, shakeStrengthDissipationIncrement: 0.33f);
            Projectile.localAI[0] = 1f;
        }

        // Cause the wave to expand outward, along with its hitbox.
        Radius = Lerp(Radius, 3200f, 0.039f);
        Projectile.scale = Lerp(1.2f, 4.5f, InverseLerp(Lifetime, 0f, Projectile.timeLeft));
        Projectile.Opacity = InverseLerp(2f, 15f, Projectile.timeLeft);

        // Randomly create small light particles.
        float lightVelocityArc = Pi * InverseLerp(Lifetime, 0f, Projectile.timeLeft);
        for (int i = 0; i < 25; i++)
        {
            Vector2 particleSpawnPosition = Projectile.Center + Main.rand.NextVector2Unit() * Radius * Projectile.scale * Main.rand.NextFloat(0.75f, 0.96f);
            Vector2 particleVelocity = (particleSpawnPosition - Projectile.Center).SafeNormalize(Vector2.UnitY).RotatedBy(lightVelocityArc) * Main.rand.NextFloat(2f, 25f);
            SquishyLightParticle particle = new SquishyLightParticle(particleSpawnPosition, particleVelocity, Main.rand.NextFloat(0.24f, 0.41f), Color.Lerp(Color.Wheat, Color.Yellow, Main.rand.NextFloat(0.7f)), Main.rand.Next(25, 44));
            particle.Spawn();
        }
    }

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        return CircularHitboxCollision(Projectile.Center, Radius * 0.4f, targetHitbox);
    }

    public override bool PreDraw(ref Color lightColor)
    {
        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearWrap, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        DrawData explosionDrawData = new DrawData(DendriticNoise, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), Color.White * Projectile.Opacity);

        ManagedShader shockwaveShader = ShaderManager.GetShader("NoxusBoss.ShockwaveShader");
        shockwaveShader.TrySetParameter("shockwaveColor", DetermineExplosionColor().ToVector3());
        shockwaveShader.TrySetParameter("screenSize", Main.ScreenSize.ToVector2());
        shockwaveShader.TrySetParameter("explosionDistance", Radius * Projectile.scale * 0.5f);
        shockwaveShader.TrySetParameter("projectilePosition", Projectile.Center - Main.screenPosition);
        shockwaveShader.TrySetParameter("shockwaveOpacityFactor", Projectile.Opacity);
        shockwaveShader.Apply();
        explosionDrawData.Draw(Main.spriteBatch);

        Main.spriteBatch.ResetToDefault();
        return false;
    }
}
