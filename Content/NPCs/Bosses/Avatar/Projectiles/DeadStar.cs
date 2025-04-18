using Luminance.Common.DataStructures;
using Luminance.Common.Easings;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Content.Dusts;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm;
using NoxusBoss.Content.Particles;
using NoxusBoss.Core.Configuration;
using NoxusBoss.Core.DataStructures;
using NoxusBoss.Core.Graphics.Automators;
using NoxusBoss.Core.Graphics.RenderTargets;
using NoxusBoss.Core.Utilities;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using static NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm.AvatarOfEmptiness;

namespace NoxusBoss.Content.NPCs.Bosses.Avatar.Projectiles;

public class DeadStar : ModProjectile, IDrawsWithShader, IProjOwnedByBoss<AvatarOfEmptiness>
{
    // Ensure that the star shader has layering priority over other shader projectiles.
    public float LayeringPriority => 0.71f;

    /// <summary>
    /// Whether blur glow effects are enabled for this star.
    /// </summary>
    public static bool BlurEnabled => WoTGConfig.Instance.VisualOverlayIntensity >= 0.7f;

    /// <summary>
    /// The place in which this star was initially summoned.
    /// </summary>
    public Vector2 SpawnPosition
    {
        get;
        set;
    }

    /// <summary>
    /// How intense the telegraph visual is overall.
    /// </summary>
    public float TelegraphIntensity
    {
        get;
        set;
    }

    /// <summary>
    /// A multiplier of how far the telegraph should extend at any given moment.
    /// </summary>
    public float TelegraphLengthFactor
    {
        get;
        set;
    } = 1f;

    /// <summary>
    /// The direction of the telegraph.
    /// </summary>
    public Vector2 TelegraphDirection
    {
        get;
        set;
    }

    /// <summary>
    /// The amount of iron bursts that have been performed.
    /// </summary>
    public int BurstCounter
    {
        get;
        set;
    }

    /// <summary>
    /// A dedicated timer used for the telegraph and its shoot behaviors.
    /// </summary>
    public int ShootTimer
    {
        get;
        set;
    }

    /// <summary>
    /// A dedicated timer used for when the star is in the process of shattering.
    /// </summary>
    public int ShatterTimer
    {
        get;
        set;
    }

    /// <summary>
    /// The amount of time it's been since this projectile was created.
    /// </summary>
    public ref float Time => ref Projectile.ai[0];

    /// <summary>
    /// A 0-1 interpolant of how "dead" the star is, aka how close it is to iron.
    /// </summary>
    public ref float DeathInterpolant => ref Projectile.ai[1];

    /// <summary>
    /// How long it's been since the star completely died (aka turned to iron).
    /// </summary>
    public ref float TimeSinceDeath => ref Projectile.ai[2];

    /// <summary>
    /// The spin of the star. Corresponds to spherical scrolling on the texture.
    /// </summary>
    public ref float SpinOffset => ref Projectile.localAI[0];

    /// <summary>
    /// The size of cracks on the star when it's iron.
    /// </summary>
    public ref float CrackSize => ref Projectile.localAI[1];

    /// <summary>
    /// The death star's glow interpolant. Used as it dies.
    /// </summary>
    public ref float ShatterInterpolant => ref Projectile.localAI[2];

    /// <summary>
    /// The standard hitbox size of the star.
    /// </summary>
    public static int StartingSize => 202;

    /// <summary>
    /// The maximum quantity of bursts before the star should begin glowing and fading.
    /// </summary>
    public static int MaxBurstCount => 7;

    /// <summary>
    /// The standard visual scaling factor of the star.
    /// </summary>
    public static float MaxScale => 4f;

    /// <summary>
    /// The render target responsible for holding dead star draw contents.
    /// </summary>
    public static InstancedRequestableTarget StarTarget
    {
        get;
        private set;
    }

    /// <summary>
    /// The render target responsible for holding blurred dead star draw contents.
    /// </summary>
    public static InstancedRequestableTarget BlurTarget
    {
        get;
        private set;
    }

    /// <summary>
    /// The palette that sparks spawned by this star can cycle through.
    /// </summary>
    public static readonly Palette SparkParticlePalette = new Palette().
        AddColor(Color.White).
        AddColor(Color.Wheat).
        AddColor(Color.Yellow).
        AddColor(Color.Orange).
        AddColor(Color.Red);

    public override string Texture => GetAssetPath("Content/NPCs/Bosses/Avatar/Projectiles", Name);

    public override void SetStaticDefaults()
    {
        if (Main.netMode == NetmodeID.Server)
            return;

        StarTarget = new InstancedRequestableTarget();
        Main.ContentThatNeedsRenderTargets.Add(StarTarget);

        BlurTarget = new InstancedRequestableTarget();
        Main.ContentThatNeedsRenderTargets.Add(BlurTarget);
    }

    public override void SetDefaults()
    {
        Projectile.width = StartingSize;
        Projectile.height = StartingSize;
        Projectile.penetrate = -1;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.hostile = true;
        Projectile.timeLeft = 60000;
        Projectile.netImportant = true;
        Projectile.hide = true;
        CooldownSlot = ImmunityCooldownID.Bosses;
    }

    public override void SendExtraAI(BinaryWriter writer)
    {
        writer.Write(Projectile.width);
        writer.Write(Projectile.height);
        writer.Write(Projectile.scale);

        writer.Write(BurstCounter);
        writer.Write(ShatterTimer);
        writer.Write(TelegraphLengthFactor);
        writer.Write(TelegraphIntensity);
        writer.WriteVector2(TelegraphDirection);
        writer.WriteVector2(SpawnPosition);
    }

    public override void ReceiveExtraAI(BinaryReader reader)
    {
        int width = reader.ReadInt32();
        int height = reader.ReadInt32();
        Projectile.Resize(width, height);

        Projectile.scale = reader.ReadSingle();

        BurstCounter = reader.ReadInt32();
        ShatterTimer = reader.ReadInt32();
        TelegraphLengthFactor = reader.ReadSingle();
        TelegraphIntensity = reader.ReadSingle();
        TelegraphDirection = reader.ReadVector2();
        SpawnPosition = reader.ReadVector2();
    }

    public override void AI()
    {
        // No Avatar? Die.
        if (Myself is null)
        {
            Projectile.Kill();
            return;
        }

        // Create a twinkle somewhere in the sky on the first frame, indicating that the star was stolen.
        if (Time == 1f)
        {
            Vector2 twinkleSpawnPosition = Main.screenPosition + new Vector2(Main.rand.NextFloat(400f, Main.screenWidth - 400f), Main.rand.NextFloat(200f, 400f));
            Color twinkleColor = Color.Lerp(Color.Goldenrod, Color.Red, Main.rand.NextFloat(0.37f, 0.9f));
            TwinkleParticle twinkle = new TwinkleParticle(twinkleSpawnPosition, Vector2.Zero, twinkleColor, 36, 8, Vector2.One * 3.5f, Color.Red);
            twinkle.Spawn();

            SoundEngine.PlaySound(GennedAssets.Sounds.Common.Twinkle with { MaxInstances = 1, SoundLimitBehavior = SoundLimitBehavior.ReplaceOldest });

            // Store the spawn position.
            SpawnPosition = twinkleSpawnPosition + Vector2.UnitY * Projectile.height * 0.5f;
            Projectile.netUpdate = true;

            // Shake the screen slightly.
            ScreenShakeSystem.StartShake(6f);
        }

        // Play the star kill sound as it starts to grow as a star.
        int growDelay = DyingStarsWind_GrowDelay;
        int growTime = DyingStarsWind_GrowToFullSizeTime;
        int crackDelay = DyingStarsWind_CrackDelay;
        if (Time == growDelay)
            SoundEngine.PlaySound(GennedAssets.Sounds.Avatar.StarKill with { Volume = 1.2f });

        // Stick above the Avatar.
        // The vertical offset value is specially picked to give the appearance that the Avatar's lily is hooking into the star and draining it.
        float appearInterpolant = InverseLerp(1f, growTime, Time - growDelay);
        float hoverPositionInterpolant = SmoothStep(0f, 1f, appearInterpolant);
        Vector2 hoverDestination = Myself.Center - Vector2.UnitY * Myself.scale * 592f;
        Projectile.Bottom = Vector2.Lerp(SpawnPosition, hoverDestination + Vector2.UnitY * (StartingSize - Projectile.width), EasingCurves.Quintic.Evaluate(EasingType.Out, hoverPositionInterpolant));
        Projectile.Opacity = Utils.Remap(appearInterpolant, 0.55f, 1f, 0.15f, 1f);

        // Grow to full size.
        if (Time <= growDelay + growTime)
            Projectile.scale = Pow(appearInterpolant, 1.5f) * MaxScale;

        if (Time == growDelay + growTime)
            Projectile.netUpdate = true;

        // Spin around. The speed of this dramatically decreases as the star dies.
        SpinOffset += Lerp(1f, 0.09f, DeathInterpolant.Squared()) * (1f - ShatterInterpolant) * 0.009f;

        // Make the star gradually die.
        if (Time >= DyingStarsWind_DeathDelay)
            DeathInterpolant = Saturate(DeathInterpolant + 1f / DyingStarsWind_DeathCoolTime);

        // Make the star crack and release particles when it's dead.
        if (TimeSinceDeath >= crackDelay)
        {
            CrackSize = Clamp(CrackSize + 0.15f, 0f, 2f);
            CreateIdleIronParticles();

            int particleCount = (int)Lerp(1f, 8f, ShatterInterpolant);
            if (Main.rand.NextBool())
                CreateParticlesTowardsSpiderLily(particleCount);
        }

        // Play a crack sound shortly after dying.
        if (TimeSinceDeath == crackDelay)
        {
            SoundEngine.PlaySound(GennedAssets.Sounds.Avatar.DeadStarCoreCrack with { Volume = 1.4f });
            ScreenShakeSystem.StartShakeAtPoint(Projectile.Center, 8f);
        }

        // Increment special timers when the star is dead.
        if (DeathInterpolant >= 1f)
        {
            TimeSinceDeath++;

            if (TimeSinceDeath >= DyingStarsWind_ShootDelay)
                ShootTimer++;
        }

        // Handle telegraph and shoot behaviors.
        if (BurstCounter < MaxBurstCount)
            HandleShootingAndTelegraph();
        else
        {
            // Play a collapse sound at first.
            if (ShatterInterpolant <= 0f)
                SoundEngine.PlaySound(GennedAssets.Sounds.Avatar.DeadStarCoreCritical with { Volume = 1.4f });

            TelegraphIntensity *= 0.75f;
            ShatterInterpolant = Saturate(ShatterInterpolant + 1f / DyingStarsWind_CollapseDelay);
            if (ShatterInterpolant >= 1f)
                ShatterTimer++;
            if (ShatterTimer >= DyingStarsWind_ShatterDelay)
            {
                CreateParticlesTowardsSpiderLily(67);
                PerformShatterExplosion();
                Projectile.Kill();
            }

            Projectile.scale = Lerp(1f, 0.9f + Sin(TwoPi * Time / 4f) * ShatterInterpolant * (ShatterTimer * 0.0009f + 0.03f) - ShatterTimer * 0.0027f, Pow(ShatterInterpolant, 0.7f)) * MaxScale;
        }

        // Release residual sparks briefly after the iron chunks have been ejected.
        if (ShootTimer >= DyingStarsWind_TelegraphTime + 1 && TelegraphIntensity >= 0.3f)
            EmitMetalSparkParticles(12, TelegraphIntensity + 0.5f, 2.8f);

        Time++;
    }

    public void EmitMetalSparkParticles(int particleCount, float particleSpeedFactor, float maxArc)
    {
        for (int i = 0; i < particleCount; i++)
        {
            // Determine the iron's spawn position. This will be at the edge of the telegraph on the star.
            Vector2 ironSpawnPosition = Projectile.Center + TelegraphDirection.RotatedByRandom(0.03f) * Main.rand.NextFloat(0.9f, 1.1f) * Projectile.width * 1.25f;

            // Calculate the iron's velocity. The iron will be able to move in a decently wide arc, but particles whose path deviate considerably from the telegraph direction
            // are considerably slower.
            float offsetAngleInterpolant = Main.rand.NextFloat();
            float velocityOffsetAngle = Main.rand.NextFromList(-maxArc, maxArc) * offsetAngleInterpolant;
            Vector2 ironVelocity = TelegraphDirection.RotatedBy(velocityOffsetAngle) * Main.rand.NextFloat(18f, 50f);
            ironVelocity *= (1.12f - offsetAngleInterpolant * 0.8f) * particleSpeedFactor;

            // Calculate the iron's scale. Particles that start off if a faster velocity are smaller.
            float ironScale = Utils.Remap(ironVelocity.Length(), 50f, 18f, 0.15f, 0.4f) * (1f - offsetAngleInterpolant);

            // Calculate iron colors. These are generally white, yellow, orange, etc.
            // Colors that you'd expect from an angle grinder's sparks.
            Color ironColor = SparkParticlePalette.SampleColor(Main.rand.NextFloat());
            Color glowColor = Color.Wheat * 0.95f;

            // Calculate the iron's lifetime based on how big it is.
            // Since the scale depends on speed, this means that faster particles die faster.
            int ironLifetime = (int)(ironScale * 70f);

            MetalSparkParticle iron = new MetalSparkParticle(ironSpawnPosition, ironVelocity, Main.rand.NextBool(6), ironLifetime, new Vector2(0.4f, Main.rand.NextFloat(0.4f, 0.85f)) * ironScale, 1f, ironColor, glowColor);
            iron.Spawn();
        }
    }

    public void CreateIdleIronParticles()
    {
        // Calculate the particle spawn chance for each loop iteration.
        float particleSpawnChance = InverseLerp(0f, 180f, TimeSinceDeath) * (1f - ShatterInterpolant) * 0.8f;

        for (int i = 0; i < 8; i++)
        {
            if (Main.rand.NextFloat() > particleSpawnChance)
                continue;

            // Determine the iron's spawn position. This will be somewhere at the edge of the star.
            Vector2 ironSpawnPosition = Projectile.Center + Main.rand.NextVector2Unit() * Main.rand.NextFloat(0.9f, 1.1f) * Projectile.width * 1.25f;

            // Calculate the iron's velocity. It will move away from the star with a considerable horizontal bias.
            Vector2 ironVelocity = (ironSpawnPosition - Projectile.Center).SafeNormalize(Vector2.UnitY) * new Vector2(14f, 3f);

            // Spawn the iron particle.
            Dust iron = Dust.NewDustPerfect(ironSpawnPosition, ModContent.DustType<GenericWindFlyDust>(), ironVelocity);
            iron.scale = 0.75f;
            iron.color = Color.Lerp(Color.White, new Color(122, 108, 95), Main.rand.NextFloat(0.7f));
            iron.noGravity = true;
        }
    }

    public void HandleShootingAndTelegraph()
    {
        // Initialize the telegraph on the first frame of shoot preparations.
        // These telegraphs will generally aim upwards but will also try to aim horizontally towards the player, to prevent "rungod" strategies.
        if (ShootTimer == 1)
        {
            // Calculate a random telegraph direction that aims mostly upward.
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                // Use a bit of horizontal predictiveness.
                Player closest = Main.player[Player.FindClosest(Projectile.Center, 1, 1)];
                TelegraphDirection = Projectile.SafeDirectionTo(closest.Center + closest.velocity * 20f).RotatedByRandom(0.23f);

                // Fire a net update so that the telegraph direction is known.
                Projectile.netUpdate = true;
            }

            // Play a telegraph sound.
            SoundEngine.PlaySound(GennedAssets.Sounds.Avatar.DeadStarTelegraph with { Volume = 1.05f, MaxInstances = 0 });
        }

        // Calculate the extend interpolant.
        float extendInterpolant = InverseLerp(DyingStarsWind_TelegraphTime - 9f, DyingStarsWind_TelegraphTime + 10f, ShootTimer);

        // If the extension has not concluded, go outward.
        if (extendInterpolant < 1f)
        {
            TelegraphIntensity = Sqrt(InverseLerp(0f, DyingStarsWind_TelegraphTime - 10f, ShootTimer)) * Utils.Remap(extendInterpolant, 0.4f, 1f, 1f, 0.7f);

            // Create a momentary, incredibly strong flash around the halfway mark of the extension. This will coincide with the 
            // spawning of projectiles.
            TelegraphIntensity += InverseLerpBump(0.4f, 0.5f, 0.6f, 0.61f, extendInterpolant) * 0.5f;

            // Calculate the length factor.
            TelegraphLengthFactor = 1f + extendInterpolant.Squared() * 15f;
        }

        // If the extension has concluded, recede back until it's back to its original value in anticipation of the next firing.
        else
        {
            float stabilizeSpeedInterpolant = 0.2f;
            TelegraphIntensity = Lerp(TelegraphIntensity, 0f, stabilizeSpeedInterpolant);
            TelegraphLengthFactor = Lerp(TelegraphLengthFactor, 1f, stabilizeSpeedInterpolant);
            if (TelegraphIntensity <= 0.02f)
            {
                ShootTimer = 0;
                TelegraphIntensity = 0f;
                TelegraphLengthFactor = 0f;
            }
        }

        // Release chunks of iron at the end of the telegraph.
        if (ShootTimer == DyingStarsWind_TelegraphTime)
        {
            // Shake the screen and play a burst sound for impact.
            ScreenShakeSystem.StartShakeAtPoint(Projectile.Center, 8f, shakeStrengthDissipationIncrement: 0.35f);
            SoundEngine.PlaySound(GennedAssets.Sounds.Avatar.DeadStarBurst with { Volume = 1.2f, MaxInstances = 0 });

            // Create a ton of metal spark particles.
            EmitMetalSparkParticles(250, 1f, 0.6f);

            // Create absorption particles.
            CreateParticlesTowardsSpiderLily(32);

            // Release a burst of iron that'll fall down.
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                // Lose a bit of mass.
                Projectile.width -= StartingSize / 19;

                if (Myself is not null)
                    Myself.netUpdate = true;

                for (int i = 0; i < 14; i++)
                {
                    Vector2 ironSpawnPosition = Projectile.Center + TelegraphDirection.RotatedByRandom(0.03f) * Main.rand.NextFloat(0.9f, 1.1f) * Projectile.width * 1.25f;
                    Vector2 ironVelocity = Projectile.SafeDirectionTo(ironSpawnPosition).RotatedByRandom(DyingStarsWind_IronChunkBurstAngle) * Main.rand.NextFloat(24f, 32f) * new Vector2(1.2f, 1f);
                    NewProjectileBetter(Projectile.GetSource_FromThis(), ironSpawnPosition, ironVelocity, ModContent.ProjectileType<DeadStarIron>(), IronDamage, 0f);
                }

                BurstCounter++;
                Projectile.netUpdate = true;
            }

            // Make the lily glow boost increase temporarily.
            Myself.As<AvatarOfEmptiness>().LilyGlowIntensityBoost += 0.45f;
        }
    }

    public void PerformShatterExplosion()
    {
        ScreenShakeSystem.StartShakeAtPoint(Projectile.Center, 30f);

        // Create a ton of metal spark particles.
        EmitMetalSparkParticles(450, 1f, TwoPi);

        // Play an explode sound.
        SoundEngine.PlaySound(GennedAssets.Sounds.Avatar.DeadStarCoreExplode with { Volume = 1.4f });

        if (Main.netMode != NetmodeID.MultiplayerClient)
        {
            TelegraphDirection = -Vector2.UnitY;
            for (int i = 0; i < 50; i++)
            {
                Vector2 ironSpawnPosition = Projectile.Center + TelegraphDirection.RotatedByRandom(0.03f) * Main.rand.NextFloat(0.9f, 1.1f) * Projectile.width * 1.25f;
                Vector2 ironVelocity = Projectile.SafeDirectionTo(ironSpawnPosition).RotatedByRandom(1.09f) * Main.rand.NextFloat(13f, 34f) * new Vector2(1.3f, 1f);
                NewProjectileBetter(Projectile.GetSource_FromThis(), ironSpawnPosition, ironVelocity, ModContent.ProjectileType<DeadStarIron>(), IronDamage, 0f);
            }

            NewProjectileBetter(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero, ModContent.ProjectileType<DarkWave>(), 0, 0f, -1, 0f, 0f, 1f);
        }

        // Make the lily glow boost increase temporarily.
        Myself.As<AvatarOfEmptiness>().LilyGlowIntensityBoost += 1.3f;
    }

    public void CreateParticlesTowardsSpiderLily(int particleCount)
    {
        for (int i = 0; i < particleCount; i++)
        {
            Vector2 particleSpawnOffset = Vector2.UnitY.RotatedByRandom(PiOver2) * Projectile.width * Projectile.scale * 0.36f;
            particleSpawnOffset += Main.rand.NextVector2Circular(16f, 3f);
            Vector2 particleSpawnPosition = Projectile.Center + particleSpawnOffset;
            Vector2 particleVelocity = (Myself.As<AvatarOfEmptiness>().SpiderLilyPosition - particleSpawnPosition) * Main.rand.NextFloat(0.05f, 0.067f);
            if (particleCount >= 10)
                particleVelocity *= Main.rand.NextFloat(1f, 2.4f);

            Dust particle = Dust.NewDustPerfect(particleSpawnPosition, 264, particleVelocity);
            particle.color = Color.Lerp(Color.Red, new Color(255, 8, 48), Main.rand.NextFloat(0.8f));
            particle.noGravity = true;
            particle.noLight = true;
            particle.scale = Main.rand.NextFloat(1f, 1.4f);
        }
    }

    public override bool PreDraw(ref Color lightColor)
    {
        float bloomFlareRotation = Main.GlobalTimeWrappedHourly * 0.9f + Projectile.identity;
        Color bloomFlareColor1 = Color.Orange * (1f - DeathInterpolant);
        Color bloomFlareColor2 = Color.Red * (1f - DeathInterpolant);
        bloomFlareColor1.A = 0;
        bloomFlareColor2.A = 0;

        Vector2 drawPosition = Projectile.Center - Main.screenPosition;
        float scale = Projectile.scale * 0.4f;
        Main.spriteBatch.Draw(BloomFlare, drawPosition, null, bloomFlareColor1 * Projectile.Opacity * 0.75f, bloomFlareRotation, BloomFlare.Size() * 0.5f, scale, 0, 0f);
        Main.spriteBatch.Draw(BloomFlare, drawPosition, null, bloomFlareColor2 * Projectile.Opacity * 0.45f, -bloomFlareRotation, BloomFlare.Size() * 0.5f, scale * 1.2f, 0, 0f);
        return false;
    }

    public void DrawWithShader(SpriteBatch spriteBatch)
    {
        StarTarget.Request(1200, 1200, Projectile.identity, () =>
        {
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);

            float opacity = DeathInterpolant;
            if (opacity < 0.9f)
                DrawAliveSun(Sqrt(1f - opacity) * InverseLerp(0.85f, 0.6f, opacity));
            DrawDeadSun(Saturate(opacity * Projectile.Opacity * 1.5f));

            Main.spriteBatch.End();
        });

        DrawBlurToTarget();
        DrawFromTarget();
        DrawTelegraphBeam();
    }

    public void DrawAliveSun(float opacity)
    {
        Vector2 drawPosition = ViewportSize * 0.5f;
        Vector2 scale = Vector2.One * Projectile.width * Projectile.scale * 1.5f / DendriticNoiseZoomedOut.Size();

        // Calculate star colors.
        Vector3 coronaColor = new Vector3(1f, 0f, 0f) * Pow(opacity, 1.5f);
        Vector3 darkerColor = new Vector3(12f, 6.89f, -0.73f) * opacity * 0.1f;
        Vector3 accentColor = new Vector3(0.35f, 0f, -3f) * opacity;

        // Supply information to the star shader.
        var sunShader = ShaderManager.GetShader("NoxusBoss.AvatarSunShader");
        sunShader.TrySetParameter("coronaIntensityFactor", 0.11f);
        sunShader.TrySetParameter("mainColor", coronaColor);
        sunShader.TrySetParameter("darkerColor", darkerColor);
        sunShader.TrySetParameter("subtractiveAccentFactor", accentColor);
        sunShader.TrySetParameter("sphereSpinTime", SpinOffset);
        sunShader.SetTexture(PerlinNoise, 1);
        sunShader.SetTexture(GennedAssets.Textures.Extra.PsychedelicWingTextureOffsetMap, 2);
        sunShader.Apply();

        // Draw the star.
        Main.spriteBatch.Draw(FireNoiseA, drawPosition, null, new Color(255, 176, 176) * opacity, Projectile.rotation, FireNoiseA.Size() * 0.5f, scale, 0, 0f);
    }

    public void DrawDeadSun(float opacity)
    {
        Texture2D metal = TextureAssets.Projectile[Type].Value;

        float heatCrackIntensity = 1.25f;
        float heatEmergePower = InverseLerp(0.5f, 1f, DeathInterpolant) * 0.3f - ShatterInterpolant * 0.28f;
        Vector2 heatCrackDirection = Lerp(1f, 0.21f, ShatterInterpolant) * TelegraphDirection;
        float specialHeatCrackIntensity = MathF.Max(TelegraphIntensity * 0.6f, ShatterInterpolant * 1.067f);

        var sunShader = ShaderManager.GetShader("NoxusBoss.DeadSunShader");
        sunShader.TrySetParameter("sphereSpinTime", SpinOffset + 0.25f);

        // Supply information to the star shader pertaining to heat cracks.
        sunShader.TrySetParameter("heatDistortionMaxOffset", 0.018f);
        sunShader.TrySetParameter("heatCrackEmergePower", heatEmergePower);
        sunShader.TrySetParameter("heatCrackGlowIntensity", heatCrackIntensity);
        sunShader.TrySetParameter("specialHeatCrackDirection", heatCrackDirection);
        sunShader.TrySetParameter("specialHeatCrackIntensity", specialHeatCrackIntensity);

        // Supply information to the star shader pertaining to metal cracks.
        float extendInterpolant = InverseLerp(DyingStarsWind_TelegraphTime - 25f, DyingStarsWind_TelegraphTime + 15f, ShootTimer);
        float initialHeatInterpolant = Pow(InverseLerp(DyingStarsWind_CrackDelay + DyingStarsWind_DeathDelay, 0f, Time), 0.3f);
        float crackSizeBoost = initialHeatInterpolant * 7f;
        float crackHeatBoost = initialHeatInterpolant * 0.76f;
        float metalCrackHeatInterpolant = InverseLerpBump(0.04f, 0.5f, 0.61f, 0.95f, extendInterpolant) + crackHeatBoost;

        sunShader.TrySetParameter("metalCrackSize", InverseLerp(0.985f, 1f, DeathInterpolant) * 0.78f + (1f - Projectile.width / (float)StartingSize) * 7.7f + ShatterInterpolant * 6f + metalCrackHeatInterpolant * 0.5f + crackSizeBoost);
        sunShader.TrySetParameter("metalCrackIntensity", Saturate(CrackSize - heatCrackIntensity) * 0.4f);
        sunShader.TrySetParameter("metalCrackHeatInterpolant", metalCrackHeatInterpolant * Lerp(0.8f, 0.9f, Cos01(Main.GlobalTimeWrappedHourly * 2f)));
        sunShader.TrySetParameter("metalCrackColor", Vector3.Lerp(Vector3.Zero, new(2f, 2f, 0f), ShatterInterpolant));

        // Supply information to the star shader pertaining to the highlight.
        sunShader.TrySetParameter("minHighlightBrightness", 0.4f);
        sunShader.TrySetParameter("maxHighlightBrightness", Cos01(TwoPi * Time / 12f) * ShatterInterpolant * 1.2f + 1.6f + ShatterTimer * 0.08f);
        sunShader.TrySetParameter("highlightSharpness", 4.6f);
        sunShader.TrySetParameter("highlightPosition", new Vector2(0.58f, 0.42f));
        sunShader.TrySetParameter("brightHighlightColor", Vector3.Lerp(new Vector3(0.95f, 0.92f, 0.862f), new Vector3(6f, 0.5f, 0.5f), ShatterInterpolant));
        sunShader.TrySetParameter("dullHighlightColor", new Color(165, 149, 142));

        // Supply information to the star shader pertaining to the underglow from the Avatar's lily.
        sunShader.TrySetParameter("underglowStartInterpolant", 0.56f);
        sunShader.TrySetParameter("underglowEndInterpolant", 0.7f);
        sunShader.TrySetParameter("underglowColor", new Color(175, 7, 39));

        // Supply information to the star shader pertaining to the collapse.
        sunShader.TrySetParameter("collapseInterpolant", Pow(ShatterTimer / (float)DyingStarsWind_ShatterDelay, 2f));

        // Supply textures to the star shader.
        sunShader.SetTexture(CrackedNoiseA, 1, SamplerState.LinearWrap);
        sunShader.SetTexture(DendriticNoiseZoomedOut, 2, SamplerState.LinearWrap);
        sunShader.SetTexture(WavyBlotchNoise, 3, SamplerState.LinearWrap);
        sunShader.Apply();

        // Draw the dead star.
        Vector2 drawPosition = ViewportSize * 0.5f;
        Vector2 scale = Vector2.One * Projectile.width * Projectile.scale * 1.2f / DendriticNoiseZoomedOut.Size();
        Main.spriteBatch.Draw(metal, drawPosition, null, new Color(244, 240, 232) * opacity, Projectile.rotation, metal.Size() * 0.5f, scale, 0, 0f);
    }

    public void DrawTelegraphBeam()
    {
        // Calculate telegraph draw information.
        float laserStretch = Pow(TelegraphIntensity, 3f) * TelegraphLengthFactor;
        float laserLength = Lerp(220f, 330f, Cos01(Main.GlobalTimeWrappedHourly * 34f)) * laserStretch;
        var telegraphShader = ShaderManager.GetShader("NoxusBoss.DeadSunTelegraphBeamShader");
        telegraphShader.TrySetParameter("laserStretch", laserStretch);

        // Draw the telegraph.
        List<Vector2> laserControlPoints = Projectile.GetLaserControlPoints(12, laserLength, TelegraphDirection);
        PrimitiveSettings settings = new PrimitiveSettings(TelegraphWidthFunction, TelegraphColorFunction, _ => TelegraphDirection * Projectile.width * (1.4f - laserStretch * 0.01f), Shader: telegraphShader);
        PrimitiveRenderer.RenderTrail(laserControlPoints, settings, 23);
    }

    public void DrawBlurToTarget()
    {
        if (!StarTarget.TryGetTarget(Projectile.identity, out RenderTarget2D? target) || target is null)
            return;

        BlurTarget.Request(target.Width, target.Height, Projectile.identity, () =>
        {
            if (!BlurEnabled)
                return;

            float generalBloomOpacity = (1f - ShatterInterpolant).Squared() * InverseLerp(0f, 30f, TimeSinceDeath - DyingStarsWind_CrackDelay);
            if (generalBloomOpacity <= 0f)
                return;

            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);

            float[] blurWeights = new float[9];
            for (int i = 0; i < blurWeights.Length; i++)
                blurWeights[i] = GaussianDistribution(i - blurWeights.Length * 0.5f, 1.25f) / 9f;
            Vector2 drawPosition = ViewportSize * 0.5f;

            // Apply post-processing atop the star.
            for (int i = 0; i < 3; i++)
            {
                float interpolant = i / 3f;
                float blurOffset = Lerp(0f, 0.00056f, interpolant);
                ManagedShader postProcessingShader = ShaderManager.GetShader("NoxusBoss.DeadSunPostProcessingShader");
                postProcessingShader.TrySetParameter("blurOffset", blurOffset);
                postProcessingShader.TrySetParameter("blurWeights", blurWeights);
                postProcessingShader.TrySetParameter("avoidanceDirection", TelegraphDirection);
                postProcessingShader.TrySetParameter("avoidanceOpacityInterpolant", 1f - ShatterInterpolant);
                postProcessingShader.TrySetParameter("bottomOpacityBias", 1f - DeathInterpolant);
                postProcessingShader.Apply();

                Main.spriteBatch.Draw(target, drawPosition, null, Color.White * (1f - interpolant), 0f, target.Size() * 0.5f, 1f, 0, 0f);
            }

            Main.spriteBatch.End();
        });
    }

    public void DrawFromTarget()
    {
        if (!StarTarget.TryGetTarget(Projectile.identity, out RenderTarget2D? baseTarget) || baseTarget is null)
            return;
        if (!BlurTarget.TryGetTarget(Projectile.identity, out RenderTarget2D? blurTarget) || blurTarget is null)
            return;

        // Draw the dead star.
        Vector2 drawPosition = Projectile.Center - Main.screenPosition;
        Main.spriteBatch.Draw(baseTarget, drawPosition, null, Color.White, 0f, baseTarget.Size() * 0.5f, 1f, 0, 0f);

        // Draw the bloom.
        if (BlurEnabled)
        {
            float generalBloomOpacity = (1f - ShatterInterpolant).Squared() * InverseLerp(0f, 30f, TimeSinceDeath - DyingStarsWind_CrackDelay);
            for (int i = 0; i < 50; i++)
            {
                float opacity = (1f - i / 50f).Squared() * 0.4f;
                float scale = 1f + i * 0.01f;
                Main.spriteBatch.Draw(blurTarget, drawPosition, null, Color.White * opacity * generalBloomOpacity, 0f, blurTarget.Size() * 0.5f, scale, 0, 0f);
            }
            Main.spriteBatch.Draw(blurTarget, drawPosition, null, Color.White * generalBloomOpacity, 0f, blurTarget.Size() * 0.5f, 1f, 0, 0f);
        }

        // Draw a glow atop everything if the star is in the process of cooling, but not quite fully cooled.
        Texture2D glow = BloomCircleSmall.Value;
        float warmthInterpolant = InverseLerpBump(0.25f, 0.6f, 0.75f, 1f, DeathInterpolant);
        Main.spriteBatch.Draw(glow, drawPosition, null, Color.OrangeRed with { A = 0 } * warmthInterpolant, 0f, glow.Size() * 0.5f, Projectile.scale * 1.8f, 0, 0f);
    }

    public float TelegraphWidthFunction(float completionRatio) => Projectile.width * Pow(TelegraphIntensity, 2.5f) * 0.7f;

    public Color TelegraphColorFunction(float completionRatio) => Projectile.GetAlpha(Color.OrangeRed) * Pow(TelegraphIntensity, 4f);

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        if (Myself is null)
            return false;

        // Make the Avatar's lily do contact damage.
        float _ = 0f;
        Vector2 topLeft = targetHitbox.TopLeft();
        Vector2 hitboxArea = targetHitbox.Size();
        Vector2 lilyStart = Myself.As<AvatarOfEmptiness>().SpiderLilyPosition - Vector2.UnitY * Myself.scale * 76f;
        Vector2 lilyEnd = Myself.As<AvatarOfEmptiness>().SpiderLilyPosition + Vector2.UnitY * Myself.scale * 74f;
        if (Time >= DyingStarsWind_GrowDelay + 30f && Collision.CheckAABBvLineCollision(topLeft, hitboxArea, lilyStart, lilyEnd, Myself.scale * 134f, ref _))
            return true;

        // Calculate sphere intersection checks.
        if (CircularHitboxCollision(projHitbox.Center(), Projectile.width * 0.9f, targetHitbox) && Projectile.Opacity >= 0.9f)
            return true;

        float laserStretch = Pow(TelegraphIntensity, 3f) * TelegraphLengthFactor;
        float laserLength = Lerp(220f, 330f, Cos01(Main.GlobalTimeWrappedHourly * 34f)) * laserStretch;
        if (laserLength < 560f)
            return false;

        // Calculate telegraph burst information.
        Vector2 start = Projectile.Center + TelegraphDirection * Projectile.width * 1.25f;
        Vector2 end = start + TelegraphDirection * laserLength * 0.85f;
        return Collision.CheckAABBvLineCollision(topLeft, hitboxArea, start, end, TelegraphWidthFunction(0f) * 0.85f, ref _);
    }
}
