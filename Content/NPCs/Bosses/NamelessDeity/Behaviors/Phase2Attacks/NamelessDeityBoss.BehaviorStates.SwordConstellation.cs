using Luminance.Common.StateMachines;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using NoxusBoss.Assets;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity.Projectiles;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity.Rendering;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity.SpecificEffectManagers;
using NoxusBoss.Content.Particles;
using NoxusBoss.Core.AdvancedProjectileOwnership;
using NoxusBoss.Core.DataStructures;
using NoxusBoss.Core.Graphics.GeneralScreenEffects;
using NoxusBoss.Core.Graphics.SpecificEffectManagers;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.NamelessDeity;

public partial class NamelessDeityBoss : ModNPC
{
    /// <summary>
    /// Nameless' horizontal sword slash counter during his Sword Constellation state.
    /// </summary>
    public int SwordSlashCounter
    {
        get;
        set;
    }

    /// <summary>
    /// Nameless' horizontal slash direction during his Sword Constellation state.
    /// </summary>
    public int SwordSlashDirection
    {
        get;
        set;
    }

    /// <summary>
    /// Nameless' horizontal sword animation timer during his Sword Constellation state.
    /// </summary>
    public int SwordAnimationTimer
    {
        get;
        set;
    }

    /// <summary>
    /// How long Nameless' first sword slash takes to conclude during his Sword Constellation state.
    /// </summary>
    public static int SwordConstellation_BaseSlashAnimationTime => (int)Clamp(GetAIInt("SwordConstellation_BaseSlashAnimationTime") / Myself_DifficultyFactor, 1f, 10000f);

    /// <summary>
    /// How long Nameless' sword constellation takes to materialize during his Sword Constellation state.
    /// </summary>
    public static int SwordConstellation_ConstellationConvergeTime => (int)Clamp(GetAIInt("SwordConstellation_ConstellationConvergeTime") / Myself_DifficultyFactor, 50f, 1000f);

    /// <summary>
    /// The lower bound for the sword slash animation duration during Nameless' Sword Constellation state.
    /// </summary>
    public static int SwordConstellation_MinSlashAnimationTime => (int)Clamp(GetAIInt("SwordConstellation_MinSlashAnimationTime") / Myself_DifficultyFactor, 32f, 1000f);

    /// <summary>
    /// How much faster each successive sword slash animation should be during Nameless' Sword Constellation state. These decrements will not go below <see cref="SwordConstellation_MinSlashAnimationTime"/>.
    /// </summary>
    public static int SwordConstellation_SlashAnimationTimeDecrement => (int)(GetAIInt("SwordConstellation_SlashAnimationTimeDecrement") + (Myself_DifficultyFactor - 1f) * 7.5f);

    /// <summary>
    /// The amount of slashes Nameless should perform during his Sword Constellation state.
    /// </summary>
    public static int SwordConstellation_SlashCount => (int)Clamp(GetAIInt("SwordConstellation_SlashCount") + Myself_DifficultyFactor * 3f, 0f, 11f);

    /// <summary>
    /// How long Nameless waits before slashing during his Sword Constellation state.
    /// </summary>
    public static int SwordConstellation_SlashDelay => GetAIInt("SwordConstellation_SlashDelay");

    /// <summary>
    /// How long Nameless' teleports go on for during his Sword Constellation state.
    /// </summary>
    public static int SwordConstellation_TeleportVisualsTime => GetAIInt("SwordConstellation_TeleportVisualsTime");

    /// <summary>
    /// The palette that this Nameless' sword twinkle particles can cycle through.
    /// </summary>
    public static readonly Palette SwordTwinklePalette = new Palette().
        AddColor(Color.SkyBlue).
        AddColor(Color.Yellow).
        AddColor(Color.Orange).
        AddColor(Color.Red);

    [AutomatedMethodInvoke]
    public void LoadStateTransitions_SwordConstellation()
    {
        // Load the transition from SwordConstellation to the next in the cycle.
        StateMachine.RegisterTransition(NamelessAIType.SwordConstellation, null, false, () =>
        {
            return SwordSlashCounter >= SwordConstellation_SlashCount && SwordAnimationTimer >= 2;
        }, () =>
        {
            SwordSlashCounter = 0;
            foreach (NamelessDeityHand hand in Hands)
                hand.ForearmIKLengthFactor = 0.5f;
        });

        // Load the AI state behavior.
        StateMachine.RegisterStateBehavior(NamelessAIType.SwordConstellation, DoBehavior_SwordConstellation);
    }

    public void DoBehavior_SwordConstellation()
    {
        int constellationConvergeTime = SwordConstellation_ConstellationConvergeTime;
        int slashAnimationTime = SwordConstellation_BaseSlashAnimationTime - SwordSlashCounter * SwordConstellation_SlashAnimationTimeDecrement;
        int slashDelay = SwordConstellation_SlashDelay;
        int slashCount = SwordConstellation_SlashCount;
        int teleportVisualsTime = SwordConstellation_TeleportVisualsTime;
        IEnumerable<Projectile> swords = AllProjectilesByID(ModContent.ProjectileType<SwordConstellation>());
        float maxHandOffset = 300f;
        ref float swordDoesDamage = ref NPC.ai[2];

        // Enforce a lower bound on the slash animation time, to prevent unfairness.
        if (slashAnimationTime < SwordConstellation_MinSlashAnimationTime)
            slashAnimationTime = SwordConstellation_MinSlashAnimationTime;

        // Use a small Z position.
        ZPosition = 0.12f;

        // Flap wings.
        UpdateWings(AITimer / 35f);

        // Summon the sword constellation, along with a single hand to wield it on the first frame.
        // Also teleport above the target.
        if (AITimer == 1)
        {
            // Delete leftover projectiles on the first frame.
            ClearAllProjectiles();

            SwordAnimationTimer = 0;
            NamelessDeityKeyboardShader.BrightnessIntensity = 1f;
            StartTeleportAnimation(() =>
            {
                Vector2 teleportOffset = Vector2.UnitX.RotatedByRandom(0.86f) * Main.rand.NextFromList(-1f, 1f) * 350f;
                Vector2 teleportPosition = Target.Center + teleportOffset;
                return teleportPosition;
            }, teleportVisualsTime, teleportVisualsTime + 4);

            // Apply visual and sound effects.
            ScreenShakeSystem.StartShakeAtPoint(NPC.Center, 9.6f);
            GeneralScreenEffectSystem.RadialBlur.Start(NPC.Center, 1f, 12);
            RadialScreenShoveSystem.Start(EyePosition, 45);

            // Create the sword.
            if (Main.netMode != NetmodeID.MultiplayerClient)
                NPC.NewProjectileBetter(NPC.GetSource_FromAI(), Target.Center, Vector2.Zero, ModContent.ProjectileType<SwordConstellation>(), SwordConstellationDamage, 0f, -1, 1f);

            // Play a sound to accompany the converging stars.
            SoundEngine.PlaySound(GennedAssets.Sounds.NamelessDeity.StarConvergenceFast with { Volume = 0.8f });
        }

        // Play mumble sounds.
        if (AITimer == constellationConvergeTime - 32f)
            PerformMumble();

        // Decide the slash direction.
        if (Distance(NPC.Center.X, Target.Center.X) >= 270f || SwordSlashDirection == 0f)
            SwordSlashDirection = NPC.velocity.X.NonZeroSign();

        if (SwordSlashCounter >= slashCount)
        {
            // Destroy the swords.
            foreach (Projectile sword in swords)
                sword.Kill();

            SwordAnimationTimer++;
            DestroyAllHands();
            return;
        }

        // Increment the slash counter.
        if (AITimer >= constellationConvergeTime)
        {
            SwordAnimationTimer++;

            // Calculate the teleport visuals interpolant.
            TeleportVisualsInterpolant = InverseLerp(-15f, -1f, SwordAnimationTimer - slashDelay - slashAnimationTime) * 0.5f;
            if (SwordSlashCounter >= 1)
                TeleportVisualsInterpolant += InverseLerp(11f, 0f, SwordAnimationTimer) * 0.5f;
            else
                TeleportVisualsInterpolant = 1f;
        }

        if (SwordAnimationTimer >= slashAnimationTime)
        {
            SwordSlashCounter++;
            SwordAnimationTimer = 0;
            NPC.netUpdate = true;

            Vector2 teleportOffsetDirection = Target.Velocity.SafeNormalize(Vector2.UnitY).RotatedByRandom(0.93f);
            if (SwordSlashCounter % 3 == 0)
                teleportOffsetDirection = teleportOffsetDirection.RotatedBy(PiOver2);

            // Vertically offset teleports have been shown in testing to be overly difficult to manage. As such, they are negated if they appear.
            while (Abs(Vector2.Dot(teleportOffsetDirection, Vector2.UnitY)) >= 0.82f)
                teleportOffsetDirection = Main.rand.NextVector2Unit();

            // You don't want to know how much pain was put into this line of code.
            teleportOffsetDirection.Y *= 0.5f;

            Vector2 teleportOffset = teleportOffsetDirection * -850f;
            ImmediateTeleportTo(Target.Center + teleportOffset);
            RerollAllSwappableTextures();
        }

        // Calculate the anticipation interpolants.
        float anticipationAnimationRatio = 0.56f;
        float playerDriftAnimationRatio = anticipationAnimationRatio * (NPC.WithinRange(Target.Center, 200f) ? 0.9f : 0.7f);
        float swordAnimationCompletion = InverseLerp(0f, slashAnimationTime - 1f, SwordAnimationTimer - slashDelay);
        float anticipationCompletion = InverseLerp(0f, anticipationAnimationRatio, swordAnimationCompletion);
        float swordScale = InverseLerpBump(-0.1f, 0.07f, 0.5f, 0.8f, swordAnimationCompletion);

        float downwardDirectionToPlayerDot = Abs(Vector2.Dot(NPC.SafeDirectionTo(Target.Center), Vector2.UnitY));
        float downwardDirectionLeniency = InverseLerp(0.54f, 0.8f, downwardDirectionToPlayerDot);
        float handOffsetAngle = SwordSlashDirection == -1 ? -0.49f : -PiOver4;
        if (SwordSlashDirection == -1)
            handOffsetAngle = -handOffsetAngle + Pi;

        // Calculate sword direction values.
        Vector2 handOffsetSquishFactor = new Vector2(SwordSlashDirection, 0.5f);
        Vector2 handOffset = handOffsetSquishFactor.SafeNormalize(Vector2.UnitY).RotatedBy(NPC.velocity.ToRotation() + handOffsetAngle) * maxHandOffset;
        Vector2 swordDirection = handOffset.SafeNormalize(Vector2.UnitY).RotatedBy(PiOver2);

        // Drift towards the target at first.
        if (swordAnimationCompletion <= playerDriftAnimationRatio)
            NPC.velocity = NPC.SafeDirectionTo(Target.Center + Vector2.UnitY * 125f) * 1.2f;

        // Slash at the target when ready.
        float slashAnticipationCompletion = InverseLerp(0f, slashDelay + (int)((slashAnimationTime - 1f) * anticipationAnimationRatio), SwordAnimationTimer);
        bool slashAboutToHappen = SwordAnimationTimer >= slashDelay + (int)((slashAnimationTime - 1f) * anticipationAnimationRatio * 0.84f);
        bool slashJustHappened = SwordAnimationTimer == slashDelay + (int)((slashAnimationTime - 1f) * anticipationAnimationRatio);
        bool slashHasHappened = SwordAnimationTimer > slashDelay + (int)((slashAnimationTime - 1f) * anticipationAnimationRatio);
        if (slashJustHappened)
        {
            float slashSpeed = NPC.Distance(Target.Center) * 0.22f;
            if (slashSpeed < 140f)
                slashSpeed = 140f;

            NPC.velocity = NPC.velocity.SafeNormalize(Vector2.UnitY) * Pow(DifficultyFactor, 0.4f) * slashSpeed;
            NPC.netUpdate = true;

            // Create sounds and visuals.
            ScreenShakeSystem.StartShakeAtPoint(NPC.Center, 10f);
            NamelessDeityKeyboardShader.BrightnessIntensity = 0.9f;
            RadialScreenShoveSystem.Start(NPC.Center, 45);
            GeneralScreenEffectSystem.HighContrast.Start(NPC.Center, SwordSlashCounter * 0.2f + 1.15f, 32);
            SoundEngine.PlaySound(GennedAssets.Sounds.NamelessDeity.SwordSlash with { Volume = 1.3f, MaxInstances = 4 });
        }

        // Slow down after the slash.
        if (SwordAnimationTimer > slashDelay + (int)((slashAnimationTime - 1f) * 0.55f) && !NPC.WithinRange(Target.Center, 230f))
            NPC.velocity *= 0.9f;

        // Reset contact damage to 0 if not attacking.
        else
            NPC.damage = 0;

        // Make the sword do damage when the slash is close to happening.
        swordDoesDamage = slashAboutToHappen.ToInt();

        // Move the hands, keeping the sword attached to the active hand.
        if (Hands.Count >= 2)
        {
            // Store the active and idle hands in temporary variables for ease of access.
            int activeHandIndex = SwordSlashDirection == 0 ? 1 : 0;
            int idleHandIndex = 1 - activeHandIndex;
            NamelessDeityHand activeHand = Hands[activeHandIndex];
            NamelessDeityHand idleHand = Hands[idleHandIndex];

            float reelBackInterpolant = Lerp(1f, 0.2f, InverseLerp(0.3f, 0.76f, slashAnticipationCompletion));
            float activeHandHorizontalOffset = Lerp(500f, 400f, downwardDirectionLeniency) * reelBackInterpolant;
            float activeHandVerticalOffset = 30f / reelBackInterpolant.Squared();
            if (activeHandHorizontalOffset < 198f)
                activeHandHorizontalOffset = 198f;

            activeHand.RotationOffset = SwordSlashDirection * -PiOver4;
            activeHand.HandType = NamelessDeityHandType.ClosedFist;
            DefaultHandDrift(activeHand, NPC.Center + handOffset + new Vector2(SwordSlashDirection * activeHandHorizontalOffset, activeHandVerticalOffset), 300f);

            activeHand.FreeCenter = NPC.Center + handOffset + Vector2.UnitX * SwordSlashDirection * activeHandHorizontalOffset;
            activeHand.ForearmIKLengthFactor = 0.6f;
            activeHand.ArmInverseKinematicsFlipOverride = SwordSlashDirection == 1;

            idleHand.ForearmIKLengthFactor = 0.5f;
            idleHand.ArmInverseKinematicsFlipOverride = null;
            idleHand.HandType = NamelessDeityHandType.ClosedFist;
            idleHand.RotationOffset = 0f;
            idleHand.DirectionOverride = SwordSlashDirection;
            DefaultHandDrift(idleHand, NPC.Center + new Vector2(SwordSlashDirection * -1000f, 140f) * TeleportVisualsAdjustedScale, 4f);

            // Update the sword.
            if (swords.Any())
            {
                Projectile sword = swords.First();
                sword.As<SwordConstellation>().SlashCompletion = anticipationCompletion;
                sword.rotation = swordDirection.ToRotation();
                if (SwordSlashDirection == 1)
                    sword.rotation += 0.3f;

                Vector2 armDirection = activeHand.Direction;
                sword.scale = swordScale;
                sword.Center = activeHand.ActualCenter + Vector2.UnitY * armDirection.Y * 100f;

                // Make the sword emit stardust as it's being fired.
                float stardustSpawnRate = InverseLerp(24f, 60f, NPC.velocity.Length());
                for (int i = 0; i < 7; i++)
                {
                    if (Main.rand.NextFloat() >= stardustSpawnRate)
                        continue;

                    int starPoints = Main.rand.Next(3, 9);
                    float starScaleInterpolant = Main.rand.NextFloat();
                    int starLifetime = (int)Lerp(36f, 90f, starScaleInterpolant);
                    float starScale = Lerp(0.7f, 1.9f, starScaleInterpolant);
                    Color starColor = SwordTwinklePalette.SampleColor(starScaleInterpolant * 0.9f);
                    starColor = Color.Lerp(starColor, Color.Wheat, 0.4f);

                    Vector2 starVelocity = NPC.velocity.SafeNormalize(Vector2.UnitY).RotatedBy(PiOver2) * Main.rand.NextFloatDirection() * 30f;
                    TwinkleParticle star = new TwinkleParticle(sword.Center + Main.rand.NextVector2Circular(60f, 150f).RotatedBy(sword.rotation), starVelocity, starColor, starLifetime, starPoints, new Vector2(Main.rand.NextFloat(0.4f, 1.6f), 1f) * starScale, starColor * 0.5f);
                    star.Spawn();
                }

                // Create a screen slice when the slash happens.
                if (Main.netMode != NetmodeID.MultiplayerClient && slashJustHappened)
                {
                    Vector2 sliceDirection = NPC.velocity.SafeNormalize(Vector2.UnitY);
                    Vector2 sliceSpawnPosition = activeHand.FreeCenter - sliceDirection * 2000f - armDirection.RotatedBy(PiOver2) * armDirection.X.NonZeroSign() * -200f;
                    NPC.NewProjectileBetter(NPC.GetSource_FromAI(), sliceSpawnPosition, sliceDirection, ModContent.ProjectileType<TelegraphedScreenSlice>(), ScreenSliceDamage, 0f, -1, 3f, 4000f);
                }
            }
        }
    }
}
