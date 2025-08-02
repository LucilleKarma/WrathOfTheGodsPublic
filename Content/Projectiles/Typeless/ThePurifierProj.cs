using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Content.Items.MiscOPTools;
using NoxusBoss.Content.Particles;
using NoxusBoss.Core.Graphics;
using NoxusBoss.Core.Graphics.GeneralScreenEffects;
using NoxusBoss.Core.Graphics.SpecificEffectManagers;
using NoxusBoss.Core.World.GameScenes.PurifierMonologue;
using NoxusBoss.Core.World.Subworlds;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.WorldBuilding;

namespace NoxusBoss.Content.Projectiles.Typeless;

public class ThePurifierProj : ModProjectile, IPixelatedPrimitiveRenderer
{
    public class ChargingEnergyStreak
    {
        public float Opacity = 1f;

        public float BaseWidth;

        public float SpeedInterpolant;

        public Color GeneralColor;

        public Vector2 CurrentOffset;

        public Vector2 StartingOffset;

        public ChargingEnergyStreak(float speedInterpolant, float baseWidth, Color generalColor, Vector2 startingOffset)
        {
            // Initialize things.
            SpeedInterpolant = speedInterpolant;
            BaseWidth = baseWidth;
            GeneralColor = generalColor;
            CurrentOffset = startingOffset;
            StartingOffset = startingOffset;
        }

        public void Update()
        {
            CurrentOffset = Vector2.Lerp(CurrentOffset, Vector2.Zero, SpeedInterpolant);
            CurrentOffset = Utils.MoveTowards(CurrentOffset, Vector2.Zero, SpeedInterpolant * 19f);

            if (CurrentOffset.Length() <= 8f)
            {
                StartingOffset = Vector2.Lerp(StartingOffset, Vector2.Zero, SpeedInterpolant * 1.4f);
                Opacity = Saturate(Opacity * 0.94f - 0.09f);
            }
        }

        public float EnergyWidthFunction(float completionRatio) => BaseWidth - (1f - completionRatio) * 3f;

        public Color EnergyColorFunction(float _) => GeneralColor with { A = 0 } * Opacity;
    }

    public List<ChargingEnergyStreak> EnergyStreaks = [];

    public float AnimationCompletion => Time / Lifetime;

    public ref float Time => ref Projectile.ai[0];

    public const int Lifetime = 900;

    public override string Texture => GetAssetPath("Content/Items/MiscOPTools", ModContent.GetInstance<ThePurifier>().Name);

    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.DrawScreenCheckFluff[Type] = 600;
    }

    public override void SetDefaults()
    {
        Projectile.width = 10;
        Projectile.height = 10;
        Projectile.penetrate = -1;
        Projectile.timeLeft = Lifetime;
        Projectile.Opacity = 0f;
        Projectile.tileCollide = false;
    }

    public override void AI()
    {
        // Immediately disappear if in the garden subworld or about to enter it.
        if (EternalGardenUpdateSystem.WasInSubworldLastUpdateFrame || AnyProjectiles(ModContent.ProjectileType<TerminusProj>()))
        {
            Projectile.active = false;
            return;
        }

        // Play the buildup sound on the first frame.
        // If in multiplayer, broadcast a warning as well.
        if (Projectile.localAI[0] == 0f)
        {
            if (Main.netMode == NetmodeID.Server)
                CreateDeploymentAlert();
            SoundEngine.PlaySound(GennedAssets.Sounds.Item.PurifierBuildup);
            Projectile.localAI[0] = 1f;
        }

        // Fade in.
        Projectile.Opacity = InverseLerp(0f, 8f, Time);

        // Jitter before exploding.
        float jitter = InverseLerp(0.6f, 0.9f, AnimationCompletion) * 3f;
        Projectile.Center += Main.rand.NextVector2Circular(1f, 1f) * jitter;

        // Shake the screen.
        ScreenShakeSystem.SetUniversalRumble(AnimationCompletion * 10f, TwoPi, null, 0.2f);

        // Create chromatic aberration effects.
        if (Time % 20f == 19f)
            GeneralScreenEffectSystem.ChromaticAberration.Start(Projectile.Center, AnimationCompletion * 2f, 15);

        // Create pulse rings and bloom periodically.
        if (Time % 15f == 0f)
        {
            // Play pulse sounds.
            float suckFadeIn = InverseLerp(0.1f, 0.3f, AnimationCompletion);
            if (suckFadeIn >= 0.01f)
                SoundEngine.PlaySound(GennedAssets.Sounds.NamelessDeity.BossRushPulse with { PitchVariance = 0.3f, MaxInstances = 10, Volume = AnimationCompletion * suckFadeIn + 0.01f });

            Color energyColor = Color.Lerp(Color.Wheat, Color.Red, Main.rand.NextFloat(0.5f)) * suckFadeIn;
            PulseRing ring = new PulseRing(Projectile.Center, Vector2.Zero, energyColor, 4.2f, 0f, 32);
            ring.Spawn();

            StrongBloom bloom = new StrongBloom(Projectile.Center, Vector2.Zero, energyColor, AnimationCompletion * 4.5f + 0.5f, 30);
            bloom.Spawn();

            StrongBloom bloomBright = new StrongBloom(Projectile.Center, Vector2.Zero, Color.White * InverseLerp(0.45f, 0.75f, AnimationCompletion), 4f, 45);
            bloomBright.Spawn();
        }

        // Rotate and gradually slow down.
        if (Time <= 10f)
            Projectile.rotation = Projectile.velocity.ToRotation();
        else
            Projectile.rotation += Projectile.velocity.X * 0.02f;
        Projectile.velocity *= 0.978f;

        // Update all streaks.
        EnergyStreaks.RemoveAll(s => s.Opacity <= 0.003f);
        for (int i = 0; i < EnergyStreaks.Count; i++)
            EnergyStreaks[i].Update();

        // Create energy streaks at a rate proportional to the animation completion.
        if (AnimationCompletion >= 0.1f)
        {
            for (int i = 0; i < 3; i++)
            {
                if (Main.rand.NextFloat() >= AnimationCompletion)
                    continue;

                float streakSpeedInterpolant = Main.rand.NextFloat(0.15f, 0.19f);
                float streakWidth = Main.rand.NextFloat(4f, 5.6f);
                Vector2 streakOffset = Main.rand.NextVector2Unit() * Main.rand.NextFloat(250f, 600f);
                Color streakColor = Color.Lerp(Color.Wheat, Color.IndianRed, Main.rand.NextFloat(0.74f));
                EnergyStreaks.Add(new(streakSpeedInterpolant, streakWidth, streakColor * InverseLerp(0.1f, 0.45f, AnimationCompletion), streakOffset));
            }
        }

        // Make everything go bright before the explosion happens.
        TotalScreenOverlaySystem.OverlayInterpolant = InverseLerp(0.97f, 0.99f, AnimationCompletion);

        Time++;
    }

    public void CreateDeploymentAlert()
    {
        string playerWhoWillBeBlamed = Main.player[Projectile.owner].name;

        // Randomly blame a random player if there are more than three people present.
        List<Player> activePlayers = Main.player.Where(p => p.active && !p.dead).ToList();
        if (Main.rand.NextBool() && activePlayers.Count >= 3)
            playerWhoWillBeBlamed = Main.rand.Next(activePlayers).name;

        string text = Language.GetText($"Mods.NoxusBoss.Dialog.PurifierMultiplayerUseAlertText").Format(playerWhoWillBeBlamed);
        BroadcastText(text, DialogColorRegistry.PurifierWarningTextColor);
    }

    public override void OnKill(int timeLeft)
    {
        WorldGen.generatingWorld = true;
        SoundEngine.PlaySound(GennedAssets.Sounds.NamelessDeity.ScreamLong with { Volume = 1.2f });

        // Kick clients out.
        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            WorldGen.generatingWorld = false;
            Netplay.Disconnect = true;
            Main.netMode = NetmodeID.SinglePlayer;
        }

        // Block player inputs.
        BlockerSystem.Start(true, false, () => WorldGen.generatingWorld);

        GenerationProgress _ = new GenerationProgress();
        new Thread(context =>
        {
            WorldGen.worldGenCallback(context);
            PurifierMonologueDrawer.TimeSinceWorldgenFinished = 1;
        }).Start(_);
    }

    public override Color? GetAlpha(Color lightColor)
    {
        Color baseColor = Main.hslToRgb(Main.GlobalTimeWrappedHourly * 0.57f % 1f, 0.8f, 0.9f);
        return baseColor * Projectile.Opacity;
    }

    public override bool PreDraw(ref Color lightColor)
    {
        float suckPulse = 1f - Main.GlobalTimeWrappedHourly * 4.8f % 1f;
        float suckRotation = Main.GlobalTimeWrappedHourly * -3f;
        float suckFadeIn = InverseLerp(0.1f, 0.25f, AnimationCompletion);
        Color suckColor = Color.Wheat * InverseLerpBump(0.05f, 0.25f, 0.67f, 1f, suckPulse) * Projectile.Opacity * suckFadeIn;
        suckColor.A = 0;

        Main.spriteBatch.Draw(ChromaticBurst, Projectile.Center - Main.screenPosition, null, suckColor, suckRotation, ChromaticBurst.Size() * 0.5f, Vector2.One * suckPulse * 2.6f, 0, 0f);
        return true;
    }

    public void RenderPixelatedPrimitives(SpriteBatch spriteBatch)
    {
        // Configure the streak shader's texture.
        ManagedShader streakShader = ShaderManager.GetShader("NoxusBoss.GenericTrailStreak");
        streakShader.SetTexture(GennedAssets.Textures.TrailStreaks.StreakBloomLine, 1);

        // Draw energy streaks as primitives.
        Vector2 drawCenter = Projectile.Center - Vector2.UnitY.RotatedBy(Projectile.rotation) * Projectile.scale * 6f;
        for (int i = 0; i < EnergyStreaks.Count; i++)
        {
            ChargingEnergyStreak streak = EnergyStreaks[i];
            Vector2 start = streak.StartingOffset;
            Vector2 end = streak.CurrentOffset;
            Vector2 midpoint1 = Vector2.Lerp(start, end, 0.2f);
            Vector2 midpoint2 = Vector2.Lerp(start, end, 0.4f);
            Vector2 midpoint3 = Vector2.Lerp(start, end, 0.6f);
            Vector2 midpoint4 = Vector2.Lerp(start, end, 0.8f);
            PrimitiveSettings settings = new PrimitiveSettings(streak.EnergyWidthFunction, streak.EnergyColorFunction, _ => drawCenter, Pixelate: true, Shader: streakShader);
            PrimitiveRenderer.RenderTrail(new Vector2[]
            {
                end, midpoint4, midpoint3, midpoint2, midpoint1, start
            }, settings, 27);
        }
    }
}
