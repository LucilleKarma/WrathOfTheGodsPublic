using Luminance.Assets;
using Luminance.Common.DataStructures;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Content.NPCs.Friendly;
using NoxusBoss.Core.Graphics.RenderTargets;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.Draedon.Projectiles;

public class ExoelectricDisintegrationRay : ModProjectile, IProjOwnedByBoss<MarsBody>
{
    private int solynIndex;

    internal static InstancedRequestableTarget LaserbeamTarget;

    /// <summary>
    /// The <see cref="Owner"/> index in the NPC array.
    /// </summary>
    public int OwnerIndex => (int)Projectile.ai[0];

    /// <summary>
    /// The owner of this laserbeam.
    /// </summary>
    public NPC Owner => Main.npc[OwnerIndex];

    /// <summary>
    /// How long this laserbeam has existed, in frames.
    /// </summary>
    public ref float Time => ref Projectile.ai[1];

    /// <summary>
    /// How long this laserbeam currently is.
    /// </summary>
    public ref float LaserbeamLength => ref Projectile.ai[2];

    /// <summary>
    /// How long this laserbeam should exist for, in frames.
    /// </summary>
    public static int Lifetime => MarsBody.CarvedLaserbeam_LaserShootTime;

    /// <summary>
    /// The maximum length of this laserbeam.
    /// </summary>
    public static float MaxLaserbeamLength => 5600f;

    public override string Texture => MiscTexturesRegistry.InvisiblePixelPath;

    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.DrawScreenCheckFluff[Type] = 8000;

        if (Main.netMode != NetmodeID.Server)
        {
            LaserbeamTarget = new InstancedRequestableTarget();
            Main.ContentThatNeedsRenderTargets.Add(LaserbeamTarget);
        }
    }

    public override void SetDefaults()
    {
        Projectile.width = 2;
        Projectile.height = 2;
        Projectile.penetrate = -1;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.hostile = true;
        Projectile.timeLeft = Lifetime;
        CooldownSlot = ImmunityCooldownID.Bosses;
    }

    public override void AI()
    {
        if (OwnerIndex < 0 || OwnerIndex >= Main.maxNPCs || !Owner.active || Owner.type != ModContent.NPCType<MarsBody>())
        {
            Projectile.Kill();
            return;
        }

        solynIndex = NPC.FindFirstNPC(ModContent.NPCType<BattleSolyn>());

        Projectile.Center = Owner.As<MarsBody>().CorePosition;
        Projectile.velocity = Owner.As<MarsBody>().CarvedLaserbeam_LaserbeamDirection.ToRotationVector2();

        LaserbeamLength = Clamp(LaserbeamLength + 172f, 0f, MaxLaserbeamLength);

        ScreenShakeSystem.StartShake(InverseLerp(0f, 20f, Time) * 2f);

        CreateInnerParticles();
        CreateOuterParticles();

        Time++;
    }

    /// <summary>
    /// Creates particles along the deathray's inner boundaries.
    /// </summary>
    public void CreateInnerParticles()
    {
        if (solynIndex == -1)
            return;

        NPC solyn = Main.npc[solynIndex];

        Vector2 perpendicular = Projectile.velocity.SafeNormalize(Vector2.Zero).RotatedBy(PiOver2);
        for (int i = 0; i < 6; i++)
        {
            float energyLengthInterpolant = Main.rand.NextFloat();
            float perpendicularDirection = Main.rand.NextFromList(-1f, 1f);
            Vector2 energySpawnPosition = solyn.Center + Projectile.velocity * Lerp(100f, 3000f, energyLengthInterpolant);
            energySpawnPosition += perpendicular * perpendicularDirection * MarsBody.CarvedLaserbeam_LaserSafeZoneWidth * 0.5f;

            if (!energySpawnPosition.WithinRange(Projectile.Center, LaserbeamLength * 0.95f))
                continue;

            Dust energy = Dust.NewDustPerfect(energySpawnPosition, 264, perpendicular * -perpendicularDirection * Main.rand.NextFloat(2f, 4f));
            energy.noGravity = true;
            energy.color = Color.Wheat;

            if (Main.netMode != NetmodeID.MultiplayerClient && Main.rand.NextBool(7))
            {
                float arcReachInterpolant = Main.rand.NextFloat();
                int arcLifetime = Main.rand.Next(6, 14);
                Vector2 arcOffset = Main.rand.NextVector2Unit() * Lerp(40f, 200f, Pow(arcReachInterpolant, 5f));
                NewProjectileBetter(Projectile.GetSource_FromAI(), energySpawnPosition, arcOffset, ModContent.ProjectileType<SmallTeslaArc>(), 0, 0f, -1, arcLifetime, 1f);
            }
        }
    }

    /// <summary>
    /// Creates particles along the deathray's outer boundaries.
    /// </summary>
    public void CreateOuterParticles()
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
            return;

        Vector2 perpendicular = Projectile.velocity.SafeNormalize(Vector2.Zero).RotatedBy(PiOver2);
        for (int i = 0; i < 6; i++)
        {
            int arcLifetime = Main.rand.Next(6, 14);
            float energyLengthInterpolant = Main.rand.NextFloat();
            float perpendicularDirection = Main.rand.NextFromList(-1f, 1f);
            float arcReachInterpolant = Main.rand.NextFloat();
            Vector2 energySpawnPosition = Projectile.Center + Projectile.velocity * energyLengthInterpolant * LaserbeamLength + perpendicular * LaserWidthFunction(0.5f) * perpendicularDirection * 0.9f;
            Vector2 arcOffset = perpendicular.RotatedBy(1.04f) * Lerp(40f, 320f, Pow(arcReachInterpolant, 4f)) * perpendicularDirection;
            NewProjectileBetter(Projectile.GetSource_FromAI(), energySpawnPosition, arcOffset, ModContent.ProjectileType<SmallTeslaArc>(), 0, 0f, -1, arcLifetime, 1f);
        }
    }

    public float LaserWidthFunction(float completionRatio)
    {
        float initialBulge = Convert01To010(InverseLerp(0.15f, 0.85f, LaserbeamLength / MaxLaserbeamLength)) * InverseLerp(0f, 0.05f, completionRatio) * 32f;
        float idealWidth = initialBulge + Cos(Main.GlobalTimeWrappedHourly * 90f) * 6f + MarsBody.CarvedLaserbeam_LaserWidth;
        float closureInterpolant = InverseLerp(0f, 8f, Lifetime - Time);

        float circularStartInterpolant = InverseLerp(0.05f, 0.012f, completionRatio);
        float circularStart = Sqrt(1.001f - circularStartInterpolant.Squared());

        return Utils.Remap(LaserbeamLength, 0f, MaxLaserbeamLength, 4f, idealWidth) * closureInterpolant * circularStart;
    }

    public float BloomWidthFunction(float completionRatio) => LaserWidthFunction(completionRatio) * 1.9f;

    public Color LaserColorFunction(float completionRatio)
    {
        float lengthOpacity = InverseLerp(0f, 0.45f, LaserbeamLength / MaxLaserbeamLength);
        float startOpacity = InverseLerp(0f, 0.032f, completionRatio);
        float endOpacity = InverseLerp(0.95f, 0.81f, completionRatio);
        float opacity = lengthOpacity * startOpacity * endOpacity;
        Color startingColor = Projectile.GetAlpha(new(255, 40, 55));
        return startingColor * opacity;
    }

    public static Color BloomColorFunction(float completionRatio) => new Color(255, 10, 20) * InverseLerpBump(0.02f, 0.05f, 0.81f, 0.95f, completionRatio) * 0.54f;

    public override bool CanHitPlayer(Player target)
    {
        if (solynIndex == -1)
            return true;

        Vector2 offsetFromSolyn = Main.npc[solynIndex].Center - target.Center;
        Vector2 directionToSolyn = offsetFromSolyn.SafeNormalize(Vector2.Zero);
        float normalizedAngle = directionToSolyn.AngleBetween(-Projectile.velocity) * offsetFromSolyn.Length();

        return normalizedAngle > MarsBody.CarvedLaserbeam_LaserSafeZoneWidth * 0.5f;
    }

    public override bool PreDraw(ref Color lightColor)
    {
        float theMagicFactorThatMakesEveryElectricShineEffectSoMuchBetter = Cos01(Main.GlobalTimeWrappedHourly * 85f);

        List<Vector2> laserPositions = Projectile.GetLaserControlPoints(12, LaserbeamLength);
        laserPositions[0] -= Projectile.velocity * 100f;

        // Prepare the laserbeam render target.
        LaserbeamTarget.Request(Main.screenWidth, Main.screenHeight, Projectile.identity, () =>
        {
            // Draw bloom.
            ManagedShader shader = ShaderManager.GetShader("NoxusBoss.PrimitiveBloomShader");
            shader.TrySetParameter("innerGlowIntensity", 0.45f);
            PrimitiveSettings bloomSettings = new PrimitiveSettings(BloomWidthFunction, BloomColorFunction, _ => Projectile.Size * 0.5f, Shader: shader, UseUnscaledMatrix: true);
            PrimitiveRenderer.RenderTrail(laserPositions, bloomSettings, 70);

            // Draw the beam.
            ManagedShader deathrayShader = ShaderManager.GetShader("NoxusBoss.ExoelectricDisintegrationRayShader");
            deathrayShader.TrySetParameter("centerGlowExponent", 2.9f);
            deathrayShader.TrySetParameter("centerGlowCoefficient", 9.3f);
            deathrayShader.TrySetParameter("edgeGlowIntensity", 0.046f);
            deathrayShader.TrySetParameter("centerDarkeningFactor", 0.6f);
            deathrayShader.TrySetParameter("innerScrollSpeed", 0.85f);
            deathrayShader.TrySetParameter("middleScrollSpeed", 0.5f);
            deathrayShader.TrySetParameter("outerScrollSpeed", 0.2f);
            deathrayShader.SetTexture(DendriticNoiseZoomedOut, 1, SamplerState.LinearWrap);
            deathrayShader.SetTexture(PerlinNoise, 2, SamplerState.LinearWrap);

            PrimitiveSettings laserSettings = new PrimitiveSettings(LaserWidthFunction, LaserColorFunction, _ => Projectile.Size * 0.5f, Shader: deathrayShader, UseUnscaledMatrix: true);
            PrimitiveRenderer.RenderTrail(laserPositions, laserSettings, 120);
        });

        // Draw the laserbeam with Solyn's forcefield parting through it.
        if (LaserbeamTarget.TryGetTarget(Projectile.identity, out RenderTarget2D? target) && target is not null && solynIndex != -1)
        {
            NPC solyn = Main.npc[solynIndex];
            Main.spriteBatch.PrepareForShaders();

            ManagedShader overlayShader = ShaderManager.GetShader("NoxusBoss.TransientSolynForcefieldOverlayShader");
            overlayShader.TrySetParameter("laserDirection", -Projectile.velocity);
            overlayShader.TrySetParameter("zoom", Main.GameViewMatrix.Zoom.X);
            overlayShader.TrySetParameter("safeZoneWidth", MarsBody.CarvedLaserbeam_LaserSafeZoneWidth);
            overlayShader.TrySetParameter("pulse", theMagicFactorThatMakesEveryElectricShineEffectSoMuchBetter * 0.08f);
            overlayShader.TrySetParameter("solynPosition", Vector2.Transform(solyn.Center - Main.screenPosition - Projectile.velocity * 76f, Main.GameViewMatrix.TransformationMatrix));
            overlayShader.Apply();

            Main.spriteBatch.Draw(target, Vector2.Zero, Color.White);

            Main.spriteBatch.ResetToDefault();
        }

        // Draw a superheated lens flare and bloom instance at the center of the beam.
        float shineIntensity = InverseLerp(0f, 12f, Time) * Lerp(1f, 1.2f, theMagicFactorThatMakesEveryElectricShineEffectSoMuchBetter);
        Vector2 drawPosition = Projectile.Center - Main.screenPosition;
        Texture2D glow = BloomCircleSmall.Value;
        Texture2D flare = MiscTexturesRegistry.ShineFlareTexture.Value;

        for (int i = 0; i < 3; i++)
            Main.spriteBatch.Draw(flare, drawPosition, null, Projectile.GetAlpha(Color.Wheat with { A = 0 }), 0f, flare.Size() * 0.5f, shineIntensity * 2f, 0, 0f);
        for (int i = 0; i < 2; i++)
            Main.spriteBatch.Draw(glow, drawPosition, null, Projectile.GetAlpha(Color.White with { A = 0 }), 0f, glow.Size() * 0.5f, shineIntensity * 2f, 0, 0f);

        return false;
    }

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        float _ = 0f;
        float laserWidth = LaserWidthFunction(0.25f) * 1.8f;
        Vector2 start = Projectile.Center;
        Vector2 end = start + Projectile.velocity.SafeNormalize(Vector2.Zero) * LaserbeamLength * 0.95f;
        return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, laserWidth, ref _);
    }

    public override bool ShouldUpdatePosition() => false;
}
