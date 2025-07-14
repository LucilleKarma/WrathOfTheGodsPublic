using Luminance.Common.StateMachines;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using NoxusBoss.Assets;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity.Projectiles;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity.Rendering;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity.Rendering.RenderSteps;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity.SpecificEffectManagers;
using NoxusBoss.Core.AdvancedProjectileOwnership;
using NoxusBoss.Core.CrossCompatibility.Inbound;
using NoxusBoss.Core.Graphics.GeneralScreenEffects;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.NamelessDeity;

public partial class NamelessDeityBoss : ModNPC
{
    /// <summary>
    /// How long it's been since the black hole disappeared during Nameless' Crush Star into Quasar state.
    /// </summary>
    public ref float CrushStarIntoQuasar_PostAttackTimer => ref NPC.ai[2];

    /// <summary>
    /// How long Nameless spends redirecting near the player at the start of his Crush Star into Quasar state.
    /// </summary>
    public static int CrushStarIntoQuasar_RedirectTime => GetAIInt("CrushStarIntoQuasar_RedirectTime");

    /// <summary>
    /// How long Nameless spends building up pressure on the star during his Crush Star into Quasar state.
    /// </summary>
    public static int CrushStarIntoQuasar_StarPressureBuildTime => GetAIInt("CrushStarIntoQuasar_StarPressureBuildTime");

    /// <summary>
    /// How long Nameless spends waiting after building up pressure on the star to create a supernova and quasar during his Crush Star into Quasar state.
    /// </summary>
    public static int CrushStarIntoQuasar_SupernovaDelay => GetAIInt("CrushStarIntoQuasar_SupernovaDelay");

    /// <summary>
    /// How long Nameless waits after creating the supernova to shoot plasma projectiles during his Crush Star into Quasar state.
    /// </summary>
    public static int CrushStarIntoQuasar_PlasmaShootDelay => GetAIInt("CrushStarIntoQuasar_PlasmaShootDelay");

    /// <summary>
    /// The rate at which Nameless releases plasma projectiles in his first phase during his Crush Star into Quasar state.
    /// </summary>
    public static int CrushStarIntoQuasar_PlasmaShootRatePhase1 => GetAIInt("CrushStarIntoQuasar_PlasmaShootRatePhase1");

    /// <summary>
    /// The rate at which Nameless releases plasma projectiles in his second phase during his Crush Star into Quasar state.
    /// </summary>
    public static int CrushStarIntoQuasar_PlasmaShootRatePhase2 => GetAIInt("CrushStarIntoQuasar_PlasmaShootRatePhase2");

    /// <summary>
    /// How long Nameless' quasar lasts during his Crush Star into Quasar state.
    /// </summary>
    public static int CrushStarIntoQuasar_QuasarLifetime => GetAIInt("CrushStarIntoQuasar_QuasarLifetime");

    /// <summary>
    /// How long Nameless spends releasing plasma projectiles during his Crush Star into Quasar state.
    /// </summary>
    public static int CrushStarIntoQuasar_PlasmaShootTime => CrushStarIntoQuasar_QuasarLifetime - SecondsToFrames(1.5f);

    /// <summary>
    /// The speed of plasma projectiles during Nameless' Crush Star into Quasar state.
    /// </summary>
    public static float CrushStarIntoQuasar_PlasmaShootSpeed => GetAIFloat("CrushStarIntoQuasar_PlasmaShootSpeed");

    /// <summary>
    /// The desired orbit offset of Nameless' hands during the Crush Star into Quasar state as he crushes the star.
    /// </summary>
    public static float CrushStarIntoQuasar_PressureHandOrbitOffset => GetAIFloat("CrushStarIntoQuasar_PressureHandOrbitOffset");

    /// <summary>
    /// The desired orbit offset of Nameless' hands during the Crush Star into Quasar state as he crushes the star.
    /// </summary>
    public static float CrushStarIntoQuasar_QuasarAcclerationForce => GetAIFloat("CrushStarIntoQuasar_QuasarAcclerationForce");

    /// <summary>
    /// The desired orbit offset of Nameless' hands during the Crush Star into Quasar state as he crushes the star.
    /// </summary>
    public static float CrushStarIntoQuasar_QuasarMaxSpeed => GetAIFloat("CrushStarIntoQuasar_QuasarMaxSpeed");

    [AutomatedMethodInvoke]
    public void LoadStateTransitions_CrushStarIntoQuasar()
    {
        // Load the transition from CrushStarIntoQuasar to the next in the cycle.
        // These happens if either there's no star or quasar projectile. Since the quasar eventually disappears, this process catches both edge-cases and natural attack cycle progression.
        StateMachine.RegisterTransition(NamelessAIType.CrushStarIntoQuasar, null, false, () =>
        {
            bool noStarsOrQuasars = !AnyProjectiles(ModContent.ProjectileType<ControlledStar>()) && !AnyProjectiles(ModContent.ProjectileType<BlackHoleHostile>());
            return noStarsOrQuasars && CrushStarIntoQuasar_PostAttackTimer >= 30f;
        }, () => NPC.Opacity = 1f);

        // Load the AI state behavior.
        StateMachine.RegisterStateBehavior(NamelessAIType.CrushStarIntoQuasar, DoBehavior_CrushStarIntoQuasar);
    }

    public void DoBehavior_CrushStarIntoQuasar()
    {
        int redirectTime = CrushStarIntoQuasar_RedirectTime;
        int starPressureBuildTime = CrushStarIntoQuasar_StarPressureBuildTime;
        int supernovaDelay = CrushStarIntoQuasar_SupernovaDelay + 24;
        int plasmaShootDelay = CrushStarIntoQuasar_PlasmaShootDelay;
        int plasmaShootRate = CurrentPhase >= 1 ? CrushStarIntoQuasar_PlasmaShootRatePhase2 : CrushStarIntoQuasar_PlasmaShootRatePhase1;
        int plasmaShootTime = CrushStarIntoQuasar_PlasmaShootTime;
        float plasmaShootSpeed = CrushStarIntoQuasar_PlasmaShootSpeed * Pow(DifficultyFactor, 0.56f);
        float handOrbitOffset = CrushStarIntoQuasar_PressureHandOrbitOffset;
        float pressureInterpolant = InverseLerp(redirectTime, redirectTime + starPressureBuildTime, AITimer);

        plasmaShootRate = (int)Clamp(plasmaShootRate / Pow(DifficultyFactor, 0.54f), 1f, plasmaShootRate);

        // The balancing reasons behind this are a bit complicated.
        // Fundamentally, I don't believe the sparks being centered around the quasar is good for introductory behaviors for the boss.
        // When it's relative to the quasar, it sets an incredibly high bar of mechanical expectations, because at that point in order to
        // learn the attack you have to not just learn how to move the player, but secretly also how to move the quasar, which is far less easy.
        // Testers have had mixed reactions on it, and I don't want to necessarily deny those who can handle and enjoy that skill bar, hence this being in death mode.
        // However, for everyone else I think it's more consistent with the rest of Nameless' difficulty to not have this be the case.
        bool centerSparksAroundQuasar = CommonCalamityVariables.DeathModeActive;

        // Flap wings.
        UpdateWings(AITimer / 40f);

        Projectile? star = null;
        Projectile? quasar = null;
        List<Projectile> stars = AllProjectilesByID(ModContent.ProjectileType<ControlledStar>()).ToList();
        List<Projectile> quasars = AllProjectilesByID(ModContent.ProjectileType<BlackHoleHostile>()).ToList();
        if (stars.Count != 0)
            star = stars.First();
        if (quasars.Count != 0)
            quasar = quasars.First();

        // Use the robed arm variant.
        if ((star is not null || quasar is not null) && !RenderComposite.Find<ArmsStep>().UsingPreset)
        {
            RenderComposite.Find<ArmsStep>().ArmTexture.ForceToTexture("Arm1");
            RenderComposite.Find<ArmsStep>().ForearmTexture.ForceToTexture("Forearm1");
            RenderComposite.Find<ArmsStep>().HandTexture.ForceToTexture("Hand1");
        }

        // Make relative darkening effects stay in place.
        float darkeningIncrement = InverseLerp(0f, 60f, AITimer - redirectTime - starPressureBuildTime - supernovaDelay).Squared() * 0.08f;
        RelativeDarkening = Clamp(RelativeDarkening + darkeningIncrement, 0.5f, 0.81f);

        if (Main.netMode != NetmodeID.Server)
        {
            float vignetteCompletion = InverseLerp(-37f, 5f, AITimer - redirectTime - starPressureBuildTime - supernovaDelay);
            float vignetteIntensity = InverseLerpBump(0f, 0.35f, 0.9f, 1f, vignetteCompletion);
            float distortionIntensity = InverseLerp(-65f, 0f, AITimer - redirectTime - starPressureBuildTime - supernovaDelay);
            if (AITimer <= redirectTime + starPressureBuildTime + supernovaDelay + 30f)
            {
                if (AITimer >= redirectTime + starPressureBuildTime + supernovaDelay)
                    distortionIntensity = 0f;

                ManagedScreenFilter vignetteShader = ShaderManager.GetFilter("NoxusBoss.CollapsingStarVignetteShader");
                vignetteShader.TrySetParameter("intensity", vignetteIntensity);
                vignetteShader.TrySetParameter("distortionIntensity", distortionIntensity);
                if (star is not null)
                    vignetteShader.TrySetParameter("vignetteSource", Vector2.Transform(star.Center - Main.screenPosition, Main.GameViewMatrix.TransformationMatrix));
                vignetteShader.Activate();
            }
        }

        // Destroy leftover starbursts on the first frame.
        if (AITimer == 1f)
        {
            int arcingStarburstID = ModContent.ProjectileType<ArcingStarburst>();
            int starburstID = ModContent.ProjectileType<Starburst>();
            foreach (Projectile p in Main.ActiveProjectiles)
            {
                if (p.type != arcingStarburstID && p.type != starburstID)
                    continue;

                p.Kill();
            }

            return;
        }

        // Keep the star below Nameless, and disable its contact damage now that it's not an actual attacking element anymore.
        if (star is not null)
        {
            float starFlySpeedInterpolant = Pow(InverseLerp(0f, 60f, AITimer), 4f);
            Vector2 starDestination = NPC.Center + Vector2.UnitY * (350f - pressureInterpolant * 70f);
            star.Center = Vector2.Lerp(star.Center, starDestination, starFlySpeedInterpolant);
            star.damage = 0;
        }

        // Have Nameless rapidly attempt to hover above the player at first, with a bit of a horizontal offset.
        if (AITimer < redirectTime)
        {
            Vector2 hoverDestination = Target.Center + new Vector2((Target.Center.X < NPC.Center.X).ToDirectionInt() * 300f, -250f);
            NPC.SmoothFlyNear(hoverDestination, 0.12f, 0.85f);

            if (star is not null)
                DoBehavior_CrushIntoQuasar_UpdateHands(star.Center, Vector2.Zero, 495f);
        }
        else if (quasar is null && star is not null)
        {
            NPC.velocity = Vector2.Lerp(NPC.velocity, NPC.SafeDirectionTo(Target.Center - Vector2.UnitY * 300f) * 3f, 0.03f);

            // Make hands close in on the star, as though collapsing it.
            // The hands jitter a bit during this, as a way of indicating that they're fighting slightly to collapse the star.
            float starScale = Utils.Remap(pressureInterpolant, 0.1f, 0.8f, ControlledStar.MaxScale, 0.8f);
            star.scale = starScale;
            star.ai[1] = Saturate(pressureInterpolant + 0.002f);
            handOrbitOffset += Sin(AITimer / 4f) * pressureInterpolant * 8f;

            Vector2 pressureJitterOffset = Main.rand.NextVector2Circular(16f, 12f) * pressureInterpolant.Squared();
            DoBehavior_CrushIntoQuasar_UpdateHands(star.Center, pressureJitterOffset, SmoothStep(495f, 220f, pressureInterpolant.Squared()));
        }
        else if (quasar is not null)
        {
            // Hover in place silently below the player.
            float teleportAwayInterpolant = InverseLerp(0f, 9f, AITimer - redirectTime - starPressureBuildTime - supernovaDelay);
            TeleportVisualsInterpolant = teleportAwayInterpolant * 0.5f;
            if (TeleportVisualsInterpolant >= 0.5f)
                TeleportVisualsInterpolant = 0f;

            if (teleportAwayInterpolant >= 1f)
            {
                NPC.Center = quasar.Center;
                NPC.velocity = Vector2.Zero;
                NPC.dontTakeDamage = true;
                NPC.Opacity = 0f;
                for (int i = 0; i < Hands.Count; i++)
                    Hands[i].Opacity = 0f;
                DefaultUniversalHandMotion();
            }
            else
                NPC.velocity *= 0.9f;
        }
        else
        {
            NPC.velocity.Y -= InverseLerp(0f, 7f, CrushStarIntoQuasar_PostAttackTimer) * 2.2f;
            NPC.dontTakeDamage = true;
            NPC.Opacity = 1f;
            for (int i = 0; i < Hands.Count; i++)
                Hands[i].Opacity = 1f;
            DefaultUniversalHandMotion();

            TeleportVisualsInterpolant = InverseLerp(0f, 21f, CrushStarIntoQuasar_PostAttackTimer) * 0.5f + 0.5f;
            if (TeleportVisualsInterpolant >= 1f)
            {
                TeleportVisualsInterpolant = 0f;
                NPC.velocity *= 0.82f;
            }

            CrushStarIntoQuasar_PostAttackTimer++;
        }

        // Play the star crush sound.
        if (AITimer == Math.Max(redirectTime, redirectTime + starPressureBuildTime + supernovaDelay - 183))
            SoundEngine.PlaySound(GennedAssets.Sounds.NamelessDeity.StarCrush with { Volume = 1.4f });

        // Destroy the star and create a supernova and quasar.
        if (AITimer == redirectTime + starPressureBuildTime + supernovaDelay - 1)
        {
            // Apply sound and visual effects.
            SoundEngine.PlaySound(GennedAssets.Sounds.NamelessDeity.BigSupernova with { Volume = 1.5f, MaxInstances = 10 });
            if (star is not null && Main.netMode != NetmodeID.MultiplayerClient)
            {
                NPC.NewProjectileBetter(NPC.GetSource_FromAI(), star.Center, Vector2.Zero, ModContent.ProjectileType<Supernova>(), 0, 0f);
                NPC.NewProjectileBetter(NPC.GetSource_FromAI(), star.Center, Vector2.Zero, ModContent.ProjectileType<BlackHoleHostile>(), QuasarDamage, 0f, -1, 0f, 0f, CrushStarIntoQuasar_QuasarLifetime);
                NPC.NewProjectileBetter(NPC.GetSource_FromAI(), star.Center, Vector2.Zero, ModContent.ProjectileType<ExplodingStar>(), 0, 0f, -1, 0f, 0.6f);
            }

            if (star is not null)
            {
                ScreenShakeSystem.StartShakeAtPoint(star.Center, 25f);
                GeneralScreenEffectSystem.ChromaticAberration.Start(star.Center, 1.5f, 54);
                NamelessDeityKeyboardShader.BrightnessIntensity = 1f;

                star.Kill();
            }

            // Delete the hands.
            DestroyAllHands();

            NPC.netUpdate = true;
        }

        // Create plasma around the player that converges into the black quasar.
        if (Main.netMode != NetmodeID.MultiplayerClient &&
            quasar is not null &&
            AITimer >= redirectTime + starPressureBuildTime + supernovaDelay + plasmaShootDelay &&
            AITimer <= redirectTime + starPressureBuildTime + supernovaDelay + plasmaShootDelay + plasmaShootTime &&
            AITimer % plasmaShootRate == 0)
        {
            float spawnOffsetBoost = centerSparksAroundQuasar ? quasar.Distance(Target.Center) : 150f;
            Vector2 plasmaSpawnCenter = centerSparksAroundQuasar ? quasar.Center : Target.Center;
            Vector2 plasmaSpawnPosition = plasmaSpawnCenter + (TwoPi * AITimer / 30f).ToRotationVector2() * (Main.rand.NextFloat(600f, 700f) + spawnOffsetBoost);
            Vector2 plasmaVelocity = (plasmaSpawnCenter - plasmaSpawnPosition).SafeNormalize(Vector2.UnitY) * plasmaShootSpeed;
            while (Target.Center.WithinRange(plasmaSpawnPosition, 1040f))
                plasmaSpawnPosition -= plasmaVelocity;

            NPC.NewProjectileBetter(NPC.GetSource_FromAI(), plasmaSpawnPosition, plasmaVelocity, ModContent.ProjectileType<ConvergingSupernovaEnergy>(), SupernovaPlasmaDamage, 0f);
        }
    }

    public void DoBehavior_CrushIntoQuasar_UpdateHands(Vector2 starPosition, Vector2 jitterOffset, float horizontalStarHandOffset)
    {
        while (Hands.Count < 3)
            ConjureHandsAtPosition(NPC.Center - Vector2.UnitX * 300f, Vector2.Zero);
        while (Hands.Count < 4)
            ConjureHandsAtPosition(NPC.Center + Vector2.UnitX * 300f, Vector2.Zero);

        NamelessDeityHand leftMeditationHand = Hands[0];
        NamelessDeityHand rightMeditationHand = Hands[1];

        // Have two hands move outward in a meditative pose.
        float moveSpeedInterpolant = InverseLerp(0f, 60f, AITimer);
        DefaultHandDrift(leftMeditationHand, NPC.Center + new Vector2(-3300f, 500f) * TeleportVisualsAdjustedScale, moveSpeedInterpolant * 1.9f);
        DefaultHandDrift(rightMeditationHand, NPC.Center + new Vector2(3300f, 500f) * TeleportVisualsAdjustedScale, moveSpeedInterpolant * 1.9f);
        leftMeditationHand.ArmInverseKinematicsFlipOverride = false;
        rightMeditationHand.ArmInverseKinematicsFlipOverride = true;
        leftMeditationHand.RotationOffset = 0f;
        rightMeditationHand.RotationOffset = 0f;

        // Keep two hands focused on the star.
        float starHandVerticalOffset = Cos01(TwoPi * AITimer / 90f) * 30f;
        NamelessDeityHand leftStarHand = Hands[2];
        NamelessDeityHand rightStarHand = Hands[3];

        float starHandFlySpeed = Pow(moveSpeedInterpolant, 6f);
        Vector2 leftStarHandDestination = starPosition + jitterOffset + new Vector2(-horizontalStarHandOffset, 35f - starHandVerticalOffset) * TeleportVisualsAdjustedScale;
        Vector2 rightStarHandDestination = starPosition + jitterOffset + new Vector2(horizontalStarHandOffset, 35f + starHandVerticalOffset) * TeleportVisualsAdjustedScale;
        leftStarHand.FreeCenter = Vector2.Lerp(leftStarHand.FreeCenter, leftStarHandDestination, starHandFlySpeed);
        rightStarHand.FreeCenter = Vector2.Lerp(rightStarHand.FreeCenter, rightStarHandDestination, starHandFlySpeed);

        leftStarHand.RotationOffset = leftStarHand.RotationOffset.AngleLerp(leftStarHand.ActualCenter.AngleTo(starPosition) - PiOver2 - 0.5f, starHandFlySpeed * 0.2f);
        rightStarHand.RotationOffset = rightStarHand.RotationOffset.AngleLerp(rightStarHand.ActualCenter.AngleTo(starPosition) - PiOver2 + 0.5f, starHandFlySpeed * 0.2f);
        leftStarHand.ArmInverseKinematicsFlipOverride = true;
        rightStarHand.ArmInverseKinematicsFlipOverride = false;
        leftStarHand.VisuallyFlipHand = true;
        rightStarHand.VisuallyFlipHand = true;
        leftStarHand.ForearmIKLengthFactor = 1.2f;
        rightStarHand.ForearmIKLengthFactor = 1.2f;
    }
}
