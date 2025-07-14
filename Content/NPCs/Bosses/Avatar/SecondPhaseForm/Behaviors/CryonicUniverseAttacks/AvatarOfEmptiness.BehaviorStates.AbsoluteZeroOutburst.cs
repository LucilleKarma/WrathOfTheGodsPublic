using Luminance.Common.DataStructures;
using Luminance.Common.Easings;
using Luminance.Common.StateMachines;
using Luminance.Core.Graphics;

using Microsoft.Xna.Framework;

using NoxusBoss.Assets;
using NoxusBoss.Content.Buffs;
using NoxusBoss.Content.NPCs.Bosses.Avatar.Projectiles;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SpecificEffectManagers;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SpecificEffectManagers.SolynDialogue;
using NoxusBoss.Content.NPCs.Friendly;
using NoxusBoss.Content.Particles;
using NoxusBoss.Core.Balancing;
using NoxusBoss.Core.CrossCompatibility.Inbound;
using NoxusBoss.Core.Graphics.GeneralScreenEffects;
using NoxusBoss.Core.Graphics.SpecificEffectManagers;

using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm;

public partial class AvatarOfEmptiness
{
    /// <summary>
    /// How long the Avatar spends having his lily glow during the Absolute Zero Outburst attack.
    /// </summary>
    public static int AbsoluteZeroOutburst_LilyGlowTime => GetAIInt("AbsoluteZeroOutburst_LilyGlowTime");

    /// <summary>
    /// How long the Avatar spends having his lily freeze during the Absolute Zero Outburst attack.
    /// </summary>
    public static int AbsoluteZeroOutburst_LilyFreezeTime => GetAIInt("AbsoluteZeroOutburst_LilyFreezeTime");

    /// <summary>
    /// How long it takes for the Cryonic universe background to begin disappearing during the Avatar's Absolute Zero Outburst attack.
    /// </summary>
    public static int AbsoluteZeroOutburst_BackgroundDisappearStartTime => GetAIInt("AbsoluteZeroOutburst_BackgroundDisappearStartTime");

    /// <summary>
    /// How long it takes for the Cryonic universe background to complete disappearing during the Avatar's Absolute Zero Outburst attack.
    /// </summary>
    public static int AbsoluteZeroOutburst_BackgroundDisappearEndTime => AbsoluteZeroOutburst_LilyFreezeTime - SecondsToFrames(1f);

    /// <summary>
    /// How long it takes for the Avatar to create the frost wave explosion after the chirps during the Absolute Zero Outburst attack.
    /// </summary>
    public static int AbsoluteZeroOutburst_ExplodeDelay => GetAIInt("AbsoluteZeroOutburst_ExplodeDelay");

    /// <summary>
    /// How long the Avatar spends leaving during the Absolute Zero Outburst punishment.
    /// </summary>
    public static int AbsoluteZeroOutburstPunishment_LeaveTime => GetAIInt("AbsoluteZeroOutburstPunishment_LeaveTime");

    /// <summary>
    /// How long the Avatar waits before shattering the player's ice during the Absolute Zero Outburst punishment.
    /// </summary>
    public static int AbsoluteZeroOutburstPunishment_ShatterDelay => GetAIInt("AbsoluteZeroOutburstPunishment_ShatterDelay");

    /// <summary>
    /// How long the Avatar waits after shattering the player's ice during the Absolute Zero Outburst punishment to transition to the next state.
    /// </summary>
    public static int AbsoluteZeroOutburstPunishment_AttackTransitionDelay => GetAIInt("AbsoluteZeroOutburstPunishment_AttackTransitionDelay");

    /// <summary>
    /// The minimum amount of HP the player can be left with after being hit by the Avatar's slam during the Absolute Zero Outburst punishment.
    /// </summary>
    public static int AbsoluteZeroOutburstPunishment_MinPlayerHP => GetAIInt("AbsoluteZeroOutburstPunishment_MinPlayerHPToKeep");

    /// <summary>
    /// The maximum amount of HP the player can be left with after being hit by the Avatar's slam during the Absolute Zero Outburst punishment.
    /// </summary>
    public static int AbsoluteZeroOutburstPunishment_MaxPlayerHP => GetAIInt("AbsoluteZeroOutburstPunishment_MaxPlayerHPToKeep");

    [AutomatedMethodInvoke]
    public void LoadState_AbsoluteZeroOutburst()
    {
        StateMachine.RegisterTransition(AvatarAIType.AbsoluteZeroOutburst, AvatarAIType.AbsoluteZeroOutburstPunishment, false, () =>
        {
            if (AITimer <= 5 && NPC.Center.Y <= 1300f)
                return true;

            return AITimer >= AbsoluteZeroOutburst_LilyFreezeTime + AbsoluteZeroOutburst_ExplodeDelay + FreezingWave.Lifetime;
        });
        StateMachine.RegisterTransition(AvatarAIType.AbsoluteZeroOutburstPunishment, null, false, () =>
        {
            bool endAttackEarly = AITimer <= 5;
            if (endAttackEarly)
            {
                int buffID = ModContent.BuffType<Glaciated>();
                foreach (Player player in Main.ActivePlayers)
                {
                    if (player.HasBuff(buffID))
                        endAttackEarly = false;
                }
            }

            return endAttackEarly || (AITimer >= AbsoluteZeroOutburstPunishment_ShatterDelay + AbsoluteZeroOutburstPunishment_AttackTransitionDelay && ZPosition <= 0.01f);
        });

        // Load the AI state behavior.
        StateMachine.RegisterStateBehavior(AvatarAIType.AbsoluteZeroOutburst, DoBehavior_AbsoluteZeroOutburst);
        StateMachine.RegisterStateBehavior(AvatarAIType.AbsoluteZeroOutburstPunishment, DoBehavior_AbsoluteZeroOutburstPunishment);

        AttackDimensionRelationship[AvatarAIType.AbsoluteZeroOutburst] = AvatarDimensionVariants.CryonicDimension;
        AttackDimensionRelationship[AvatarAIType.AbsoluteZeroOutburstPunishment] = AvatarDimensionVariants.CryonicDimension;

        StatesToNotStartTeleportDuring.Add(AvatarAIType.AbsoluteZeroOutburst);
        StatesToNotStartTeleportDuring.Add(AvatarAIType.AbsoluteZeroOutburstPunishment);
    }

    public void DoBehavior_AbsoluteZeroOutburst()
    {
        LookAt(Target.Center);

        int explosionWaitTime = AbsoluteZeroOutburst_LilyFreezeTime + AbsoluteZeroOutburst_ExplodeDelay;

        ZPosition *= 0.84f;
        NPC.velocity *= 0.9f;

        if (AITimer == 1)
            IProjOwnedByBoss<AvatarOfEmptiness>.KillAll();

        if (AITimer == 2)
        {
            SoundStyle chargeUpSound = CommonCalamityVariables.DeathModeActive ? GennedAssets.Sounds.Avatar.AbsoluteZeroChargeUpDeathMode : GennedAssets.Sounds.Avatar.AbsoluteZeroChargeUp;
            SoundEngine.PlaySound(chargeUpSound with { Volume = 1.8f });
        }

        // Ensure that the player can't just run away, get some free adrenaline, and then melt the Avatar by making it charge slower if the player is far away.
        AdrenalineGrowthModificationSystem.AdrenalineYieldFactor *= InverseLerp(2450f, 800f, NPC.Distance(Main.LocalPlayer.Center));

        float coldReleaseInterpolant = InverseLerp(0f, 10f, AITimer - explosionWaitTime);
        LilyGlowIntensityBoost = InverseLerp(1f, AbsoluteZeroOutburst_LilyGlowTime, AITimer) * 1.6f;
        LilyFreezeInterpolant = InverseLerp(0f, AbsoluteZeroOutburst_LilyFreezeTime, AITimer) * (1f - coldReleaseInterpolant);
        AmbientSoundVolumeFactor = 1f - coldReleaseInterpolant;
        AvatarOfEmptinessSky.WindSpeedFactor = Lerp(1f, 20f, LilyFreezeInterpolant) * (1f - coldReleaseInterpolant);

        // Make the screen rumble before the lily completely freezes.
        if (LilyFreezeInterpolant <= 0.95f)
            ScreenShakeSystem.SetUniversalRumble(LilyFreezeInterpolant.Squared() * 10f, TwoPi, null, 0.225f);

        // Create particles and converging energy.
        if (Main.rand.NextBool(LilyFreezeInterpolant.Squared()) && LilyFreezeInterpolant <= 0.8f)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                Vector2 snowEnergySpawnPosition = SpiderLilyPosition + Main.rand.NextVector2Unit() * Main.rand.NextFloat(1075f, 3300f);
                NewProjectileBetter(NPC.GetSource_FromAI(), snowEnergySpawnPosition, Vector2.Zero, ModContent.ProjectileType<ConvergingSnowEnergy>(), 0, 0f);
            }

            for (int i = 0; i < 4; i++)
                DoBehavior_AbsoluteZeroOutburst_CreateGleam(NPC.Center, 1200f, 30, 45);

            TwinkleParticle twinkle = new TwinkleParticle(NPC.Center + Main.rand.NextVector2Circular(800f, 800f), Vector2.Zero, Color.Wheat, 20, 6, Vector2.One * 2f);
            twinkle.Spawn();
        }

        // Make Solyn warn the player to get away before the Avatar creates the blast.
        SolynAction = s => StandardSolynBehavior_FlyNearPlayer(s, NPC);
        if (AITimer == explosionWaitTime - SecondsToFrames(3f))
        {
            var anyPlayerCloseToAvatar = false;
            foreach (var player in Main.ActivePlayers)
            {
                if (NPC.WithinRange(player.Center, 1400f))
                {
                    anyPlayerCloseToAvatar = true;
                    break;
                }
            }

            var solyn = BattleSolyn.GetOriginalSolyn();
            if (solyn is not null && anyPlayerCloseToAvatar) 
            {
                SolynWorldDialogueManager.CreateNew("Mods.NoxusBoss.Dialog.SolynRunAway", -solyn.NPC.spriteDirection, solyn.NPC.Top, 150, true);
            }
        }

        // Create an explosion.
        if (AITimer == explosionWaitTime)
        {
            SoundEngine.PlaySound(GennedAssets.Sounds.Avatar.AbsoluteZeroWave with { Volume = 2f });
            ScreenShakeSystem.StartShake(32f, TwoPi, null, 0.7f);

            for (int i = 0; i < 50; i++)
                DoBehavior_AbsoluteZeroOutburst_CreateGleam(NPC.Center, 2500f, 30, 120);

            if (Main.netMode != NetmodeID.MultiplayerClient)
                NewProjectileBetter(NPC.GetSource_FromAI(), SpiderLilyPosition, Vector2.Zero, ModContent.ProjectileType<FreezingWave>(), 0, 0f);
        }

        float explodeInterpolant = InverseLerp(0f, 11f, AITimer - AbsoluteZeroOutburst_LilyFreezeTime - AbsoluteZeroOutburst_ExplodeDelay);
        Vector2 armOffset = Vector2.UnitX * Lerp(-250f, 280f, LilyFreezeInterpolant) * InverseLerp(0f, 12f, AITimer);
        armOffset.X -= EasingCurves.Elastic.Evaluate(EasingType.Out, explodeInterpolant) * 110f;

        GenerateCryonicDimensionSnowflakes();
        PerformStandardLimbUpdates(Lerp(0.3f, 4f, explodeInterpolant), armOffset, -armOffset);
    }

    public void DoBehavior_AbsoluteZeroOutburstPunishment()
    {
        float shatterCompletion = InverseLerp(0f, AbsoluteZeroOutburstPunishment_ShatterDelay, AITimer);

        AvatarOfEmptinessSky.WindSpeedFactor = 0f;
        AmbientSoundVolumeFactor = 0f;

        if (AITimer <= AbsoluteZeroOutburstPunishment_LeaveTime)
        {
            ZPosition = InverseLerp(0f, AbsoluteZeroOutburstPunishment_LeaveTime * 0.67f, AITimer).Squared() * 10f;
            NPC.Opacity = InverseLerp(AbsoluteZeroOutburstPunishment_LeaveTime, AbsoluteZeroOutburstPunishment_LeaveTime * 0.5f, AITimer);
        }

        PerformStandardLimbUpdates(InverseLerp(0f, 40f, AITimer).Cubed() * 3f);

        if (AITimer >= 350)
        {
            if (AITimer == 350)
            {
                ZPosition = 30f;
                NPC.Center = Target.Center - Vector2.UnitY * 600f;
                NPC.Opacity = 0f;
                NPC.netUpdate = true;
            }

            NPC.SmoothFlyNearWithSlowdownRadius(Target.Center, 0.05f, 0.95f, 400f);
            ZPosition = Lerp(ZPosition, 0f, 0.14f);
            NPC.Opacity = Lerp(NPC.Opacity, 1f, 0.2f);

            LegScale = Vector2.One;
            LeftFrontArmScale = 1f;
            RightFrontArmScale = 1f;
        }

        // Slam forward.
        if (AITimer >= AbsoluteZeroOutburstPunishment_ShatterDelay - 14 && AITimer < 350)
        {
            LegScale = Vector2.Zero;
            LeftFrontArmScale = 0.4f;
            RightFrontArmScale = 0.4f;
            DrawnAsSilhouette = true;
            if (AITimer == AbsoluteZeroOutburstPunishment_ShatterDelay - 14)
            {
                SoundEngine.PlaySound(GennedAssets.Sounds.Avatar.Jumpscare);
                LeftArmPosition = NPC.Center;
                RightArmPosition = NPC.Center;
                HeadPosition = NPC.Center;
            }

            ZPosition = Clamp(ZPosition - 0.8f, -0.95f, 20f);
            NPC.Opacity = (ZPosition >= -0.7f).ToInt();
            NPC.Center = Target.Center - Vector2.UnitY * 300f;
        }

        // Handle post-slam effects.
        if (AITimer >= AbsoluteZeroOutburstPunishment_ShatterDelay)
        {
            TotalScreenOverlaySystem.OverlayColor = Color.White;
            if (AITimer <= AbsoluteZeroOutburstPunishment_ShatterDelay + 5)
                TotalScreenOverlaySystem.OverlayInterpolant = 1f;

            int buffID = ModContent.BuffType<Glaciated>();
            foreach (Player player in Main.ActivePlayers)
            {
                if (player.HasBuff(buffID))
                {
                    int minHP = AbsoluteZeroOutburstPunishment_MinPlayerHP;
                    int maxHP = AbsoluteZeroOutburstPunishment_MaxPlayerHP;

                    player.ClearBuff(buffID);
                    player.Hurt(new Player.HurtInfo()
                    {
                        Damage = Utils.Clamp(player.statLife - Main.rand.Next(minHP, maxHP), 1, 5000),
                        DamageSource = PlayerDeathReason.ByNPC(NPC.whoAmI),
                        Dodgeable = false,
                        SoundDisabled = true
                    });

                    for (int i = 0; i < 20; i++)
                    {
                        GlowyShardParticle ice = new GlowyShardParticle(player.Center + Main.rand.NextVector2Circular(25f, 25f), Main.rand.NextVector2Circular(20f, 20f), Color.White, Color.DeepSkyBlue * 0.6f, Main.rand.NextFloat(0.8f, 1.2f), Main.rand.NextFloat(0.7f, 1.4f), 30);
                        ice.Spawn();
                    }

                    GeneralScreenEffectSystem.ChromaticAberration.Start(NPC.Center, 5f, 180);

                    if (Main.myPlayer == player.whoAmI)
                    {
                        ScreenShakeSystem.StartShake(40f, TwoPi, null, 1.3f);
                        SoundEngine.PlaySound(GennedAssets.Sounds.Common.EarRinging with { Volume = 0.56f, SoundLimitBehavior = SoundLimitBehavior.IgnoreNew, MaxInstances = 1 });
                    }
                }
            }
        }

        float rumblePower = InverseLerpBump(0f, 0.7f, 0.8f, 0.875f, shatterCompletion).Squared() * 11f;
        ScreenShakeSystem.SetUniversalRumble(rumblePower, TwoPi, null, 0.5f);
    }

    public static void DoBehavior_AbsoluteZeroOutburst_CreateGleam(Vector2 center, float area, int minLifetime, int maxLifetime)
    {
        Color gleamColor = Color.Lerp(Color.DeepSkyBlue, Color.White, Main.rand.NextFloat(0.7f));
        int gleamLifetime = Main.rand.Next(minLifetime, maxLifetime + 1);
        float gleamScale = Main.rand.NextFloat(0.2f, 0.32f);
        BloomCircleGleamParticle gleam = new BloomCircleGleamParticle(center + Main.rand.NextVector2Circular(area, area), Vector2.Zero, gleamColor, gleamLifetime, gleamScale);
        gleam.Spawn();
    }
}
