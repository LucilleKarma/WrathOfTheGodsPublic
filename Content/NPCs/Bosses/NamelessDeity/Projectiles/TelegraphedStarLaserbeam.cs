using Luminance.Common.DataStructures;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity.SpecificEffectManagers;
using NoxusBoss.Core.AdvancedProjectileOwnership;
using NoxusBoss.Core.BaseEntities;
using NoxusBoss.Core.Graphics.Automators;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.NamelessDeity.Projectiles;

public class TelegraphedStarLaserbeam : BaseTelegraphedPrimitiveLaserbeam, IDrawsWithShader, IProjOwnedByBoss<NamelessDeityBoss>
{
    public float TelegraphSizeInterpolant => Sin01(Projectile.identity * 13f + Main.GlobalTimeWrappedHourly * 1.4f);

    public ref float MaxSpinAngularVelocity => ref Projectile.ai[2];

    // This laser should be drawn in the DrawWithShader interface, and as such should not be drawn manually via the base projectile.
    public override bool UseStandardDrawing => false;

    // This is used by the IDrawsWithShader to ensure that this projectile's drawing via DrawWithShader is performed under the Additive blend state.
    public bool ShaderShouldDrawAdditively => true;

    public override int TelegraphPointCount => 47;

    public override int LaserPointCount => 14;

    public override float MaxLaserLength => 3300f;

    public override float LaserExtendSpeedInterpolant => 0.07f;

    public override ManagedShader TelegraphShader => ShaderManager.GetShader("NoxusBoss.SunLaserTelegraphShader");

    public override ManagedShader LaserShader => ShaderManager.GetShader("NoxusBoss.NamelessDeityStarLaserShader");

    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailingMode[Type] = 2;
        ProjectileID.Sets.TrailCacheLength[Type] = 20;
        ProjectileID.Sets.DrawScreenCheckFluff[Type] = (int)MaxLaserLength + 20;
    }

    public override void SetDefaults()
    {
        Projectile.width = 138;
        Projectile.height = 138;
        Projectile.penetrate = -1;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.hostile = true;
        Projectile.timeLeft = 60000;
    }

    public override void SendExtraAI(BinaryWriter writer)
    {
        writer.Write(LaserLengthFactor);
    }

    public override void ReceiveExtraAI(BinaryReader reader)
    {
        LaserLengthFactor = reader.ReadSingle();
    }

    // This uses PreAI instead of PostAI to ensure that the AI hook uses the correct, updated velocity when deciding Projectile.rotation.
    public override bool PreAI()
    {
        // Stick to a star if possible. If there is no star, die immediately.
        List<Projectile> stars = AllProjectilesByID(ModContent.ProjectileType<ControlledStar>()).ToList();
        if (stars.Count != 0)
            Projectile.Center = stars.First().Center + Projectile.velocity * 280f;
        else
        {
            Projectile.Kill();
            return false;
        }

        // Be briefly invisible at first.
        Projectile.hide = Time <= 1f;

        // This is calculated as necessary to ensure that a turn of exactly MaxSpinAngularVelocity * TelegraphTime is achieved
        // during the telegraph. This will use a turning factor based on the Convert01To010 function, or sin(pi * x)^p
        // In order to determine this, it is useful to frame the problem in terms of a series of discrete steps, because
        // the entire process is a series of discrete angular updates across a discrete number of frames.

        // In this case, N is the number of discrete steps being performed. For the sake of example, it will be assumed to be 6. p will be assumed to be 4.

        // Frame zero would be an angular offset of sin(pi * 0 / 6)^4, or 0.
        // Frame one would be sin(pi * 1 / 6)^4, or about 0.0625.
        // Frame two would be sin(pi * 2 / 6)^4, or about 0.5625.
        // Frame three would be sin(pi * 3 / 6)^4, or about 1.
        // Frame four would be sin(pi * 4 / 6)^4, or about 0.5625.
        // Frame five would be sin(pi * 5 / 6)^4, or about 0.0625.
        // And frame six would be sin(pi * 6 / 6)^4, or 0 again.

        // Adding this together results in a total offset of about 1.249. This is obviously quite a bit of turning for just five frames.
        // Naturally, it would make sense to "normalize" the offsets by dividing each step by 6, resulting in a total of around 0.622 instead.
        // Now, ideally this value would be 1, because if that were the case that'd mean simple multiplication would allow specification of how much
        // angular change happens across the entire process.

        // In order to achieve this, we must figure out what the 0.622 value approaches as N gets bigger and bigger and we slice the entire process into more and more frames.
        // This is exactly what the purpose of a definite integral is, interestingly! In order to figure out what that 0.622 approaches as N approaches infinity, we can do some
        // calculus to find the exact value, like so:

        // ∫(0, 1) sin(pi * x)^p * dx =
        // 1 / pi * ∫(0, pi) sin(x)^p =
        // 0.375 for p = 4
        // In order to make the entire process add up neatly to MaxSpinAngularVelocity * TelegraphTime, a correction factor of 1 / 0.375 will be necessary.
        float spinInterpolant = InverseLerp(0f, TelegraphTime, Time);
        float angularVelocity = Pow(Convert01To010(spinInterpolant), 4f) * MaxSpinAngularVelocity / 0.375f;
        Projectile.velocity = Projectile.velocity.RotatedBy(angularVelocity);

        // Fade out when the laser is about to die.
        Projectile.Opacity = InverseLerp(TelegraphTime + LaserShootTime - 1f, TelegraphTime + LaserShootTime - 12f, Time);

        if (LaserLengthFactor < 0.16f)
            LaserLengthFactor = 0.16f;

        return true;
    }

    public override void OnLaserFire()
    {
        // Inform Nameless that the laser has fired.
        if (!Projectile.TryGetGenericOwner(out NPC nameless))
            return;

        if (nameless is not null)
            nameless.ai[3] = 1f;

        // Shake the screen.
        if (ScreenShakeSystem.OverallShakeIntensity <= 7.5f)
            ScreenShakeSystem.StartShakeAtPoint(Projectile.Center, 4f);

        // Play firing sounds.
        SoundEngine.PlaySound(GennedAssets.Sounds.NamelessDeity.SunBeamShoot with
        {
            Volume = 1.05f,
            MaxInstances = 1,
            SoundLimitBehavior = SoundLimitBehavior.ReplaceOldest,
            PitchVariance = 0.12f
        });

        if (Main.netMode != NetmodeID.Server)
        {
            for (int i = 0; i < 35; i++)
            {
                float arcInterpolant = Main.rand.NextFloat();
                Vector2 fireVelocity = Projectile.velocity.RotatedBy(Main.rand.NextFromList(-1f, 1f) * arcInterpolant * 0.44f) * Lerp(120f, 31f, arcInterpolant) * Main.rand.NextFloat(0.6f, 1.5f);
                fireVelocity += Main.rand.NextVector2Circular(15f, 15f);

                float fireSize = SmoothStep(70f, 280f, arcInterpolant);
                NamelessFireParticleSystemManager.ParticleSystem.CreateNew(Projectile.Center, fireVelocity, new Vector2(Main.rand.NextFloat(0.5f, 1f), 1f) * fireSize, new Color(255, 150, 0));
            }
        }
    }

    public override float TelegraphWidthFunction(float completionRatio)
    {
        float telegraphCompletion = InverseLerp(0f, TelegraphTime, Time);
        float scaleFactor = Utils.Remap(telegraphCompletion, 0.7f, 0.94f, 1f, 2.55f) * Lerp(0.6f, 1f, TelegraphSizeInterpolant);
        return Projectile.Opacity * Projectile.width * scaleFactor;
    }

    public override Color TelegraphColorFunction(float completionRatio)
    {
        float telegraphCompletion = InverseLerp(0f, TelegraphTime, Time);
        float timeBasedOpacity = Utils.Remap(telegraphCompletion, 0.6f, 0.9f, 0.55f, 0.8f);
        float endFadeOpacity = InverseLerpBump(0f, 0.15f, 0.67f, 1f, completionRatio);
        return Color.LightGoldenrodYellow * endFadeOpacity * Projectile.Opacity * timeBasedOpacity;
    }

    public override float LaserWidthFunction(float completionRatio) => Projectile.Opacity * Projectile.width;

    public override Color LaserColorFunction(float completionRatio) => Color.OrangeRed * InverseLerp(LaserShootTime - 1f, LaserShootTime - 8f, Time - TelegraphTime) * Projectile.Opacity;

    public override void PrepareTelegraphShader(ManagedShader telegraphShader)
    {
        float telegraphCompletion = InverseLerp(0f, TelegraphTime, Time);
        float startThreshold = Utils.Remap(telegraphCompletion, 0.92f, 1f, 0f, 0.074f);
        float endThreshold = Utils.Remap(telegraphCompletion, 0f, 0.3f, 0.03f, 0.67f);

        telegraphShader.TrySetParameter("generalOpacity", Projectile.Opacity);
        telegraphShader.TrySetParameter("verticalCutoffStartThreshold", startThreshold);
        telegraphShader.TrySetParameter("verticalCutoffEndThreshold", endThreshold);
        telegraphShader.TrySetParameter("highlightScrollOffset", Vector2.UnitX * Projectile.identity * 0.148f);
        telegraphShader.TrySetParameter("brightnessNoiseScrollOffset", Vector2.UnitX * Projectile.identity * 0.205f);
        telegraphShader.SetTexture(DendriticNoiseZoomedOut, 1, SamplerState.LinearWrap);
        telegraphShader.SetTexture(DendriticNoise, 2, SamplerState.LinearWrap);
    }

    public override void PrepareLaserShader(ManagedShader laserShader)
    {
        laserShader.TrySetParameter("uStretchReverseFactor", LaserLengthFactor * 0.7f);
        laserShader.SetTexture(WavyBlotchNoise, 1, SamplerState.LinearWrap);
    }

    public override List<Vector2> GenerateTelegraphControlPoints()
    {
        float telegraphCompletion = InverseLerp(0f, TelegraphTime, Time);
        float telegraphLengthFactor = MaxLaserLength * (InverseLerp(0.9f, 1f, telegraphCompletion) * LaserLengthFactor + 0.135f) * Lerp(0.64f, 1.1f, TelegraphSizeInterpolant) * 1.25f;

        telegraphLengthFactor *= Lerp(1f, 0.6f, Convert01To010(InverseLerp(0.6f, 0.97f, telegraphCompletion)));

        List<Vector2> controlPoints = [];

        for (int i = 0; i < 8; i++)
        {
            float laserDistance = i / 7f * telegraphLengthFactor;
            Vector2 laserDirection = Projectile.oldRot[i].ToRotationVector2();
            controlPoints.Add(Projectile.Center + laserDirection * laserDistance);
        }

        return controlPoints;
    }

    public void DrawWithShader(SpriteBatch spriteBatch)
    {
        if (Time <= 3f)
            return;

        // Draw the regular telegraph/laser stuff.
        DrawTelegraphOrLaser(false);
    }
}
