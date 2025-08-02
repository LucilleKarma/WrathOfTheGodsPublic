using Luminance.Common.Easings;
using Luminance.Common.StateMachines;
using Microsoft.Xna.Framework;
using NoxusBoss.Content.NPCs.Bosses.Avatar.Projectiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm;

public partial class AvatarOfEmptiness
{
    /// <summary>
    /// The direction that planetoids summoned by the Avatar are flying away in during the world smash attack.
    /// </summary>
    public ref float WorldSmash_PlanetoidFlyOffDirection => ref NPC.ai[2];

    /// <summary>
    /// The amount of times the Avatar has summoned planetoids.
    /// </summary>
    public ref float WorldSmash_PlanetoidSummonCounter => ref NPC.ai[3];

    /// <summary>
    /// The amount of time the Avatar spends entering the background during his world smash attack.
    /// </summary>
    public static int WorldSmash_EnterBackgroundTime => GetAIInt("WorldSmash_EnterBackgroundTime");

    /// <summary>
    /// The amount of time the Avatar spends bringing planetoids down during his world smash attack.
    /// </summary>
    public static int WorldSmash_PlanetoidDescentTime => GetAIInt("WorldSmash_PlanetoidDescentTime");

    /// <summary>
    /// The amount of time the Avatar spends sending planetoids away during his world smash attack.
    /// </summary>
    public static int WorldSmash_FlyOffTime => GetAIInt("WorldSmash_FlyOffTime");

    /// <summary>
    /// The amount of time the Avatar should summon planetoids during his world smash attack.
    /// </summary>
    public static int WorldSmash_PlanetoidSmashCount => GetAIInt("WorldSmash_PlanetoidSmashCount");

    /// <summary>
    /// How long planetoids that are close to each other should spend slowing down prior to exploding.
    /// </summary>
    public static int WorldSmash_SlowDownTime => GetAIInt("WorldSmash_SlowDownTime");

    /// <summary>
    /// How long the Avatar waits before transitioning to the next attack after the world smash attack.
    /// </summary>
    public static int WorldSmash_AttackTransitionDelay => GetAIInt("WorldSmash_AttackTransitionDelay");

    /// <summary>
    /// The amount of damage molten blobs from planetoids exploded by the Avatar do.
    /// </summary>
    public static int MoltenBlobDamage => GetAIInt("MoltenBlobDamage");

    /// <summary>
    /// The amount of damage planetoids flung by the Avatar do.
    /// </summary>
    public static int StolenPlanetoidDamage => GetAIInt("StolenPlanetoidDamage");

    /// <summary>
    /// The base speed at which planetoids are flung at during the Avatar's world smash attack.
    /// </summary>
    public float WorldSmash_PlanetoidFlingSpeed => WorldSmash_PlanetoidSummonCounter * 3.1f + 24.1f;

    /// <summary>
    /// The distance at which planetoids are considered close enough that they should slow down prior to impact.
    /// </summary>
    public static float WorldSmash_SlowDownDistance => GetAIFloat("WorldSmash_SlowDownDistance");

    /// <summary>
    /// The distance at which planetoids are considered close enough to explode.
    /// </summary>
    public static float WorldSmash_ExplodeDistance => GetAIFloat("WorldSmash_ExplodeDistance");

    /// <summary>
    /// Searches for an approximate for a root of a given function.
    /// </summary>
    /// <param name="fx">The function to find the root for.</param>
    /// <param name="initialGuess">The initial guess for what the root could be.</param>
    /// <param name="iterations">The amount of iterations to perform. The higher this is, the more generally accurate the result will be.</param>
    public static double IterativelySearchForRoot(Func<double, double> fx, double initialGuess, int iterations)
    {
        // This uses the Newton-Raphson method to iteratively get closer and closer to roots of a given function.
        // The exactly formula is as follows:
        // x = x - f(x) / f'(x)
        // In most circumstances repeating the above equation will result in closer and closer approximations to a root.
        // The exact reason as to why this intuitively works can be found at the following video:
        // https://www.youtube.com/watch?v=-RdOwhmqP5s
        double result = initialGuess;
        for (int i = 0; i < iterations; i++)
        {
            double derivative = fx.ApproximateDerivative(result);
            result -= fx(result) / derivative;
        }

        return result;
    }

    /// <summary>
    /// Calculates the exponential decay speed factor that planetoids should adhere to when slowing down.
    /// </summary>
    /// <param name="startingSpeed">The speed at which the planetoid starts flying at, before acceleration begins.</param>
    public static float CalculatePlanetoidSlowdownDeceleration(float startingSpeed)
    {
        /* Fundamentally, this seeks to answer a basic question:
         * "I have an initial speed of v0. In the span of 't' frames I want to move 's' pixels. What exponential decay factor is necessary to achieve this?'
         * 
         * Written mathematically, this idea can be expressed in the following way:
         * sum(n over 0 to t) (v0 * x^n) = s
         * 
         * The reason a sum is used is because distance represents the sum of speed over a period of time.
         * Before working on solving this, it's important to recognize that this concept is close to that of a similar object that has the same "sum over an area" type behavior:
         * Definite integrals.
         * 
         * Though definite integrals work in a continuous rather than discrete context, they are still a useful framing for the problem, and after working the math out the margin of error
         * is sufficiently low that I don't believe it needs to be dealt with.
         * With that out of the way, let's rewrite the above equation in terms of an integral and solve it:
         * 
         * For context, 'a' will be the variable of integration.
         * 
         * ∫(0, t) v0 * x^a * da = s                                                    (Set up equation as integral)
         * t * ∫(0, 1) (v0 * x^(a * t) * da) = s                                        (Rewrite integral to be in 0-1 bounds)
         * t * (v0 * x ^ (1 * t) / (t * ln(x)) - v0 * x ^ (0 * t) / (t * ln(x))) = s    (Expand integral via an evaluation at upper and lower bounds)
         * t * ((v0 * x ^ t - v0) / (t * ln(x))) = s                                    (Simplify)
         * (v0 * x ^ t - v0) / (t * ln(x)) = s / t                                      (Move t from left to right side)
         * (v0 * x ^ t - v0) / (t * ln(x)) - s / t = 0                                  (Subtract s/t so that the result is rewritten in f(x) = 0 form. This is important for operations below)
         */
        double distanceFunction(double x)
        {
            int slowDownTime = WorldSmash_SlowDownTime;
            float distanceOffset = WorldSmash_SlowDownDistance - WorldSmash_ExplodeDistance;
            return startingSpeed * (Math.Pow(x, slowDownTime) - 1f) / (slowDownTime * Math.Log(x)) - distanceOffset / slowDownTime;
        }

        // Unfortunately, the distance function seemingly has no further useful reductions that allow for finding a value of x that satisfies the above mathematical condition with algebraic precision.
        // In order to address this, the Newton-Raphson method will be used as a means of finding a good approximate to the above equation.
        return (float)IterativelySearchForRoot(distanceFunction, 0.98, 5);
    }

    [AutomatedMethodInvoke]
    public void LoadState_WorldSmash()
    {
        StateMachine.RegisterTransition(AvatarAIType.WorldSmash, null, false, () =>
        {
            return WorldSmash_PlanetoidSummonCounter >= WorldSmash_PlanetoidSmashCount && AITimer >= WorldSmash_AttackTransitionDelay;
        }, () =>
        {
            WorldSmash_PlanetoidFlyOffDirection = 0f;
            WorldSmash_PlanetoidSummonCounter = 0f;
        });

        // Load the AI state behavior.
        StateMachine.RegisterStateBehavior(AvatarAIType.WorldSmash, DoBehavior_WorldSmash);

        StatesToNotStartTeleportDuring.Add(AvatarAIType.WorldSmash);
    }

    public void DoBehavior_WorldSmash()
    {
        LookAt(Target.Center);

        if (WorldSmash_PlanetoidSummonCounter >= WorldSmash_PlanetoidSmashCount)
        {
            PerformStandardLimbUpdates();
            return;
        }

        int enterBackgroundTime = WorldSmash_EnterBackgroundTime;
        bool spawnedPlanetoidsThisFrame = false;
        float planetStartingZPosition = 6.3f;

        // Calculate body part variables.
        float headVerticalOffset = 400f;
        Vector2 leftArmJutDirection = (-Vector2.UnitX).RotatedBy(-0.59f);
        Vector2 rightArmJutDirection = Vector2.UnitX.RotatedBy(0.59f);
        Vector2 leftArmDestination = NPC.Center + (leftArmJutDirection * 720f + Vector2.UnitY * Cos(TwoPi * FightTimer / 150f) * 20f) * NPC.scale * LeftFrontArmScale;
        Vector2 rightArmDestination = NPC.Center + (rightArmJutDirection * 720f + Vector2.UnitY * Cos(TwoPi * FightTimer / 150f + 1.91f) * 20f) * NPC.scale * RightFrontArmScale;

        // Disable the distortion.
        IdealDistortionIntensity = 0f;

        // Hover behind the player in the background.
        ZPosition = MathF.Max(ZPosition, Sqrt(InverseLerp(0f, enterBackgroundTime, AITimer)) * 2.8f);
        if (ZPosition > 2.8f)
            ZPosition = Lerp(ZPosition, 2.8f, 0.13f);

        NPC.SmoothFlyNear(Target.Center - Vector2.UnitY * 400f, 0.05f, 0.955f);

        // Summon planetoids.
        if (Main.netMode != NetmodeID.MultiplayerClient && AITimer == enterBackgroundTime)
        {
            Vector2 worldTopLeft = Vector2.One * 1500f;
            Vector2 worldBottomRight = new Vector2(Main.maxTilesX, Main.maxTilesY) * 16f - Vector2.One * 1500f;
            Vector2 leftPlanetoidSpawnPosition = Vector2.Clamp(Target.Center - Vector2.UnitX * 2000f, worldTopLeft, worldBottomRight);
            Vector2 rightPlanetoidSpawnPosition = Vector2.Clamp(Target.Center + Vector2.UnitX * 2000f, worldTopLeft, worldBottomRight);
            NewProjectileBetter(NPC.GetSource_FromAI(), leftPlanetoidSpawnPosition, Vector2.Zero, ModContent.ProjectileType<StolenPlanetoid>(), StolenPlanetoidDamage, 0f, -1, planetStartingZPosition);
            NewProjectileBetter(NPC.GetSource_FromAI(), rightPlanetoidSpawnPosition, Vector2.Zero, ModContent.ProjectileType<StolenPlanetoid>(), StolenPlanetoidDamage, 0f, -1, planetStartingZPosition);
            spawnedPlanetoidsThisFrame = true;
        }

        // Update planetoids.
        float flyOffInterpolant = InverseLerp(0f, WorldSmash_FlyOffTime, AITimer - enterBackgroundTime - WorldSmash_PlanetoidDescentTime);
        List<Projectile> planetoids = AllProjectilesByID(ModContent.ProjectileType<StolenPlanetoid>()).ToList();
        if (planetoids.Count >= 2 && flyOffInterpolant < 1f)
        {
            // Calculate the planetoids and their Z positions so that the Avatar can change them.
            Projectile leftPlanetoid = planetoids[0];
            Projectile rightPlanetoid = planetoids[1];
            if (leftPlanetoid.Center.X > rightPlanetoid.Center.X)
                Utils.Swap(ref leftPlanetoid, ref rightPlanetoid);

            ref float leftPlanetoidZPosition = ref leftPlanetoid.As<StolenPlanetoid>().ZPosition;
            ref float rightPlanetoidZPosition = ref rightPlanetoid.As<StolenPlanetoid>().ZPosition;

            // Calculate the descent interpolant.
            float descendInterpolant = InverseLerp(0f, WorldSmash_PlanetoidDescentTime, AITimer - enterBackgroundTime);

            // Make the arms move.
            leftArmDestination.X += descendInterpolant * 50f;
            rightArmDestination.X -= descendInterpolant * 100f;
            leftArmDestination.Y -= EasingCurves.Quartic.Evaluate(EasingType.InOut, descendInterpolant) * 200f;
            rightArmDestination.Y += EasingCurves.Quartic.Evaluate(EasingType.InOut, descendInterpolant) * 350f;

            // Hold both planetoids in place.
            leftPlanetoidZPosition = 4.1f;
            rightPlanetoidZPosition = 4.1f;
            leftPlanetoid.Center = Target.Center + new Vector2(-550f, -340f - (1f - descendInterpolant).Squared() * 1105f);
            rightPlanetoid.Center = Target.Center + new Vector2(550f, -340f - (1f - descendInterpolant).Squared() * 1105f);

            // Decide the direction in which the planetoids fly off.
            if (AITimer == WorldSmash_PlanetoidDescentTime + 4)
            {
                Vector2 aimDirection = Target.Velocity.SafeNormalize(Main.rand.NextVector2Unit()).RotatedByRandom(0.5f);

                // Ensure that planetoids do not fly over the player at first before coming back.
                aimDirection.X = Abs(aimDirection.X);

                // Store the fly direction.
                WorldSmash_PlanetoidFlyOffDirection = aimDirection.ToRotation();

                NPC.netUpdate = true;
            }

            // Send the planets in opposite directions.
            float flyOffset = Pow(flyOffInterpolant, 7f) * 2500f;
            leftPlanetoid.Center -= WorldSmash_PlanetoidFlyOffDirection.ToRotationVector2() * flyOffset;
            rightPlanetoid.Center += WorldSmash_PlanetoidFlyOffDirection.ToRotationVector2() * flyOffset;
            leftPlanetoidZPosition = (1f - flyOffInterpolant) * planetStartingZPosition;
            rightPlanetoidZPosition = (1f - flyOffInterpolant) * planetStartingZPosition;

            // Make the Avatar's arms move horizontally.
            float horizontalArmStretchInterpolant = EasingCurves.Cubic.Evaluate(EasingType.InOut, InverseLerp(0.25f, 0.46f, flyOffInterpolant));
            leftArmDestination = Vector2.Lerp(leftArmDestination, NPC.Center + new Vector2(-870f, 325f), horizontalArmStretchInterpolant);
            rightArmDestination = Vector2.Lerp(rightArmDestination, NPC.Center + new Vector2(870f, 325f), horizontalArmStretchInterpolant);
        }

        // Send planetoids flying towards each other.
        if (planetoids.Count >= 2 && flyOffInterpolant >= 1f && Main.netMode != NetmodeID.MultiplayerClient)
        {
            Projectile leftPlanetoid = planetoids[0];
            Projectile rightPlanetoid = planetoids[1];
            Vector2 centerPoint = (leftPlanetoid.Center + rightPlanetoid.Center) * 0.5f;
            if (leftPlanetoid.velocity == Vector2.Zero || rightPlanetoid.velocity == Vector2.Zero)
            {
                // Send planetoids to the foreground.
                leftPlanetoid.As<StolenPlanetoid>().ZPosition = 0f;
                rightPlanetoid.As<StolenPlanetoid>().ZPosition = 0f;

                // Update planetoid velocities.
                leftPlanetoid.velocity = leftPlanetoid.SafeDirectionTo(centerPoint) * WorldSmash_PlanetoidFlingSpeed;
                rightPlanetoid.velocity = rightPlanetoid.SafeDirectionTo(centerPoint) * WorldSmash_PlanetoidFlingSpeed;

                // Sync both planetoids.
                leftPlanetoid.netUpdate = true;
                rightPlanetoid.netUpdate = true;
            }
        }

        if (Main.netMode == NetmodeID.Server && AITimer % 30 == 0)
        {
            foreach (Projectile planetoid in planetoids)
                planetoid.netUpdate = true;
        }

        // Bring arms together as the planetoids begin colliding.
        float handBringTogetherInterpolant = InverseLerp(75f, 0f, AITimer - enterBackgroundTime - WorldSmash_PlanetoidDescentTime - WorldSmash_FlyOffTime);
        if (flyOffInterpolant >= 1f)
        {
            leftArmDestination = NPC.Center + new Vector2(-150f - handBringTogetherInterpolant * 500f, 600f);
            rightArmDestination = NPC.Center + new Vector2(150f + handBringTogetherInterpolant * 500f, 600f);
        }

        // Prepare to smash the next planet set once ready.
        if (Main.netMode != NetmodeID.MultiplayerClient && planetoids.Count == 0 && AITimer > enterBackgroundTime + 5 && !spawnedPlanetoidsThisFrame)
        {
            AITimer = enterBackgroundTime - 1;
            WorldSmash_PlanetoidSummonCounter++;
            if (WorldSmash_PlanetoidSummonCounter >= WorldSmash_PlanetoidSmashCount)
                AITimer = 0;

            NPC.netUpdate = true;
        }

        // Update limbs.
        HeadPosition = Vector2.Lerp(HeadPosition, NPC.Center + new Vector2(2f, headVerticalOffset) * HeadScale * NeckAppearInterpolant, 0.14f);
        LeftArmPosition = Vector2.Lerp(LeftArmPosition, leftArmDestination, 0.16f);
        RightArmPosition = Vector2.Lerp(RightArmPosition, rightArmDestination, 0.16f);
    }
}
