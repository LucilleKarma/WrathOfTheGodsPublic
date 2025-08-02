using Luminance.Common.DataStructures;
using Luminance.Common.Utilities;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Core.AdvancedProjectileOwnership;
using NoxusBoss.Core.Data;
using NoxusBoss.Core.Graphics.SpecificEffectManagers;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using static NoxusBoss.Content.NPCs.Bosses.NamelessDeity.Projectiles.LightDagger;

namespace NoxusBoss.Content.NPCs.Bosses.NamelessDeity.Projectiles;

public class PsychedelicFeather : ModProjectile, IProjOwnedByBoss<NamelessDeityBoss>
{
    /// <summary>
    /// The destination of this feather after being fired.
    /// </summary>
    public Vector2 FireDestination
    {
        get => new(Projectile.ai[1], Projectile.ai[2]);
        set
        {
            Projectile.ai[1] = value.X;
            Projectile.ai[2] = value.Y;
        }
    }

    /// <summary>
    /// Whether this feather has played its pre-disappear sound yet or not.
    /// </summary>
    public bool HasPlayedPreDisappearSound
    {
        get;
        set;
    }

    /// <summary>
    /// The pupil offset of this feather's eye.
    /// </summary>
    public Vector2 PupilOffset
    {
        get;
        set;
    }

    /// <summary>
    /// The ideal pupil offset of this feather's eye.
    /// </summary>
    public Vector2 IdealPupilOffset
    {
        get;
        set;
    }

    /// <summary>
    /// The outer iris color of this feather's eye.
    /// </summary>
    public Vector3 OuterIrisColor
    {
        get;
        set;
    }

    /// <summary>
    /// The inner iris color of this feather's eye.
    /// </summary>
    public Vector3 InnerIrisColor
    {
        get;
        set;
    }

    /// <summary>
    /// How long this feather has existed for, in frames.
    /// </summary>
    public ref float Time => ref Projectile.localAI[1];

    /// <summary>
    /// The starting aim direction of this feather.
    /// </summary>
    public ref float StartingAimDirection => ref Projectile.ai[1];

    /// <summary>
    /// The spin rotation of this feather.
    /// </summary>
    public ref float SpinRotation => ref Projectile.localAI[0];

    /// <summary>
    /// How long this feather spends fading in.
    /// </summary>
    public static int FadeInTime => SecondsToFrames(0.25f);

    /// <summary>
    /// How long this feather spends aiming for it fires.
    /// </summary>
    public static int AimTime => SecondsToFrames(0.54f);

    /// <summary>
    /// How long this feather spends speeding up its acceleration.
    /// </summary>
    public static int AccelerateSpeedUpTime => SecondsToFrames(0.25f);

    /// <summary>
    /// How long this feather should exist for, at maximum, in frames.
    /// </summary>
    public const int Lifetime = 300;

    public override string Texture => GetAssetPath("Content/NPCs/Bosses/NamelessDeity/Projectiles", Name);

    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailingMode[Type] = 2;
        ProjectileID.Sets.TrailCacheLength[Type] = 20;
    }

    public override void SetDefaults()
    {
        Projectile.width = 32;
        Projectile.height = 280;
        Projectile.penetrate = -1;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.hostile = true;
        Projectile.timeLeft = Lifetime;
        Projectile.Opacity = 0f;
    }

    public override void OnSpawn(IEntitySource source)
    {
        if (Main.netMode == NetmodeID.Server)
            return;

        Dictionary<string, Vector3[]> paletteData = LocalDataManager.Read<Vector3[]>("Content/NPCs/Bosses/NamelessDeity/NamelessDeityPalettes.json");
        Vector3[] outerIrisPalettesPalette = paletteData["PsychedelicFeatherOuterIrisColors"];
        Vector3[] innerIrisPalettesPalette = paletteData["PsychedelicFeatherInnerIrisColors"];

        OuterIrisColor = Main.rand.Next(outerIrisPalettesPalette);
        InnerIrisColor = Main.rand.Next(innerIrisPalettesPalette);
    }

    public override void AI()
    {
        if (!Projectile.TryGetGenericOwner(out NPC nameless) || nameless.ModNPC is not NamelessDeityBoss namelessModNPC)
        {
            Projectile.Kill();
            return;
        }

        // Fade in.
        Projectile.Opacity = InverseLerp(1f, FadeInTime, Time);
        Projectile.scale = Projectile.Opacity;

        Player target = Main.player[Player.FindClosest(Projectile.Center, 1, 1)];

        // Decide rotation.
        if (Time < AimTime)
        {
            float aimForwardInterpolant = InverseLerp(0f, AimTime * 0.75f, Time);
            Vector2 aimDirection = Vector2.Lerp(Projectile.SafeDirectionTo(target.Center), StartingAimDirection.ToRotationVector2(), 0.04f).SafeNormalize(Vector2.UnitY);

            Vector2 aimVelocity = aimDirection * Lerp(-19f, 2f, aimForwardInterpolant);
            Projectile.rotation = Projectile.AngleTo(target.Center);
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, aimVelocity, 0.5f);
        }
        else
        {
            Projectile.rotation = Projectile.rotation.AngleLerp(Projectile.velocity.ToRotation(), 0.4f);
            Projectile.MaxUpdates = 2;

            Vector2 currentDirection = Projectile.velocity.SafeNormalize(Vector2.UnitY);

            if (Time == AimTime)
            {
                Vector2 predictiveAim = target.velocity * 35f;
                if (Vector2.Dot(predictiveAim, Projectile.SafeDirectionTo(target.Center)) < 0f)
                    predictiveAim = Vector2.Zero;

                FireDestination = target.Center + currentDirection * Main.rand.NextFloat(360f, 600f) + predictiveAim;
                Projectile.netUpdate = true;

                Projectile.oldPos = new Vector2[Projectile.oldPos.Length];
            }

            // Play a graze sound if a player was very, very close to being hit.
            HandleGrazeCheck();

            // Accelerate.
            float terminalVelocityInterpolant = InverseLerp(35f, 18f, Projectile.velocity.Length());
            float accelerationWindUpInterpolant = InverseLerp(0f, AccelerateSpeedUpTime, Time - AimTime).Squared();
            float acceleration = terminalVelocityInterpolant * SmoothStep(0f, 15f, accelerationWindUpInterpolant);
            Projectile.velocity += currentDirection * acceleration * namelessModNPC.DifficultyFactor;

            // Fade out prior to disappearing.
            float signedDistanceToDestination = Vector2.Dot(currentDirection, FireDestination - Projectile.Center);
            Projectile.scale *= InverseLerp(0f, 300f, signedDistanceToDestination);
            Projectile.Opacity *= Projectile.scale;

            if (signedDistanceToDestination <= 480f && !HasPlayedPreDisappearSound && Projectile.ai[0] == 0f)
            {
                SoundEngine.PlaySound(GennedAssets.Sounds.NamelessDeity.FeatherPreDisappear with { MaxInstances = 0, Volume = 0.45f }, Projectile.Center);
                HasPlayedPreDisappearSound = true;
            }

            // Disappear if past the fire destination.
            if (signedDistanceToDestination < 0f)
                Projectile.Kill();
        }

        // Make the pupil dart around randomly.
        UpdatePupil();

        Time++;
    }

    /// <summary>
    /// Updates the pupil on the eye of the feather, making it randomly and erratically dart around.
    /// </summary>
    public void UpdatePupil()
    {
        if (Main.rand.NextBool(25) && PupilOffset.WithinRange(IdealPupilOffset, 2f))
        {
            Vector2 oldOffset = IdealPupilOffset;
            do
            {
                IdealPupilOffset = Main.rand.NextVector2CircularEdge(20f, 8f);
            }
            while (oldOffset.WithinRange(IdealPupilOffset, 15f));
        }
        PupilOffset = PupilOffset.MoveTowards(IdealPupilOffset, 8f);
    }

    /// <summary>
    /// Performs a check to determine whether a graze sound and visual should be performed.
    /// </summary>
    public void HandleGrazeCheck()
    {
        int closestIndex = Player.FindClosest(Projectile.Center, 1, 1);
        Player closest = Main.player[closestIndex];
        Vector2 currentDirection = Projectile.velocity.SafeNormalize(Vector2.Zero);

        float horizontalDistanceFromPlayer = Abs(SignedDistanceToLine(closest.Center, Projectile.Center, currentDirection.RotatedBy(PiOver2)));
        float verticalDistanceFromPlayer = Abs(SignedDistanceToLine(closest.Center, Projectile.Center, currentDirection));

        bool dangerouslyCloseToHit = horizontalDistanceFromPlayer >= Projectile.width && horizontalDistanceFromPlayer <= 90f && verticalDistanceFromPlayer <= 300f;
        if (dangerouslyCloseToHit && Main.myPlayer == closestIndex && closest.GetValueRef<int>(GrazeEchoFieldName) <= 0)
        {
            if (NamelessDeityBoss.Myself_CurrentState != NamelessDeityBoss.NamelessAIType.EnterPhase2)
                ScreenShakeSystem.StartShake(10f, Pi / 3f, Projectile.velocity, 0.15f);

            SoundEngine.PlaySound(GennedAssets.Sounds.NamelessDeity.DaggerGrazeEcho with { Volume = 2.4f });
            RadialScreenShoveSystem.Start(Projectile.Center, 24);

            closest.GetValueRef<int>(GrazeEchoFieldName).Value = DefaultGrazeDelay;
        }
    }

    public static void DrawFeather(Vector2 drawPosition, Vector2 scale, Vector2 pupilOffset, Vector2 idealPupilOffset, Vector3 outerIrisColor, Vector3 innerIrisColor, Color color,
        float rotation, float localTime, float psychedelicInterpolant, float blurOffset, int frameX)
    {
        Texture2D texture = GennedAssets.Textures.Projectiles.PsychedelicFeather;
        Texture2D eyeMask = GennedAssets.Textures.Projectiles.PsychedelicFeatherEyeMask;
        Rectangle frame = texture.Frame(2, 1, frameX, 0);

        float[] blurWeights = new float[7];
        for (int i = 0; i < blurWeights.Length; i++)
            blurWeights[i] = GaussianDistribution(i - blurWeights.Length * 0.5f, 2.1f) * 0.6f;

        float pupilDistanceFromIdeal = idealPupilOffset.Distance(pupilOffset);
        float dilation = Lerp(0.72f, 1.2f, InverseLerp(12f, 1f, pupilDistanceFromIdeal));
        Vector2 effectivePupilOffset = pupilOffset * 0.004f;

        switch (frameX)
        {
            case 1:
                effectivePupilOffset.Y += 0.02f;
                break;
        }

        ManagedShader featherShader = ShaderManager.GetShader("NoxusBoss.PsychedelicFeatherShader");
        featherShader.TrySetParameter("localTime", localTime);
        featherShader.TrySetParameter("pupilDilation", dilation);
        featherShader.TrySetParameter("pupilOffset", effectivePupilOffset + Vector2.UnitY * 0.06f);
        featherShader.TrySetParameter("outerIrisColor", outerIrisColor);
        featherShader.TrySetParameter("innerIrisColor", innerIrisColor);
        featherShader.TrySetParameter("psychedelicInterpolant", psychedelicInterpolant);
        featherShader.TrySetParameter("blurWeights", blurWeights);
        featherShader.TrySetParameter("blurOffset", blurOffset);
        featherShader.TrySetParameter("frame", new Vector4(frame.X, frame.Y, frame.Width, frame.Height));
        featherShader.SetTexture(eyeMask, 1);
        featherShader.SetTexture(PerlinNoise, 2, SamplerState.LinearWrap);
        featherShader.SetTexture(GennedAssets.Textures.Extra.Psychedelic, 3, SamplerState.LinearWrap);
        featherShader.Apply();

        Main.spriteBatch.Draw(texture, drawPosition, frame, color, rotation - PiOver2 + 0.08f, frame.Size() * 0.5f, scale, 0, 0f);
    }

    public override bool PreDraw(ref Color lightColor)
    {
        int frameX = Projectile.identity % 2;
        float speed = Projectile.velocity.Length() * Projectile.MaxUpdates;
        float blurOffset = InverseLerp(20f, 60f, speed).Squared() * 0.005f;
        float psychedelicInterpolant = InverseLerp(32f, 54f, speed).Squared() * 0.72f + (1f - Projectile.Opacity) * 3f;
        float squish = InverseLerp(10f, 32f, speed);
        float afterimageClumpInterpolant = InverseLerp(1f, 0.1f, Projectile.scale);
        Vector2 scale = new Vector2(Lerp(1f, 0.66f, squish), 1f) * Projectile.scale;
        Vector2 drawPosition = Projectile.Center - Main.screenPosition;
        float localTime = Main.GlobalTimeWrappedHourly + Projectile.identity * 0.271f;

        Main.spriteBatch.PrepareForShaders(BlendState.Additive);
        for (int i = Projectile.oldPos.Length - 1; i >= 1; i--)
        {
            float afterimageOpacity = (1f - i / (float)Projectile.oldPos.Length) * 0.5f;
            Vector2 afterimageDrawPosition = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
            afterimageDrawPosition = Vector2.Lerp(afterimageDrawPosition, Projectile.Center - Main.screenPosition, afterimageClumpInterpolant);

            DrawFeather(afterimageDrawPosition, scale, PupilOffset, IdealPupilOffset, OuterIrisColor, InnerIrisColor, Projectile.GetAlpha(Color.White) * afterimageOpacity, Projectile.rotation, localTime, psychedelicInterpolant, blurOffset, frameX);
        }

        Main.spriteBatch.PrepareForShaders(BlendState.NonPremultiplied);
        DrawFeather(drawPosition, scale, PupilOffset, IdealPupilOffset, OuterIrisColor, InnerIrisColor, Projectile.GetAlpha(Color.White), Projectile.rotation, localTime, psychedelicInterpolant, blurOffset, frameX);

        Main.spriteBatch.ResetToDefault();

        return false;
    }

    public override void OnKill(int timeLeft)
    {
        if (Projectile.ai[0] == 0f)
            SoundEngine.PlaySound(GennedAssets.Sounds.NamelessDeity.FeatherDisappear with { MaxInstances = 0, Volume = 2f });
        RadialScreenDistortionSystem.CreateDistortion(Projectile.Center, Main.rand.NextFloat(100f, 250f));
        ScreenShakeSystem.StartShakeAtPoint(Projectile.Center, 4f);
    }

    public override bool? CanDamage() => Time >= AimTime && Projectile.scale >= 0.72f;

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) =>
        Utilities.RotatingHitboxCollision(Projectile, targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.velocity.SafeNormalize(Vector2.UnitY));
}
