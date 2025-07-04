using Luminance.Common.StateMachines;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using NoxusBoss.Assets;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity.SpecificEffectManagers;
using NoxusBoss.Core.CrossCompatibility.Inbound;
using NoxusBoss.Core.Graphics.GeneralScreenEffects;
using NoxusBoss.Core.Graphics.SpecificEffectManagers;
using NoxusBoss.Core.SoundSystems;
using NoxusBoss.Core.SoundSystems.Music;
using NoxusBoss.Core.World.GameScenes.OriginalLight;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;
using static NoxusBoss.Content.NPCs.Bosses.NamelessDeity.SpecificEffectManagers.NamelessDeitySky;

namespace NoxusBoss.Content.NPCs.Bosses.NamelessDeity;

public partial class NamelessDeityBoss : ModNPC
{
    public bool TargetIsUsingRodOfHarmony
    {
        get;
        set;
    }

    /// <summary>
    /// How long Nameless waits before attacking during his phase 2 transition.
    /// </summary>
    public static int EnterPhase2_AttackWaitTime
    {
        get
        {
            // Make the attack delays a bit shorter outside of Rev+, since otherwise there's an awkward wait at the point where the rippers would otherwise be destroyed.
            if (!CommonCalamityVariables.RevengeanceModeActive)
                return 150;

            return 240;
        }
    }

    /// <summary>
    /// How long Nameless' phase 3 transition goes on for.
    /// </summary>
    public static int EnterPhase3_AttackTransitionDelay => 71;

    [AutomatedMethodInvoke]
    public void LoadStateTransitions_Phase2TransitionStart()
    {
        // Prepare to enter phase 2 if ready. This will ensure that once the attack has finished Nameless will enter the second phase.
        StateMachine.AddTransitionStateHijack(originalState =>
        {
            if (CurrentPhase == 0 && WaitingForPhase2Transition && originalState != NamelessAIType.DeathAnimation && originalState != NamelessAIType.DeathAnimation_GFB)
                return NamelessAIType.EnterPhase2;

            return originalState;
        });

        // As an addendum to the above, this effect happens immediately during the star blender attack instead of waiting due to pacing concerns.
        StateMachine.RegisterTransition(NamelessAIType.SunBlenderBeams, NamelessAIType.EnterPhase2, false, () =>
        {
            return LifeRatio <= Phase2LifeRatio && CurrentPhase == 0;
        });
    }

    [AutomatedMethodInvoke]
    public void LoadStateTransitions_Phase2TransitionEnd()
    {
        // Load the transition from EnterPhase2 to the rod of harmony rant.
        StateMachine.RegisterTransition(NamelessAIType.EnterPhase2, NamelessAIType.RodOfHarmonyRant, false, () =>
        {
            return TargetIsUsingRodOfHarmony && AITimer >= EnterPhase2_AttackWaitTime;
        });

        StateMachine.RegisterTransition(NamelessAIType.EnterPhase2, NamelessAIType.EnterPhase2_AttackPlayer, false, () =>
        {
            return !TargetIsUsingRodOfHarmony && AITimer >= EnterPhase2_AttackWaitTime;
        });
    }

    [AutomatedMethodInvoke]
    public void LoadStateTransitions_Phase3TransitionStart()
    {
        // Enter phase 3 if ready. Unlike phase 2, this happens immediately since the transition effect is intentionally sudden and jarring, like a "psychic damage" attack.
        StateMachine.ApplyToAllStatesExcept(previousState =>
        {
            StateMachine.RegisterTransition(previousState, NamelessAIType.EnterPhase3, false, () =>
            {
                return LifeRatio <= Phase3LifeRatio && CurrentPhase == 1;
            }, () =>
            {
                SoundEngine.StopTrackedSounds();
                ClearAllProjectiles();
                CurrentPhase = 2;
                NPC.netUpdate = true;

                // Use a special track for phase 3 and onward.
                Music = MusicLoader.GetMusicSlot(Mod, "Assets/Sounds/Music/ARIA BEYOND THE SHINING FIRMAMENT");
            });
        }, NamelessAIType.DeathAnimation, NamelessAIType.DeathAnimation_GFB, NamelessAIType.SavePlayerFromAvatar);

        // Load the AI state behavior.
        StateMachine.RegisterStateBehavior(NamelessAIType.EnterPhase2, DoBehavior_EnterPhase2);
    }

    [AutomatedMethodInvoke]
    public void LoadStateTransitions_Phase3TransitionEnd()
    {
        // Load the transition from EnterPhase3 to the regular cycle.
        StateMachine.RegisterTransition(NamelessAIType.EnterPhase3, NamelessAIType.ResetCycle, false, () =>
        {
            return AITimer >= EnterPhase3_AttackTransitionDelay;
        }, () =>
        {
            // Play the glitch sound.
            SoundEngine.PlaySound(GennedAssets.Sounds.Common.Glitch with { PitchVariance = 0.2f, Volume = 1.3f, MaxInstances = 8 });

            // Teleport behind the target.
            ImmediateTeleportTo(Target.Center - Vector2.UnitX * TargetDirection * 400f, false);

            // Return sounds back to normal.
            SoundMufflingSystem.MuffleFactor = 1f;
            MusicVolumeManipulationSystem.MuffleFactor = 1f;

            // Reset the sword slash variables.
            // The reason this is necessary is because sometimes the sword can be terminated suddenly by this attack, without getting a chance to reset
            // the counter. This can make it so that the first sword slash attack in the third phase can have incorrect data.
            SwordSlashDirection = 0;
            SwordSlashCounter = 0;

            // Disable the light effect.
            OriginalLightGlitchOverlaySystem.OverlayInterpolant = 0f;
            OriginalLightGlitchOverlaySystem.GlitchIntensity = 0f;
            OriginalLightGlitchOverlaySystem.EyeOverlayOpacity = 0f;
            OriginalLightGlitchOverlaySystem.WhiteOverlayInterpolant = 0f;

            // Create disorienting visual effects.
            TotalScreenOverlaySystem.OverlayInterpolant = 1f;
            GeneralScreenEffectSystem.ChromaticAberration.Start(NPC.Center, 2.2f, 150);
            ScreenShakeSystem.StartShakeAtPoint(NPC.Center, 15f);
            SoundEngine.PlaySound(GennedAssets.Sounds.Common.EarRinging with { Volume = 0.12f, SoundLimitBehavior = SoundLimitBehavior.IgnoreNew, MaxInstances = 1 });
        });

        // Load the AI state behavior.
        StateMachine.RegisterStateBehavior(NamelessAIType.EnterPhase3, DoBehavior_EnterPhase3);
    }

    public void DoBehavior_EnterPhase2()
    {
        int cooldownTime = EnterPhase2_AttackWaitTime;
        int ripperDestructionAnimationTime = 80;
        if (!CommonCalamityVariables.RevengeanceModeActive)
            ripperDestructionAnimationTime = 1;

        // Undo relative darkness effects, in case the phase transition happens after Nameless' sun attack.
        if (RelativeDarkening > 0f)
        {
            RelativeDarkening = Saturate(RelativeDarkening - 0.075f);
            HeavenlyBackgroundIntensity = 1f - RelativeDarkening;
        }

        KaleidoscopeInterpolant = InverseLerp(1f, 40f, AITimer);

        // Destroy the ripper UI.
        float ripperDestructionAnimationCompletion = InverseLerp(0f, ripperDestructionAnimationTime, AITimer - cooldownTime + ripperDestructionAnimationTime + 30);
        if (!RipperUIDestructionSystem.IsUIDestroyed)
            RipperUIDestructionSystem.FistOpacity = SmoothStep(0f, 1f, InverseLerp(0.04f, 0.74f, ripperDestructionAnimationCompletion));
        else
            RipperUIDestructionSystem.FistOpacity = Saturate(RipperUIDestructionSystem.FistOpacity - 0.04f);

        // Handle GFB-specific checks.
        DoBehavior_EnterPhase2_PerformGFBChecks(cooldownTime, ripperDestructionAnimationCompletion);

        NPC.SmoothFlyNearWithSlowdownRadius(Target.Center - Vector2.UnitY * 325f, 0.06f, 0.93f, 200f);
        ZPosition = Lerp(ZPosition, 0f, 0.067f);

        // Play a mumble sound on the first frame.
        if (AITimer == 1f)
            PerformMumble();

        // Update wings.
        UpdateWings(AITimer / 48f);

        // Update hands.
        DefaultUniversalHandMotion();
    }

    public void DoBehavior_EnterPhase2_PerformGFBChecks(int cooldownTime, float ripperDestructionAnimationCompletion)
    {
        // Censor items and obliterate rods of harmony in GFB.
        if (Main.zenithWorld)
        {
            HotbarUICensorSystem.CensorOpacity = InverseLerp(0.09f, 0.81f, ripperDestructionAnimationCompletion);

            if (AITimer == cooldownTime - 1f)
            {
                // Check if the rod of harmony is is the target's inventory and they have no legitimate cheat permission slip. If it is, do a special rant.
                if (NPC.HasPlayerTarget && RoHDestructionSystem.PerformRodOfHarmonyCheck(Main.player[NPC.target]))
                {
                    TargetIsUsingRodOfHarmony = true;
                    NPC.netUpdate = true;
                }
            }
        }

        if (!RipperUIDestructionSystem.IsUIDestroyed && ripperDestructionAnimationCompletion >= 1f)
        {
            RipperUIDestructionSystem.CreateBarDestructionEffects();
            RipperUIDestructionSystem.IsUIDestroyed = true;
            NamelessDeityKeyboardShader.BrightnessIntensity = 1f;
        }
    }

    public void DoBehavior_EnterPhase3()
    {
        int glitchDelay = 32;

        // Disable sounds and music temporarily.
        SoundMufflingSystem.MuffleFactor = 0.004f;
        MusicVolumeManipulationSystem.MuffleFactor = 0f;

        // Handle first-frame initializations.
        if (AITimer == 1f)
        {
            // Start the phase transition sound.
            SoundEngine.PlaySound(GennedAssets.Sounds.NamelessDeity.Phase3Transition);

            // Disable the UI and inputs for the duration of the attack.
            BlockerSystem.Start(true, true, () => CurrentState == NamelessAIType.EnterPhase3);

            // Create a white overlay effect.
            OriginalLightGlitchOverlaySystem.WhiteOverlayInterpolant = 1f;
        }

        // Show the player the Original Light.
        if (AITimer <= 10f)
            OriginalLightGlitchOverlaySystem.OverlayInterpolant = 1f;

        // Create glitch effects.
        if (AITimer == glitchDelay)
        {
            OriginalLightGlitchOverlaySystem.GlitchIntensity = 1f;
            OriginalLightGlitchOverlaySystem.EyeOverlayOpacity = 1f;
            OriginalLightGlitchOverlaySystem.WhiteOverlayInterpolant = 1f;

            // Play the glitch sound.
            SoundEngine.PlaySound(GennedAssets.Sounds.Common.Glitch with { PitchVariance = 0.2f, Volume = 1.3f, MaxInstances = 8 });
        }
    }
}
