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
    /// The rate at which Mars releases missiles during his first phase.
    /// </summary>
    public static int FirstPhaseMissileRailgunCombo_MissileShootRate => GetAIInt("FirstPhaseMissileRailgunCombo_MissileShootRate");

    /// <summary>
    /// The amount of time Mars spends waiting before telegraphing his railgun during his first phase.
    /// </summary>
    public static int FirstPhaseMissileRailgunCombo_TelegraphDelay => GetAIInt("FirstPhaseMissileRailgunCombo_TelegraphDelay");

    /// <summary>
    /// The amount of time Mars spends telegraphing his railgun during his first phase.
    /// </summary>
    public static int FirstPhaseMissileRailgunCombo_TelegraphTime => GetAIInt("FirstPhaseMissileRailgunCombo_TelegraphTime");

    /// <summary>
    /// The amount of time Mars waiting before shooting his railgun during his first phase.
    /// </summary>
    public static int FirstPhaseMissileRailgunCombo_ShootDelay => GetAIInt("FirstPhaseMissileRailgunCombo_ShootDelay");

    /// <summary>
    /// The amount of time Mars shooting his railgun during his first phase.
    /// </summary>
    public static int FirstPhaseMissileRailgunCombo_ShootTime => GetAIInt("FirstPhaseMissileRailgunCombo_ShootTime");

    /// <summary>
    /// The amount of time Mars spends not firing missiles in a cycle during his first phase.
    /// </summary>
    public static int FirstPhaseMissileRailgunCombo_MissileDownTime => GetAIInt("FirstPhaseMissileRailgunCombo_MissileDownTime");

    /// <summary>
    /// The amount of time Mars spends firing missiles in a cycle during his first phase.
    /// </summary>
    public static int FirstPhaseMissileRailgunCombo_MissileFireTime => GetAIInt("FirstPhaseMissileRailgunCombo_MissileFireTime");

    /// <summary>
    /// The amount of damage fortified missiles from Mars do.
    /// </summary>
    public static int FortifiedMissileDamage => GetAIInt("FortifiedMissileDamage");

    /// <summary>
    /// The amount of damage blasts from Mars' railgun do.
    /// </summary>
    public static int RailgunBlastDamage => GetAIInt("RailgunBlastDamage");

    /// <summary>
    /// The rate at which Mars releases missiles during his first phase.
    /// </summary>
    public static float FirstPhaseMissileRailgunCombo_TelegraphAimSpeed => GetAIFloat("FirstPhaseMissileRailgunCombo_TelegraphAimSpeed");

    [AutomatedMethodInvoke]
    public void LoadState_FirstPhaseMissileRailgunCombo()
    {
        StateMachine.RegisterTransition(MarsAIType.FirstPhaseMissileRailgunCombo, null, false, () =>
        {
            return NPC.life <= NPC.lifeMax * Phase2LifeRatio && !AnyProjectiles(ModContent.ProjectileType<RailGunCannonDeathray>());
        }, LoadState_FirstPhaseMissileRailgunCombo_EndEffects);
        StateMachine.RegisterStateBehavior(MarsAIType.FirstPhaseMissileRailgunCombo, DoBehavior_FirstPhaseMissileRailgunCombo);
    }

    /// <summary>
    /// Performs Mars' first phase combo attack.
    /// </summary>
    public void DoBehavior_FirstPhaseMissileRailgunCombo()
    {
        SolynAction = DoBehavior_FirstPhaseMissileRailgunCombo_Solyn;
        ProvideForcefieldsToPlayers();

        // Circle around the player.
        // This is super important in ensuring that Mars' positioning (and consequently, the missile positioning), is dynamic.
        float flySpeedInterpolant = InverseLerp(0f, 60f, AITimer).Squared() * 0.3f;
        Vector2 hoverOffset = -Vector2.UnitY.RotatedBy(TwoPi * AITimer / 600f) * new Vector2(600f, 370f);
        NPC.SmoothFlyNearWithSlowdownRadius(Target.Center + hoverOffset, flySpeedInterpolant * 0.1f, 1f - flySpeedInterpolant * 0.055f, 50f);
        NPC.rotation = NPC.rotation.AngleLerp(NPC.velocity.X * 0.01f, 0.1f);

        Vector2 leftArmOffset = new Vector2(-240f, 216f).RotatedBy(NPC.AngleTo(Target.Center) - PiOver2);
        leftArmOffset.X = Lerp(leftArmOffset.X, -240f, 0.5f);
        leftArmOffset.Y *= Lerp(1f, 0.45f, InverseLerp(0f, 100f, leftArmOffset.Y));

        MoveArmsTowards(leftArmOffset, new Vector2(170f, 216f), 0.14f, 0.6f);

        int telegraphDelay = FirstPhaseMissileRailgunCombo_TelegraphDelay;
        int telegraphTime = FirstPhaseMissileRailgunCombo_TelegraphTime;
        int shootDelay = FirstPhaseMissileRailgunCombo_ShootDelay;
        int shootTime = FirstPhaseMissileRailgunCombo_ShootTime;
        int wrappedTimer = AITimer % (telegraphDelay + telegraphTime + shootDelay + shootTime);
        float railGunAimSpeed = InverseLerp(shootDelay, 0f, wrappedTimer - telegraphDelay - telegraphTime) * FirstPhaseMissileRailgunCombo_TelegraphAimSpeed;

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
            {
                NewProjectileBetter(NPC.GetSource_FromAI(), LeftHandPosition, RailgunCannonAngle.ToRotationVector2(), ModContent.ProjectileType<RailGunCannonDeathray>(), RailgunBlastDamage, 0f, -1, NPC.whoAmI);
                LeftHandVelocity -= RailgunCannonAngle.ToRotationVector2() * 60f;
                NPC.netUpdate = true;
            }
        }

        // Fire missiles.
        int downTime = FirstPhaseMissileRailgunCombo_MissileDownTime;
        int fireTime = FirstPhaseMissileRailgunCombo_MissileFireTime;
        bool shootMissiles = AITimer % (downTime + fireTime) >= downTime;
        if (Main.netMode != NetmodeID.MultiplayerClient && AITimer >= 60 && AITimer % FirstPhaseMissileRailgunCombo_MissileShootRate == 0 && shootMissiles)
            NewProjectileBetter(NPC.GetSource_FromAI(), NPC.Center, NPC.velocity.SafeDirectionTo(Target.Center).RotatedByRandom(PiOver2) * -20f, ModContent.ProjectileType<MarsMissile>(), FortifiedMissileDamage, 0f, -1);

        float idealRailGunAngle = LeftHandPosition.AngleTo(Target.Center);
        RailgunCannonAngle = RailgunCannonAngle.AngleLerp(idealRailGunAngle, railGunAimSpeed);
        EnergyCannonAngle = EnergyCannonAngle.AngleLerp(rightElbowPosition.AngleTo(RightHandPosition), 0.03f);
    }

    /// <summary>
    /// Instructs Solyn to stay near the player for the first phase combo attack.
    /// </summary>
    public void DoBehavior_FirstPhaseMissileRailgunCombo_Solyn(BattleSolyn solyn)
    {
        int forcefieldID = ModContent.ProjectileType<DirectionalSolynForcefield>();
        float forcefieldDirection = 0f;
        foreach (Projectile projectile in Main.ActiveProjectiles)
        {
            if (projectile.owner == solyn.Player.whoAmI && projectile.type == forcefieldID)
            {
                forcefieldDirection = WrapAngle360(projectile.velocity.ToRotation());
                break;
            }
        }

        NPC solynNPC = solyn.NPC;
        Vector2 lookDestination = solyn.Player.Center;

        float angleSnapValue = PiOver2;
        float snappedForcefieldDirection = Round(forcefieldDirection * angleSnapValue) / angleSnapValue;
        Vector2 hoverOffset = snappedForcefieldDirection.ToRotationVector2() * -56f + Vector2.UnitX * 4f;
        Vector2 hoverDestination = solyn.Player.Center + hoverOffset;

        solynNPC.SmoothFlyNear(hoverDestination, 0.2f, 0.8f);

        solyn.UseStarFlyEffects();

        solynNPC.spriteDirection = (int)solynNPC.HorizontalDirectionTo(lookDestination);
        solynNPC.rotation = solynNPC.rotation.AngleLerp(0f, 0.3f);
        HandleSolynPlayerTeamAttack(solyn);
    }

    /// <summary>
    /// Attempts to calculate the end position of a line, accounting for intersections with directional forcefields.
    /// </summary>
    /// <param name="start">The starting point of the line.</param>
    /// <param name="end">The ending point of the line, assuming no intersection obstruction.</param>
    public static Vector2 AttemptForcefieldIntersection(Vector2 start, Vector2 end, float graceFactor = 1f)
    {
        Vector2 direction = (end - start).SafeNormalize(Vector2.Zero);

        int forcefieldID = ModContent.ProjectileType<DirectionalSolynForcefield>();
        float minDistance = 9999f;
        foreach (Projectile forcefield in Main.ActiveProjectiles)
        {
            if (forcefield.type == forcefieldID && forcefield.Opacity >= 0.7f)
            {
                LineEllipseIntersectionCheck(start, direction, forcefield.Center, forcefield.Size * forcefield.scale * new Vector2(0.75f, 0.1f) * graceFactor * 0.5f, forcefield.rotation, out Vector2 a, out Vector2 b);

                float aDistanceFromStart = start.Distance(a);
                float bDistanceFromStart = start.Distance(b);
                Vector2 candidate = aDistanceFromStart < bDistanceFromStart ? a : b;
                float candidateDistance = MathF.Min(aDistanceFromStart, bDistanceFromStart);

                if (candidateDistance < minDistance)
                {
                    end = candidate;
                    minDistance = candidateDistance;
                }
            }
        }

        return end;
    }

    /// <summary>
    /// Calculates the intersection points between a ellipse and a line.
    /// </summary>
    /// <param name="start">The line's pivot point.</param>
    /// <param name="direction">The line's direction.</param>
    /// <param name="ellipseCenter">The center position of the ellipse.</param>
    /// <param name="ellipseSize">The size of the ellipse.</param>
    /// <param name="ellipseRotation">The rotation of the ellipse.</param>
    /// <param name="solutionA">The first resulting solution.</param>
    /// <param name="solutionB">The second resulting solution.</param>
    public static void LineEllipseIntersectionCheck(Vector2 start, Vector2 direction, Vector2 ellipseCenter, Vector2 ellipseSize, float ellipseRotation, out Vector2 solutionA, out Vector2 solutionB)
    {
        // Taken by solving solutions from the following two equations:
        // y - v = m * (x - u)
        // (x / w)^2 + (y / h)^2 = 1

        // Rearranging terms in the linear equation results in the following definition for y:
        // y = m * (x - u) + v

        // In order to solve for x, it's simply a matter of plugging in this equation in for y in the ellipse equation, like so:
        // (x / w)^2 + ((m * (x - u) + v) / h)^2 = 1

        // And now, for solving...
        // Just go to some online website to get the result. I'm not writing out all the diabolical algebra steps in a code comment.
        // https://www.symbolab.com/solver/step-by-step/%5Cleft(%5Cfrac%7Bx%7D%7Bw%7D%5Cright)%5E%7B2%7D%2B%5Cleft(%5Cfrac%7Bm%5Cleft(x-u%5Cright)%2Bv%7D%7Bh%7D%5Cright)%5E%7B2%7D%3D1?or=input

        // Rotating the actual ellipse in the above equation makes everything kind of brain-melting so instead just do a little bit of a relativistic magic and
        // do a reverse-rotation on the line for the same effect in practice.
        start = start.RotatedBy(-ellipseRotation, ellipseCenter);
        direction = direction.RotatedBy(-ellipseRotation);

        float m = direction.Y / direction.X;
        float u = start.X - ellipseCenter.X;
        float v = start.Y - ellipseCenter.Y;
        float w = ellipseSize.X;
        float h = ellipseSize.Y;

        float numeratorFirstHalf = -w * (m.Squared() * u * -2f + m * v * 2f);
        float numeratorSecondHalf = Sqrt(-m.Squared() * u.Squared() + m * u * v * 2f + m.Squared() * w.Squared() + h.Squared() - v.Squared()) * h * 2f;
        float denominator = (m.Squared() * w.Squared() + h.Squared()) * 2f;

        float xSolutionA = (numeratorFirstHalf - numeratorSecondHalf) * w / denominator;
        float xSolutionB = (numeratorFirstHalf + numeratorSecondHalf) * w / denominator;

        // Now that the two solution X values are known, it's simply a matter of plugging X back into the linear equation to get Y.
        float ySolutionA = m * (xSolutionA - u) + v;
        float ySolutionB = m * (xSolutionB - u) + v;

        solutionA = new Vector2(xSolutionA, ySolutionA).RotatedBy(ellipseRotation) + ellipseCenter;
        solutionB = new Vector2(xSolutionB, ySolutionB).RotatedBy(ellipseRotation) + ellipseCenter;
    }

    /// <summary>
    /// Handles general-purpose end-of-attack effects for Mars' first phase.
    /// </summary>
    public static void LoadState_FirstPhaseMissileRailgunCombo_EndEffects()
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
