using Luminance.Assets;
using Luminance.Common.DataStructures;
using Microsoft.Xna.Framework;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm;
using NoxusBoss.Content.Particles.Metaballs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.Avatar.Projectiles;

public class DarkGas : ModProjectile, IProjOwnedByBoss<AvatarOfEmptiness>
{
    public ref float Time => ref Projectile.ai[0];

    public override string Texture => MiscTexturesRegistry.InvisiblePixelPath;

    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        ProjectileID.Sets.TrailCacheLength[Projectile.type] = 5;
    }

    public override void SetDefaults()
    {
        Projectile.width = 68;
        Projectile.height = 68;
        Projectile.hostile = true;
        Projectile.ignoreWater = true;
        Projectile.tileCollide = false;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 270;
        CooldownSlot = ImmunityCooldownID.Bosses;
    }

    public override void AI()
    {
        // Create gas particles.
        DarkGasMetaball metaball = ModContent.GetInstance<DarkGasMetaball>();
        float gasSize = InverseLerp(-3f, 25f, Time) * Projectile.width * 0.86f;
        float angularOffset = Sin(Time / 5f) * 0.77f;
        metaball.CreateParticle(Projectile.Center + Projectile.velocity * 2f, Main.rand.NextVector2Circular(2f, 2f) + Projectile.velocity.RotatedBy(angularOffset).RotatedByRandom(0.6f) * 0.26f, gasSize);

        // Dissipate if on top of the nearest player.
        Player closest = Main.player[Player.FindClosest(Projectile.Center, 1, 1)];
        if (Projectile.WithinRange(closest.Center, 35f))
        {
            for (int i = 0; i < 25; i++)
            {
                Vector2 gasParticleVelocity = Main.rand.NextVector2Circular(3.2f, 3.2f) + Projectile.velocity * 0.2f;
                metaball.CreateParticle(Projectile.Center, gasParticleVelocity, gasSize * Main.rand.NextFloat(0.2f, 0.46f));
            }

            Projectile.Kill();
        }

        // Accelerate after existing for a sufficient amount of time.
        if (Time >= 120f)
            Projectile.velocity *= 1.014f;

        Time++;
    }

    public override bool PreDraw(ref Color lightColor) => false;
}
