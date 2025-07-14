using Luminance.Common.DataStructures;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity.SpecificEffectManagers;
using NoxusBoss.Content.Particles;
using NoxusBoss.Core.AdvancedProjectileOwnership;
using NoxusBoss.Core.Configuration;
using NoxusBoss.Core.DataStructures.ShapeCurves;
using NoxusBoss.Core.SoundSystems;
using ReLogic.Utilities;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.NamelessDeity.Projectiles;

public class ClockConstellation : ConstellationProjectile, IProjOwnedByBoss<NamelessDeityBoss>
{
    private static bool timeIsStopped;

    public override int ConvergeTime => (int)(NamelessDeityBoss.ClockConstellation_ClockConvergenceDuration * 0.7f);

    public override int StarDrawIncrement => 4;

    public override float StarConvergenceSpeed => 0.00031f;

    public override float StarRandomOffsetFactor => 0.5f;

    protected override ShapeCurve constellationShape
    {
        get
        {
            ShapeCurveManager.TryFind("Clock", out ShapeCurve? curve);
            return curve!.Upscale(Projectile.width * Projectile.scale * 1.414f);
        }
    }

    public override Color DecidePrimaryBloomFlareColor(float colorVariantInterpolant)
    {
        return Color.Lerp(Color.Red, Color.Yellow, Pow(colorVariantInterpolant, 2f)) * 0.33f;
    }

    public override Color DecideSecondaryBloomFlareColor(float colorVariantInterpolant)
    {
        return Color.Lerp(Color.Orange, Color.White, colorVariantInterpolant) * 0.4f;
    }

    public SlotId TickSound;

    public int StartingHour;

    public int TimeRestartDelay;

    public int TollCounter;

    public float DeathZoneVisualsTimer;

    public float EffectiveDeathZoneRadius = 3200f;

    public float PreviousHourRotation = -10f;

    // Every second toll reverses time.
    public bool TimeIsReversed => TollCounter % 2 == 1;

    public bool AudioShouldReverse;

    public int TollSpinDuration
    {
        get
        {
            // The first toll involves convergence behaviors for the stars. As such, the convergence time must be added for it specifically.
            if (TollCounter == 0)
                return ConvergeTime + NamelessDeityBoss.ClockConstellation_RegularSpinDuration;

            // Otherwise, use the corresponding spin duration based on whether time is being reversed.
            return TimeIsReversed ? NamelessDeityBoss.ClockConstellation_ReversedTimeSpinDuration : NamelessDeityBoss.ClockConstellation_RegularSpinDuration;
        }
    }

    public static int MaxTolls => 2;

    public static float HourArc => Pi / 6f;

    // These angular velocity constants manipulate the hour hand, hence their slow pacing.
    public static float DefaultAngularVelocity => HourArc / NamelessDeityBoss.ClockConstellation_RegularSpinDuration;

    public static float ReversedTimeMinSpeedFactor => NamelessDeityBoss.ClockConstellation_ReversedTimeMinSpeedFactor;

    public static float ReversedTimeMaxSpeedFactor => NamelessDeityBoss.ClockConstellation_ReversedTimeMaxSpeedFactor;

    // This may seem a bit opaque, but the math behind how this is arrived at should elucidate things.
    // Basically, the equation for the reversed time arc movement factor is as follows:

    // ArcWindUpInterpolant = InverseLerp(0f, ReversedTimeSpinDuration, TimeSinceLastToll)
    // ArcMovementFactor = Lerp(ReversedTimeMinSpeedFactor, ReversedTimeMaxSpeedFactor, Pow(ArcWindUpInterpolant, ReversedTimeArcInterpolantPower))

    // Things like wind-up time and speed factors are constants above, since unlike the ReversedTimeArcInterpolantPower variable those are intuitive and easy to control.

    // So how do we solve for the exponent?
    // Well, it's necessary to look at the problem in terms of discrete steps, since this process will happen in discrete steps every frame.
    // Each frame, we'll increment the angle by DefaultAngularVelocity * ArcMovementFactor. This journey should encompass 360 / 12 (or 30) degrees (aka going from one hour to another).
    // So basically, we want the sum of all of these increments to equal pi/6, and in such a way that ReversedTimeArcInterpolantPower is a calculated variable.
    // Fortunately, there's a perfect tool for this task: a definite integral.
    // While it will be slightly off, since integrals work on infinitesimal sum increments rather than discrete frame rates, it should be close enough to be sufficient.
    // This integral will allow for the calculation of things *based on their incremental behavior*. Let's rewrite the equation in terms of it:

    // For reference, ArcWindUpInterpolant is the variable of integration as t, ReversedTimeMinSpeedFactor is a, ReversedTimeMaxSpeedFactor is b, DefaultAngularVelocity is c, ReversedTimeSpinDuration is f, and ReversedTimeArcInterpolantPower is x.
    // c * f * ∫(0, 1) (a + (b - a) * t^x) * dt = pi / 6
    // ∫(0, 1) (a + (b - a) * t^x) * dt = pi / (c * f * 6)                                              (Get constants on right side)
    // ∫(0, 1) a * dt + ∫(0, 1) (b - a) * t^x * dt = pi / (c * f * 6)                                   (Split constant term into second integral)
    // ∫(0, 1) (b - a) * t^x * dt = pi / (c * f * 6) - a                                                (Separate constant and move to the right side)
    // (1^(x + 1) * (b - a)) / (x + 1) - (0^(x + 1) * (b - a)) / (x + 1) = pi / (c * f * 6) - a         (Evaluate integral at upper and lower bounds and subtract the two)
    // (b - a) / (x + 1) = pi / (c * f * 6) - a                                                         (Simplify results)
    // (x + 1) / (b - a) = 1 / (pi / (c * f * 6) - a)                                                   (Invert so that x + 1 is on in the left hand numerator)
    // x + 1 = (b - a) / (pi / (c * f * 6) - a)                                                         (Remove denominator from left side)
    // x = (b - a) / (pi / (c * f * 6) - a) - 1                                                         (Remove the subtraction by 1 to get the value of x)
    public static float ReversedTimeArcInterpolantPower =>
        (ReversedTimeMaxSpeedFactor - ReversedTimeMinSpeedFactor) / (HourArc / (DefaultAngularVelocity * NamelessDeityBoss.ClockConstellation_ReversedTimeSpinDuration) - ReversedTimeMinSpeedFactor) - 1f;

    public static bool TimeIsStopped
    {
        get
        {
            // Turn off the time stop effect if Nameless isn't actually present.
            if (NamelessDeityBoss.Myself is null)
                timeIsStopped = false;

            return timeIsStopped;
        }
        set => timeIsStopped = value;
    }

    public ref float HourHandRotation => ref Projectile.ai[0];

    public ref float MinuteHandRotation => ref Projectile.ai[1];

    public ref float TimeSinceLastToll => ref Projectile.ai[2];

    public override void SetStaticDefaults()
    {
        if (Main.netMode == NetmodeID.Server)
            return;

        AudioReversingSystem.ReversingConditionEvent += ReverseAudioWhenTimeIsReversed;
        AudioReversingSystem.FreezingConditionEvent += StopAudioWhenTimeIsStopped;
    }

    private bool ReverseAudioWhenTimeIsReversed()
    {
        foreach (Projectile projectile in Main.ActiveProjectiles)
        {
            if (projectile.type == Type && projectile.As<ClockConstellation>().AudioShouldReverse)
                return true;
        }

        return false;
    }

    private static bool StopAudioWhenTimeIsStopped() => TimeIsStopped;

    public override void SetDefaults()
    {
        Projectile.width = 840;
        Projectile.height = 840;
        Projectile.penetrate = -1;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.netImportant = true;
        Projectile.timeLeft = 60000;
    }

    public override void SendExtraAI(BinaryWriter writer)
    {
        writer.Write(TimeRestartDelay);
        writer.Write(TollCounter);
        writer.Write(PreviousHourRotation);
    }

    public override void ReceiveExtraAI(BinaryReader reader)
    {
        TimeRestartDelay = reader.ReadInt32();
        TollCounter = reader.ReadInt32();
        PreviousHourRotation = reader.ReadSingle();
    }

    public override void PostAI()
    {
        // Fade in at first. If the final toll has happened, fade out.
        if (TollCounter >= MaxTolls)
            Projectile.Opacity = Saturate(Projectile.Opacity - NamelessDeityBoss.ClockConstellation_FadeOutIncrement);
        else
            Projectile.Opacity = InverseLerp(0f, 45f, Time);

        if (WoTGConfig.Instance.PhotosensitivityMode)
            Projectile.Opacity *= 0.75f;

        // No Nameless Deity? Die.
        if (!Projectile.TryGetGenericOwner(out NPC nameless) || nameless.ModNPC is not NamelessDeityBoss namelessModNPC)
        {
            Projectile.Kill();
            return;
        }

        // Update the screen shader.
        UpdateScreenShader();

        // Make the time restart delay go down.
        TimeIsStopped = false;
        Player target = Main.player[Player.FindClosest(Projectile.Center, 1, 1)];
        int starburstID = ModContent.ProjectileType<Starburst>();
        if (TimeRestartDelay >= 1)
        {
            TimeIsStopped = true;
            TimeRestartDelay--;

            // Make all starbursts go back.
            if (TimeRestartDelay <= 0)
            {
                foreach (Projectile p in Main.ActiveProjectiles)
                {
                    if (p.type == starburstID)
                    {
                        p.ai[2] = 1f;
                        p.velocity = p.SafeDirectionTo(Projectile.Center) * p.velocity.Length() * 0.31f;
                        if (!p.WithinRange(Projectile.Center, NamelessDeityBoss.ClockConstellation_ReversedTimeSpinDuration * 16f))
                            p.Kill();

                        p.netUpdate = true;
                    }
                }
                HourHandRotation -= 0.004f;
                Projectile.netUpdate = true;
            }
        }
        else
        {
            TimeSinceLastToll++;

            // Create periodically collapsing chromatic bursts. This doesn't happen on low graphics settings.
            bool lowGraphicsSettings = WoTGConfig.Instance.PhotosensitivityMode || Main.gfxQuality <= 0.4f;
            if (TimeIsReversed && TimeSinceLastToll % 30 == 1 && !lowGraphicsSettings)
            {
                ExpandingChromaticBurstParticle burst = new ExpandingChromaticBurstParticle(Projectile.Center, Vector2.Zero, Color.Coral * 0.54f, 14, 18.3f, -1.3f);
                burst.Spawn();
            }
        }

        // Kill all starbursts that are going back in time and are close to the clock.
        foreach (Projectile p in Main.ActiveProjectiles)
        {
            if (p.type == starburstID && p.ai[2] == 1f)
            {
                if (p.WithinRange(Projectile.Center, 220f))
                    p.scale *= 0.9f;
                if (p.WithinRange(Projectile.Center, 30f))
                    p.Kill();
            }
        }

        // Approach the nearest player.
        if (Projectile.WithinRange(target.Center, 100f) || TimeIsStopped || TimeIsReversed || TollCounter >= MaxTolls)
            Projectile.velocity *= 0.82f;
        else
        {
            float approachSpeed = Pow(InverseLerp(ConvergeTime, 0f, Time), 2f) * 19f + 3f;
            Projectile.velocity = Projectile.SafeDirectionTo(target.Center) * approachSpeed;
        }

        // Make the hands move quickly as they fade in before moving more gradually.
        // This cause a time stop if the hour hand reaches a new hour and the clock has completely faded in.
        float handAppearInterpolant = InverseLerp(0f, ConvergeTime, Time);
        float baseAngularVelocity = DefaultAngularVelocity;
        float handMovementSpeed = 1f;

        // Make the hands accelerate as time goes backwards.
        if (TimeIsReversed)
        {
            float arcWindUpInterpolant = InverseLerp(0f, NamelessDeityBoss.ClockConstellation_ReversedTimeSpinDuration, TimeSinceLastToll);
            handMovementSpeed = Lerp(ReversedTimeMinSpeedFactor, ReversedTimeMaxSpeedFactor, Pow(arcWindUpInterpolant, ReversedTimeArcInterpolantPower));
        }
        if (TollCounter >= MaxTolls)
            handMovementSpeed = 0f;

        // Make the time go on.
        float hourHandOffset = baseAngularVelocity * handMovementSpeed * (1f - TimeIsStopped.ToInt()) * TimeIsReversed.ToDirectionInt();

        // Make the hour move manually at the start.
        if (handAppearInterpolant < 1f)
        {
            PreviousHourRotation = StartingHour * HourArc;
            HourHandRotation = Pow(handAppearInterpolant, 0.15f) * HourArc * 12f + HourArc * StartingHour;
        }
        else
            HourHandRotation -= hourHandOffset;

        // Ensure that hand rotations stay within a 0-2pi range.
        PreviousHourRotation = WrapAngle360(PreviousHourRotation);
        HourHandRotation = WrapAngle360(HourHandRotation);

        // Make the minute hand rotate in accordance with the hour hand.
        MinuteHandRotation = WrapAngle360(HourHandRotation * 12f - PiOver2);

        if (TimeSinceLastToll == TollSpinDuration - 5)
            SoundEngine.PlaySound(GennedAssets.Sounds.NamelessDeity.ClockStrike with { Volume = 1.35f });

        // Make the clock strike if it reaches a new hour.
        if (TimeSinceLastToll >= TollSpinDuration)
        {
            // Create a clock strike sound and other visuals.
            ScreenShakeSystem.StartShakeAtPoint(Projectile.Center, 11f);

            float closestHourRotation = Round(WrapAngle360(HourHandRotation) / HourArc) * HourArc;
            MinuteHandRotation = HourHandRotation * 12f - PiOver2;
            HourHandRotation = closestHourRotation;
            TimeSinceLastToll = 0f;
            TimeRestartDelay = NamelessDeityBoss.ClockConstellation_TollWaitDuration;
            Projectile.netUpdate = true;
            TollCounter++;
            NamelessDeityKeyboardShader.BrightnessIntensity += 0.8f;

            AudioShouldReverse = TimeIsReversed;

            // Make the clock hands split the screen instead on the final toll.
            if (TollCounter >= MaxTolls)
            {
                ScreenShakeSystem.StartShakeAtPoint(Projectile.Center, 16f);

                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    int telegraphTime = (int)Clamp(41f / namelessModNPC.DifficultyFactor, 27f, 1000f);
                    float telegraphLineLength = 4500f;
                    Vector2 hourHandDirection = HourHandRotation.ToRotationVector2();
                    Vector2 minuteHandDirection = MinuteHandRotation.ToRotationVector2();

                    foreach (var starburst in AllProjectilesByID(starburstID))
                        starburst.Kill();

                    Projectile.NewProjectileBetter_InheritedOwner(Projectile.GetSource_FromThis(), Projectile.Center - minuteHandDirection * telegraphLineLength * 0.5f, minuteHandDirection, ModContent.ProjectileType<TelegraphedScreenSlice>(), 0, 0f, -1, telegraphTime, telegraphLineLength);
                    Projectile.NewProjectileBetter_InheritedOwner(Projectile.GetSource_FromThis(), Projectile.Center - hourHandDirection * telegraphLineLength * 0.5f, hourHandDirection, ModContent.ProjectileType<TelegraphedScreenSlice>(), 0, 0f, -1, telegraphTime, telegraphLineLength);
                }
            }
        }

        if (TimeSinceLastToll == TollSpinDuration - 60)
            AudioShouldReverse = !TimeIsReversed;

        // Start the loop sound and initialize the starting hour configuration on the first frame.
        if (Projectile.localAI[0] == 0f || ((!SoundEngine.TryGetActiveSound(TickSound, out ActiveSound? s2) || !s2.IsPlaying) && !TimeIsStopped))
        {
            if (Projectile.localAI[0] == 0f)
            {
                StartingHour = Main.rand.Next(12);
                HourHandRotation = StartingHour * HourArc;
                Projectile.netUpdate = true;
            }

            SoundStyle tickSound = TimeIsReversed ? GennedAssets.Sounds.NamelessDeity.ClockTickReversed : GennedAssets.Sounds.NamelessDeity.ClockTick;
            TickSound = SoundEngine.PlaySound(tickSound with { Volume = 1.1f, IsLooped = true }, Projectile.Center);
            Projectile.localAI[0] = 1f;
        }

        // Update the ticking loop sound.
        if (SoundEngine.TryGetActiveSound(TickSound, out ActiveSound? s))
        {
            s.Position = Projectile.Center;
            s.Volume = Projectile.Opacity * handAppearInterpolant * 1.9f;

            // Make the sound temporarily stop if time is stopped.
            if (TimeIsStopped || Time <= ConvergeTime * 0.65f || TollCounter >= MaxTolls)
                s.Stop();
        }

        // Release starbursts in an even spread.
        int starburstReleaseRate = NamelessDeityBoss.ClockConstellation_StarburstReleaseRate;
        int starburstCount = NamelessDeityBoss.ClockConstellation_StarburstCount;
        float starburstShootSpeed = NamelessDeityBoss.ClockConstellation_StarburstShootSpeed;

        if (handAppearInterpolant >= 0.75f && Time % starburstReleaseRate == 0f && !TimeIsStopped && TollCounter < MaxTolls)
        {
            ScreenShakeSystem.StartShakeAtPoint(Projectile.Center, 3.6f);

            bool canPlaySound = !TimeIsReversed || CountProjectiles(starburstID) >= 30;
            if (canPlaySound)
            {
                SoundEngine.PlaySound((TimeIsReversed ? GennedAssets.Sounds.NamelessDeity.SunFireballShootSoundReversed : GennedAssets.Sounds.NamelessDeity.SunFireballShootSound) with
                {
                    Volume = 0.76f,
                    MaxInstances = 20
                }, Projectile.Center);
            }

            if (Main.netMode != NetmodeID.MultiplayerClient && !TimeIsReversed)
            {
                float shootOffsetAngle = (Time % (starburstReleaseRate * 2f) == 0f) ? Pi / starburstCount : 0f;
                shootOffsetAngle += Main.rand.NextFloatDirection() * 0.0294f;

                for (int i = 0; i < starburstCount; i++)
                {
                    Vector2 starburstVelocity = (TwoPi * i / starburstCount + shootOffsetAngle + Projectile.AngleTo(target.Center)).ToRotationVector2() * starburstShootSpeed;
                    Projectile.NewProjectileBetter_InheritedOwner(Projectile.GetSource_FromThis(), Projectile.Center, starburstVelocity, starburstID, NamelessDeityBoss.StarburstDamage, 0f, -1, 0f, 2f);
                }
            }
        }

        // Adjust the time. This process can technically make Main.time negative but that doesn't seem to cause any significant problems, and works fine with the watch UI.
        int hour = (int)((HourHandRotation + PiOver2 + 0.002f).Modulo(TwoPi) / TwoPi * 12f);
        int minute = (int)((MinuteHandRotation + PiOver2 + 0.002f).Modulo(TwoPi) / TwoPi * 60f);
        int totalMinutes = hour * 60 + minute;
        Main.dayTime = true;
        Main.time = totalMinutes * 60 - 16200f;
    }

    public void UpdateScreenShader()
    {
        if (!timeIsStopped)
            DeathZoneVisualsTimer += TimeIsReversed.ToDirectionInt() * (WoTGConfig.Instance.PhotosensitivityMode ? 0.009f : 0.01667f);

        float deathRadius = NamelessDeityBoss.ClockConstellation_DeathZoneRadius / SmoothStep(0.0001f, 1f, Projectile.Opacity);

        // Make the distortion visuals start farther out if time is reversed, to allow the player to better see the incoming starbursts.
        if (TimeIsReversed)
            deathRadius *= 1.11f;

        EffectiveDeathZoneRadius = Lerp(EffectiveDeathZoneRadius, deathRadius, 0.1f);

        if (Main.netMode != NetmodeID.Server)
        {
            ManagedScreenFilter deathShader = ShaderManager.GetFilter("NoxusBoss.NamelessClockDeathZoneShader");
            deathShader.TrySetParameter("time", DeathZoneVisualsTimer);
            deathShader.TrySetParameter("distortionIntensity", Projectile.Opacity * (1f - WoTGConfig.Instance.PhotosensitivityMode.ToInt()));
            deathShader.TrySetParameter("zoom", Main.GameViewMatrix.Zoom);
            deathShader.TrySetParameter("center", Projectile.Center);
            deathShader.TrySetParameter("deathRadius", EffectiveDeathZoneRadius);
            deathShader.TrySetParameter("whiteGlow", WoTGConfig.Instance.PhotosensitivityMode ? 0.75f : 0.5f);
            deathShader.Activate();
        }
    }

    public void DrawClockHands()
    {
        // Calculate clock hand colors.
        float handOpacity = InverseLerp(ConvergeTime - 40f, ConvergeTime + 96f, Time);
        Color generalHandColor = Color.Lerp(Color.OrangeRed, Color.Coral, 0.24f) with { A = 20 };
        Color minuteHandColor = Projectile.GetAlpha(generalHandColor) * handOpacity;
        Color hourHandColor = Projectile.GetAlpha(generalHandColor) * handOpacity;

        // Collect textures.
        Texture2D minuteHandTexture = GennedAssets.Textures.Projectiles.ClockMinuteHand.Value;
        Texture2D hourHandTexture = GennedAssets.Textures.Projectiles.ClockHourHand.Value;

        // Calculate the clock hand scale and draw positions. The scale is relative to the hitbox of the projectile so that the clock can be arbitrarily sized without issue.
        float handScale = Projectile.width / (float)hourHandTexture.Width * 0.52f;
        Vector2 handBaseDrawPosition = Projectile.Center - Main.screenPosition;
        Vector2 minuteHandDrawPosition = handBaseDrawPosition - MinuteHandRotation.ToRotationVector2() * handScale * 26f;
        Vector2 hourHandDrawPosition = handBaseDrawPosition - HourHandRotation.ToRotationVector2() * handScale * 26f;

        // Draw the hands with afterimages.
        for (int i = 0; i < 24; i++)
        {
            float afterimageOpacity = 1f - i / 24f;
            float minuteHandAfterimageRotation = MinuteHandRotation + i * (TimeIsStopped ? 0.002f : 0.008f) * TimeIsReversed.ToDirectionInt();
            float hourHandAfterimageRotation = HourHandRotation + i * (TimeIsStopped ? 0.0013f : 0.005f) * TimeIsReversed.ToDirectionInt();
            Main.spriteBatch.Draw(minuteHandTexture, minuteHandDrawPosition, null, minuteHandColor * afterimageOpacity, minuteHandAfterimageRotation, Vector2.UnitY * minuteHandTexture.Size() * 0.5f, handScale, 0, 0f);
            Main.spriteBatch.Draw(hourHandTexture, hourHandDrawPosition, null, hourHandColor * afterimageOpacity, hourHandAfterimageRotation, Vector2.UnitY * hourHandTexture.Size() * 0.5f, handScale, 0, 0f);
        }
    }

    public override bool PreDraw(ref Color lightColor)
    {
        float opacityFactor = Lerp(1f, 0.6f, InverseLerpBump(0.25f, 0.5f, 0.75f, 1f, TimeRestartDelay / (float)NamelessDeityBoss.ClockConstellation_TollWaitDuration));
        opacityFactor *= WoTGConfig.Instance.PhotosensitivityMode ? 0.1f : 1f;
        Projectile.Opacity *= opacityFactor;

        // Draw the bloom backglow.
        Vector2 drawPosition = Projectile.Center - Main.screenPosition;
        Main.spriteBatch.Draw(BloomCircleSmall, drawPosition, null, Projectile.GetAlpha(Color.Orange with { A = 0 }) * 0.3f, 0f, BloomCircleSmall.Size() * 0.5f, 17f, 0, 0f);
        Main.spriteBatch.Draw(BloomCircleSmall, drawPosition, null, Projectile.GetAlpha(Color.Coral with { A = 0 }) * 0.5f, 0f, BloomCircleSmall.Size() * 0.5f, 11f, 0, 0f);
        Main.spriteBatch.Draw(BloomCircleSmall, drawPosition, null, Projectile.GetAlpha(Color.Wheat with { A = 0 }) * 0.9f, 0f, BloomCircleSmall.Size() * 0.5f, 2f, 0, 0f);
        Main.spriteBatch.Draw(BloomCircleSmall, drawPosition, null, Projectile.GetAlpha(Color.White with { A = 0 }), 0f, BloomCircleSmall.Size() * 0.5f, 0.7f, 0, 0f);

        // Draw clock hands.
        DrawClockHands();
        Projectile.Opacity /= opacityFactor;

        return false;
    }

    public override void OnKill(int timeLeft)
    {
        if (SoundEngine.TryGetActiveSound(TickSound, out ActiveSound? s))
            s.Stop();
        TimeIsStopped = false;
    }
}
