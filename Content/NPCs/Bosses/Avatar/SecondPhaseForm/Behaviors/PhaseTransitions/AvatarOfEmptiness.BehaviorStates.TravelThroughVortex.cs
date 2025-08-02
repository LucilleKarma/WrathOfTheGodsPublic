using Luminance.Common.DataStructures;
using Luminance.Common.Easings;
using Luminance.Common.StateMachines;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using NoxusBoss.Content.NPCs.Bosses.Avatar.Projectiles;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SpecificEffectManagers;
using NoxusBoss.Core.Configuration;
using NoxusBoss.Core.SoundSystems;
using ReLogic.Utilities;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm;

public partial class AvatarOfEmptiness
{
    /// <summary>
    /// Whether the Avatar has sent all players through the vortex to his home universe yet or not.
    /// </summary>
    public bool HasSentPlayersThroughVortex
    {
        get;
        set;
    }

    /// <summary>
    /// The value shown on the speedometer while the player travels through the vortex.
    /// </summary>
    public float TravelThroughVortex_ShownPlayerSpeed
    {
        get;
        set;
    }

    /// <summary>
    /// The offset of the vortex's center.
    /// </summary>
    public Vector2 TravelThroughVortex_VortexPositionOffset
    {
        get;
        set;
    }

    /// <summary>
    /// How much the vortex hole in the center should grow during the Avatar's Travel Through Vortex attack.
    /// </summary>
    public ref float TravelThroughVortex_HoleGrowInterpolant => ref NPC.ai[2];

    /// <summary>
    /// How much the vortex hole in the center should reveal the back dimension during the Avatar's Travel Through Vortex attack.
    /// </summary>
    public ref float TravelThroughVortex_RevealDimensionInterpolant => ref NPC.ai[3];

    /// <summary>
    /// How long the Avatar waits before creating stars during his Travel Through Vortex attack.
    /// </summary>
    public static int TravelThroughVortex_StarShootDelay => GetAIInt("TravelThroughVortex_StarShootDelay");

    /// <summary>
    /// How long the players spend flying through the vortex to reach the Avatar's universe.
    /// </summary>
    public static int TravelThroughVortex_TravelTime => GetAIInt("TravelThroughVortex_TravelTime");

    [AutomatedMethodInvoke]
    public void LoadState_TravelThroughVortex()
    {
        StateMachine.RegisterTransition(AvatarAIType.TravelThroughVortex, null, false, () =>
        {
            return AITimer >= TravelThroughVortex_TravelTime;
        }, () =>
        {
            HasSentPlayersThroughVortex = true;
        });

        // Prepare to enter phase 3 if ready. This will ensure that once the attack has finished the Avatar will enter the third phase.
        // As an exception, this does not happen if the Avatar is actually expecting a death animation to happen instead. This way, if a player uses a one-hit-kill
        // option the Avatar will only do the animation.
        StateMachine.AddTransitionStateHijack(originalState =>
        {
            if (CurrentPhase == 0 && WaitingForPhase3Transition)
            {
                // Transition to the next phase.
                CurrentPhase = 1;
                DrawnAsSilhouette = false;
                PreviousState = Phase3StartingAttacks.First();

                IProjOwnedByBoss<AvatarOfEmptiness>.KillAll();

                // If the Avatar was in the middle of a state chain that was interrupted (such as dead sun into lily stars) then the next parts of the state chain may linger after the phase 3 transition, potentially
                // not working due to assuming that it was going to follow after the interrupted attack.
                // In order to address this, the state stack is completely cleared.
                StateMachine.StateStack.Clear();
                foreach (AvatarAIType attack in Phase3StartingAttacks.Reverse())
                    StateMachine.StateStack.Push(StateMachine.StateRegistry[attack]);

                return AvatarAIType.TravelThroughVortex;
            }
            return originalState;
        });

        // Load the AI state behaviors.
        StateMachine.RegisterStateBehavior(AvatarAIType.TravelThroughVortex, DoBehavior_TravelThroughVortex);
    }

    public void DoBehavior_TravelThroughVortex()
    {
        Music = MusicLoader.GetMusicSlot(Mod, "Assets/Sounds/Music/AvatarUniverseTravel");
        SoundMufflingSystem.MuffleFactor = 0f;

        // Clear residual gores and dusts, so that there aren't any leaves or whatever sticking around.
        if (AITimer == 1)
        {
            Main.musicFade[Main.curMusic] = 0f;

            for (int i = 0; i < Main.maxGore; i++)
                Main.gore[i].active = false;
            for (int i = 0; i < Main.maxDust; i++)
                Main.dust[i].active = false;
        }

        if (AITimer <= 5)
            NPC.Center = Target.Center;
        NPC.velocity = Vector2.Zero;
        NPC.ShowNameOnHover = false;

        IdealDistortionIntensity = 0.7f;
        DistortionIntensity = IdealDistortionIntensity;

        // Inherit the upcoming dimension, so that when the vortex is exited the player is plopped into the dimension they're going to be in for the next attack.
        if (AttackDimensionRelationship.TryGetValue(FirstDimensionAttack, out AvatarDimensionVariant? upcomingDimension))
            AvatarOfEmptinessSky.Dimension = upcomingDimension;
        else
            AvatarOfEmptinessSky.Dimension = AvatarDimensionVariants.DarkDimension;

        // Apply a radial blur effect, to help define the look of the inner part of the vortex.
        if (Main.netMode != NetmodeID.Server && !WoTGConfig.Instance.PhotosensitivityMode)
        {
            ManagedScreenFilter blurShader = ShaderManager.GetFilter("NoxusBoss.RadialMotionBlurShader");
            blurShader.TrySetParameter("blurIntensity", InverseLerp(TravelThroughVortex_TravelTime, TravelThroughVortex_TravelTime - 60f, AITimer) * 0.23f);
            blurShader.Activate();
        }

        // Move the vortex's center around.
        float time = AITimer / 45f;
        float offsetX = Cos(time * 0.69f) * Cos(time * 1.3f) * 104f;
        float offsetY = Cos(time * 1.1f) * Cos(time * 0.4f) * 56f;
        TravelThroughVortex_VortexPositionOffset = new(offsetX, offsetY);

        // Release lily stars.
        if (Main.netMode != NetmodeID.MultiplayerClient && AITimer >= TravelThroughVortex_StarShootDelay && AITimer < TravelThroughVortex_TravelTime - SecondsToFrames(3.75f))
        {
            float lilySpeedInterpolant = InverseLerp(3.5f, 12f, Target.Velocity.Length());
            Vector2 lilySpawnPosition = Target.Center + TravelThroughVortex_VortexPositionOffset;
            Vector2 lilyStartDestination = Target.Center + Main.rand.NextVector2Circular(1600f, 1600f) + Target.Velocity * 56f;
            Vector2 lilyStartingVelocity = Target.Center.SafeDirectionTo(lilyStartDestination).RotatedByRandom(0.7f) * Main.rand.NextFloat(3f, 56f + lilySpeedInterpolant * 40f);
            if (Target.Velocity.Length() >= 8f)
                lilyStartingVelocity *= 1.4f;

            if (Main.rand.NextBool(10))
            {
                lilyStartDestination = Target.Center + Main.rand.NextVector2Circular(74f, 74f);
                lilyStartingVelocity *= 0.2f;
            }

            NewProjectileBetter(NPC.GetSource_FromAI(), lilySpawnPosition, lilyStartingVelocity, ModContent.ProjectileType<LilyStar>(), DisgustingStarDamage, 0f, -1, 11f, lilyStartDestination.X, lilyStartDestination.Y);
        }

        float previousTravelInterpolant = EasingCurves.Quintic.Evaluate(EasingType.InOut, (AITimer - 1) / (float)TravelThroughVortex_TravelTime);
        float travelInterpolant = EasingCurves.Quintic.Evaluate(EasingType.InOut, AITimer / (float)TravelThroughVortex_TravelTime);

        // Bias the player's speed in accordance with the vortex, as though their speed is being modified as they move forward through it.
        float effectivePlayerSpeed = (float)(new Vector2D(HorizontalParsecs, VerticalParsecs).Length() * (travelInterpolant - previousTravelInterpolant)) + Main.LocalPlayer.velocity.Length();
        TravelThroughVortex_ShownPlayerSpeed = effectivePlayerSpeed * 44f / 225f;

        // Use custom parsec text for the compass and depth meter.
        CompassTextInMyUniverse = Language.GetText($"Mods.{Mod.Name}.CellPhoneInfoOverrides.ParsecText").Format($"{travelInterpolant * HorizontalParsecs:n0}");
        DepthTextInMyUniverse = Language.GetText($"Mods.{Mod.Name}.CellPhoneInfoOverrides.ParsecText").Format($"{travelInterpolant * VerticalParsecs:n0}");

        TravelThroughVortex_HoleGrowInterpolant = Sqrt(InverseLerp(-SecondsToFrames(6f), 0f, AITimer - TravelThroughVortex_TravelTime));
        TravelThroughVortex_RevealDimensionInterpolant = InverseLerp(-SecondsToFrames(5.2f), 0f, AITimer - TravelThroughVortex_TravelTime);
        AmbientSoundVolumeFactor = 0f;
        HideBar = true;
        ZPosition = 0f;
        NPC.Opacity = 0f;
        NPC.dontTakeDamage = true;
    }
}
