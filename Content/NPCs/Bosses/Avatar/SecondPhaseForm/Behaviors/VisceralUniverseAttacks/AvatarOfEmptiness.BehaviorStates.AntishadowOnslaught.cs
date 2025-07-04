using Luminance.Common.DataStructures;
using Luminance.Common.StateMachines;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using NoxusBoss.Assets;
using NoxusBoss.Content.NPCs.Bosses.Avatar.Projectiles;
using NoxusBoss.Core.Graphics.SpecificEffectManagers;
using NoxusBoss.Core.World.TileDisabling;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm;

public partial class AvatarOfEmptiness
{
    /// <summary>
    /// The vertical offset of the Avatar's left arm during the antishadow onslaught attack. Used to make his body jolt forward upon being hit.
    /// </summary>
    public ref float AntishadowOnslaught_LeftArmOffset => ref NPC.ai[1];

    /// <summary>
    /// The vertical offset of the Avatar's right arm during the antishadow onslaught attack. Used to make his body jolt forward upon being hit.
    /// </summary>
    public ref float AntishadowOnslaught_RightArmOffset => ref NPC.ai[2];

    /// <summary>
    /// The vertical offset of the Avatar's head during the antishadow onslaught attack. Used to make his body jolt forward upon being hit.
    /// </summary>
    public ref float AntishadowOnslaught_HeadOffset => ref NPC.ai[3];

    /// <summary>
    /// How long it takes for the melee weapons to be summoned and telegraph during the antishadow onslaught attack.
    /// </summary>
    public static int AntishadowOnslaught_WeaponSummonDelay => GetAIInt("AntishadowOnslaught_WeaponSummonDelay");

    /// <summary>
    /// How long the Avatar spends summoning melee weapons during the antishadow onslaught attack.
    /// </summary>
    public static int AntishadowOnslaught_WeaponSummonTime => GetAIInt("AntishadowOnslaught_WeaponSummonTime");

    /// <summary>
    /// The amount of damage the Avatar's weapons summoned during the antishadow onslaught attack do.
    /// </summary>
    public static int AntishadowWeaponDamage => GetAIInt("AntishadowWeaponDamage");

    /// <summary>
    /// The amount of damage the Avatar's blood torrent used during the antishadow onslaught attack does.
    /// </summary>
    public static int BloodTorrentDamage => GetAIInt("BloodTorrentDamage");

    /// <summary>
    /// The summon rate of melee weapons during the antishadow onslaught attack.
    /// </summary>
    public static int AntishadowOnslaught_StakeSummonRate => GetAIInt("AntishadowOnslaught_StakeSummonRate");

    /// <summary>
    /// The fall speed of melee weapons during the antishadow onslaught attack.
    /// </summary>
    public static float AntishadowOnslaught_StakeFallSpeed => GetAIFloat("AntishadowOnslaught_StakeFallSpeed");

    /// <summary>
    /// The Avatar's chase speed interpolant during the antishadow onslaught attack.
    /// </summary>
    public static float AntishadowOnslaught_ChaseSpeedInterpolant => GetAIFloat("AntishadowOnslaught_ChaseSpeedInterpolant");

    /// <summary>
    /// The background color during the Avatar's antishadow onslaught attack.
    /// </summary>
    public static Color AntishadowBackgroundColor => new Color(179, 0, 36);

    [AutomatedMethodInvoke]
    public void LoadState_AntishadowOnslaught()
    {
        StateMachine.RegisterTransition(AvatarAIType.AntishadowOnslaught, null, false, () =>
        {
            return AITimer >= AntishadowOnslaught_WeaponSummonDelay + AntishadowOnslaught_WeaponSummonTime;
        }, IProjOwnedByBoss<AvatarOfEmptiness>.KillAll);

        // Load the AI state behavior.
        StateMachine.RegisterStateBehavior(AvatarAIType.AntishadowOnslaught, DoBehavior_AntishadowOnslaught);

        StatesToNotStartTeleportDuring.Add(AvatarAIType.AntishadowOnslaught);
        AttackDimensionRelationship[AvatarAIType.AntishadowOnslaught] = AvatarDimensionVariants.AntishadowDimension;
    }

    public void DoBehavior_AntishadowOnslaught()
    {
        AmbientSoundVolumeFactor = Utils.Remap(AITimer, 0f, 60f, 1f, 0.4f);

        // For visual clarity, disable Solyn's attacking behavior.
        SolynAction = s => StandardSolynBehavior_FlyNearPlayer(s, NPC);

        // Shatter the screen on the first frame to reveal the background.
        if (AITimer <= 1)
        {
            TotalScreenOverlaySystem.OverlayColor = AntishadowBackgroundColor;
            TotalScreenOverlaySystem.OverlayInterpolant = 1.2f;

            // Kill leftover projectiles.
            IProjOwnedByBoss<AvatarOfEmptiness>.KillAll();
            ScreenShakeSystem.StartShake(9f);
        }

        if (AITimer <= AntishadowOnslaught_WeaponSummonDelay / 2)
        {
            NPC.Center = Target.Center - Vector2.UnitY * 330f;
            NPC.velocity = Vector2.Zero;
            NPC.netUpdate = true;
        }

        if (AITimer == AntishadowOnslaught_WeaponSummonDelay - SecondsToFrames(0.85f))
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
                NewProjectileBetter(NPC.GetSource_FromAI(), NPC.Center, Vector2.UnitY, ModContent.ProjectileType<BloodTorrent>(), BloodTorrentDamage, 0f);
        }

        // Disable distortion.
        IdealDistortionIntensity = 0f;

        // Disable visual name hover effects.
        NPC.ShowNameOnHover = false;

        // Use the antishadow background.
        if (AITimer >= 2)
            DrawnAsSilhouette = true;

        // Stay in the background.
        ZPosition = Lerp(0.3f, 1.1f, Sqrt(InverseLerp(0f, 25f, AITimer)));

        if (AITimer <= AntishadowOnslaught_WeaponSummonDelay)
            DoBehavior_AntishadowOnslaught_GetHitByWeapons();
        else if (AITimer <= AntishadowOnslaught_WeaponSummonTime)
        {
            float moveSpeedInterpolant = InverseLerp(0f, 45f, AITimer - AntishadowOnslaught_WeaponSummonDelay);
            Vector2 hoverDestination = Target.Center - Vector2.UnitY * 420f;
            NPC.SmoothFlyNear(hoverDestination, moveSpeedInterpolant * AntishadowOnslaught_ChaseSpeedInterpolant, 0.92f);

            float moveAbovePlayerInterpolant = InverseLerp(400f, 150f, Target.Center.Y - NPC.Center.Y);
            NPC.Center = Vector2.Lerp(NPC.Center, new(NPC.Center.X, hoverDestination.Y), moveAbovePlayerInterpolant * 0.3f);

            if (AITimer % AntishadowOnslaught_StakeSummonRate == 0)
            {
                Vector2 fallDirection = new Vector2(-NPC.HorizontalDirectionTo(Target.Center), 3.19f).SafeNormalize(Vector2.UnitY);
                Vector2 weaponSpawnPosition = Target.Center + new Vector2(Main.rand.NextFloatDirection() * 1600f + Target.Velocity.X * 56f, -1050f);
                Vector2 weaponVelocity = fallDirection.RotatedByRandom(0.18f) * AntishadowOnslaught_StakeFallSpeed;
                NewProjectileBetter(NPC.GetSource_FromAI(), weaponSpawnPosition, weaponVelocity, ModContent.ProjectileType<FallingMeleeWeapon>(), AntishadowWeaponDamage, 0f);
            }
        }
        else
            NPC.velocity = Vector2.Lerp(NPC.velocity, NPC.SafeDirectionTo(Target.Center - Vector2.UnitY * 400f) * 8.1f, 0.03f);

        // Since pixels are completely stripped of nuance due to the antishadow screen shader, (light) pets can significantly limit the player's visibility of themselves by casting
        // large black shadows on top of them. To mitigate this, kill all pets.
        if (AITimer >= 2)
        {
            foreach (Projectile p in Main.ActiveProjectiles)
            {
                bool isPet = ProjectileID.Sets.LightPet[p.type] || Main.projPet[p.type];
                if (isPet && p.active)
                    p.active = false;
            }
        }

        // Disable tile interactions.
        TileDisablingSystem.TilesAreUninteractable = AITimer >= 2;

        // Roar after attacking.
        int screenSlamTimer = AITimer - AntishadowOnslaught_WeaponSummonDelay - AntishadowOnslaught_WeaponSummonTime;
        if (screenSlamTimer == 1)
        {
            ScreenShakeSystem.StartShake(9.3f);
            SoundEngine.PlaySound(GennedAssets.Sounds.Avatar.Angry);
        }

        // Update limbs.
        AntishadowOnslaught_LeftArmOffset *= 0.86f;
        AntishadowOnslaught_RightArmOffset *= 0.86f;
        AntishadowOnslaught_HeadOffset *= 0.82f;
        float armOffsetAngle = PiOver4;
        Vector2 leftArmDestination = NPC.Center + armOffsetAngle.ToRotationVector2() * new Vector2(-667f, 400f);
        Vector2 rightArmDestination = NPC.Center + armOffsetAngle.ToRotationVector2() * new Vector2(667f, 400f);
        leftArmDestination += new Vector2(-1f, 2f) * AntishadowOnslaught_LeftArmOffset;
        rightArmDestination += new Vector2(1f, 2f) * AntishadowOnslaught_RightArmOffset;

        LeftArmPosition = Vector2.Lerp(LeftArmPosition, leftArmDestination, 0.19f);
        RightArmPosition = Vector2.Lerp(RightArmPosition, rightArmDestination, 0.19f);

        // Update the head.
        float verticalOffset = Cos01(TwoPi * FightTimer / 90f) * 12f + AntishadowOnslaught_HeadOffset + 415f;
        HeadPosition = Vector2.Lerp(HeadPosition, NPC.Center + new Vector2(3f, verticalOffset) * HeadScale * NeckAppearInterpolant, 0.9f);

        if (Main.netMode != NetmodeID.Server)
        {
            ManagedScreenFilter antishadowShader = ShaderManager.GetFilter("NoxusBoss.AntishadowSilhouetteShader");
            antishadowShader.TrySetParameter("silhouetteColor", Color.Black);
            antishadowShader.TrySetParameter("foregroundColor", AntishadowBackgroundColor);
            antishadowShader.Activate();
        }
    }

    public void DoBehavior_AntishadowOnslaught_GetHitByWeapons()
    {
        if (AITimer % 9 == 8 && AITimer >= 45 && AITimer < AntishadowOnslaught_WeaponSummonDelay - SecondsToFrames(0.36f))
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                Vector2 weaponSpawnPosition = NPC.Center - Vector2.UnitY.RotatedByRandom(1.07f) * 240f;
                Vector2 weaponVelocity = weaponSpawnPosition.SafeDirectionTo(NPC.Center) * 70f;
                NewProjectileBetter(NPC.GetSource_FromAI(), weaponSpawnPosition, weaponVelocity, ModContent.ProjectileType<FallingMeleeWeapon>(), 0, 0f, -1, (int)FallingMeleeWeapon.AvatarStickState.WaitingForCollision);
            }
        }

        NPC.velocity *= 0.2f;
    }
}
