using Luminance.Common.DataStructures;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Core.AdvancedProjectileOwnership;
using NoxusBoss.Core.DataStructures;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.NamelessDeity.Projectiles;

public class StarPatternedStarburst : ModProjectile, IPixelatedPrimitiveRenderer, IProjOwnedByBoss<NamelessDeityBoss>
{
    public Vector2 ClosestPlayerHoverDestination
    {
        get;
        set;
    }

    public float RadiusOffset;

    public float ConvergenceAngleOffset;

    public ref float Time => ref Projectile.ai[0];

    public ref float DelayUntilFreeMovement => ref Projectile.ai[1];

    public ref float OffsetAngle => ref Projectile.localAI[0];

    public static int StarPointCount => 6;

    public static float MaxSpeedFactor => 1.05f;

    public override string Texture => ModContent.GetInstance<Starburst>().Texture;

    public override void SetStaticDefaults()
    {
        Main.projFrames[Type] = 6;
        ProjectileID.Sets.TrailingMode[Type] = 2;
        ProjectileID.Sets.TrailCacheLength[Type] = 9;
    }

    public override void SetDefaults()
    {
        Projectile.width = 26;
        Projectile.height = 26;
        Projectile.penetrate = -1;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.hostile = true;
        Projectile.MaxUpdates = 2;
        Projectile.timeLeft = Projectile.MaxUpdates * 210;
    }

    public override void SendExtraAI(BinaryWriter writer) => writer.Write(RadiusOffset);

    public override void ReceiveExtraAI(BinaryReader reader) => RadiusOffset = reader.ReadSingle();

    /// <summary>
    /// A polar equation for a star petal with a given amount of points.
    /// </summary>
    /// <param name="pointCount">The amount of points the star should have.</param>
    /// <param name="angle">The input angle for the polar equation.</param>
    public static Vector2 StarPolarEquation(int pointCount, float angle)
    {
        float spacedAngle = angle;

        // There should be a star point that looks directly upward. However, that isn't the case for odd star counts with the equation below.
        // To address this, a -90 degree rotation is performed.
        if (pointCount % 2 != 0)
            spacedAngle -= PiOver2;

        // Refer to desmos to view the resulting shape this creates. It's basically a black box of trig otherwise.
        float numerator = Cos(Pi * (pointCount + 1f) / pointCount);
        float correctiveFactor = Abs(numerator / Cos(Pi / (pointCount * 2f) + PiOver4));
        float starAdjustedAngle = Asin(Cos(pointCount * spacedAngle)) * 2f;
        float denominator = Cos((starAdjustedAngle + PiOver2 * pointCount) / (pointCount * 2f));
        Vector2 result = angle.ToRotationVector2() * numerator / denominator / correctiveFactor;
        return result;
    }

    public override void AI()
    {
        // No Nameless Deity? Die.
        if (!Projectile.TryGetGenericOwner(out NPC nameless) || nameless.ModNPC is not NamelessDeityBoss namelessModNPC)
        {
            Projectile.Kill();
            return;
        }

        // Release short-lived orange-red sparks.
        if (Main.rand.NextBool(15))
        {
            Color sparkColor = Color.Lerp(Color.Yellow, Color.Red, Main.rand.NextFloat(0.25f, 0.75f));
            sparkColor = Color.Lerp(sparkColor, Color.Wheat, 0.4f);

            Dust spark = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(10f, 10f), 264);
            spark.noLight = true;
            spark.color = sparkColor;
            spark.velocity = Main.rand.NextVector2Circular(10f, 10f);
            spark.noGravity = spark.velocity.Length() >= 3.5f;
            spark.scale = spark.velocity.Length() * 0.1f + 0.64f;
        }

        // Stick in place at first.
        float hoverSnapInterpolant = InverseLerp(11f, 0f, Time - DelayUntilFreeMovement);
        if (hoverSnapInterpolant > 0f)
        {
            float radius = Pow(InverseLerp(0f, 12f, Time), 2.3f) * (RadiusOffset + 700f) + 50f;
            float angle = WrapAngle360(Projectile.velocity.ToRotation());
            float stickInPerfectCircleInterpolant = Sqrt(1f - hoverSnapInterpolant);
            if (stickInPerfectCircleInterpolant >= 0.8f)
                stickInPerfectCircleInterpolant = 1f;

            // Calculate the angle to snap to before moving forward.
            float angleSnapOffset = TwoPi / StarPointCount;
            float snapAngle = Round(angle / angleSnapOffset) * angleSnapOffset;

            Vector2 hoverCenter = Vector2.Lerp(namelessModNPC.CensorPosition, nameless.GetTargetData().Center, InverseLerp(0f, 10f, Time));

            Vector2 starOffset = StarPolarEquation(StarPointCount, angle) * radius;
            Vector2 circleOffset = (snapAngle + Pi / StarPointCount + ConvergenceAngleOffset).ToRotationVector2() * radius * 1.1f;
            Vector2 hoverOffset = Vector2.Lerp(starOffset, circleOffset, stickInPerfectCircleInterpolant);
            Vector2 hoverDestination = hoverCenter + hoverOffset;
            if (stickInPerfectCircleInterpolant >= 0.9f)
            {
                Projectile.Center = hoverDestination;
                ClosestPlayerHoverDestination = hoverCenter;
            }

            ClosestPlayerHoverDestination = Vector2.Lerp(ClosestPlayerHoverDestination, hoverCenter, hoverSnapInterpolant);
            Projectile.Center = Vector2.Lerp(Projectile.Center, hoverDestination, hoverSnapInterpolant);
            Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitY) * 4f;
            OffsetAngle = Projectile.velocity.ToRotation();
        }

        // Collapse inward.
        else
        {
            // Perform the convergence behavior.
            float idealDirection = Projectile.AngleTo(ClosestPlayerHoverDestination);
            Projectile.velocity = Projectile.velocity.ToRotation().AngleLerp(idealDirection, 0.075f).ToRotationVector2() * Projectile.velocity.Length();
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, Projectile.SafeDirectionTo(ClosestPlayerHoverDestination) * 17.5f, 0.017f);

            if (Projectile.WithinRange(ClosestPlayerHoverDestination, 35f))
            {
                if (!AnyProjectiles(ModContent.ProjectileType<ExplodingStar>()))
                {
                    Projectile.NewProjectileBetter_InheritedOwner(Projectile.GetSource_FromThis(), ClosestPlayerHoverDestination, Vector2.Zero, ModContent.ProjectileType<LightWave>(), 0, 0f);
                    Projectile.NewProjectileBetter_InheritedOwner(Projectile.GetSource_FromThis(), ClosestPlayerHoverDestination, Vector2.Zero, ModContent.ProjectileType<ExplodingStar>(), NamelessDeityBoss.ExplodingStarDamage, 0f, -1, 1.112f);

                    int starburstCount = 9;
                    float angularOffset = OffsetAngle + Main.rand.NextFloatDirection() * 0.6f;
                    float starburstSpeed = 2.6f;

                    // GFB? Fuck you.
                    if (Main.zenithWorld)
                    {
                        starburstCount = 23;
                        starburstSpeed = 6.1f;
                    }

                    starburstCount = (int)(starburstCount * namelessModNPC.DifficultyFactor);
                    starburstSpeed *= namelessModNPC.DifficultyFactor;

                    for (int i = 0; i < starburstCount; i++)
                    {
                        Vector2 starburstVelocity = (TwoPi * i / starburstCount + angularOffset).ToRotationVector2() * starburstSpeed;
                        Projectile.NewProjectileBetter_InheritedOwner(Projectile.GetSource_FromThis(), ClosestPlayerHoverDestination, starburstVelocity, ModContent.ProjectileType<Starburst>(), NamelessDeityBoss.StarburstDamage, 0f, -1, 0f, 2f);
                    }
                }

                Projectile.Kill();
            }
        }

        // Fade in and out based on how long the starburst has existed.
        Projectile.Opacity = InverseLerp(0f, 24f, Projectile.timeLeft);
        Projectile.scale = InverseLerp(2f, 18f, Time) * 1.5f;

        // Increment time and update frames.
        if (Projectile.IsFinalExtraUpdate())
            Time++;
        Projectile.frame = (int)Time / 4 % Main.projFrames[Type];
    }

    public float FlameTrailWidthFunction(float completionRatio)
    {
        return SmoothStep(25f, 5f, completionRatio) * Projectile.scale * Projectile.Opacity;
    }

    public Color FlameTrailColorFunction(float completionRatio)
    {
        // Make the trail fade out at the end and fade in sharply at the start, to prevent the trail having a definitive, flat "start".
        float trailOpacity = InverseLerpBump(0f, 0.08f, 0.27f, 0.75f, completionRatio) * 0.9f;

        // Interpolate between a bunch of colors based on the completion ratio.
        Color startingColor = Color.Lerp(Color.White, Color.IndianRed, 0.25f);
        Color middleColor = Color.Lerp(Color.OrangeRed, Color.Yellow, 0.4f);
        Color endColor = Color.Lerp(Color.Purple, Color.Black, 0.35f);

        Palette palette = new Palette(startingColor, middleColor, endColor);
        Color color = palette.SampleColor(completionRatio) * trailOpacity;

        color.A = 0;
        return color * Projectile.Opacity;
    }

    public override bool PreDraw(ref Color lightColor)
    {
        // Draw a bloom flare behind the starburst.
        Starburst.DrawStarburstBloomFlare(Projectile, 0.4f);

        // Draw afterimages that trail closely behind the star core.
        int afterimageCount = 4;
        DrawAfterimagesCentered(Projectile, ProjectileID.Sets.TrailingMode[Type], Color.White with { A = 0 }, 1, afterimageCount, 0.001f, 0.7f);
        return false;
    }

    public void RenderPixelatedPrimitives(SpriteBatch spriteBatch)
    {
        ManagedShader fireTrailShader = ShaderManager.GetShader("NoxusBoss.GenericFlameTrail");
        fireTrailShader.SetTexture(GennedAssets.Textures.TrailStreaks.StreakMagma, 1);

        PrimitiveSettings settings = new PrimitiveSettings(FlameTrailWidthFunction, FlameTrailColorFunction, _ => Projectile.Size * 0.5f, Pixelate: true, Shader: fireTrailShader);
        PrimitiveRenderer.RenderTrail(Projectile.oldPos, settings, 6);
    }

    public override bool ShouldUpdatePosition() => Time >= DelayUntilFreeMovement;

    public override bool? CanDamage() => Time >= DelayUntilFreeMovement + 16f;
}
