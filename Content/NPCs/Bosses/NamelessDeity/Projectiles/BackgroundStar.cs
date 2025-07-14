using Luminance.Common.DataStructures;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity.SpecificEffectManagers;
using NoxusBoss.Core.AdvancedProjectileOwnership;
using NoxusBoss.Core.DataStructures;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.NamelessDeity.Projectiles;

public class BackgroundStar : ModProjectile, IPixelatedPrimitiveRenderer, IProjOwnedByBoss<NamelessDeityBoss>
{
    public Vector2 ScreenDestinationOffset
    {
        get;
        set;
    }

    public Vector2 WorldDestination => NamelessDeityBoss.Myself is null ? Vector2.Zero : NamelessDeityBoss.Myself.GetTargetData().Center + ScreenDestinationOffset;

    public static LazyAsset<Texture2D> MyTexture
    {
        get;
        private set;
    }

    public ref float ZPosition => ref Projectile.ai[0];

    public ref float Index => ref Projectile.ai[1];

    public bool ApproachingScreen
    {
        get;
        set;
    }

    public ref float Time => ref Projectile.localAI[0];

    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.RainbowRodBullet}";

    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailingMode[Type] = 2;
        ProjectileID.Sets.TrailCacheLength[Type] = 13;

        if (Main.netMode != NetmodeID.Server)
            MyTexture = LazyAsset<Texture2D>.FromPath(Texture);
    }

    public override void SetDefaults()
    {
        Projectile.width = 30;
        Projectile.height = 30;
        Projectile.penetrate = -1;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.hostile = true;
        Projectile.MaxUpdates = 2;
        Projectile.timeLeft = Projectile.MaxUpdates * 360;
        Projectile.hide = true;
    }

    public override void SendExtraAI(BinaryWriter writer)
    {
        writer.Write(ApproachingScreen);
        writer.WriteVector2(ScreenDestinationOffset);
    }

    public override void ReceiveExtraAI(BinaryReader reader)
    {
        ApproachingScreen = reader.ReadBoolean();
        ScreenDestinationOffset = reader.ReadVector2();
    }

    public override void AI()
    {
        // Determine the scale of the star based on its Z position.
        Projectile.scale = 2f / (ZPosition + 1f);

        // Create fire on the first frame.
        if (Projectile.localAI[1] == 0f)
        {
            if (Main.netMode != NetmodeID.Server)
            {
                for (int i = 0; i < 10; i++)
                {
                    Color fireColor = Color.Lerp(Color.Aqua, Color.Wheat, Main.rand.NextFloat(0.67f));
                    NamelessFireParticleSystemManager.ParticleSystem.CreateNew(Projectile.Center, Main.rand.NextVector2Circular(18f, 18f), Vector2.One * Main.rand.NextFloat(50f, 100f), fireColor);
                }
            }

            Projectile.localAI[1] = 1f;
        }

        // Get close to the screen if instructed to do so.
        if (ApproachingScreen)
        {
            ZPosition = Lerp(ZPosition, -0.93f, 0.23f);
            Projectile.velocity = Projectile.SafeDirectionTo(WorldDestination) * Clamp(Projectile.velocity.Length() + 5.4f, 7f, 99f);

            if (ZPosition <= -0.92f)
            {
                if (ScreenShakeSystem.OverallShakeIntensity < 8f)
                    ScreenShakeSystem.StartShakeAtPoint(Projectile.Center, 3f);

                SoundEngine.PlaySound(GennedAssets.Sounds.NamelessDeity.Supernova with { Volume = 0.8f, MaxInstances = 20 }, Projectile.Center);
                SoundEngine.PlaySound(GennedAssets.Sounds.NamelessDeity.GenericBurst with { Volume = 1.3f, PitchVariance = 0.15f }, Projectile.Center);
                if (Main.netMode != NetmodeID.MultiplayerClient && NamelessDeityBoss.Myself is not null)
                {
                    // Create the start explosion.
                    int starIndex = Projectile.NewProjectileBetter_InheritedOwner(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero, ModContent.ProjectileType<ExplodingStar>(), 0, 0f, -1, 1.089f);
                    if (starIndex >= 0 && starIndex < Main.maxProjectiles)
                        Main.projectile[starIndex].As<ExplodingStar>().Time = 18f;

                    // Explode into somewhat slow moving spark patterns.
                    int sparkCount = 4;
                    float sparkSpeed = 3f;
                    if (Main.zenithWorld)
                    {
                        sparkCount = 9;
                        sparkSpeed = 9.6f;
                    }

                    sparkCount = (int)(sparkCount * Sqrt(NamelessDeityBoss.Myself_DifficultyFactor));

                    float angleToTarget = Projectile.AngleTo(NamelessDeityBoss.Myself.GetTargetData().Center);
                    for (int i = 0; i < sparkCount; i++)
                    {
                        Vector2 sparkVelocity = (TwoPi * i / sparkCount + angleToTarget).ToRotationVector2() * sparkSpeed;
                        Projectile.NewProjectileBetter_InheritedOwner(Projectile.GetSource_FromThis(), WorldDestination, sparkVelocity, ModContent.ProjectileType<SlowSolarSpark>(), NamelessDeityBoss.StarburstDamage, 0f);
                    }

                    NamelessDeitySky.HeavenlyBackgroundIntensity += 0.85f;
                    NamelessDeityKeyboardShader.BrightnessIntensity = 0.8f;
                }

                Projectile.Kill();
            }
        }

        // Fade in based on how long the starburst has existed.
        Projectile.Opacity = InverseLerp(0f, 12f, Time);

        // Make the opacity weaker depending on how close the star is to the background.
        Projectile.Opacity *= Utils.Remap(ZPosition, 0.8f, 3.4f, 1f, 0.5f);

        Time++;
    }

    public float FlameTrailWidthFunction(float completionRatio)
    {
        return SmoothStep(25f, 5f, completionRatio) * Pow(Projectile.scale, 0.6f) * Projectile.Opacity;
    }

    public Color FlameTrailColorFunction(float completionRatio)
    {
        // Make the trail fade out at the end and fade in sharply at the start, to prevent the trail having a definitive, flat "start".
        float trailOpacity = InverseLerpBump(0f, 0.067f, 0.27f, 0.75f, completionRatio) * 0.9f;

        // Interpolate between a bunch of colors based on the completion ratio.
        Color startingColor = Color.Lerp(Color.White, Color.Cyan, 0.25f);
        Color middleColor = Color.Lerp(Color.SkyBlue, Color.Yellow, 0.4f);
        Color endColor = Color.Lerp(Color.Purple, Color.Black, 0.35f);

        Palette palette = new Palette(startingColor, middleColor, endColor);
        Color color = palette.SampleColor(completionRatio) * trailOpacity;

        color.A = (byte)(trailOpacity * 255);
        return color * Projectile.Opacity;
    }

    public void DrawBloomFlare()
    {
        float bloomFlareRotation = Main.GlobalTimeWrappedHourly * 1.1f + Projectile.identity;
        float scale = Pow(Projectile.scale, 0.77f);

        float colorInterpolant = Projectile.identity / 13f % 1f;
        Color bloomFlareColor1 = Color.Lerp(Color.SkyBlue, Color.Orange, colorInterpolant % 0.6f) with { A = 0 } * Projectile.Opacity * 0.54f;
        Color bloomFlareColor2 = Color.Lerp(Color.Cyan, Color.White, colorInterpolant % 0.6f) with { A = 0 } * Projectile.Opacity * 0.81f;

        Vector2 bloomFlareDrawPosition = Projectile.Center - Main.screenPosition;
        Main.spriteBatch.Draw(BloomFlare, bloomFlareDrawPosition, null, bloomFlareColor1, bloomFlareRotation, BloomFlare.Size() * 0.5f, scale * 0.13f, 0, 0f);
        Main.spriteBatch.Draw(BloomFlare, bloomFlareDrawPosition, null, bloomFlareColor2, -bloomFlareRotation, BloomFlare.Size() * 0.5f, scale * 0.146f, 0, 0f);
    }

    public override bool PreDraw(ref Color lightColor)
    {
        // Draw a bloom flare behind the starburst.
        DrawBloomFlare();

        Texture2D texture = MyTexture.Value;
        Vector2 drawPosition = Projectile.Center - Main.screenPosition;
        Color color = Projectile.GetAlpha(Color.Wheat) with { A = 0 };
        Rectangle frame = texture.Frame(1, Main.projFrames[Type], 0, Projectile.frame);
        Main.spriteBatch.Draw(texture, drawPosition, frame, color, Projectile.rotation, frame.Size() * 0.5f, Sqrt(Projectile.scale), 0, 0f);
        return false;
    }

    public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs, List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI)
    {
        behindNPCsAndTiles.Add(index);
    }

    public void RenderPixelatedPrimitives(SpriteBatch spriteBatch)
    {
        if (ZPosition >= 0f)
            return;

        ManagedShader shader = ShaderManager.GetShader("NoxusBoss.MoltenFlameTrail");
        shader.SetTexture(GennedAssets.Textures.TrailStreaks.StreakMagma, 1, SamplerState.LinearWrap);

        PrimitiveSettings settings = new PrimitiveSettings(FlameTrailWidthFunction, FlameTrailColorFunction, _ => Projectile.Size * 0.5f, Pixelate: true, Shader: shader);
        PrimitiveRenderer.RenderTrail(Projectile.oldPos, settings, 8);
    }
}
