using Luminance.Common.DataStructures;
using Luminance.Core;
using Luminance.Core.Graphics;
using Luminance.Core.Sounds;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Content.Dusts;
using NoxusBoss.Content.Particles.Metaballs;
using NoxusBoss.Core.AdvancedProjectileOwnership;
using NoxusBoss.Core.Configuration;
using NoxusBoss.Core.Data;
using NoxusBoss.Core.Physics.VerletIntergration;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using static NoxusBoss.Content.NPCs.Bosses.NamelessDeity.NamelessDeityBoss;

namespace NoxusBoss.Content.NPCs.Bosses.NamelessDeity.Projectiles;

public class CelestialDreamcatcher : ModProjectile, IProjOwnedByBoss<NamelessDeityBoss>
{
    public class StringAttachedFeather
    {
        public VerletSimulatedRope String;

        public Vector2 PupilOffset;

        public Vector2 IdealPupilOffset;

        public Vector3 OuterIrisColor;

        public Vector3 InnerIrisColor;

        public void Update(Vector2 attachPosition)
        {
            String.Update(attachPosition, 0.3f);

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
    }

    /// <summary>
    /// The ambient sound loop.
    /// </summary>
    public LoopedSoundInstance? AmbienceLoop
    {
        get;
        private set;
    }

    public StringAttachedFeather[] FeatherStrings
    {
        get;
        set;
    } = new StringAttachedFeather[3];

    /// <summary>
    /// How long this dreamcatcher has exist for so far, in frames.
    /// </summary>
    public ref float Time => ref Projectile.ai[0];

    public override string Texture => GetAssetPath("Content/NPCs/Bosses/NamelessDeity/Projectiles", Name);

    public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 72000;

    public override void SetDefaults()
    {
        Projectile.width = 600;
        Projectile.height = 600;
        Projectile.penetrate = -1;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.hostile = true;
        Projectile.timeLeft = EnterPhase2_AttackPlayer_ShootDelay + EnterPhase2_AttackPlayer_ShootTime + EnterPhase2_AttackPlayer_FadeOutDelay + EnterPhase2_AttackPlayer_FadeOutTime;
    }

    public override void AI()
    {
        // No Nameless Deity? Die.
        if (!Projectile.TryGetGenericOwner(out NPC nameless) || nameless.ModNPC is not NamelessDeityBoss namelessModNPC)
        {
            Projectile.Kill();
            return;
        }

        // Stay in the world.
        Projectile.Center = Vector2.Clamp(Projectile.Center, Vector2.One * 480f, new Vector2(Main.maxTilesX, Main.maxTilesY) * 16f - Vector2.One * 480f);

        int namelessAITimer = namelessModNPC.AITimer;
        bool attacking = namelessAITimer >= EnterPhase2_AttackPlayer_ShootDelay && namelessAITimer <= EnterPhase2_AttackPlayer_ShootDelay + EnterPhase2_AttackPlayer_ShootTime;
        int wrappedTimer = (int)Time % (EnterPhase2_AttackPlayer_PurifyingMatterSuckRate + EnterPhase2_AttackPlayer_PurifyingMatterExpelRate);

        float flySpeedInterpolant = InverseLerp(0f, 120f, Time);
        NPCAimedTarget target = nameless.GetTargetData();
        Vector2 swayOffset = Vector2.UnitX * Sin(TwoPi * Time / 360f) * 20f;
        Vector2 hoverDestination = target.Center - Vector2.UnitY.RotatedBy(TwoPi * Time / 360f) * new Vector2(500f, 350f);
        Vector2 idealVelocity = ((hoverDestination - Projectile.Center) * 0.11f + swayOffset) * flySpeedInterpolant;
        if (wrappedTimer < EnterPhase2_AttackPlayer_PurifyingMatterSuckRate)
            idealVelocity *= 0.21f;

        Projectile.velocity.X = Lerp(Projectile.velocity.X, idealVelocity.X, 0.05f);
        Projectile.velocity.Y = Lerp(Projectile.velocity.Y, idealVelocity.Y, 0.09f);

        float ambienceClosenessInterpolant = Lerp(-2f, 0.75f, InverseLerp(0f, 90f, Time));
        Vector2 ambienceSourcePosition = Vector2.Lerp(Projectile.Center, Main.LocalPlayer.Center, ambienceClosenessInterpolant);
        AmbienceLoop ??= LoopedSoundManager.CreateNew(GennedAssets.Sounds.NamelessDeity.DreamcatcherAmbienceLoop with { Volume = 1.33f }, () => !Projectile.active);
        AmbienceLoop.Update(ambienceSourcePosition, sound =>
        {
            sound.Volume = InverseLerp(15f, 90f, Time) * Projectile.Opacity.Squared() * 1.75f;
        });

        if (Projectile.Opacity < 1f)
        {
            int particleCount = (int)(Pow(1f - Projectile.Opacity, 0.75f) * 30f);
            for (int i = 0; i < particleCount; i++)
            {
                Vector2 sparkleSpawnPosition = Projectile.Center + Main.rand.NextVector2Circular(Projectile.width, Projectile.height) * 0.4f;
                Dust sparkle = Dust.NewDustPerfect(sparkleSpawnPosition, ModContent.DustType<TwinkleDust>(), Main.rand.NextVector2Circular(5f, 5f));
                sparkle.color = Main.hslToRgb(Main.rand.NextFloat(), 0.9f, 0.55f);
                sparkle.scale = Main.rand.NextFloat(0.3f, 0.45f);
            }
        }

        if (attacking)
        {
            // Suck in matter.
            if (wrappedTimer == 1)
            {
                SoundEngine.PlaySound(GennedAssets.Sounds.NamelessDeity.FeatherPreDisappear).WithVolumeBoost(1.5f);
                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    int damage = PurifyingMatterDamage;
                    for (int i = 0; i < 9; i++)
                    {
                        Vector2 purifyingMatterSpawnPosition = Projectile.Center + (TwoPi * i / 9f).ToRotationVector2() * 1600f;
                        Vector2 purifyingMatterVelocity = purifyingMatterSpawnPosition.SafeDirectionTo(Projectile.Center) * 2f;
                        Projectile.NewProjectileBetter_InheritedOwner(Projectile.GetSource_FromAI(), purifyingMatterSpawnPosition, purifyingMatterVelocity, ModContent.ProjectileType<PurifyingMatter>(), damage, 0f, -1, Projectile.AngleTo(target.Center), 0f, 1f);
                    }
                }
            }

            // Expel matter.
            if (wrappedTimer == EnterPhase2_AttackPlayer_PurifyingMatterSuckRate)
            {
                ScreenShakeSystem.StartShake(8f);
                SoundEngine.PlaySound(GennedAssets.Sounds.NamelessDeity.FeatherAppear).WithVolumeBoost(1.5f);

                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    for (int i = 0; i < 7; i++)
                    {
                        float arc = Lerp(-27f, 27f, i / 6f);
                        Vector2 perpendicular = Projectile.SafeDirectionTo(target.Center).RotatedBy(PiOver2) * arc;
                        Vector2 purifyingMatterVelocity = Projectile.SafeDirectionTo(target.Center) * (Abs(arc) * -0.56f + 5f) + perpendicular;
                        Projectile.NewProjectileBetter_InheritedOwner(Projectile.GetSource_FromAI(), Projectile.Center, purifyingMatterVelocity, ModContent.ProjectileType<PurifyingMatter>(), 360, 0f, -1, Projectile.AngleTo(target.Center));
                    }
                }
            }
        }

        UpdateStrings();

        Projectile.Opacity = SmoothStep(0f, 1f, InverseLerp(0f, EnterPhase2_AttackPlayer_FadeOutTime, Projectile.timeLeft - EnterPhase2_AttackPlayer_FadeOutDelay));
        Projectile.scale = Lerp(1.1f, 1f, Projectile.Opacity);

        Time++;
    }

    public void UpdateStrings()
    {
        Dictionary<string, Vector3[]> paletteData = LocalDataManager.Read<Vector3[]>("Content/NPCs/Bosses/NamelessDeity/NamelessDeityPalettes.json");
        for (int i = 0; i < FeatherStrings.Length; i++)
        {
            if (FeatherStrings[i] is null)
            {
                Vector3[] outerIrisPalettesPalette = paletteData["PsychedelicFeatherOuterIrisColors"];
                Vector3[] innerIrisPalettesPalette = paletteData["PsychedelicFeatherInnerIrisColors"];

                FeatherStrings[i] ??= new StringAttachedFeather()
                {
                    String = new VerletSimulatedRope(Projectile.Center, Vector2.Zero, 11, i == 1 ? 7f : 30f),
                    OuterIrisColor = Main.rand.Next(outerIrisPalettesPalette),
                    InnerIrisColor = Main.rand.Next(innerIrisPalettesPalette)
                };
            }
        }

        float radius = Projectile.width * Projectile.scale * 0.475f;
        for (int i = 0; i < 2; i++)
        {
            FeatherStrings[0].Update(Projectile.Center + Projectile.velocity + Vector2.UnitY.RotatedBy(-0.51f) * radius);
            FeatherStrings[1].Update(Projectile.Center + Projectile.velocity + Vector2.UnitY * (radius + Projectile.scale * 322f));
            FeatherStrings[2].Update(Projectile.Center + Projectile.velocity + Vector2.UnitY.RotatedBy(0.42f) * radius);
        }
    }

    public void DrawDreamcatcher(Vector2 worldPosition, Vector2 size, int stringCount, float rotation)
    {
        ManagedShader dreamcatcherShader = ShaderManager.GetShader("NoxusBoss.DreamcatcherShader");
        dreamcatcherShader.TrySetParameter("pixelationFactor", Vector2.One * 1.25f / Projectile.Size);
        dreamcatcherShader.TrySetParameter("stringCount", stringCount);
        dreamcatcherShader.TrySetParameter("localTime", Main.GlobalTimeWrappedHourly + size.X * 0.00381f + Projectile.identity * 0.174f);
        dreamcatcherShader.TrySetParameter("psychedelicRingZoom", 1.1f);
        dreamcatcherShader.TrySetParameter("psychedelicPulseRadius", 0.14f);
        dreamcatcherShader.TrySetParameter("psychedelicWarpInfluence", 0.19f);
        dreamcatcherShader.TrySetParameter("psychedelicPulseAnimationSpeed", 1.24f);
        dreamcatcherShader.SetTexture(PerlinNoise, 1, SamplerState.LinearWrap);
        dreamcatcherShader.SetTexture(GennedAssets.Textures.Extra.Psychedelic, 2, SamplerState.LinearWrap);
        dreamcatcherShader.Apply();
        Main.spriteBatch.Draw(WhitePixel, worldPosition - Main.screenPosition, null, Projectile.GetAlpha(new(211, 177, 76)), rotation, WhitePixel.Size() * 0.5f, size, 0, 0f);
    }

    public void ManageScreenShader()
    {
        bool warpEnabled = ModContent.GetInstance<Config>().ScreenshakeModifier >= 40f && !WoTGConfig.Instance.PhotosensitivityMode;
        if (!Main.UseHeatDistortion)
            warpEnabled = false;

        ManagedScreenFilter psychedelicShader = ShaderManager.GetFilter("NoxusBoss.PsychedelicShader");
        psychedelicShader.TrySetParameter("intensity", InverseLerp(0f, 60f, Time) * Projectile.Opacity);
        psychedelicShader.TrySetParameter("baseWarpOffsetMax", warpEnabled ? 0.0044f : 0f);
        psychedelicShader.TrySetParameter("colorInfluenceFactor", 48f);
        psychedelicShader.TrySetParameter("zoom", Main.GameViewMatrix.Zoom);
        psychedelicShader.TrySetParameter("dreamcatcherCenter", Vector2.Transform(Projectile.Center - Main.screenPosition, Main.GameViewMatrix.TransformationMatrix));
        psychedelicShader.SetTexture(PerlinNoise, 1, SamplerState.LinearWrap);
        psychedelicShader.SetTexture(GennedAssets.Textures.Extra.Psychedelic, 2, SamplerState.LinearWrap);
        psychedelicShader.SetTexture(ModContent.GetInstance<PurifyingMatterMetaball>().LayerTargets[0], 3, SamplerState.LinearWrap);
        psychedelicShader.Activate();
    }

    public void DrawLineWithBead(Vector2 start, Vector2 end, Color color, float width, params float[] beadOffsetInterpolants)
    {
        Main.spriteBatch.DrawLineBetter(start, end, color, width);

        Texture2D beadTexture = TextureAssets.Projectile[Type].Value;
        float beadRotation = start.AngleTo(end) + PiOver2;
        float beadScale = 0.16f;
        for (int i = 0; i < beadOffsetInterpolants.Length; i++)
        {
            Vector2 beadDrawPosition = Vector2.Lerp(start, end, beadOffsetInterpolants[i]) - Main.screenPosition;
            Main.spriteBatch.Draw(beadTexture, beadDrawPosition, null, Projectile.GetAlpha(Color.White), beadRotation, beadTexture.Size() * 0.5f, beadScale, 0, 0f);
        }
    }

    public override bool PreDraw(ref Color lightColor)
    {
        ManageScreenShader();

        Main.spriteBatch.PrepareForShaders(BlendState.NonPremultiplied);

        Vector2 baseSideDreamcatcherOffset = Vector2.UnitY * Projectile.scale * 75f;
        float leftScale = 0.28f;
        Vector2 leftDirection = new Vector2(-0.5f, 0.75f).SafeNormalize(Vector2.Zero);
        Vector2 leftEdge = Projectile.Center + leftDirection * Projectile.width * Projectile.scale * 0.47f;
        Vector2 leftDreamcatcherPosition = leftEdge + leftDirection * Projectile.width * Projectile.scale * leftScale * 0.5f + baseSideDreamcatcherOffset;

        float rightScale = 0.24f;
        Vector2 rightDirection = new Vector2(0.5f, 0.75f).SafeNormalize(Vector2.Zero);
        Vector2 rightEdge = Projectile.Center + rightDirection * Projectile.width * Projectile.scale * 0.47f;
        Vector2 rightDreamcatcherPosition = rightEdge + rightDirection * Projectile.width * Projectile.scale * rightScale * 0.5f + baseSideDreamcatcherOffset;

        float bottomScale = 0.374f;
        Vector2 bottomEdge = Projectile.Center + Vector2.UnitY * Projectile.width * Projectile.scale * 0.47f;
        Vector2 bottomDreamcatcherPosition = bottomEdge + Vector2.UnitY * Projectile.scale * 220f;

        // Draw strings and feathers.
        Color stringColor = Projectile.GetAlpha(Color.Wheat);
        Main.pixelShader.CurrentTechnique.Passes[0].Apply();
        DrawLineWithBead(leftDreamcatcherPosition, leftDreamcatcherPosition - Vector2.UnitY * Projectile.scale * leftScale * 680f, stringColor, 2.5f, 0.55f);
        DrawLineWithBead(rightDreamcatcherPosition, rightDreamcatcherPosition - Vector2.UnitY * Projectile.scale * rightScale * 680f, stringColor, 2.5f, 0.55f);
        DrawLineWithBead(bottomEdge, bottomDreamcatcherPosition - Vector2.UnitY * bottomScale * 280f, stringColor, 2.5f, 0.6f, 0.85f);

        Texture2D beadTexture = TextureAssets.Projectile[Type].Value;
        for (int i = 0; i < FeatherStrings.Length; i++)
        {
            Main.pixelShader.CurrentTechnique.Passes[0].Apply();

            List<VerletSimulatedSegment> featherString = FeatherStrings[i].String.Rope;
            for (int j = 0; j < featherString.Count - 1; j++)
                Main.spriteBatch.DrawLineBetter(featherString[j].Position, featherString[j + 1].Position, stringColor, 2.5f);

            int featherFrame = (i + 1) % 2;
            float featherRotation = featherString[^1].Position.AngleTo(featherString[^2].Position);
            Vector2 featherScale = Vector2.One * 0.75f;
            Vector2 featherDrawPosition = featherString.Last().Position - Main.screenPosition - featherRotation.ToRotationVector2() * featherScale * 100f;
            if (featherFrame == 0)
                featherDrawPosition += (featherRotation + PiOver2).ToRotationVector2() * featherScale * -13f;
            if (featherFrame == 1)
                featherDrawPosition += (featherRotation + PiOver2).ToRotationVector2() * featherScale * -4f;

            PsychedelicFeather.DrawFeather(featherDrawPosition, featherScale, FeatherStrings[i].PupilOffset, FeatherStrings[i].IdealPupilOffset,
                FeatherStrings[i].OuterIrisColor, FeatherStrings[i].InnerIrisColor, Projectile.GetAlpha(Color.White), featherRotation, Main.GlobalTimeWrappedHourly, 0f, 0f, featherFrame);

            Main.pixelShader.CurrentTechnique.Passes[0].Apply();

            float beadRotation = PiOver2;
            float beadScale = 0.16f;
            Vector2 beadDrawPosition = featherString[^7].Position - Main.screenPosition;
            Main.spriteBatch.Draw(beadTexture, beadDrawPosition, null, Projectile.GetAlpha(Color.White), beadRotation, beadTexture.Size() * 0.5f, beadScale, 0, 0f);
        }

        // Draw the main dreamcatcher.
        DrawDreamcatcher(Projectile.Center, Projectile.Size * Projectile.scale, 12, 0f);

        // Draw the side dreamcatchers.
        DrawDreamcatcher(leftDreamcatcherPosition, Projectile.Size * leftScale, 5, -Main.GlobalTimeWrappedHourly * 0.25f);
        DrawDreamcatcher(rightDreamcatcherPosition, Projectile.Size * rightScale, 9, Main.GlobalTimeWrappedHourly * 0.4f);

        // Draw the bottom dreamcatcher.
        DrawDreamcatcher(bottomDreamcatcherPosition, Projectile.Size * bottomScale, 9, Main.GlobalTimeWrappedHourly * 0.93f);

        Main.spriteBatch.ResetToDefault();

        return false;
    }
}
