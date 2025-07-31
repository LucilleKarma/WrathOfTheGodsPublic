using Luminance.Common.StateMachines;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using NoxusBoss.Assets;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity.Projectiles;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity.SpecificEffectManagers;
using NoxusBoss.Core.AdvancedProjectileOwnership;
using NoxusBoss.Core.Graphics.GeneralScreenEffects;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using static NoxusBoss.Content.NPCs.Bosses.NamelessDeity.SpecificEffectManagers.NamelessDeitySky;

namespace NoxusBoss.Content.NPCs.Bosses.NamelessDeity;

public partial class NamelessDeityBoss : ModNPC
{
    /// <summary>
    /// Whether Nameless' Clock Constellation attack has concluded.
    /// </summary>
    public bool ClockConstellation_AttackHasConcluded
    {
        get => NPC.ai[2] == 1f;
        set => NPC.ai[2] = value.ToInt();
    }

    /// <summary>
    /// How long Nameless' clock constellation spends converging during his Clock Constellation state.
    /// </summary>
    public static int ClockConstellation_ClockConvergenceDuration => (int)Clamp(GetAIInt("ClockConstellation_ClockConvergenceDuration") / Myself_DifficultyFactor, 60f, 10000f);

    /// <summary>
    /// How long Nameless' clock constellation spends spinning its hour hand to the next hour during his Clock Constellation state.
    /// </summary>
    public static int ClockConstellation_RegularSpinDuration => GetAIInt("ClockConstellation_RegularSpinDuration");

    /// <summary>
    /// How long Nameless' clock constellation spends spinning its hour hand in reverse to the previous hour during his Clock Constellation state.
    /// </summary>
    public static int ClockConstellation_ReversedTimeSpinDuration => GetAIInt("ClockConstellation_ReversedTimeSpinDuration");

    /// <summary>
    /// How long Nameless waits before continuing his attack after his clock constellation strikes a new toll during his Clock Constellation state.
    /// </summary>
    public static int ClockConstellation_TollWaitDuration => GetAIInt("ClockConstellation_TollWaitDuration");

    /// <summary>
    /// The amount of starbursts Nameless releases from his clock constellation during his Clock Constellation state.
    /// </summary>
    public static int ClockConstellation_StarburstCount => GetAIInt("ClockConstellation_StarburstCount");

    /// <summary>
    /// The rate at which Nameless releases starbursts from his clock constellation during his Clock Constellation state.
    /// </summary>
    public static int ClockConstellation_StarburstReleaseRate => (int)Clamp(GetAIInt("ClockConstellation_StarburstReleaseRate") / Pow(Myself_DifficultyFactor, 0.51f), 1f, 1000f);

    /// <summary>
    /// The radius relative to Nameless' clock constellation in which players are seriously harmed during his Clock Constellation state.
    /// </summary>
    public static float ClockConstellation_DeathZoneRadius => GetAIFloat("ClockConstellation_DeathZoneRadius") / Pow(Myself_DifficultyFactor, 0.3f);

    /// <summary>
    /// How much Nameless' clock constellation fades out at the end of the attack during his Clock Constellation state.
    /// </summary>
    public static float ClockConstellation_FadeOutIncrement => GetAIFloat("ClockConstellation_FadeOutIncrement");

    /// <summary>
    /// The starting speed factor of the hour hand of Nameless' clock constellation when time is being reversed during his Clock Constellation state.
    /// </summary>
    public static float ClockConstellation_ReversedTimeMinSpeedFactor => GetAIFloat("ClockConstellation_ReversedTimeMinSpeedFactor");

    /// <summary>
    /// The ending speed factor of the hour hand of Nameless' clock constellation when time is being reversed during his Clock Constellation state.
    /// </summary>
    public static float ClockConstellation_ReversedTimeMaxSpeedFactor => GetAIFloat("ClockConstellation_ReversedTimeMaxSpeedFactor");

    /// <summary>
    /// The ending speed factor of the hour hand of Nameless' clock constellation when time is being reversed during his Clock Constellation state.
    /// </summary>
    public static float ClockConstellation_StarburstShootSpeed => GetAIFloat("ClockConstellation_StarburstShootSpeed") * Pow(Myself_DifficultyFactor, 1.33f);

    [AutomatedMethodInvoke]
    public void LoadStateTransitions_ClockConstellation()
    {
        // Load the transition from ClockConstellation to the next in the cycle.
        StateMachine.RegisterTransition(NamelessAIType.ClockConstellation, null, false, () =>
        {
            return HeavenlyBackgroundIntensity >= 0.9999f && ClockConstellation_AttackHasConcluded;
        });


        // Load the AI state behavior.
        StateMachine.RegisterStateBehavior(NamelessAIType.ClockConstellation, DoBehavior_ClockConstellation);
    }

    public void DoBehavior_ClockConstellation()
    {
        int redirectTime = 35;
        int spinDuration = ClockConstellation_RegularSpinDuration + ClockConstellation_ReversedTimeSpinDuration;
        int waitDuration = (int)(1f / ClockConstellation_FadeOutIncrement) + ClockConstellation_TollWaitDuration * ClockConstellation.MaxTolls;
        int attackDuration = redirectTime + ClockConstellation_ClockConvergenceDuration + spinDuration + waitDuration + 10;
        IEnumerable<Projectile> clocks = AllProjectilesByID(ModContent.ProjectileType<ClockConstellation>());
        bool clockExists = clocks.Any();

        // Flap wings.
        UpdateWings(AITimer / 45f);

        // Hover near the target at first.
        if (AITimer <= redirectTime)
        {
            Vector2 hoverDestination = Target.Center + new Vector2((Target.Center.X < NPC.Center.X).ToDirectionInt() * 400f, -250f);
            NPC.Center = Vector2.Lerp(NPC.Center, hoverDestination, 0.29f);
            NPC.Opacity = 1f;

            // Make the background dark.
            ClockConstellation_AttackHasConcluded = false;
            HeavenlyBackgroundIntensity = Lerp(1f, 0.18f, AITimer / redirectTime);
            SeamScale = 0f;
        }

        // Teleport away after redirecting and create a clock constellation on top of the target.
        if (AITimer == redirectTime)
        {
            ScreenShakeSystem.StartShake(12f);
            SoundEngine.PlaySound(GennedAssets.Sounds.NamelessDeity.Supernova with { Volume = 0.8f, MaxInstances = 20 });
            SoundEngine.PlaySound(GennedAssets.Sounds.NamelessDeity.ScreamShort with { Volume = 1.05f, MaxInstances = 20 });
            SoundEngine.PlaySound(GennedAssets.Sounds.NamelessDeity.GenericBurst with { Volume = 1.3f, PitchVariance = 0.15f });
            NamelessDeityKeyboardShader.BrightnessIntensity = 0.6f;

            GeneralScreenEffectSystem.RadialBlur.Start(NPC.Center, 1f, 30);

            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                NPC.NewProjectileBetter(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero, ModContent.ProjectileType<LightWave>(), 0, 0f);
                NPC.NewProjectileBetter(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero, ModContent.ProjectileType<ClockConstellation>(), 0, 0f);
            }

            ImmediateTeleportTo(Target.Center + Vector2.UnitY * 4000f);

            // Play a sound to accompany the converging stars.
            SoundEngine.PlaySound(GennedAssets.Sounds.NamelessDeity.StarConvergence with { Volume = 0.8f });
        }

        // Stay at the top of the clock after redirecting.
        if (AITimer >= redirectTime && AITimer <= attackDuration - 90f && !ClockConstellation_AttackHasConcluded)
        {
            NPC.Opacity = 1f;

            if (clockExists)
                NPC.Center = Vector2.Lerp(NPC.Center, clocks.First().Center + new Vector2(2f, Cos(AITimer / 34.5f) * 50f + 800f), 0.14f);

            // Burn the target if they try to leave the clock.
            if (clockExists && !Target.Center.WithinRange(clocks.First().Center, ClockConstellation_DeathZoneRadius) && AITimer >= redirectTime + 60f)
            {
                if (NPC.HasPlayerTarget)
                    Main.player[NPC.target].Hurt(PlayerDeathReason.ByNPC(NPC.whoAmI), Main.rand.Next(900, 950), 0);
                else if (NPC.HasNPCTarget)
                    Main.npc[NPC.TranslatedTargetIndex].SimpleStrikeNPC(9500, 0);
            }
        }

        // Go to the next attack immediately if the clock is missing when it should be present.
        if (AITimer >= redirectTime + 1 && !clockExists)
            ClockConstellation_AttackHasConcluded = true;

        if (AITimer >= attackDuration && !ClockConstellation_AttackHasConcluded)
        {
            foreach (Projectile? clock in clocks)
            {
                clock.Kill();
                ImmediateTeleportTo(clock.Center);
            }

            SoundEngine.PlaySound(GennedAssets.Sounds.NamelessDeity.Supernova with { Volume = 0.8f, MaxInstances = 20 });
            SoundEngine.PlaySound(GennedAssets.Sounds.NamelessDeity.GenericBurst with { Volume = 1.3f, PitchVariance = 0.15f });
            ScreenShakeSystem.StartShake(12f);

            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                ClockConstellation_AttackHasConcluded = true;
                NPC.Opacity = 1f;
                NPC.netUpdate = true;

                NPC.NewProjectileBetter(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero, ModContent.ProjectileType<LightWave>(), 0, 0f);
            }
        }

        // Update universal hands.
        float handHoverOffset = Sin(AITimer / 6f) * (1f - ClockConstellation.TimeIsStopped.ToInt()) * 120f + 950f;
        DefaultUniversalHandMotion(handHoverOffset);

        // Make the background return.
        if (ClockConstellation_AttackHasConcluded)
            HeavenlyBackgroundIntensity = Saturate(HeavenlyBackgroundIntensity + 0.05f);
    }
}
