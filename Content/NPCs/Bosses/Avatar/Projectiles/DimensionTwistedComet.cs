using Luminance.Common.DataStructures;
using Microsoft.Xna.Framework;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm;
using NoxusBoss.Content.Particles.Metaballs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.Avatar.Projectiles;

public class DimensionTwistedComet : ModProjectile, IProjOwnedByBoss<AvatarOfEmptiness>
{
    public ref float Time => ref Projectile.ai[1];

    public override string Texture => GetAssetPath("Content/NPCs/Bosses/Avatar/Projectiles", Name);

    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        ProjectileID.Sets.TrailCacheLength[Projectile.type] = 12;
    }

    public override void SetDefaults()
    {
        Projectile.width = 34;
        Projectile.height = 34;
        Projectile.hostile = true;
        Projectile.ignoreWater = true;
        Projectile.tileCollide = false;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 300;
        CooldownSlot = ImmunityCooldownID.Bosses;
    }

    public override void AI()
    {
        // Accelerate.
        float newSpeed = Projectile.velocity.Length() + 0.42f;
        Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitY) * newSpeed;
        Projectile.velocity.Y += Cos01(Projectile.identity + Time * 0.022f) * 0.045f;

        // Decide the current rotation.
        Projectile.rotation = (Projectile.position - Projectile.oldPosition).ToRotation();
        Projectile.spriteDirection = Cos(Projectile.rotation).NonZeroSign();
        if (Projectile.spriteDirection == -1)
            Projectile.rotation += Pi;

        if (AvatarOfEmptiness.Myself is not null && Distance(Projectile.Center.X, AvatarOfEmptiness.Myself.Center.X) <= 40f)
            Projectile.Kill();

        // Create gas particles.
        if (Main.rand.NextBool(3))
        {
            PaleAvatarBlobMetaball metaball = ModContent.GetInstance<PaleAvatarBlobMetaball>();
            float gasSize = InverseLerp(-3f, 25f, Time) * Projectile.width * 0.68f;
            float angularOffset = Sin(Time / 5f) * 0.77f;
            metaball.CreateParticle(Projectile.Center + Projectile.velocity * 2f, Main.rand.NextVector2Circular(2f, 2f) + Projectile.velocity.RotatedBy(angularOffset).RotatedByRandom(0.6f) * 0.26f, gasSize);
        }

        Time++;
    }

    public override void OnKill(int timeLeft)
    {
        // Create gas particles.
        PaleAvatarBlobMetaball metaball = ModContent.GetInstance<PaleAvatarBlobMetaball>();
        for (int i = 0; i < 15; i++)
        {
            float gasSize = Projectile.width * Main.rand.NextFloat(0.32f, 1.6f);
            metaball.CreateParticle(Projectile.Center + Projectile.velocity * 2f, Main.rand.NextVector2Circular(8f, 8f), gasSize);
        }
    }

    public override bool PreDraw(ref Color lightColor)
    {
        Color drawColor = Color.White;
        DrawAfterimagesCentered(Projectile, ProjectileID.Sets.TrailingMode[Projectile.type], drawColor, positionClumpInterpolant: 0.56f);
        return false;
    }
}
