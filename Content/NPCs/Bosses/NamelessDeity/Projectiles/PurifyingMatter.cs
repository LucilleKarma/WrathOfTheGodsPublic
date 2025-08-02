using Luminance.Assets;
using Luminance.Common.DataStructures;
using Microsoft.Xna.Framework;
using NoxusBoss.Content.Particles.Metaballs;
using NoxusBoss.Core.AdvancedProjectileOwnership;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.NamelessDeity.Projectiles;

public class PurifyingMatter : ModProjectile, IProjOwnedByBoss<NamelessDeityBoss>
{
    /// <summary>
    /// The ideal velocity of this matter.
    /// </summary>
    public Vector2 IdealVelocity => Projectile.ai[0].ToRotationVector2() * NamelessDeityBoss.EnterPhase2_AttackPlayer_PurifyingMatterMaxSpeedFactor * 30f;

    /// <summary>
    /// How long this matter has exist for so far, in frames.
    /// </summary>
    public ref float Time => ref Projectile.ai[1];

    /// <summary>
    /// Whether this purifying matter is being sucked in or not.
    /// </summary>
    public bool SuckingIn
    {
        get => Projectile.ai[2] == 1f;
        set => Projectile.ai[2] = value.ToInt();
    }

    public override string Texture => MiscTexturesRegistry.InvisiblePixelPath;

    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailingMode[Type] = 2;
        ProjectileID.Sets.TrailCacheLength[Type] = 28;
    }

    public override void SetDefaults()
    {
        Projectile.width = 51;
        Projectile.height = 51;
        Projectile.penetrate = -1;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.hostile = true;
        Projectile.hide = true;
        Projectile.timeLeft = 600;
    }

    public override void AI()
    {
        // Die if Nameless is not present.
        if (!Projectile.TryGetGenericOwner(out NPC nameless))
        {
            Projectile.Kill();
            return;
        }

        if (SuckingIn)
        {
            List<Projectile> dreamcatchers = AllProjectilesByID(ModContent.ProjectileType<CelestialDreamcatcher>()).ToList();
            if (dreamcatchers.Count <= 0)
            {
                Projectile.Kill();
                return;
            }

            Projectile dreamcatcher = dreamcatchers.First();
            Vector2 destination = dreamcatcher.Center;
            float flySpeed = SmoothStep(21f, 39f, InverseLerp(250f, 60f, Projectile.Distance(destination))) * NamelessDeityBoss.EnterPhase2_AttackPlayer_PurifyingMatterMaxSpeedFactor;

            Projectile.velocity = Vector2.Lerp(Projectile.velocity, Projectile.SafeDirectionTo(destination) * flySpeed, 0.6f);
            if (Projectile.WithinRange(destination, 60f) || dreamcatcher.Opacity <= 0.4f)
                Projectile.Kill();
        }
        else
        {
            // Accelerate.
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, IdealVelocity, InverseLerp(10f, 60f, Time) * NamelessDeityBoss.EnterPhase2_AttackPlayer_PurifyingMatterAcceleration);
        }

        ModContent.GetInstance<MistMetaball>().CreateParticle(Projectile.Center, Main.rand.NextVector2Circular(5f, 5f), Projectile.Size.X * 2.8f, Main.rand.Next(4));

        Time++;
    }
}
