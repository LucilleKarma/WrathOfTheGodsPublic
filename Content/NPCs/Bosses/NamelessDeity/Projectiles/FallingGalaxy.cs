using Luminance.Assets;
using Luminance.Common.DataStructures;
using Luminance.Common.Easings;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity.SpecificEffectManagers;
using NoxusBoss.Content.Particles;
using NoxusBoss.Core.AdvancedProjectileOwnership;
using NoxusBoss.Core.Graphics.Automators;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.NamelessDeity.Projectiles;

public class FallingGalaxy : ModProjectile, IDrawsWithShader, IProjOwnedByBoss<NamelessDeityBoss>
{
    public bool SetActiveFalseInsteadOfKill => true;

    public ref float Time => ref Projectile.ai[0];

    public ref float Hue => ref Projectile.localAI[0];

    public ref float SpinRotation => ref Projectile.localAI[1];

    public static int TelegraphTime => 50;

    public static int Lifetime => 180;

    public override string Texture => MiscTexturesRegistry.InvisiblePixelPath;

    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailingMode[Type] = 2;
        ProjectileID.Sets.TrailCacheLength[Type] = 8;
        ProjectileID.Sets.DrawScreenCheckFluff[Type] = 5000;
    }

    public override void SetDefaults()
    {
        Projectile.width = 150;
        Projectile.height = 150;
        Projectile.penetrate = -1;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.hostile = true;
        Projectile.timeLeft = Lifetime;
        Projectile.hide = false;
        Projectile.scale = Main.rand?.NextFloat(0.5f, 2f) ?? 1f;
    }

    public override void SendExtraAI(BinaryWriter writer) => writer.Write(Projectile.scale);

    public override void ReceiveExtraAI(BinaryReader reader) => Projectile.scale = reader.ReadSingle();

    public override void AI()
    {
        // Decide a hue and rotation.
        if (Hue == 0f)
        {
            Hue = Main.rand.NextFloat(0.001f, 1f);
            Projectile.rotation = Main.rand.NextFloat(TwoPi);
        }

        // No Nameless Deity? Cease.
        if (!Projectile.TryGetGenericOwner(out NPC nameless))
        {
            Projectile.Kill();
            return;
        }

        NPCAimedTarget target = nameless.GetTargetData();

        // Stay above the target while telegraphing.
        if (Time < TelegraphTime)
            Projectile.position.Y = target.Center.Y - 1020f;

        // Accelerate and release stardust if no longer telegraphing.
        else
        {
            // Release galaxy fall sounds.
            if (Time % 12f == 11f)
                SoundEngine.PlaySound(GennedAssets.Sounds.NamelessDeity.GalaxyFallGlitch with { MaxInstances = 20, Volume = 0.56f }, Projectile.Center);

            float maxSpeed = 80f;
            Projectile.velocity = Vector2.Lerp(Projectile.velocity * 1.03f, Vector2.UnitY * maxSpeed, 0.04f).ClampLength(0f, maxSpeed);

            // Make the sword emit stardust as it's being fired.
            float stardustSpawnRate = InverseLerp(24f, 60f, Projectile.velocity.Length()) * 0.3f;
            for (int i = 0; i < 7; i++)
            {
                if (Main.rand.NextFloat() >= stardustSpawnRate)
                    continue;

                int starPoints = Main.rand.Next(3, 9);
                float starScaleInterpolant = Main.rand.NextFloat();
                int starLifetime = (int)Lerp(20f, 54f, starScaleInterpolant);
                float starScale = Lerp(0.4f, 0.8f, starScaleInterpolant) * Projectile.scale;
                Color starColor = Main.hslToRgb((Hue + Main.rand.NextFloat(0.4f)) % 1f, 0.9f, 0.64f);
                starColor = Color.Lerp(starColor, Color.Wheat, 0.4f) * 0.2f;

                Vector2 starVelocity = Projectile.velocity.SafeNormalize(Vector2.UnitY).RotatedBy(PiOver2).RotatedByRandom(0.9f) * Main.rand.NextFloatDirection() * 30f;
                TwinkleParticle star = new TwinkleParticle(Projectile.Center, starVelocity, starColor, starLifetime, starPoints, new Vector2(Main.rand.NextFloat(0.4f, 1.6f), 1f) * starScale, starColor * 0.5f);
                star.Spawn();
            }
        }

        // Collide with tiles if ready.
        Projectile.tileCollide = Projectile.Bottom.Y >= target.Center.Y + (Main.zenithWorld ? 200f : -120f);

        // SPEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEN!
        SpinRotation += Projectile.velocity.Y * Sin(Projectile.rotation).NonZeroSign() * 0.01f;
        Projectile.rotation += Projectile.velocity.Y * 0.000176f;

        // Increment the time.
        Time++;
    }

    public override void OnKill(int timeLeft)
    {
        // Shove everything down a bit.
        Projectile.Center += Vector2.UnitY * Projectile.scale * 100f;

        // Play an explosion sound.
        float explosionPitch = Utils.Remap(Projectile.scale, 0.5f, 2f, -0.6f, 0.25f);
        SoundEngine.PlaySound(GennedAssets.Sounds.NamelessDeity.GalaxyImpact with { Volume = 1.4f, MaxInstances = 15 }, Projectile.Center);

        // Create a bunch of stars.
        for (int i = 0; i < 35; i++)
        {
            int starPoints = Main.rand.Next(3, 9);
            float starScaleInterpolant = Main.rand.NextFloat();
            int starLifetime = (int)Lerp(30f, 67f, starScaleInterpolant);
            float starScale = Lerp(0.75f, 1.2f, starScaleInterpolant) * Projectile.scale;
            Color starColor = Main.hslToRgb((Hue + Main.rand.NextFloat(0.4f)) % 1f, 0.9f, 0.64f);
            starColor = Color.Lerp(starColor, Color.Wheat, 0.4f) * 0.4f;

            // Calculate the star velocity.
            // If it happens to point downward into the ground, fly upward at a fast speed instead, like a geyser.
            Vector2 starVelocity = Main.rand.NextVector2Circular(60f, 15f);
            if (starVelocity.Y > 0f)
            {
                starVelocity.X *= 0.5f;
                starVelocity.Y = -starVelocity.Y - Main.rand.NextFloat(40f);
            }

            TwinkleParticle star = new TwinkleParticle(Projectile.Center, starVelocity, starColor, starLifetime, starPoints, new Vector2(Main.rand.NextFloat(0.4f, 1.6f), 1f) * starScale, starColor * 0.5f);
            star.Spawn();
        }

        // Create particles.
        ExpandingGreyscaleCircleParticle circle = new ExpandingGreyscaleCircleParticle(Projectile.Center, Vector2.Zero, Main.hslToRgb((Hue + Main.rand.NextFloat(0.2f)) % 1f, 0.95f, 0.67f), 10, 0.28f);
        MagicBurstParticle magicBurst = new MagicBurstParticle(Projectile.Center, Vector2.Zero, Color.Wheat, 12, 1f, 0.04f);
        circle.Spawn();
        magicBurst.Spawn();

        for (int i = 0; i < 4; i++)
        {
            StrongBloom light = new StrongBloom(Projectile.Center, Vector2.Zero, Color.White * 0.6f, 5f, 36);
            light.Spawn();
        }

        // Shake the screen a bit.
        ScreenShakeSystem.StartShakeAtPoint(Projectile.Center, 4f);

        // Brighten the keyboard.
        NamelessDeityKeyboardShader.BrightnessIntensity += 0.5f;
    }

    public override bool? CanDamage() => Time >= TelegraphTime;

    public override bool ShouldUpdatePosition() => Time >= TelegraphTime;

    public float TelegraphWidthFunction(float completionRatio)
    {
        // Calculate the 0-1 interpolant of how complete the telegraph is.
        float telegraphCompletion = InverseLerp(0f, TelegraphTime, Time);

        // Use the projectile's width as a base for the telegraph's width.
        float baseWidth = Projectile.width * 1.96f;

        // Make it so that the width expands outward in a cute, slightly cartoonish way as it appears.
        float fadeInScale = Clamp(EasingCurves.Elastic.Evaluate(EasingType.Out, InverseLerp(0f, 0.25f, telegraphCompletion)), 0f, 10f);

        // Make the width increase as the telegraph nears its completion. This corresponds with a decrease in opacity, as though the telegraph is dissipating.
        float fadeOutScale = InverseLerp(0.7f, 1f, telegraphCompletion) * 2f;

        // Combine the scale factors and use them
        float widthScaleFactor = (fadeInScale + fadeOutScale) * 0.5f;
        return widthScaleFactor * baseWidth;
    }

    public Color TelegraphColorFunction(float completionRatio)
    {
        // Calculate the 0-1 interpolant of how complete the telegraph is.
        float telegraphCompletion = InverseLerp(0f, TelegraphTime, Time);

        // Make the telegraph fade out at its top and bottom.
        float endFadeOpacity = InverseLerpBump(0f, 0.2f, 0.64f, 1f, completionRatio);

        // Calculate the overall opacity based on the Projectile.Opacity, endFadeOpacity, and the telegraph's lifetime.
        // As the telegraph approaches its death, it fades out.
        float opacity = InverseLerpBump(0f, 0.6f, 0.7f, 1f, telegraphCompletion) * Projectile.Opacity * endFadeOpacity * 0.5f;

        // Calculate the color with the opacity in mind.
        Color color = Main.hslToRgb(Hue, 0.96f, 0.78f) * opacity;
        color.A = 0;

        return color;
    }

    public override bool PreDraw(ref Color lightColor)
    {
        // Draw the telegraph at first.
        if (Time < TelegraphTime)
        {
            // Configure the streak shader's texture.
            ManagedShader streakShader = ShaderManager.GetShader("NoxusBoss.GenericTrailStreak");
            streakShader.SetTexture(GennedAssets.Textures.TrailStreaks.StreakBloomLine, 1);
            streakShader.Apply();

            List<Vector2> telegraphPoints = Projectile.GetLaserControlPoints(7, 3200f, Vector2.UnitY);
            PrimitiveSettings settings = new PrimitiveSettings(TelegraphWidthFunction, TelegraphColorFunction, Shader: streakShader);
            PrimitiveRenderer.RenderTrail(telegraphPoints, settings, 6);
        }

        return false;
    }

    public void DrawWithShader(SpriteBatch spriteBatch)
    {
        if (Time < TelegraphTime)
            return;

        // Draw the galaxy after the telegraph is gone.
        for (float i = 1f; i > 0f; i -= 0.1f)
        {
            Vector2 drawWorldPosition = Projectile.Center - Projectile.velocity * Pow(1f - i, 0.7f) * 2f;
            DrawGalaxy(drawWorldPosition, i);
        }
    }

    public void DrawGalaxy(Vector2 worldPosition, float opacity)
    {
        // Apply the shader, calculating the transformation matrix in the process.
        Matrix flatScale = new Matrix()
        {
            M11 = 2.4f,
            M12 = 0f,
            M21 = 0f,
            M22 = 1.7f
        };
        ManagedShader galaxyShader = ShaderManager.GetShader("NoxusBoss.GalaxyShader");
        Matrix spinInPlaceRotation = Matrix.CreateRotationZ(SpinRotation);
        Matrix orientationRotation = Matrix.CreateRotationZ(Projectile.rotation);
        galaxyShader.TrySetParameter("time", Main.GlobalTimeWrappedHourly);
        galaxyShader.TrySetParameter("transformation", orientationRotation * flatScale * spinInPlaceRotation);
        galaxyShader.Apply();

        // Calculate colors.
        Color galaxyColor1 = Main.hslToRgb(Hue, 1f, 0.62f) * Projectile.Opacity * opacity;
        Color galaxyColor2 = Main.hslToRgb((Hue + 0.28f) % 1f, 1f, 0.62f) * Projectile.Opacity * opacity;
        galaxyColor1.G = (byte)(galaxyColor1.G * 0.633f);
        galaxyColor2.G /= 2;
        galaxyColor1.A = 0;
        galaxyColor2.A = 0;

        // Calculate the draw position.
        Vector2 galaxyDrawPosition = worldPosition - Main.screenPosition + Vector2.UnitY * 32f;

        // Draw the galaxy. Projectile.scale is intrinsic to Projectile.width when the projectile is initially spawned, so it doesn't need to be accounted for here.
        float scale = Projectile.width / (float)MoltenNoise.Value.Width * opacity * 4f;
        Main.spriteBatch.Draw(MoltenNoise, galaxyDrawPosition, null, galaxyColor1, 0f, MoltenNoise.Size() * 0.5f, scale, 0, 0f);
        Main.spriteBatch.Draw(MoltenNoise, galaxyDrawPosition, null, galaxyColor2, 0f, MoltenNoise.Size() * 0.5f, scale * 0.7f, 0, 0f);

        // Draw a bright center.
        Main.pixelShader.CurrentTechnique.Passes[0].Apply();
        Main.spriteBatch.Draw(BloomCircleSmall, galaxyDrawPosition, null, Color.Wheat with { A = 0 } * 0.4f, 0f, BloomCircleSmall.Size() * 0.5f, scale, 0, 0f);
        Main.spriteBatch.Draw(BloomCircleSmall, galaxyDrawPosition, null, galaxyColor1 * 0.2f, 0f, BloomCircleSmall.Size() * 0.5f, scale * 1.8f, 0, 0f);
    }
}
