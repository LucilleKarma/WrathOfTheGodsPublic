using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Core.Graphics.TentInterior;

public class ConstellationsRegistry : ModSystem
{
    /// <summary>
    /// The set of all defined constellations.
    /// </summary>
    public static readonly Dictionary<string, Constellation> Constellations = [];

    public override void OnModLoad()
    {
        if (Main.netMode == NetmodeID.Server)
            return;

        Type sourceHolderType = typeof(GennedAssets.Textures.Constellations);
        foreach (FieldInfo field in sourceHolderType.GetFields())
        {
            object? fieldValue = field.GetValue(null);
            if (fieldValue is null)
                continue;

            LazyAsset<Texture2D> asset = (LazyAsset<Texture2D>)fieldValue;

            // Wait for asynchronous loading to complete.
            asset.Asset.Wait();

            Texture2D constellationTexture = asset.Asset.Value;

            // Acquire color data from the texture on the main thread.
            Color[] colorData = new Color[constellationTexture.Width * constellationTexture.Height];
            Main.RunOnMainThread(() => constellationTexture.GetData(colorData)).Wait();

            LoadConstellation(field.Name, colorData, constellationTexture.Width, constellationTexture.Height);
        }
    }

    private static Color SafeAccess(Color[,] image, Point p)
    {
        int x = p.X;
        int y = p.Y;
        if (x < 0 || x >= image.GetLength(0) || y < 0 || y >= image.GetLength(1))
            return default;

        return image[x, y];
    }

    private static LineSegment FindLineSegment(Point startingPoint, Color[,] image, List<Point> visitedLineSegmentCandidates)
    {
        Stack<Point> stack = new Stack<Point>(128);
        stack.Push(startingPoint);

        List<Point> segmentPoints = new List<Point>(128)
        {
            startingPoint
        };

        while (stack.Count >= 1)
        {
            Point checkPoint = stack.Pop();
            visitedLineSegmentCandidates.Add(checkPoint);

            // Explore points in eight-way directions to determine if they have any green pixels to them.
            // If they do, that means that it's a part of the line segment.
            for (int i = -1; i <= 1; i++)
            {
                for (int j = -1; j <= 1; j++)
                {
                    // Ignore the center, that's just the current check point.
                    if (i == 0 && j == 0)
                        continue;

                    // Don't check the same point twice. That'll just result in an infinite loop as the stack evaluates the same two points back and forth forever.
                    Point offsetPoint = new Point(checkPoint.X + i, checkPoint.Y + j);
                    if (!visitedLineSegmentCandidates.Contains(offsetPoint))
                        visitedLineSegmentCandidates.Add(offsetPoint);
                    else
                        continue;

                    // Check if the point has any green in it.
                    // If it does, that means it's a part of a line segment, and should be considered.
                    if (SafeAccess(image, offsetPoint).G >= 1)
                    {
                        stack.Push(offsetPoint);
                        segmentPoints.Add(offsetPoint);
                    }
                }
            }
        }

        if (segmentPoints.Count <= 3)
            return default;

        // Evaluate the segment points, starting by calculating the center of mass of all the points.
        Vector2 centerOfMass = Vector2.Zero;
        for (int i = 0; i < segmentPoints.Count; i++)
            centerOfMass += segmentPoints[i].ToVector2();
        centerOfMass /= segmentPoints.Count;

        IOrderedEnumerable<Point> orderedSegmentPoints = segmentPoints.OrderBy(s => s.ToVector2().Distance(centerOfMass));
        Point closestToCenterOfMass = orderedSegmentPoints.Where(s => !s.ToVector2().WithinRange(centerOfMass, 1f)).First();

        // Now, evaluate each point along the line to construct the vectorized line segment.
        Point furthestPointA = closestToCenterOfMass;
        Point furthestPointB = closestToCenterOfMass;
        Point startingPointA = furthestPointA;
        Vector2 startingPointAOffsetFromCenter = startingPointA.ToVector2() - centerOfMass;
        foreach (Point point in orderedSegmentPoints)
        {
            Vector2 offsetFromCenter = point.ToVector2() - centerOfMass;

            // Determine whether the current point if opposite from furthestPointA.
            // If it is, that means this point is in the other direction relative to the center of mass, and furthestPointB should be set instead.
            // If isn't, then it's in the same direction as furthestPointA, and as such furthestPointA should be updated to match.
            // This will create two points that iteratively get further and further away from each other, until eventually reaching the end points of the line.
            float distanceFromCenter = point.ToVector2().Distance(centerOfMass);
            bool oppositeToSideA = Vector2.Dot(offsetFromCenter, startingPointAOffsetFromCenter) < 0f;
            if (oppositeToSideA && distanceFromCenter > furthestPointB.ToVector2().Distance(centerOfMass))
                furthestPointB = point;
            else if (distanceFromCenter > furthestPointA.ToVector2().Distance(centerOfMass))
                furthestPointA = point;
        }

        return new LineSegment(furthestPointA.ToVector2(), furthestPointB.ToVector2());
    }

    internal static void LoadConstellation(string name, Color[] colorData, int width, int height)
    {
        // Create a 2D reconstruction of the 1D color data array, for ease of access in successive calculations.
        Color[,] image = new Color[width, height];
        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < height; j++)
                image[i, j] = colorData[i + width * j];
        }

        List<Point> visitedLineSegmentCandidates = new List<Point>(256);
        List<Vector2> starPoints = new List<Vector2>();
        List<LineSegment> lines = new List<LineSegment>(16);

        // Calculate line segment and star points.
        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < height; j++)
            {
                Point point = new Point(i, j);

                if (SafeAccess(image, point).R >= 1)
                    starPoints.Add(point.ToVector2());

                // Ignore this point if it's already been considered for an existing line segment.
                if (visitedLineSegmentCandidates.Contains(point))
                    continue;

                // Ignore this point if it isn't part of a line segment.
                if (SafeAccess(image, point).G <= 0)
                    continue;

                LineSegment line = FindLineSegment(point, image, visitedLineSegmentCandidates);
                if (line.Start != Vector2.Zero && line.End != Vector2.Zero)
                    lines.Add(line);
            }
        }

        Constellations[name] = new Constellation(new Vector2(width, height), starPoints, lines);
    }
}
