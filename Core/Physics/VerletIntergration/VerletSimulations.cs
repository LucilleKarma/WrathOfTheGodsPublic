using Microsoft.Xna.Framework;
using Terraria;

namespace NoxusBoss.Core.Physics.VerletIntergration;

/// <summary>
/// Contains various simulations for verlet chains.
/// </summary>
public static class VerletSimulations
{
    public static List<VerletSimulatedSegment> StandardVerletSimulation(List<VerletSimulatedSegment> segments, float segmentDistance, Vector2 externalForce, int loops = 10, float gravity = 0.3f)
    {
        for (int i = segments.Count - 1; i >= 0; i--)
        {
            VerletSimulatedSegment segment = segments[i];
            if (!segment.Locked)
            {
                Vector2 positionBeforeUpdate = segment.Position;
                Vector2 gravityForce = Vector2.UnitY * gravity;
                segment.Position += (segment.Position - segment.OldPosition) * 0.5f;
                segment.Position += gravityForce + externalForce;

                segment.OldPosition = positionBeforeUpdate;
            }
        }

        int segmentCount = segments.Count;

        for (int k = 0; k < loops; k++)
        {
            for (int j = 0; j < segmentCount - 1; j++)
            {
                VerletSimulatedSegment pointA = segments[j];
                VerletSimulatedSegment pointB = segments[j + 1];
                Vector2 segmentCenter = (pointA.Position + pointB.Position) / 2f;
                Vector2 segmentDirection = (pointA.Position - pointB.Position).SafeNormalize(Vector2.UnitY);

                if (!pointA.Locked)
                    pointA.Position = segmentCenter + segmentDirection * segmentDistance / 2f;

                if (!pointB.Locked)
                    pointB.Position = segmentCenter - segmentDirection * segmentDistance / 2f;

                segments[j] = pointA;
                segments[j + 1] = pointB;
            }
        }

        return segments;
    }

    public static List<VerletSimulatedSegment> TileCollisionVerletSimulation(List<VerletSimulatedSegment> segments, float segmentDistance, Vector2 externalForce, int loops = 10, float gravity = 0.3f)
    {
        // https://youtu.be/PGk0rnyTa1U?t=400 is a good verlet integration chains reference.
        List<int> groundHitSegments = [];
        for (int i = segments.Count - 1; i >= 0; i--)
        {
            VerletSimulatedSegment segment = segments[i];
            if (!segment.Locked)
            {
                Vector2 positionBeforeUpdate = segment.Position;

                Vector2 gravityForce = Vector2.UnitY * gravity;

                // Add gravity to the segment.
                Vector2 velocity = segment.Velocity + gravityForce;
                velocity.Y = Clamp(velocity.Y, -23f, 23f);
                velocity += externalForce;

                // This adds conservation of energy to the segments. This makes it super bouncy and shouldn't be used but it's really funny.
                segment.Position += (segment.Position - segment.OldPosition) * -0.03f;

                segment.Position += velocity;
                segment.Velocity = velocity;
                segment.Velocity.X *= 0.94f;
                segment.Position.X = Lerp(segment.Position.X, segments[0].Position.X, 0.04f);

                segment.OldPosition = positionBeforeUpdate;
            }
        }

        int segmentCount = segments.Count;

        for (int k = 0; k < loops; k++)
        {
            for (int j = 0; j < segmentCount - 1; j++)
            {
                VerletSimulatedSegment pointA = segments[j];
                VerletSimulatedSegment pointB = segments[j + 1];
                Vector2 segmentCenter = (pointA.Position + pointB.Position) / 2f;
                Vector2 segmentDirection = (pointA.Position - pointB.Position).SafeNormalize(Vector2.UnitY);

                if (!pointA.Locked && !groundHitSegments.Contains(j))
                    pointA.Position = segmentCenter + segmentDirection * segmentDistance / 2f;

                if (!pointB.Locked && !groundHitSegments.Contains(j + 1))
                    pointB.Position = segmentCenter - segmentDirection * segmentDistance / 2f;

                segments[j] = pointA;
                segments[j + 1] = pointB;
            }
        }

        return segments;
    }
}
