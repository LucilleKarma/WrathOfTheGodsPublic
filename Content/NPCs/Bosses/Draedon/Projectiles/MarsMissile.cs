using Luminance.Common.DataStructures;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Content.NPCs.Bosses.Draedon.Projectiles.SolynProjectiles;
using NoxusBoss.Content.NPCs.Bosses.Draedon.Rendering;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity.SpecificEffectManagers;
using NoxusBoss.Content.Particles;
using NoxusBoss.Core.DataStructures;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.Draedon.Projectiles;

public class MarsMissile : ModProjectile, IProjOwnedByBoss<MarsBody>, IPixelatedPrimitiveRenderer, INotResistedByMars
{
    public bool SetActiveFalseInsteadOfKill => false;

    /// <summary>
    /// The moving average for the smart AI which dictates how close the missile is to colliding into a forcefield.
    /// </summary>
    public float AboutToCollideInterpolantMovingAverage
    {
        get;
        set;
    }

    /// <summary>
    /// The amount by which this missile's acceleration factor should be boosted.
    /// </summary>
    public ref float AccelerationBoost => ref Projectile.ai[0];

    /// <summary>
    /// The amount by which this missile's max speed.
    /// </summary>
    public ref float MaxSpeedBoost => ref Projectile.ai[1];

    /// <summary>
    /// How long this missile has existed, in frames.
    /// </summary>
    public ref float Time => ref Projectile.ai[2];

    /// <summary>
    /// A 0-1 interpolant which dictates the visuals that indicate that this missile is friendly.
    /// </summary>
    public ref float FriendlinessVisualsInterpolant => ref Projectile.localAI[0];

    /// <summary>
    /// The base maximum speed that this missile can reach. Does not account for <see cref="MaxSpeedBoost"/>.
    /// </summary>
    public static float MaxSpeedup => 24f;

    public override string Texture => GetAssetPath("Content/NPCs/Bosses/Draedon/Projectiles", Name);

    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailingMode[Type] = 2;
        ProjectileID.Sets.TrailCacheLength[Type] = 10;
    }

    public override void SetDefaults()
    {
        Projectile.width = 54;
        Projectile.height = 54;
        Projectile.penetrate = -1;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.hostile = true;
        Projectile.timeLeft = 600;
        Projectile.hide = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 1;
        Projectile.DamageType = DamageClass.Generic;
        CooldownSlot = ImmunityCooldownID.Bosses;
    }

    public override void AI()
    {
        if (Projectile.hostile)
            DoHostileAI();
        else
        {
            NPC? target = Projectile.FindTargetWithinRange(1850f, true);
            if (target is not null)
            {
                Projectile.velocity += Projectile.SafeDirectionTo(target.Center) * 4.25f;
                Projectile.velocity = Projectile.velocity.RotateTowards(Projectile.AngleTo(target.Center), 0.06f);
            }
        }

        Projectile.tileCollide = Time >= 60f;
        Projectile.rotation = Projectile.velocity.ToRotation() + PiOver2;
        Projectile.scale = InverseLerp(0f, 10f, Time);

        FriendlinessVisualsInterpolant = Saturate(FriendlinessVisualsInterpolant + Projectile.friendly.ToDirectionInt() * 0.1f);

        Time++;
    }

    /// <summary>
    /// Handles this missile's hostile AI, before it's reflected.
    /// </summary>
    public void DoHostileAI()
    {
        Player target = Main.player[Player.FindClosest(Projectile.Center, 1, 1)];
        if (Projectile.WithinRange(target.Center, Projectile.velocity.Length() + 35f))
            Projectile.Kill();

        if (Time <= 180f)
        {
            if (Projectile.identity % 5 == 0)
                DoHostileAI_Smart(target);
            else
                DoHostileAI_Standard(target);
        }
        else if (Projectile.velocity.Length() < 34f)
            Projectile.velocity *= 1.014f;

        if (Time >= 27f)
        {
            int forcefieldID = ModContent.ProjectileType<DirectionalSolynForcefield>();
            foreach (Projectile projectile in Main.ActiveProjectiles)
            {
                if (projectile.type == forcefieldID && projectile.Opacity >= 0.6f)
                {
                    bool hitboxCollision = projectile.Colliding(projectile.Hitbox, Projectile.Hitbox);
                    bool reasonableIncomingAngle = projectile.velocity.AngleBetween(-Projectile.velocity) <= 0.93f;

                    // Don't let the player reflect missiles by just spinning the shield rapidly.
                    float spinMovingAverage = projectile.As<DirectionalSolynForcefield>().SpinSpeedMovingAverage;
                    bool tryingToCheese = spinMovingAverage >= ToRadians(13f);

                    if (hitboxCollision && reasonableIncomingAngle && !tryingToCheese)
                    {
                        SoundEngine.PlaySound(GennedAssets.Sounds.Solyn.ForcefieldHit with { MaxInstances = 0 }, Projectile.Center).WithVolumeBoost(1.45f);
                        Projectile.velocity = Projectile.velocity * -1.2f + Main.player[projectile.owner].velocity;
                        Projectile.hostile = false;
                        Projectile.friendly = true;
                        Projectile.penetrate = 1;
                        break;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Handles this missile's standard hostile AI, before it's reflected.
    /// </summary>
    public void DoHostileAI_Standard(Player target)
    {
        float homeInAcceleration = InverseLerp(90f, 30f, Time) * 1.4f;
        Vector2 aimDestination = target.Center + (Projectile.identity / 3f).ToRotationVector2() * (Projectile.identity / 7f % 1f * 200f);

        if (Time <= 30f && Projectile.velocity.Length() > MaxSpeedup + MaxSpeedBoost)
            Projectile.velocity *= 0.93f;
        else
        {
            float acceleration = AccelerationBoost + 1.0083f;

            Vector2 idealVelocity = (Projectile.velocity * acceleration + Projectile.SafeDirectionTo(aimDestination) * homeInAcceleration).ClampLength(0f, MaxSpeedup + MaxSpeedBoost);
            idealVelocity = idealVelocity.RotateTowards(Projectile.AngleTo(target.Center), 0.0039f);

            Projectile.velocity = Vector2.Lerp(Projectile.velocity, idealVelocity, 0.25f);
        }
    }

    /// <summary>
    /// Handles this missile's smart hostile AI, before it's reflected.
    /// </summary>
    public void DoHostileAI_Smart(Player target)
    {
        float homeInAcceleration = InverseLerp(180f, 60f, Time) * 3f;
        Vector2 aimDestination = target.Center;
        float distanceToTarget = Projectile.Distance(aimDestination);
        float speedUpFactor = AccelerationBoost + 1.01f;

        if (Time <= 30f && Projectile.velocity.Length() > MaxSpeedup + MaxSpeedBoost)
            Projectile.velocity *= 0.93f;
        else
        {
            // Calculate the impact point on the nearest forcefield, assuming there is one, for this missile.
            Vector2 forcefieldImpactPoint = MarsBody.AttemptForcefieldIntersection(Projectile.Center, Projectile.Center + Projectile.velocity.SafeNormalize(Vector2.UnitY) * 2000f, 2.5f);

            // Calculate, as a 0-1 interpolant, how close the missile is to colliding with the impact point on the forcefield and explode.
            // This tapers off if the missile is REALLY close to a player, at which point it is assumed that the missile is close enough for the player to not be able to deflect it.
            float forcefieldImpactDistance = Projectile.Distance(forcefieldImpactPoint);
            float aboutToCollideInterpolant = InverseLerp(600f, 450f, forcefieldImpactDistance) * InverseLerp(100f, 200f, distanceToTarget);

            // If an impact point was found, but it's further away than the target is, that means the missile is behind the player, and as such the about-to-collide interpolant should be zeroed out.
            bool wouldHitFromBehind = Projectile.Distance(aimDestination) < forcefieldImpactDistance;
            if (wouldHitFromBehind)
                aboutToCollideInterpolant = 0f;

            // Use a moving average based on the aforementioned about-to-collide interpolant, to ensure that the calculations below are smooth and work as the missile's orientation moves.
            // If this isn't done, the about-to-collide interpolant will shift down to 0 as it moves past the forcefield, since naturally if it's moved away from it it's not going to be colliding into
            // it anymore.
            AboutToCollideInterpolantMovingAverage = Lerp(AboutToCollideInterpolantMovingAverage, aboutToCollideInterpolant, 0.05f);

            // Use the moving average to calculate an angular discrepancy.
            // If it's at 1, then the missile will attempt to move perpendicular to the player in an attempt to fly over the forcefield before getting an opportunity to
            // hit them from behind.
            Vector2 force = Projectile.SafeDirectionTo(aimDestination).RotatedBy(AboutToCollideInterpolantMovingAverage * -PiOver2) * homeInAcceleration;

            Vector2 idealVelocity = (Projectile.velocity * speedUpFactor + force).ClampLength(0f, MaxSpeedup + MaxSpeedBoost);
            idealVelocity = idealVelocity.RotateTowards(force.ToRotation(), 0.09f);

            Projectile.velocity = Vector2.Lerp(Projectile.velocity, idealVelocity, 0.23f);
        }
    }

    public override void OnKill(int timeLeft)
    {
        if (Main.netMode == NetmodeID.Server)
            return;

        ScreenShakeSystem.StartShakeAtPoint(Projectile.Center, 5f);
        SoundEngine.PlaySound(GennedAssets.Sounds.Mars.MissileExplode with { MaxInstances = 4 }, Projectile.Center).WithVolumeBoost(1.5f);

        Vector2 blastDirection = -Projectile.velocity.SafeNormalize(Vector2.UnitY);

        for (int i = 0; i < 54; i++)
        {
            Color fireColor = Color.Lerp(Color.OrangeRed, Color.Yellow, Main.rand.NextFloat(0.56f)) * Main.rand.NextFloat(0.12f, 0.4f);
            Vector2 fireVelocity = blastDirection.RotatedByRandom(0.54f).SafeNormalize(Vector2.UnitY) * Main.rand.NextFloat(8f, 58f);
            SmallSmokeParticle fire = new SmallSmokeParticle(Projectile.Center, fireVelocity, fireColor, Color.LightGoldenrodYellow, Main.rand.NextFloat(0.5f, 1f), 130f);
            fire.Spawn();
        }

        for (int i = 0; i < 20; i++)
        {
            Dust fire = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(30f, 30f), 6);
            fire.fadeIn = 1f;
            fire.noGravity = true;
            fire.velocity = Main.rand.NextVector2Circular(10f, 10f);
            fire.scale = 0.6f;
        }

        for (int i = 0; i < 16; i++)
        {
            Vector2 fireSpawnPosition = Projectile.Center;
            Vector2 fireVelocity = Main.rand.NextVector2Circular(28f, 28f) + blastDirection.RotatedByRandom(0.4f) * 40f;
            Color fireColor = new Color(255, 150, 3).HueShift(Main.rand.NextFloatDirection() * 0.036f);
            fireColor = fireColor.MultiplyRGB(Color.White * Main.rand.NextFloat(0.6f, 1f));

            NamelessFireParticleSystemManager.ParticleSystem.CreateNew(fireSpawnPosition, fireVelocity, Vector2.One * Main.rand.NextFloat(70f, 150f), fireColor);
        }

        StrongBloom bloom = new StrongBloom(Projectile.Center, blastDirection * 8f, Color.OrangeRed, 1.5f, 19);
        bloom.Spawn();
        bloom = new(Projectile.Center, blastDirection * 8f, Color.Yellow, 1.3f, 13);
        bloom.Spawn();
        bloom = new(Projectile.Center, blastDirection * 8f, Color.Wheat, 0.85f, 9);
        bloom.Spawn();
    }

    public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
    {
        modifiers.FinalDamage *= 20f;

        if (target.ModNPC is MarsBody mars)
        {
            target.velocity += Projectile.velocity.SafeNormalize(Vector2.Zero) * 2f;
            target.netUpdate = true;

            mars.BurnMarks.Add(new BurnMarkImpact()
            {
                Seed = Main.rand.NextFloat(),
                Lifetime = Main.rand.Next(120, 180),
                RelativePosition = Projectile.Center - target.Center,
                Scale = Main.rand.NextFloat(0.7f, 1.56f)
            });
        }
    }

    public override bool PreDraw(ref Color lightColor)
    {
        lightColor = Color.Lerp(lightColor, Color.White, 0.75f);

        Texture2D texture = TextureAssets.Projectile[Type].Value;
        Texture2D glowTexture = GennedAssets.Textures.Projectiles.MarsMissileGlow;
        Vector2 drawPosition = Projectile.Center - Main.screenPosition;
        float glow = InverseLerp(10f, 30f, Projectile.velocity.Length());
        Color glowColor = (Color.White with { A = 0 }) * Lerp(0.4f, 1f, Cos01(Main.GlobalTimeWrappedHourly * -16f + Projectile.identity * 2f)) * glow;
        Main.spriteBatch.Draw(texture, drawPosition, null, Projectile.GetAlpha(lightColor), Projectile.rotation, texture.Size() * 0.5f, Projectile.scale, 0, 0f);
        Main.spriteBatch.Draw(glowTexture, drawPosition, null, Projectile.GetAlpha(glowColor), Projectile.rotation, texture.Size() * 0.5f, Projectile.scale, 0, 0f);

        return false;
    }

    public float FlameTrailWidthFunction(float completionRatio, float widthFactor)
    {
        float squish = Utils.Remap(Projectile.velocity.Length(), 20f, 54f, 1f, 0.25f);
        float baseWidth = SmoothStep(30f, 2f, completionRatio);
        squish *= Lerp(0.7f, 1f, Cos01(Main.GlobalTimeWrappedHourly * -16f + Projectile.identity * 2f + completionRatio * TwoPi * 2f));
        squish *= InverseLerp(0.2f, 0.4f, completionRatio);
        return baseWidth * widthFactor * squish;
    }

    public Color FlameTrailColorFunction(float completionRatio, float opacityFactor)
    {
        float trailOpacity = InverseLerp(0.8f, 0.27f, completionRatio) * InverseLerp(0.05f, 0.2f, completionRatio);
        Color startingColor = Color.Lerp(Color.SkyBlue, Color.White, 0.6f);
        Color middleColor = Color.Lerp(Color.Red, Color.Yellow, 0.32f);
        Color endColor = Color.Lerp(Color.Orange, Color.Red, 0.7f);

        startingColor = Color.Lerp(startingColor, Color.Wheat, FriendlinessVisualsInterpolant);
        middleColor = Color.Lerp(middleColor, Color.DeepSkyBlue, FriendlinessVisualsInterpolant);
        endColor = Color.Lerp(endColor, Color.BlueViolet, FriendlinessVisualsInterpolant * 0.9f);

        Palette flameTrailPalette = new Palette(startingColor, middleColor, endColor);
        return flameTrailPalette.SampleColor(completionRatio) * trailOpacity * opacityFactor;
    }

    public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs, List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI)
    {
        overPlayers.Add(index);
    }

    public void RenderPixelatedPrimitives(SpriteBatch spriteBatch)
    {
        ManagedShader trailShader = ShaderManager.GetShader("NoxusBoss.MissileFlameTrailShader");
        trailShader.Apply();

        PrimitiveSettings settings = new PrimitiveSettings(c => FlameTrailWidthFunction(c, 1f), c => FlameTrailColorFunction(c, 1f), _ => Projectile.Size * 0.5f, Pixelate: true, Shader: trailShader);
        PrimitiveRenderer.RenderTrail(Projectile.oldPos, settings, 15);
    }

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) =>
        Projectile.RotatingHitboxCollision(targetHitbox.TopLeft(), targetHitbox.Size());
}
