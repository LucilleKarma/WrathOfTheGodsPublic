using Luminance.Common.DataStructures;
using Luminance.Common.StateMachines;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Content.NPCs.Bosses.Avatar.Projectiles;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SpecificEffectManagers;
using NoxusBoss.Core.Graphics.GeneralScreenEffects;
using NoxusBoss.Core.Graphics.SpecificEffectManagers;
using NoxusBoss.Core.World.TileDisabling;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm;

public partial class AvatarOfEmptiness
{
    /// <summary>
    /// The Y position of the ground during the reality shatter attack.
    /// </summary>
    public ref float RealityShatter_GroundY => ref NPC.ai[2];

    public ref float RealityShatter_CrackConcentration => ref NPC.ai[3];

    /// <summary>
    /// How long it takes for the Avatar's telegraph to disappear during the reality shatter attack.
    /// </summary>
    public static int RealityShatter_TelegraphTime => GetAIInt("RealityShatter_TelegraphTime");

    /// <summary>
    /// How long it takes for the Avatar's impact visuals to process during the reality shatter attack.
    /// </summary>
    public static int RealityShatter_ImpactVisualsTime => GetAIInt("RealityShatter_ImpactVisualsTime");

    /// <summary>
    /// How long it takes for the Avatar's screen tear to appear during the reality shatter attack.
    /// </summary>
    public static int RealityShatter_TearOpenDelay => GetAIInt("RealityShatter_TearOpenDelay");

    /// <summary>
    /// The rate at which the Avatar releases walls of comets during the reality shatter attack.
    /// </summary>
    public static int RealityShatter_CometWallReleaseRate => GetAIInt("RealityShatter_CometWallReleaseRate");

    /// <summary>
    /// How long it the Avatar waits before releasing comet walls during the reality shatter attack.
    /// </summary>
    public static int RealityShatter_CometWallReleaseDelay => GetAIInt("RealityShatter_CometWallReleaseDelay");

    /// <summary>
    /// How long it takes for the Avatar to release comet walls during the reality shatter attack.
    /// </summary>
    public static int RealityShatter_CometWallReleaseTime => GetAIInt("RealityShatter_CometWallReleaseTime");

    /// <summary>
    /// How long it takes for the Avatar to transition to the next attack after releasing comet walls during the reality shatter attack.
    /// </summary>
    public static int RealityShatter_AttackTransitionDelay => GetAIInt("RealityShatter_AttackTransitionDelay");

    /// <summary>
    /// The starting speed at which the Avatar slams downward during the reality shatter attack.
    /// </summary>
    public static float RealityShatter_StartingDownwardSlamSpeed => GetAIFloat("RealityShatter_StartingDownwardSlamSpeed");

    /// <summary>
    /// The maximum speed at which the Avatar slams downward during the reality shatter attack.
    /// </summary>
    public static float RealityShatter_EndingDownwardSlamSpeed => GetAIFloat("RealityShatter_EndingDownwardSlamSpeed");

    /// <summary>
    /// The acceleration of the Avatar's downward slams during the reality shatter attack.
    /// </summary>
    public static float RealityShatter_DownwardSlamAcceleration => GetAIFloat("RealityShatter_DownwardSlamAcceleration");

    /// <summary>
    /// The distance, in pixels, that the imaginary floor is below the player during the reality shatter attack.
    /// </summary>
    public static float RealityShatter_CrackVerticalOffsetFromPlayer => GetAIFloat("RealityShatter_CrackVerticalOffsetFromPlayer");

    /// <summary>
    /// The spacing of comets in walls summoned during the Avatar's reality shatter attack.
    /// </summary>
    public static float RealityShatter_CometWallSpacing => GetAIFloat("RealityShatter_CometWallSpacing");

    /// <summary>
    /// The height of comet walls in the Avatar's reality shatter attack.
    /// </summary>
    public static float RealityShatter_CometWallHeight => GetAIFloat("RealityShatter_CometWallHeight");

    /// <summary>
    /// The speed at which comets walls are fired during the Avatar's reality shatter attack.
    /// </summary>
    public static float RealityShatter_CometShootSpeed => GetAIFloat("RealityShatter_CometShootSpeed");

    [AutomatedMethodInvoke]
    public void LoadState_RealityShatter()
    {
        StateMachine.RegisterTransition(AvatarAIType.RealityShatter_CreateAndWaitForTelegraph, AvatarAIType.RealityShatter_SlamDownward, false, () =>
        {
            return AITimer >= RealityShatter_TelegraphTime;
        });
        StateMachine.RegisterTransition(AvatarAIType.RealityShatter_SlamDownward, AvatarAIType.RealityShatter_GroundCrack, false, () =>
        {
            return NPC.Center.Y >= Target.Center.Y + RealityShatter_CrackVerticalOffsetFromPlayer;
        });
        StateMachine.RegisterTransition(AvatarAIType.RealityShatter_GroundCrack, AvatarAIType.RealityShatter_DimensionTwist, false, () =>
        {
            return AITimer >= RealityShatter_ImpactVisualsTime + RealityShatter_TearOpenDelay;
        });
        StateMachine.RegisterTransition(AvatarAIType.RealityShatter_DimensionTwist, null, false, () =>
        {
            return AITimer >= RealityShatter_CometWallReleaseDelay + RealityShatter_CometWallReleaseTime + RealityShatter_AttackTransitionDelay;
        }, () =>
        {
            AvatarOfEmptinessSky.ScreenTearInterpolant = 0f;

            if (Main.netMode != NetmodeID.Server)
            {
                ManagedScreenFilter tearShader = ShaderManager.GetFilter("NoxusBoss.AvatarRealityTearShader");
                tearShader.TrySetParameter("uIntensity", 0f);
                tearShader.Activate();
            }

            SoundEngine.PlaySound(GennedAssets.Sounds.Avatar.HarshGlitch with { Volume = 0.8f, MaxInstances = 0 });

            TotalScreenOverlaySystem.OverlayColor = Color.Black;
            TotalScreenOverlaySystem.OverlayInterpolant = 2f;

            // Ensure that players aren't shoved inside of tiles due to the teleport.
            foreach (Player player in Main.ActivePlayers)
            {
                while (Collision.SolidCollision(player.TopLeft - Vector2.One * 500f, player.width + 1000, player.height + 1000))
                {
                    player.position.Y -= 16f;
                    if (player.position.Y <= 1000f)
                    {
                        player.position.Y = 1000f;
                        player.position.X += (Main.maxTilesX * 8f - player.position.X).NonZeroSign() * 16f;
                    }
                }
            }
        });

        // Load the AI state behaviors.
        StateMachine.RegisterStateBehavior(AvatarAIType.RealityShatter_CreateAndWaitForTelegraph, DoBehavior_RealityShatter_CreateAndWaitForTelegraph);
        StateMachine.RegisterStateBehavior(AvatarAIType.RealityShatter_SlamDownward, DoBehavior_RealityShatter_SlamDownward);
        StateMachine.RegisterStateBehavior(AvatarAIType.RealityShatter_GroundCrack, DoBehavior_RealityShatter_GroundCrack);
        StateMachine.RegisterStateBehavior(AvatarAIType.RealityShatter_DimensionTwist, DoBehavior_RealityShatter_DimensionTwist);

        AttackDimensionRelationship[AvatarAIType.RealityShatter_CreateAndWaitForTelegraph] = AvatarDimensionVariants.DarkDimension;
        AttackDimensionRelationship[AvatarAIType.RealityShatter_SlamDownward] = AvatarDimensionVariants.DarkDimension;
        AttackDimensionRelationship[AvatarAIType.RealityShatter_GroundCrack] = AvatarDimensionVariants.DarkDimension;
        AttackDimensionRelationship[AvatarAIType.RealityShatter_DimensionTwist] = AvatarDimensionVariants.DarkDimension;
    }

    public void DoBehavior_RealityShatter_CreateAndWaitForTelegraph()
    {
        // Reset the distortion.
        IdealDistortionIntensity = 0f;

        // Close the HP bar.
        HideBar = true;

        // Make tile invisible.
        TileDisablingSystem.TilesAreUninteractable = true;

        // Silently teleport above the player in anticipation of the impending slam.
        if (Main.netMode != NetmodeID.MultiplayerClient && AITimer == 1)
        {
            // Leave the background.
            ZPosition = 0f;

            NPC.Center = Target.Center - Vector2.UnitY * 3200f;
            NPC.velocity = Vector2.Zero;
            Vector2 telegraphSpawnPosition = NPC.Center;
            if (telegraphSpawnPosition.Y < 800f)
                telegraphSpawnPosition.Y = 800f;

            NewProjectileBetter(NPC.GetSource_FromAI(), telegraphSpawnPosition, Vector2.Zero, ModContent.ProjectileType<AvatarSlamTelegraph>(), 0, 0f);

            NPC.netUpdate = true;
        }

        // Remove the player's residual velocity on the first frame.
        if (AITimer == 1)
            Main.LocalPlayer.velocity.X = 0f;
    }

    public void DoBehavior_RealityShatter_SlamDownward()
    {
        // Do damage.
        NPC.damage = NPC.defDamage;

        // Make tile invisible.
        TileDisablingSystem.TilesAreUninteractable = true;

        // Reset the distortion.
        IdealDistortionIntensity = 0f;

        // Close the HP bar.
        HideBar = true;

        // Roar and slam downward.
        if (AITimer == 1)
        {
            ScreenShakeSystem.StartShake(11f);
            NPC.velocity = Vector2.UnitY * RealityShatter_StartingDownwardSlamSpeed;
            NPC.netUpdate = true;
        }

        // Accelerate while slamming.
        NPC.velocity.Y = Clamp(NPC.velocity.Y + RealityShatter_DownwardSlamAcceleration, RealityShatter_StartingDownwardSlamSpeed, RealityShatter_EndingDownwardSlamSpeed);

        // Update limbs.
        PerformStandardLimbUpdates(3f, -Vector2.UnitY * 150f, -Vector2.UnitY * 150f);
    }

    public void DoBehavior_RealityShatter_GroundCrack()
    {
        // Reset the distortion.
        IdealDistortionIntensity = 0f;

        // Store the ground position.
        if (AITimer == 1)
        {
            SoundEngine.PlaySound(GennedAssets.Sounds.Avatar.RealityImpact);
            GeneralScreenEffectSystem.RadialBlur.Start(Vector2.Lerp(NPC.Center, Target.Center, 0.6f), 3f, 60);
            RadialScreenShoveSystem.Start(NPC.Center, 60);
            ScreenShakeSystem.StartShake(10f, shakeStrengthDissipationIncrement: 0.42f);
            RealityShatter_GroundY = Target.Center.Y + RealityShatter_CrackVerticalOffsetFromPlayer;
            NPC.netUpdate = true;
        }

        // Make tile invisible.
        TileDisablingSystem.TilesAreUninteractable = true;

        // Make arms spread out.
        LeftArmPosition.X -= InverseLerp(11f, 0f, AITimer) * 114f;
        RightArmPosition.X += InverseLerp(11f, 0f, AITimer) * 114f;

        // Impose gravity upon limbs.
        LeftArmPosition.Y += 40f;
        RightArmPosition.Y += 40f;
        HeadPosition.Y += 40f;
        HeadPosition.X = Lerp(HeadPosition.X, NPC.Center.X, 0.5f);

        // Prevent limbs from going past the ground.
        DoBehavior_RealityShatter_DimensionTwist_LimitLimbPositions();

        // Make the cracks unfurl.
        RealityShatter_CrackConcentration = Clamp(RealityShatter_CrackConcentration * 1.04f + 0.01f, 0f, 2f);

        // Cease all vertical motion.
        NPC.velocity.Y = 0f;

        // Handle impact visuals.
        DoBehavior_RealityShatter_HandleImpactVisuals();

        if (AITimer == RealityShatter_ImpactVisualsTime)
        {
            ScreenShakeSystem.StartShake(90f, shakeStrengthDissipationIncrement: 0.345f);
            SoundEngine.PlaySound(GennedAssets.Sounds.Avatar.RealityShatter);
        }

        // Tear the screen.
        AvatarOfEmptinessSky.ScreenTearInterpolant = InverseLerp(0f, RealityShatter_TearOpenDelay * 0.23f, AITimer - RealityShatter_ImpactVisualsTime + 8f).Squared();
    }

    public void DoBehavior_RealityShatter_DimensionTwist()
    {
        SolynAction = s => StandardSolynBehavior_FlyNearPlayer(s, NPC);

        // Reset the distortion.
        IdealDistortionIntensity = 0.4f;

        // Make tile invisible.
        TileDisablingSystem.TilesAreUninteractable = true;

        // Make limbs shake.
        LeftArmPosition += Main.rand.NextVector2Circular(1.5f, 3f);
        RightArmPosition += Main.rand.NextVector2Circular(1.5f, 3f);

        // Prevent limbs from going past the ground.
        DoBehavior_RealityShatter_DimensionTwist_LimitLimbPositions();

        // Release comets outward.
        bool canReleaseCometWalls = AITimer >= RealityShatter_CometWallReleaseDelay && AITimer < RealityShatter_CometWallReleaseDelay + RealityShatter_CometWallReleaseTime;
        if (canReleaseCometWalls && AITimer % RealityShatter_CometWallReleaseRate == RealityShatter_CometWallReleaseRate - 1)
        {
            SoundEngine.PlaySound(GennedAssets.Sounds.Avatar.RiftShoot with { MaxInstances = 20, Volume = 0.6f });

            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                for (float cometOffset = 0f; cometOffset < RealityShatter_CometWallHeight * 0.5f; cometOffset += RealityShatter_CometWallSpacing)
                {
                    Vector2 leftCometShootVelocity = Vector2.UnitX * RealityShatter_CometShootSpeed;
                    Vector2 rightCometShootVelocity = Vector2.UnitX * -RealityShatter_CometShootSpeed;

                    // Summon comets above the target.
                    float cometXPosition = Target.Center.X - (NPC.Center.X - Target.Center.X).NonZeroSign() * 1000f;
                    Vector2 cometSpawnPosition = new Vector2(cometXPosition, Target.Center.Y - cometOffset);
                    NewProjectileBetter(NPC.GetSource_FromAI(), cometSpawnPosition, leftCometShootVelocity, ModContent.ProjectileType<DimensionTwistedComet>(), CometDamage, 0f);
                    NewProjectileBetter(NPC.GetSource_FromAI(), cometSpawnPosition, rightCometShootVelocity, ModContent.ProjectileType<DimensionTwistedComet>(), CometDamage, 0f);

                    // Summon comets below the target. This does not execute if the comet offset is 0, since doing so would effectively
                    // summon two comets at the same position.
                    if (cometOffset != 0f)
                    {
                        cometSpawnPosition = new Vector2(cometXPosition, Target.Center.Y + cometOffset);
                        NewProjectileBetter(NPC.GetSource_FromAI(), cometSpawnPosition, leftCometShootVelocity, ModContent.ProjectileType<DimensionTwistedComet>(), CometDamage, 0f);
                        NewProjectileBetter(NPC.GetSource_FromAI(), cometSpawnPosition, rightCometShootVelocity, ModContent.ProjectileType<DimensionTwistedComet>(), CometDamage, 0f);
                    }
                }
            }
        }

        Vector2 checkZone = new Vector2(NPC.Center.X, Target.Center.Y);
        float deathZoneDistance = (Pow((checkZone.Y - NPC.Center.Y) / 1440f * 1.33f, 2f) * 0.15f + 0.07f) * AvatarOfEmptinessSky.ScreenTearInterpolant * 2300f;
        bool targetInDeathZone = Collision.CheckAABBvLineCollision(Target.Center, Vector2.One * 8f, checkZone - Vector2.UnitX * deathZoneDistance, checkZone + Vector2.UnitX * deathZoneDistance);
        if (targetInDeathZone && AITimer < RealityShatter_CometWallReleaseDelay + RealityShatter_CometWallReleaseTime + RealityShatter_AttackTransitionDelay)
        {
            if (NPC.HasPlayerTarget)
            {
                Player playerTarget = Main.player[NPC.TranslatedTargetIndex];

                if (playerTarget.statLife >= playerTarget.statLifeMax2 * 0.667f)
                {
                    playerTarget.statLife = 5;

                    TotalScreenOverlaySystem.OverlayColor = Color.Black;
                    TotalScreenOverlaySystem.OverlayInterpolant = 1.5f;
                    IProjOwnedByBoss<AvatarOfEmptiness>.KillAll();
                }
                else
                {
                    NetworkText killText = NetworkText.FromKey("Mods.NoxusBoss.PlayerDeathMessages.ScreenSplitDeathZone", playerTarget.name);
                    playerTarget.KillMe(PlayerDeathReason.ByCustomReason(killText), 1000000D, 0);
                }
            }

            SoundEngine.StopTrackedSounds();
            SoundEngine.PlaySound(GennedAssets.Sounds.Common.Glitch with { PitchVariance = 0.2f, Volume = 1.3f, MaxInstances = 8 });
            SoundEngine.PlaySound(GennedAssets.Sounds.Common.EarRinging with { Volume = 0.56f, SoundLimitBehavior = SoundLimitBehavior.IgnoreNew, MaxInstances = 1 });

            AITimer = RealityShatter_CometWallReleaseDelay + RealityShatter_CometWallReleaseTime + RealityShatter_AttackTransitionDelay;
            NPC.netUpdate = true;
        }

        // Tear the screen.
        AvatarOfEmptinessSky.ScreenTearInterpolant = 1f;
    }

    public void DoBehavior_RealityShatter_DimensionTwist_LimitLimbPositions()
    {
        if (LeftArmPosition.Y > RealityShatter_GroundY + 500f)
            LeftArmPosition.Y = RealityShatter_GroundY + 500f;
        if (RightArmPosition.Y > RealityShatter_GroundY + 500f)
            RightArmPosition.Y = RealityShatter_GroundY + 500f;
        if (HeadPosition.Y > RealityShatter_GroundY + 200f)
            HeadPosition.Y = RealityShatter_GroundY + 200f;
    }

    public void DrawLightCracks(Vector2 screenPos)
    {
        if (CurrentState != AvatarAIType.RealityShatter_GroundCrack && CurrentState != AvatarAIType.RealityShatter_DimensionTwist)
            return;

        // Prepare for shader drawing.
        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);

        // Draw cracks.
        ManagedShader crackShader = ShaderManager.GetShader("NoxusBoss.LightFloorCrackShader");
        crackShader.TrySetParameter("shatterInterpolant", RealityShatter_CrackConcentration + 0.03f);
        crackShader.SetTexture(VoronoiNoise, 1, SamplerState.LinearWrap);
        crackShader.SetTexture(DendriticNoiseZoomedOut, 2, SamplerState.LinearWrap);
        crackShader.Apply();

        float opacity = InverseLerp(-500f, -200f, RealityShatter_GroundY - NPC.Center.Y);
        Vector2 cracksDrawPosition = new Vector2(NPC.Center.X, RealityShatter_GroundY + 216f) - screenPos;
        Vector2 cracksArea = new Vector2(1950f, 700f);
        Main.spriteBatch.Draw(WhitePixel, cracksDrawPosition, null, Color.White * opacity, 0f, WhitePixel.Size() * new Vector2(0.5f, 0f), cracksArea / WhitePixel.Size(), 0, 0f);

        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);
    }

    public void DoBehavior_RealityShatter_HandleImpactVisuals()
    {
        if (Main.netMode == NetmodeID.Server || !ShaderManager.TryGetFilter("NoxusBoss.ExtremeBlurSaturationShader", out ManagedScreenFilter impactShader))
            return;

        float impactVisualsCompletion = InverseLerp(0f, RealityShatter_ImpactVisualsTime, AITimer);
        float contrastInterpolant = InverseLerpBump(0f, 0.01f, 0.2f, 1f, impactVisualsCompletion);
        float blurIntensity = InverseLerpBump(0f, 0.01f, 0.45f, 1f, impactVisualsCompletion) + Convert01To010(InverseLerp(0.8f, 1f, impactVisualsCompletion).Squared()) * 3.3f;
        float greyscaleInterpolant = Pow(1f - Convert01To010(impactVisualsCompletion), 0.4f);
        Matrix contrastMatrix = GeneralScreenEffectSystem.CalculateContrastMatrix(contrastInterpolant.Cubed() * 100f);
        impactShader.TrySetParameter("contrastMatrix", contrastMatrix);
        impactShader.TrySetParameter("impactPoint", WorldSpaceToScreenUV(NPC.Center));
        impactShader.TrySetParameter("blurIntensity", blurIntensity);
        impactShader.TrySetParameter("greyscaleInterpolant", greyscaleInterpolant);
        impactShader.TrySetParameter("returnToNormalInterpolant", InverseLerp(0.96f, 1f, impactVisualsCompletion));
        impactShader.Activate();
    }
}
