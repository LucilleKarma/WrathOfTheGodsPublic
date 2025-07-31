using Luminance.Common.Easings;
using Luminance.Common.StateMachines;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using NoxusBoss.Assets;
using NoxusBoss.Content.NPCs.Bosses.Avatar.FirstPhaseForm;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm.FormPresets;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SpecificEffectManagers;
using NoxusBoss.Content.Particles;
using NoxusBoss.Core.CrossCompatibility.Inbound.BaseCalamity;
using NoxusBoss.Core.Graphics.GeneralScreenEffects;
using NoxusBoss.Core.Graphics.ScreenShake;
using NoxusBoss.Core.SoundSystems.Music;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm;

public partial class AvatarOfEmptiness
{
    public static int Awaken_RiftSizeIncreaseTime => SecondsToFrames(2f);

    public static int Awaken_LegEmergenceTime => SecondsToFrames(1.56f);

    public static int Awaken_ArmJutTime => SecondsToFrames(3f);

    public static int Awaken_HeadEmergenceTime => SecondsToFrames(5.1f);

    public static int Awaken_ScreamTime => SecondsToFrames(4f);

    [AutomatedMethodInvoke]
    public void LoadState_Awaken()
    {
        StateMachine.RegisterTransition(AvatarAIType.Awaken_RiftSizeIncrease, AvatarAIType.Awaken_LegEmergence, false, () =>
        {
            return AITimer >= Awaken_RiftSizeIncreaseTime;
        });
        StateMachine.RegisterTransition(AvatarAIType.Awaken_LegEmergence, AvatarAIType.Awaken_ArmJutOut, false, () =>
        {
            return AITimer >= Awaken_LegEmergenceTime;
        });
        StateMachine.RegisterTransition(AvatarAIType.Awaken_ArmJutOut, AvatarAIType.Awaken_HeadEmergence, false, () =>
        {
            return AITimer >= Awaken_ArmJutTime;
        });
        StateMachine.RegisterTransition(AvatarAIType.Awaken_HeadEmergence, AvatarAIType.Awaken_Scream, false, () =>
        {
            return AITimer >= Awaken_HeadEmergenceTime;
        });
        StateMachine.RegisterTransition(AvatarAIType.Awaken_Scream, null, false, () =>
        {
            return AITimer >= Awaken_ScreamTime;
        }, () =>
        {
            PreviousState = StartingAttacks.First();
            foreach (AvatarAIType attack in StartingAttacks.Reverse())
                StateMachine.StateStack.Push(StateMachine.StateRegistry[attack]);
        });

        // Load the AI state behaviors.
        StateMachine.RegisterStateBehavior(AvatarAIType.Awaken_RiftSizeIncrease, DoBehavior_Awaken_RiftSizeIncrease);
        StateMachine.RegisterStateBehavior(AvatarAIType.Awaken_LegEmergence, DoBehavior_Awaken_LegEmergence);
        StateMachine.RegisterStateBehavior(AvatarAIType.Awaken_ArmJutOut, DoBehavior_Awaken_ArmJutOut);
        StateMachine.RegisterStateBehavior(AvatarAIType.Awaken_HeadEmergence, DoBehavior_Awaken_HeadEmergence);
        StateMachine.RegisterStateBehavior(AvatarAIType.Awaken_Scream, DoBehavior_Awaken_Scream);
    }

    public void DoBehavior_Awaken_RiftSizeIncrease()
    {
        FightTimer = 0;

        // Prevent revealing the new name too soon.
        NPC.ShowNameOnHover = false;

        // Create a lightning effect.
        if (AITimer == 10)
        {
            CustomScreenShakeSystem.Start(75, 25f);
            SoundEngine.PlaySound(GennedAssets.Sounds.Avatar.Phase2IntroLightning with { Volume = 2f, MaxInstances = 0 });
        }
        if (AITimer == 10 || AITimer == 13 || AITimer == 16)
            SoundEngine.PlaySound(GennedAssets.Sounds.Avatar.Phase2IntroSuspense with { Volume = 1.7f });

        // Don't give the player free rippers, since the Avatar isn't even attacking.
        if (NPC.HasPlayerTarget)
            CalamityCompatibility.ResetRippers(Main.player[NPC.TranslatedTargetIndex]);

        // Calculate the animation completion. This stops just short of the attack's end.
        float animationCompletion = InverseLerp(0f, Awaken_RiftSizeIncreaseTime - 10f, AITimer);

        // Use the animation completion to calculate the rift size scaling interpolant.
        // This uses an elastic easing curve to make the rift overextend in size somewhat before stabilizing.
        float riftSizeInterpolant = EasingCurves.Elastic.Evaluate(EasingType.Out, animationCompletion);

        // Size up from the phase 1 rift scale to the phase 2 scale.
        Vector2 startingSize = AvatarRift.DefaultHitboxSize;
        Vector2 endingSize = DefaultHitboxSize;
        SetHitboxSize(Vector2.Lerp(startingSize, endingSize, riftSizeInterpolant));

        // Disable damage.
        NPC.dontTakeDamage = true;

        // Reset values.
        HeadOpacity = 0f;
        LeftFrontArmOpacity = 0f;
        RightFrontArmOpacity = 0f;
        LegScale = Vector2.Zero;
        LilyScale = 0f;

        // Disable the boss HP bar.
        HideBar = true;

        // Make the music fade out.
        MusicVolumeManipulationSystem.MuffleFactor = 0.1f;

        // Move the camera to the Avatar.
        HeadPosition = NPC.Center + Vector2.UnitY * HeadScale * 100f;
        LockCameraToSelf(animationCompletion * 0.5f, 0f);
    }

    public void DoBehavior_Awaken_LegEmergence()
    {
        FightTimer = 0;

        // Prevent revealing the new name too soon.
        NPC.ShowNameOnHover = false;

        // Don't give the player free rippers, since the Avatar isn't even attacking.
        if (NPC.HasPlayerTarget)
            CalamityCompatibility.ResetRippers(Main.player[NPC.TranslatedTargetIndex]);

        // Play a leg sound on the first frame.
        if (AITimer == (int)(Awaken_LegEmergenceTime * 0.748f))
            SoundEngine.PlaySound(GennedAssets.Sounds.Avatar.Phase2IntroLegsEmerge with { Volume = 4f });

        // Calculate the animation completion.
        float animationCompletion = InverseLerp(0f, Awaken_LegEmergenceTime, AITimer);

        // Make the legs appear.
        LegScale = new Vector2(Pow(InverseLerp(0f, 0.84f, animationCompletion), 4f), Pow(InverseLerp(0.23f, 0.93f, animationCompletion), 4.3f));

        // Disable damage.
        NPC.dontTakeDamage = true;

        // Reset the head opacity.
        HeadOpacity = 0f;

        // Disable the boss HP bar.
        HideBar = true;

        // Make the music fade out.
        MusicVolumeManipulationSystem.MuffleFactor = 0.1f;

        // Keep the camera on the Avatar.
        LockCameraToSelf(Lerp(0.5f, 1f, InverseLerp(0f, 0.641f, animationCompletion)), 0f);
    }

    public void DoBehavior_Awaken_ArmJutOut()
    {
        FightTimer = 0;

        // Prevent revealing the new name too soon.
        NPC.ShowNameOnHover = false;

        // Don't give the player free rippers, since the Avatar isn't even attacking.
        if (NPC.HasPlayerTarget)
            CalamityCompatibility.ResetRippers(Main.player[NPC.TranslatedTargetIndex]);

        // Calculate the animation completion.
        float animationCompletion = InverseLerp(0f, Awaken_ArmJutTime, AITimer);

        // Make the left arm jut out.
        float leftArmJutInterpolant = InverseLerp(0f, 0.25f, animationCompletion);
        Vector2 leftArmJutDirection = (-Vector2.UnitX).RotatedBy(-0.51f);
        Vector2 rightArmJutDirection = Vector2.UnitX.RotatedBy(0.51f);
        LeftArmPosition = NPC.Center + leftArmJutDirection * Lerp(1100f, 710f, EasingCurves.Elastic.Evaluate(EasingType.Out, leftArmJutInterpolant));
        LeftArmPosition += Vector2.UnitY * Cos(TwoPi * AITimer / 150f) * 60f;
        LeftArmPosition -= Vector2.UnitY * (1f - leftArmJutInterpolant) * 320f;
        LeftFrontArmOpacity = 1f;
        LeftFrontArmScale = Sqrt(leftArmJutInterpolant);

        // Make the right arm jut out.
        float rightArmJutInterpolant = InverseLerp(0.25f, 0.5f, animationCompletion);
        RightArmPosition = NPC.Center + rightArmJutDirection * Lerp(1000f, 710f, EasingCurves.Elastic.Evaluate(EasingType.Out, rightArmJutInterpolant));
        RightArmPosition += Vector2.UnitY * Cos(TwoPi * AITimer / 150f + 1.91f) * 60f;
        RightArmPosition -= Vector2.UnitY * (1f - rightArmJutInterpolant) * 320f;
        RightFrontArmOpacity = InverseLerp(0f, 0.2f, rightArmJutInterpolant);
        RightFrontArmScale = Sqrt(rightArmJutInterpolant);

        // Clench the arms as they open.
        HandGraspAngle = (1f - leftArmJutInterpolant) * -0.3f;

        SetHitboxSize(DefaultHitboxSize * Lerp(0.975f, 1.025f, Sin01(TwoPi * AITimer / 60f)));

        bool leftJutEffect = AITimer == 1;
        bool rightJutEffect = AITimer == Awaken_ArmJutTime / 4;
        if (leftJutEffect || rightJutEffect)
        {
            SoundEngine.PlaySound(GennedAssets.Sounds.Avatar.ArmJutOut with { MaxInstances = 0, Volume = 0.85f }, NPC.Center);
            GeneralScreenEffectSystem.RadialBlur.Start(NPC.Center, 1.5f, 23);
            CustomScreenShakeSystem.Start(60, 12f).WithDirectionalBias(new Vector2(1f, 0.5f));

            for (int i = 0; i < 27; i++)
            {
                float bloodScale = Main.rand.NextFloat(0.6f, 1.5f);
                Color bloodColor = Color.Lerp(new(Main.rand.Next(115, 233), 0, 0), new(76, 24, 60), Main.rand.NextFloat(0.4f));
                Vector2 bloodVelocity = Main.rand.NextVector2Circular(6f, 9f) - Vector2.UnitY * Main.rand.NextFloat(4f, 10f);
                if (leftJutEffect)
                    bloodVelocity = bloodVelocity.RotatedBy(-0.95f);
                if (rightJutEffect)
                    bloodVelocity = bloodVelocity.RotatedBy(1.23f);

                BloodParticle2 bloodSplatter = new BloodParticle2(NPC.Center + bloodVelocity * 20f, bloodVelocity, 36, bloodScale, bloodColor);
                bloodSplatter.Spawn();
            }
            for (int i = 0; i < 45; i++)
            {
                float bloodScale = Main.rand.NextFloat(0.6f, 2.5f);
                Color bloodColor = Color.Lerp(Color.Red, new(137, 44, 78), Main.rand.NextFloat(0.7f));
                Vector2 bloodVelocity = (Main.rand.NextVector2Circular(6f, 11f) - Vector2.UnitY * Main.rand.NextFloat(5.8f, 13f)) * Main.rand.NextFloat(1.3f, 3.3f);
                if (leftJutEffect)
                    bloodVelocity = bloodVelocity.RotatedBy(-0.95f);
                if (rightJutEffect)
                    bloodVelocity = bloodVelocity.RotatedBy(1.23f);

                BloodParticle blood = new BloodParticle(NPC.Center + bloodVelocity * 10f, bloodVelocity, 36, bloodScale, bloodColor);
                blood.Spawn();
            }

            LilyScale = 1f;
        }

        // Disable damage.
        NPC.dontTakeDamage = true;

        // Reset the head opacity.
        HeadOpacity = 0f;

        // Disable the boss HP bar.
        HideBar = true;

        // Make the music fade out.
        MusicVolumeManipulationSystem.MuffleFactor = 0.1f;

        // Keep the camera on the Avatar.
        LockCameraToSelf(1f, SmoothStep(0f, 0.35f, InverseLerp(0f, 0.5f, animationCompletion)));
    }

    public void DoBehavior_Awaken_HeadEmergence()
    {
        // Don't give the player free rippers, since the Avatar isn't even attacking.
        if (NPC.HasPlayerTarget)
            CalamityCompatibility.ResetRippers(Main.player[NPC.TranslatedTargetIndex]);

        // Calculate the animation completion.
        float animationCompletion = InverseLerp(0f, Awaken_HeadEmergenceTime - 75f, AITimer);

        HeadScaleFactor = 1f;

        // Calculate the neck appear interpolant.
        NeckAppearInterpolant = Pow(InverseLerp(0.1f, 0.67f, animationCompletion), 1.6f);

        // Make the head fade in.
        HeadOpacity = InverseLerp(0.1f, 0.15f, NeckAppearInterpolant);

        // Reset the fight duration so that the Avatar's face has an appearance delay.
        if (animationCompletion < 0.4f)
            FightTimer = 0;
        if (FightTimer == 1)
            SoundEngine.PlaySound(GennedAssets.Sounds.Avatar.Phase2IntroFaceManifest with { Volume = 2f });

        // Decide the Avatar's mask frame.
        MaskFrame = Utils.Clamp(FightTimer / 4, 1, 23);
        if (AvatarFormPresetRegistry.UsingLucillePreset)
            MaskFrame = 0;

        // Play a head emergence sound at first.
        if (AITimer == 30)
            SoundEngine.PlaySound(GennedAssets.Sounds.Avatar.Phase2IntroHeadEmerge with { Volume = 2f });

        // Play neck sounds.
        if (AITimer == 120)
            SoundEngine.PlaySound(GennedAssets.Sounds.Avatar.Phase2IntroNeckDrop with { Volume = 2f });
        if (AITimer == 160)
            SoundEngine.PlaySound(GennedAssets.Sounds.Avatar.Phase2IntroNeckSnap with { Volume = 10f, MaxInstances = 0 });

        // Make Solyn respond in horror to the Avatar's appearance.
        if (AITimer == 132)
        {
            SolynAction = solyn =>
            {
                solyn.NPC.velocity.Y -= 8f;
                solyn.NPC.netUpdate = true;
            };
        }

        // Move the arms up and down.
        Vector2 leftArmJutDirection = (-Vector2.UnitX).RotatedBy(-0.51f);
        Vector2 rightArmJutDirection = Vector2.UnitX.RotatedBy(0.51f);
        Vector2 leftArmDestination = NPC.Center + leftArmJutDirection * 710f + Vector2.UnitY * -Sin(TwoPi * AITimer / 150f) * 60f;
        Vector2 rightArmDestination = NPC.Center + rightArmJutDirection * 710f + Vector2.UnitY * -Sin(TwoPi * AITimer / 150f + 1.91f) * 60f;

        float inwardArmOffset = SmoothStep(0f, 160f, InverseLerp(0.39f, 1f, animationCompletion));
        leftArmDestination.X += inwardArmOffset;
        rightArmDestination.X -= inwardArmOffset;

        LeftArmPosition = Vector2.Lerp(LeftArmPosition, leftArmDestination, 0.19f);
        RightArmPosition = Vector2.Lerp(RightArmPosition, rightArmDestination, 0.19f);

        // Shake the screen a bit as the Avatar's head emerges from the rift.
        if (AITimer == 30)
            CustomScreenShakeSystem.Start(40, 8f);

        // Make the head appear and dangle down.
        float bounceInterpolant = Convert01To010(InverseLerp(0.61f, 0.8f, animationCompletion));
        float fastFallInterpolant = EasingCurves.Cubic.Evaluate(EasingType.In, InverseLerp(0.36f, 0.61f, animationCompletion) - bounceInterpolant * 0.06f);
        float verticalOffset = Cos01(TwoPi * AITimer / 90f) * 24f + fastFallInterpolant * 415f;
        HeadPosition = NPC.Center + new Vector2(3f, verticalOffset) * HeadScale;

        // Disable damage.
        NPC.dontTakeDamage = true;

        // Look at the target.
        MaskRotation = NPC.rotation.AngleLerp(Clamp((Target.Center - HeadPosition).X * -0.009f, -0.5f, 0.3f), 0.03f);

        // Disable the boss HP bar.
        HideBar = true;

        // Make the music fade out.
        MusicVolumeManipulationSystem.MuffleFactor = 0.1f;

        // Keep the camera on the Avatar.
        LockCameraToSelf(1f, SmoothStep(0.35f, -0.14f, InverseLerp(0f, 0.3f, animationCompletion)));
    }

    public void DoBehavior_Awaken_Scream()
    {
        // Don't give the player free rippers, since the Avatar isn't even attacking.
        if (NPC.HasPlayerTarget)
            CalamityCompatibility.ResetRippers(Main.player[NPC.TranslatedTargetIndex]);

        float vibrationInterpolant = InverseLerp(Awaken_ScreamTime, Awaken_ScreamTime * 0.75f, AITimer);

        // Make the head dangle down.
        float verticalOffset = Cos(TwoPi * AITimer / 90f) * 16f + 392f;
        HeadPosition = Vector2.Lerp(HeadPosition, NPC.Center + new Vector2(48f, verticalOffset) * HeadScale, 0.16f);

        // Disable damage.
        NPC.dontTakeDamage = true;

        // Look at the target.
        MaskRotation = NPC.rotation.AngleLerp(Clamp((Target.Center - HeadPosition).X * -0.009f, -0.5f, 0.3f), 0.03f);

        // Make the music stronger.
        MusicVolumeManipulationSystem.MuffleFactor = Lerp(MusicVolumeManipulationSystem.MuffleFactor, 1.75f, 0.2f);

        if (AITimer == 1)
            SoundEngine.PlaySound(GennedAssets.Sounds.Avatar.Scream with { MaxInstances = 0, Volume = 1.76f });

        // Scream.
        if (AITimer % 7 == 0)
        {
            ScreenShakeSystem.StartShakeAtPoint(HeadPosition, vibrationInterpolant * 6f);

            ExpandingChromaticBurstParticle burst = new ExpandingChromaticBurstParticle(HeadPosition + Vector2.UnitY * ZPositionScale * 282f, Vector2.Zero, Main.rand.NextBool() ? Color.Red : Color.Cyan, 30, 0.2f, 1.7f);
            burst.Spawn();

            HeadPosition += Main.rand.NextVector2CircularEdge(50f, 50f) * vibrationInterpolant;

            GeneralScreenEffectSystem.ChromaticAberration.Start(NPC.Center, 6f, 7);
        }

        float animationCompletion = InverseLerp(0f, Awaken_ScreamTime, AITimer);
        float armHorizontalMovementSpeed = SmoothStep(56f, 0f, InverseLerp(0f, 0.07f, animationCompletion));

        LeftArmPosition += Main.rand.NextVector2CircularEdge(5f, 10f) * vibrationInterpolant;
        RightArmPosition += Main.rand.NextVector2CircularEdge(5f, 10f) * vibrationInterpolant;
        LeftArmPosition.X -= armHorizontalMovementSpeed;
        RightArmPosition.X += armHorizontalMovementSpeed;

        while (LeftArmPosition.Y < NPC.Center.Y + 20f)
            LeftArmPosition += Vector2.UnitY * 10f;
        while (RightArmPosition.Y < NPC.Center.Y + 20f)
            RightArmPosition += Vector2.UnitY * 10f;

        // Make the wind go super fast.
        AvatarOfEmptinessSky.WindSpeedFactor = 2.2f;

        // Keep the camera on the Avatar.
        LockCameraToSelf(1f, SmoothStep(-0.14f, 0.41f, InverseLerp(0f, 0.15f, animationCompletion)));

        if (Main.netMode != NetmodeID.Server)
        {
            ManagedScreenFilter blurShader = ShaderManager.GetFilter("NoxusBoss.RadialMotionBlurShader");
            blurShader.TrySetParameter("blurIntensity", InverseLerpBump(0f, 0.2f, 0.85f, 0.98f, animationCompletion) * 0.25f);
            blurShader.Activate();
        }

        // Ensure that the border waits a bit before appearing once the Avatar starts attacking, to prevent cheap hits.
        BorderAppearanceDelay = SecondsToFrames(3f);
    }

    public void SetHitboxSize(Vector2 size)
    {
        NPC.TopLeft += NPC.Size * 0.5f;
        NPC.Size = size;
        NPC.TopLeft -= NPC.Size * 0.5f;
    }

    public void LockCameraToSelf(float panInterpolant, float zoomIncrease)
    {
        CalamityCompatibility.ResetStealthBarOpacity(Main.LocalPlayer);
        CameraPanSystem.PanTowards(HeadPosition - Vector2.UnitY * ZPositionScale * 30f, panInterpolant);
        CameraPanSystem.Zoom = panInterpolant * -0.14f + zoomIncrease * 0.6f;
    }
}
