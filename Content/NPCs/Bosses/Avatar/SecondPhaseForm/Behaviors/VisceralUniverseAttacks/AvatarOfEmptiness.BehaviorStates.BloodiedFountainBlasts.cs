using Luminance.Common.Easings;
using Luminance.Common.StateMachines;
using Luminance.Core.Graphics;

using Microsoft.Xna.Framework;

using NoxusBoss.Assets;
using NoxusBoss.Content.NPCs.Bosses.Avatar.Projectiles;
using NoxusBoss.Content.NPCs.Bosses.Avatar.Projectiles.SolynProjectiles;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SpecificEffectManagers;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SpecificEffectManagers.SolynDialogue;
using NoxusBoss.Content.NPCs.Friendly;
using NoxusBoss.Core.Graphics.ScreenShake;

using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm;

public partial class AvatarOfEmptiness
{
    /// <summary>
    /// The opacity of the visceral background's blood fountain.
    /// </summary>
    public float VisceralBackgroundBloodFountainOpacity
    {
        get;
        set;
    }

    /// <summary>
    /// The X offset that Solyn should attempt to move to relative to the player before the heavy rain during the Avatar's dense Bloodied Fountain Blasts attack.
    /// </summary>
    public ref float BloodiedFountainBlasts_SolynXHoverOffset => ref NPC.ai[2];

    /// <summary>
    /// How long the Avatar's Bloodied Fountain Blasts attack should go on for.
    /// </summary>
    public static int BloodiedFountainBlasts_AttackDuration => GetAIInt("BloodiedFountainBlasts_AttackDuration");

    // This exists to allow the background projectiles appear and fly upward first, indicating that they're falling back down.
    /// <summary>
    /// How long the Avatar waits before firing blood blobs from above during his Bloodied Fountain Blasts attack.
    /// </summary>
    public static int BloodiedFountainBlasts_BloodFireDelay => GetAIInt("BloodiedFountainBlasts_BloodFireDelay");

    /// <summary>
    /// The rate at which the Avatar releases blood blobs from above during his Bloodied Fountain Blasts attack.
    /// </summary>
    public static int BloodiedFountainBlasts_BloodReleaseRate => GetAIInt("BloodiedFountainBlasts_BloodReleaseRate");

    /// <summary>
    /// How long the Avatar waits before summoning torrents of blood during his dense Bloodied Fountain Blasts attack.
    /// </summary>
    public static int BloodiedFountainBlasts_GetUnderSolynGracePeriod => GetAIInt("BloodiedFountainBlasts_GetUnderSolynGracePeriod");

    /// <summary>
    /// How long the Avatar spends summoning torrents of blood during his dense Bloodied Fountain Blasts attack.
    /// </summary>
    public static int BloodiedFountainBlasts_HeavyRainTime => GetAIInt("BloodiedFountainBlasts_HeavyRainTime");

    /// <summary>
    /// How much extra damage blood blobs do during the Avatar's dense Bloodied Fountain Blasts attack.
    /// </summary>
    public static int BloodiedFountainBlasts_BloodBlobDamageBoost => GetAIInt("BloodiedFountainBlasts_BloodBlobDamageBoost");

    /// <summary>
    /// The maximum hover offset Solyn solyn can use during the Avatar's dense Bloodied Fountain Blasts attack.
    /// </summary>
    public static float BloodiedFountainBlasts_MaxSolynXOffset => GetAIFloat("BloodiedFountainBlasts_MaxSolynXOffset");

    /// <summary>
    /// The horizontal coverage of blood blobs during the Avatar's Bloodied Fountain Blasts attack.
    /// </summary>
    public static float BloodiedFountainBlasts_BloodHorizontalCoverage => GetAIFloat("BloodiedFountainBlasts_BloodHorizontalCoverage");

    [AutomatedMethodInvoke]
    public void LoadState_BloodiedFountainBlasts()
    {
        StateMachine.RegisterTransition(AvatarAIType.BloodiedFountainBlasts, AvatarAIType.BloodiedFountainBlasts_DenseBurst, false, () =>
        {
            return AITimer >= BloodiedFountainBlasts_AttackDuration;
        });

        StateMachine.RegisterTransition(AvatarAIType.BloodiedFountainBlasts_DenseBurst, null, false, () =>
        {
            return AITimer >= BloodiedFountainBlasts_GetUnderSolynGracePeriod + BloodiedFountainBlasts_HeavyRainTime + 90;
        });

        StateMachine.RegisterStateBehavior(AvatarAIType.BloodiedFountainBlasts, DoBehavior_BloodiedFountainBlasts);
        StateMachine.RegisterStateBehavior(AvatarAIType.BloodiedFountainBlasts_DenseBurst, DoBehavior_BloodiedFountainBlasts_DenseBurst);
        AttackDimensionRelationship[AvatarAIType.BloodiedFountainBlasts] = AvatarDimensionVariants.VisceralDimension;
        AttackDimensionRelationship[AvatarAIType.BloodiedFountainBlasts_DenseBurst] = AvatarDimensionVariants.VisceralDimension;

        StatesToNotStartTeleportDuring.Add(AvatarAIType.BloodiedFountainBlasts);
        StatesToNotStartTeleportDuring.Add(AvatarAIType.BloodiedFountainBlasts_DenseBurst);
    }

    public void DoBehavior_BloodiedFountainBlasts()
    {
        AvatarOfEmptinessSky.WindSpeedFactor = 2.87f;

        HideBar = true;
        NPC.Center = Vector2.Lerp(NPC.Center, Target.Center - Vector2.UnitY * 300f, 0.04f);
        NPC.dontTakeDamage = true;
        NPC.Opacity = InverseLerp(10f, 6f, ZPosition);
        ZPosition = MathF.Max(ZPosition, InverseLerp(0f, 40f, AITimer).Squared() * 11f);

        float ambienceMuffleInterpolant = InverseLerpBump(0f, 45f, 180f, 240f, AITimer);
        AmbientSoundVolumeFactor = SmoothStep(1f, 0.35f, ambienceMuffleInterpolant);

        if (AITimer == 1)
        {
            if (SoundEngine.TryGetActiveSound(SoundEngine.PlaySound(GennedAssets.Sounds.Avatar.BloodFountainErupt), out ActiveSound? sound))
                sound.Volume *= 3.2f;
        }

        float rumble = InverseLerpBump(0f, 60f, 60f, 67f, AITimer).Squared() * 6f;
        if (rumble > 0f)
            ScreenShakeSystem.SetUniversalRumble(rumble, TwoPi, null, 0.3f);
        if (AITimer >= 67 && AITimer <= 91)
        {
            float speedFactor = Utils.Remap(AITimer, 67f, 91f, 1f, 1.4f);
            CreateBackgroundBloodSplatter(50, speedFactor, 1.15f);

            if (AITimer == 67)
            {
                CustomScreenShakeSystem.Start(42, 60f).
                    WithDirectionalBias(new Vector2(0.75f, 1f)).
                    WithDissipationCurve(EasingCurves.Quartic).
                    WithEasingType(EasingType.In);
            }
        }
        if (AITimer >= 67)
            CreateBackgroundBloodSplatter(15, 2.1f, 0.276f);

        if (AITimer % BloodiedFountainBlasts_BloodReleaseRate == 0 && AITimer >= 67)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                if (AITimer <= LilyStars_AttackDuration - 90)
                {
                    Vector2 backgroundBloodSpawnPosition = Target.Center + new Vector2(Main.rand.NextFloatDirection() * 95f, Main.rand.NextFloat(-60f, -30f));
                    Vector2 backgroundBloodVelocity = -Vector2.UnitY.RotatedByRandom(0.41f) * Main.rand.NextFloat(9.5f, 18.25f) + Main.rand.NextVector2Circular(4f, 4f);
                    NewProjectileBetter(NPC.GetSource_FromAI(), backgroundBloodSpawnPosition, backgroundBloodVelocity, ModContent.ProjectileType<BackgroundBloodBlob>(), 0, 0f);
                }

                if (AITimer >= BloodiedFountainBlasts_BloodFireDelay)
                {
                    Vector2 bloodSpawnOffset = new Vector2(Main.rand.NextFloatDirection() * BloodiedFountainBlasts_BloodHorizontalCoverage, -880f);
                    Vector2 bloodSpawnPosition = Target.Center + bloodSpawnOffset;
                    if (bloodSpawnPosition.Y < 150f)
                        bloodSpawnPosition.Y = 150f;

                    Vector2 bloodVelocity = new Vector2(bloodSpawnOffset.X * 0.004f, 21f);
                    NewProjectileBetter(NPC.GetSource_FromAI(), bloodSpawnPosition, bloodVelocity, ModContent.ProjectileType<BloodBlob>(), BloodBlobDamage, 0f);
                }
            }
        }

        PerformStandardLimbUpdates(2f);
    }

    public void DoBehavior_BloodiedFountainBlasts_DenseBurst()
    {
        HideBar = true;
        NPC.Center = Vector2.Lerp(NPC.Center, Target.Center - Vector2.UnitY * 300f, 0.04f);
        NPC.dontTakeDamage = true;
        NPC.Opacity = 0f;

        CreateBackgroundBloodSplatter(15, 2.1f, 0.276f);

        if (Main.netMode != NetmodeID.MultiplayerClient && AITimer >= BloodiedFountainBlasts_GetUnderSolynGracePeriod - 60 && AITimer <= BloodiedFountainBlasts_GetUnderSolynGracePeriod + BloodiedFountainBlasts_HeavyRainTime - 60)
        {
            Vector2 backgroundBloodSpawnPosition = Target.Center + new Vector2(Main.rand.NextFloatDirection() * 95f, Main.rand.NextFloat(-60f, -30f));
            Vector2 backgroundBloodVelocity = -Vector2.UnitY.RotatedByRandom(0.32f) * Main.rand.NextFloat(12f, 25f) + Main.rand.NextVector2Circular(5f, 5f);
            NewProjectileBetter(NPC.GetSource_FromAI(), backgroundBloodSpawnPosition, backgroundBloodVelocity, ModContent.ProjectileType<BackgroundBloodBlob>(), 0, 0f);
        }

        if (AITimer >= BloodiedFountainBlasts_GetUnderSolynGracePeriod && AITimer <= BloodiedFountainBlasts_GetUnderSolynGracePeriod + BloodiedFountainBlasts_HeavyRainTime)
            DoBehavior_BloodiedFountainBlasts_DenseBurst_CreateProjectilesNearSolyn();

        SolynAction = DoBehavior_BloodiedFountainBlasts_DenseBurst_SolynBehavior;
        PerformStandardLimbUpdates(2f);
    }

    public void DoBehavior_BloodiedFountainBlasts_DenseBurst_CreateProjectilesNearSolyn()
    {
        int solynIndex = NPC.FindFirstNPC(ModContent.NPCType<BattleSolyn>());
        if (Main.netMode != NetmodeID.MultiplayerClient && solynIndex >= 0)
        {
            NPC solyn = Main.npc[solynIndex];
            for (int i = 0; i < 2; i++)
            {
                Vector2 bloodSpawnOffset = new Vector2(Main.rand.NextFloatDirection() * BloodiedFountainBlasts_BloodHorizontalCoverage, -1080f);
                Vector2 bloodSpawnPosition = new Vector2(Target.Center.X, MathF.Min(solyn.Center.Y, Target.Center.Y)) + bloodSpawnOffset;
                if (bloodSpawnPosition.Y < 150f)
                    bloodSpawnPosition.Y = 150f;

                Vector2 bloodVelocity = new Vector2(bloodSpawnOffset.X * 0.0027f, Main.rand.NextFloat(125f, 135f));
                NewProjectileBetter(NPC.GetSource_FromAI(), bloodSpawnPosition, bloodVelocity, ModContent.ProjectileType<BloodBlob>(), BloodBlobDamage + BloodiedFountainBlasts_BloodBlobDamageBoost, 0f, -1, 0f, 400f, 0.5f);
            }

            Vector2 backgroundBloodSpawnOffset = new Vector2(Main.rand.NextFloatDirection() * BloodiedFountainBlasts_BloodHorizontalCoverage, -1080f);
            Vector2 backgroundBloodSpawnPosition = new Vector2(Target.Center.X, MathF.Min(solyn.Center.Y, Target.Center.Y)) + backgroundBloodSpawnOffset;
            if (backgroundBloodSpawnPosition.Y < 150f)
                backgroundBloodSpawnPosition.Y = 150f;

            Vector2 backgroundBloodVelocity = new Vector2(backgroundBloodSpawnOffset.X * 0.0027f, Main.rand.NextFloat(35f, 75f));
            NewProjectileBetter(NPC.GetSource_FromAI(), backgroundBloodSpawnPosition, backgroundBloodVelocity, ModContent.ProjectileType<BackgroundBloodBlob>(), 0, 0f);
        }
        ScreenShakeSystem.StartShake(1.5f);
    }

    public void DoBehavior_BloodiedFountainBlasts_DenseBurst_SolynBehavior(BattleSolyn solyn)
    {
        NPC solynNPC = solyn.NPC;

        if (AITimer == 5 && Main.netMode != NetmodeID.MultiplayerClient)
        {
            BloodiedFountainBlasts_SolynXHoverOffset = Main.rand.NextFloatDirection() * BloodiedFountainBlasts_MaxSolynXOffset;
            NPC.netUpdate = true;
        }

        if (AITimer == 60)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
                NewProjectileBetter(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero, ModContent.ProjectileType<SolynProtectiveForcefield>(), 0, 0f, -1, 0, solyn.NPC.whoAmI);

            SolynWorldDialogueManager.CreateNew("Mods.NoxusBoss.Dialog.SolynStayBelowMe", -solynNPC.spriteDirection, solynNPC.Top - Vector2.UnitY * 90f, 150, true);
        }

        int hoverRedirectTime = 30;
        if (AITimer <= hoverRedirectTime + 25)
        {
            float redirectSpeed = Lerp(0.11f, 0.25f, Convert01To010(InverseLerp(0f, hoverRedirectTime, AITimer).Squared()));
            Vector2 hoverDestination = solyn.Player.Center + new Vector2(BloodiedFountainBlasts_SolynXHoverOffset, -250f);
            solynNPC.SmoothFlyNearWithSlowdownRadius(hoverDestination, redirectSpeed, 1f - redirectSpeed, 50f);
        }

        else
        {
            solynNPC.velocity *= 0.85f;

            if (Distance(solynNPC.Center.Y, solyn.Player.Center.Y) >= 1200f)
                solynNPC.Center = Vector2.Lerp(solynNPC.Center, solyn.Player.Center, 0.1f);
        }

        solynNPC.rotation = solynNPC.velocity.X * 0.008f;
        if (Abs(solynNPC.velocity.X) >= 3f)
            solynNPC.spriteDirection = solynNPC.velocity.X.NonZeroSign();

        solyn.UseStarFlyEffects();

        DoBehavior_BloodiedFountainBlasts_DenseBurst_HandleForcefield(solyn, AITimer - 60);
    }

    public void DoBehavior_BloodiedFountainBlasts_DenseBurst_HandleForcefield(BattleSolyn solyn, int timeSinceSpawned)
    {
        IEnumerable<Projectile> forcefields = AllProjectilesByID(ModContent.ProjectileType<SolynProtectiveForcefield>());
        Projectile? forcefield = forcefields.FirstOrDefault(x => x.As<SolynProtectiveForcefield>().SolynIndex == solyn.NPC.whoAmI);
        if (forcefield is null)
            return;

        forcefield.Center = solyn.NPC.Center + Vector2.UnitY * 40f;
        forcefield.Opacity = InverseLerp(0f, 11f, timeSinceSpawned);
        forcefield.scale = EasingCurves.Elastic.Evaluate(EasingType.Out, InverseLerp(0f, 105f, timeSinceSpawned)) - forcefield.As<SolynProtectiveForcefield>().FlashInterpolant * 0.1f;

        float fadeOut = InverseLerp(10f, 0f, AITimer - BloodiedFountainBlasts_GetUnderSolynGracePeriod - BloodiedFountainBlasts_HeavyRainTime);
        forcefield.scale *= fadeOut;
        forcefield.Opacity *= fadeOut;

        if (AITimer >= BloodiedFountainBlasts_GetUnderSolynGracePeriod + BloodiedFountainBlasts_HeavyRainTime && forcefield.scale <= 0f)
            forcefield.Kill();
    }
}
