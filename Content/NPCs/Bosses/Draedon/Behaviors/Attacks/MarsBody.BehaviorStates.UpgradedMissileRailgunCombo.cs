using Luminance.Common.DataStructures;
using Luminance.Common.StateMachines;
using Microsoft.Xna.Framework;
using NoxusBoss.Assets;
using NoxusBoss.Content.NPCs.Bosses.Draedon.Projectiles;
using NoxusBoss.Content.NPCs.Bosses.Draedon.Projectiles.SolynProjectiles;
using NoxusBoss.Content.NPCs.Friendly;
using NoxusBoss.Core.Graphics.SpecificEffectManagers;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.Draedon;

public partial class MarsBody
{
    /// <summary>
    /// The rate at which Mars releases missiles during missile railgun combo attack.
    /// </summary>
    public static int UpgradedMissileRailgunCombo_MissileShootRate => GetAIInt("UpgradedMissileRailgunCombo_MissileShootRate");

    /// <summary>
    /// The amount of time Mars spends waiting before telegraphing his railgun during missile railgun combo attack.
    /// </summary>
    public static int UpgradedMissileRailgunCombo_TelegraphDelay => GetAIInt("UpgradedMissileRailgunCombo_TelegraphDelay");

    /// <summary>
    /// The amount of time Mars spends telegraphing his railgun during missile railgun combo attack.
    /// </summary>
    public static int UpgradedMissileRailgunCombo_TelegraphTime => GetAIInt("UpgradedMissileRailgunCombo_TelegraphTime");

    /// <summary>
    /// The amount of time Mars waiting before shooting his railgun during missile railgun combo attack.
    /// </summary>
    public static int UpgradedMissileRailgunCombo_ShootDelay => GetAIInt("UpgradedMissileRailgunCombo_ShootDelay");

    /// <summary>
    /// The amount of time Mars shooting his railgun during missile railgun combo attack.
    /// </summary>
    public static int UpgradedMissileRailgunCombo_ShootTime => GetAIInt("UpgradedMissileRailgunCombo_ShootTime");

    /// <summary>
    /// The amount of time Mars spends not firing missiles in a cycle during missile railgun combo attack.
    /// </summary>
    public static int UpgradedMissileRailgunCombo_MissileDownTime => GetAIInt("UpgradedMissileRailgunCombo_MissileDownTime");

    /// <summary>
    /// The amount of time Mars spends firing missiles in a cycle during missile railgun combo attack.
    /// </summary>
    public static int UpgradedMissileRailgunCombo_MissileFireTime => GetAIInt("UpgradedMissileRailgunCombo_MissileFireTime");

    /// <summary>
    /// How long Mars' missile railgun combo attack goes on for.
    /// </summary>
    public static int UpgradedMissileRailgunCombo_AttackDuration => GetAIInt("UpgradedMissileRailgunCombo_AttackDuration");

    /// <summary>
    /// The rate at which Mars releases missiles during missile railgun combo attack.
    /// </summary>
    public static float UpgradedMissileRailgunCombo_TelegraphAimSpeed => GetAIFloat("UpgradedMissileRailgunCombo_TelegraphAimSpeed");

    [AutomatedMethodInvoke]
    public void LoadState_UpgradedMissileRailgunCombo()
    {
        StateMachine.RegisterTransition(MarsAIType.UpgradedMissileRailgunCombo, null, false, () =>
        {
            return AITimer >= UpgradedMissileRailgunCombo_AttackDuration && !AnyProjectiles(ModContent.ProjectileType<RailGunCannonDeathray>());
        }, LoadState_UpgradedMissileRailgunCombo_EndEffects);
        StateMachine.RegisterStateBehavior(MarsAIType.UpgradedMissileRailgunCombo, DoBehavior_UpgradedMissileRailgunCombo);
    }

    /// <summary>
    /// Performs Mars' missile railgun combo attack combo attack.
    /// </summary>
    public void DoBehavior_UpgradedMissileRailgunCombo()
    {
        SolynAction = DoBehavior_UpgradedMissileRailgunCombo_Solyn;
        ProvideForcefieldsToPlayers();

        // Circle around the player.
        // This is super important in ensuring that Mars' positioning (and consequently, the missile positioning), is dynamic.
        float flySpeedInterpolant = InverseLerp(0f, 45f, AITimer).Squared() * 0.3f;
        Vector2 hoverOffset = -Vector2.UnitY.RotatedBy(TwoPi * AITimer / 500f) * new Vector2(600f, 370f);
        NPC.Center = Vector2.Lerp(NPC.Center, Target.Center + hoverOffset, 0.023f);
        NPC.SmoothFlyNearWithSlowdownRadius(Target.Center + hoverOffset, flySpeedInterpolant * 0.1f, 1f - flySpeedInterpolant * 0.055f, 50f);
        NPC.rotation = NPC.rotation.AngleLerp(NPC.velocity.X * 0.01f, 0.1f);

        Vector2 leftArmHoverOffset = NPC.SafeDirectionTo(Target.Center) * new Vector2(550f, 140f);
        if (leftArmHoverOffset.X > 0f)
            leftArmHoverOffset.X *= 0.16f;

        MoveArmsTowards(new Vector2(-370f, 216f) + leftArmHoverOffset, new(570f, 216f), 0.14f, 0.6f);

        int telegraphDelay = UpgradedMissileRailgunCombo_TelegraphDelay;
        int telegraphTime = UpgradedMissileRailgunCombo_TelegraphTime;
        int shootDelay = UpgradedMissileRailgunCombo_ShootDelay;
        int shootTime = UpgradedMissileRailgunCombo_ShootTime;
        int wrappedTimer = AITimer % (telegraphDelay + telegraphTime + shootDelay + shootTime);
        float railGunAimSpeed = InverseLerp(shootDelay, 0f, wrappedTimer - telegraphDelay - telegraphTime) * UpgradedMissileRailgunCombo_TelegraphAimSpeed;

        // Aim the telegraph sightline.
        if (wrappedTimer >= telegraphDelay && wrappedTimer <= telegraphDelay + telegraphTime)
            RailgunCannonTelegraphOpacity = InverseLerpBump(0f, 9f, telegraphTime - 9f, telegraphTime, wrappedTimer - telegraphDelay) * Cos01(AITimer / 3.5f);

        // Play a sound as the telegraph sightline appears.
        if (wrappedTimer == telegraphDelay)
            SoundEngine.PlaySound(GennedAssets.Sounds.Mars.RailgunChargeup).WithVolumeBoost(0.75f);

        // Fire the beam.
        if (wrappedTimer == telegraphDelay + telegraphTime + shootDelay)
        {
            SoundEngine.PlaySound(GennedAssets.Sounds.Mars.RailgunFire).WithVolumeBoost(1.15f);

            RadialScreenShoveSystem.Start(LeftHandPosition, 20);
            if (Main.netMode != NetmodeID.MultiplayerClient)
                NewProjectileBetter(NPC.GetSource_FromAI(), LeftHandPosition, RailgunCannonAngle.ToRotationVector2(), ModContent.ProjectileType<RailGunCannonDeathray>(), RailgunBlastDamage, 0f, NPC.target, NPC.whoAmI);
        }

        // Fire missiles.
        int downTime = UpgradedMissileRailgunCombo_MissileDownTime;
        int fireTime = UpgradedMissileRailgunCombo_MissileFireTime;
        bool shootMissiles = AITimer % (downTime + fireTime) >= downTime;
        if (Main.netMode != NetmodeID.MultiplayerClient && AITimer >= 60 && AITimer % UpgradedMissileRailgunCombo_MissileShootRate == 0)
            NewProjectileBetter(NPC.GetSource_FromAI(), NPC.Center, NPC.velocity.SafeDirectionTo(Target.Center).RotatedByRandom(PiOver2) * -23f, ModContent.ProjectileType<MarsMissile>(), FortifiedMissileDamage, 0f, NPC.target);

        float idealRailGunAngle = LeftHandPosition.AngleTo(Target.Center);
        RailgunCannonAngle = RailgunCannonAngle.AngleLerp(idealRailGunAngle, railGunAimSpeed);
    }

    /// <summary>
    /// Instructs Solyn to stay near the player for the missile railgun combo attack.
    /// </summary>
    public void DoBehavior_UpgradedMissileRailgunCombo_Solyn(BattleSolyn solyn)
    {
        int forcefieldID = ModContent.ProjectileType<DirectionalSolynForcefield>();
        float forcefieldDirection = 0f;
        foreach (Projectile projectile in Main.ActiveProjectiles)
        {
            if (projectile.owner == NPC.target && projectile.type == forcefieldID)
            {
                forcefieldDirection = WrapAngle360(projectile.velocity.ToRotation());
                break;
            }
        }

        NPC solynNPC = solyn.NPC;
        Vector2 lookDestination = Target.Center;

        float angleSnapValue = PiOver2;
        float snappedForcefieldDirection = Round(forcefieldDirection * angleSnapValue) / angleSnapValue;
        Vector2 hoverOffset = snappedForcefieldDirection.ToRotationVector2() * -56f + Vector2.UnitX * 4f;
        Vector2 hoverDestination = Target.Center + hoverOffset;

        solynNPC.SmoothFlyNear(hoverDestination, 0.2f, 0.8f);

        solyn.UseStarFlyEffects();

        solynNPC.spriteDirection = (int)solynNPC.HorizontalDirectionTo(lookDestination);
        solynNPC.rotation = solynNPC.rotation.AngleLerp(0f, 0.3f);
        HandleSolynPlayerTeamAttack(solyn);
    }

    /// <summary>
    /// Handles general-purpose end-of-attack effects for Mars' upgraded missile railgun combo attack.
    /// </summary>
    public static void LoadState_UpgradedMissileRailgunCombo_EndEffects()
    {
        int forcefieldID = ModContent.ProjectileType<DirectionalSolynForcefield>();
        foreach (Projectile projectile in Main.ActiveProjectiles)
        {
            if (projectile.type == forcefieldID)
                projectile.As<DirectionalSolynForcefield>().BeginDisappearing();
        }

        IProjOwnedByBoss<MarsBody>.KillAll();
    }
}
