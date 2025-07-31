using System.Reflection;
using Luminance.Common.StateMachines;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using NoxusBoss.Assets;
using NoxusBoss.Content.MainMenuThemes;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity.Rendering;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity.SpecificEffectManagers;
using NoxusBoss.Content.Particles;
using NoxusBoss.Core.CrossCompatibility.Inbound.BaseCalamity;
using NoxusBoss.Core.Graphics.GeneralScreenEffects;
using NoxusBoss.Core.Graphics.ScreenShatter;
using NoxusBoss.Core.Graphics.SpecificEffectManagers;
using NoxusBoss.Core.SoundSystems;
using NoxusBoss.Core.SoundSystems.Music;
using NoxusBoss.Core.World.WorldSaving;
using SubworldLibrary;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using static NoxusBoss.Content.NPCs.Bosses.NamelessDeity.SpecificEffectManagers.NamelessDeitySky;

namespace NoxusBoss.Content.NPCs.Bosses.NamelessDeity;

public partial class NamelessDeityBoss : ModNPC
{
    public int TimeSinceScreenSmash
    {
        get;
        set;
    }

    public bool WaitingForPhase2Transition
    {
        get;
        set;
    }

    public bool WaitingForDeathAnimation
    {
        get;
        set;
    }

    [AutomatedMethodInvoke]
    public void LoadStateTransitions_DeathAnimations()
    {
        // Ensure that phase transitions get priority if they're being waited on and the attack has concluded.
        // An exception to this is if Nameless is quickly killed in the final phase, and the player doesn't see the Moment of Creation attack.
        // If this happens, the Moment of Creation attack happens before Nameless does his defeat animation.
        StateMachine.AddTransitionStateHijack(originalState =>
        {
            if (HasExperiencedFinalAttack && WaitingForDeathAnimation)
                return Main.zenithWorld ? NamelessAIType.DeathAnimation_GFB : NamelessAIType.DeathAnimation;
            if (!HasExperiencedFinalAttack && WaitingForDeathAnimation)
                return NamelessAIType.MomentOfCreation;

            return originalState;
        }, finalState =>
        {
            // Disable the phase 2 transition wait if Nameless is dying.
            WaitingForPhase2Transition = false;
        });

        // Load the AI state behaviors.
        StateMachine.RegisterStateBehavior(NamelessAIType.DeathAnimation, DoBehavior_DeathAnimation);
        StateMachine.RegisterStateBehavior(NamelessAIType.DeathAnimation_GFB, DoBehavior_DeathAnimation_GFB);
    }

    public void DoBehavior_DeathAnimation()
    {
        int handCount = 222;
        int handReleaseRate = 1;
        int blackDelay = 210;
        int riseDelay = 60;
        int riseTime = 210;
        int chargeLineUpTime = 45;
        int screenShatterDelay = 14;
        int crashDelay = 269;
        float handScale = 1.3f / Pow(ZPosition + 1f, 0.75f);
        ref float screenShattered = ref NPC.ai[2];
        ref float congratulatoryTextDrawnBefore = ref NPC.ai[3];

        if (Main.netMode != NetmodeID.MultiplayerClient && NamelessDeathAnimationSkipSystem.SkipNextDeathAnimation)
        {
            NPC.NPCLoot();
            NPC.active = false;
            BossDownedSaveSystem.SetDefeatState<NamelessDeityBoss>(true);
            NamelessDeathAnimationSkipSystem.SkipNextDeathAnimation = false;

            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.WorldData);

            return;
        }

        // Make the different stars interpolant dissipate, in case Nameless was defeated when it was changed.
        DifferentStarsInterpolant = Saturate(DifferentStarsInterpolant - 0.03f);

        // Stay at 1 HP.
        NPC.life = 1;
        NPC.dontTakeDamage = true;

        // Ensure that hands draw at full opacity irrespective of the fact that Nameless will become invisible/occluded.
        HandsShouldInheritOpacity = false;

        // Disable sound muffling.
        SoundMufflingSystem.MuffleFactor = 1f;
        MusicVolumeManipulationSystem.MuffleFactor = 1f;

        // Flap wings.
        UpdateWings(AITimer / 54f);

        // Get rid of Nameless' in-game name.
        NPC.GivenName = string.Empty;

        // Close the HP bar.
        CalamityCompatibility.MakeCalamityBossBarClose(NPC);

        Vector2 hoverDestinationForHand(int handIndex)
        {
            float goldenRatio = 1.618033f;
            Vector2 center = NPC.Center;
            return center + (TwoPi * goldenRatio * handIndex).ToRotationVector2() * (handIndex * NPC.scale * 12f + 40f) * Pow(ZPosition + 1f, 0.2f);
        }

        // Enter the background and hover behind the player.
        if (UniversalBlackOverlayInterpolant <= 0.9f)
        {
            ZPosition = Clamp(ZPosition + 0.1f, 0f, 5f);
            NPC.SmoothFlyNear(Target.Center - Vector2.UnitY * ZPosition * 50f, 0.25f, 0.7f);
            handScale = 1f;
        }
        else if (AITimer < blackDelay + riseDelay + riseTime + handCount * handReleaseRate + chargeLineUpTime)
        {
            NPC.velocity *= 0.9f;

            if (AITimer > blackDelay + riseDelay + riseTime && Hands.Count >= handCount - 1)
                ZPosition = Clamp(ZPosition + 0.3f, 0f, 12f);
            else
                ZPosition = 0f;
        }

        // Update hands.
        if (AITimer <= blackDelay + 10f)
            DefaultUniversalHandMotion(1500f);

        if (AITimer <= blackDelay + riseDelay + 60f)
        {
            foreach (NamelessDeityHand hand in Hands)
            {
                hand.Opacity = 0f;
                hand.HandType = NamelessDeityHandType.OpenPalm;
                if (AITimer <= blackDelay + 10f)
                {
                    hand.Opacity = 1f;
                    hand.HandType = NamelessDeityHandType.Standard;
                    hand.ScaleFactor = handScale;
                }
            }
            NPC.netUpdate = true;
        }

        // Slow down and make the background go pitch black again while screaming at first.
        if (AITimer <= blackDelay)
        {
            // Teleport in front of the target on the first frame.
            if (AITimer == 1f)
            {
                ImmediateTeleportTo(Target.Center + Vector2.UnitX * TargetDirection * 500f, false);

                // Ensure that Nameless doesn't teleport into the ground.
                while (Collision.SolidCollision(NPC.Center - new Vector2(350f, 300f), 700, 600) && NPC.position.Y >= 1200f)
                    NPC.position.Y -= 16f;
            }

            HeavenlyBackgroundIntensity = Saturate(HeavenlyBackgroundIntensity - 0.02f);
            SeamScale = 0f;
            NamelessDeityKeyboardShader.BrightnessIntensity = 1f;
        }
        else
        {
            UniversalBlackOverlayInterpolant = Saturate(UniversalBlackOverlayInterpolant + 0.06f);
            NamelessDeityKeyboardShader.DarknessIntensity = UniversalBlackOverlayInterpolant;
        }

        // Scream at first.
        if (AITimer == 1f)
            SoundEngine.PlaySound(GennedAssets.Sounds.NamelessDeity.ScreamLong with { Volume = 1.2f, Pitch = -0.075f });
        if (AITimer % 8f == 0f && AITimer <= blackDelay + riseDelay - 75f)
        {
            Color burstColor = Main.rand.NextBool() ? Color.LightGoldenrodYellow : Color.Lerp(Color.White, Color.IndianRed, 0.7f);

            // Create blur and burst particle effects.
            ExpandingChromaticBurstParticle burst = new ExpandingChromaticBurstParticle(EyePosition, Vector2.Zero, burstColor, 16, 0.1f);
            burst.Spawn();
            GeneralScreenEffectSystem.RadialBlur.Start(NPC.Center, 1f, 30);

            if (ScreenShakeSystem.OverallShakeIntensity <= 11f)
                ScreenShakeSystem.StartShakeAtPoint(NPC.Center, 5f);
        }

        // Block the UI and inputs until they return to the title screen.
        if (AITimer >= blackDelay + riseDelay - 75f)
            BlockerSystem.Start(true, true, () => NPC.active);

        // Display congratulatory text.
        if (AITimer == blackDelay + riseDelay + 60f)
        {
            DrawCongratulatoryText = true;
            congratulatoryTextDrawnBefore = 1f;
            SoundEngine.PlaySound(GennedAssets.Sounds.NamelessDeity.BossRushPulse);
        }
        if (AITimer == blackDelay + riseDelay + riseTime - 1f)
        {
            DrawCongratulatoryText = false;
            SoundEngine.PlaySound(SoundID.MenuClose);
        }

        // Prevent the player from being at the edge of the world and messing up the text drawing.
        if (congratulatoryTextDrawnBefore == 1f)
        {
            Main.LocalPlayer.position.X = Main.maxTilesX * 8f - 750f;
            NPC.Center = Main.LocalPlayer.Center;
        }

        // Let the player press escape to turn the effect off if they've already defeated Nameless.
        if (Main.netMode == NetmodeID.SinglePlayer && Main.keyState.IsKeyDown(Keys.Escape) && congratulatoryTextDrawnBefore == 1f && BossDownedSaveSystem.HasDefeated<NamelessDeityBoss>())
        {
            NPC.active = false;
            NPC.NPCLoot();
            NPC.Center = Target.Center;
            Main.BestiaryTracker.Kills.RegisterKill(NPC);
            SoundEngine.PlaySound(GennedAssets.Sounds.NamelessDeity.ScreamDistant);

            TotalScreenOverlaySystem.OverlayInterpolant = 1f;
            TotalScreenOverlaySystem.OverlayColor = Color.Black;

            return;
        }

        // Calculate the background hover position.
        Vector2 hoverDestination = Target.Center + Vector2.UnitY * (ZPosition * -30f - 200f) + Vector2.UnitX * 125f;

        // Stay above the target while in the background.
        if (AITimer >= blackDelay + riseDelay)
        {
            NPC.Center = Vector2.Lerp(NPC.Center, hoverDestination, 0.1f);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hoverDestination - NPC.Center) * 0.07f, 0.06f);
        }

        // Cast a ridiculous quantity of arms outward.
        if (AITimer >= blackDelay + riseDelay + riseTime && AITimer % handReleaseRate == 0f && Hands.Count < handCount)
        {
            foreach (NamelessDeityHand hand in Hands)
                hand.Opacity = 1f;
            ConjureHandsAtPosition(hoverDestinationForHand(Hands.Count), Vector2.Zero);
        }

        // Move further into the background.
        if (AITimer >= blackDelay + riseDelay + riseTime + handCount * handReleaseRate && AITimer < blackDelay + riseDelay + riseTime + handCount * handReleaseRate + chargeLineUpTime)
        {
            float hoverSpeedInterpolant = Utils.Remap(ZPosition, 3f, 7f, 0.03f, 0.8f);
            NPC.velocity *= 0.8f;
            NPC.Center = Vector2.Lerp(NPC.Center, Target.Center - Vector2.UnitY * 100f, hoverSpeedInterpolant);
        }

        // Charge forward into the screen.
        if (AITimer >= blackDelay + riseDelay + riseTime + handCount * handReleaseRate + chargeLineUpTime)
        {
            ZPosition = Clamp(ZPosition - 0.8f, -0.94f, 18f);
            NPC.Center = Vector2.Lerp(NPC.Center, Target.Center, 0.08f);
            if (screenShattered == 0f && ZPosition <= -0.94f)
            {
                SoundEngine.PlaySound(GennedAssets.Sounds.NamelessDeity.Supernova with { Volume = 3.3f, MaxInstances = 20 });
                SoundEngine.PlaySound(GennedAssets.Sounds.NamelessDeity.GenericBurst with { Volume = 4f, PitchVariance = 0.15f });
                ScreenShakeSystem.StartShake(25f);
                DestroyAllHands();
                ScreenShatterSystem.CreateShatterEffect(NPC.Center - Main.screenPosition, true);
                GeneralScreenEffectSystem.ChromaticAberration.Start(NPC.Center, 3f, 90);

                foreach (Projectile projectile in Main.ActiveProjectiles)
                    projectile.active = false;

                screenShattered = 1f;
                NPC.netUpdate = true;
            }

            // Disable music and make the keyboard shader dance chaotically after the screen has been shattered.
            if (screenShattered == 1f)
            {
                Music = 0;
                NamelessDeityKeyboardShader.RandomColorsTimer = 5;
                TimeSinceScreenSmash++;
            }

            if (AITimer >= blackDelay + riseDelay + riseTime + handCount * handReleaseRate + chargeLineUpTime + screenShatterDelay + crashDelay)
            {
                // Completely disappear in multiplayer. This will happen server-side, while all clients involved in the fight are kicked out of the server.
                NPC.active = false;

                if (Main.netMode != NetmodeID.Server)
                {
                    // Get out of the subworld.
                    typeof(SubworldSystem).GetField("current", BindingFlags.NonPublic | BindingFlags.Static)?.SetValue(null, null);
                    typeof(SubworldSystem).GetField("cache", BindingFlags.NonPublic | BindingFlags.Static)?.SetValue(null, null);

                    // Bring the player to the main menu, and enable the marker that ensures they will receive loot upon re-entry.
                    Main.menuMode = 0;
                    Main.gameMenu = true;
                    Main.LocalPlayer.GetValueRef<bool>(PlayerGiveLootFieldName).Value = true;

                    // Mark Nameless as defeated.
                    BossDownedSaveSystem.SetDefeatState<NamelessDeityBoss>(true);

                    // Ensure that the player sees the "You have passed the test." dialog upon being sent to the main menu.
                    NamelessDeityTipsOverrideSystem.UseDeathAnimationText = true;

                    // Save the player's file data, to ensure that the loot re-entry is registered.
                    Player.SavePlayer(Main.ActivePlayerFileData);

                    // Kick clients out of the server.
                    if (Main.netMode == NetmodeID.MultiplayerClient)
                    {
                        Netplay.Disconnect = true;
                        Main.netMode = NetmodeID.SinglePlayer;
                    }

                    // Forcefully change the menu theme to the Nameless Deity one if he was defeated for the first time.
                    // This is done half as an indicator that the "I got kicked out of my world!" behavior isn't a bug and half as a reveal that the option is unlocked.
                    if (!GlobalBossDownedSaveSystem.IsDefeated<NamelessDeityBoss>())
                    {
                        GlobalBossDownedSaveSystem.MarkDefeated<NamelessDeityBoss>();

                        do
                            typeof(MenuLoader).GetMethod("OffsetModMenu", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object[] { Main.rand.Next(-2, 3) });
                        while (((ModMenu)typeof(MenuLoader).GetField("switchToMenu", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!).FullName != XNamelessDeityDimensionMainMenu.Instance.FullName);
                    }
                }
            }
        }

        // Keep hands moving.
        for (int i = 0; i < Hands.Count; i++)
        {
            NamelessDeityHand hand = Hands[i];

            DefaultHandDrift(hand, hoverDestinationForHand(i), 5.6f);
            if (AITimer > blackDelay + 10f)
            {
                hand.HandType = NamelessDeityHandType.OpenPalm;
                hand.ScaleFactor = handScale;
                hand.HasArms = false;
            }
        }
    }

    public void DoBehavior_DeathAnimation_GFB()
    {
        // Stay at 1 HP.
        NPC.life = 1;
        NPC.dontTakeDamage = true;

        // Flap wings.
        UpdateWings(AITimer / 72f);

        // Get rid of Nameless' in-game name.
        NPC.GivenName = string.Empty;

        // Close the HP bar.
        CalamityCompatibility.MakeCalamityBossBarClose(NPC);

        // Make hands invisible.
        foreach (NamelessDeityHand hand in Hands)
            hand.Opacity = 0f;

        // Teleport in front of the target on the first frame and create a funny death sound.
        if (AITimer == 1f)
        {
            ImmediateTeleportTo(Target.Center + Vector2.UnitX * TargetDirection * 500f, false);
            SoundEngine.PlaySound(GennedAssets.Sounds.NPCKilled.NamelessDeityFuckingDies_GFB with { Volume = 1.5f });
        }

        // Turn off the music.
        Music = 0;

        // Die and create a Deltarune explosion.
        if (AITimer >= 274f)
        {
            SoundEngine.PlaySound(GennedAssets.Sounds.NPCKilled.DeltaruneExplosion with { Volume = 1.4f });
            DeltaruneExplosionParticle explosion = new DeltaruneExplosionParticle(NPC.Center, Vector2.Zero, Color.White, 60, 1.9f);
            explosion.Spawn();

            Myself = null;
            NPC.life = 0;
            NPC.HitEffect();
            NPC.NPCLoot();
            Main.BestiaryTracker.Kills.RegisterKill(NPC);
            NPC.active = false;

            BossDownedSaveSystem.SetDefeatState<NamelessDeityBoss>(true);
        }
    }
}
