using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace NoxusBoss.Content.NPCs.Bosses.Avatar.FirstPhaseForm;

public class AvatarRiftLiquidInfo
{
    public class LiquidPoint(Vector2 position, Vector2 velocity)
    {
        public Vector2 Velocity = velocity;

        public Vector2 Position = position;

        public float Rotation;

        public void Update(float gravity)
        {
            Velocity.Y += gravity;
            Position += Velocity;
            Rotation = Velocity.ToRotation();
        }
    }

    /// <summary>
    /// The upper limit to the amount of entries <see cref="LiquidPoints"/> can contain.
    /// </summary>
    public readonly int MaxLiquidPoints;

    /// <summary>
    /// The gravity of the liquid.
    /// </summary>
    public readonly float Gravity;

    /// <summary>
    /// The collection of liquid points.
    /// </summary>
    public readonly List<LiquidPoint> LiquidPoints = [];

    /// <summary>
    /// The width function of the liquid.
    /// </summary>
    public readonly PrimitiveSettings.VertexWidthFunction WidthFunction;

    /// <summary>
    /// The size of the liquid render target.
    /// </summary>
    public static readonly Vector2 Size = new Vector2(784, 1024);

    public AvatarRiftLiquidInfo(int maxLiquidPoints, float gravity, PrimitiveSettings.VertexWidthFunction widthFunction)
    {
        MaxLiquidPoints = maxLiquidPoints;
        Gravity = gravity;
        WidthFunction = widthFunction;
    }

    /// <summary>
    /// Updates the <see cref="LiquidPoints"/> collection.
    /// </summary>
    /// <param name="liquidSource">The source of the latest liquid point.</param>
    public void UpdateLiquid(Vector2 liquidSource)
    {
        LiquidPoints.Add(new(liquidSource, Vector2.UnitY * Gravity));

        // Prune the oldest liquid point if the point count has been reached.
        if (LiquidPoints.Count > MaxLiquidPoints)
            LiquidPoints.RemoveAt(0);

        // Update all liquids.
        for (int i = 0; i < LiquidPoints.Count; i++)
            LiquidPoints[i].Update(Gravity);
    }

    public void DrawLiquid(Vector2 generalOffset)
    {
        ManagedShader trailShader = ShaderManager.GetShader("NoxusBoss.AvatarRiftLiquidTrail");
        trailShader.SetTexture(SwirlNoise, 1, SamplerState.LinearWrap);

        PrimitiveSettings settings = new PrimitiveSettings(WidthFunction, LiquidInfoColorFunction, _ => generalOffset, Shader: trailShader, ProjectionAreaWidth: (int)AvatarRiftTargetContent.Size.X, ProjectionAreaHeight: (int)AvatarRiftTargetContent.Size.Y, UseUnscaledMatrix: true);
        PrimitiveRenderer.RenderTrail(LiquidPoints.Select(p => p.Position).ToList(), settings, 20);
    }

    public static Color LiquidInfoColorFunction(float completionRatio)
    {
        float scrollSpeed = InverseLerp(1f, 0.2f, completionRatio) * 0.7f;
        return new(0f, 0f, scrollSpeed, InverseLerp(0f, 0.3f, completionRatio));
    }
}
