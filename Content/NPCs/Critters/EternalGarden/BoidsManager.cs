using Microsoft.Xna.Framework;
using NoxusBoss.Core.DataStructures;
using Terraria;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Critters.EternalGarden;

public class BoidsManager : ModSystem
{
    public delegate Vector2 BoidForceApplicationRule(List<IBoid> boids, Vector2 center, Vector2 previousVelocity);

    public static BoidForceApplicationRule CreateAlignmentRule(float matchingInterpolant)
    {
        return new((boids, center, velocity) =>
        {
            if (boids.Count == 0)
                return velocity;

            // Calculate the average velocity of the flock.
            Vector2 averageVelocity = Vector2.Zero;
            for (int i = 0; i < boids.Count; i++)
                averageVelocity += boids[i].BoidVelocity;
            averageVelocity /= boids.Count;

            // Interpolate towards the average velocity to ensure the flock has a consistent direction.
            return Vector2.Lerp(velocity, averageVelocity, matchingInterpolant);
        });
    }

    public static BoidForceApplicationRule CreateCohesionRule(float centeringForceFactor)
    {
        return new((boids, center, velocity) =>
        {
            if (boids.Count == 0)
                return velocity;

            // Calculate the center of mass.
            Vector2 centerOfMass = Vector2.Zero;
            for (int i = 0; i < boids.Count; i++)
                centerOfMass += boids[i].BoidCenter;
            centerOfMass /= boids.Count;

            // Approach the center of mass.
            return velocity + (centerOfMass - center) * centeringForceFactor;
        });
    }

    public static BoidForceApplicationRule CreateSeparationRule(float socialDistancingRange, float socialDistancingFactor)
    {
        return new((boids, center, velocity) =>
        {
            if (boids.Count == 0)
                return velocity;

            // Calculate the separation force.
            Vector2 separationForce = Vector2.Zero;
            for (int i = 0; i < boids.Count; i++)
            {
                if (!center.WithinRange(boids[i].BoidCenter, socialDistancingRange))
                    continue;

                float distanceToBoid = center.Distance(boids[i].BoidCenter);
                separationForce -= (boids[i].BoidCenter - center).SafeNormalize(Vector2.UnitY) * (socialDistancingRange - distanceToBoid);
            }

            // Move away from other boids.
            return velocity + separationForce * socialDistancingFactor;
        });
    }

    public static BoidForceApplicationRule AvoidGroundRule(float height, float liftAcceleration)
    {
        return new((boids, center, velocity) =>
        {
            if (!Collision.CanHit(center, 1, 1, center + Vector2.UnitY * height, 1, 1))
                velocity.Y -= liftAcceleration;

            return velocity;
        });
    }

    public static BoidForceApplicationRule StayNearGroundRule(float height, float descendAcceleration)
    {
        return new((boids, center, velocity) =>
        {
            if (Collision.CanHit(center, 1, 1, center + Vector2.UnitY * height, 1, 1))
                velocity.Y += descendAcceleration;

            return velocity;
        });
    }

    public static BoidForceApplicationRule ClampVelocityRule(float maxSpeed)
    {
        return new((boids, center, velocity) =>
        {
            if (velocity.Length() > maxSpeed)
                velocity *= 0.96f;

            return velocity;
        });
    }

    public override void PreUpdateEntities()
    {
        // Get all entities that are considered boids.
        List<IBoid> boids = [];
        for (int i = 0; i < Main.maxNPCs; i++)
        {
            NPC n = Main.npc[i];

            // Ignore NPCs that are not active boids.
            if (!n.active || n.ModNPC is null || n.ModNPC is not IBoid boid || !boid.CurrentlyUsingBoidBehavior)
                continue;

            // Add boid NPCs to the list.
            boids.Add(boid);
        }
        foreach (Projectile p in Main.ActiveProjectiles)
        {
            // Ignore projectiles that are not active boids.
            if (p.ModProjectile is null || p.ModProjectile is not IBoid boid || !boid.CurrentlyUsingBoidBehavior)
                continue;

            // Add boid projectiles to the list.
            boids.Add(boid);
        }

        // Group boids based on ID.
        IEnumerable<IGrouping<int, IBoid>> boidGroups = boids.GroupBy(b => b.GroupID);
        foreach (IGrouping<int, IBoid> boidGroup in boidGroups)
            UpdateBoidGroup(boidGroup.ToList());
    }

    public static void UpdateBoidGroup(List<IBoid> boids)
    {
        // Get simulation rules.
        List<BoidForceApplicationRule> simulationRules = boids[0].SimulationRules;

        // Store boids in an efficient quad tree data structure.
        QuadTree<IBoid> boidsOrganized = new QuadTree<IBoid>(0, new(-10000, -10000, Main.maxTilesX * 16 + 20000, Main.maxTilesY * 16 + 20000));
        for (int i = 0; i < boids.Count; i++)
            boidsOrganized.Insert(boids[i], boids[i].BoidArea);

        for (int i = 0; i < boids.Count; i++)
        {
            // Get all nearby boids.
            List<IBoid> nearbyBoids = boidsOrganized.GetAllInRange(Utils.CenteredRectangle(boids[i].BoidCenter, Vector2.One * boids[i].FlockmateDetectionRange));
            nearbyBoids.RemoveAll(b => b == boids[i] || !b.BoidCenter.WithinRange(boids[i].BoidCenter, boids[i].FlockmateDetectionRange));

            // Apply simulation rules.
            for (int j = 0; j < simulationRules.Count; j++)
                boids[i].BoidVelocity = simulationRules[j](nearbyBoids, boids[i].BoidCenter, boids[i].BoidVelocity);
        }
    }
}
