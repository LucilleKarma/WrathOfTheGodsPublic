using Luminance.Common.Easings;
using Luminance.Core.Graphics;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using NoxusBoss.Assets;
using NoxusBoss.Content.NPCs.Bosses.Avatar.FirstPhaseForm;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SpecificEffectManagers;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity;
using NoxusBoss.Content.Particles;
using NoxusBoss.Content.Projectiles;
using NoxusBoss.Core.Configuration;
using NoxusBoss.Core.DataStructures;
using NoxusBoss.Core.Graphics.SpecificEffectManagers;
using NoxusBoss.Core.Netcode;
using NoxusBoss.Core.Netcode.Packets;

using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Core.Graphics.GenesisEffects;

public class GenesisVisualsSystem : ModSystem
{
    /// <summary>
    /// The activation phase of the Genesis.
    /// </summary>
    public static GenesisActivationPhase ActivationPhase
    {
        get;
        internal set;
    } = GenesisActivationPhase.Inactive;

    /// <summary>
    /// The amount of frames it's been since the Genesis effect began.
    /// </summary>
    public static int Time
    {
        get;
        internal set;
    }

    /// <summary>
    /// The position at which visual effects, such as the firing of the laser, are performed relative to.
    /// </summary>
    public static Vector2 Position
    {
        get;
        internal set;
    }

    /// <summary>
    /// Whether the Genesis can be activated or not.
    /// </summary>
    public static bool CanBeActivated => AvatarRift.Myself is null && AvatarOfEmptiness.Myself is null && NamelessDeityBoss.Myself is null && !EffectActive;

    /// <summary>
    /// The factor by which the glow intensity shader should be relative to.
    /// </summary>
    public static float GlowIntensityFactor => WoTGConfig.Instance.PhotosensitivityMode ? 0.35f : 1f;

    /// <summary>
    /// Whether the Genesis effect is ongoing.
    /// </summary>
    public static bool EffectActive => ActivationPhase != GenesisActivationPhase.Inactive;

    /// <summary>
    /// The palette that the energy particle streaks can cycle through.
    /// </summary>
    public static readonly Palette StreakParticlePalette = new Palette().
        AddColor(new Color(112, 30, 255)).
        AddColor(new Color(246, 240, 177)).
        AddColor(new Color(40, 40, 40));

    public override void PreUpdateEntities()
    {
        if (!EffectActive)
            return;

        switch (ActivationPhase)
        {
            case GenesisActivationPhase.EnergyChargeUp:
                DoBehavior_EnergyChargeup();
                break;
            case GenesisActivationPhase.MomentOfSuspense:
                DoBehavior_MomentOfSuspense();
                break;
            case GenesisActivationPhase.LaserFires:
                DoBehavior_LaserFires();
                break;
        }

        Time++;
    }

    /// <summary>
    /// Performs the energy charge-up effect atop the Genesis.
    /// </summary>
    public static void DoBehavior_EnergyChargeup()
    {
        if (Time == 1)
            SoundEngine.PlaySound(GennedAssets.Sounds.Genesis.GenesisCharge);

        // Keep the overall timings of this at 7.55 seconds, per Moonburn's request. This includes the moment of suspense.
        int backgroundDimTime = 210;
        int energyChargeUpTime = 300;
        int idleEnergyTime = 138;
        float energyChargeUpCompletion = InverseLerp(0f, energyChargeUpTime, Time);

        GenesisSky.BackgroundDimness = InverseLerp(0f, backgroundDimTime, Time);

        float opacity = energyChargeUpCompletion;
        for (int i = 0; i < energyChargeUpCompletion * 50f; i++)
        {
            if (Main.rand.NextBool(Sqrt(energyChargeUpCompletion)))
            {
                Vector2 streakPosition = Position + Main.rand.NextVector2Unit() * Main.rand.NextFloat(100f, energyChargeUpCompletion * 600f + 540f);
                Vector2 streakVelocity = (Position - streakPosition) * 0.085f;
                Vector2 startingScale = new Vector2(0.8f, 0.05f);
                Vector2 endingScale = new Vector2(1.6f, 0.015f);
                LineStreakParticle energy = new LineStreakParticle(streakPosition, streakVelocity, StreakParticlePalette.SampleColor(Main.rand.NextFloat(0.95f)) * opacity, 11, streakVelocity.ToRotation(), startingScale, endingScale);
                energy.Spawn();
            }
        }

        if (Main.netMode != NetmodeID.MultiplayerClient && Time % 2 == 0 && Time < energyChargeUpTime + idleEnergyTime - 105)
        {
            for (int i = 0; i < 5; i++)
            {
                if (energyChargeUpCompletion <= 0.25f || !Main.rand.NextBool(energyChargeUpCompletion.Cubed()))
                    continue;

                float energySpawnRadius = 1000f + Main.rand.NextFloat(energyChargeUpCompletion * 1550f);
                Vector2 energySpawnPosition = Position + Main.rand.NextVector2CircularEdge(energySpawnRadius, energySpawnRadius) + Main.rand.NextVector2Circular(100f, 100f);
                NewProjectileBetter(new EntitySource_WorldEvent(), energySpawnPosition, Vector2.Zero, ModContent.ProjectileType<GenesisConvergingEnergy>(), 0, 0f);
            }
        }

        if (Time % 4 == 0)
        {
            float startingScale = Lerp(1.3f, 4f, energyChargeUpCompletion);
            Color startingColor = StreakParticlePalette.SampleColor(Main.rand.NextFloat(0.95f));
            Color endingColor = StreakParticlePalette.SampleColor(Main.rand.NextFloat(0.95f));
            ExpandingEnergyRingParticle ring = new ExpandingEnergyRingParticle(Position, startingColor * opacity, endingColor * opacity * 0.4f, startingScale, 0.05f, EasingCurves.Quadratic, EasingType.Out, 20);
            ring.Spawn();
        }

        if (Main.netMode != NetmodeID.Server)
        {
            float blurIntensity = energyChargeUpCompletion.Cubed() * Lerp(0.5f, 1.5f, Cos01(TwoPi * Time / 30f)) * 0.5f;
            ManagedScreenFilter waveShader = ShaderManager.GetFilter("NoxusBoss.GenesisWaveMotionBlurShader");
            waveShader.TrySetParameter("blurOrigin", WorldSpaceToScreenUV(Position));
            waveShader.TrySetParameter("blurIntensity", blurIntensity);
            waveShader.TrySetParameter("glowIntensity", Lerp(0.05f, 0.11f, energyChargeUpCompletion) * GlowIntensityFactor);
            waveShader.TrySetParameter("blurEnabled", !WoTGConfig.Instance.PhotosensitivityMode);
            waveShader.TrySetParameter("glowColor", new Color(193, 108, 255).ToVector4());
            waveShader.SetTexture(TileTargetManagers.TileTarget, 1, SamplerState.PointClamp);
            waveShader.Activate();

            ScreenShakeSystem.StartShakeAtPoint(Position, Pow(energyChargeUpCompletion, 1.5f) * 5f, TwoPi, Vector2.UnitX, 0.3f, 7500f, 5000f);
        }

        if (Time >= energyChargeUpTime + idleEnergyTime)
        {
            Time = 0;
            ActivationPhase = GenesisActivationPhase.MomentOfSuspense;

            if (Main.netMode != NetmodeID.SinglePlayer)
                PacketManager.SendPacket<GenesisAnimationStatePacket>();
        }
    }

    /// <summary>
    /// Performs the energy moment of suspense effect.
    /// </summary>
    public static void DoBehavior_MomentOfSuspense()
    {
        int suspenseTime = 15;

        if (Main.netMode != NetmodeID.Server)
        {
            ManagedScreenFilter waveShader = ShaderManager.GetFilter("NoxusBoss.GenesisWaveMotionBlurShader");
            waveShader.TrySetParameter("blurOrigin", WorldSpaceToScreenUV(Position));
            waveShader.TrySetParameter("blurIntensity", InverseLerp(15f, 0f, Time));
            waveShader.TrySetParameter("glowIntensity", GlowIntensityFactor * 0.08f);
            waveShader.TrySetParameter("blurEnabled", !WoTGConfig.Instance.PhotosensitivityMode);
            waveShader.TrySetParameter("glowColor", new Color(193, 108, 255).ToVector4());
            waveShader.SetTexture(TileTargetManagers.TileTarget, 1, SamplerState.PointClamp);
            waveShader.Activate();
        }

        if (Time >= suspenseTime)
        {
            Time = 0;
            ActivationPhase = GenesisActivationPhase.LaserFires;

            if (Main.netMode != NetmodeID.SinglePlayer)
                PacketManager.SendPacket<GenesisAnimationStatePacket>();
        }
    }

    /// <summary>
    /// Performs the laser-firing effect atop the Genesis.
    /// </summary>
    public static void DoBehavior_LaserFires()
    {
        if (Time == 1)
        {
            SoundEngine.PlaySound(GennedAssets.Sounds.Genesis.GenesisFire);
            if (Main.netMode != NetmodeID.MultiplayerClient)
                NewProjectileBetter(new EntitySource_WorldEvent(), Position + Vector2.UnitY * 18f, Vector2.Zero, ModContent.ProjectileType<GenesisOmegaDeathray>(), 0, 0f);
        }

        if (Main.netMode != NetmodeID.Server)
        {
            float cameraPanInterpolant = SmoothStep(0f, 1f, InverseLerp(0f, 45f, Time));
            CameraPanSystem.PanTowards(Position - Vector2.UnitY * 280f, cameraPanInterpolant);

            ScreenShakeSystem.StartShake(7.5f);

            float fadeOut = InverseLerp(GenesisOmegaDeathray.Lifetime, GenesisOmegaDeathray.Lifetime - 10f, Time);
            ManagedScreenFilter waveShader = ShaderManager.GetFilter("NoxusBoss.GenesisWaveMotionBlurShader");
            waveShader.TrySetParameter("blurOrigin", WorldSpaceToScreenUV(Position));
            waveShader.TrySetParameter("blurIntensity", Lerp(1.2f, 1.4f, Cos01(TwoPi * Time / 15f)) * fadeOut);
            waveShader.TrySetParameter("glowIntensity", GlowIntensityFactor * fadeOut * 0.23f);
            waveShader.TrySetParameter("blurEnabled", false);
            waveShader.TrySetParameter("glowColor", new Color(193, 108, 255).ToVector4());
            waveShader.SetTexture(TileTargetManagers.TileTarget, 1, SamplerState.PointClamp);
            waveShader.Activate();
        }

        if (Time >= GenesisOmegaDeathray.Lifetime)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                Player closesstPlayer = Main.player[Player.FindClosest(Position, 1, 1)];
                NPC.NewNPC(new EntitySource_WorldEvent(), (int)closesstPlayer.Center.X - 400, (int)closesstPlayer.Center.Y, ModContent.NPCType<AvatarRift>(), 1);
            }

            AvatarRiftSky.LightningSpawnCountdown = 180;
            ActivationPhase = GenesisActivationPhase.Inactive;
            Time = 0;

            if (Main.netMode == NetmodeID.Server)
                PacketManager.SendPacket<GenesisAnimationStatePacket>();
        }
    }

    /// <summary>
    /// Starts the Genesis visuals at a given position.
    /// </summary>
    /// <param name="position">The point at which the Genesis' visuals should occur.</param>
    public static void Start(Vector2 position)
    {
        if (!CanBeActivated)
            return;

        ActivationPhase = GenesisActivationPhase.EnergyChargeUp;
        Position = position;

        BlockerSystem.Start(true, true, () => EffectActive);

        if (Main.netMode != NetmodeID.SinglePlayer)
            PacketManager.SendPacket<GenesisAnimationStatePacket>();
    }
}
