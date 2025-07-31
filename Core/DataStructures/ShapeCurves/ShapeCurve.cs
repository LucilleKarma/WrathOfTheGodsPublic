using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;

namespace NoxusBoss.Core.DataStructures.ShapeCurves;

public class ShapeCurve
{
    public Vector2 Center;

    public List<Vector2> ShapePoints;

    public ShapeCurve()
    {
        ShapePoints = [];
        Center = Vector2.Zero;
    }

    public ShapeCurve(List<Vector2> shapePoints)
    {
        ShapePoints = shapePoints;

        Center = Vector2.Zero;
        for (int i = 0; i < ShapePoints.Count; i++)
            Center += ShapePoints[i];
        Center /= ShapePoints.Count;
    }

    public ShapeCurve Upscale(float upscaleFactor)
    {
        List<Vector2> upscaledPoints = [];
        float maxX = ShapePoints.Max(p => p.X);
        for (int i = 0; i < ShapePoints.Count; i++)
            upscaledPoints.Add((ShapePoints[i] - Vector2.UnitY * 0.5f) * upscaleFactor + Vector2.UnitY * 0.5f + Vector2.UnitX * -maxX * upscaleFactor * 0.5f);

        return new(upscaledPoints);
    }

    public ShapeCurve Rotate(float angle)
    {
        List<Vector2> rotatedPoints = [];
        for (int i = 0; i < ShapePoints.Count; i++)
            rotatedPoints.Add(ShapePoints[i].RotatedBy(angle, Center));

        return new(rotatedPoints);
    }

    public ShapeCurve LinearlyTransform(Matrix transformation)
    {
        List<Vector2> transformedPoints = [];
        for (int i = 0; i < ShapePoints.Count; i++)
            transformedPoints.Add(Vector2.Transform(ShapePoints[i], transformation));

        return new(transformedPoints);
    }

    public ShapeCurve VerticalFlip()
    {
        List<Vector2> rotatedPoints = [];
        float maxY = ShapePoints.Max(p => p.Y);

        for (int i = 0; i < ShapePoints.Count; i++)
            rotatedPoints.Add(new(ShapePoints[i].X, maxY - ShapePoints[i].Y));

        return new(rotatedPoints);
    }

    public bool Intersects(Vector2 shapeOffset, Rectangle aabb)
    {
        Rectangle polygonBounds = GetPolygonBounds(shapeOffset);

        if (!polygonBounds.Intersects(aabb))
            return false;

        foreach (LineSegment edge in GetPolygonEdges(shapeOffset))
        {
            if (LineIntersectsRectangle(edge, aabb))
                return true;
        }

        return PointInsidePolygon(aabb.TopLeft(), shapeOffset);
    }

    private Rectangle GetPolygonBounds(Vector2 shapeOffset)
    {
        int minX = int.MaxValue;
        int minY = int.MaxValue;
        int maxX = int.MinValue;
        int maxY = int.MinValue;

        foreach (Vector2 point in ShapePoints)
        {
            minX = Math.Min(minX, (int)(point.X + shapeOffset.X));
            minY = Math.Min(minY, (int)(point.Y + shapeOffset.Y));
            maxX = Math.Max(maxX, (int)(point.X + shapeOffset.X));
            maxY = Math.Max(maxY, (int)(point.Y + shapeOffset.Y));
        }

        return new(minX, minY, maxX - minX, maxY - minY);
    }

    private IEnumerable<LineSegment> GetPolygonEdges(Vector2 shapeOffset)
    {
        int pointCount = ShapePoints.Count;

        for (int i = 0; i < pointCount; i++)
        {
            Vector2 start = ShapePoints[i] + shapeOffset;
            Vector2 end = ShapePoints[(i + 1) % pointCount] + shapeOffset;
            yield return new LineSegment(start, end);
        }
    }

    private static bool LineIntersectsRectangle(LineSegment line, Rectangle aabb)
    {
        if (aabb.Intersects(Utils.CenteredRectangle(line.Start, Vector2.One)) || aabb.Intersects(Utils.CenteredRectangle(line.End, Vector2.One)))
            return true;

        if (line.Start.X < aabb.Left && line.End.X < aabb.Left)
            return false;

        if (line.Start.X > aabb.Right && line.End.X > aabb.Right)
            return false;

        if (line.Start.Y < aabb.Top && line.End.Y < aabb.Top)
            return false;

        if (line.Start.Y > aabb.Bottom && line.End.Y > aabb.Bottom)
            return false;

        return LineIntersectsLine(line, new LineSegment(aabb.TopLeft(), aabb.TopRight())) ||
               LineIntersectsLine(line, new LineSegment(aabb.TopRight(), aabb.TopRight())) ||
               LineIntersectsLine(line, new LineSegment(aabb.TopRight(), aabb.BottomLeft())) ||
               LineIntersectsLine(line, new LineSegment(aabb.BottomLeft(), aabb.TopLeft()));
    }

    private static bool LineIntersectsLine(LineSegment line1, LineSegment line2)
    {
        float denominator = (line2.End.Y - line2.Start.Y) * (line1.End.X - line1.Start.X) -
                            (line2.End.X - line2.Start.X) * (line1.End.Y - line1.Start.Y);

        if (denominator == 0)
            return false;

        float numerator1 = (line2.End.X - line2.Start.X) * (line1.Start.Y - line2.Start.Y) -
                           (line2.End.Y - line2.Start.Y) * (line1.Start.X - line2.Start.X);

        float numerator2 = (line1.End.X - line1.Start.X) * (line1.Start.Y - line2.Start.Y) -
                           (line1.End.Y - line1.Start.Y) * (line1.Start.X - line2.Start.X);

        float t1 = numerator1 / denominator;
        float t2 = numerator2 / denominator;

        return t1 >= 0 && t1 <= 1 && t2 >= 0 && t2 <= 1;
    }

    private bool PointInsidePolygon(Vector2 point, Vector2 shapeOffset)
    {
        int pointCount = ShapePoints.Count;
        int i, j = pointCount - 1;
        bool inside = false;

        for (i = 0; i < pointCount; i++)
        {
            Vector2 p1 = ShapePoints[i] + shapeOffset;
            Vector2 p2 = ShapePoints[j] + shapeOffset;
            if (p1.Y > point.Y != p2.Y > point.Y && point.X < (p2.X - p1.X) * (point.Y - p1.Y) / (p2.Y - p1.Y) + p1.X)
                inside = !inside;

            j = i;
        }

        return inside;
    }
}
